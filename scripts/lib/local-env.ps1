# Shared environment helpers for EduConnect local PowerShell scripts.
# Mirrors scripts/lib/local-env.sh for Windows.

$ErrorActionPreference = 'Stop'

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Get-RepoRoot {
    return $script:RepoRoot
}

function Load-RepoEnv {
    $envFile = Join-Path $script:RepoRoot '.env'
    if (-not (Test-Path $envFile)) { return }

    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq '' -or $line.StartsWith('#')) { return }

        $eq = $line.IndexOf('=')
        if ($eq -lt 1) { return }

        $key = $line.Substring(0, $eq).Trim()
        $value = $line.Substring($eq + 1).Trim()

        # Strip inline comment (only if value isn't quoted)
        if (-not ($value.StartsWith('"') -or $value.StartsWith("'"))) {
            $hash = $value.IndexOf('#')
            if ($hash -ge 0) { $value = $value.Substring(0, $hash).Trim() }
        }

        # Strip surrounding quotes
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        [System.Environment]::SetEnvironmentVariable($key, $value, 'Process')
    }
}

function Set-EnvDefault {
    param([string]$Name, [string]$Value)
    if (-not [System.Environment]::GetEnvironmentVariable($Name, 'Process')) {
        [System.Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    }
}

function Set-BackendDefaults {
    Set-EnvDefault 'ASPNETCORE_ENVIRONMENT' 'Development'
    Set-EnvDefault 'API_PORT' '5000'
    Set-EnvDefault 'POSTGRES_HOST_PORT' '5433'
    Set-EnvDefault 'DATABASE_URL' "postgresql://educonnect:educonnect_dev@localhost:$($env:POSTGRES_HOST_PORT)/educonnect"
    Set-EnvDefault 'JWT_SECRET' 'dev-secret-key-minimum-64-characters-long-for-hmac-sha256-signing-requirement'
    Set-EnvDefault 'JWT_ISSUER' 'educonnect-api'
    Set-EnvDefault 'JWT_AUDIENCE' 'educonnect-client'
    Set-EnvDefault 'PIN_MIN_LENGTH' '4'
    Set-EnvDefault 'PIN_MAX_LENGTH' '6'
    Set-EnvDefault 'CORS_ALLOWED_ORIGINS' 'http://localhost:3000'
    Set-EnvDefault 'RATE_LIMIT_API_PER_USER_PER_MINUTE' '60'
    Set-EnvDefault 'ASPNETCORE_URLS' "http://localhost:$($env:API_PORT)"
}

function Set-FrontendDefaults {
    Set-EnvDefault 'PORT' '3000'
    Set-EnvDefault 'NEXT_PUBLIC_APP_URL' "http://localhost:$($env:PORT)"
    Set-EnvDefault 'NEXT_PUBLIC_API_URL' 'http://localhost:5000'
}

function Print-BackendSummary {
    Write-Host "Repo root: $($script:RepoRoot)"
    Write-Host "API URL:   $($env:ASPNETCORE_URLS)"
    Write-Host "DB:        $($env:DATABASE_URL)"
    Write-Host "DB Port:   $($env:POSTGRES_HOST_PORT)"
    Write-Host "CORS:      $($env:CORS_ALLOWED_ORIGINS)"
}

function Print-FrontendSummary {
    Write-Host "Repo root: $($script:RepoRoot)"
    Write-Host "Web URL:   $($env:NEXT_PUBLIC_APP_URL)"
    Write-Host "API URL:   $($env:NEXT_PUBLIC_API_URL)"
    Write-Host "Port:      $($env:PORT)"
}
