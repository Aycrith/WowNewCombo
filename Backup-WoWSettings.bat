@echo off
:: Run the Backup-WoWSettings.ps1 script

echo ============================================
echo   WoW Settings Backup Utility
echo ============================================
echo.

powershell -ExecutionPolicy Bypass -File "C:\WowClassicGrindBot\Scripts\Backup-WoWSettings.ps1"

pause
