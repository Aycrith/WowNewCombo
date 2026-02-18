@echo off
REM ==============================================================================
REM  WowClassicGrindBot - One-Click Launcher
REM  Double-click this file to start the complete bot system
REM ==============================================================================

setlocal enabledelayedexpansion
cd /d "%~dp0"

REM Colors and formatting
cls
echo.
echo  ╔════════════════════════════════════════════════════════════════╗
echo  ║                                                                ║
echo  ║            WowClassicGrindBot - Startup Launcher              ║
echo  ║                                                                ║
echo  ╚════════════════════════════════════════════════════════════════╝
echo.

REM ==============================================================================
REM  SECTION 1: Validate Prerequisites
REM ==============================================================================
echo  [STEP 1] Validating Prerequisites...
echo.

REM Check .NET is installed
where dotnet >nul 2>&1
if errorlevel 1 (
    echo.
    echo  ╔════════════════════════════════════════════════════════════════╗
    echo  ║  ERROR: .NET SDK not found                                     ║
    echo  ║                                                                ║
    echo  ║  Required: .NET 10.0 or later                                  ║
    echo  ║  Download: https://dotnet.microsoft.com/download               ║
    echo  ║                                                                ║
    echo  ║  After installing .NET, restart this launcher.                 ║
    echo  ╚════════════════════════════════════════════════════════════════╝
    echo.
    pause
    exit /b 1
)
echo    ✓ .NET SDK found

REM Check BlazorServer project exists
if not exist "BlazorServer\BlazorServer.csproj" (
    echo.
    echo  ╔════════════════════════════════════════════════════════════════╗
    echo  ║  ERROR: BlazorServer project not found                         ║
    echo  ║                                                                ║
    echo  ║  Expected at: %cd%\BlazorServer\BlazorServer.csproj            ║
    echo  ║                                                                ║
    echo  ║  Please ensure you extracted the bot files correctly.          ║
    echo  ╚════════════════════════════════════════════════════════════════╝
    echo.
    pause
    exit /b 1
)
echo    ✓ BlazorServer project found

REM Check addon config exists
if not exist "addon_config.json" (
    echo.
    echo  ⚠  addon_config.json not found - will be created on first run
)

echo    ✓ Configuration files OK
echo.

REM ==============================================================================
REM  SECTION 2: Build the Project
REM ==============================================================================
echo  [STEP 2] Building Project...
echo.

dotnet build BlazorServer\BlazorServer.csproj --configuration Release --no-restore >nul 2>&1
if errorlevel 1 (
    echo.
    echo  ╔════════════════════════════════════════════════════════════════╗
    echo  ║  ERROR: Build failed                                           ║
    echo  ║                                                                ║
    echo  ║  Try running with administrator privileges:                    ║
    echo  ║  1. Right-click Start.bat                                      ║
    echo  ║  2. Select "Run as administrator"                              ║
    echo  ║                                                                ║
    echo  ║  Or check logs for details (see console output above)           ║
    echo  ╚════════════════════════════════════════════════════════════════╝
    echo.
    pause
    exit /b 1
)
echo    ✓ Build successful
echo.

REM ==============================================================================
REM  SECTION 3: Validate WoW Installation
REM ==============================================================================
echo  [STEP 3] Checking WoW Installation...
echo.

REM Try to auto-detect WoW
if exist "C:\Program Files (x86)\World of Warcraft\_anniversary_\WowClassic.exe" (
    echo    ✓ WoW TBC Anniversary found at default location
    set "WOW_PATH=C:\Program Files (x86)\World of Warcraft\_anniversary_"
) else if exist "C:\Program Files (x86)\World of Warcraft\_classic_\WowClassic.exe" (
    echo    ✓ WoW Classic found at alternate location
    set "WOW_PATH=C:\Program Files (x86)\World of Warcraft\_classic_"
) else (
    echo.
    echo  ⚠  World of Warcraft not found at default locations
    echo.
    echo    Checked locations:
    echo    - C:\Program Files (x86)\World of Warcraft\_anniversary_
    echo    - C:\Program Files (x86)\World of Warcraft\_classic_
    echo.
    echo    The bot will attempt auto-detection when started.
    echo    If WoW is installed elsewhere, please:
    echo.
    echo    1. Start WoW manually
    echo    2. Log into a character
    echo    3. Then start this launcher again
    echo.
)

REM ==============================================================================
REM  SECTION 4: Check Navigation Server
REM ==============================================================================
echo.
echo  [STEP 4] Checking Navigation Server...
echo.

if exist "Navigation\AmeisenNavigationServer.exe" (
    echo    ✓ Navigation Server found
    
    if exist "Navigation\mmaps\*.mmap" (
        echo    ✓ MMAP files available (pathfinding enabled)
    ) else (
        echo    ⚠  No MMAP files found
        echo       Pathfinding will be limited without MMAPs
        echo       See: https://github.com/FreeHongKongMMO/WowClassicGrindBot/wiki/Navigation
    )
) else (
    echo    ⚠  Navigation Server not found
    echo       Bot can run without it, but pathfinding will not work
)

REM ==============================================================================
REM  SECTION 5: Check DataToColor Addon
REM ==============================================================================
echo.
echo  [STEP 5] Checking DataToColor Addon...
echo.

if exist "C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns\DataToColor" (
    echo    ✓ DataToColor addon installed
) else if exist "Addons\DataToColor" (
    echo    ⚠  DataToColor addon found in repo but not installed to WoW
    echo       It will be auto-installed on first run
) else (
    echo    ⚠  DataToColor addon not found
    echo       It will be auto-installed on first run
)

REM ==============================================================================
REM  SECTION 6: Ready to Start
REM ==============================================================================
echo.
echo  ════════════════════════════════════════════════════════════════
echo.
echo    All checks complete! Ready to start WowClassicGrindBot
echo.
echo    IMPORTANT: Please ensure you:
echo.
echo      1. Have World of Warcraft running
echo      2. Are logged into a character
echo      3. Can see the game world (not at login screen)
echo.
echo    Once ready, press any key to continue...
echo.
echo  ════════════════════════════════════════════════════════════════
echo.

pause >nul

REM ==============================================================================
REM  SECTION 7: Launch the Bot
REM ==============================================================================
echo.
echo  Starting WowClassicGrindBot...
echo.
echo  The bot will:
echo    • Auto-detect your WoW installation
echo    • Start the navigation server
echo    • Install required addons
echo    • Open the web interface in your browser (http://localhost:5000)
echo.
echo  To stop the bot, close this window or press Ctrl+C
echo.
echo  ════════════════════════════════════════════════════════════════
echo.

REM Start bot in a separate window so we can open browser
REM We need to wait a moment for the bot to be ready before opening browser
REM Start the bot - using Release build if available, otherwise Debug
if exist "BlazorServer\bin\Release\net10.0\BlazorServer.exe" (
    echo Launching bot (Release build)...
    REM Start bot in separate window
    start "" BlazorServer\bin\Release\net10.0\BlazorServer.exe
    
    REM Wait for bot to start (5 seconds should be enough)
    echo Waiting for bot to start...
    timeout /t 5 /nobreak
    
    REM Open browser to localhost:5000
    echo Opening browser...
    start http://localhost:5000
    
    REM Wait for user to close the bot
    echo.
    echo Bot is running. Browser should open shortly.
    echo Press Ctrl+C in the bot window to stop.
    echo.
    pause
) else (
    echo Launching bot (Debug build)...
    REM For debug builds, we can't easily separate the windows, so just start it
    dotnet run --project BlazorServer\BlazorServer.csproj --no-build
)

REM If we reach here, either Release build exited or Debug build stopped
REM For Release builds started with 'start', this message won't show
REM For Debug builds, this will show when user closes or stops the bot
echo.
echo  ════════════════════════════════════════════════════════════════
echo.
echo  WowClassicGrindBot has stopped.
echo.
echo  To restart, double-click Start.bat again.
echo.
echo  For support: https://github.com/FreeHongKongMMO/WowClassicGrindBot/issues
echo.
echo  ════════════════════════════════════════════════════════════════
echo.

pause
exit /b 0
