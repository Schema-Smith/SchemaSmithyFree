@echo off
setlocal

echo Building SchemaQuench for Docker demos...

:: Detect architecture
if "%PROCESSOR_ARCHITECTURE%"=="ARM64" (
    set RID=linux-arm64
) else (
    set RID=linux-x64
)

echo   Architecture: %RID%

:: Read <IcuVersion> from Directory.Build.props so the verification below stays in
:: lockstep with the package version. Single source of truth.
set ICU_VERSION=
for /f "tokens=2 delims=<>" %%V in ('findstr /R "<IcuVersion>" "%~dp0Directory.Build.props"') do (
    if /I "%%V"=="IcuVersion" (
        rem skip the opening tag itself; the value is in the next token
    ) else (
        set ICU_VERSION=%%V
    )
)
if "%ICU_VERSION%"=="" (
    echo BUILD FAILED: Could not read ^<IcuVersion^> from Directory.Build.props.
    exit /b 1
)

:: Clean publish/ so NuGet native runtime assets (libicu*.so) are always re-staged.
:: Incremental dotnet publish skips re-copying package native assets when MSBuild
:: considers the project up-to-date, which can leave publish/ missing the ICU
:: libraries and crash the Linux container at the first globalization call with
:: "Failed to load app-local ICU: libicudata.so.<IcuVersion>".
if exist "%~dp0SchemaQuench\publish" rmdir /s /q "%~dp0SchemaQuench\publish"

dotnet publish "%~dp0SchemaQuench\SchemaQuench.csproj" -c Release -r %RID% --self-contained -o "%~dp0SchemaQuench\publish"
if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED
    exit /b 1
)

if not exist "%~dp0SchemaQuench\publish\SchemaQuench" (
    echo BUILD FAILED: SchemaQuench\publish\SchemaQuench was not produced. Check the dotnet publish output above.
    echo Likely cause: .NET 10 SDK is not installed, or a NuGet restore step failed silently.
    exit /b 1
)

for %%I in (libicudata libicui18n libicuuc) do (
    if not exist "%~dp0SchemaQuench\publish\%%I.so.%ICU_VERSION%" (
        echo BUILD FAILED: %%I.so.%ICU_VERSION% missing from publish\ -- Microsoft.ICU.ICU4C.Runtime native assets were not staged.
        echo Likely cause: NuGet restore did not pull the linux runtime native assets for this RID.
        exit /b 1
    )
)

echo   Build complete: SchemaQuench/publish/
