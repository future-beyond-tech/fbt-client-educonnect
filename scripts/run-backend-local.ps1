#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\local-env.ps1')

Load-RepoEnv
Set-BackendDefaults

$repoRoot = Get-RepoRoot
$projectPath = Join-Path $repoRoot 'apps\api\src\EduConnect.Api\EduConnect.Api.csproj'

if ($DryRun) {
    Print-BackendSummary
    Write-Host "Command: dotnet run --project $projectPath"
    exit 0
}

Print-BackendSummary

$healthUrl = "http://localhost:$($env:API_PORT)/health"
$pollIntervalSeconds = 2
$maxPolls = 60
$dbNotReadyNotified = $false

Write-Host "Starting backend API..."
$apiProcess = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $projectPath) `
    -NoNewWindow -PassThru

# Ensure cleanup on Ctrl+C / exit
$cleanup = {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        try { Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue } catch {}
    }
}
Register-EngineEvent PowerShell.Exiting -Action $cleanup | Out-Null

try {
    for ($i = 0; $i -lt $maxPolls; $i++) {
        if ($apiProcess.HasExited) {
            exit $apiProcess.ExitCode
        }

        try {
            $resp = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            $status = [int]$resp.StatusCode
        } catch {
            $status = 0
            if ($_.Exception.Response) {
                $status = [int]$_.Exception.Response.StatusCode
            }
        }

        if ($status -eq 200) {
            Write-Host "Backend server started on $($env:ASPNETCORE_URLS) (health: $healthUrl)"
            $apiProcess.WaitForExit()
            exit $apiProcess.ExitCode
        }

        if ($status -eq 503 -and -not $dbNotReadyNotified) {
            Write-Host "Backend API is running, but the database is not reachable yet."
            Write-Host "Hint: start Postgres with 'docker compose up db -d' (Docker uses host port $($env:POSTGRES_HOST_PORT)) or verify DATABASE_URL."
            $dbNotReadyNotified = $true
        }

        Start-Sleep -Seconds $pollIntervalSeconds
    }

    Write-Host "Backend process is running, but $healthUrl did not become ready within $($pollIntervalSeconds * $maxPolls) seconds."
    $apiProcess.WaitForExit()
    exit $apiProcess.ExitCode
}
finally {
    & $cleanup
}
