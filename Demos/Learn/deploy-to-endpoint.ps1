[CmdletBinding()]
param(
  [Parameter(Mandatory)] [ValidateSet('sqlserver','postgres','mysql','mariadb')] [string] $Engine,
  [Parameter(Mandatory)] [string] $Server,
  [int] $Port,
  [string] $User,
  [string] $Password,
  [switch] $Force
)
$ErrorActionPreference = 'Stop'

# The no-Docker sibling of Demos/Learn/docker: creates (or cleanly resets) the empty,
# helper-owned `learn` sandbox database on a server you already run, so a learner can do
# the labs without Docker. It stamps the database it creates and only ever drops a stamped
# `learn` — an existing `learn` it didn't create is refused, never touched.
$db = 'learn'
$stamp = 'SchemaSmith_DemoProvisioned'

# --- Per-engine client + default port ---
$client = @{ sqlserver='sqlcmd'; postgres='psql'; mysql='mysql'; mariadb='mariadb' }[$Engine]
if (-not $Port) { $Port = @{ sqlserver=1433; postgres=5432; mysql=3306; mariadb=3306 }[$Engine] }

if (-not (Get-Command $client -ErrorAction SilentlyContinue)) {
  Write-Error "$client is required for -Engine $Engine but was not found on PATH. Install the engine's command-line client and re-open your shell (see docs/end-user/guide/use-your-own-server.md), then verify with '$client --version'."
  exit 1
}
$windowsAuth = ($Engine -eq 'sqlserver' -and [string]::IsNullOrEmpty($User))
if ($Engine -ne 'sqlserver' -and [string]::IsNullOrEmpty($User)) {
  Write-Error "-User and -Password are required for -Engine $Engine (only SQL Server supports Windows Authentication)."
  exit 1
}

# --- Invoke-Sql: run a SQL batch on the target, return stdout ---
function Invoke-Sql([string]$Sql) {
  switch ($Engine) {
    'sqlserver' {
      $srv = "$Server,$Port"
      $a = @('-S', $srv, '-C', '-b', '-h', '-1', '-W')
      if ($windowsAuth) { $a += '-E' } else { $a += @('-U', $User, '-P', $Password) }
      $out = $Sql | & sqlcmd @a
    }
    'postgres' {
      $env:PGPASSWORD = $Password
      $out = $Sql | & psql -h $Server -p $Port -U $User -d postgres -w -v ON_ERROR_STOP=1 --no-psqlrc -t -A
    }
    default {   # mysql / mariadb
      $env:MYSQL_PWD = $Password
      $out = $Sql | & $client -h $Server -P $Port -u $User -N -s
    }
  }
  if ($LASTEXITCODE -ne 0) { throw "$client failed: $out" }
  return ($out -join "`n")
}

# --- Per-engine SQL: check / create+stamp / drop ---
$checkSql = switch ($Engine) {
  'sqlserver' { @"
SET NOCOUNT ON;
IF DB_ID('$db') IS NULL BEGIN PRINT 'STAMP_RESULT:absent'; RETURN; END;
DECLARE @s BIT;
EXEC sp_executesql N'SELECT @s = CASE WHEN EXISTS (SELECT 1 FROM [$db].sys.extended_properties WHERE class = 0 AND name = ''$stamp'') THEN 1 ELSE 0 END', N'@s BIT OUTPUT', @s = @s OUTPUT;
PRINT 'STAMP_RESULT:' + CASE WHEN @s = 1 THEN 'stamped' ELSE 'unstamped' END;
"@ }
  'postgres' { @"
SELECT CASE
  WHEN NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = '$db') THEN 'STAMP_RESULT:absent'
  WHEN COALESCE((SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname = '$db'), '') = '$stamp' THEN 'STAMP_RESULT:stamped'
  ELSE 'STAMP_RESULT:unstamped' END;
"@ }
  default { @"
SELECT CONCAT('STAMP_RESULT:', CASE
  WHEN NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = '$db') THEN 'absent'
  WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '$db' AND table_name = '$stamp') THEN 'stamped'
  ELSE 'unstamped' END);
"@ }
}
$createSql = switch ($Engine) {
  'sqlserver' { "CREATE DATABASE [$db]; EXEC [$db].sys.sp_addextendedproperty @name = N'$stamp', @value = N'1';" }
  'postgres'  { "CREATE DATABASE ""$db""; COMMENT ON DATABASE ""$db"" IS '$stamp';" }
  default     { "CREATE DATABASE ``$db`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE TABLE ``$db``.``$stamp`` (marker TINYINT NOT NULL);" }
}
$dropSql = switch ($Engine) {
  'sqlserver' { "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db];" }
  'postgres'  { "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$db' AND pid <> pg_backend_pid(); DROP DATABASE IF EXISTS ""$db"";" }
  default     { "DROP DATABASE IF EXISTS ``$db``;" }
}

# --- Classify, then reset + create ---
$res = Invoke-Sql $checkSql
if ($res -match 'STAMP_RESULT:unstamped') {
  Write-Error @"
A database named '$db' already exists on $Server but was NOT created by this helper.
Refusing to drop a database the helper didn't create. If that '$db' is yours, move or
rename it first; the helper always manages a database literally named '$db'.
"@
  exit 2
}
$exists = $res -match 'STAMP_RESULT:stamped'
if ($exists -and -not $Force) {
  Write-Host "The '$db' sandbox database on $Server WILL BE DROPPED and recreated empty." -ForegroundColor Yellow
  if ((Read-Host "Type 'yes' to continue") -ne 'yes') { Write-Host 'Aborted.'; exit 0 }
}
if ($exists) { Invoke-Sql $dropSql | Out-Null }
Invoke-Sql $createSql | Out-Null
Write-Host "Done. Empty '$db' sandbox ready on $Server ($Engine). Point your connect.settings.json at it and kindle the forge." -ForegroundColor Green
