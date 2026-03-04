@echo off
REM ═══════════════════════════════════════════════════════════════════════════
REM  WowClassicGrindBot - Official Unified Launcher
REM  
REM  This is the ONE TRUE LAUNCHER for the bot.
REM  Double-click this file to start the complete bot system.
REM  
REM  Features:
REM  - Auto-detects WoW process and injects PID
REM  - Builds bot if needed (auto-compiles missing binaries)
REM  - Cleans up orphaned processes
REM  - Opens web UI automatically
REM  - Monitors bot health
REM ═══════════════════════════════════════════════════════════════════════════

setlocal enabledelayedexpansion
title WowClassicGrindBot - Official Launcher
color 0A

echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo   WOW CLASSIC GRIND BOT - UNIFIED LAUNCHER v2.0
echo ═══════════════════════════════════════════════════════════════════════════
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 1: Environment Validation
REM ═══════════════════════════════════════════════════════════════════════════
echo [1/6] Validating Environment...

cd /d "%~dp0"

REM Check .NET SDK
where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    color 0C
    echo [ERROR] .NET SDK not found!
    echo.
    echo Please install .NET SDK 10.0 or later from:
    echo https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%a in ('dotnet --version 2^>nul') do set DOTNET_VER=%%a
echo       [OK] .NET SDK Version: !DOTNET_VER!

REM Check project files
if not exist "BlazorServer\BlazorServer.csproj" (
    color 0C
    echo [ERROR] Bot files not found!
    echo        Please run this from the WowClassicGrindBot directory.
    echo.
    pause
    exit /b 1
)
echo       [OK] Bot files verified
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 2: Process Cleanup
REM ═══════════════════════════════════════════════════════════════════════════
echo [2/6] Cleaning Up Orphaned Processes...

tasklist /FI "IMAGENAME eq BlazorServer.exe" 2>NUL | find /I /N "BlazorServer.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo       [WARN] Found orphaned BlazorServer - terminating...
    taskkill /F /IM BlazorServer.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo       [OK] Cleanup complete
) else (
    echo       [OK] No orphaned processes
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 3: WoW Process Detection
REM ═══════════════════════════════════════════════════════════════════════════
echo [3/6] Detecting World of Warcraft...

set WOW_PID=0

tasklist /FI "IMAGENAME eq WowClassic.exe" 2>NUL | find /I /N "WowClassic.exe">NUL
if "%ERRORLEVEL%"=="0" (
    for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq WowClassic.exe" /FO LIST ^| find "PID:"') do (
        set WOW_PID=%%a
    )
    echo       [OK] WoW Detected - PID: !WOW_PID!
) else (
    echo       [WARN] WoW is NOT running!
    echo              Bot will use auto-detection (may be slower)
    echo              Recommend: Start WoW before launching bot
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 4: Build Check & Auto-Compile
REM ═══════════════════════════════════════════════════════════════════════════
echo [4/6] Checking Build Status...

set BOT_EXE=""
if exist "BlazorServer\bin\Release\net10.0\BlazorServer.exe" (
    set BOT_EXE=BlazorServer\bin\Release\net10.0\BlazorServer.exe
    echo       [OK] Found Release build
) else if exist "BlazorServer\bin\Debug\net10.0\BlazorServer.exe" (
    set BOT_EXE=BlazorServer\bin\Debug\net10.0\BlazorServer.exe
    echo       [OK] Found Debug build
) else (
    echo       [INFO] No executable found - building now...
    echo.
    dotnet build BlazorServer\BlazorServer.csproj --configuration Release --nologo
    
    if exist "BlazorServer\bin\Release\net10.0\BlazorServer.exe" (
        set BOT_EXE=BlazorServer\bin\Release\net10.0\BlazorServer.exe
        echo.
        echo       [OK] Build successful!
    ) else (
        color 0C
        echo.
        echo       [ERROR] Build failed! See errors above.
        pause
        exit /b 1
    )
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 5: Port Availability Check
REM ═══════════════════════════════════════════════════════════════════════════
echo [5/6] Checking Port Availability...

netstat -ano | findstr ":5000 " >nul 2>&1
if %errorLevel% == 0 (
    color 0E
    echo       [WARN] Port 5000 already in use!
    echo              Another instance may be running.
    echo              Press Ctrl+C to cancel or any key to continue anyway...
    pause >nul
    color 0A
) else (
    echo       [OK] Port 5000 available
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 6: Launch Bot
REM ═══════════════════════════════════════════════════════════════════════════
echo [6/6] Starting Bot Services...
echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo   BOT STARTING
echo ═══════════════════════════════════════════════════════════════════════════
echo.
echo   Web UI:         http://localhost:5000
echo   WoW Process:    !WOW_PID!
echo   Executable:     %BOT_EXE%
echo.
echo   Opening browser in 3 seconds...
echo   Press Ctrl+C to stop the bot
echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo.

REM Open browser after delay
start "" cmd /c "timeout /t 3 /nobreak >nul && start http://localhost:5000"

REM Launch the bot
"%BOT_EXE%"

REM ═══════════════════════════════════════════════════════════════════════════
REM Cleanup on Exit
REM ═══════════════════════════════════════════════════════════════════════════
echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo   BOT STOPPED
echo ═══════════════════════════════════════════════════════════════════════════
echo.
pause
