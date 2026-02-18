@echo off
:: ============================================
:: WowClassicGrindBot Launcher
:: ============================================
:: This script launches the bot with proper settings
:: and opens the web UI in your default browser.
::
:: Usage: Just double-click to run!
:: ============================================

title WowClassicGrindBot Launcher

set BOT_PATH=C:\WowClassicGrindBot
set BLAZOR_SERVER=%BOT_PATH%\BlazorServer\bin\Release\net10.0\BlazorServer.exe

echo ============================================
echo   WowClassicGrindBot Launcher
echo ============================================
echo.

:: Check if bot exists
if not exist "%BLAZOR_SERVER%" (
    echo [ERROR] BlazorServer.exe not found!
    echo.
    echo Expected path: %BLAZOR_SERVER%
    echo.
    echo Please run the build first:
    echo   cd %BOT_PATH%
    echo   dotnet build -c Release
    echo.
    pause
    exit /b 1
)

:: Check if WoW is running
tasklist /FI "IMAGENAME eq WowClassic.exe" 2>NUL | find /I /N "WowClassic.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [OK] WoW Classic is running
) else (
    echo [WARN] WoW Classic is not running!
    echo        Make sure WoW is running before starting the bot.
    echo.
)

:: Check if addons are installed
set ADDON_PATH=C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns\DataToColor
if exist "%ADDON_PATH%" (
    echo [OK] DataToColor addon is installed
) else (
    echo [WARN] DataToColor addon not found!
    echo        Run Install-Addons.ps1 as Administrator first.
    echo.
)

echo.
echo Starting BlazorServer...
echo Web UI will be available at: http://localhost:5000
echo.
echo Press Ctrl+C to stop the bot.
echo ============================================
echo.

:: Change to bot directory and run
cd /d "%BOT_PATH%\BlazorServer\bin\Release\net10.0"

:: Wait a moment then open browser
start "" cmd /c "timeout /t 3 /nobreak >nul && start http://localhost:5000"

:: Start the server
"%BLAZOR_SERVER%"

echo.
echo Bot stopped.
pause
