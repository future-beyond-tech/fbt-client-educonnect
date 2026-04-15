#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env

# ── Args: --db <docker|local|remote>, --dry-run, --no-db-autostart ────
DRY_RUN=false
AUTO_START_DB=true
DB_MODE_CLI=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --)                 shift ;;
    --db)               DB_MODE_CLI="$2"; shift 2 ;;
    --db=*)             DB_MODE_CLI="${1#*=}"; shift ;;
    --no-db-autostart)  AUTO_START_DB=false; shift ;;
    --dry-run)          DRY_RUN=true; shift ;;
    -h|--help)
      cat <<'USAGE'
Usage: scripts/run-backend-local.sh [options]

Options:
  --db docker|local|remote   Choose Postgres backend (default: docker)
  --no-db-autostart          Don't auto-start the docker Postgres container
  --dry-run                  Print resolved env and exit
  -h, --help                 Show this help
USAGE
      exit 0
      ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

# An explicit --db flag overrides anything loaded from .env. Clear inherited
# DB env vars so resolve_db_mode rebuilds them from the CLI-chosen mode.
if [[ -n "${DB_MODE_CLI}" ]]; then
  export EDUCONNECT_DB_MODE="${DB_MODE_CLI}"
  unset DATABASE_URL POSTGRES_HOST_PORT
fi

set_backend_defaults

if [[ "${DRY_RUN}" == "true" ]]; then
  print_backend_summary
  echo "Command: dotnet run --project ${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj"
  exit 0
fi

# In docker mode, transparently bring up the Postgres container so the user
# never has to remember `docker compose up db -d`. The .NET API will then
# auto-apply EF Core migrations and the development seed scripts on startup.
if [[ "${EDUCONNECT_DB_MODE}" == "docker" && "${AUTO_START_DB}" == "true" ]]; then
  if command -v docker >/dev/null 2>&1; then
    echo "Ensuring docker Postgres is up..."
    "${REPO_ROOT}/scripts/db.sh" --db docker up
  else
    echo "WARNING: docker not found on PATH; skipping auto-start. Set DB_MODE=local or install Docker." >&2
  fi
fi

print_backend_summary
HEALTHCHECK_URL="http://localhost:${API_PORT}/health"
POLL_INTERVAL_SECONDS=2
MAX_POLLS=60
db_not_ready_notified="false"

echo "Starting backend API..."
dotnet run --project "${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj" &
API_PID=$!

cleanup() {
  if kill -0 "${API_PID}" 2>/dev/null; then
    kill "${API_PID}" 2>/dev/null || true
  fi
}

trap cleanup INT TERM EXIT

for _ in $(seq 1 "${MAX_POLLS}"); do
  if ! kill -0 "${API_PID}" 2>/dev/null; then
    wait "${API_PID}"
    EXIT_CODE=$?
    trap - INT TERM EXIT
    exit "${EXIT_CODE}"
  fi

  HTTP_STATUS="$(curl -s -o /dev/null -w "%{http_code}" "${HEALTHCHECK_URL}" || true)"

  if [[ "${HTTP_STATUS}" == "200" ]]; then
    echo "Backend server started on ${ASPNETCORE_URLS} (health: ${HEALTHCHECK_URL})"
    wait "${API_PID}"
    EXIT_CODE=$?
    trap - INT TERM EXIT
    exit "${EXIT_CODE}"
  fi

  if [[ "${HTTP_STATUS}" == "503" && "${db_not_ready_notified}" != "true" ]]; then
    echo "Backend API is running, but the database is not reachable yet."
    echo "Hint: start Postgres with 'docker compose up db -d' (Docker uses host port ${POSTGRES_HOST_PORT}) or verify DATABASE_URL."
    db_not_ready_notified="true"
  fi

  sleep "${POLL_INTERVAL_SECONDS}"
done

echo "Backend process is running, but ${HEALTHCHECK_URL} did not become ready within $((POLL_INTERVAL_SECONDS * MAX_POLLS)) seconds."
wait "${API_PID}"
