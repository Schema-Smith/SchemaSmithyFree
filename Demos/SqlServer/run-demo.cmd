@echo off
setlocal

:: Build SchemaQuench if not already built
if not exist "%~dp0..\..\SchemaQuench\publish\SchemaQuench" (
    echo SchemaQuench not built yet, building...
    call "%~dp0..\..\build-schemaquench.cmd"
    if %ERRORLEVEL% neq 0 exit /b 1
)

echo Starting SQL Server demo...
:: `up -d` blocks on the service_completed_successfully chain, so by the time it
:: returns every quench has finished. Don't follow up with `docker compose wait
:: completed` -- it races with the completed service's cleanup and Docker Desktop
:: emits "no containers for project" when the container is already gone.
docker compose up --build -d
