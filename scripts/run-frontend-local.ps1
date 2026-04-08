#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\local-env.ps1')

Load-RepoEnv
Set-FrontendDefaults
if (-not $env:NODE_ENV) { $env:NODE_ENV = 'development' }

$repoRoot = Get-RepoRoot
$webDir = Join-Path $repoRoot 'apps\web'

if ($DryRun) {
    Print-FrontendSummary
    Write-Host "Command: pnpm --dir $webDir exec next dev --hostname 0.0.0.0 --port $($env:PORT)"
    exit 0
}

Print-FrontendSummary

& pnpm --dir $webDir exec next dev --hostname 0.0.0.0 --port $env:PORT
exit $LASTEXITCODE
