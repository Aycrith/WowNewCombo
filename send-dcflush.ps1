Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Threading;

public class WowInputV2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    const uint WM_KEYDOWN = 0x0100;
    const uint WM_KEYUP = 0x0101;
    const uint WM_CHAR = 0x0102;
    
    public static void SendKey(IntPtr hwnd, int vk) {
        PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        Thread.Sleep(50);
        PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        Thread.Sleep(50);
    }
    
    public static void SendChar(IntPtr hwnd, char c) {
        PostMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
        Thread.Sleep(30);
    }
    
    public static void TypeText(IntPtr hwnd, string text) {
        foreach (char c in text) {
            SendChar(hwnd, c);
        }
    }
}
"@

$hwnd = [IntPtr]::new(2033338)
Write-Host "WoW HWND: $hwnd"

# Focus WoW - use SetForegroundWindow only (NOT ShowWindow SW_RESTORE which breaks fullscreen)
[WowInputV2]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

$fg = [WowInputV2]::GetForegroundWindow()
Write-Host "Foreground: $fg (expected: $hwnd)"

# VK codes
$VK_RETURN = 0x0D

# Press Enter to open chat (no Escape - ESC can open Game Menu and break fullscreen)
Write-Host "Opening chat..."
[WowInputV2]::SendKey($hwnd, $VK_RETURN)
Start-Sleep -Milliseconds 400

# Type /dc using WM_CHAR messages directly to the window
Write-Host "Typing /dcflush..."
[WowInputV2]::TypeText($hwnd, "/dcflush")
Start-Sleep -Milliseconds 200

# Press Enter to execute  
Write-Host "Executing..."
[WowInputV2]::SendKey($hwnd, $VK_RETURN)
Start-Sleep -Milliseconds 500

Write-Host "Done - /dc sent via PostMessage"

