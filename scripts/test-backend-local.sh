#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${SCRIPT_DIR}/lib/local-env.sh"

ensure_toolchain_path
load_repo_env
set_backend_defaults

print_backend_summary
exec dotnet test "${REPO_ROOT}/apps/api/tests/EduConnect.Api.Tests/EduConnect.Api.Tests.csproj" -c Release
