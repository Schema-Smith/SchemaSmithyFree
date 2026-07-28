# Create + seed the Course 6 datafix tenant databases on each sandbox engine, or on your
# own server's single activated engine (LEARN_SERVER). Creates shop_tenant_a, shop_tenant_b,
# shop_tenant_c. Each database has the identical Shop schema and a deterministic
# price-defect batch: OrderItems on May-2026 SalesOrders carry UnitPrice = ROUND(
# Product.UnitPrice * 0.81, 2) -- a 10% discount applied twice. All other OrderItems
# carry the intended ROUND(Product.UnitPrice * 0.90, 2). Re-running is safe -- all
# DDL is idempotent. PASS is reported only after a shop table is confirmed to exist.
#
# -Reset drops and recreates the tenant databases empty before reseeding. Only databases
# the labs created are ever dropped -- see Remove-LabDatabase.
[CmdletBinding()]
param([switch] $Reset)

. "$PSScriptRoot\..\lab-sql.ps1"

$failed = $false
$labels = @{ sqlserver = 'SQL Server'; postgres = 'PostgreSQL'; mysql = 'MySQL'; mariadb = 'MariaDB' }

function Initialize-Db {
    param([string]$Engine, [string]$Db)
    Write-Host -NoNewline ("  {0,-26} " -f $Db)
    try {
        if ($Reset) {
            $removed = Remove-LabDatabase -Engine $Engine -Database $Db
            if ($removed -eq 'refused') {
                throw "'$Db' exists but wasn't created by the labs, so it will not be dropped. Rename or move it, then re-run."
            }
        }
        Confirm-LabDatabase -Engine $Engine -Database $Db | Out-Null
        Invoke-LabSqlFile -Engine $Engine -Database $Db -Path (Join-Path $PSScriptRoot "seed/$Engine/shop.sql") | Out-Null
        $readySql = switch ($Engine) {
            'sqlserver' { "SELECT 'READY' WHERE OBJECT_ID('dbo.Customer') IS NOT NULL" }
            'postgres'  { "SELECT 1 FROM information_schema.tables WHERE table_name='customer'" }
            default     { "SELECT 1 FROM information_schema.tables WHERE table_schema='$Db' AND table_name='Customer'" }
        }
        $ready = Invoke-LabSql -Engine $Engine -Database $Db -Sql $readySql
        if ($ready -notmatch 'READY|1') { throw "seed completed but the shop schema's Customer table was not found." }
        Write-Host $(if ($Reset) { 'PASS (reset)' } else { 'PASS' })
        return $true
    } catch {
        Write-Host 'FAIL'
        Write-Host "    $($_.Exception.Message)"
        $script:failed = $true
        return $false
    }
}

# Apply the scoped datafix_user role for an engine. One invocation handles all
# three tenants; run AFTER the tenant tables exist (PostgreSQL grants ON ALL
# TABLES only cover tables present at grant time). Idempotent.
function Set-DatafixRole {
    param([string]$Engine)
    Write-Host -NoNewline ("  {0,-26} " -f 'datafix_user role')
    try {
        $admin = Get-LabAdminDatabase $Engine
        Invoke-LabSqlFile -Engine $Engine -Database $admin -Path (Join-Path $PSScriptRoot "seed/$Engine/datafix_role.sql") | Out-Null
        $readySql = switch ($Engine) {
            'sqlserver' { "SELECT 'READY' FROM sys.server_principals WHERE name='datafix_user'" }
            'postgres'  { "SELECT 1 FROM pg_roles WHERE rolname='datafix_user'" }
            default     { "SELECT 1 FROM mysql.user WHERE user='datafix_user'" }
        }
        $ready = Invoke-LabSql -Engine $Engine -Database $admin -Sql $readySql
        if ($ready -notmatch 'READY|1') { throw "datafix_role.sql ran but the datafix_user role/login was not found." }
        Write-Host 'PASS'
    } catch {
        Write-Host 'FAIL'
        Write-Host "    $($_.Exception.Message)"
        $script:failed = $true
    }
}

$tenants = @('shop_tenant_a', 'shop_tenant_b', 'shop_tenant_c')

$total = 0
$parts = @()
foreach ($engine in Get-LabEngines) {
    Write-Host $labels[$engine]
    $n = 0
    foreach ($db in $tenants) { if (Initialize-Db $engine $db) { $n++ } }
    Set-DatafixRole $engine
    $total += $n
    $parts += "$n $($labels[$engine])"
}

Write-Host ''
if (-not $failed) {
    Write-Host "All $total databases are seeded and the datafix_user role is created ($($parts -join ', '))."
    exit 0
} else {
    Write-Host 'One or more databases could not be seeded. Is the sandbox up (or your own server reachable)? See Demos/Learn/README.md.'
    exit 1
}
