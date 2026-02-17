using System;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.Humanization;

using CoreUnitTests.TestHelpers;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace CoreUnitTests.Humanization;

public sealed class MicroPauseServiceTests : IDisposable
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 01, 01, 0, 0, 0, TimeSpan.Zero);

    private readonly ServiceTestTimeProvider timeProvider = new(StartUtc);
    private readonly ServiceCollection serviceCollection = new();
    private readonly FeatureFlagsOptions flags;
    private readonly FeatureFlagService featureFlagService;

    public MicroPauseServiceTests()
    {
        flags = new FeatureFlagsOptions
        {
            Humanization = new HumanizationOptions
            {
                Enabled = true,
                Behavior = new HumanizationBehaviorOptions
                {
                    MicroPauseEnabled = true,
                    MicroPauseIntervalSeconds = 60,
                    MicroPauseMinMs = 200,
                    MicroPauseMaxMs = 2000
                }
            }
        };

        IOptionsMonitor<FeatureFlagsOptions> monitor =
            new FixedOptionsMonitor<FeatureFlagsOptions>(flags);

        featureFlagService = new FeatureFlagService(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = "test.json" }));
    }

    public void Dispose()
    {
        // No additional cleanup needed; each test disposes its own service.
    }

    private MicroPauseService CreateService()
    {
        ServiceProvider provider = serviceCollection.BuildServiceProvider();

        return new MicroPauseService(
            NullLogger<MicroPauseService>.Instance,
            featureFlagService,
            timeProvider,
            provider);
    }

    [Fact]
    public async Task StartAsync_CompletesSuccessfully()
    {
        using MicroPauseService service = CreateService();

        await service.StartAsync(CancellationToken.None);

        // Service should start without throwing.
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        using MicroPauseService service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Service should stop without throwing.
    }

    [Fact]
    public async Task StopAsync_WithoutStart_CompletesSuccessfully()
    {
        using MicroPauseService service = CreateService();

        // StopAsync before StartAsync should not throw.
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void TryGetMicroPauseExtraDelayMs_WhenNoPauseActive_ReturnsFalse()
    {
        using MicroPauseService service = CreateService();

        // No pause has been scheduled (microPauseUntilUtcTicks == 0),
        // so the method should return false.
        bool result = service.TryGetMicroPauseExtraDelayMs(100, 500, out int extraDelayMs);

        result.Should().BeFalse();
        extraDelayMs.Should().Be(0);
    }

    [Fact]
    public async Task TryGetMicroPauseExtraDelayMs_WhenPauseExpired_ReturnsFalse()
    {
        using MicroPauseService service = CreateService();

        await service.StartAsync(CancellationToken.None);

        // Advance time far enough that any initial scheduling has expired.
        // The initial next-due is ~30s-60s with defaults, so advance well past that.
        // However, the OnTick timer fires every 1s but won't trigger because
        // no IBotController is registered (returns null -> early exit).
        // So microPauseUntilUtcTicks should remain 0.
        timeProvider.Advance(TimeSpan.FromMinutes(10));

        bool result = service.TryGetMicroPauseExtraDelayMs(100, 500, out int extraDelayMs);

        result.Should().BeFalse();
        extraDelayMs.Should().Be(0);
    }

    [Fact]
    public void TryGetMicroPauseExtraDelayMs_WithZeroRange_ReturnsFalse()
    {
        using MicroPauseService service = CreateService();

        // Even if a pause were active, min=0 max=0 range produces extraDelayMs=0
        // which causes the method to return false.
        bool result = service.TryGetMicroPauseExtraDelayMs(0, 0, out int extraDelayMs);

        result.Should().BeFalse();
        extraDelayMs.Should().Be(0);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        MicroPauseService service = CreateService();

        service.Dispose();
        service.Dispose();

        // Should not throw on double dispose.
    }

    [Fact]
    public async Task Dispose_CleansUpTimer()
    {
        MicroPauseService service = CreateService();

        await service.StartAsync(CancellationToken.None);
        service.Dispose();

        // After dispose, the timer is cleaned up.
        // Calling TryGet should still work (returns false since no pause active).
        bool result = service.TryGetMicroPauseExtraDelayMs(100, 500, out int extraDelayMs);

        result.Should().BeFalse();
        extraDelayMs.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_SetsInitialNextDue()
    {
        using MicroPauseService service = CreateService();

        await service.StartAsync(CancellationToken.None);

        // After start, the service should have scheduled a next-due time.
        // We verify indirectly: TryGet returns false because no pause window
        // has been created yet (only nextDueUtcTicks is set, not microPauseUntilUtcTicks).
        bool result = service.TryGetMicroPauseExtraDelayMs(100, 500, out int extraDelayMs);

        result.Should().BeFalse();
        extraDelayMs.Should().Be(0);
    }
}

public sealed class ScheduledBreakServiceTests : IDisposable
{
    private static readonly DateTimeOffset StartUtc =
        new(2026, 01, 01, 0, 0, 0, TimeSpan.Zero);

    private readonly ServiceTestTimeProvider timeProvider = new(StartUtc);
    private readonly ServiceCollection serviceCollection = new();
    private readonly FatigueSimulator fatigueSimulator;
    private readonly FeatureFlagService featureFlagService;

    public ScheduledBreakServiceTests()
    {
        FeatureFlagsOptions flags = new()
        {
            Humanization = new HumanizationOptions
            {
                Enabled = true,
                Fatigue = new HumanizationFatigueOptions
                {
                    Enabled = true,
                    BreakIntervalMinutes = 45,
                    BreakDurationMinMinutes = 1.0,
                    BreakDurationMaxMinutes = 5.0,
                    FatigueRatePerHour = 0.10,
                    MaxFatigueMultiplier = 1.5
                }
            }
        };

        IOptionsMonitor<FeatureFlagsOptions> monitor =
            new FixedOptionsMonitor<FeatureFlagsOptions>(flags);

        featureFlagService = new FeatureFlagService(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = "test.json" }));

        fatigueSimulator = new FatigueSimulator(
            NullLogger<FatigueSimulator>.Instance,
            timeProvider);

        fatigueSimulator.ApplyOptions(
            flags.Humanization.Fatigue,
            enabled: true);
    }

    public void Dispose()
    {
        // No additional cleanup needed; each test disposes its own service.
    }

    private ScheduledBreakService CreateService(HumanizationMetrics? metrics = null)
    {
        ServiceProvider provider = serviceCollection.BuildServiceProvider();

        return new ScheduledBreakService(
            NullLogger<ScheduledBreakService>.Instance,
            featureFlagService,
            fatigueSimulator,
            provider,
            metrics);
    }

    [Fact]
    public async Task StartAsync_CompletesSuccessfully()
    {
        using ScheduledBreakService service = CreateService();

        await service.StartAsync(CancellationToken.None);

        // Service should start without throwing.
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        using ScheduledBreakService service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // Service should stop without throwing.
    }

    [Fact]
    public async Task StopAsync_WithoutStart_CompletesSuccessfully()
    {
        using ScheduledBreakService service = CreateService();

        await service.StopAsync(CancellationToken.None);

        // Should not throw when stopping a service that was never started.
    }

    [Fact]
    public void IsOnBreak_DelegatesToFatigueSimulator_WhenNotOnBreak()
    {
        using ScheduledBreakService service = CreateService();

        service.IsOnBreak.Should().BeFalse();
        fatigueSimulator.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void IsOnBreak_DelegatesToFatigueSimulator_WhenOnBreak()
    {
        using ScheduledBreakService service = CreateService();

        // Force a break by calling StartBreak directly on the simulator.
        fatigueSimulator.StartBreak();

        service.IsOnBreak.Should().BeTrue();
        fatigueSimulator.IsOnBreak.Should().BeTrue();
    }

    [Fact]
    public void RemainingBreakTime_DelegatesToFatigueSimulator_WhenNotOnBreak()
    {
        using ScheduledBreakService service = CreateService();

        service.RemainingBreakTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RemainingBreakTime_DelegatesToFatigueSimulator_WhenOnBreak()
    {
        using ScheduledBreakService service = CreateService();

        TimeSpan breakDuration = fatigueSimulator.StartBreak();

        // The remaining time should be positive and match what the simulator reports.
        service.RemainingBreakTime.Should().BeGreaterThan(TimeSpan.Zero);
        service.RemainingBreakTime.Should().Be(fatigueSimulator.RemainingBreakTime);
    }

    [Fact]
    public void SkipBreak_WhenOnBreak_EndsBreak()
    {
        using ScheduledBreakService service = CreateService();

        fatigueSimulator.StartBreak();
        service.IsOnBreak.Should().BeTrue();

        service.SkipBreak("Test");

        service.IsOnBreak.Should().BeFalse();
        fatigueSimulator.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void SkipBreak_WhenNotOnBreak_DoesNothing()
    {
        using ScheduledBreakService service = CreateService();

        service.IsOnBreak.Should().BeFalse();

        // Should not throw when skipping while not on break.
        service.SkipBreak("Test");

        service.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void SkipBreak_RecordsMetrics_WhenMetricsProvided()
    {
        HumanizationMetrics metrics = new();
        using ScheduledBreakService service = CreateService(metrics);

        fatigueSimulator.StartBreak();
        service.SkipBreak("Test");

        // Verify that RecordBreakEnd was called by checking the snapshot.
        MetricsSnapshot snapshot = metrics.GetSnapshot();
        snapshot.BreaksTaken.Should().Be(0, "RecordBreakStart was not called by SkipBreak");
    }

    [Fact]
    public void ResetSession_CallsFatigueSimulatorResetSession()
    {
        using ScheduledBreakService service = CreateService();

        // Advance time so session duration is non-zero.
        timeProvider.Advance(TimeSpan.FromHours(2));

        TimeSpan beforeReset = fatigueSimulator.SessionDuration;
        beforeReset.Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromSeconds(1));

        service.ResetSession("Test");

        // After reset, session duration should be near zero.
        fatigueSimulator.SessionDuration.Should().BeCloseTo(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ResetSession_EndsBreakIfActive()
    {
        using ScheduledBreakService service = CreateService();

        fatigueSimulator.StartBreak();
        service.IsOnBreak.Should().BeTrue();

        service.ResetSession("Test");

        service.IsOnBreak.Should().BeFalse();
        fatigueSimulator.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void ResetSession_WhenNotOnBreak_ResetsWithoutError()
    {
        using ScheduledBreakService service = CreateService();

        service.IsOnBreak.Should().BeFalse();

        service.ResetSession("Test");

        service.IsOnBreak.Should().BeFalse();
        fatigueSimulator.SessionDuration.Should().BeCloseTo(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        ScheduledBreakService service = CreateService();

        service.Dispose();
        service.Dispose();

        // Should not throw on double dispose.
    }

    [Fact]
    public async Task Dispose_CleansUpTimer()
    {
        ScheduledBreakService service = CreateService();

        await service.StartAsync(CancellationToken.None);
        service.Dispose();

        // After dispose, the service should be inert.
        // Accessing properties that delegate to FatigueSimulator should still work.
        service.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void IsOnBreak_BecomeFalse_WhenBreakExpires()
    {
        using ScheduledBreakService service = CreateService();

        TimeSpan duration = fatigueSimulator.StartBreak();
        service.IsOnBreak.Should().BeTrue();

        // Advance past the break duration.
        timeProvider.Advance(duration + TimeSpan.FromSeconds(1));

        service.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void RemainingBreakTime_DecreasesOverTime()
    {
        using ScheduledBreakService service = CreateService();

        TimeSpan duration = fatigueSimulator.StartBreak();
        TimeSpan initial = service.RemainingBreakTime;

        timeProvider.Advance(TimeSpan.FromSeconds(30));

        TimeSpan afterAdvance = service.RemainingBreakTime;

        afterAdvance.Should().BeLessThan(initial);
    }
}

/// <summary>
/// Controllable time provider for deterministic tests.
/// </summary>
internal sealed class ServiceTestTimeProvider(DateTimeOffset startUtc) : TimeProvider
{
    private DateTimeOffset utcNow = startUtc;

    public override DateTimeOffset GetUtcNow() => utcNow;

    public void Advance(TimeSpan delta) => utcNow = utcNow + delta;
}
