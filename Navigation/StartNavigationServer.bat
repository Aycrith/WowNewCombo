@echo off
REM ==============================================================================
REM  AmeisenNavigation Server Launcher
REM  WowClassicGrindBot - V3 Remote Pathfinder
REM ==============================================================================
TITLE AmeisenNavigation Server - WowClassicGrindBot

echo.
echo  ======================================================
echo   AmeisenNavigation Server v1.8.3.2
echo   TCP Navigation Server using TrinityCore MMAPs
echo  ======================================================
echo.

cd /d "%~dp0"

REM Check if mmaps folder exists
if not exist "mmaps" (
    echo  [WARNING] MMAP folder not found!
    echo.
    echo  You need to provide MMAP files for navigation to work.
    echo  Options:
    echo    1. Extract MMAPs using TrinityCore tools (recommended)
    echo    2. Download pre-extracted MMAPs from the internet
    echo.
    echo  The mmaps folder should contain .map and .mmtile files.
    echo  Example: 000.map, 0002727.mmtile, etc.
    echo.
    echo  Creating empty mmaps folder...
    mkdir mmaps
    echo.
    echo  Please add MMAP files to: %~dp0mmaps
    echo.
    pause
    exit /b 1
)

REM Count MMAP files
for /f %%a in ('dir /b /a-d mmaps\*.mmtile 2^>nul ^| find /c /v ""') do set MMTILE_COUNT=%%a
for /f %%a in ('dir /b /a-d mmaps\*.map 2^>nul ^| find /c /v ""') do set MAP_COUNT=%%a

echo  Found %MAP_COUNT% .map files and %MMTILE_COUNT% .mmtile files
echo.

if "%MAP_COUNT%"=="0" (
    echo  [ERROR] No .map files found in mmaps folder!
    echo  Please add MMAP files before starting the server.
    pause
    exit /b 1
)

echo  Configuration:
echo    - IP: 127.0.0.1
echo    - Port: 47111
echo    - MMAP Format: Auto-detect
echo    - Smoothing: Catmull-Rom Spline
echo.
echo  Starting server...
echo.

AmeisenNavigationServer.exe

pause
