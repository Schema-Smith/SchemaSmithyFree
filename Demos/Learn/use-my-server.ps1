# Point the Learn labs at a database server you already run -- no Docker needed.
#
# SOURCE it (note the leading dot) so the settings land in your shell:
#   . .\use-my-server.ps1 -Engine sqlserver -Server myhost -Port 1433 -User sa -Password 'secret'
#   . .\use-my-server.ps1 -Engine sqlserver -Server 'localhost\SQLEXPRESS'   # Windows Authentication
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
           'SmithySettings_Target__User', 'SmithySettings_Target__Password',
           'SmithySettings_Target__IntegratedSecurity'

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

# A named SQL Server instance ("host\INSTANCE") routes itself via the browser service and usually
# listens on a dynamic port, so appending a default 1433 would point at the wrong endpoint entirely.
# Leave the port unset for that form and let the instance name do the work.
$IsNamedInstance = $Engine -eq 'sqlserver' -and $Server -like '*\*'
if (-not $Port -and -not $IsNamedInstance) {
    $Port = @{ sqlserver = 1433; postgres = 5432; mysql = 3306; mariadb = 3306 }[$Engine]
}

# SQL Server can use Windows Authentication (2.5.0+): Target:IntegratedSecurity=true supersedes any
# credential the lab settings file carries, which is what makes this expressible at all -- on Windows an
# environment variable cannot be set to empty, so the file's "User" could never be overridden away.
# Every other engine still needs a login.
$UseWindowsAuth = $Engine -eq 'sqlserver' -and [string]::IsNullOrEmpty($User) -and [string]::IsNullOrEmpty($Password)

if (-not $UseWindowsAuth -and ([string]::IsNullOrEmpty($User) -or [string]::IsNullOrEmpty($Password))) {
    if ($Engine -eq 'sqlserver') {
        Write-Host @'
Pass BOTH -User and -Password, or NEITHER.

On SQL Server, omitting both uses Windows Authentication. Supplying only one is ambiguous, so
nothing is assumed.
'@ -ForegroundColor Red
    } else {
        Write-Host @"
-User and -Password are required for $Engine.

Only SQL Server supports Windows Authentication; every other engine needs a login.
"@ -ForegroundColor Red
    }
    return
}

if ($UseWindowsAuth -and $Server -match '^(127\.0\.0\.1|::1|localhost)$' -and $Port) {
    Write-Host @"
Windows Authentication cannot be used with a loopback address and a port.

There is no SPN to resolve for $Server on port $Port, so the connection fails with
"Cannot generate SSPI context" before SchemaSmith is involved. Name the instance instead:
  -Server 'localhost\SQLEXPRESS'   (no -Port)
Or pass a SQL login with -User/-Password.
"@ -ForegroundColor Red
    return
}

$env:LEARN_ENGINE   = $Engine
$env:LEARN_SERVER   = $Server
$env:LEARN_PORT     = "$Port"
$env:LEARN_USER     = $User
$env:LEARN_PASSWORD = $Password

$env:SmithySettings_Target__Server   = $Server
if ($Port) {
    $env:SmithySettings_Target__Port = "$Port"
} else {
    Remove-Item Env:SmithySettings_Target__Port -ErrorAction SilentlyContinue
}
if ($UseWindowsAuth) {
    # Do NOT set User/Password: on Windows, assigning '' deletes the variable, so the settings
    # file's value would win. IntegratedSecurity is what supersedes it.
    Remove-Item Env:SmithySettings_Target__User     -ErrorAction SilentlyContinue
    Remove-Item Env:SmithySettings_Target__Password -ErrorAction SilentlyContinue
    $env:SmithySettings_Target__IntegratedSecurity = 'true'
} else {
    Remove-Item Env:SmithySettings_Target__IntegratedSecurity -ErrorAction SilentlyContinue
    $env:SmithySettings_Target__User     = $User
    $env:SmithySettings_Target__Password = $Password
}

$who   = if ($UseWindowsAuth) { 'Windows Authentication' } else { "'$User'" }
$where = if ($Port) { "${Server}:${Port}" } else { $Server }
Write-Host "Learn labs now target $Engine at $where using $who." -ForegroundColor Green
Write-Host '  This shell only -- open a new terminal and you will need to source this again.'
Write-Host "  Run each course's setup script before its labs; it creates that course's databases on your server."
switch ($Engine) {
  'sqlserver' { Write-Host '  A few labs need SQL Server 2016 or newer; they say so under "Before you start" and stop at pre-flight if your server is older. See "Will these labs run on my server?" in README.md.' }
  'mysql'     { Write-Host '  A few labs need MySQL 8.0 or newer; they say so under "Before you start" and stop at pre-flight if your server is older. See "Will these labs run on my server?" in README.md.' }
  default     { Write-Host "  Every lab runs at this engine's supported floor -- nothing to check." }
}
Write-Host '  Back to the sandbox: . .\use-my-server.ps1 -Off'
