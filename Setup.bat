@echo off
TITLE WowClassicGrindBot - Setup Wizard
cd /d "%~dp0"
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Scripts\SetupWizard.ps1" %*
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0Scripts\SetupWizard.ps1" %*
)
pause
