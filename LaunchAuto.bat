@echo off
TITLE WowClassicGrindBot - Auto Launcher
cd /d "%~dp0"
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Scripts\WowGrindBotLauncher.ps1" -AutoLaunchWoW %*
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0Scripts\WowGrindBotLauncher.ps1" -AutoLaunchWoW %*
)
pause
