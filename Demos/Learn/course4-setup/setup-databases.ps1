# Create the Course 4 cookbook databases on each sandbox engine.
# Re-running is safe — all DDL is idempotent. PASS is reported only after the database
# is confirmed to exist (a create that silently fails reports FAIL, never a false PASS).

$failed = $false

function Confirm-Db {
    param([string]$Engine, [string]$Db)
    Write-Host -NoNewline ("  {0,-26} " -f $Db)
    $ok = $false
    switch ($Engine) {
        'sqlserver' {
            docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -Q "IF DB_ID('$Db') IS NULL CREATE DATABASE [$Db]" 2>$null | Out-Null
            $out = docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -h -1 -W -Q "SELECT 'READY' WHERE DB_ID('$Db') IS NOT NULL" 2>$null
            $ok = (($out -join '') -match 'READY')
        }
        'postgres' {
            docker exec learn-postgres psql -U postgres -d postgres -c "CREATE DATABASE $Db" 2>$null | Out-Null
            $out = docker exec learn-postgres psql -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$Db'" 2>$null
            $ok = (($out -join '') -match '1')
        }
        'mysql' {
            docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS ``$Db``" 2>$null | Out-Null
            $out = docker exec learn-mysql mysql -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='$Db'" 2>$null
            $ok = (($out -join '') -match '1')
        }
        'mariadb' {
            docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -e "CREATE DATABASE IF NOT EXISTS ``$Db``" 2>$null | Out-Null
            $out = docker exec learn-mariadb mariadb -uroot -pLearn!Passw0rd -N -e "SELECT 1 FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='$Db'" 2>$null
            $ok = (($out -join '') -match '1')
        }
    }
    if ($ok) { Write-Host 'PASS' } else { Write-Host 'FAIL'; $script:failed = $true }
}

$databases = @(
    'cookbook_r1_prod'
    'cookbook_r1_nonprod'
    'cookbook_r2'
    'cookbook_r3'
    'cookbook_r4'
    'cookbook_r5'
    'cookbook_r6'
    'cookbook_r8'
    'cookbook_r9'
)

Write-Host 'SQL Server'
foreach ($db in $databases) { Confirm-Db 'sqlserver' $db }

Write-Host 'PostgreSQL'
foreach ($db in $databases) { Confirm-Db 'postgres' $db }

Write-Host 'MySQL'
foreach ($db in $databases) { Confirm-Db 'mysql' $db }

Write-Host 'MariaDB'
foreach ($db in $databases) { Confirm-Db 'mariadb' $db }

Write-Host ''
if (-not $failed) {
    Write-Host 'All 36 databases are ready.'
    exit 0
} else {
    Write-Host 'One or more databases could not be created. Is the sandbox up? See Demos/Learn/README.md.'
    exit 1
}
