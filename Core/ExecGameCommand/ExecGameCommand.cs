using Game;
using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Core;

public sealed class ExecGameCommand
{
    private readonly ILogger<ExecGameCommand> logger;
    private readonly WowProcessInput input;
    private readonly CancellationToken token;

    public ExecGameCommand(ILogger<ExecGameCommand> logger,
        CancellationTokenSource cts, WowProcessInput input)
    {
        this.logger = logger;
        token = cts.Token;
        this.input = input;
    }

    public void Run(string content)
    {
        input.SetForegroundWindow();
        logger.LogInformation(content);

        input.SetClipboard(content);
        token.WaitHandle.WaitOne(Random.Shared.Next(100, 250));

        // Open chat inputbox
        input.PressRandom(ConsoleKey.Enter, 100, token);

        input.PasteFromClipboard();
        token.WaitHandle.WaitOne(Random.Shared.Next(100, 250));

        // Close chat inputbox
        input.PressRandom(ConsoleKey.Enter, 100, token);
        token.WaitHandle.WaitOne(Random.Shared.Next(100, 250));
    }
}
