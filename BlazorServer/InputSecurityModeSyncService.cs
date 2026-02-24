using System;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;

using Game;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorServer;

/// <summary>
/// Applies persisted InputSecurity feature flag mode to WowProcessInput at startup and on hot reload.
/// This keeps the runtime input mode aligned with runtime_feature_flags.json defaults.
/// </summary>
public sealed class InputSecurityModeSyncService : BackgroundService
{
    private readonly ILogger<InputSecurityModeSyncService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly object sync = new();

    private Action<FeatureFlagsOptions>? subscription;
    private bool? lastAppliedBackgroundMode;

    public InputSecurityModeSyncService(
        ILogger<InputSecurityModeSyncService> logger,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        FeatureFlagService? featureFlags = serviceProvider.GetService<FeatureFlagService>();
        if (featureFlags == null)
        {
            logger.LogWarning("[InputModeSync     ] FeatureFlagService unavailable");
            return;
        }

        subscription = flags => ApplyFromFeatureFlags(flags, "hot-reload");
        featureFlags.OnFlagsChanged += subscription;

        try
        {
            ApplyFromFeatureFlags(featureFlags.Current, "startup");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        finally
        {
            if (subscription != null)
            {
                featureFlags.OnFlagsChanged -= subscription;
            }
        }
    }

    private void ApplyFromFeatureFlags(FeatureFlagsOptions flags, string source)
    {
        bool backgroundCompatible = !flags.InputSecurity.FocusGuard && !flags.InputSecurity.HybridModifiers;
        bool mixedMode = flags.InputSecurity.FocusGuard != flags.InputSecurity.HybridModifiers;

        if (mixedMode)
        {
            logger.LogWarning(
                "[InputModeSync     ] Mixed InputSecurity flags from {Source} (FocusGuard={FocusGuard}, HybridModifiers={HybridModifiers}); coercing to ForegroundSafe mode",
                source,
                flags.InputSecurity.FocusGuard,
                flags.InputSecurity.HybridModifiers);
            backgroundCompatible = false;
        }

        lock (sync)
        {
            if (lastAppliedBackgroundMode == backgroundCompatible)
            {
                return;
            }

            lastAppliedBackgroundMode = backgroundCompatible;
        }

        try
        {
            WowProcessInput? wowInput = serviceProvider.GetService<WowProcessInput>();
            if (wowInput == null)
            {
                logger.LogDebug("[InputModeSync     ] WowProcessInput unavailable during {Source}", source);
                return;
            }

            wowInput.EmergencyReleaseAllKeys();
            wowInput.SetBackgroundCompatibleInputMode(backgroundCompatible);

            logger.LogInformation(
                "[InputModeSync     ] Applied {Mode} from FeatureFlags ({Source})",
                backgroundCompatible ? "BackgroundCompatible" : "ForegroundSafe",
                source);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[InputModeSync     ] Failed applying input mode from {Source}", source);
        }
    }
}
