using Core.FeatureFlags;
using Core.Humanization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.IO;

using Xunit;

namespace CoreUnitTests.Humanization;

public sealed class HumanizationProviderTimingTests
{
    [Fact]
    public void GetKeyHoldDurationMs_IncreasesWithFatigueMultiplier()
    {
        TestTimeProvider timeProvider = new(new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero));

        FeatureFlagsOptions flags = new()
        {
            Humanization = new HumanizationOptions
            {
                Enabled = true,
                InputTiming = new HumanizationInputTimingOptions
                {
                    KeyHoldMeanMs = 60,
                    KeyHoldStdDevMs = 10,
                    KeyHoldMinMs = 10,
                    KeyHoldMaxMs = 500,
                    ReactionMaxMs = 500
                },
                Fatigue = new HumanizationFatigueOptions
                {
                    Enabled = true,
                    BreakIntervalMinutes = 60,
                    BreakDurationMinMinutes = 1,
                    BreakDurationMaxMinutes = 1,
                    FatigueRatePerHour = 0.10,
                    MaxFatigueMultiplier = 2.0
                },
                MouseMovement = new HumanizationMouseMovementOptions
                {
                    Enabled = false
                },
                Behavior = new HumanizationBehaviorOptions
                {
                    MicroPauseEnabled = false
                }
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

        IServiceProvider services = new ServiceCollection().BuildServiceProvider();

        FatigueSimulator fatigueSimulator = new(NullLogger<FatigueSimulator>.Instance, timeProvider);
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

        const int baseMs = 100;
        double freshAverage = Average(provider, sampleCount: 15_000, baseMs);

        timeProvider.Advance(TimeSpan.FromHours(3)); // fatigue ~= 1.3x
        double fatiguedAverage = Average(provider, sampleCount: 15_000, baseMs);

        Assert.True(fatiguedAverage > (freshAverage * 1.15));
    }

    private static double Average(HumanizationProvider provider, int sampleCount, int baseMs)
    {
        long sum = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            sum += provider.GetKeyHoldDurationMs(baseMs);
        }

        return (double)sum / sampleCount;
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        private readonly T value = value;

        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset startUtc) : TimeProvider
    {
        private DateTimeOffset utcNow = startUtc;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan delta) => utcNow = utcNow + delta;
    }
}
