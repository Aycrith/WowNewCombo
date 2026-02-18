@echo off
:: Run the Install-Addons.ps1 script as Administrator
:: This batch file requests elevation automatically

echo ============================================
echo   WowClassicGrindBot Addon Installer
echo ============================================
echo.
echo This will create symbolic links for bot addons
echo in your WoW AddOns folder.
echo.
echo Requires Administrator privileges.
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: Run the PowerShell script
powershell -ExecutionPolicy Bypass -File "C:\WowClassicGrindBot\Scripts\Install-Addons.ps1"

pause
