@echo off
TITLE WowClassicGrindBot - Troubleshooter
cd /d "%~dp0"
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Scripts\Troubleshooter.ps1" %*
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0Scripts\Troubleshooter.ps1" %*
)
pause
