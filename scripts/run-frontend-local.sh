#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env
set_frontend_defaults
export NODE_ENV="${NODE_ENV:-development}"

if [[ "${1:-}" == "--dry-run" ]]; then
  print_frontend_summary
  echo "Command: pnpm --dir ${REPO_ROOT}/apps/web exec next dev --hostname 0.0.0.0 --port ${PORT}"
  exit 0
fi

print_frontend_summary
exec pnpm --dir "${REPO_ROOT}/apps/web" exec next dev --hostname 0.0.0.0 --port "${PORT}"
