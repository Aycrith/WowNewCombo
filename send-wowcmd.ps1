param(
    [Parameter(Position = 0)]
    [string]$Command = "/reload"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$allowed = @(
    "/reload",
    "/dc",
    "/dcflush",
    "/dcbindings",
    "/dcnumberkeys",
    "/dcactions"
)

$normalized = $Command.Trim().ToLowerInvariant()
if ($allowed -notcontains $normalized)
{
    throw "Unsupported command '$Command'. Allowed: $($allowed -join ', ')"
}

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class WowInputSend
{
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public static void PrepUiForSlashCommand(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
        Thread.Sleep(500);
    }

    public static void SendVirtualKey(int vk)
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = (ushort)vk;
        inputs[0].U.ki.wScan = 0;
        inputs[0].U.ki.dwFlags = 0;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = (ushort)vk;
        inputs[1].U.ki.wScan = 0;
        inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(60);
    }

    public static void SendUnicodeChar(char c)
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = 0;
        inputs[0].U.ki.wScan = c;
        inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = 0;
        inputs[1].U.ki.wScan = c;
        inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(35);
    }

    public static void SendText(string text)
    {
        foreach (char c in text)
        {
            SendUnicodeChar(c);
        }
    }
}
"@

$wow = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $wow) { throw "WowClassic process not found." }

$wow.Refresh()
$rawHwnd = [long]$wow.MainWindowHandle
if ($rawHwnd -le 0)
{
    throw "WowClassic MainWindowHandle is unavailable (0). Ensure the WoW client has a visible window and is not minimized/tray-hidden."
}

$hwnd = [IntPtr]$rawHwnd
Write-Host "WoW HWND: $hwnd"
Write-Host "Sending: $normalized"

[WowInputSend]::PrepUiForSlashCommand($hwnd)
Start-Sleep -Milliseconds 250

$fg = [WowInputSend]::GetForegroundWindow()
Write-Host "Foreground: $fg (expected: $hwnd)"
if ($fg -ne $hwnd)
{
    Write-Warning "WoW is not in foreground; SendInput may target a different window."
}

[WowInputSend]::SendVirtualKey(0x1B) # Escape
Start-Sleep -Milliseconds 200
[WowInputSend]::SendVirtualKey(0x1B) # Escape
Start-Sleep -Milliseconds 250
[WowInputSend]::SendVirtualKey(0x0D) # Enter
Start-Sleep -Milliseconds 300
[WowInputSend]::SendText($normalized)
Start-Sleep -Milliseconds 250
[WowInputSend]::SendVirtualKey(0x0D) # Enter
Start-Sleep -Milliseconds 900

Write-Host "Done"
