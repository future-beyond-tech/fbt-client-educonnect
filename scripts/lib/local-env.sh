#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/../.." && pwd)"

ensure_toolchain_path() {
  export PATH="/opt/homebrew/bin:/usr/local/share/dotnet:${PATH}"
}

load_repo_env() {
  if [[ -f "${REPO_ROOT}/.env" ]]; then
    set -a
    # shellcheck disable=SC1091
    source "${REPO_ROOT}/.env"
    set +a
  fi
}

## ─────────────────────────────────────────────────────────────────────
## DB mode switching
##
## EDUCONNECT_DB_MODE controls which Postgres the backend connects to:
##   • docker  → containerized Postgres started by docker compose
##               (default; uses host port POSTGRES_HOST_PORT, default 5433)
##   • local   → a Postgres you run natively on the host
##               (default port 5432, db/user "educonnect")
##   • remote  → use whatever DATABASE_URL is already in .env / shell
##               (Railway, staging, anything you don't want overwritten)
##
## You can override on a single run:    DB_MODE=local pnpm local:backend:run
## Or pass --db local to run-backend-local.sh.
## ─────────────────────────────────────────────────────────────────────
resolve_db_mode() {
  : "${EDUCONNECT_DB_MODE:=${DB_MODE:-docker}}"

  case "${EDUCONNECT_DB_MODE}" in
    docker)
      : "${POSTGRES_HOST_PORT:=5433}"
      DATABASE_URL="postgresql://educonnect:educonnect_dev@localhost:${POSTGRES_HOST_PORT}/educonnect"
      ;;
    local)
      : "${POSTGRES_HOST_PORT:=5432}"
      : "${LOCAL_DB_USER:=educonnect}"
      : "${LOCAL_DB_PASSWORD:=educonnect_dev}"
      : "${LOCAL_DB_NAME:=educonnect}"
      DATABASE_URL="postgresql://${LOCAL_DB_USER}:${LOCAL_DB_PASSWORD}@localhost:${POSTGRES_HOST_PORT}/${LOCAL_DB_NAME}"
      ;;
    remote)
      if [[ -z "${DATABASE_URL:-}" ]]; then
        echo "ERROR: EDUCONNECT_DB_MODE=remote but DATABASE_URL is not set." >&2
        exit 1
      fi
      : "${POSTGRES_HOST_PORT:=remote}"
      ;;
    *)
      echo "ERROR: Unknown EDUCONNECT_DB_MODE='${EDUCONNECT_DB_MODE}'. Use docker | local | remote." >&2
      exit 1
      ;;
  esac

  export EDUCONNECT_DB_MODE
  export POSTGRES_HOST_PORT
  export DATABASE_URL
}

set_backend_defaults() {
  : "${ASPNETCORE_ENVIRONMENT:=Development}"
  : "${API_PORT:=5000}"
  resolve_db_mode
  : "${JWT_SECRET:=dev-secret-key-minimum-64-characters-long-for-hmac-sha256-signing-requirement}"
  : "${JWT_ISSUER:=educonnect-api}"
  : "${JWT_AUDIENCE:=educonnect-client}"
  : "${PIN_MIN_LENGTH:=4}"
  : "${PIN_MAX_LENGTH:=6}"
  : "${CORS_ALLOWED_ORIGINS:=http://localhost:3000}"
  : "${RATE_LIMIT_API_PER_USER_PER_MINUTE:=60}"
  : "${NEXT_PUBLIC_APP_URL:=http://localhost:3000}"
  # Dev-safe placeholders keep the API bootable locally; replace them with real
  # Resend credentials before testing forgot/reset email delivery.
  : "${RESEND_API_KEY:=dev-resend-api-key}"
  : "${RESEND_FROM_EMAIL:=EduConnect <no-reply@example.com>}"
  : "${ASPNETCORE_URLS:=http://localhost:${API_PORT}}"

  export ASPNETCORE_ENVIRONMENT
  export API_PORT
  export POSTGRES_HOST_PORT
  export DATABASE_URL
  export JWT_SECRET
  export JWT_ISSUER
  export JWT_AUDIENCE
  export PIN_MIN_LENGTH
  export PIN_MAX_LENGTH
  export CORS_ALLOWED_ORIGINS
  export RATE_LIMIT_API_PER_USER_PER_MINUTE
  export NEXT_PUBLIC_APP_URL
  export RESEND_API_KEY
  export RESEND_FROM_EMAIL
  export ASPNETCORE_URLS
}

set_frontend_defaults() {
  : "${PORT:=3000}"
  : "${NEXT_PUBLIC_APP_URL:=http://localhost:${PORT}}"
  : "${NEXT_PUBLIC_API_URL:=http://localhost:5000}"

  export PORT
  export NEXT_PUBLIC_APP_URL
  export NEXT_PUBLIC_API_URL
}

print_backend_summary() {
  cat <<EOF
Repo root: ${REPO_ROOT}
API URL:   ${ASPNETCORE_URLS}
App URL:   ${NEXT_PUBLIC_APP_URL}
DB mode:   ${EDUCONNECT_DB_MODE}
DB:        ${DATABASE_URL}
DB Port:   ${POSTGRES_HOST_PORT}
CORS:      ${CORS_ALLOWED_ORIGINS}
EOF
}

print_frontend_summary() {
  cat <<EOF
Repo root: ${REPO_ROOT}
Web URL:   ${NEXT_PUBLIC_APP_URL}
API URL:   ${NEXT_PUBLIC_API_URL}
Port:      ${PORT}
EOF
}
