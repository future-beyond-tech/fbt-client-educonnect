#!/usr/bin/env bash
# Switch the active .env profile between "local" (native Postgres on 5432)
# and "docker" (Docker Postgres on host port 5433).
#
# Usage: use-env-profile.sh <local|docker>

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

PROFILE="${1:-}"
if [[ "${PROFILE}" != "local" && "${PROFILE}" != "docker" ]]; then
  echo "Usage: $(basename "$0") <local|docker>" >&2
  exit 2
fi

SOURCE="${REPO_ROOT}/.env.${PROFILE}"
TARGET="${REPO_ROOT}/.env"

if [[ ! -f "${SOURCE}" ]]; then
  echo "Profile file not found: ${SOURCE}" >&2
  echo "Create it from .env.example first." >&2
  exit 1
fi

# Preserve any existing .env as a one-shot backup so switches are non-destructive.
if [[ -f "${TARGET}" ]]; then
  cp "${TARGET}" "${TARGET}.bak"
fi

cp "${SOURCE}" "${TARGET}"

DB_LINE="$(grep -E '^DATABASE_URL=' "${TARGET}" || true)"
PORT_LINE="$(grep -E '^POSTGRES_HOST_PORT=' "${TARGET}" || true)"

echo "Activated profile: ${PROFILE}"
echo "  ${PORT_LINE}"
echo "  ${DB_LINE}"
echo "Backup of previous .env (if any) saved to .env.bak"
