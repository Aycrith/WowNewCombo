using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using WinAPI;

namespace Game
{
    public partial class WowProcessInput : IMouseInput
    {
        public readonly bool LogInput = false;

        private const int MIN_DELAY = 25;
        private const int MAX_DELAY = 55;

        protected readonly ILogger logger;
        private readonly WowProcess wowProcess;
        private readonly IInput nativeInput;
        private readonly IInput simulatorInput;

        private readonly Dictionary<ConsoleKey, bool> keyDownDict = new();

        public WowProcessInput(ILogger logger, WowProcess wowProcess)
        {
            this.logger = logger;
            this.wowProcess = wowProcess;

            this.nativeInput = new InputWindowsNative(wowProcess.WarcraftProcess, MIN_DELAY, MAX_DELAY);
            this.simulatorInput = new InputSimulator(wowProcess.WarcraftProcess, MIN_DELAY, MAX_DELAY);
        }

        private void KeyDown(ConsoleKey key)
        {
            if (IsKeyDown(key))
                return;

            if (LogInput)
                LogKeyDown(logger, key);

            keyDownDict[key] = true;
            nativeInput.KeyDown((int)key);
        }

        private void KeyUp(ConsoleKey key)
        {
            if (!IsKeyDown(key))
                return;

            if (LogInput)
                LogKeyUp(logger, key);

            nativeInput.KeyUp((int)key);
            keyDownDict[key] = false;
        }

        public bool IsKeyDown(ConsoleKey key)
        {
            if (keyDownDict.TryGetValue(key, out bool down))
                return down;
            return false;
        }

        public void SendText(string payload)
        {
            simulatorInput.SendText(payload);
        }

        public void PasteFromClipboard()
        {
            simulatorInput.PasteFromClipboard();
        }

        public void SetForegroundWindow()
        {
            NativeMethods.SetForegroundWindow(wowProcess.WarcraftProcess.MainWindowHandle);
        }


        public void KeyPress(ConsoleKey key, int milliseconds)
        {
            keyDownDict[key] = true;
            int totalElapsedMs = nativeInput.KeyPress((int)key, milliseconds);
            keyDownDict[key] = false;
            if (LogInput)
            {
                LogKeyPress(logger, key, totalElapsedMs);
            }
        }

        public void KeyPressSleep(ConsoleKey key, int milliseconds, CancellationTokenSource cts)
        {
            if (milliseconds < 1)
                return;

            if (LogInput)
            {
                LogKeyPress(logger, key, milliseconds);
            }

            keyDownDict[key] = true;
            nativeInput.KeyPressSleep((int)key, milliseconds, cts);
            keyDownDict[key] = false;
        }

        public void SetKeyState(ConsoleKey key, bool pressDown)
        {
            if (pressDown) { KeyDown(key); } else { KeyUp(key); }
        }

        public void SetCursorPosition(Point position)
        {
            nativeInput.SetCursorPosition(position);
        }

        public void RightClickMouse(Point position)
        {
            nativeInput.RightClickMouse(position);
        }

        public void LeftClickMouse(Point position)
        {
            nativeInput.LeftClickMouse(position);
        }

        [LoggerMessage(
            EventId = 25,
            Level = LogLevel.Debug,
            Message = @"Input: KeyDown {key}")]
        static partial void LogKeyDown(ILogger logger, ConsoleKey key);

        [LoggerMessage(
            EventId = 26,
            Level = LogLevel.Debug,
            Message = @"Input: KeyUp {key}")]
        static partial void LogKeyUp(ILogger logger, ConsoleKey key);

        [LoggerMessage(
            EventId = 27,
            Level = LogLevel.Debug,
            Message = @"Input: [{key}] pressed for {milliseconds}ms")]
        static partial void LogKeyPress(ILogger logger, ConsoleKey key, int milliseconds);

        [LoggerMessage(
            EventId = 28,
            Level = LogLevel.Debug,
            Message = @"Input: [{key}] pressing for {milliseconds}ms")]
        static partial void LogKeyPressNoDelay(ILogger logger, ConsoleKey key, int milliseconds);
    }
}
