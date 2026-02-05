using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Core.Launch;

/// <summary>
/// Produces live launch-readiness snapshots for UI and external callers.
/// </summary>
public sealed class LaunchReadinessService
{
    private readonly ILogger<LaunchReadinessService> logger;
    private readonly IBotStartGuard guard;
    private readonly LaunchOverrideState overrides;

    public event Action? SnapshotChanged;

    public LaunchReadinessSnapshot LastSnapshot { get; private set; } = new(
        IsLaunchReady: false,
        CanStartBot: false,
        TimestampUtc: DateTimeOffset.MinValue,
        Checks: Array.Empty<LaunchSubsystemCheck>(),
        Overrides: new LaunchOverrideSnapshot(false, false, false, false));

    public LaunchReadinessService(
        ILogger<LaunchReadinessService> logger,
        IBotStartGuard guard,
        LaunchOverrideState overrides)
    {
        this.logger = logger;
        this.guard = guard;
        this.overrides = overrides;

        overrides.Changed += OnOverridesChanged;
    }

    public LaunchReadinessSnapshot Evaluate(ClassConfiguration? classConfig, RouteInfo? routeInfo)
    {
        LaunchReadinessSnapshot snapshot = guard.Evaluate(classConfig, routeInfo);
        LastSnapshot = snapshot;
        SnapshotChanged?.Invoke();
        return snapshot;
    }

    public async Task<LaunchReadinessSnapshot> WaitForGreenAsync(
        ClassConfiguration? classConfig,
        RouteInfo? routeInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        LaunchReadinessSnapshot snapshot = Evaluate(classConfig, routeInfo);

        while (!snapshot.IsLaunchReady && !cancellationToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow - start > timeout)
            {
                return snapshot;
            }

            await Task.Delay(250, cancellationToken);
            snapshot = Evaluate(classConfig, routeInfo);
        }

        return snapshot;
    }

    private void OnOverridesChanged()
    {
        try
        {
            SnapshotChanged?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[LaunchReadinessService] SnapshotChanged handler failed");
        }
    }
}

