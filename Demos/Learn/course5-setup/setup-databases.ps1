# Create + seed the Course 5 migration-track databases on each sandbox engine.
# Each database is the post-migration state a source tool would have left behind:
# the shared shop schema plus that tool's own bookkeeping table. Re-running is
# safe — all DDL is idempotent. PASS is reported only after a shop table is
# confirmed to exist (a seed that silently fails reports FAIL, never a false PASS).

$failed = $false

function Invoke-SqlFile {
    param([string]$Engine, [string]$Db, [string]$File)
    if (-not (Test-Path $File)) { Write-Host "    MISSING $File"; $script:failed = $true; return }
    $sql = Get-Content $File -Raw
    switch ($Engine) {
        'sqlserver' { $sql | docker exec -i learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d $Db | Out-Null }
        'postgres'  { $sql | docker exec -i learn-postgres psql -U postgres -d $Db -v ON_ERROR_STOP=1 | Out-Null }
        'mysql'     { $sql | docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd $Db 2>$null | Out-Null }
    }
}

function Initialize-Db {
    param([string]$Engine, [string]$Db, [string]$Tracker)
    Write-Host -NoNewline ("  {0,-26} " -f $Db)
    switch ($Engine) {
        'sqlserver' { docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q "IF DB_ID('$Db') IS NULL CREATE DATABASE [$Db]" 2>$null | Out-Null }
        'postgres'  { docker exec learn-postgres psql -U postgres -d postgres -c "CREATE DATABASE $Db" 2>$null | Out-Null }
        'mysql'     { docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS ``$Db``" 2>$null | Out-Null }
    }
    Invoke-SqlFile $Engine $Db (Join-Path $PSScriptRoot "seed/$Engine/shop.sql")
    if ($Tracker) { Invoke-SqlFile $Engine $Db (Join-Path $PSScriptRoot "seed/$Engine/$Tracker.sql") }
    $out = ''
    switch ($Engine) {
        'sqlserver' { $out = docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d $Db -Q "SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL" 2>$null }
        'postgres'  { $out = docker exec learn-postgres psql -U postgres -d $Db -tAc "SELECT 1 FROM information_schema.tables WHERE table_name='customer'" 2>$null }
        'mysql'     { $out = docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='$Db' AND table_name='Customer'" 2>$null }
    }
    if (($out -join '') -match 'READY|1') { Write-Host 'PASS' } else { Write-Host 'FAIL'; $script:failed = $true }
}

# database -> tracker-file-stem  (empty tracker = no bookkeeping table, e.g. DACPAC)
$threeEngineDbs = [ordered]@{
    'shop_from_flyway'    = 'tracker_flyway'
    'shop_from_liquibase' = 'tracker_liquibase'
    'shop_from_efcore'    = 'tracker_efcore'
    'shop_from_scripts'   = 'tracker_scripts'
}

Write-Host 'SQL Server'
foreach ($db in $threeEngineDbs.Keys) { Initialize-Db 'sqlserver' $db $threeEngineDbs[$db] }
# DACPAC is a SQL Server technology (Course 5 Module 4 is SQL-Server-only by design); no tracker table.
Initialize-Db 'sqlserver' 'shop_from_dacpac' ''

Write-Host 'PostgreSQL'
foreach ($db in $threeEngineDbs.Keys) { Initialize-Db 'postgres' $db $threeEngineDbs[$db] }

Write-Host 'MySQL'
foreach ($db in $threeEngineDbs.Keys) { Initialize-Db 'mysql' $db $threeEngineDbs[$db] }

Write-Host ''
if (-not $failed) {
    Write-Host 'All 13 databases are seeded and ready (5 SQL Server, 4 PostgreSQL, 4 MySQL).'
    exit 0
} else {
    Write-Host 'One or more databases could not be seeded. Is the sandbox up? See Demos/Learn/README.md.'
    exit 1
}
