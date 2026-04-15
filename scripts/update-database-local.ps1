#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$DryRun,

    [ValidateSet('docker', 'local', 'remote')]
    [string]$Db,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\local-env.ps1')

Load-RepoEnv
if ($Db) {
    $env:EDUCONNECT_DB_MODE = $Db
    Remove-Item Env:DATABASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:POSTGRES_HOST_PORT -ErrorAction SilentlyContinue
}

Resolve-DbMode

$repoRoot = Get-RepoRoot
$projectPath = Join-Path $repoRoot 'apps\api\src\EduConnect.Api\EduConnect.Api.csproj'

if ($DryRun) {
    Write-Host "Repo root: $repoRoot"
    Write-Host "DB mode:   $($env:EDUCONNECT_DB_MODE)"
    Write-Host "DB:        $($env:DATABASE_URL)"
    Write-Host "Command:   dotnet ef database update --project $projectPath --startup-project $projectPath $($ExtraArgs -join ' ')"
    exit 0
}

Write-Host "Applying EF Core migrations to $($env:DATABASE_URL)"
& dotnet ef database update --project $projectPath --startup-project $projectPath @ExtraArgs
exit $LASTEXITCODE
