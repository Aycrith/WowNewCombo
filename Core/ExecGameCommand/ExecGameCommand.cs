using Game;
using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Core
{
    public sealed class ExecGameCommand
    {
        private readonly ILogger logger;
        private readonly WowProcessInput wowProcessInput;
        private readonly CancellationToken ct;

        public ExecGameCommand(ILogger logger, CancellationTokenSource cts, WowProcessInput wowProcessInput)
        {
            this.logger = logger;
            ct = cts.Token;
            this.wowProcessInput = wowProcessInput;
        }

        public void Run(string content)
        {
            wowProcessInput.SetForegroundWindow();
            logger.LogInformation(content);

            wowProcessInput.SetClipboard(content);
            Wait(100, 250);

            // Open chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, Random.Shared.Next(50, 100));

            wowProcessInput.PasteFromClipboard();
            Wait(100, 250);

            // Close chat inputbox
            wowProcessInput.KeyPress(ConsoleKey.Enter, Random.Shared.Next(50, 100));
            Wait(100, 250);
        }

        private void Wait(int min, int max)
        {
            ct.WaitHandle.WaitOne(Random.Shared.Next(min, max));
        }
    }
}
