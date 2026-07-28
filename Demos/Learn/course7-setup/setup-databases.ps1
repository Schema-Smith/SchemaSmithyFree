# Create the Course 7 tenant fleet on each sandbox engine, or on your own server's single
# activated engine (LEARN_SERVER): five EMPTY databases fleet_tenant_001..005. No schema is
# seeded -- the Module 1 deploy is what forges the Shop schema into each tenant. Re-running
# is safe -- creation is idempotent. PASS is reported only after the five databases are
# confirmed to exist on an engine.
#
# Tenant creation goes through Confirm-LabDatabase so each tenant is stamped as
# lab-provisioned -- that stamp is what lets -Reset safely drop and recreate the fleet on
# your own server without ever touching a same-named database it didn't create.
#
# -Reset drops and recreates the five tenant databases empty. Only databases the labs
# created are ever dropped -- see Remove-LabDatabase.
[CmdletBinding()]
param([switch] $Reset)

. "$PSScriptRoot\..\lab-sql.ps1"

$failed = $false
$labels = @{ sqlserver = 'SQL Server'; postgres = 'PostgreSQL'; mysql = 'MySQL'; mariadb = 'MariaDB' }
$tenants = @('fleet_tenant_001', 'fleet_tenant_002', 'fleet_tenant_003', 'fleet_tenant_004', 'fleet_tenant_005')

function Get-TenantNames {
    param([string]$Engine)
    $sql = switch ($Engine) {
        'sqlserver' { "SELECT name FROM sys.databases WHERE name LIKE 'fleet[_]tenant[_]%'" }
        'postgres'  { "SELECT datname FROM pg_database WHERE datname LIKE 'fleet\_tenant\_%'" }
        default     { "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'fleet\_tenant\_%'" }
    }
    $out = Invoke-LabSql -Engine $Engine -Database (Get-LabAdminDatabase $Engine) -Sql $sql
    if ([string]::IsNullOrWhiteSpace($out)) { return @() }
    return @($out -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

$engines = Get-LabEngines
foreach ($engine in $engines) {
    Write-Host -NoNewline ("{0,-12} " -f $labels[$engine])
    try {
        if ($Reset) {
            # Drop the whole fleet, not just 001-005: Module 3 onboards fleet_tenant_006, and a
            # reset that left it behind wouldn't be the clean slate it promises.
            foreach ($db in (Get-TenantNames $engine)) {
                if ((Remove-LabDatabase -Engine $engine -Database $db) -eq 'refused') {
                    throw "'$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
                }
            }
        }
        foreach ($db in $tenants) { Confirm-LabDatabase -Engine $engine -Database $db | Out-Null }

        # Confirm the five the course needs. Extra tenants are course progress, not a fault:
        # Module 3 onboards fleet_tenant_006 on purpose, so a strict count would report FAIL
        # for having followed the course.
        $found = Get-TenantNames $engine
        $missing = @($tenants | Where-Object { $found -notcontains $_ })
        if ($missing.Count) { throw "missing tenant database(s): $($missing -join ', ')" }
        $extra = @($found | Where-Object { $tenants -notcontains $_ })

        $note = if ($Reset) { 'PASS (reset, 5 tenant databases)' } else { 'PASS (5 tenant databases)' }
        if ($extra.Count) { $note += " + $($extra -join ', ') from a later module" }
        Write-Host $note
    } catch {
        Write-Host 'FAIL'
        Write-Host "    $($_.Exception.Message)"
        $failed = $true
    }
}

Write-Host ''
if (-not $failed) {
    $total = 5 * $engines.Count
    $parts = ($engines | ForEach-Object { "5 $($labels[$_])" }) -join ', '
    Write-Host "All $total tenant databases created ($parts) -- empty, ready for Module 1."
    exit 0
} else {
    Write-Host 'One or more engines could not be set up. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md.'
    exit 1
}
