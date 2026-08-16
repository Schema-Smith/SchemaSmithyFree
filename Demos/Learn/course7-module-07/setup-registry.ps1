# Create the Course 7 Module 7 fleet registries on each sandbox engine, or on your own
# server's single activated engine (LEARN_SERVER): three registry databases per engine
# (Dev/Prod/Empty), each holding a seeded Tenants table that Module 7's
# DatabaseIdentificationScript reads to discover which tenant databases to deploy into.
# Dev and Prod both list fleet_tenant_001..005 -- Dev has 005 inactive, Prod has it active.
# Empty gets the Tenants table with zero rows, to exercise a registry with no tenants.
# Re-running is safe -- creation and seeding are idempotent. PASS is reported only after
# the three registries' Tenants row counts are confirmed (Dev=5, Prod=5, Empty=0).
#
# Registry creation goes through Confirm-LabDatabase so each registry is stamped as
# lab-provisioned -- that stamp is what lets -Reset safely drop and recreate the
# registries on your own server without ever touching a same-named database it didn't
# create.
#
# -Reset drops and recreates the three registry databases (re-seeded). Only databases the
# labs created are ever dropped -- see Remove-LabDatabase.
[CmdletBinding()]
param([switch] $Reset)

. "$PSScriptRoot\..\lab-sql.ps1"

$failed = $false
$labels = @{ sqlserver = 'SQL Server'; postgres = 'PostgreSQL'; mysql = 'MySQL'; mariadb = 'MariaDB' }
$kinds = @('dev', 'prod', 'empty')
$regLabels = @{ dev = 'Dev'; prod = 'Prod'; empty = 'Empty' }

function Get-RegistryDatabase {
    param([string]$Engine, [string]$Kind)
    if ($Engine -eq 'sqlserver') {
        return @{ dev = 'FleetRegistry_Dev'; prod = 'FleetRegistry_Prod'; empty = 'FleetRegistry_Empty' }[$Kind]
    }
    return @{ dev = 'fleetregistry_dev'; prod = 'fleetregistry_prod'; empty = 'fleetregistry_empty' }[$Kind]
}

function Get-TableRef {
    param([string]$Engine)
    switch ($Engine) {
        'sqlserver' { return 'dbo.Tenants' }
        'postgres'  { return 'public.tenants' }
        default     { return 'Tenants' }
    }
}

function Get-ExpectedCount {
    param([string]$Kind)
    if ($Kind -eq 'empty') { return '0' }
    return '5'
}

# Builds the CREATE TABLE + seed statement for one engine/kind. Dev and Prod differ only in
# fleet_tenant_005's Active flag; Empty gets the table with no seed at all.
function Get-DdlSql {
    param([string]$Engine, [string]$Kind)
    $fiveActive = if ($Kind -eq 'dev') { '0' } else { '1' }

    switch ($Engine) {
        'sqlserver' {
            $sql = "IF OBJECT_ID('dbo.Tenants') IS NULL CREATE TABLE dbo.Tenants (DbName sysname NOT NULL PRIMARY KEY, Active bit NOT NULL);"
            if ($Kind -ne 'empty') {
                $sql += " MERGE dbo.Tenants AS t USING (VALUES ('fleet_tenant_001',1),('fleet_tenant_002',1),('fleet_tenant_003',1),('fleet_tenant_004',1),('fleet_tenant_005',$fiveActive)) AS s(DbName,Active) ON t.DbName=s.DbName WHEN NOT MATCHED THEN INSERT(DbName,Active) VALUES(s.DbName,s.Active) WHEN MATCHED THEN UPDATE SET Active=s.Active;"
            }
            return $sql
        }
        'postgres' {
            $sql = "CREATE TABLE IF NOT EXISTS public.tenants (dbname text PRIMARY KEY, active boolean NOT NULL);"
            if ($Kind -ne 'empty') {
                $fiveBool = if ($Kind -eq 'dev') { 'false' } else { 'true' }
                $sql += " INSERT INTO public.tenants(dbname,active) VALUES ('fleet_tenant_001',true),('fleet_tenant_002',true),('fleet_tenant_003',true),('fleet_tenant_004',true),('fleet_tenant_005',$fiveBool) ON CONFLICT (dbname) DO UPDATE SET active=EXCLUDED.active;"
            }
            return $sql
        }
        default {
            $sql = "CREATE TABLE IF NOT EXISTS Tenants (DbName varchar(128) NOT NULL PRIMARY KEY, Active tinyint NOT NULL);"
            if ($Kind -ne 'empty') {
                $sql += " INSERT INTO Tenants(DbName,Active) VALUES ('fleet_tenant_001',1),('fleet_tenant_002',1),('fleet_tenant_003',1),('fleet_tenant_004',1),('fleet_tenant_005',$fiveActive) ON DUPLICATE KEY UPDATE Active=VALUES(Active);"
            }
            return $sql
        }
    }
}

$engines = Get-LabEngines
foreach ($engine in $engines) {
    Write-Host -NoNewline ("{0,-12} " -f $labels[$engine])
    try {
        if ($Reset) {
            # Drop+recreate all three registries so -Reset is a clean slate for Module 7.
            foreach ($kind in $kinds) {
                $db = Get-RegistryDatabase $engine $kind
                if ((Remove-LabDatabase -Engine $engine -Database $db) -eq 'refused') {
                    throw "'$db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
                }
            }
        }

        foreach ($kind in $kinds) {
            $db = Get-RegistryDatabase $engine $kind
            Confirm-LabDatabase -Engine $engine -Database $db | Out-Null
            $sql = Get-DdlSql $engine $kind
            Invoke-LabSql -Engine $engine -Database $db -Sql $sql | Out-Null
        }

        # Confirm the seeded row counts (Dev=5, Prod=5, Empty=0) before reporting PASS.
        foreach ($kind in $kinds) {
            $db = Get-RegistryDatabase $engine $kind
            $expected = Get-ExpectedCount $kind
            $countSql = "SELECT COUNT(*) FROM $(Get-TableRef $engine)"
            $found = (Invoke-LabSql -Engine $engine -Database $db -Sql $countSql) -replace "`r", ''
            if ($found -ne $expected) {
                throw "$db ($($regLabels[$kind])): expected $expected row(s) in $(Get-TableRef $engine), found '$found'."
            }
        }

        $note = if ($Reset) { 'PASS (reset, 3 registries: Dev=5, Prod=5, Empty=0)' } else { 'PASS (3 registries: Dev=5, Prod=5, Empty=0)' }
        Write-Host $note
    } catch {
        Write-Host 'FAIL'
        Write-Host "    $($_.Exception.Message)"
        $failed = $true
    }
}

Write-Host ''
if (-not $failed) {
    $total = 3 * $engines.Count
    $parts = ($engines | ForEach-Object { "3 $($labels[$_])" }) -join ', '
    Write-Host "All $total registry databases created ($parts) -- seeded (Dev=5, Prod=5, Empty=0 Tenants rows), ready for Module 7."
    exit 0
} else {
    Write-Host 'One or more engines could not be set up. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md.'
    exit 1
}
