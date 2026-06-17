# Verify the SchemaSmith Learn sandbox: wait for each engine to report healthy,
# then run a trivial connection/query against it. Prints PASS/FAIL per engine.

$ErrorActionPreference = 'Continue'
$failed = $false

function Wait-Healthy {
    param([string]$Name, [int]$Tries = 90)
    for ($i = 0; $i -lt $Tries; $i++) {
        $status = (docker inspect -f '{{.State.Health.Status}}' $Name 2>$null)
        if ($LASTEXITCODE -ne 0) { return 'missing' }
        if ($status -eq 'healthy') { return 'healthy' }
        Start-Sleep -Seconds 2
    }
    return 'timeout'
}

function Test-Engine {
    param([string]$Label, [string]$Container, [string[]]$Query)
    Write-Host -NoNewline ("{0,-12} " -f $Label)
    switch (Wait-Healthy $Container) {
        'missing' { Write-Host "FAIL (container '$Container' not found - is the sandbox up?)"; $script:failed = $true; return }
        'timeout' { Write-Host "FAIL (never became healthy)"; $script:failed = $true; return }
    }
    docker exec $Container @Query *> $null
    if ($LASTEXITCODE -eq 0) { Write-Host "PASS" } else { Write-Host "FAIL (could not run a query)"; $script:failed = $true }
}

Write-Host "Verifying the SchemaSmith Learn sandbox (this can take a minute on first start)..."
Test-Engine "SQL Server" "learn-sqlserver" @('bash', '-c', 'exec 3<>/dev/tcp/localhost/1433')
Test-Engine "PostgreSQL" "learn-postgres"  @('psql', '-U', 'postgres', '-d', 'learn', '-tAc', 'SELECT 1')
Test-Engine "MySQL"      "learn-mysql"     @('mysql', '-uroot', '-pLearn!Passw0rd', '-N', '-e', 'SELECT 1')

Write-Host ""
if (-not $failed) {
    Write-Host "All three engines are ready."
    exit 0
} else {
    Write-Host "One or more engines failed. Try again in a minute, or check 'docker compose logs'."
    exit 1
}
