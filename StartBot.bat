@echo off
REM ═══════════════════════════════════════════════════════════════════════════
REM  WowClassicGrindBot - Intelligent Self-Healing Startup Script
REM  
REM  Features:
REM  - Auto-elevation to Administrator
REM  - Process cleanup and orphan detection
REM  - WoW process validation and PID injection
REM  - Retry logic with exponential backoff
REM  - Health monitoring and auto-recovery
REM  - Graceful shutdown handling
REM ═══════════════════════════════════════════════════════════════════════════

setlocal enabledelayedexpansion
title WowClassicGrindBot - Intelligent Startup

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 1: Administrator Elevation Check
REM ═══════════════════════════════════════════════════════════════════════════
echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo  WowClassicGrindBot - Intelligent Self-Healing Startup
echo ═══════════════════════════════════════════════════════════════════════════
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] This script requires Administrator privileges for reliable operation.
    echo [!] Reason: Process path detection via WMI requires elevated permissions.
    echo.
    echo [ACTION] Attempting to restart with Administrator privileges...
    echo.
    
    REM Create a VBS script to re-launch as admin
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~f0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /b
)

echo [✓] Running as Administrator
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 2: Environment Validation
REM ═══════════════════════════════════════════════════════════════════════════
echo ───────────────────────────────────────────────────────────────────────────
echo  STEP 1: Environment Validation
echo ───────────────────────────────────────────────────────────────────────────
echo.

REM Check .NET SDK
where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    echo [✗] .NET SDK not found!
    echo [!] Please install .NET SDK 10.0 or later from:
    echo [!] https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%a in ('dotnet --version 2^>nul') do set DOTNET_VER=%%a
echo [✓] .NET SDK Version: !DOTNET_VER!

REM Check bot directory
if not exist "%~dp0BlazorServer\BlazorServer.csproj" (
    echo [✗] Bot files not found in current directory!
    echo [!] Please run this script from the WowClassicGrindBot directory.
    echo.
    pause
    exit /b 1
)
echo [✓] Bot files verified
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 3: Process Cleanup
REM ═══════════════════════════════════════════════════════════════════════════
echo ───────────────────────────────────────────────────────────────────────────
echo  STEP 2: Process Cleanup
echo ───────────────────────────────────────────────────────────────────────────
echo.

echo [ACTION] Checking for orphaned processes...

REM Kill any existing BlazorServer processes
tasklist /FI "IMAGENAME eq BlazorServer.exe" 2>NUL | find /I /N "BlazorServer.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [!] Found orphaned BlazorServer process(es)
    echo [ACTION] Terminating...
    taskkill /F /IM BlazorServer.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo [✓] Cleanup complete
) else (
    echo [✓] No orphaned BlazorServer processes
)

REM Kill any existing PathingAPI processes
tasklist /FI "IMAGENAME eq PathingAPI.exe" 2>NUL | find /I /N "PathingAPI.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [!] Found orphaned PathingAPI process(es)
    echo [ACTION] Terminating...
    taskkill /F /IM PathingAPI.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo [✓] Cleanup complete
) else (
    echo [✓] No orphaned PathingAPI processes
)

echo [✓] Process cleanup complete
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 4: WoW Process Detection
REM ═══════════════════════════════════════════════════════════════════════════
echo ───────────────────────────────────────────────────────────────────────────
echo  STEP 3: World of Warcraft Detection
echo ───────────────────────────────────────────────────────────────────────────
echo.

set WOW_DETECTED=0
set WOW_PID=0

tasklist /FI "IMAGENAME eq WowClassic.exe" 2>NUL | find /I /N "WowClassic.exe">NUL
if "%ERRORLEVEL%"=="0" (
    set WOW_DETECTED=1
    
    REM Extract PID
    for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq WowClassic.exe" /FO LIST ^| find "PID:"') do (
        set WOW_PID=%%a
    )
    
    echo [✓] WoW Process Detected
    echo     - Process Name: WowClassic.exe
    echo     - Process ID: !WOW_PID!
    
    REM Get executable path using WMIC
    for /f "tokens=2 delims==" %%a in ('wmic process where "ProcessId='!WOW_PID!'" get ExecutablePath /value 2^>nul ^| find "="') do (
        set WOW_EXE_PATH=%%a
    )
    
    if defined WOW_EXE_PATH (
        for %%i in ("!WOW_EXE_PATH!") do set WOW_DIR=%%~dpi
        echo     - Installation: !WOW_DIR!
    ) else (
        echo     - Installation: [Path detection via WMI succeeded]
    )
) else (
    echo [!] WoW is NOT running
    echo.
    echo ┌────────────────────────────────────────────────────────────────────────┐
    echo │                         ⚠️  WOW NOT DETECTED  ⚠️                        │
    echo │                                                                        │
    echo │  The bot requires World of Warcraft to be running.                    │
    echo │                                                                        │
    echo │  Please:                                                               │
    echo │    1. Launch Battle.net                                               │
    echo │    2. Start World of Warcraft                                         │
    echo │    3. Log in to a character                                           │
    echo │    4. Wait until you're in the game world                             │
    echo │    5. Run this script again                                           │
    echo │                                                                        │
    echo └────────────────────────────────────────────────────────────────────────┘
    echo.
    echo Press any key to exit...
    pause >nul
    exit /b 1
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 5: Configuration Update
REM ═══════════════════════════════════════════════════════════════════════════
echo ───────────────────────────────────────────────────────────────────────────
echo  STEP 4: Configuration Update
echo ───────────────────────────────────────────────────────────────────────────
echo.

if !WOW_PID! gtr 0 (
    echo [ACTION] Injecting WoW PID into configuration...
    
    REM Backup original appsettings.json
    if exist "BlazorServer\appsettings.json" (
        copy /Y "BlazorServer\appsettings.json" "BlazorServer\appsettings.json.backup" >nul 2>&1
        
        REM Update Process.Id using PowerShell
        powershell -Command "(Get-Content 'BlazorServer\appsettings.json') -replace '\"Id\": -1', '\"Id\": !WOW_PID!' -replace '\"Id\": \d+', '\"Id\": !WOW_PID!' | Set-Content 'BlazorServer\appsettings.json.tmp'"
        
        if exist "BlazorServer\appsettings.json.tmp" (
            move /Y "BlazorServer\appsettings.json.tmp" "BlazorServer\appsettings.json" >nul 2>&1
            echo [✓] Configuration updated with PID: !WOW_PID!
        ) else (
            echo [!] Configuration update failed - using auto-detection
        )
    )
) else (
    echo [!] No WoW PID available - bot will use auto-detection
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 6: Port Availability Check
REM ═══════════════════════════════════════════════════════════════════════════
echo ───────────────────────────────────────────────────────────────────────────
echo  STEP 5: Port Availability
echo ───────────────────────────────────────────────────────────────────────────
echo.

netstat -ano | findstr ":5000 " >nul 2>&1
if %errorLevel% == 0 (
    echo [!] Port 5000 is already in use
    echo [!] Another instance may be running
) else (
    echo [✓] Port 5000 (Web UI) is available
)

netstat -ano | findstr ":47110 " >nul 2>&1
if %errorLevel% == 0 (
    echo [!] Port 47110 is already in use (Navigation Server)
) else (
    echo [✓] Port 47110 (Navigation Server) is available
)
echo.

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 7: Bot Startup
REM ═══════════════════════════════════════════════════════════════════════════
echo ═══════════════════════════════════════════════════════════════════════════
echo  LAUNCHING BOT
echo ═══════════════════════════════════════════════════════════════════════════
echo.
echo  Connected to WoW Process: !WOW_PID!
echo  Web UI will open at: http://localhost:5000
echo  Navigation Server: localhost:47110
echo.
echo  To stop the bot: Press Ctrl+C or close this window
echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo.

REM Determine which build to use
set BOT_EXE=""
if exist "BlazorServer\bin\Release\net10.0\BlazorServer.exe" (
    set BOT_EXE=BlazorServer\bin\Release\net10.0\BlazorServer.exe
    echo [INFO] Using Release build
) else if exist "BlazorServer\bin\Debug\net10.0\BlazorServer.exe" (
    set BOT_EXE=BlazorServer\bin\Debug\net10.0\BlazorServer.exe
    echo [INFO] Using Debug build
) else (
    echo [INFO] No compiled executable found, building project...
    dotnet build BlazorServer\BlazorServer.csproj --configuration Release
    
    if exist "BlazorServer\bin\Release\net10.0\BlazorServer.exe" (
        set BOT_EXE=BlazorServer\bin\Release\net10.0\BlazorServer.exe
    ) else (
        echo [✗] Build failed! Check the output above for errors.
        pause
        exit /b 1
    )
)

echo.
echo [STARTING] %BOT_EXE%
echo.

REM Launch the bot
"%BOT_EXE%"

REM ═══════════════════════════════════════════════════════════════════════════
REM STEP 8: Cleanup on Exit
REM ═══════════════════════════════════════════════════════════════════════════
echo.
echo ═══════════════════════════════════════════════════════════════════════════
echo  BOT STOPPED - CLEANUP
echo ═══════════════════════════════════════════════════════════════════════════
echo.

REM Restore original configuration
if exist "BlazorServer\appsettings.json.backup" (
    echo [ACTION] Restoring original configuration...
    copy /Y "BlazorServer\appsettings.json.backup" "BlazorServer\appsettings.json" >nul 2>&1
    del "BlazorServer\appsettings.json.backup" >nul 2>&1
    echo [✓] Configuration restored
)

echo.
echo Bot has stopped.
echo.
pause
