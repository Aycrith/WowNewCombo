@echo off
setlocal enabledelayedexpansion

echo ========================================================================
echo WowClassicGrindBot - Comprehensive Diagnostic Startup
echo ========================================================================
echo.

REM Step 1: Environment Check
echo [STEP 1] Environment Diagnostics
echo ----------------------------------------

REM Check if running as admin
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Running as Administrator
) else (
    echo [WARNING] Not running as Administrator
    echo [ACTION] Some features may require admin privileges
    echo.
    echo Attempting to restart as Administrator...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

REM Check WoW process
echo.
echo [STEP 2] WoW Process Detection
echo ----------------------------------------
tasklist /FI "IMAGENAME eq WowClassic.exe" 2>NUL | find /I /N "WowClassic.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [OK] WoW Process Found: WowClassic.exe
    for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq WowClassic.exe" /FO LIST ^| find "PID:"') do (
        set WOW_PID=%%a
        echo [INFO] WoW Process ID: !WOW_PID!
    )
) else (
    echo [ERROR] WoW is NOT running!
    echo.
    echo CRITICAL: You must start World of Warcraft first.
    echo.
    echo Please:
    echo   1. Start Battle.net
    echo   2. Launch World of Warcraft
    echo   3. Log into a character
    echo   4. Wait until you can see the game world
    echo   5. Then run this script again
    echo.
    pause
    exit /b 1
)

REM Get WoW executable path using WMIC
echo.
echo [STEP 3] WoW Installation Path Detection
echo ----------------------------------------
for /f "tokens=2 delims==" %%a in ('wmic process where "name='WowClassic.exe'" get ExecutablePath /value 2^>nul ^| find "="') do (
    set WOW_EXE_PATH=%%a
)

if defined WOW_EXE_PATH (
    echo [OK] WoW Executable: !WOW_EXE_PATH!
    for %%i in ("!WOW_EXE_PATH!") do set WOW_DIR=%%~dpi
    echo [OK] WoW Directory: !WOW_DIR!
) else (
    echo [WARNING] Could not detect WoW path via WMIC
    echo [ACTION] Trying standard locations...
    
    if exist "C:\Program Files (x86)\World of Warcraft\_anniversary_\WowClassic.exe" (
        set "WOW_DIR=C:\Program Files (x86)\World of Warcraft\_anniversary_\"
        echo [OK] Found at standard location: !WOW_DIR!
    ) else if exist "C:\Program Files (x86)\World of Warcraft\_classic_\WowClassic.exe" (
        set "WOW_DIR=C:\Program Files (x86)\World of Warcraft\_classic_\"
        echo [OK] Found at classic location: !WOW_DIR!
    ) else (
        echo [ERROR] Cannot detect WoW installation path
        echo.
        pause
        exit /b 1
    )
)

REM Check .NET
echo.
echo [STEP 4] .NET SDK Verification
echo ----------------------------------------
where dotnet >nul 2>&1
if %errorLevel% == 0 (
    for /f "tokens=*" %%a in ('dotnet --version 2^>nul') do set DOTNET_VER=%%a
    echo [OK] .NET SDK Version: !DOTNET_VER!
) else (
    echo [ERROR] .NET SDK not found
    echo [ACTION] Install from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Check ports
echo.
echo [STEP 5] Port Availability Check
echo ----------------------------------------
netstat -ano | findstr ":5000 " >nul 2>&1
if %errorLevel% == 0 (
    echo [WARNING] Port 5000 is in use
    echo [ACTION] Will attempt to use alternate port
) else (
    echo [OK] Port 5000 is available
)

netstat -ano | findstr ":47110 " >nul 2>&1
if %errorLevel% == 0 (
    echo [WARNING] Port 47110 is in use (Navigation Server)
) else (
    echo [OK] Port 47110 is available
)

REM Update appsettings.json with WoW PID
echo.
echo [STEP 6] Configuration Update
echo ----------------------------------------
if defined WOW_PID (
    echo [ACTION] Updating appsettings.json with WoW PID: !WOW_PID!
    
    REM Backup original
    copy /Y BlazorServer\appsettings.json BlazorServer\appsettings.json.bak >nul 2>&1
    
    REM Update Process.Id using PowerShell
    powershell -Command "(Get-Content 'BlazorServer\appsettings.json') -replace '\"Id\": -1', '\"Id\": !WOW_PID!' | Set-Content 'BlazorServer\appsettings.json'"
    
    echo [OK] Configuration updated with PID: !WOW_PID!
) else (
    echo [WARNING] WoW PID not detected, using auto-detection
)

REM Launch bot
echo.
echo [STEP 7] Bot Startup
echo ========================================================================
echo.
echo Starting WowClassicGrindBot...
echo.
echo The bot will:
echo   - Connect to WoW process (PID: !WOW_PID!)
echo   - Start navigation server on port 47110
echo   - Launch web UI on port 5000
echo   - Open browser automatically
echo.
echo Browser will open to: http://localhost:5000
echo.
echo To stop the bot, close this window or press Ctrl+C
echo.
echo ========================================================================
echo.

REM Start bot
if exist "BlazorServer\bin\Release\net10.0\BlazorServer.exe" (
    echo [INFO] Launching Release build...
    BlazorServer\bin\Release\net10.0\BlazorServer.exe
) else (
    echo [INFO] Launching Debug build...
    dotnet run --project BlazorServer\BlazorServer.csproj
)

REM Restore original config on exit
if exist "BlazorServer\appsettings.json.bak" (
    copy /Y BlazorServer\appsettings.json.bak BlazorServer\appsettings.json >nul 2>&1
    del BlazorServer\appsettings.json.bak >nul 2>&1
)

echo.
echo Bot has stopped.
echo.
pause
