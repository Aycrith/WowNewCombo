# Create a desktop shortcut for WowClassicGrindBot
$DesktopPath = [Environment]::GetFolderPath("Desktop")
$ShortcutPath = Join-Path $DesktopPath "WowClassicGrindBot.lnk"

$Shell = New-Object -ComObject WScript.Shell
$Shortcut = $Shell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = "C:\WowClassicGrindBot\Start.bat"
$Shortcut.WorkingDirectory = "C:\WowClassicGrindBot"
$Shortcut.WindowStyle = 1
$Shortcut.Description = "WowClassicGrindBot - One-Click Launcher"
$Shortcut.Save()

Write-Host "Desktop shortcut created at: $ShortcutPath"
Write-Host ""
Write-Host "You can now double-click the shortcut to start WowClassicGrindBot!"
