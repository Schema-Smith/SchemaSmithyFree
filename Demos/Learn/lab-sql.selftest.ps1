# Self-test for lab-sql.ps1. Requires the Learn sandbox to be up (docker compose up -d
# in Demos/Learn/docker) and sqlcmd on PATH for the own-server checks.
# Run: .\lab-sql.selftest.ps1
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lab-sql.ps1"

$failed = 0
function Assert-Equal($Expected, $Actual, $What) {
    if ("$Expected" -eq "$Actual") { Write-Host "  PASS  $What" }
    else { Write-Host "  FAIL  $What (expected '$Expected', got '$Actual')"; $script:failed++ }
}
function Assert-Throws($Script, $Match, $What) {
    try { & $Script | Out-Null; Write-Host "  FAIL  $What (no error raised)"; $script:failed++ }
    catch {
        if ("$_" -match $Match) { Write-Host "  PASS  $What" }
        else { Write-Host "  FAIL  $What (message was '$_')"; $script:failed++ }
    }
}
function Clear-LabEnv {
    foreach ($v in 'LEARN_SERVER', 'LEARN_PORT', 'LEARN_USER', 'LEARN_PASSWORD', 'LEARN_ENGINE') {
        Remove-Item "Env:$v" -ErrorAction SilentlyContinue
    }
}
function Set-LabEnv($Engine, $Port) {
    $env:LEARN_SERVER = 'localhost'; $env:LEARN_PORT = "$Port"; $env:LEARN_ENGINE = $Engine
    $env:LEARN_USER = @{ sqlserver = 'sa'; postgres = 'postgres'; mysql = 'root'; mariadb = 'root' }[$Engine]
    $env:LEARN_PASSWORD = 'Learn!Passw0rd'
}

Write-Host 'sandbox mode'
Clear-LabEnv
Assert-Equal $false (Test-LabOwnServer) 'no LEARN_SERVER means sandbox mode'
Assert-Equal 'sqlserver postgres mysql mariadb' ((Get-LabEngines) -join ' ') 'sandbox runs all four engines'
Assert-Equal '1' (Invoke-LabSql -Engine sqlserver -Database master   -Sql 'SELECT 1') 'sqlserver inline query'
Assert-Equal '1' (Invoke-LabSql -Engine postgres  -Database postgres -Sql 'SELECT 1') 'postgres inline query'
Assert-Equal '1' (Invoke-LabSql -Engine mysql     -Database learn    -Sql 'SELECT 1') 'mysql inline query'
Assert-Equal '1' (Invoke-LabSql -Engine mariadb   -Database learn    -Sql 'SELECT 1') 'mariadb inline query'

Write-Host 'own-server mode (pointed at the sandbox''s published ports)'
Set-LabEnv 'sqlserver' 11433
Assert-Equal $true (Test-LabOwnServer) 'LEARN_SERVER means own-server mode'
Assert-Equal 'sqlserver' ((Get-LabEngines) -join ' ') 'own-server runs only the activated engine'
Assert-Equal '1' (Invoke-LabSql -Engine sqlserver -Database master -Sql 'SELECT 1') 'sqlserver local-client query'

Write-Host 'failures are loud'
Set-LabEnv 'sqlserver' 11499   # nothing listening
Assert-Throws { Invoke-LabSql -Engine sqlserver -Database master -Sql 'SELECT 1' } `
    'could not reach sqlserver' 'unreachable endpoint raises, never returns text'
Set-LabEnv 'sqlserver' 11433
Assert-Throws { Invoke-LabSql -Engine sqlserver -Database master -Sql 'SELECT * FROM no_such_table_here' } `
    'could not reach sqlserver' 'a failed query raises rather than returning the error text'

Clear-LabEnv
Write-Host ''
if ($failed -eq 0) { Write-Host 'lab-sql self-test: all checks passed'; exit 0 }
Write-Host "lab-sql self-test: $failed check(s) failed"
exit 1
