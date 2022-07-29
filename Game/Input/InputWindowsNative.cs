using System;
using System.Drawing;
using System.Threading;
using System.Runtime.CompilerServices;
using TextCopy;
using static WinAPI.NativeMethods;

namespace Game
{
    public class InputWindowsNative : IInput
    {
        private readonly int MIN_DELAY;
        private readonly int MAX_DELAY;

        private readonly WowProcess wowProcess;
        private readonly Random random;

        private readonly CancellationTokenSource _cts;

        public InputWindowsNative(WowProcess wowProcess, int minDelay, int maxDelay)
        {
            this.wowProcess = wowProcess;

            MIN_DELAY = minDelay;
            MAX_DELAY = maxDelay;

            random = new();
            _cts = new();
        }

        private int Delay(int milliseconds)
        {
            int delay = milliseconds + random.Next(1, MAX_DELAY);
            _cts.Token.WaitHandle.WaitOne(delay);
            return delay;
        }

        public void KeyDown(int key)
        {
            PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYDOWN, key, 0);
        }

        public void KeyUp(int key)
        {
            PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYUP, key, 0);
        }

        public int KeyPress(int key, int milliseconds)
        {
            PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYDOWN, key, 0);
            int delay = Delay(milliseconds);
            PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYUP, key, 0);

            return delay;
        }

        public void KeyPressSleep(int key, int milliseconds, CancellationTokenSource cts)
        {
            PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYDOWN, key, 0);
            cts.Token.WaitHandle.WaitOne(milliseconds);
            PostMessage(wowProcess.Process.MainWindowHandle, WM_KEYUP, key, 0);
        }

        public void LeftClickMouse(Point p)
        {
            SetCursorPosition(p);

            ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
            int lparam = MakeLParam(p.X, p.Y);

            PostMessage(wowProcess.Process.MainWindowHandle, WM_LBUTTONDOWN, 0, lparam);

            Delay(MIN_DELAY);

            GetCursorPos(out p);
            ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
            lparam = MakeLParam(p.X, p.Y);

            PostMessage(wowProcess.Process.MainWindowHandle, WM_LBUTTONUP, 0, lparam);
        }

        public void RightClickMouse(Point p)
        {
            SetCursorPosition(p);

            ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
            int lparam = MakeLParam(p.X, p.Y);

            PostMessage(wowProcess.Process.MainWindowHandle, WM_RBUTTONDOWN, 0, lparam);

            Delay(MIN_DELAY);

            GetCursorPos(out p);
            ScreenToClient(wowProcess.Process.MainWindowHandle, ref p);
            lparam = MakeLParam(p.X, p.Y);

            PostMessage(wowProcess.Process.MainWindowHandle, WM_RBUTTONUP, 0, lparam);
        }

        public void SetCursorPosition(Point p)
        {
            SetCursorPos(p.X, p.Y);
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
}
