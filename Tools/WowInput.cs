// WowInput.cs - Standalone tool to send input to WoW client
// Usage: dotnet run -- <command>
// Examples:
//   dotnet run -- /dcactions
//   dotnet run -- /dccheck
//   dotnet run -- KEY:SHIFT+PAGEUP
//   dotnet run -- KEY:ENTER

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

class WowInput
{
    // Windows API imports
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    static extern short VkKeyScan(char ch);

    const int SW_RESTORE = 9;
    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    const uint KEYEVENTF_SCANCODE = 0x0008;  // Required for DirectInput games like WoW
    const uint MAPVK_VK_TO_VSC = 0;

    // Virtual key codes
    const int VK_RETURN = 0x0D;
    const int VK_SHIFT = 0x10;
    const int VK_CONTROL = 0x11;
    const int VK_MENU = 0x12;  // Alt
    const int VK_PRIOR = 0x21; // Page Up
    const int VK_NEXT = 0x22;  // Page Down
    const int VK_DELETE = 0x2E;
    const int VK_INSERT = 0x2D;

    // INPUT structure for SendInput - must match Windows API exactly
    // On x64: sizeof(INPUT) = 40 bytes
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    // MOUSEINPUT is larger than KEYBDINPUT, so we need to account for this
    // MOUSEINPUT on x64 = 32 bytes, KEYBDINPUT = 24 bytes (with padding)
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct INPUT
    {
        [FieldOffset(0)] public uint type;
        // Union starts at offset 8 on x64 (4 bytes type + 4 bytes padding)
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: WowInput <command>");
            Console.WriteLine("  /dcactions     - Type /dcactions and press Enter");
            Console.WriteLine("  /dccheck       - Type /dccheck and press Enter");
            Console.WriteLine("  KEY:SHIFT+PAGEUP - Press Shift+PageUp");
            Console.WriteLine("  KEY:ENTER      - Press Enter");
            Console.WriteLine("  TEXT:hello     - Type 'hello'");
            return;
        }

        // Find WoW process
        using var wowProcess = FindWowProcess();
        if (wowProcess == null)
        {
            Console.WriteLine("ERROR: WoW process not found");
            return;
        }

        Console.WriteLine($"Found WoW: PID={wowProcess.Id}, Handle={wowProcess.MainWindowHandle}");

        // Bring WoW to foreground
        if (!BringToForeground(wowProcess.MainWindowHandle))
        {
            Console.WriteLine("WARNING: Could not bring WoW to foreground");
        }
        else
        {
            Console.WriteLine("WoW brought to foreground successfully");
        }
        
        // Check if WoW is now the foreground window
        IntPtr fgWnd = GetForegroundWindow();
        Console.WriteLine($"Foreground window handle: {fgWnd} (WoW: {wowProcess.MainWindowHandle})");
        if (fgWnd != wowProcess.MainWindowHandle)
        {
            Console.WriteLine("WARNING: WoW is NOT the foreground window - SendInput may fail!");
        }

        Thread.Sleep(100); // Wait for focus

        string command = string.Join(" ", args);
        Console.WriteLine($"Executing: {command}");

        if (command.StartsWith("KEY:"))
        {
            // Send key combination
            string keySpec = command.Substring(4);
            SendKeyCombo(keySpec);
        }
        else if (command.StartsWith("TEXT:"))
        {
            // Type text without Enter
            string text = command.Substring(5);
            TypeText(text);
        }
        else if (command.StartsWith("/"))
        {
            // Slash command - open chat, type, press enter
            SendSlashCommand(command);
        }
        else
        {
            Console.WriteLine($"Unknown command format: {command}");
        }

        Console.WriteLine("Done");
    }

    static Process? FindWowProcess()
    {
        Process[] processes = Process.GetProcesses();
        Process? found = null;

        foreach (Process proc in processes)
        {
            if (found == null &&
                (proc.ProcessName.Contains("WowClassic", StringComparison.OrdinalIgnoreCase) ||
                 proc.ProcessName.Contains("Wow", StringComparison.OrdinalIgnoreCase)) &&
                proc.MainWindowHandle != IntPtr.Zero)
            {
                found = proc;
            }
            else
            {
                proc.Dispose();
            }
        }

        return found;
    }

    static bool BringToForeground(IntPtr hWnd)
    {
        if (GetForegroundWindow() == hWnd)
            return true;

        ShowWindow(hWnd, SW_RESTORE);
        Thread.Sleep(50);
        return SetForegroundWindow(hWnd);
    }

    static void SendSlashCommand(string command)
    {
        // Press Enter to open chat
        SendKey(VK_RETURN);
        Thread.Sleep(100);

        // Type the command
        TypeText(command);
        Thread.Sleep(50);

        // Press Enter to execute
        SendKey(VK_RETURN);
    }

    static void TypeText(string text)
    {
        foreach (char c in text)
        {
            TypeChar(c);
            Thread.Sleep(10); // Small delay between characters
        }
    }

    static void TypeChar(char c)
    {
        short vkResult = VkKeyScan(c);
        if (vkResult == -1)
        {
            Console.WriteLine($"Cannot type character: {c}");
            return;
        }

        int vk = vkResult & 0xFF;
        bool needsShift = (vkResult & 0x100) != 0;

        if (needsShift)
            SendKeyDown(VK_SHIFT);

        SendKey(vk);

        if (needsShift)
            SendKeyUp(VK_SHIFT);
    }

    static void SendKeyCombo(string keySpec)
    {
        // Parse key spec like "SHIFT+PAGEUP" or "CTRL+ALT+DELETE"
        string[] parts = keySpec.ToUpper().Split('+');
        
        bool shift = false, ctrl = false, alt = false;
        int mainKey = 0;

        foreach (string part in parts)
        {
            switch (part.Trim())
            {
                case "SHIFT": shift = true; break;
                case "CTRL": ctrl = true; break;
                case "ALT": alt = true; break;
                case "PAGEUP": mainKey = VK_PRIOR; break;
                case "PAGEDOWN": mainKey = VK_NEXT; break;
                case "DELETE": mainKey = VK_DELETE; break;
                case "INSERT": mainKey = VK_INSERT; break;
                case "ENTER": mainKey = VK_RETURN; break;
                default:
                    if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                        mainKey = char.ToUpper(part[0]);
                    else
                        Console.WriteLine($"Unknown key: {part}");
                    break;
            }
        }

        if (mainKey == 0)
        {
            Console.WriteLine("No main key specified");
            return;
        }

        // Press modifiers
        if (shift) SendKeyDown(VK_SHIFT);
        if (ctrl) SendKeyDown(VK_CONTROL);
        if (alt) SendKeyDown(VK_MENU);

        Thread.Sleep(20);

        // Press and release main key
        SendKey(mainKey);

        Thread.Sleep(20);

        // Release modifiers (reverse order)
        if (alt) SendKeyUp(VK_MENU);
        if (ctrl) SendKeyUp(VK_CONTROL);
        if (shift) SendKeyUp(VK_SHIFT);
    }

    static void SendKey(int vk)
    {
        SendKeyDown(vk);
        Thread.Sleep(30);
        SendKeyUp(vk);
    }

    static void SendKeyDown(int vk)
    {
        var input = CreateKeyInput(vk, false);
        uint result = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        int error = result == 0 ? Marshal.GetLastWin32Error() : 0;
        Console.WriteLine($"  SendInput DOWN vk=0x{vk:X2} scan={input.ki.wScan} flags=0x{input.ki.dwFlags:X} -> result={result} error={error}");
    }

    static void SendKeyUp(int vk)
    {
        var input = CreateKeyInput(vk, true);
        uint result = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        int error = result == 0 ? Marshal.GetLastWin32Error() : 0;
        Console.WriteLine($"  SendInput UP   vk=0x{vk:X2} scan={input.ki.wScan} flags=0x{input.ki.dwFlags:X} -> result={result} error={error}");
    }

    static INPUT CreateKeyInput(int vk, bool keyUp)
    {
        // For DirectInput games (like WoW), we MUST use KEYEVENTF_SCANCODE
        // The virtual key is ignored when SCANCODE flag is set
        // For key up, we need BOTH KEYEVENTF_KEYUP AND KEYEVENTF_SCANCODE
        uint flags = KEYEVENTF_SCANCODE;
        
        if (keyUp)
            flags |= KEYEVENTF_KEYUP;
        
        // Extended keys need the extended flag even with scan codes
        if (vk == VK_PRIOR || vk == VK_NEXT || vk == VK_DELETE || vk == VK_INSERT)
            flags |= KEYEVENTF_EXTENDEDKEY;

        var input = new INPUT();
        input.type = INPUT_KEYBOARD;
        input.ki.wVk = 0;  // Ignored when using SCANCODE
        input.ki.wScan = (ushort)MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
        input.ki.dwFlags = flags;
        input.ki.time = 0;
        input.ki.dwExtraInfo = UIntPtr.Zero;
        return input;
    }
}
