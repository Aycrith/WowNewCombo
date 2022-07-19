using Game;
using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Core
{
    public class ExecGameCommand
    {
        private readonly ILogger logger;
        private readonly WowProcessInput wowProcessInput;
        private readonly CancellationTokenSource cts;

        private readonly Random random = new();

        public ExecGameCommand(ILogger logger, CancellationTokenSource cts, WowProcessInput wowProcessInput)
        {
            this.logger = logger;
            this.cts = cts;
            this.wowProcessInput = wowProcessInput;
        }

        public void Run(string content)
        {
            wowProcessInput.SetForegroundWindow();
            logger.LogInformation(content);

            wowProcessInput.SetClipboard(content);
            Wait(100, 250);

            // Open chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, random.Next(50, 100));

            wowProcessInput.PasteFromClipboard();
            Wait(100, 250);

            // Close chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, random.Next(50, 100));
            Wait(100, 250);
        }

        private void Wait(int min, int max)
        {
            cts.Token.WaitHandle.WaitOne(random.Next(min, max));
        }
    }
}
