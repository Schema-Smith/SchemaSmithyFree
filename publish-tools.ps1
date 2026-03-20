# Publish SchemaSmithyFree CLI tools for local development
param(
    [string]$Rid
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDir = Join-Path (Join-Path $ScriptDir "Release") "Tools"

if (-not $Rid) {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $Rid = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "win-arm64" } else { "win-x64" }
    } elseif ($IsLinux) {
        $Rid = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "linux-arm64" } else { "linux-x64" }
    } elseif ($IsMacOS) {
        $Rid = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "osx-arm64" } else { "osx-x64" }
    } else {
        Write-Error "Cannot detect RID. Usage: .\publish-tools.ps1 -Rid <rid>"
        exit 1
    }
}

Write-Host "Publishing tools to $OutputDir (RID: $Rid)..."
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

foreach ($tool in @("SchemaQuench", "SchemaTongs", "DataTongs")) {
    Write-Host "  Publishing $tool..."
    dotnet publish "$ScriptDir/$tool/$tool.csproj" -c Release -r $Rid -o $OutputDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Done. Tools published to $OutputDir"
