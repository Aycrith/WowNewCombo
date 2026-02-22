Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WF3 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@
$hwnd = [IntPtr]::new(1377998)
[WF3]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 800
[WF3]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 1200
Add-Type -AssemblyName System.Windows.Forms
# Press Escape then close WoW with /logout (proper/clean close)
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("/logout")
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep 2
Write-Host "Logout command sent"
