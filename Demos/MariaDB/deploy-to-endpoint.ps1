[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Server,
  [int] $Port = 3306,
  [Parameter(Mandatory)] [string] $User,
  [Parameter(Mandatory)] [string] $Password,
  [string] $ManifestPath = "$PSScriptRoot/demo-databases.manifest",
  [switch] $Force
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

# --- Preflight: the MariaDB client. Current releases ship `mariadb`; 10.2-10.4 ship only
# `mysql`, so accept either rather than tell someone to install a client they already have. ---
$MariaCli = if (Get-Command mariadb -ErrorAction SilentlyContinue) { 'mariadb' }
            elseif (Get-Command mysql -ErrorAction SilentlyContinue) { 'mysql' }
            else { $null }
if (-not $MariaCli) {
  Write-Error @"
mariadb (or mysql, on MariaDB 10.2-10.4) is required but neither was found on PATH.
Install the MariaDB client tools and re-open your shell:
  Windows : winget install MariaDB.Client   (or the MariaDB MSI 'Client Programs' feature)
  macOS   : brew install mariadb   (then add its bin to PATH)
  Linux   : apt-get install mariadb-client   (or dnf install MariaDB-client)
Verify with:  mariadb --version
"@
  exit 1
}

# mariadb reads the password from MYSQL_PWD (avoids the "password on the command line
# is insecure" warning). Connect with NO default database so admin ops work even when
# the target database does not exist yet.
$env:MYSQL_PWD = $Password

function Invoke-Maria([string]$InFile, [string]$Db, [string]$Op) {
  $args = @('-h', $Server, '-P', $Port, '-u', $User, '-N', '-s')
  if ($Db) { $args += @('--init-command', "SET @db='$Db', @op='$Op'") }
  $out = Get-Content $InFile -Raw | & $MariaCli @args
  if ($LASTEXITCODE -ne 0) { throw "$MariaCli failed: $out" }
  return $out
}

# --- Read manifest ---
$rows = Get-Content $ManifestPath | Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object {
  $f = $_.Split('|'); [pscustomobject]@{ Type=$f[0]; Name=$f[1]; Token=$f[2]; Package=$f[3] }
}

# --- Teardown scan: classify each managed DB ---
$toDrop = @(); $collisions = @()
foreach ($r in $rows) {
  $res = (Invoke-Maria -InFile "$here/endpoint/stamp.sql" -Db $r.Name -Op 'check') -join ''
  if     ($res -match 'STAMP_RESULT:stamped')   { $toDrop += $r.Name }
  elseif ($res -match 'STAMP_RESULT:unstamped') { $collisions += $r.Name }
  # 'absent' => nothing to do
}
if ($collisions.Count -gt 0) {
  Write-Error @"
These databases already exist on $Server but were NOT created by this helper:
  $($collisions -join ', ')
Refusing to drop databases the helper didn't create. To proceed, rename the
colliding entries in $ManifestPath (their TOKEN stays the same), then re-run.
"@
  exit 2
}

# --- Loud confirmation ---
if ($toDrop.Count -gt 0 -and -not $Force) {
  Write-Host "The following demo databases on $Server WILL BE DROPPED and recreated:`n  $($toDrop -join ', ')" -ForegroundColor Yellow
  if ((Read-Host "Type 'yes' to continue") -ne 'yes') { Write-Host 'Aborted.'; exit 0 }
}

# --- Teardown (stamped only) ---
foreach ($name in $toDrop) { Invoke-Maria -InFile "$here/endpoint/stamp.sql" -Db $name -Op 'dropIfStamped' | Out-Null }

# --- Bootstrap TestMain (control DB the Initialize templates connect to) ---
Invoke-Maria -InFile "$here/endpoint/bootstrap.sql" | Out-Null

# --- Quench each product, then stamp its DB ---
foreach ($r in $rows | Where-Object Type -eq 'product') {
  $pkg = Join-Path $here $r.Package
  Push-Location $pkg
  $env:SmithySettings_Target__Server = $Server
  $env:SmithySettings_Target__Port = $Port
  $env:SmithySettings_Target__User = $User
  $env:SmithySettings_Target__Password = $Password
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
  Invoke-Maria -InFile "$here/endpoint/stamp.sql" -Db $r.Name -Op 'add' | Out-Null
}
Write-Host "Done. Demo databases provisioned on $Server." -ForegroundColor Green
