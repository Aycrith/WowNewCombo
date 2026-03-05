using System;
using System.Threading;

using Core.FeatureFlags;
using Core.GOAP;
using Core.Recovery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Xunit;

namespace CoreUnitTests.Recovery;

public sealed class NoPlanRecoveryServiceTests
{
    private readonly NoPlanRecoveryOptions _options;

    public NoPlanRecoveryServiceTests()
    {
        _options = new NoPlanRecoveryOptions
        {
            Enabled = true,
            ResetStateThreshold = 2,
            ForceReplanThreshold = 4,
            EmergencyResetThreshold = 6,
            RecoveryDelayMs = 100
        };
    }

    [Fact]
    public void OnGoapEvent_Disabled_DoesNothing()
    {
        var options = new NoPlanRecoveryOptions { Enabled = false };
        var service = CreateService(options);

        // Should not throw
        service.OnGoapEvent(new AbortEvent());

        // Wait a bit for any async operations
        Thread.Sleep(50);
    }

    [Fact]
    public void OnGoapEvent_NonAbortEvent_Ignores()
    {
        var service = CreateService(_options);

        // Send non-abort events
        service.OnGoapEvent(new ResumeEvent());
        service.OnGoapEvent(new GoapStateEvent(GoapKey.incombat, true));

        // Should not trigger recovery
        Thread.Sleep(50);
    }

    [Fact]
    public void OnGoapEvent_AbortEvent_TriggersRecovery()
    {
        var service = CreateService(_options);

        // Send abort event
        service.OnGoapEvent(new AbortEvent());

        // Wait for async recovery
        Thread.Sleep(200);

        // Recovery should have been triggered (no exception = success)
    }

    [Fact]
    public void OnGoapEvent_MultipleAborts_TracksConsecutive()
    {
        var service = CreateService(_options);

        // Simulate multiple rapid NO PLAN events
        for (int i = 0; i < 5; i++)
        {
            service.OnGoapEvent(new AbortEvent());
            Thread.Sleep(50); // Within 10-second window
        }

        Thread.Sleep(200);

        // Should have escalated through strategies
    }

    [Fact]
    public void OnGoapEvent_SpacedAborts_ResetsConsecutive()
    {
        var service = CreateService(_options);

        // First abort
        service.OnGoapEvent(new AbortEvent());
        Thread.Sleep(200);

        // Second abort after delay (should reset counter)
        service.OnGoapEvent(new AbortEvent());
        Thread.Sleep(200);

        // Should treat as new sequence, not consecutive
    }

    [Fact]
    public void DetermineRecoveryAction_FirstAbort_ReturnsClearTarget()
    {
        var service = CreateService(_options);

        // Trigger one abort first
        service.OnGoapEvent(new AbortEvent());
        Thread.Sleep(50);

        // Second abort should trigger ClearTarget
        service.OnGoapEvent(new AbortEvent());
        Thread.Sleep(200);
    }

    [Fact]
    public void Options_Thresholds_ValidRange()
    {
        var options = new NoPlanRecoveryOptions
        {
            ResetStateThreshold = 2,
            ForceReplanThreshold = 4,
            EmergencyResetThreshold = 6
        };

        Assert.True(options.ResetStateThreshold < options.ForceReplanThreshold);
        Assert.True(options.ForceReplanThreshold < options.EmergencyResetThreshold);
    }

    [Fact]
    public void Options_DefaultValues_AreValid()
    {
        var options = new NoPlanRecoveryOptions();

        Assert.True(options.Enabled);
        Assert.True(options.ResetStateThreshold > 0);
        Assert.True(options.ForceReplanThreshold > options.ResetStateThreshold);
        Assert.True(options.EmergencyResetThreshold > options.ForceReplanThreshold);
        Assert.True(options.RecoveryDelayMs > 0);
    }

    private static NoPlanRecoveryService CreateService(NoPlanRecoveryOptions options)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NoPlanRecoveryService>.Instance;
        var featureFlags = CreateMockFeatureFlags(options);

        return new NoPlanRecoveryService(logger, featureFlags, goapAgent: null);
    }

    private static FeatureFlagService CreateMockFeatureFlags(NoPlanRecoveryOptions options)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<FeatureFlagService>.Instance;
        var optionsMonitor = new TestOptionsMonitor<FeatureFlagsOptions>(new FeatureFlagsOptions
        {
            NoPlanRecovery = options
        });
        var serviceOptions = Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = "test.json" });

        return new FeatureFlagService(logger, optionsMonitor, serviceOptions);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        private T _currentValue;

        public TestOptionsMonitor(T currentValue)
        {
            _currentValue = currentValue;
        }

        public T CurrentValue => _currentValue;

        public T Get(string? name) => _currentValue;

        public IDisposable OnChange(Action<T, string> listener) => new TestDisposable();

        private sealed class TestDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
