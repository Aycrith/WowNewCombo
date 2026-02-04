@echo off
REM ============================================================================
REM  WowClassicGrindBot - One-Click Production Launcher (Windows)
REM  Double-click to bring the full ecosystem online with monitoring.
REM ============================================================================

setlocal
cd /d "%~dp0"

REM Prefer PowerShell 7 if installed
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
  pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\\OneClickLauncher.ps1"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\\OneClickLauncher.ps1"
)

pause

