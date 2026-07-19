[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Server,
  [Parameter(Mandatory)] [string] $User,
  [Parameter(Mandatory)] [string] $Password,
  [string] $ManifestPath = "$PSScriptRoot/demo-databases.manifest",
  [switch] $Force
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

# --- Preflight: sqlcmd must be present ---
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
  Write-Error @"
sqlcmd is required but was not found on PATH.
Install the SQL Server command-line tools and re-open your shell:
  Windows : winget install Microsoft.SQLServer.SqlCmd   (or the 'MsSqlCmdLnUtils' MSI)
  macOS   : brew install sqlcmd
  Linux   : follow https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility (mssql-tools18)
Verify with:  sqlcmd -?
"@
  exit 1
}

function Invoke-Sql([string]$Query, [string[]]$Vars = @(), [string]$InFile) {
  $args = @('-S', $Server, '-U', $User, '-P', $Password, '-C', '-b', '-h', '-1', '-W')
  foreach ($v in $Vars) { $args += @('-v', $v) }
  if ($InFile) { $args += @('-i', $InFile) } else { $args += @('-Q', $Query) }
  $out = & sqlcmd @args
  if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $out" }
  return $out
}

# --- Read manifest ---
$rows = Get-Content $ManifestPath | Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object {
  $f = $_.Split('|'); [pscustomobject]@{ Type=$f[0]; Name=$f[1]; Token=$f[2]; Package=$f[3] }
}

# --- Teardown scan: classify each managed DB ---
$toDrop = @(); $collisions = @()
foreach ($r in $rows) {
  $res = (Invoke-Sql -InFile "$here/endpoint/stamp.sql" -Vars @("Op=check", "Db=$($r.Name)")) -join ''
  if     ($res -match 'STAMP_RESULT:stamped')   { $toDrop += $r.Name }
  elseif ($res -match 'STAMP_RESULT:unstamped') { $collisions += $r.Name }
  # 'absent' => nothing to do
}
if ($collisions.Count -gt 0) {
  Write-Error @"
These databases already exist on $Server but were NOT created by this helper:
  $($collisions -join ', ')
Refusing to drop databases the helper didn't create. To proceed, rename the
colliding entries in $ManifestPath (and their TOKEN stays the same), then re-run.
"@
  exit 2
}

# --- Loud confirmation ---
if ($toDrop.Count -gt 0 -and -not $Force) {
  Write-Host "The following demo databases on $Server WILL BE DROPPED and recreated:`n  $($toDrop -join ', ')" -ForegroundColor Yellow
  if ((Read-Host "Type 'yes' to continue") -ne 'yes') { Write-Host 'Aborted.'; exit 0 }
}

# --- Teardown (stamped only) ---
foreach ($name in $toDrop) { Invoke-Sql -InFile "$here/endpoint/stamp.sql" -Vars @("Op=dropIfStamped", "Db=$name") | Out-Null }

# --- Bootstrap TestMain/TestSecondary ---
Invoke-Sql -InFile "$here/endpoint/bootstrap.sql" | Out-Null

# --- Quench each product, then stamp its DB ---
foreach ($r in $rows | Where-Object Type -eq 'product') {
  Push-Location "$here/$($r.Package)"
  $env:SmithySettings_Target__Server = $Server
  $env:SmithySettings_Target__User = $User
  $env:SmithySettings_Target__Password = $Password
  $env:SmithySettings_Target__ConnectionProperties__TrustServerCertificate = 'True'
  # --SchemaPackagePath=. points SchemaQuench at this package (cwd); the
  # --ScriptTokens override renames the deployed DB when the manifest NAME
  # differs from the package default (the collision workaround), and is a
  # harmless no-op when NAME matches.
  & schemaquench --SchemaPackagePath=. "--ScriptTokens:$($r.Token)=$($r.Name)"
  if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Quench failed for $($r.Name)" }
  Pop-Location
  Invoke-Sql -InFile "$here/endpoint/stamp.sql" -Vars @("Op=add", "Db=$($r.Name)") | Out-Null
}
Write-Host "Done. Demo databases provisioned on $Server." -ForegroundColor Green
