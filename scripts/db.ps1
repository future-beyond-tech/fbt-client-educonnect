#requires -Version 5.1
<#
.SYNOPSIS
    EduConnect database lifecycle helper (Windows mirror of scripts/db.sh).

.DESCRIPTION
    Manages the Postgres backing the API in any of three modes:
      docker  → docker compose service `db` (default)
      local   → native Postgres on the host
      remote  → uses DATABASE_URL from .env (no lifecycle commands available)

.PARAMETER Command
    up | down | reset | status | psql | url

.PARAMETER Db
    docker | local | remote (overrides EDUCONNECT_DB_MODE / DB_MODE)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('up', 'down', 'reset', 'status', 'psql', 'url')]
    [string]$Command,

    [Parameter()]
    [ValidateSet('docker', 'local', 'remote')]
    [string]$Db
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\local-env.ps1')

Load-RepoEnv
if ($Db) {
    $env:EDUCONNECT_DB_MODE = $Db
    Remove-Item Env:DATABASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:POSTGRES_HOST_PORT -ErrorAction SilentlyContinue
}
Set-BackendDefaults

$repoRoot = Get-RepoRoot

function Test-Db {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $tcp.Connect('localhost', [int]$env:POSTGRES_HOST_PORT)
        $tcp.Close()
        return $true
    } catch {
        return $false
    }
}

function Require-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'docker is not installed or not on PATH.'
    }
}

function Require-Psql {
    if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
        throw 'psql is not installed. Install postgresql client.'
    }
}

function Cmd-Up {
    switch ($env:EDUCONNECT_DB_MODE) {
        'docker' {
            Require-Docker
            Write-Host "Starting Postgres container (host port $($env:POSTGRES_HOST_PORT))..."
            Push-Location $repoRoot
            try {
                & docker compose up -d db
            } finally { Pop-Location }
            for ($i = 0; $i -lt 30; $i++) {
                if (Test-Db) {
                    Write-Host "Postgres is ready at localhost:$($env:POSTGRES_HOST_PORT)."
                    return
                }
                Start-Sleep -Seconds 1
            }
            throw 'Postgres did not become ready within 30s.'
        }
        'local' {
            Write-Host "Mode 'local' assumes Postgres is already running natively on localhost:$($env:POSTGRES_HOST_PORT)."
            if (Test-Db) {
                Write-Host 'Postgres is reachable.'
            } else {
                throw 'Postgres is NOT reachable. Start it via your OS service manager.'
            }
        }
        'remote' {
            Write-Host "Mode 'remote' uses DATABASE_URL=$($env:DATABASE_URL). Lifecycle is managed externally."
        }
    }
}

function Cmd-Down {
    switch ($env:EDUCONNECT_DB_MODE) {
        'docker' {
            Require-Docker
            Push-Location $repoRoot
            try { & docker compose stop db } finally { Pop-Location }
            Write-Host 'Postgres container stopped.'
        }
        'local'  { Write-Host "Mode 'local': stop Postgres via your OS service manager." }
        'remote' { Write-Host "Mode 'remote': nothing to stop." }
    }
}

function Cmd-Reset {
    switch ($env:EDUCONNECT_DB_MODE) {
        'docker' {
            Require-Docker
            Write-Host 'Wiping Docker Postgres volume...'
            Push-Location $repoRoot
            try { & docker compose down db -v } finally { Pop-Location }
            Cmd-Up
            Write-Host 'Done. The .NET API will re-run all schema + seed migrations on next startup.'
        }
        'local' {
            Require-Psql
            $dbName = if ($env:LOCAL_DB_NAME) { $env:LOCAL_DB_NAME } else { 'educonnect' }
            $dbUser = if ($env:LOCAL_DB_USER) { $env:LOCAL_DB_USER } else { 'educonnect' }
            $env:PGPASSWORD = if ($env:LOCAL_DB_PASSWORD) { $env:LOCAL_DB_PASSWORD } else { 'educonnect_dev' }
            Write-Host "Dropping and recreating database '$dbName'..."
            & psql -h localhost -p $env:POSTGRES_HOST_PORT -U $dbUser -d postgres -c "DROP DATABASE IF EXISTS $dbName;"
            & psql -h localhost -p $env:POSTGRES_HOST_PORT -U $dbUser -d postgres -c "CREATE DATABASE $dbName;"
            Write-Host 'Done. The .NET API will re-run all schema + seed migrations on next startup.'
        }
        'remote' {
            throw 'Refusing to reset remote database.'
        }
    }
}

function Cmd-Status {
    Print-BackendSummary
    Write-Host ''
    if (Test-Db) {
        Write-Host 'Status: REACHABLE'
    } else {
        Write-Host 'Status: UNREACHABLE'
        switch ($env:EDUCONNECT_DB_MODE) {
            'docker' { Write-Host 'Hint: scripts\db.ps1 up' }
            'local'  { Write-Host 'Hint: start Postgres natively' }
            'remote' { Write-Host 'Hint: check VPN / DATABASE_URL' }
        }
        exit 1
    }
}

function Cmd-Psql {
    Require-Psql
    Write-Host "Connecting to $($env:DATABASE_URL)"
    & psql $env:DATABASE_URL
}

function Cmd-Url { Write-Host $env:DATABASE_URL }

switch ($Command) {
    'up'     { Cmd-Up }
    'down'   { Cmd-Down }
    'reset'  { Cmd-Reset }
    'status' { Cmd-Status }
    'psql'   { Cmd-Psql }
    'url'    { Cmd-Url }
}
