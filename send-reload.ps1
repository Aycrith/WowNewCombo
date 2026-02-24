Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
public class WowInputSend {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    const uint WM_KEYDOWN = 0x0100;
    const uint WM_KEYUP   = 0x0101;
    const uint WM_CHAR    = 0x0102;
    public static void SendKey(IntPtr hwnd, int vk) {
        PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        Thread.Sleep(60);
        PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        Thread.Sleep(60);
    }
    public static void TypeText(IntPtr hwnd, string text) {
        foreach (char c in text) {
            PostMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
            Thread.Sleep(35);
        }
    }
    public static void SendCommand(IntPtr hwnd, string cmd) {
        SetForegroundWindow(hwnd);
        Thread.Sleep(300);
        SendKey(hwnd, 0x0D);
        Thread.Sleep(400);
        TypeText(hwnd, cmd);
        Thread.Sleep(200);
        SendKey(hwnd, 0x0D);
        Thread.Sleep(600);
    }
}
"@

$wow = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $wow) { throw "WowClassic process not found." }
$wow.Refresh()
$rawHwnd = [long]$wow.MainWindowHandle
if ($rawHwnd -le 0) { throw "WowClassic MainWindowHandle is unavailable (0). Ensure the WoW client has a visible window and is not minimized/tray-hidden." }
$hwnd = [IntPtr]$rawHwnd
Write-Host "WoW HWND: $hwnd"

$cmd = if ($args.Count -gt 0) { $args[0] } else { "/reload" }
Write-Host "Sending: $cmd"
[WowInputSend]::SendCommand($hwnd, $cmd)
Write-Host "Done"
