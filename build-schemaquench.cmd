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

dotnet publish "%~dp0SchemaQuench\SchemaQuench.csproj" -c Release -r %RID% --self-contained -o "%~dp0SchemaQuench\publish"
if %ERRORLEVEL% neq 0 (
    echo BUILD FAILED
    exit /b 1
)

echo   Build complete: SchemaQuench/publish/
