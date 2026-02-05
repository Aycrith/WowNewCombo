using Core.FeatureFlags;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Humanization;

public sealed class MicroPauseService : IHostedService, IDisposable
{
    private readonly ILogger<MicroPauseService> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly TimeProvider timeProvider;
    private readonly IServiceProvider services;

    private Timer? timer;
    private long microPauseUntilUtcTicks;
    private long nextDueUtcTicks;

    public MicroPauseService(
        ILogger<MicroPauseService> logger,
        FeatureFlagService featureFlags,
        TimeProvider timeProvider,
        IServiceProvider services)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.timeProvider = timeProvider;
        this.services = services;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        timer = new Timer(OnTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        ScheduleNext(timeProvider.GetUtcNow());
        logger.LogInformation("[MicroPauseService] Started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        timer?.Change(Timeout.Infinite, Timeout.Infinite);
        logger.LogInformation("[MicroPauseService] Stopped");
        return Task.CompletedTask;
    }

    public bool TryGetMicroPauseExtraDelayMs(int minMs, int maxMs, out int extraDelayMs)
    {
        extraDelayMs = 0;

        long untilTicks = Interlocked.Read(ref microPauseUntilUtcTicks);
        if (untilTicks == 0)
        {
            return false;
        }

        long nowTicks = timeProvider.GetUtcNow().UtcTicks;
        if (nowTicks >= untilTicks)
        {
            return false;
        }

        int min = Math.Max(0, minMs);
        int max = Math.Max(min, maxMs);
        extraDelayMs = Random.Shared.Next(min, max + 1);
        return extraDelayMs > 0;
    }

    private void OnTick(object? state)
    {
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();

            FeatureFlagsOptions flags = featureFlags.Current;
            if (flags.GlobalKillSwitch || !flags.Humanization.Enabled || !flags.Humanization.Behavior.MicroPauseEnabled)
            {
                return;
            }

            IBotController? botController = services.GetService<IBotController>();
            if (botController == null || !botController.IsBotActive)
            {
                return;
            }

            AddonBits? bits = services.GetService<AddonBits>();
            if (bits != null && bits.Combat())
            {
                return;
            }

            long dueTicks = Interlocked.Read(ref nextDueUtcTicks);
            if (dueTicks == 0 || now.UtcTicks < dueTicks)
            {
                return;
            }

            int meanMs = Math.Max(10, flags.Humanization.Behavior.MicroPauseIntervalSeconds * 1000);
            int stdDevMs = Math.Max(10, meanMs / 4);
            int nextIntervalMs = HumanizedRandom.NextGaussianInt(meanMs, stdDevMs, meanMs / 2, meanMs * 2);

            int minPause = Math.Max(0, flags.Humanization.Behavior.MicroPauseMinMs);
            int maxPause = Math.Max(minPause, flags.Humanization.Behavior.MicroPauseMaxMs);
            int pauseMs = HumanizedRandom.NextGaussianInt(
                mean: (minPause + maxPause) / 2.0,
                stdDev: Math.Max(10, (maxPause - minPause) / 4.0),
                min: minPause,
                max: maxPause);

            Interlocked.Exchange(ref microPauseUntilUtcTicks, (now + TimeSpan.FromMilliseconds(pauseMs)).UtcTicks);
            Interlocked.Exchange(ref nextDueUtcTicks, (now + TimeSpan.FromMilliseconds(nextIntervalMs)).UtcTicks);

            logger.LogDebug("[MicroPauseService] Micro-pause window {Ms}ms", pauseMs);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MicroPauseService] Tick failed");
        }
    }

    private void ScheduleNext(DateTimeOffset now)
    {
        int initialMs = 30_000;
        try
        {
            FeatureFlagsOptions flags = featureFlags.Current;
            if (flags.Humanization.Enabled)
            {
                int meanMs = Math.Max(10, flags.Humanization.Behavior.MicroPauseIntervalSeconds * 1000);
                int stdDevMs = Math.Max(10, meanMs / 4);
                initialMs = HumanizedRandom.NextGaussianInt(meanMs, stdDevMs, meanMs / 2, meanMs * 2);
            }
        }
        catch
        {
        }

        Interlocked.Exchange(ref nextDueUtcTicks, (now + TimeSpan.FromMilliseconds(initialMs)).UtcTicks);
    }

    public void Dispose()
    {
        timer?.Dispose();
    }
}
