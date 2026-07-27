# Shared connection helper for the Learn labs.
#
# Sandbox (default): runs each engine's client inside its container, exactly as the labs
# always have. Own server (LEARN_SERVER set by use-my-server.ps1): runs the engine's
# client on your machine against your endpoint. The output is identical either way, so
# callers never care which path ran.
#
# Dot-source it as a library:  . ..\lab-sql.ps1
# Or call it as a command:     ..\lab-sql.ps1 sqlserver ordersservice_prod "SELECT 1"

function Test-LabOwnServer { return -not [string]::IsNullOrWhiteSpace($env:LEARN_SERVER) }

function Get-LabEngines {
    if (-not (Test-LabOwnServer)) { return @('sqlserver', 'postgres', 'mysql', 'mariadb') }
    if ([string]::IsNullOrWhiteSpace($env:LEARN_ENGINE)) {
        throw 'LAB-SQL: LEARN_SERVER is set but LEARN_ENGINE is not. Re-run use-my-server.ps1.'
    }
    return @($env:LEARN_ENGINE)
}

function Get-LabEndpointLabel {
    param([Parameter(Mandatory)][string]$Engine)
    if (Test-LabOwnServer) { return "$($env:LEARN_SERVER):$($env:LEARN_PORT)" }
    return "learn-$Engine (container)"
}

function Get-LabClient {
    param([Parameter(Mandatory)][string]$Engine)
    $client = @{ sqlserver = 'sqlcmd'; postgres = 'psql'; mysql = 'mysql'; mariadb = 'mariadb' }[$Engine]
    if (-not (Get-Command $client -ErrorAction SilentlyContinue)) {
        throw "LAB-SQL: '$client' is required to reach your own $Engine server but is not on PATH. Install the engine's command-line client (see docs/end-user/guide/use-your-own-server.md), re-open your shell, and verify with '$client --version'."
    }
    return $client
}

function Get-LabContainer {
    param([Parameter(Mandatory)][string]$Engine)
    return @{ sqlserver = 'learn-sqlserver'; postgres = 'learn-postgres'; mysql = 'learn-mysql'; mariadb = 'learn-mariadb' }[$Engine]
}

# PowerShell 5.1 wraps a native command's redirected stderr in ErrorRecords, which
# $ErrorActionPreference='Stop' (set by most lab scripts) turns into a terminating error
# before we can inspect the exit code. Reading them as plain text keeps our own message
# the one the learner sees.
function ConvertTo-LabText($Streams) {
    $lines = $Streams | ForEach-Object {
        if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.Exception.Message } else { $_ }
    }
    return (($lines -join [Environment]::NewLine).Trim())
}

function Invoke-LabSql {
    param(
        [Parameter(Mandatory)][string]$Engine,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Sql
    )
    $ErrorActionPreference = 'Continue'
    $out = $null
    if (Test-LabOwnServer) {
        $client = Get-LabClient $Engine
        $server = $env:LEARN_SERVER; $port = $env:LEARN_PORT
        $user = $env:LEARN_USER; $password = $env:LEARN_PASSWORD
        switch ($Engine) {
            'sqlserver' { $out = & sqlcmd -S "$server,$port" -U $user -P $password -C -b -d $Database -h -1 -W -Q "SET NOCOUNT ON; $Sql" 2>&1 }
            'postgres'  { $env:PGPASSWORD = $password
                          $out = & psql -h $server -p $port -U $user -d $Database -w -v ON_ERROR_STOP=1 --no-psqlrc -tAc $Sql 2>&1 }
            default     { $env:MYSQL_PWD = $password
                          $out = & $client -h $server -P $port -u $user -N -s -D $Database -e $Sql 2>&1 }
        }
    }
    else {
        $container = Get-LabContainer $Engine
        switch ($Engine) {
            'sqlserver' { $out = docker exec $container /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Learn!Passw0rd' -C -b -d $Database -h -1 -W -Q "SET NOCOUNT ON; $Sql" 2>&1 }
            'postgres'  { $out = docker exec $container psql -U postgres -d $Database -v ON_ERROR_STOP=1 -tAc $Sql 2>&1 }
            'mysql'     { $out = docker exec -e MYSQL_PWD=Learn!Passw0rd $container mysql -uroot -N -s -D $Database -e $Sql 2>&1 }
            'mariadb'   { $out = docker exec -e MYSQL_PWD=Learn!Passw0rd $container mariadb -uroot -N -s -D $Database -e $Sql 2>&1 }
        }
    }
    $text = ConvertTo-LabText $out
    if ($LASTEXITCODE -ne 0) {
        throw "LAB-SQL: could not reach $Engine at $(Get-LabEndpointLabel $Engine) [database '$Database'] -- $text"
    }
    return $text
}

# Called as a command rather than dot-sourced: lab-sql.ps1 <engine> <database> <sql>
if ($MyInvocation.InvocationName -ne '.' -and $args.Count -ge 3) {
    Invoke-LabSql -Engine $args[0] -Database $args[1] -Sql ($args[2..($args.Count - 1)] -join ' ')
}
