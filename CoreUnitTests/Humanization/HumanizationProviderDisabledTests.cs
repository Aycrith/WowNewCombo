using Core.FeatureFlags;
using Core.Humanization;
using CoreUnitTests.TestHelpers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;

using System;
using System.IO;

using Xunit;

namespace CoreUnitTests.Humanization;

public sealed class HumanizationProviderDisabledTests
{
    [Fact]
    public void DisabledProvider_ReturnsBaselineValues()
    {
        FeatureFlagsOptions flags = new()
        {
            Humanization = new HumanizationOptions
            {
                Enabled = false
            }
        };

        IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(flags);
        IOptions<FeatureFlagServiceOptions> serviceOptions = Options.Create(new FeatureFlagServiceOptions
        {
            ConfigFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "runtime_feature_flags.json")
        });

        FeatureFlagService featureFlags = new(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            serviceOptions);

        TimeProvider timeProvider = TimeProvider.System;
        FatigueSimulator fatigueSimulator = new(NullLogger<FatigueSimulator>.Instance, timeProvider);
        IServiceProvider services = new ServiceCollection().BuildServiceProvider();
        MicroPauseService microPauseService = new(
            NullLogger<MicroPauseService>.Instance,
            featureFlags,
            timeProvider,
            services);

        using HumanizationProvider provider = new(
            NullLogger<HumanizationProvider>.Instance,
            featureFlags,
            fatigueSimulator,
            microPauseService);

        Assert.False(provider.Enabled);
        Assert.Equal(1.0, provider.FatigueMultiplier);
        Assert.False(provider.IsOnBreak);
        Assert.Equal(TimeSpan.Zero, provider.RemainingBreakTime);

        Assert.Equal(123, provider.GetKeyHoldDurationMs(123));
        Assert.Equal(0, provider.GetInterKeyDelayMs(0));
        Assert.Equal(5, provider.GetInterKeyDelayMs(5));
        Assert.Equal(0, provider.GetPreActionReactionDelayMs(complexity: 2, isMovementAction: false));

        Span<Point> buffer = stackalloc Point[16];
        int count = provider.BuildMousePath(new Point(0, 0), new Point(10, 10), buffer);
        Assert.Equal(0, count);
    }

}
