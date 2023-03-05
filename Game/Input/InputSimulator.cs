using System;
using System.Drawing;
using System.Threading;
using GregsStack.InputSimulatorStandard.Native;
using static WinAPI.NativeMethods;
using TextCopy;

namespace Game;

public sealed class InputSimulator : IInput
{
    private readonly int maxDelay;

    private readonly GregsStack.InputSimulatorStandard.InputSimulator simulator;
    private readonly WowProcess wowProcess;

    private readonly CancellationToken _ct;

    public InputSimulator(WowProcess wowProcess, CancellationTokenSource cts, int maxDelay)
    {
        this.wowProcess = wowProcess;
        _ct = cts.Token;

        this.maxDelay = maxDelay;

        simulator = new GregsStack.InputSimulatorStandard.InputSimulator();
    }

    private int DelayTime(int milliseconds)
    {
        return milliseconds + Random.Shared.Next(maxDelay);
    }

    public void KeyDown(int key)
    {
        if (GetForegroundWindow() != wowProcess.Process.MainWindowHandle)
            SetForegroundWindow(wowProcess.Process.MainWindowHandle);

        simulator.Keyboard.KeyDown((VirtualKeyCode)key);
    }

    public void KeyUp(int key)
    {
        if (GetForegroundWindow() != wowProcess.Process.MainWindowHandle)
            SetForegroundWindow(wowProcess.Process.MainWindowHandle);

        simulator.Keyboard.KeyUp((VirtualKeyCode)key);
    }

    public int PressRandom(int key, int milliseconds)
    {
        simulator.Keyboard.KeyDown((VirtualKeyCode)key);
        int delay = DelayTime(milliseconds);
        simulator.Keyboard.KeyUp((VirtualKeyCode)key);
        return delay;
    }

    public int PressRandom(int key, int milliseconds, CancellationToken ct)
    {
        simulator.Keyboard.KeyDown((VirtualKeyCode)key);

        int delay = milliseconds + Random.Shared.Next(1, maxDelay);
        ct.WaitHandle.WaitOne(delay);

        simulator.Keyboard.KeyUp((VirtualKeyCode)key);

        return delay;
    }

    public void PressFixed(int key, int milliseconds, CancellationToken ct)
    {
        simulator.Keyboard.KeyDown((VirtualKeyCode)key);
        ct.WaitHandle.WaitOne(milliseconds);
        simulator.Keyboard.KeyUp((VirtualKeyCode)key);
    }

    public void LeftClick(Point p)
    {
        SetCursorPos(p);

        simulator.Mouse.LeftButtonDown();
        _ct.WaitHandle.WaitOne(DelayTime(maxDelay));
        simulator.Mouse.LeftButtonUp();
    }

    public void RightClick(Point p)
    {
        SetCursorPos(p);

        simulator.Mouse.RightButtonDown();
        _ct.WaitHandle.WaitOne(DelayTime(maxDelay));
        simulator.Mouse.RightButtonUp();
    }

    public void SetCursorPos(Point p)
    {
        GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);
        p.X = p.X * 65535 / rect.Width;
        p.Y = p.Y * 65535 / rect.Height;
        simulator.Mouse.MoveMouseTo(Convert.ToDouble(p.X), Convert.ToDouble(p.Y));
    }

    public void SendText(string text)
    {
        if (GetForegroundWindow() != wowProcess.Process.MainWindowHandle)
            SetForegroundWindow(wowProcess.Process.MainWindowHandle);

        simulator.Keyboard.TextEntry(text);

        _ct.WaitHandle.WaitOne(DelayTime(maxDelay));
    }

    public void SetClipboard(string text)
    {
        ClipboardService.SetText(text);
    }

    public void PasteFromClipboard()
    {
        if (GetForegroundWindow() != wowProcess.Process.MainWindowHandle)
            SetForegroundWindow(wowProcess.Process.MainWindowHandle);

        simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);
    }
}
