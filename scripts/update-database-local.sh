#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env

DRY_RUN=false
DB_MODE_CLI=""
EXTRA_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --)
      shift
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
    *)
      EXTRA_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ -n "${DB_MODE_CLI}" ]]; then
  export EDUCONNECT_DB_MODE="${DB_MODE_CLI}"
  unset DATABASE_URL POSTGRES_HOST_PORT
fi

resolve_db_mode

if [[ "${DRY_RUN}" == "true" ]]; then
  extra_args_string="${EXTRA_ARGS[*]-}"
  cat <<EOF
Repo root: ${REPO_ROOT}
DB mode:   ${EDUCONNECT_DB_MODE}
DB:        ${DATABASE_URL}
Command:   dotnet ef database update --project ${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj --startup-project ${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj ${extra_args_string}
EOF
  exit 0
fi

echo "Applying EF Core migrations to ${DATABASE_URL}"

if (( ${#EXTRA_ARGS[@]} )); then
  exec dotnet ef database update \
    --project "${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj" \
    --startup-project "${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj" \
    "${EXTRA_ARGS[@]}"
fi

exec dotnet ef database update \
  --project "${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj" \
  --startup-project "${REPO_ROOT}/apps/api/src/EduConnect.Api/EduConnect.Api.csproj"
