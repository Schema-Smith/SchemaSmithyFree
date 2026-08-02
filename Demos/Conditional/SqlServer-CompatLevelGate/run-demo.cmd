@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\..\.."
set "REPO_ROOT=%CD%"
popd

echo Publishing SchemaQuench...
call "%REPO_ROOT%\build-schemaquench.cmd"
if errorlevel 1 exit /b %errorlevel%

echo Starting SQL Server compatibility-level demo...
cd /d "%SCRIPT_DIR%"

docker compose up --build --abort-on-container-exit verify
