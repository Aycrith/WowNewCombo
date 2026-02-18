using Core.FeatureFlags;
using Core.Humanization;

using Microsoft.Extensions.Logging.Abstractions;

using System;

using Xunit;

namespace CoreUnitTests.Humanization;

public sealed class FatigueSimulatorTests
{
    [Fact]
    public void FatigueMultiplier_IncreasesOverTime()
    {
        TestTimeProvider timeProvider = new(new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero));

        FatigueSimulator simulator = new(
            NullLogger<FatigueSimulator>.Instance,
            timeProvider);

        simulator.ApplyOptions(
            new HumanizationFatigueOptions
            {
                Enabled = true,
                BreakIntervalMinutes = 60,
                BreakDurationMinMinutes = 1,
                BreakDurationMaxMinutes = 1,
                FatigueRatePerHour = 0.10,
                MaxFatigueMultiplier = 2.0
            },
            enabled: true);

        Assert.InRange(simulator.FatigueMultiplier, 1.0, 1.0001);

        timeProvider.Advance(TimeSpan.FromHours(3));

        Assert.InRange(simulator.FatigueMultiplier, 1.29, 1.31);
    }

    [Fact]
    public void BreakDue_RespectsJitterWindow()
    {
        TestTimeProvider timeProvider = new(new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero));

        FatigueSimulator simulator = new(
            NullLogger<FatigueSimulator>.Instance,
            timeProvider);

        simulator.ApplyOptions(
            new HumanizationFatigueOptions
            {
                Enabled = true,
                BreakIntervalMinutes = 60,
                BreakDurationMinMinutes = 1,
                BreakDurationMaxMinutes = 1,
                FatigueRatePerHour = 0.0,
                MaxFatigueMultiplier = 1.5
            },
            enabled: true);

        timeProvider.Advance(TimeSpan.FromMinutes(51)); // 0.85 * 60, always before jitter min (0.90)
        Assert.False(simulator.IsBreakDue());

        timeProvider.Advance(TimeSpan.FromMinutes(18)); // total 69 minutes, always after jitter max (1.10)
        Assert.True(simulator.IsBreakDue());
    }

    [Fact]
    public void StartBreak_EntersAndLeavesBreak()
    {
        TestTimeProvider timeProvider = new(new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero));

        FatigueSimulator simulator = new(
            NullLogger<FatigueSimulator>.Instance,
            timeProvider);

        simulator.ApplyOptions(
            new HumanizationFatigueOptions
            {
                Enabled = true,
                BreakIntervalMinutes = 1,
                BreakDurationMinMinutes = 1,
                BreakDurationMaxMinutes = 1,
                FatigueRatePerHour = 0.0,
                MaxFatigueMultiplier = 1.5
            },
            enabled: true);

        TimeSpan duration = simulator.StartBreak();
        Assert.Equal(TimeSpan.FromMinutes(1), duration);

        Assert.True(simulator.IsOnBreak);
        Assert.InRange(simulator.RemainingBreakTime, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        timeProvider.Advance(TimeSpan.FromSeconds(61));
        Assert.False(simulator.IsOnBreak);
        Assert.Equal(TimeSpan.Zero, simulator.RemainingBreakTime);
    }

    private sealed class TestTimeProvider(DateTimeOffset startUtc) : TimeProvider
    {
        private DateTimeOffset utcNow = startUtc;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan delta) => utcNow = utcNow + delta;
    }
}
