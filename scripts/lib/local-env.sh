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

set_backend_defaults() {
  : "${ASPNETCORE_ENVIRONMENT:=Development}"
  : "${API_PORT:=5000}"
  : "${POSTGRES_HOST_PORT:=5433}"
  : "${DATABASE_URL:=postgresql://educonnect:educonnect_dev@localhost:${POSTGRES_HOST_PORT}/educonnect}"
  : "${JWT_SECRET:=dev-secret-key-minimum-64-characters-long-for-hmac-sha256-signing-requirement}"
  : "${JWT_ISSUER:=educonnect-api}"
  : "${JWT_AUDIENCE:=educonnect-client}"
  : "${PIN_MIN_LENGTH:=4}"
  : "${PIN_MAX_LENGTH:=6}"
  : "${CORS_ALLOWED_ORIGINS:=http://localhost:3000}"
  : "${RATE_LIMIT_API_PER_USER_PER_MINUTE:=60}"
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
