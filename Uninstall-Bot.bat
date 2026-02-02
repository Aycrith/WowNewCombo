@echo off
:: Run the Uninstall-Bot.ps1 script as Administrator

echo ============================================
echo   WowClassicGrindBot Uninstaller
echo ============================================
echo.
echo This will remove bot addon symlinks from WoW.
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: Run the PowerShell script
powershell -ExecutionPolicy Bypass -File "C:\WowClassicGrindBot\Scripts\Uninstall-Bot.ps1"

pause
