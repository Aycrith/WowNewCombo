@echo off
TITLE WowClassicGrindBot - Service Status
cd /d "%~dp0"
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Scripts\ServiceMonitor.ps1" -Status %*
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0Scripts\ServiceMonitor.ps1" -Status %*
)
pause
