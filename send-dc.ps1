Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class WowInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@

$wow = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $wow) { throw "WowClassic process not found." }
if ($wow.MainWindowHandle -eq 0) { throw "WowClassic MainWindowHandle is 0 (window not ready)." }
$hwnd = [IntPtr]::new($wow.MainWindowHandle)
Write-Host "WoW HWND: $hwnd"

# Focus WoW (avoid ShowWindow restore; can resize/reposition the client)
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

# Open chat  
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 500

# Type /dc 
[System.Windows.Forms.SendKeys]::SendWait("/dc")
Start-Sleep -Milliseconds 400

# Press Enter to execute
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 500

Write-Host "Done - /dc command sent"
