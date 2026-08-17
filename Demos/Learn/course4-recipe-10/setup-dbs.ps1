# Create the Course 4 Recipe 10 replication topology databases -- SQL Server only, since
# this recipe's mechanism (a cross-database EXEC from an After Script) is SQL Server
# specific. Two databases: Shop_Primary (the publisher -- Customers, Orders, Inventory) and
# Shop_Replica (the subscriber -- gets only the tables whose Extensions.ReplicationEnabled
# is true, deployed into it by Shop_Primary's After Script). Re-running is safe -- creation
# is idempotent. PASS is reported only after both databases are confirmed to exist.
#
# -Reset drops and recreates both databases empty. Only a database the labs created is
# ever dropped -- see Remove-LabDatabase.
[CmdletBinding()]
param([switch] $Reset)

. "$PSScriptRoot\..\lab-sql.ps1"

$engine = 'sqlserver'
$dbs = @('Shop_Primary', 'Shop_Replica')
$failed = $false

try {
    if ($Reset) {
        foreach ($db in $dbs) {
            if ((Remove-LabDatabase -Engine $engine -Database $db) -eq 'refused') {
                throw "'$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
            }
        }
    }

    foreach ($db in $dbs) {
        Confirm-LabDatabase -Engine $engine -Database $db | Out-Null
    }

    $note = if ($Reset) { 'PASS (reset, Shop_Primary + Shop_Replica ready)' } else { 'PASS (Shop_Primary + Shop_Replica ready)' }
    Write-Host $note
} catch {
    Write-Host 'FAIL'
    Write-Host "    $($_.Exception.Message)"
    $failed = $true
}

Write-Host ''
if (-not $failed) {
    Write-Host 'Shop_Primary and Shop_Replica ready on SQL Server -- deploy the Package to get started.'
    exit 0
} else {
    Write-Host 'Could not set up the SQL Server databases. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md.'
    exit 1
}
