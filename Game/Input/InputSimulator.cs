using System;
using System.Drawing;
using System.Threading;
using GregsStack.InputSimulatorStandard.Native;
using static WinAPI.NativeMethods;
using TextCopy;

namespace Game
{
    public sealed class InputSimulator : IInput
    {
        private readonly int MIN_DELAY;
        private readonly int MAX_DELAY;

        private readonly GregsStack.InputSimulatorStandard.InputSimulator simulator;
        private readonly WowProcess wowProcess;

        private readonly CancellationToken _ct;

        public InputSimulator(WowProcess wowProcess, CancellationTokenSource cts, int minDelay, int maxDelay)
        {
            this.wowProcess = wowProcess;
            _ct = cts.Token;

            MIN_DELAY = minDelay;
            MAX_DELAY = maxDelay;

            simulator = new GregsStack.InputSimulatorStandard.InputSimulator();
        }

        private int Delay(int milliseconds)
        {
            int delay = milliseconds + Random.Shared.Next(1, MAX_DELAY);
            _ct.WaitHandle.WaitOne(delay);
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

        public void KeyPressSleep(int key, int milliseconds, CancellationToken ct)
        {
            simulator.Keyboard.KeyDown((VirtualKeyCode)key);
            ct.WaitHandle.WaitOne(milliseconds);
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
