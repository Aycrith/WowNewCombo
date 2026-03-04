@echo off
echo Starting push to WowCombo repository...
echo This may take several minutes due to repository size.
echo.
git push -u wowcombo dev --progress
echo.
echo Push completed!
pause
