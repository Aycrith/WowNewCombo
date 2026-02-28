Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class WowInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@

$hwnd = (Get-Process WowClassic).MainWindowHandle
Write-Host "WoW HWND: $hwnd"

# Show and focus WoW
[WowInput]::ShowWindow($hwnd, 9)
Start-Sleep -Milliseconds 800
[WowInput]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1500

$fg = [WowInput]::GetForegroundWindow()
Write-Host "Foreground window: $fg"
if ($fg -eq $hwnd) {
    Write-Host "WoW is in foreground - sending /dc command..."
} else {
    Write-Host "WARNING: WoW is NOT in foreground (got: $fg)"
}

Add-Type -AssemblyName System.Windows.Forms

# Press Escape to clear any UI state
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 400

# Open chat  
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 500

# Type /dc 
[System.Windows.Forms.SendKeys]::SendWait("/dcbindings")
Start-Sleep -Milliseconds 400

# Press Enter to execute
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 500

Write-Host "Done - /dc command sent"
