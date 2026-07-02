# Create the Course 7 tenant fleet on each sandbox engine: five EMPTY databases
# fleet_tenant_001..005 on SQL Server, PostgreSQL, and MySQL. No schema is seeded —
# the Module 1 deploy is what forges the Shop schema into each tenant. Re-running is
# safe; all CREATE DDL is guarded. PASS is reported only after the five databases
# are confirmed to exist on an engine.

$failed = $false

function Invoke-Seed {
    param([string]$Engine)
    $file = Join-Path $PSScriptRoot "seed/$Engine/01_create_tenant_databases.sql"
    if (-not (Test-Path $file)) { Write-Host "  MISSING $file"; $script:failed = $true; return }
    $sql = Get-Content $file -Raw
    switch ($Engine) {
        'sqlserver' { $sql | docker exec -i learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b | Out-Null }
        'postgres'  { $sql | docker exec -i learn-postgres psql -U postgres -v ON_ERROR_STOP=1 | Out-Null }
        'mysql'     { $sql | docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd 2>$null | Out-Null }
    }
}

function Get-TenantCount {
    param([string]$Engine)
    switch ($Engine) {
        'sqlserver' { (docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name LIKE 'fleet[_]tenant[_]%'" 2>$null).Trim() }
        'postgres'  { (docker exec learn-postgres psql -U postgres -tAc "SELECT COUNT(*) FROM pg_database WHERE datname LIKE 'fleet\_tenant\_%'" 2>$null).Trim() }
        'mysql'     { (docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name LIKE 'fleet\_tenant\_%'" 2>$null).Trim() }
    }
}

function Initialize-Engine {
    param([string]$Engine, [string]$Label)
    Write-Host -NoNewline ("{0,-12} " -f $Label)
    Invoke-Seed $Engine
    $n = Get-TenantCount $Engine
    if ($n -eq '5') { Write-Host "PASS (5 tenant databases)" }
    else { Write-Host "FAIL (found '$n', expected 5)"; $script:failed = $true }
}

Initialize-Engine 'sqlserver' 'SQL Server'
Initialize-Engine 'postgres'  'PostgreSQL'
Initialize-Engine 'mysql'     'MySQL'

Write-Host ''
if (-not $failed) {
    Write-Host 'All 15 tenant databases created (5 SQL Server, 5 PostgreSQL, 5 MySQL) — empty, ready for Module 1.'
    exit 0
} else {
    Write-Host 'One or more engines could not be set up. Is the sandbox up? See Demos/Learn/README.md.'
    exit 1
}
