# Create the Course 3 target databases (dev / staging / prod) on each sandbox engine, or
# on your own server's single activated engine (LEARN_SERVER). Re-running is safe -- all
# DDL is idempotent. PASS is reported only after the database is confirmed to exist (a
# create that silently fails reports FAIL, never a false PASS).
. "$PSScriptRoot\..\lab-sql.ps1"

$failed = $false
$total = 0
$labels = @{ sqlserver = 'SQL Server'; postgres = 'PostgreSQL'; mysql = 'MySQL'; mariadb = 'MariaDB' }

foreach ($engine in Get-LabEngines) {
    Write-Host $labels[$engine]
    foreach ($envName in @('dev', 'staging', 'prod')) {
        $db = "ordersservice_$envName"
        Write-Host -NoNewline ("  {0,-26} " -f $db)
        $total++
        try {
            Confirm-LabDatabase -Engine $engine -Database $db | Out-Null
            Write-Host 'PASS'
        } catch {
            Write-Host 'FAIL'
            Write-Host "    $($_.Exception.Message)"
            $failed = $true
        }
    }
}

Write-Host ''
if (-not $failed) {
    Write-Host "All $total databases are ready."
    exit 0
} else {
    Write-Host 'One or more databases could not be created. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md.'
    exit 1
}
