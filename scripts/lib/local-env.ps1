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

function Resolve-DbMode {
    # EDUCONNECT_DB_MODE controls which Postgres the backend connects to:
    #   docker  → containerized Postgres started by docker compose (default)
    #   local   → native Postgres on the host
    #   remote  → use whatever DATABASE_URL is already in .env
    if (-not $env:EDUCONNECT_DB_MODE) {
        if ($env:DB_MODE) {
            $env:EDUCONNECT_DB_MODE = $env:DB_MODE
        } else {
            $env:EDUCONNECT_DB_MODE = 'docker'
        }
    }

    switch ($env:EDUCONNECT_DB_MODE) {
        'docker' {
            if (-not $env:POSTGRES_HOST_PORT) { $env:POSTGRES_HOST_PORT = '5433' }
            $env:DATABASE_URL = "postgresql://educonnect:educonnect_dev@localhost:$($env:POSTGRES_HOST_PORT)/educonnect"
        }
        'local' {
            if (-not $env:POSTGRES_HOST_PORT) { $env:POSTGRES_HOST_PORT = '5432' }
            if (-not $env:LOCAL_DB_USER)      { $env:LOCAL_DB_USER     = 'educonnect' }
            if (-not $env:LOCAL_DB_PASSWORD)  { $env:LOCAL_DB_PASSWORD = 'educonnect_dev' }
            if (-not $env:LOCAL_DB_NAME)      { $env:LOCAL_DB_NAME     = 'educonnect' }
            $env:DATABASE_URL = "postgresql://$($env:LOCAL_DB_USER):$($env:LOCAL_DB_PASSWORD)@localhost:$($env:POSTGRES_HOST_PORT)/$($env:LOCAL_DB_NAME)"
        }
        'remote' {
            if (-not $env:DATABASE_URL) {
                Write-Error 'EDUCONNECT_DB_MODE=remote but DATABASE_URL is not set.'
                exit 1
            }
            if (-not $env:POSTGRES_HOST_PORT) { $env:POSTGRES_HOST_PORT = 'remote' }
        }
        default {
            Write-Error "Unknown EDUCONNECT_DB_MODE='$($env:EDUCONNECT_DB_MODE)'. Use docker | local | remote."
            exit 1
        }
    }
}

function Set-BackendDefaults {
    Set-EnvDefault 'ASPNETCORE_ENVIRONMENT' 'Development'
    Set-EnvDefault 'API_PORT' '5000'
    Resolve-DbMode
    Set-EnvDefault 'JWT_SECRET' 'dev-secret-key-minimum-64-characters-long-for-hmac-sha256-signing-requirement'
    Set-EnvDefault 'JWT_ISSUER' 'educonnect-api'
    Set-EnvDefault 'JWT_AUDIENCE' 'educonnect-client'
    Set-EnvDefault 'PIN_MIN_LENGTH' '4'
    Set-EnvDefault 'PIN_MAX_LENGTH' '6'
    Set-EnvDefault 'CORS_ALLOWED_ORIGINS' 'http://localhost:3000'
    Set-EnvDefault 'RATE_LIMIT_API_PER_USER_PER_MINUTE' '60'
    Set-EnvDefault 'NEXT_PUBLIC_APP_URL' 'http://localhost:3000'
    # Dev-safe placeholders keep the API bootable locally; replace them with
    # real Resend credentials before testing forgot/reset email delivery.
    Set-EnvDefault 'RESEND_API_KEY' 'dev-resend-api-key'
    Set-EnvDefault 'RESEND_FROM_EMAIL' 'EduConnect <no-reply@example.com>'
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
    Write-Host "App URL:   $($env:NEXT_PUBLIC_APP_URL)"
    Write-Host "DB mode:   $($env:EDUCONNECT_DB_MODE)"
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
