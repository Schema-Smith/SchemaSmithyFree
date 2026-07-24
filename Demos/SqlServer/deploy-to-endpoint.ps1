[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Server,
  [string] $User,
  [string] $Password,
  [string] $ManifestPath = "$PSScriptRoot/demo-databases.manifest",
  [switch] $Force
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

# Omit -User/-Password to connect with Windows Authentication (trusted connection).
# Supplying a user requires a password. SQL Server only — PostgreSQL/MySQL always need credentials.
$WindowsAuth = [string]::IsNullOrEmpty($User)
if (-not $WindowsAuth -and [string]::IsNullOrEmpty($Password)) {
  Write-Error "-User was supplied without -Password. Provide both for SQL auth, or omit both for Windows Authentication."
  exit 1
}

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
  $args = @('-S', $Server, '-C', '-b', '-h', '-1', '-W')
  if ($WindowsAuth) { $args += '-E' } else { $args += @('-U', $User, '-P', $Password) }
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
$toDrop = @(); $collisions = @(); $orphanedFiles = @()
foreach ($r in $rows) {
  $res = (Invoke-Sql -InFile "$here/endpoint/stamp.sql" -Vars @("Op=check", "Db=$($r.Name)")) -join ''
  if     ($res -match 'STAMP_RESULT:stamped')       { $toDrop += $r.Name }
  elseif ($res -match 'STAMP_RESULT:unstamped')     { $collisions += $r.Name }
  elseif ($res -match 'STAMP_RESULT:orphaned-file') { $orphanedFiles += $r.Name }
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
if ($orphanedFiles.Count -gt 0) {
  Write-Error @"
Database files already exist on disk on $Server but are not attached (a detached database):
  $($orphanedFiles -join ', ')
CREATE DATABASE would fail with SQL Server error 1802. The helper will not touch these files —
they may be your own data. Detach/move the .mdf/.ldf, or rename the colliding entries in
$ManifestPath (their TOKEN stays the same, so they deploy under the new name), then re-run.
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
  $pkg = Join-Path $here $r.Package
  Push-Location $pkg
  $env:SmithySettings_Target__Server = $Server
  # Empty user/password → SchemaQuench builds an Integrated Security connection (Windows Auth).
  $env:SmithySettings_Target__User = if ($WindowsAuth) { '' } else { $User }
  $env:SmithySettings_Target__Password = if ($WindowsAuth) { '' } else { $Password }
  $env:SmithySettings_Target__ConnectionProperties__TrustServerCertificate = 'True'
  # Configure via SmithySettings_* env vars (the form the Docker demo uses), NOT --CLI
  # overrides: the generic `--Key=value` override is newer than some released SchemaQuench
  # builds, so a --SchemaPackagePath override is silently ignored on an older installed CLI.
  # SchemaPackagePath points at this package; the ScriptTokens override renames the deployed
  # DB when the manifest NAME differs from the package default (collision workaround), and is
  # a harmless no-op when NAME matches.
  $env:SmithySettings_SchemaPackagePath = $pkg
  Set-Item -Path "env:SmithySettings_ScriptTokens__$($r.Token)" -Value $r.Name
  & schemaquench
  if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Quench failed for $($r.Name)" }
  Pop-Location
  Invoke-Sql -InFile "$here/endpoint/stamp.sql" -Vars @("Op=add", "Db=$($r.Name)") | Out-Null
}
Write-Host "Done. Demo databases provisioned on $Server." -ForegroundColor Green
