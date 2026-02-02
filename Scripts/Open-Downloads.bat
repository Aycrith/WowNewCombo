@echo off
REM Direct download manager for MEGA files using browser
REM This opens the downloads in your default browser where you can manage them

echo ========================================
echo   MEGA File Download Helper
echo ========================================
echo.

echo Opening MEGA download links in your browser...
echo.
echo Please save files to the specified locations when prompted.
echo If you hit rate limits, the browser will show remaining wait time.
echo.

echo [1/2] Opening common-2.MPQ (Vanilla, 1.7GB)...
start "" "https://mega.nz/file/vXQCBCha#m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8"
echo     Save to: C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ
echo.
timeout /t 3 /nobreak >nul

echo [2/2] Opening mmaps archive (~2GB)...
start "" "https://mega.nz/file/7HgkHIyA#c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk"
echo     Save to: C:\WowClassicGrindBot\Navigation\mmaps.7z
echo.

echo ========================================
echo   Download Instructions
echo ========================================
echo.
echo 1. Click "Download" on each MEGA page
echo 2. Choose "Standard Download" (free)
echo 3. Save files to the locations shown above
echo 4. If rate limited: Note the reset time and retry later
echo.
echo After downloading:
echo - common-2.MPQ goes in: C:\WowClassicGrindBot\Json\MPQ\
echo - mmaps.7z needs extraction to: C:\WowClassicGrindBot\Navigation\mmaps\
echo.

pause
