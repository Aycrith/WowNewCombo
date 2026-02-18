using Core.FeatureFlags;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Humanization;

public sealed class ScheduledBreakService : IHostedService, IDisposable
{
    private readonly ILogger<ScheduledBreakService> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly FatigueSimulator fatigueSimulator;
    private readonly IServiceProvider services;
    private readonly HumanizationMetrics? metrics;

    private Timer? timer;
    private bool disposed;

    public ScheduledBreakService(
        ILogger<ScheduledBreakService> logger,
        FeatureFlagService featureFlags,
        FatigueSimulator fatigueSimulator,
        IServiceProvider services,
        HumanizationMetrics? metrics = null)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.fatigueSimulator = fatigueSimulator;
        this.services = services;
        this.metrics = metrics;
    }

    public bool IsOnBreak => fatigueSimulator.IsOnBreak;

    public TimeSpan RemainingBreakTime => fatigueSimulator.RemainingBreakTime;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        timer = new Timer(OnTick, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        logger.LogInformation("[SchedBreakService] Started (checks every 30s)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        timer?.Change(Timeout.Infinite, Timeout.Infinite);
        logger.LogInformation("[SchedBreakService] Stopped");
        return Task.CompletedTask;
    }

    public void SkipBreak(string reason = "User")
    {
        if (!fatigueSimulator.IsOnBreak)
        {
            return;
        }

        fatigueSimulator.EndBreak();
        metrics?.RecordBreakEnd();
        logger.LogInformation("[SchedBreakService] Break skipped ({Reason})", reason);
    }

    public void ResetSession(string reason = "User")
    {
        if (fatigueSimulator.IsOnBreak)
        {
            fatigueSimulator.EndBreak();
        }

        fatigueSimulator.ResetSession();
        logger.LogInformation("[SchedBreakService] Session reset ({Reason})", reason);
    }

    private void OnTick(object? state)
    {
        try
        {
            FeatureFlagsOptions flags = featureFlags.Current;
            if (flags.GlobalKillSwitch || !flags.Humanization.Enabled || !flags.Humanization.Fatigue.Enabled)
            {
                return;
            }

            IBotController? botController = services.GetService<IBotController>();
            if (botController == null || !botController.IsBotActive)
            {
                return;
            }

            // Never start breaks during combat.
            AddonBits? bits = services.GetService<AddonBits>();
            if (bits != null && bits.Combat())
            {
                return;
            }

            if (fatigueSimulator.IsOnBreak)
            {
                return;
            }

            if (!fatigueSimulator.IsBreakDue())
            {
                return;
            }

            TimeSpan duration = fatigueSimulator.StartBreak();
            if (duration <= TimeSpan.Zero)
            {
                return;
            }

            metrics?.RecordBreakStart();
            logger.LogInformation(
                "[SchedBreakService] Break started {Minutes:F1} min (session={Hours:F2}h)",
                duration.TotalMinutes,
                fatigueSimulator.SessionDuration.TotalHours);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SchedBreakService] Tick failed");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer?.Dispose();
        timer = null;
    }
}
