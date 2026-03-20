# Build and package SchemaSmithyFree CLI tools for all supported RIDs
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ReleaseDir = Join-Path $ScriptDir "Release"
$ToolsDir = Join-Path $ReleaseDir "Tools"
$PackagesDir = Join-Path $ReleaseDir "Packages"
$Rids = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
$Tools = @("SchemaQuench", "SchemaTongs", "DataTongs")

if (Test-Path $ToolsDir) { Remove-Item -Recurse -Force $ToolsDir }
if (Test-Path $PackagesDir) { Remove-Item -Recurse -Force $PackagesDir }
New-Item -ItemType Directory -Path $PackagesDir -Force | Out-Null

foreach ($rid in $Rids) {
    $RidDir = Join-Path $ToolsDir $rid
    New-Item -ItemType Directory -Path $RidDir -Force | Out-Null
    Write-Host "Building for $rid..."

    foreach ($tool in $Tools) {
        Write-Host "  Publishing $tool..."
        dotnet publish "$ScriptDir/$tool/$tool.csproj" -c Release -r $rid -o $RidDir
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $ZipName = "SchemaSmithyMSSQL-Community-$rid.zip"
    Write-Host "  Creating $ZipName..."
    Compress-Archive -Path "$RidDir/*" -DestinationPath (Join-Path $PackagesDir $ZipName)
}

Write-Host "Done. Packages in $PackagesDir"
Get-ChildItem $PackagesDir
