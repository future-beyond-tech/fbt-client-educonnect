#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\local-env.ps1')

Load-RepoEnv
Set-FrontendDefaults

$repoRoot = Get-RepoRoot
$webDir = Join-Path $repoRoot 'apps\web'

Print-FrontendSummary

& pnpm --dir $webDir lint
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& pnpm --dir $webDir type-check
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$env:NODE_ENV = 'production'
& pnpm --dir $webDir build
exit $LASTEXITCODE
