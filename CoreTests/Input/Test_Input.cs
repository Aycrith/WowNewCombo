using System;
using System.Drawing;
using Game;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace CoreTests
{
    public class Test_Input : IDisposable
    {
        private const int delay = 500;

        private readonly CancellationTokenSource cts;

        private readonly ILogger logger;
        private readonly WowProcess wowProcess;
        private readonly WowScreen wowScreen;
        private readonly WowProcessInput wowProcessInput;

        public Test_Input(ILogger logger)
        {
            this.logger = logger;
            this.cts = new();

            wowProcess = new WowProcess();
            wowScreen = new WowScreen(logger, wowProcess);
            wowProcessInput = new WowProcessInput(logger, cts, wowProcess);
        }

        public void Dispose()
        {
            wowScreen.Dispose();
            wowProcess.Dispose();
            cts.Dispose();
        }

        public void Mouse_Movement()
        {
            wowProcessInput.SetForegroundWindow();

            wowProcessInput.SetCursorPosition(new Point(25, 25));
            cts.Token.WaitHandle.WaitOne(delay);

            wowProcessInput.SetCursorPosition(new Point(50, 50));
            cts.Token.WaitHandle.WaitOne(delay);

            logger.LogInformation($"{nameof(Mouse_Movement)} Finished");
        }

        public void Mouse_Clicks()
        {
            wowProcessInput.SetForegroundWindow();

            Point p = new(120, 120);
            wowProcessInput.LeftClickMouse(p);

            cts.Token.WaitHandle.WaitOne(delay);

            wowProcessInput.RightClickMouse(p);

            cts.Token.WaitHandle.WaitOne(delay);

            wowProcessInput.RightClickMouse(p);

            wowScreen.GetRectangle(out Rectangle rect);
            p = new Point(rect.Width / 2, rect.Height / 2);

            cts.Token.WaitHandle.WaitOne(delay);

            wowProcessInput.RightClickMouse(p);

            cts.Token.WaitHandle.WaitOne(delay);

            wowProcessInput.RightClickMouse(p);

            logger.LogInformation($"{nameof(Mouse_Clicks)} Finished");
        }

        public void Clipboard()
        {
            wowProcessInput.SetClipboard("/help");

            // Open chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, delay);

            wowProcessInput.PasteFromClipboard();
            cts.Token.WaitHandle.WaitOne(delay);

            // Close chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, delay);

            logger.LogInformation($"{nameof(Clipboard)} Finished");
        }
    }
}
