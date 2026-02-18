@echo off
REM ==============================================================================
REM  PathingAPI Server Launcher (V1 Remote)
REM  WowClassicGrindBot - Fallback Pathfinder
REM ==============================================================================
TITLE PathingAPI Server - WowClassicGrindBot

echo.
echo  ======================================================
echo   PathingAPI Server (V1 Remote)
echo   Out-of-Process MPQ-Based Pathfinder
echo  ======================================================
echo.

cd /d "C:\WowClassicGrindBot\PathingAPI\bin\Release\net10.0"

REM Check if the build exists
if not exist "PathingAPI.exe" (
    echo  [ERROR] PathingAPI not found!
    echo.
    echo  Please build the solution first:
    echo    cd C:\WowClassicGrindBot
    echo    dotnet build -c Release
    echo.
    pause
    exit /b 1
)

echo  Configuration:
echo    - Host: localhost
echo    - Port: 5001
echo.
echo  Note: This server requires expansion.MPQ in C:\WowClassicGrindBot\Json\MPQ
echo.
echo  Starting PathingAPI server...
echo.

PathingAPI.exe

pause
