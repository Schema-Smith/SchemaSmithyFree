# Create + seed the Course 6 datafix tenant databases on each sandbox engine.
# Creates shop_tenant_a, shop_tenant_b, shop_tenant_c on SQL Server, PostgreSQL,
# and MySQL. Each database has the identical Shop schema and a deterministic
# price-defect batch: OrderItems on May-2026 SalesOrders carry UnitPrice = ROUND(
# Product.UnitPrice * 0.81, 2) — a 10% discount applied twice. All other OrderItems
# carry the intended ROUND(Product.UnitPrice * 0.90, 2). Re-running is safe — all
# DDL is idempotent. PASS is reported only after a shop table is confirmed to exist.

$failed = $false

function Invoke-SqlFile {
    param([string]$Engine, [string]$Db, [string]$File)
    if (-not (Test-Path $File)) { Write-Host "    MISSING $File"; $script:failed = $true; return }
    $sql = Get-Content $File -Raw
    switch ($Engine) {
        # SQL Server via docker cp + sqlcmd -i, not a PS stdin pipe: Windows PowerShell 5.1 injects a
        # UTF-8 BOM into piped native-command input that sqlcmd rejects. psql/mysql tolerate it, so
        # those keep the (simpler) stdin pipe.
        'sqlserver' { docker cp $File learn-sqlserver:/tmp/seed.sql | Out-Null; docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d $Db -i /tmp/seed.sql | Out-Null }
        'postgres'  { $sql | docker exec -i learn-postgres psql -U postgres -d $Db -v ON_ERROR_STOP=1 | Out-Null }
        'mysql'     { $sql | docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd $Db 2>$null | Out-Null }
    }
}

function Initialize-Db {
    param([string]$Engine, [string]$Db)
    Write-Host -NoNewline ("  {0,-26} " -f $Db)
    switch ($Engine) {
        'sqlserver' { docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q "IF DB_ID('$Db') IS NULL CREATE DATABASE [$Db]" 2>$null | Out-Null }
        'postgres'  { docker exec learn-postgres psql -U postgres -d postgres -c "CREATE DATABASE $Db" 2>$null | Out-Null }
        'mysql'     { docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS ``$Db``" 2>$null | Out-Null }
    }
    Invoke-SqlFile $Engine $Db (Join-Path $PSScriptRoot "seed/$Engine/shop.sql")
    $out = ''
    switch ($Engine) {
        'sqlserver' { $out = docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d $Db -Q "SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL" 2>$null }
        'postgres'  { $out = docker exec learn-postgres psql -U postgres -d $Db -tAc "SELECT 1 FROM information_schema.tables WHERE table_name='customer'" 2>$null }
        'mysql'     { $out = docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.tables WHERE table_schema='$Db' AND table_name='Customer'" 2>$null }
    }
    if (($out -join '') -match 'READY|1') { Write-Host 'PASS' } else { Write-Host 'FAIL'; $script:failed = $true }
}

# Apply the scoped datafix_user role for an engine. One invocation handles all
# three tenants; run AFTER the tenant tables exist (PostgreSQL grants ON ALL
# TABLES only cover tables present at grant time). Idempotent.
function Set-DatafixRole {
    param([string]$Engine)
    $file = Join-Path $PSScriptRoot "seed/$Engine/datafix_role.sql"
    Write-Host -NoNewline ("  {0,-26} " -f 'datafix_user role')
    if (-not (Test-Path $file)) { Write-Host 'MISSING'; $script:failed = $true; return }
    $sql = Get-Content $file -Raw
    $out = ''
    switch ($Engine) {
        'sqlserver' { docker cp $file learn-sqlserver:/tmp/seed.sql | Out-Null; docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d master -i /tmp/seed.sql | Out-Null
                      $out = docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -d master -Q "SELECT 'READY' FROM sys.server_principals WHERE name='datafix_user'" 2>$null }
        'postgres'  { $sql | docker exec -i learn-postgres psql -U postgres -d postgres | Out-Null
                      $out = docker exec learn-postgres psql -U postgres -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='datafix_user'" 2>$null }
        'mysql'     { $sql | docker exec -i learn-mysql mysql -uroot -pLearn!Passw0rd 2>$null | Out-Null
                      $out = docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM mysql.user WHERE user='datafix_user'" 2>$null }
    }
    if (($out -join '') -match 'READY|1') { Write-Host 'PASS' } else { Write-Host 'FAIL'; $script:failed = $true }
}

$tenants = @('shop_tenant_a', 'shop_tenant_b', 'shop_tenant_c')

Write-Host 'SQL Server'
foreach ($db in $tenants) { Initialize-Db 'sqlserver' $db }
Set-DatafixRole 'sqlserver'

Write-Host 'PostgreSQL'
foreach ($db in $tenants) { Initialize-Db 'postgres' $db }
Set-DatafixRole 'postgres'

Write-Host 'MySQL'
foreach ($db in $tenants) { Initialize-Db 'mysql' $db }
Set-DatafixRole 'mysql'

Write-Host ''
if (-not $failed) {
    Write-Host 'All 9 databases are seeded and the datafix_user role is created (3 SQL Server, 3 PostgreSQL, 3 MySQL).'
    exit 0
} else {
    Write-Host 'One or more databases could not be seeded. Is the sandbox up? See Demos/Learn/README.md.'
    exit 1
}
