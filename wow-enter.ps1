Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WF4 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
Add-Type -AssemblyName System.Windows.Forms
$hwnd = [IntPtr]::new(1377998)
[WF4]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 800
[WF4]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 1200
# Press Enter to click "Enter World"
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep 1
Write-Host "Enter World clicked"
