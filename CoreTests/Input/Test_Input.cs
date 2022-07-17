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

        private readonly ILogger logger;
        private readonly WowProcess wowProcess;
        private readonly WowScreen wowScreen;
        private readonly WowProcessInput wowProcessInput;

        public Test_Input(ILogger logger)
        {
            this.logger = logger;

            wowProcess = new WowProcess();
            wowScreen = new WowScreen(logger, wowProcess);
            wowProcessInput = new WowProcessInput(logger, wowProcess);
        }

        public void Dispose()
        {
            wowScreen.Dispose();
            wowProcess.Dispose();
        }

        public void Mouse_Movement()
        {
            wowProcessInput.SetForegroundWindow();

            wowProcessInput.SetCursorPosition(new Point(25, 25));
            Thread.Sleep(delay);

            wowProcessInput.SetCursorPosition(new Point(50, 50));
            Thread.Sleep(delay);

            logger.LogInformation($"{nameof(Mouse_Movement)} Finished");
        }

        public void Mouse_Clicks()
        {
            wowProcessInput.SetForegroundWindow();

            Point p = new(120, 120);
            wowProcessInput.LeftClickMouse(p);

            Thread.Sleep(delay);

            wowProcessInput.RightClickMouse(p);

            Thread.Sleep(delay);

            wowProcessInput.RightClickMouse(p);

            wowScreen.GetRectangle(out Rectangle rect);
            p = new Point(rect.Width / 2, rect.Height / 2);

            Thread.Sleep(delay);

            wowProcessInput.RightClickMouse(p);

            Thread.Sleep(delay);

            wowProcessInput.RightClickMouse(p);

            logger.LogInformation($"{nameof(Mouse_Clicks)} Finished");
        }

        public void Clipboard()
        {
            wowProcessInput.SetClipboard("/help");

            // Open chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, delay);

            wowProcessInput.PasteFromClipboard();
            Thread.Sleep(delay);

            // Close chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, delay);

            logger.LogInformation($"{nameof(Clipboard)} Finished");
        }
    }
}
