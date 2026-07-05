# Create the Course 8 diagnostics baseline on each sandbox engine: ONE EMPTY database
# diag_baseline on SQL Server, PostgreSQL, and MySQL. No schema is seeded —
# the Module 1 deploy is what forges the Shop schema into the baseline. Re-running is
# safe; all CREATE DDL is guarded. PASS is reported only after the database
# is confirmed to exist on an engine.

$failed = $false

function Invoke-Seed {
    param([string]$Engine)
    $file = Join-Path $PSScriptRoot "seed/$Engine/01_create_baseline_database.sql"
    if (-not (Test-Path $file)) { Write-Host "  MISSING $file"; $script:failed = $true; return }
    # Copy the script into the container and run it there, rather than piping it on
    # stdin from the host: Windows PowerShell 5.1 injects a UTF-8 BOM into piped
    # native-command input, which sqlcmd rejects. docker cp + in-container execution
    # is encoding-proof and behaves identically under PowerShell 5.1, pwsh 7, and bash.
    switch ($Engine) {
        'sqlserver' { docker cp $file learn-sqlserver:/tmp/seed.sql | Out-Null; docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -i /tmp/seed.sql | Out-Null }
        'postgres'  { docker cp $file learn-postgres:/tmp/seed.sql | Out-Null; docker exec learn-postgres psql -U postgres -v ON_ERROR_STOP=1 -f /tmp/seed.sql | Out-Null }
        'mysql'     { docker cp $file learn-mysql:/tmp/seed.sql | Out-Null; docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e 'source /tmp/seed.sql' 2>$null | Out-Null }
    }
}

function Get-BaselineCount {
    param([string]$Engine)
    switch ($Engine) {
        'sqlserver' { (docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'diag_baseline'" 2>$null).Trim() }
        'postgres'  { (docker exec learn-postgres psql -U postgres -tAc "SELECT COUNT(*) FROM pg_database WHERE datname = 'diag_baseline'" 2>$null).Trim() }
        'mysql'     { (docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'diag_baseline'" 2>$null).Trim() }
    }
}

function Initialize-Engine {
    param([string]$Engine, [string]$Label)
    Write-Host -NoNewline ("{0,-12} " -f $Label)
    Invoke-Seed $Engine
    $n = Get-BaselineCount $Engine
    if ($n -eq '1') { Write-Host "PASS (diagnostics baseline database ready)" }
    else { Write-Host "FAIL (found '$n', expected 1)"; $script:failed = $true }
}

Initialize-Engine 'sqlserver' 'SQL Server'
Initialize-Engine 'postgres'  'PostgreSQL'
Initialize-Engine 'mysql'     'MySQL'

Write-Host ''
if (-not $failed) {
    Write-Host 'diagnostics baseline database ready on all 3 engines — empty, ready for Module 1.'
    exit 0
} else {
    Write-Host 'One or more engines could not be set up. Is the sandbox up? See Demos/Learn/README.md.'
    exit 1
}
