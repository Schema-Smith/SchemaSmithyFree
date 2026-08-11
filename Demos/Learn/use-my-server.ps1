# Point the Learn labs at a database server you already run -- no Docker needed.
#
# SOURCE it (note the leading dot) so the settings land in your shell:
#   . .\use-my-server.ps1 -Engine sqlserver -Server myhost -Port 1433 -User sa -Password 'secret'
#   . .\use-my-server.ps1 -Off       # back to the Docker sandbox
#
# Every lab's schemaquench command then targets your server unchanged: SchemaSmith layers
# SmithySettings_* environment variables over each lab's settings file, so nothing in the
# lab packages or their settings needs editing. The lab helper scripts read the matching
# LEARN_* variables to create databases and read catalogs on the same server.
[CmdletBinding()]
param(
    [ValidateSet('sqlserver', 'postgres', 'mysql', 'mariadb')] [string] $Engine,
    [string] $Server,
    [int]    $Port,
    [string] $User,
    [string] $Password,
    [switch] $Off
)

$labVars = 'LEARN_ENGINE', 'LEARN_SERVER', 'LEARN_PORT', 'LEARN_USER', 'LEARN_PASSWORD',
           'SmithySettings_Target__Server', 'SmithySettings_Target__Port',
           'SmithySettings_Target__User', 'SmithySettings_Target__Password'

if ($Off) {
    foreach ($v in $labVars) { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
    Write-Host 'Learn labs are back on the Docker sandbox.' -ForegroundColor Green
    return
}

if (-not $Engine -or -not $Server) {
    Write-Host 'Usage: . .\use-my-server.ps1 -Engine <sqlserver|postgres|mysql|mariadb> -Server <host> [-Port <n>] -User <user> -Password <pw>'
    Write-Host '       . .\use-my-server.ps1 -Off'
    return
}

if ($Server -like '*,*') {
    $parts = $Server.Split(',')
    Write-Host "Pass the host on its own and the port separately: -Server $($parts[0]) -Port $($parts[1])" -ForegroundColor Red
    return
}

if (-not $Port) { $Port = @{ sqlserver = 1433; postgres = 5432; mysql = 3306; mariadb = 3306 }[$Engine] }

# TRAINING-RELEASE-PIN #370 - drop this requirement once a released CLI carries
# Target:IntegratedSecurity (merged to main, unreleased as of 2.3.0). Then accept a missing
# -User/-Password on SQL Server and set SmithySettings_Target__IntegratedSecurity=true.
#
# Windows Authentication isn't expressible here yet: on Windows an environment variable
# cannot be set to empty (assigning '' deletes it), so a lab settings file's "User" can
# never be overridden away, and SchemaSmith selects integrated security only when both
# user and password are empty. Use a SQL login until that product fix ships.
if ([string]::IsNullOrEmpty($User) -or [string]::IsNullOrEmpty($Password)) {
    Write-Host @'
-User and -Password are required for every engine.

Windows Authentication isn't supported by the labs yet: a settings-file credential can't be
cleared by an override on Windows, so the lab's own user would still be used. Create a SQL
login for the labs and pass it here. Windows Authentication follows in a later release.
'@ -ForegroundColor Red
    return
}

$env:LEARN_ENGINE   = $Engine
$env:LEARN_SERVER   = $Server
$env:LEARN_PORT     = "$Port"
$env:LEARN_USER     = $User
$env:LEARN_PASSWORD = $Password

$env:SmithySettings_Target__Server   = $Server
$env:SmithySettings_Target__Port     = "$Port"
$env:SmithySettings_Target__User     = $User
$env:SmithySettings_Target__Password = $Password

Write-Host "Learn labs now target $Engine at ${Server}:${Port} as '$User'." -ForegroundColor Green
Write-Host '  This shell only -- open a new terminal and you will need to source this again.'
Write-Host "  Run each course's setup script before its labs; it creates that course's databases on your server."
switch ($Engine) {
  'sqlserver' { Write-Host '  A few labs need SQL Server 2016 or newer; they say so under "Before you start" and stop at pre-flight if your server is older. See "Will these labs run on my server?" in README.md.' }
  'mysql'     { Write-Host '  A few labs need MySQL 8.0 or newer; they say so under "Before you start" and stop at pre-flight if your server is older. See "Will these labs run on my server?" in README.md.' }
  default     { Write-Host "  Every lab runs at this engine's supported floor -- nothing to check." }
}
Write-Host '  Back to the sandbox: . .\use-my-server.ps1 -Off'
