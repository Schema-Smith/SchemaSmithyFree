@echo off
setlocal

if not exist "%~dp0..\..\SchemaQuench\publish\SchemaQuench" (
    echo SchemaQuench not built yet, building...
    call "%~dp0..\..\build-schemaquench.cmd"
    if %ERRORLEVEL% neq 0 exit /b 1
)

echo Starting PostgreSQL demo...
docker compose up --build -d
docker compose wait completed
