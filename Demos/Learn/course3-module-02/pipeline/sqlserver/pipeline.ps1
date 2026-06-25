# Course 3 - Module 2 - local CI/CD pipeline simulation (SQL Server)
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

$SchemaQuench = if ($env:SCHEMAQUENCH) { $env:SCHEMAQUENCH } else { 'schemaquench' }
$here     = $PSScriptRoot
$starter  = (Resolve-Path "$here\..\..\starter\sqlserver").Path
$solution = (Resolve-Path "$here\..\..\solution\sqlserver").Path
$settings = "$solution\base.settings.json"   # connection + package defaults, shared by every env

function Invoke-Quench {
    param([string]$PackagePath, [string]$TargetDb, [string]$WhatIf)
    $env:SmithySettings_SchemaPackagePath      = $PackagePath
    $env:SmithySettings_ScriptTokens__TargetDb = $TargetDb
    $env:SmithySettings_WhatIfONLY             = $WhatIf
    & $SchemaQuench "--ConfigFile:$settings"
    Remove-Item Env:SmithySettings_SchemaPackagePath, Env:SmithySettings_ScriptTokens__TargetDb, Env:SmithySettings_WhatIfONLY
}

function Get-IndexState {
    param([string]$Db)
    $out = docker exec learn-sqlserver /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P 'Learn!Passw0rd' -C -d $Db -h -1 -W `
        -Q "SET NOCOUNT ON; SELECT ISNULL((SELECT name FROM sys.indexes WHERE name='IX_Customer_Email'),'ABSENT')"
    ($out | Out-String).Trim()
}

Write-Host "=== Step 1: deploy the BASE (starter) to staging - establish current production state ==="
Invoke-Quench "$starter\Package" 'ordersservice_staging' 'false'

Write-Host "`n=== Step 2: WhatIf the SOLUTION against staging - the PR check (no changes applied) ==="
Invoke-Quench "$solution\Package" 'ordersservice_staging' 'true'

Write-Host "`n--- Prove WhatIf applied NOTHING: IX_Customer_Email on staging should read ABSENT ---"
$state = Get-IndexState 'ordersservice_staging'
Write-Host "    staging IX_Customer_Email: $state"
if ($state -ne 'ABSENT') { Write-Error "WhatIf should not have created the index."; exit 1 }

Write-Host "`n=== Step 3: WhatIf passed - deploy the SOLUTION to staging ==="
Invoke-Quench "$solution\Package" 'ordersservice_staging' 'false'

Write-Host "`n=== Step 4: promote the SAME artifact to prod - only the env vars change ==="
Invoke-Quench "$solution\Package" 'ordersservice_prod' 'false'

Write-Host "`n--- Both environments converged: IX_Customer_Email should now read present ---"
Write-Host "    staging: $(Get-IndexState 'ordersservice_staging')"
Write-Host "    prod:    $(Get-IndexState 'ordersservice_prod')"
Write-Host "`nPipeline complete. Same artifact, staging then prod, gated by a WhatIf preview."
