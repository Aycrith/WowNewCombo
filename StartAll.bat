@echo off
REM ==============================================================================
REM  WowClassicGrindBot - Full Stack Launcher
REM  Starts Navigation Server + Bot with optimal configuration
REM ==============================================================================
TITLE WowClassicGrindBot Full Stack Launcher

echo.
echo  ========================================================
echo   WowClassicGrindBot Full Stack Launcher
echo   Starting Navigation Server + Bot Interface
echo  ========================================================
echo.

cd /d "C:\WowClassicGrindBot"

REM Check if navigation server is already running
tasklist /FI "IMAGENAME eq AmeisenNavigationServer.exe" 2>NUL | find /I /N "AmeisenNavigationServer.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo  [OK] AmeisenNavigation Server already running
) else (
    REM Check if MMAP files exist before starting
    if exist "Navigation\mmaps\*.mmap" (
        echo  [INFO] Starting AmeisenNavigation Server...
        start "AmeisenNavigation" /D "Navigation" AmeisenNavigationServer.exe
        timeout /t 3 /nobreak > nul
        echo  [OK] AmeisenNavigation Server started on port 47110
    ) else (
        echo  [WARN] No MMAP files found - skipping AmeisenNavigation
        echo         Add MMAPs to Navigation\mmaps\ for best pathfinding
    )
)

echo.

REM Check if expansion.MPQ exists for local fallback
if exist "Json\MPQ\expansion.MPQ" (
    echo  [OK] expansion.MPQ found - Local pathfinder available
) else (
    echo  [WARN] expansion.MPQ not found in Json\MPQ\
    echo         Download from: https://mega.nz/folder/GipyXCyR#-cT2SLwsN01fBD63HJKF7w
)

echo.
echo  Starting WowClassicGrindBot...
echo.

REM Start the bot
cd BlazorServer\bin\Release\net10.0

REM Wait a moment then open browser
start "" http://localhost:5000

BlazorServer.exe

pause
