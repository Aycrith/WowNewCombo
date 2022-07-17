using System;
using System.Drawing;
using System.Threading;
using GregsStack.InputSimulatorStandard.Native;
using static WinAPI.NativeMethods;
using TextCopy;

namespace Game
{
    public class InputSimulator : IInput, IDisposable
    {
        private readonly int MIN_DELAY;
        private readonly int MAX_DELAY;

        private readonly Random random = new();
        private readonly GregsStack.InputSimulatorStandard.InputSimulator simulator;
        private readonly WowProcess wowProcess;

        private readonly CancellationTokenSource _cts;

        public InputSimulator(WowProcess wowProcess, int minDelay, int maxDelay)
        {
            this.wowProcess = wowProcess;

            MIN_DELAY = minDelay;
            MAX_DELAY = maxDelay;

            simulator = new GregsStack.InputSimulatorStandard.InputSimulator();
            _cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cts.Cancel();
        }

        private int Delay(int milliseconds)
        {
            int delay = milliseconds + random.Next(1, MAX_DELAY);
            _cts.Token.WaitHandle.WaitOne(delay);
            return delay;
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

        public int KeyPress(int key, int milliseconds)
        {
            simulator.Keyboard.KeyDown((VirtualKeyCode)key);
            int delay = Delay(milliseconds);
            simulator.Keyboard.KeyUp((VirtualKeyCode)key);
            return delay;
        }

        public void KeyPressSleep(int key, int milliseconds, CancellationTokenSource cts)
        {
            simulator.Keyboard.KeyDown((VirtualKeyCode)key);
            cts.Token.WaitHandle.WaitOne(milliseconds);
            simulator.Keyboard.KeyUp((VirtualKeyCode)key);
        }

        public void LeftClickMouse(Point p)
        {
            SetCursorPosition(p);
            simulator.Mouse.LeftButtonDown();
            Delay(MIN_DELAY);
            simulator.Mouse.LeftButtonUp();
        }

        public void RightClickMouse(Point p)
        {
            SetCursorPosition(p);
            simulator.Mouse.RightButtonDown();
            Delay(MIN_DELAY);
            simulator.Mouse.RightButtonUp();
        }

        public void SetCursorPosition(Point p)
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
            Delay(25);
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
}
