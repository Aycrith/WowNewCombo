using Game;
using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Core;

public sealed class ExecGameCommand
{
    private readonly ILogger<ExecGameCommand> logger;
    private readonly WowProcessInput wowProcessInput;
    private readonly CancellationToken ct;

    public ExecGameCommand(ILogger<ExecGameCommand> logger,
        CancellationTokenSource cts, WowProcessInput wowProcessInput)
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
        ct.WaitHandle.WaitOne(Random.Shared.Next(100, 250));

        // Open chat inputbox
        wowProcessInput.PressRandom(ConsoleKey.Enter, 100, ct);

        wowProcessInput.PasteFromClipboard();
        ct.WaitHandle.WaitOne(Random.Shared.Next(100, 250));

        // Close chat inputbox
        wowProcessInput.PressRandom(ConsoleKey.Enter, 100, ct);
        ct.WaitHandle.WaitOne(Random.Shared.Next(100, 250));
    }
}
