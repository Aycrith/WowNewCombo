Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WinFocus {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@

$hwnd = [IntPtr]::new(1377998)
[WinFocus]::ShowWindow($hwnd, 9)
Start-Sleep -Milliseconds 1000
[WinFocus]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1500

$fg = [WinFocus]::GetForegroundWindow()
Write-Host "Foreground: $fg (WoW: $hwnd) Match: $($fg -eq $hwnd)"

Add-Type -AssemblyName System.Windows.Forms

# Press Escape first to close any menus
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 500

# Alt+Enter to toggle fullscreen in WoW
[System.Windows.Forms.SendKeys]::SendWait("%{ENTER}")
Start-Sleep 3

Write-Host "Alt+Enter sent. WoW should now be switching to fullscreen."
