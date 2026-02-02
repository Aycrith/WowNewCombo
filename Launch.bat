@echo off
REM ============================================================================
REM  WowClassicGrindBot - Quick Launcher
REM  Double-click this file to start the complete bot system
REM ============================================================================
TITLE WowClassicGrindBot Launcher

cd /d "%~dp0"

REM Check if PowerShell is available
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Scripts\WowGrindBotLauncher.ps1" %*
) else (
    powershell -ExecutionPolicy Bypass -File "%~dp0Scripts\WowGrindBotLauncher.ps1" %*
)

pause
