using System;
using System.Drawing;
using System.Threading;
using TextCopy;
using static WinAPI.NativeMethods;

namespace Game;

public sealed class InputWindowsNative : IInput
{
    private readonly int maxDelay;

    private readonly WowProcess wowProcess;

    private readonly CancellationToken _ct;

    public InputWindowsNative(WowProcess wowProcess, CancellationTokenSource cts, int maxDelay)
    {
        this.wowProcess = wowProcess;
        _ct = cts.Token;

        this.maxDelay = maxDelay;
    }

    private int DelayTime(int milliseconds)
    {
        return milliseconds + Random.Shared.Next(maxDelay);
    }

    public void KeyDown(int key)
    {
        PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYDOWN, key, 0);
    }

    public void KeyUp(int key)
    {
        PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYUP, key, 0);
    }

    public int PressRandom(int key, int milliseconds)
    {
        return PressRandom(key, milliseconds, _ct);
    }

    public int PressRandom(int key, int milliseconds, CancellationToken ct)
    {
        PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYDOWN, key, 0);

        int delay = DelayTime(milliseconds);
        ct.WaitHandle.WaitOne(delay);

        PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYUP, key, 0);

        return delay;
    }

    public void PressFixed(int key, int milliseconds, CancellationToken ct)
    {
        PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYDOWN, key, 0);
        ct.WaitHandle.WaitOne(milliseconds);
        PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYUP, key, 0);
    }

    public void LeftClick(Point p)
    {
        SetCursorPos(p);

        ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
        int lparam = MakeLParam(p.X, p.Y);

        PostMessage(wowProcess.Process.MainWindowHandle, WM_LBUTTONDOWN, 0, lparam);

        _ct.WaitHandle.WaitOne(DelayTime(maxDelay));

        GetCursorPos(out p);
        ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
        lparam = MakeLParam(p.X, p.Y);

        PostMessage(wowProcess.Process.MainWindowHandle, WM_LBUTTONUP, 0, lparam);
    }

    public void RightClick(Point p)
    {
        SetCursorPos(p);

        ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
        int lparam = MakeLParam(p.X, p.Y);

        PostMessage(wowProcess.Process.MainWindowHandle, WM_RBUTTONDOWN, 0, lparam);

        _ct.WaitHandle.WaitOne(DelayTime(maxDelay));

        GetCursorPos(out p);
        ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
        lparam = MakeLParam(p.X, p.Y);

        PostMessage(wowProcess.Process.MainWindowHandle, WM_RBUTTONUP, 0, lparam);
    }

    public void SetCursorPos(Point p)
    {
        WinAPI.NativeMethods.SetCursorPos(p.X, p.Y);
    }

    public void SendText(string text)
    {
        // currently not supported
        throw new NotImplementedException();
    }

    public void SetClipboard(string text)
    {
        ClipboardService.SetText(text);
    }

    public void PasteFromClipboard()
    {
        // currently not supported
        throw new NotImplementedException();
    }
}
