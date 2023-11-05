using System;
using SixLabors.ImageSharp;
using Game;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace CoreTests;

public class Test_Input : IDisposable
{
    private const int delay = 500;

    private readonly CancellationTokenSource cts;

    private readonly ILogger logger;
    private readonly WowProcess wowProcess;
    private readonly IWowScreen wowScreen;
    private readonly WowProcessInput wowProcessInput;

    public Test_Input(ILogger logger, WowProcess wowProcess, IWowScreen wowScreen, ILoggerFactory loggerFactory)
    {
        this.logger = logger;
        this.wowProcess = wowProcess;
        this.wowScreen = wowScreen;

        this.cts = new();

        wowProcessInput = new(loggerFactory.CreateLogger<WowProcessInput>(), cts, wowProcess);
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

        wowProcessInput.SetCursorPos(new Point(25, 25));
        cts.Token.WaitHandle.WaitOne(delay);

        wowProcessInput.SetCursorPos(new Point(50, 50));
        cts.Token.WaitHandle.WaitOne(delay);

        logger.LogInformation($"{nameof(Mouse_Movement)} Finished");
    }

    public void Mouse_Clicks()
    {
        wowProcessInput.SetForegroundWindow();

        Point p = new(120, 120);
        wowProcessInput.LeftClick(p);

        cts.Token.WaitHandle.WaitOne(delay);

        wowProcessInput.RightClick(p);

        cts.Token.WaitHandle.WaitOne(delay);

        wowProcessInput.RightClick(p);

        wowScreen.GetRectangle(out Rectangle rect);
        p = new Point(rect.Width / 2, rect.Height / 2);

        cts.Token.WaitHandle.WaitOne(delay);

        wowProcessInput.RightClick(p);

        cts.Token.WaitHandle.WaitOne(delay);

        wowProcessInput.RightClick(p);

        logger.LogInformation($"{nameof(Mouse_Clicks)} Finished");
    }

    public void Clipboard()
    {
        wowProcessInput.SetClipboard("/help");

        // Open chat inputbox
        wowProcessInput.PressRandom(ConsoleKey.Enter, delay);

        wowProcessInput.PasteFromClipboard();
        cts.Token.WaitHandle.WaitOne(delay);

        // Close chat inputbox
        wowProcessInput.PressRandom(ConsoleKey.Enter, delay);

        logger.LogInformation($"{nameof(Clipboard)} Finished");
    }
}
