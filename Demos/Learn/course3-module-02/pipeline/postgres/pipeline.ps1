# Course 3 - Module 2 - local CI/CD pipeline simulation (PostgreSQL)
#
# Simulates the merge -> staging -> prod flow the chapter describes. The COMMANDS
# this script runs ARE the contract; a CI YAML is just a wrapper around them.
#
# One built artifact (the solution Package) deploys to every environment. The ONLY
# thing that changes per environment is a handful of SmithySettings_* env vars:
#   SmithySettings_ScriptTokens__TargetDb   -> which database to converge
#   SmithySettings_WhatIfONLY               -> preview (PR check) vs. apply
#
# Re-runnable: every quench converges to the declared state and stops, so running
# the whole pipeline again is a clean no-op on already-converged databases.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\..\..\lab-sql.ps1"

$SchemaQuench = if ($env:SCHEMAQUENCH) { $env:SCHEMAQUENCH } else { 'schemaquench' }
$here     = $PSScriptRoot
$starter  = (Resolve-Path "$here\..\..\starter\postgres").Path
$solution = (Resolve-Path "$here\..\..\solution\postgres").Path
$settings = "$solution\base.settings.json"   # connection + package defaults, shared by every env

function Invoke-Quench {
    param([string]$PackagePath, [string]$TargetDb, [string]$WhatIf)
    $env:SmithySettings_SchemaPackagePath      = $PackagePath
    $env:SmithySettings_ScriptTokens__TargetDb = $TargetDb
    $env:SmithySettings_WhatIfONLY             = $WhatIf
    & $SchemaQuench "--ConfigFile:$settings"
    $quenchExit = $LASTEXITCODE
    Remove-Item Env:SmithySettings_SchemaPackagePath, Env:SmithySettings_ScriptTokens__TargetDb, Env:SmithySettings_WhatIfONLY
    # A failed deploy makes every later step meaningless - stop here rather than let a
    # downstream check report a confusing symptom of this failure. (The bash twin gets
    # this from `set -e`.)
    if ($quenchExit -ne 0) {
        throw "SchemaQuench failed (exit $quenchExit) deploying $PackagePath to $TargetDb."
    }
}

function Get-IndexState {
    param([string]$Db)
    Invoke-LabSql -Engine 'postgres' -Database $Db -Sql "SELECT COALESCE((SELECT indexname FROM pg_indexes WHERE indexname='ix_customer_email'),'ABSENT')"
}

Write-Host "=== Step 1: deploy the BASE (starter) to staging - establish current production state ==="
Invoke-Quench "$starter\Package" 'ordersservice_staging' 'false'

Write-Host "`n=== Step 2: WhatIf the SOLUTION against staging - the PR check (no changes applied) ==="
Invoke-Quench "$solution\Package" 'ordersservice_staging' 'true'

Write-Host "`n--- Prove WhatIf applied NOTHING: ix_customer_email on staging should read ABSENT ---"
$state = Get-IndexState 'ordersservice_staging'
Write-Host "    staging ix_customer_email: $state"
if ($state -ne 'ABSENT') {
    Write-Error "WhatIf should not have created the index, but the catalog reports '$state'."
    exit 1
}

Write-Host "`n=== Step 3: WhatIf passed - deploy the SOLUTION to staging ==="
Invoke-Quench "$solution\Package" 'ordersservice_staging' 'false'

Write-Host "`n=== Step 4: promote the SAME artifact to prod - only the env vars change ==="
Invoke-Quench "$solution\Package" 'ordersservice_prod' 'false'

Write-Host "`n--- Both environments converged: ix_customer_email should now read present ---"
Write-Host "    staging: $(Get-IndexState 'ordersservice_staging')"
Write-Host "    prod:    $(Get-IndexState 'ordersservice_prod')"
Write-Host "`nPipeline complete. Same artifact, staging then prod, gated by a WhatIf preview."
