Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WF2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@
Add-Type -AssemblyName System.Windows.Forms

$hwnd = [IntPtr]::new(1377998)
[WF2]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 1000
[WF2]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 1500

# Make sure WoW is foreground
$fg = [WF2]::GetForegroundWindow()
Write-Host "Focused: $($fg -eq $hwnd)"

# Press Escape to clear menus
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 500

# Type /console gxWindow 0 (fullscreen mode)
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("/console gxWindow 0")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 800

# Type /console gxRestart to apply
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("/console gxRestart")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep 4

Write-Host "Done. WoW should be switching to fullscreen..."
