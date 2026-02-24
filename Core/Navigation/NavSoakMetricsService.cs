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
    private readonly object sync = new();

    private readonly ILogger<NavSoakMetricsService> logger;
    private readonly string outputDir;
    private readonly TimeSpan windowDuration;
    private readonly List<NavSoakWindow> completedWindows = new();
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private readonly HashSet<Goals.Navigation> attachedNavigations = [];

    private StuckDetector? stuckDetector;
    private string artifactPath = string.Empty;
    private DateTime soakStart;
    private int frontBypassActivations;
    private int successfulReconnects;
    private int stuckEvents;
    private int repeatStuckCount;
    private DateTime windowStart;
    private Vector3 lastStuckPosition;
    private float maxDeviation;
    private float deviationSum;
    private int deviationSampleCount;

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
        this.outputDir = ResolveOutputDir(outputDir ?? "logs");
        this.windowDuration = windowDuration ?? DefaultWindowDuration;
        ResetSessionStateLocked();

        if (stuckDetector != null && navigation != null)
        {
            AttachRuntimeSources(stuckDetector, navigation);
        }
        else
        {
            logger.LogDebug("[NavSoakMetrics ] Initialized with missing dependencies; awaiting session attachment");
        }

        logger.LogDebug("[NavSoakMetrics ] Output directory: {OutputDir}", this.outputDir);
    }

    public void Dispose()
    {
        lock (sync)
        {
            DetachAllLocked();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        CloseCurrentWindow();
        await WriteArtifactAsync(cancellationToken);
    }

    /// <summary>
    /// Late-binds the telemetry service to the active session runtime sources.
    /// Supports multiple transient Navigation instances per session while using
    /// StuckDetector identity to detect session boundaries.
    /// </summary>
    public void AttachRuntimeSources(StuckDetector sessionStuckDetector, Goals.Navigation sessionNavigation)
    {
        bool attachedNewNavigation = false;
        bool startedNewSession = false;

        lock (sync)
        {
            if (!ReferenceEquals(stuckDetector, sessionStuckDetector))
            {
                DetachAllLocked();
                ResetSessionStateLocked();

                stuckDetector = sessionStuckDetector;
                stuckDetector.OnStuckDetected += HandleStuckDetected;
                startedNewSession = true;
            }

            if (attachedNavigations.Add(sessionNavigation))
            {
                sessionNavigation.OnDynamicDetourApplied += HandleDetourApplied;
                sessionNavigation.OnSuccessfulReconnect += HandleSuccessfulReconnect;
                sessionNavigation.OnDeviationSample += HandleDeviationSampleReceived;
                attachedNewNavigation = true;
            }
        }

        if (startedNewSession)
        {
            logger.LogInformation("[NavSoakMetrics ] Attached to new session telemetry sources");
        }
        else if (attachedNewNavigation)
        {
            logger.LogDebug("[NavSoakMetrics ] Attached additional Navigation instance");
        }
    }

    private void HandleStuckDetected(StuckEventData data)
    {
        lock (sync)
        {
            stuckEvents++;

            float dx = data.Position.X - lastStuckPosition.X;
            float dy = data.Position.Y - lastStuckPosition.Y;
            if (MathF.Sqrt((dx * dx) + (dy * dy)) <= RepeatStuckRadius)
                repeatStuckCount++;

            lastStuckPosition = data.Position;
        }
        MaybeCloseWindow();
    }

    private void HandleDetourApplied()
    {
        lock (sync)
        {
            frontBypassActivations++;
        }
        MaybeCloseWindow();
    }

    private void HandleSuccessfulReconnect()
    {
        lock (sync)
        {
            successfulReconnects++;
        }
        MaybeCloseWindow();
    }

    private void HandleDeviationSampleReceived(float deviation)
    {
        lock (sync)
        {
            if (deviation > maxDeviation)
            {
                maxDeviation = deviation;
            }

            deviationSum += deviation;
            deviationSampleCount++;
        }
    }

    private void MaybeCloseWindow()
    {
        bool shouldClose;
        lock (sync)
        {
            shouldClose = (DateTime.UtcNow - windowStart) >= windowDuration;
        }

        if (!shouldClose)
            return;

        CloseCurrentWindow();
        _ = WriteArtifactAsync(CancellationToken.None);
    }

    private void CloseCurrentWindow()
    {
        NavSoakWindow? window = null;

        lock (sync)
        {
            if (stuckDetector == null || attachedNavigations.Count == 0)
            {
                return;
            }

            int tailRecalcFailures = 0;
            foreach (Goals.Navigation nav in attachedNavigations)
            {
                tailRecalcFailures += nav.TailRecalcFailures;
            }

            window = new NavSoakWindow
            {
                WindowStartUtc = windowStart,
                WindowEndUtc = DateTime.UtcNow,
                FrontBypassActivations = frontBypassActivations,
                SuccessfulReconnects = successfulReconnects,
                StuckEvents = stuckEvents,
                RepeatStuckCount = repeatStuckCount,
                TailRecalcFailures = tailRecalcFailures,
                MaxRouteDeviation = maxDeviation,
                AvgRouteDeviation = deviationSampleCount > 0 ? deviationSum / deviationSampleCount : 0f
            };
            completedWindows.Add(window);

            frontBypassActivations = 0;
            successfulReconnects = 0;
            stuckEvents = 0;
            repeatStuckCount = 0;
            maxDeviation = 0f;
            deviationSum = 0f;
            deviationSampleCount = 0;
            windowStart = DateTime.UtcNow;
        }

        logger.LogInformation(
            "[NavSoakMetrics ] Window closed: Bypass={Bypass} Reconnects={Reconnects} Stuck={Stuck} RepeatRate={Rate:F4}",
            window!.FrontBypassActivations,
            window.SuccessfulReconnects,
            window.StuckEvents,
            window.RepeatStuckRate);
    }

    private async Task WriteArtifactAsync(CancellationToken cancellationToken)
    {
        try
        {
            object artifact;
            lock (sync)
            {
                if (string.IsNullOrWhiteSpace(artifactPath) || completedWindows.Count == 0)
                    return;

                artifact = new
                {
                    SoakStartUtc = soakStart,
                    SoakEndUtc = DateTime.UtcNow,
                    Windows = completedWindows.ToArray()
                };
            }

            Directory.CreateDirectory(outputDir);

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

    private void ResetSessionStateLocked()
    {
        soakStart = DateTime.UtcNow;
        windowStart = soakStart;
        artifactPath = Path.Combine(outputDir, $"soak-nav-{soakStart:yyyyMMdd-HHmmss}.json");
        completedWindows.Clear();
        frontBypassActivations = 0;
        successfulReconnects = 0;
        stuckEvents = 0;
        repeatStuckCount = 0;
        lastStuckPosition = default;
        maxDeviation = 0f;
        deviationSum = 0f;
        deviationSampleCount = 0;
    }

    private static string ResolveOutputDir(string configuredOutputDir)
    {
        if (Path.IsPathRooted(configuredOutputDir))
        {
            return configuredOutputDir;
        }

        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? current = new(baseDir);
        while (current != null)
        {
            string solutionPath = Path.Combine(current.FullName, "MasterOfPuppets.sln");
            if (File.Exists(solutionPath))
            {
                return Path.GetFullPath(Path.Combine(current.FullName, configuredOutputDir));
            }

            current = current.Parent;
        }

        return configuredOutputDir;
    }

    private void DetachAllLocked()
    {
        if (stuckDetector != null)
        {
            stuckDetector.OnStuckDetected -= HandleStuckDetected;
            stuckDetector = null;
        }

        foreach (Goals.Navigation nav in attachedNavigations)
        {
            nav.OnDynamicDetourApplied -= HandleDetourApplied;
            nav.OnSuccessfulReconnect -= HandleSuccessfulReconnect;
            nav.OnDeviationSample -= HandleDeviationSampleReceived;
        }

        attachedNavigations.Clear();
    }
}
