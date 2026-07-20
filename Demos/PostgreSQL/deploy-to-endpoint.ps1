[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string] $Server,
  [int] $Port = 5432,
  [Parameter(Mandatory)] [string] $User,
  [Parameter(Mandatory)] [string] $Password,
  [string] $ManifestPath = "$PSScriptRoot/demo-databases.manifest",
  [switch] $Force
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

# --- Preflight: psql must be present ---
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
  Write-Error @"
psql is required but was not found on PATH.
Install the PostgreSQL client tools and re-open your shell:
  Windows : winget install PostgreSQL.PostgreSQL   (or the EDB installer; adds psql to PATH)
  macOS   : brew install libpq   (then add its bin to PATH) or brew install postgresql
  Linux   : apt-get install postgresql-client   (or dnf install postgresql)
Verify with:  psql --version
"@
  exit 1
}

# psql authenticates from PGPASSWORD; all admin runs connect to the 'postgres'
# maintenance database (you cannot CREATE/DROP the database you are connected to).
$env:PGPASSWORD = $Password

function Invoke-Psql([string]$InFile, [string[]]$Vars = @()) {
  $args = @('-h', $Server, '-p', $Port, '-U', $User, '-d', 'postgres', '-w',
            '-v', 'ON_ERROR_STOP=1', '--no-psqlrc', '-t', '-A')
  foreach ($v in $Vars) { $args += @('-v', $v) }
  $args += @('-f', $InFile)
  $out = & psql @args
  if ($LASTEXITCODE -ne 0) { throw "psql failed: $out" }
  return $out
}

# --- Read manifest ---
$rows = Get-Content $ManifestPath | Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object {
  $f = $_.Split('|'); [pscustomobject]@{ Type=$f[0]; Name=$f[1]; Token=$f[2]; Package=$f[3] }
}

# --- Teardown scan: classify each managed DB ---
$toDrop = @(); $collisions = @()
foreach ($r in $rows) {
  $res = (Invoke-Psql -InFile "$here/endpoint/stamp.sql" -Vars @("op=check", "db=$($r.Name)")) -join ''
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
foreach ($name in $toDrop) { Invoke-Psql -InFile "$here/endpoint/stamp.sql" -Vars @("op=dropIfStamped", "db=$name") | Out-Null }

# --- Bootstrap TestMain (control DB the Initialize templates connect to) ---
Invoke-Psql -InFile "$here/endpoint/bootstrap.sql" | Out-Null

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
  Invoke-Psql -InFile "$here/endpoint/stamp.sql" -Vars @("op=add", "db=$($r.Name)") | Out-Null
}
Write-Host "Done. Demo databases provisioned on $Server." -ForegroundColor Green
