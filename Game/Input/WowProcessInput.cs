using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using WinAPI;

#pragma warning disable 162

namespace Game
{
    public partial class WowProcessInput : IMouseInput
    {
        private const bool LogInput = false;
        private const bool LogMove = false;

        private const int MIN_DELAY = 25;
        private const int MAX_DELAY = 55;

        protected readonly ILogger logger;

        private readonly WowProcess wowProcess;
        private readonly InputWindowsNative nativeInput;
        private readonly IInput simulatorInput;

        private readonly Dictionary<ConsoleKey, bool> keyDownDict = new();

        public ConsoleKey ForwardKey { get; set; }
        public ConsoleKey BackwardKey { get; set; }
        public ConsoleKey TurnLeftKey { get; set; }
        public ConsoleKey TurnRightKey { get; set; }

        public WowProcessInput(ILogger logger, WowProcess wowProcess)
        {
            this.logger = logger;
            this.wowProcess = wowProcess;

            nativeInput = new InputWindowsNative(wowProcess, MIN_DELAY, MAX_DELAY);
            simulatorInput = new InputSimulator(wowProcess, MIN_DELAY, MAX_DELAY);
        }

        public void Reset()
        {
            lock (keyDownDict)
            {
                foreach (KeyValuePair<ConsoleKey, bool> kvp in keyDownDict)
                {
                    keyDownDict[kvp.Key] = false;
                }
            }
        }

        public void KeyDown(ConsoleKey key, bool forced)
        {
            if (IsKeyDown(key))
            {
                if (!forced)
                    return;
            }

            if (LogInput)
            {
                if (key == ForwardKey || key == BackwardKey || key == TurnLeftKey || key == TurnRightKey)
                {
                    if (LogMove)
                        LogKeyDown(logger, key);
                }
                else
                {
                    LogKeyDown(logger, key);
                }
            }

            keyDownDict[key] = true;
            nativeInput.KeyDown((int)key);
        }

        public void KeyUp(ConsoleKey key, bool forced)
        {
            if (!IsKeyDown(key))
            {
                if (!forced)
                    return;
            }

            if (LogInput)
            {
                if (key == ForwardKey || key == BackwardKey || key == TurnLeftKey || key == TurnRightKey)
                {
                    if (LogMove)
                        LogKeyUp(logger, key);
                }
                else
                {
                    LogKeyUp(logger, key);
                }
            }

            nativeInput.KeyUp((int)key);
            keyDownDict[key] = false;
        }

        public bool IsKeyDown(ConsoleKey key)
        {
            return keyDownDict.TryGetValue(key, out bool down) && down;
        }

        public void SendText(string payload)
        {
            simulatorInput.SendText(payload);
        }

        public void SetClipboard(string text)
        {
            simulatorInput.SetClipboard(text);
        }

        public void PasteFromClipboard()
        {
            simulatorInput.PasteFromClipboard();
        }

        public void SetForegroundWindow()
        {
            NativeMethods.SetForegroundWindow(wowProcess.Process.MainWindowHandle);
        }

        public int KeyPress(ConsoleKey key, int milliseconds)
        {
            keyDownDict[key] = true;
            int totalElapsedMs = nativeInput.KeyPress((int)key, milliseconds);
            keyDownDict[key] = false;
            
            if (LogInput)
            {
                LogKeyPress(logger, key, totalElapsedMs);
            }

            return totalElapsedMs;
        }

        public void KeyPressSleep(ConsoleKey key, int milliseconds, CancellationTokenSource cts)
        {
            if (milliseconds < 1)
                return;

            if (LogInput)
            {
                if (key == ForwardKey || key == BackwardKey || key == TurnLeftKey || key == TurnRightKey)
                {
                    if (LogMove)
                        LogKeyPress(logger, key, milliseconds);
                }
                else
                {
                    LogKeyPress(logger, key, milliseconds);
                }
            }

            keyDownDict[key] = true;
            nativeInput.KeyPressSleep((int)key, milliseconds, cts);
            keyDownDict[key] = false;
        }

        public void SetKeyState(ConsoleKey key, bool pressDown, bool forced = false)
        {
            if (pressDown) { KeyDown(key, forced); } else { KeyUp(key, forced); }
        }

        public void SetCursorPosition(Point p)
        {
            nativeInput.SetCursorPosition(p);
        }

        public void RightClickMouse(Point p)
        {
            nativeInput.RightClickMouse(p);
        }

        public void LeftClickMouse(Point p)
        {
            nativeInput.LeftClickMouse(p);
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
