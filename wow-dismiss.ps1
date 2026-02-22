Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WF5 { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd); }
"@
Add-Type -AssemblyName System.Windows.Forms
[WF5]::SetForegroundWindow([IntPtr]::new(1377998)) | Out-Null
Start-Sleep -Milliseconds 800
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 500
Write-Host "Popup dismissed"
