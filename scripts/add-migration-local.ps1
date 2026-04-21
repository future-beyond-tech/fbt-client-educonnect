#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Name,

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
    Write-Host "Repo root:  $repoRoot"
    Write-Host "DB mode:    $($env:EDUCONNECT_DB_MODE)"
    Write-Host "DB:         $($env:DATABASE_URL)"
    Write-Host "Migration:  $Name"
    Write-Host "Command:    dotnet ef migrations add $Name --project $projectPath --startup-project $projectPath $($ExtraArgs -join ' ')"
    exit 0
}

Write-Host "Scaffolding EF Core migration '$Name' against $($env:DATABASE_URL)"
& dotnet ef migrations add $Name --project $projectPath --startup-project $projectPath @ExtraArgs
exit $LASTEXITCODE
