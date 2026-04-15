#!/usr/bin/env bash
#
# EduConnect database lifecycle helper.
#
# Manages the Postgres backing the API in any of three modes:
#   docker  → docker compose service `db` (default)
#   local   → native Postgres on localhost (homebrew / postgres.app / apt)
#   remote  → uses DATABASE_URL from .env (no lifecycle commands available)
#
# Usage:
#   scripts/db.sh up        Start the database (docker mode only)
#   scripts/db.sh down      Stop the database (docker mode only)
#   scripts/db.sh reset     Wipe and recreate (docker: drop volume, local: drop+create db)
#   scripts/db.sh status    Show whether the DB is reachable
#   scripts/db.sh psql      Open a psql shell against the configured DB
#   scripts/db.sh url       Print the resolved DATABASE_URL
#
# Mode is selected via EDUCONNECT_DB_MODE / DB_MODE env var, or --db <mode>.
#
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env

# ── Parse args ────────────────────────────────────────────────────────
COMMAND=""
DB_MODE_CLI=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --db)
      DB_MODE_CLI="$2"
      shift 2
      ;;
    --db=*)
      DB_MODE_CLI="${1#*=}"
      shift
      ;;
    up|down|reset|status|psql|url)
      COMMAND="$1"
      shift
      ;;
    -h|--help|help)
      sed -n '3,20p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ -z "${COMMAND}" ]]; then
  echo "Usage: scripts/db.sh [--db docker|local|remote] <up|down|reset|status|psql|url>" >&2
  exit 1
fi

# Explicit --db wins over .env-loaded values.
if [[ -n "${DB_MODE_CLI}" ]]; then
  export EDUCONNECT_DB_MODE="${DB_MODE_CLI}"
  unset DATABASE_URL POSTGRES_HOST_PORT
fi

set_backend_defaults

# ── Helpers ───────────────────────────────────────────────────────────
require_docker() {
  if ! command -v docker >/dev/null 2>&1; then
    echo "ERROR: docker is not installed or not on PATH." >&2
    exit 1
  fi
}

require_psql() {
  if ! command -v psql >/dev/null 2>&1; then
    echo "ERROR: psql is not installed. Install postgresql client (e.g. 'brew install libpq')." >&2
    exit 1
  fi
}

probe_db() {
  # Returns 0 if reachable, 1 otherwise. Prefer libpq-aware tools so URI and
  # key/value connection strings both work, including remote mode.
  if command -v pg_isready >/dev/null 2>&1; then
    pg_isready -d "${DATABASE_URL}" -t 1 >/dev/null 2>&1 && return 0
  fi

  if command -v psql >/dev/null 2>&1; then
    PSQLRC=/dev/null psql "${DATABASE_URL}" -v ON_ERROR_STOP=1 -qAt -c 'SELECT 1;' >/dev/null 2>&1 && return 0
  fi

  if [[ "${EDUCONNECT_DB_MODE}" == "remote" ]]; then
    return 1
  fi

  (echo > "/dev/tcp/localhost/${POSTGRES_HOST_PORT}") >/dev/null 2>&1
}

# ── Commands ──────────────────────────────────────────────────────────
cmd_up() {
  case "${EDUCONNECT_DB_MODE}" in
    docker)
      require_docker
      echo "Starting Postgres container (host port ${POSTGRES_HOST_PORT})..."
      (cd "${REPO_ROOT}" && POSTGRES_HOST_PORT="${POSTGRES_HOST_PORT}" docker compose up -d db)
      echo "Waiting for Postgres to accept connections..."
      for i in {1..30}; do
        if probe_db; then
          echo "Postgres is ready at localhost:${POSTGRES_HOST_PORT}."
          return 0
        fi
        sleep 1
      done
      echo "Postgres did not become ready within 30s." >&2
      exit 1
      ;;
    local)
      echo "Mode 'local' assumes Postgres is already running natively on localhost:${POSTGRES_HOST_PORT}."
      if probe_db; then
        echo "Postgres is reachable."
      else
        echo "Postgres is NOT reachable. Start it with 'brew services start postgresql' or your OS equivalent." >&2
        exit 1
      fi
      ;;
    remote)
      echo "Mode 'remote' uses DATABASE_URL=${DATABASE_URL}. Lifecycle is managed externally." >&2
      ;;
  esac
}

cmd_down() {
  case "${EDUCONNECT_DB_MODE}" in
    docker)
      require_docker
      (cd "${REPO_ROOT}" && docker compose stop db)
      echo "Postgres container stopped."
      ;;
    local)
      echo "Mode 'local': stop Postgres via your OS service manager (brew services stop postgresql)." >&2
      ;;
    remote)
      echo "Mode 'remote': nothing to stop." >&2
      ;;
  esac
}

cmd_reset() {
  case "${EDUCONNECT_DB_MODE}" in
    docker)
      require_docker
      echo "Wiping Docker Postgres volume..."
      (cd "${REPO_ROOT}" && docker compose down db -v)
      cmd_up
      echo "Done. On next Development startup, the API will re-apply EF Core migrations and re-run the dev seed scripts."
      ;;
    local)
      require_psql
      echo "Dropping and recreating database '${LOCAL_DB_NAME:-educonnect}' on localhost:${POSTGRES_HOST_PORT}..."
      PGPASSWORD="${LOCAL_DB_PASSWORD:-educonnect_dev}" psql \
        -h localhost -p "${POSTGRES_HOST_PORT}" \
        -U "${LOCAL_DB_USER:-educonnect}" -d postgres \
        -c "DROP DATABASE IF EXISTS ${LOCAL_DB_NAME:-educonnect};"
      PGPASSWORD="${LOCAL_DB_PASSWORD:-educonnect_dev}" psql \
        -h localhost -p "${POSTGRES_HOST_PORT}" \
        -U "${LOCAL_DB_USER:-educonnect}" -d postgres \
        -c "CREATE DATABASE ${LOCAL_DB_NAME:-educonnect};"
      echo "Done. On next Development startup, the API will re-apply EF Core migrations and re-run the dev seed scripts."
      ;;
    remote)
      echo "Refusing to reset remote database. Do it manually if you really mean it." >&2
      exit 1
      ;;
  esac
}

cmd_status() {
  print_backend_summary
  echo
  if probe_db; then
    echo "Status: REACHABLE ✅"
  else
    echo "Status: UNREACHABLE ❌"
    case "${EDUCONNECT_DB_MODE}" in
      docker) echo "Hint: scripts/db.sh up" ;;
      local)  echo "Hint: start Postgres natively (brew services start postgresql)" ;;
      remote) echo "Hint: check VPN / network / DATABASE_URL credentials" ;;
    esac
    exit 1
  fi
}

cmd_psql() {
  require_psql
  echo "Connecting to ${DATABASE_URL}"
  psql "${DATABASE_URL}"
}

cmd_url() {
  echo "${DATABASE_URL}"
}

case "${COMMAND}" in
  up)     cmd_up ;;
  down)   cmd_down ;;
  reset)  cmd_reset ;;
  status) cmd_status ;;
  psql)   cmd_psql ;;
  url)    cmd_url ;;
esac
