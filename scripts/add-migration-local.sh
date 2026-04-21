#!/usr/bin/env bash

# Wraps `dotnet ef migrations add <Name>` for the EduConnect API. Follows the
# same conventions as update-database-local.sh: honors .env + DB mode so the
# migration scaffolding points at the right DbContext.
#
# Usage:
#   pnpm db:migration:add AddExams
#   pnpm db:migration:add AddExams --db local
#   pnpm db:migration:add AddExams -- --output-dir Migrations
#
# Forwards any extra flags after the name straight through to `dotnet ef`.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env

DRY_RUN=false
DB_MODE_CLI=""
MIGRATION_NAME=""
EXTRA_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --)
      shift
      EXTRA_ARGS+=("$@")
      break
      ;;
    --db)
      DB_MODE_CLI="$2"
      shift 2
      ;;
    --db=*)
      DB_MODE_CLI="${1#*=}"
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    -*)
      EXTRA_ARGS+=("$1")
      shift
      ;;
    *)
      if [[ -z "${MIGRATION_NAME}" ]]; then
        MIGRATION_NAME="$1"
      else
        EXTRA_ARGS+=("$1")
      fi
      shift
      ;;
  esac
done

if [[ -z "${MIGRATION_NAME}" ]]; then
  cat <<'EOF' >&2
Error: migration name is required.

Usage:
  pnpm db:migration:add <Name> [--db docker|local|remote] [-- --output-dir Migrations]

Example:
  pnpm db:migration:add AddExams
EOF
  exit 1
fi

if [[ -n "${DB_MODE_CLI}" ]]; then
  export EDUCONNECT_DB_MODE="${DB_MODE_CLI}"
  unset DATABASE_URL POSTGRES_HOST_PORT
fi

resolve_db_mode

PROJECT="${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj"

if [[ "${DRY_RUN}" == "true" ]]; then
  extra_args_string="${EXTRA_ARGS[*]-}"
  cat <<EOF
Repo root:    ${REPO_ROOT}
DB mode:      ${EDUCONNECT_DB_MODE}
DB:           ${DATABASE_URL}
Migration:    ${MIGRATION_NAME}
Command:      dotnet ef migrations add ${MIGRATION_NAME} --project ${PROJECT} --startup-project ${PROJECT} ${extra_args_string}
EOF
  exit 0
fi

echo "Scaffolding EF Core migration '${MIGRATION_NAME}' against ${DATABASE_URL}"

if (( ${#EXTRA_ARGS[@]} )); then
  exec dotnet ef migrations add "${MIGRATION_NAME}" \
    --project "${PROJECT}" \
    --startup-project "${PROJECT}" \
    "${EXTRA_ARGS[@]}"
fi

exec dotnet ef migrations add "${MIGRATION_NAME}" \
  --project "${PROJECT}" \
  --startup-project "${PROJECT}"
