# Create the Course 9 Northwind Commerce service databases on each sandbox engine, or on
# your own server's single activated engine (LEARN_SERVER): orders on SQL Server, catalog
# on PostgreSQL, sessions on MySQL. No schema is seeded -- each course module deploys its
# own native package into these databases. Re-running is safe -- Confirm-LabDatabase only
# creates what's missing. PASS is reported only after the database is confirmed to exist
# (a create that silently fails reports FAIL, never a false PASS).
#
# -Reset drops and recreates them empty. Use it any time you want a clean slate -- e.g.
# after a module's deploy fails partway and leaves a service in a state a later module
# doesn't expect. Only databases the labs created are ever dropped -- see Remove-LabDatabase.
[CmdletBinding()]
param([switch] $Reset)

. "$PSScriptRoot\..\lab-sql.ps1"

$failed = $false
$total = 0
$readyDbs = @()
$services = [ordered]@{
    sqlserver = @{ Db = 'orders';   Label = 'Orders (SQL Server)' }
    postgres  = @{ Db = 'catalog';  Label = 'Catalog (PostgreSQL)' }
    mysql     = @{ Db = 'sessions'; Label = 'Sessions (MySQL)' }
}

foreach ($engine in Get-LabEngines) {
    if (-not $services.Contains($engine)) { continue }
    $svc = $services[$engine]
    Write-Host -NoNewline ("{0,-30} " -f $svc.Label)
    $total++
    try {
        if ($Reset) {
            $removed = Remove-LabDatabase -Engine $engine -Database $svc.Db
            if ($removed -eq 'refused') {
                throw "'$($svc.Db)' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
            }
        }
        Confirm-LabDatabase -Engine $engine -Database $svc.Db | Out-Null
        Write-Host $(if ($Reset) { 'PASS (reset)' } else { 'PASS (service database ready)' })
        $readyDbs += $svc.Db
    } catch {
        Write-Host 'FAIL'
        Write-Host "    $($_.Exception.Message)"
        $failed = $true
    }
}

Write-Host ''
if ($total -eq 0) {
    Write-Host "Course 9 doesn't use $($env:LEARN_ENGINE) -- it needs SQL Server, PostgreSQL, and MySQL together. Point use-my-server at one of those instead, or use the sandbox for this course."
    exit 1
} elseif (-not $failed) {
    if ($total -eq 1) {
        Write-Host "The $($readyDbs[0]) service database is ready."
    } else {
        Write-Host "All $total service databases are ready -- $($readyDbs -join ', ')."
    }
    if ((Test-LabOwnServer) -and $total -lt $services.Count) {
        Write-Host ''
        Write-Host "Course 9 runs all three services together, so it needs SQL Server, PostgreSQL, and MySQL."
        Write-Host "You have $total of the three. If you run the other engines too, re-source use-my-server for"
        Write-Host 'each one and run this script again -- otherwise use the sandbox for this course.'
    }
    exit 0
} else {
    Write-Host 'One or more engines could not be set up. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md.'
    exit 1
}
