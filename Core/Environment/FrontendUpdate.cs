using System.Threading;

using Microsoft.Extensions.Logging;

namespace Core;

public sealed class FrontendUpdate
{
    private readonly ILogger logger;
    private readonly IAddonReader addonReader;
    private readonly CancellationToken ct;

    private readonly Thread thread;
    private const int tickMs = 250;

    public FrontendUpdate(ILogger logger, IAddonReader addonReader, CancellationTokenSource cts)
    {
        this.logger = logger;
        this.addonReader = addonReader;
        this.ct = cts.Token;

        thread = new(Update);
        thread.Start();
    }

    private void Update()
    {
        while (!ct.IsCancellationRequested)
        {
            addonReader.UpdateUI();
            ct.WaitHandle.WaitOne(tickMs);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Frontend thread stopped!");
    }
}
