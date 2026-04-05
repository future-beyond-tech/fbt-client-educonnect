#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env
set_frontend_defaults

print_frontend_summary
pnpm --dir "${REPO_ROOT}/apps/web" lint
pnpm --dir "${REPO_ROOT}/apps/web" type-check
export NODE_ENV="production"
NEXT_PUBLIC_APP_URL="${NEXT_PUBLIC_APP_URL}" \
NEXT_PUBLIC_API_URL="${NEXT_PUBLIC_API_URL}" \
pnpm --dir "${REPO_ROOT}/apps/web" build
