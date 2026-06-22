# Create the Course 3 target databases (dev / staging / prod) on each sandbox engine.
# Re-running is safe — all DDL is idempotent.

$ErrorActionPreference = 'Continue'
$failed = $false

# ── helpers ────────────────────────────────────────────────────────────────────

function Invoke-DbCreate {
    param([string]$Label, [string[]]$DockerArgs)
    Write-Host -NoNewline ("  {0,-26} " -f $Label)
    docker exec @DockerArgs *> $null
    if ($LASTEXITCODE -eq 0) { Write-Host "PASS" } else { Write-Host "FAIL"; $script:failed = $true }
}

# ── SQL Server ──────────────────────────────────────────────────────────────────

Write-Host "SQL Server"
foreach ($env in @('dev', 'staging', 'prod')) {
    $db = "ordersservice_$env"
    Invoke-DbCreate $db @(
        'learn-sqlserver', 'bash', '-c',
        "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q `"IF DB_ID('$db') IS NULL CREATE DATABASE $db`""
    )
}

# ── PostgreSQL ──────────────────────────────────────────────────────────────────

Write-Host "PostgreSQL"
foreach ($env in @('dev', 'staging', 'prod')) {
    $db = "ordersservice_$env"
    Invoke-DbCreate $db @(
        'learn-postgres', 'bash', '-c',
        "psql -U postgres -d postgres -tAc `"SELECT 1 FROM pg_database WHERE datname='$db'`" | grep -q 1 || psql -U postgres -d postgres -c 'CREATE DATABASE $db'"
    )
}

# ── MySQL ───────────────────────────────────────────────────────────────────────

Write-Host "MySQL"
foreach ($env in @('dev', 'staging', 'prod')) {
    $db = "ordersservice_$env"
    Invoke-DbCreate $db @(
        'learn-mysql',
        'mysql', '-uroot', '-pLearn!Passw0rd',
        '-e', "CREATE DATABASE IF NOT EXISTS $db"
    )
}

# ── result ──────────────────────────────────────────────────────────────────────

Write-Host ""
if (-not $failed) {
    Write-Host "All nine databases are ready."
    exit 0
} else {
    Write-Host "One or more databases could not be created. Is the sandbox up? See Demos/Learn/README.md."
    exit 1
}
