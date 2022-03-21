using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using WinAPI;

namespace Game
{
    public class InputWindowsNative : IInput
    {
        private readonly int MIN_DELAY;
        private readonly int MAX_DELAY;

        private readonly Process process;
        private readonly Random random = new();

        private readonly CancellationTokenSource _cts;

        private readonly IEnumerable<ConsoleKey> consoleKeys = (IEnumerable<ConsoleKey>)Enum.GetValues(typeof(ConsoleKey));

        public InputWindowsNative(Process process, int minDelay, int maxDelay)
        {
            this.process = process;

            MIN_DELAY = minDelay;
            MAX_DELAY = maxDelay;
            _cts = new CancellationTokenSource();
        }

        private int Delay(int milliseconds)
        {
            int delay = milliseconds + random.Next(1, MAX_DELAY);
            _cts.Token.WaitHandle.WaitOne(delay);
            return delay;
        }

        public void KeyDown(int key)
        {
            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_KEYDOWN, key, 0);
        }

        public void KeyUp(int key)
        {
            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_KEYUP, key, 0);
        }

        public int KeyPress(int key, int milliseconds)
        {
            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_KEYDOWN, key, 0);
            int delay = Delay(milliseconds);
            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_KEYUP, key, 0);

            return delay;
        }

        public void KeyPressSleep(int key, int milliseconds, CancellationTokenSource cts)
        {
            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_KEYDOWN, key, 0);
            cts.Token.WaitHandle.WaitOne(milliseconds);
            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_KEYUP, key, 0);
        }

        public void LeftClickMouse(Point p)
        {
            SetCursorPosition(p);

            NativeMethods.ScreenToClient(process.MainWindowHandle, ref p);
            int lparam = NativeMethods.MakeLParam(p.X, p.Y);

            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_LBUTTONDOWN, 0, lparam);

            Delay(MIN_DELAY);

            NativeMethods.GetCursorPos(out p);
            NativeMethods.ScreenToClient(process.MainWindowHandle, ref p);
            lparam = NativeMethods.MakeLParam(p.X, p.Y);

            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_LBUTTONUP, 0, lparam);
        }

        public void RightClickMouse(Point p)
        {
            SetCursorPosition(p);

            NativeMethods.ScreenToClient(process.MainWindowHandle, ref p);
            int lparam = NativeMethods.MakeLParam(p.X, p.Y);

            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_RBUTTONDOWN, 0, lparam);

            Delay(MIN_DELAY);

            NativeMethods.GetCursorPos(out p);
            NativeMethods.ScreenToClient(process.MainWindowHandle, ref p);
            lparam = NativeMethods.MakeLParam(p.X, p.Y);

            NativeMethods.PostMessage(process.MainWindowHandle, NativeMethods.WM_RBUTTONUP, 0, lparam);
        }

        public void SetCursorPosition(Point p)
        {
            NativeMethods.SetCursorPos(p.X, p.Y);
        }

        public void SendText(string text)
        {
            // only works with ConsoleKey characters
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                var consoleKey = consoleKeys.FirstOrDefault(k => k.ToString() == chars[i].ToString());
                if (consoleKey != 0)
                { 
                    KeyPress((int)consoleKey, 15);
                }
            }
        }

        public void PasteFromClipboard()
        {
            // currently not supported
            throw new NotImplementedException();
        }
    }
}
