#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env
set_backend_defaults

if [[ "${1:-}" == "--dry-run" ]]; then
  print_backend_summary
  echo "Command: dotnet run --project ${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj"
  exit 0
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
