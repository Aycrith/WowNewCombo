using Core.Goals;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Navigation;

/// <summary>
/// Session-scoped service accumulating navigation soak metrics and persisting them
/// to logs/soak-nav-YYYYMMDD-HHmmss.json at window close and on demand.
/// </summary>
public sealed class NavSoakMetricsService : IDisposable
{
    private static readonly TimeSpan DefaultWindowDuration = TimeSpan.FromMinutes(10);
    private const float RepeatStuckRadius = 10f;

    private readonly ILogger<NavSoakMetricsService> logger;
    private readonly StuckDetector stuckDetector;
    private readonly Goals.Navigation navigation;
    private readonly string outputDir;
    private readonly TimeSpan windowDuration;
    private readonly string artifactPath;
    private readonly DateTime soakStart = DateTime.UtcNow;
    private readonly List<NavSoakWindow> completedWindows = new();
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    private int frontBypassActivations;
    private int successfulReconnects;
    private int stuckEvents;
    private int repeatStuckCount;
    private DateTime windowStart;
    private Vector3 lastStuckPosition;

    public int CurrentWindowFrontBypassActivations => frontBypassActivations;
    public int CurrentWindowSuccessfulReconnects => successfulReconnects;
    public int CurrentWindowStuckEvents => stuckEvents;
    public int CurrentWindowRepeatStuckCount => repeatStuckCount;
    public double CurrentWindowRepeatStuckRate =>
        stuckEvents == 0 ? 0.0 : Math.Round((double)repeatStuckCount / stuckEvents, 4);

    public NavSoakMetricsService(
        ILogger<NavSoakMetricsService> logger,
        StuckDetector? stuckDetector = null,
        Goals.Navigation? navigation = null,
        string? outputDir = null,
        TimeSpan? windowDuration = null)
    {
        this.logger = logger;
        this.stuckDetector = stuckDetector;
        this.navigation = navigation;
        this.outputDir = outputDir ?? "logs";
        this.windowDuration = windowDuration ?? DefaultWindowDuration;
        artifactPath = Path.Combine(this.outputDir,
            $"soak-nav-{soakStart:yyyyMMdd-HHmmss}.json");
        windowStart = soakStart;

        // Wire event handlers only if dependencies are available (may be null during Phase 1)
        if (stuckDetector != null)
            stuckDetector.OnStuckDetected += HandleStuckDetected;

        if (navigation != null)
        {
            navigation.OnDynamicDetourApplied += HandleDetourApplied;
            navigation.OnSuccessfulReconnect += HandleSuccessfulReconnect;
        }

        if (stuckDetector == null || navigation == null)
            logger.LogDebug("[NavSoakMetrics ] Initialized with missing dependencies; will activate when available");
    }

    public void Dispose()
    {
        if (stuckDetector != null)
            stuckDetector.OnStuckDetected -= HandleStuckDetected;

        if (navigation != null)
        {
            navigation.OnDynamicDetourApplied -= HandleDetourApplied;
            navigation.OnSuccessfulReconnect -= HandleSuccessfulReconnect;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        CloseCurrentWindow();
        await WriteArtifactAsync(cancellationToken);
    }

    private void HandleStuckDetected(StuckEventData data)
    {
        Interlocked.Increment(ref stuckEvents);

        float dx = data.Position.X - lastStuckPosition.X;
        float dy = data.Position.Y - lastStuckPosition.Y;
        if (MathF.Sqrt((dx * dx) + (dy * dy)) <= RepeatStuckRadius)
            Interlocked.Increment(ref repeatStuckCount);

        lastStuckPosition = data.Position;
        MaybeCloseWindow();
    }

    private void HandleDetourApplied()
    {
        Interlocked.Increment(ref frontBypassActivations);
        MaybeCloseWindow();
    }

    private void HandleSuccessfulReconnect()
    {
        Interlocked.Increment(ref successfulReconnects);
        MaybeCloseWindow();
    }

    private void MaybeCloseWindow()
    {
        if (DateTime.UtcNow - windowStart < windowDuration)
            return;

        CloseCurrentWindow();
        _ = WriteArtifactAsync(CancellationToken.None);
    }

    private void CloseCurrentWindow()
    {
        NavSoakWindow window = new()
        {
            WindowStartUtc = windowStart,
            WindowEndUtc = DateTime.UtcNow,
            FrontBypassActivations = frontBypassActivations,
            SuccessfulReconnects = successfulReconnects,
            StuckEvents = stuckEvents,
            RepeatStuckCount = repeatStuckCount,
            TailRecalcFailures = navigation.TailRecalcFailures
        };
        completedWindows.Add(window);

        Interlocked.Exchange(ref frontBypassActivations, 0);
        Interlocked.Exchange(ref successfulReconnects, 0);
        Interlocked.Exchange(ref stuckEvents, 0);
        Interlocked.Exchange(ref repeatStuckCount, 0);
        windowStart = DateTime.UtcNow;

        logger.LogInformation(
            "[NavSoakMetrics ] Window closed: Bypass={Bypass} Reconnects={Reconnects} Stuck={Stuck} RepeatRate={Rate:F4}",
            window.FrontBypassActivations,
            window.SuccessfulReconnects,
            window.StuckEvents,
            window.RepeatStuckRate);
    }

    private async Task WriteArtifactAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(outputDir);

            object artifact = new { SoakStartUtc = soakStart, SoakEndUtc = DateTime.UtcNow, Windows = completedWindows };

            await using FileStream stream = new(
                artifactPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 16 * 1024, useAsync: true);

            await JsonSerializer.SerializeAsync(stream, artifact, jsonOptions, cancellationToken);

            logger.LogInformation("[NavSoakMetrics ] Artifact written: {Path}", artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[NavSoakMetrics ] Failed to write soak artifact");
        }
    }
}
