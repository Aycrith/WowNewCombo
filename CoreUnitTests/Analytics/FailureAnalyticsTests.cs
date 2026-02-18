using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Core;
using Core.Analytics;
using Core.Database;
using Core.FeatureFlags;
using Core.GOAP;

using CoreUnitTests.TestHelpers;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SharedLib;

using Xunit;

namespace CoreUnitTests.Analytics;

public sealed class FailureAnalyticsTests : IDisposable
{
    private readonly FeatureFlagsOptions _enabledFlags;
    private readonly FeatureFlagsOptions _disabledFlags;

    public FailureAnalyticsTests()
    {
        _enabledFlags = new FeatureFlagsOptions
        {
            FailureAnalytics = new FailureAnalyticsOptions
            {
                Enabled = true,
                FlushIntervalMinutes = 60,
                MaxEventsInMemory = 100,
                MaxPersistedEvents = 1000,
                RetentionDays = 30
            }
        };

        _disabledFlags = new FeatureFlagsOptions
        {
            FailureAnalytics = new FailureAnalyticsOptions
            {
                Enabled = false
            }
        };
    }

    [Fact]
    public void StartAsync_WhenDisabled_DoesNothing()
    {
        using FailureAnalytics service = CreateService(_disabledFlags);

        // Should complete without error and without starting timers
        service.StartAsync(CancellationToken.None).Wait();
    }

    [Fact]
    public void RecordDeath_CreatesDeathEvent()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        service.RecordDeath("Killed by elite mob");

        FailureStatistics stats = service.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.Death);
        stats.EventsByType[FailureType.Death].Should().Be(1);
        stats.RecentEvents.Should().HaveCount(1);
        stats.RecentEvents[0].Type.Should().Be(FailureType.Death);
        stats.RecentEvents[0].Reason.Should().Be("Killed by elite mob");
    }

    [Fact]
    public void RecordDeath_WithNullCause_UsesDefaultMessage()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        service.RecordDeath();

        FailureStatistics stats = service.GetSessionStatistics();
        stats.RecentEvents[0].Reason.Should().Be("Player died");
    }

    [Fact]
    public void RecordFailedPull_CreatesFailedPullEvent()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        service.RecordFailedPull(12345, "Target evaded");

        FailureStatistics stats = service.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.FailedPull);
        stats.EventsByType[FailureType.FailedPull].Should().Be(1);
        stats.RecentEvents[0].Reason.Should().Be("Target evaded");
        stats.RecentEvents[0].TargetGuid.Should().Be(12345);
    }

    [Fact]
    public void RecordMultiMobRetreat_CreatesMultiMobRetreatEvent()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        service.RecordMultiMobRetreat(3);

        FailureStatistics stats = service.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.MultiMobRetreat);
        stats.RecentEvents[0].Reason.Should().Be("Retreated from 3 mobs");
    }

    [Fact]
    public void GetSessionStatistics_ReturnsCorrectCounts()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        service.RecordDeath("Death 1");
        service.RecordDeath("Death 2");
        service.RecordFailedPull(1, "Pull fail");
        service.RecordMultiMobRetreat(2);

        FailureStatistics stats = service.GetSessionStatistics();
        stats.TotalFailures.Should().Be(4);
        stats.EventsByType[FailureType.Death].Should().Be(2);
        stats.EventsByType[FailureType.FailedPull].Should().Be(1);
        stats.EventsByType[FailureType.MultiMobRetreat].Should().Be(1);
    }

    [Fact]
    public void GetSessionStatistics_RecentEvents_ReturnsLast10()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        for (int i = 0; i < 15; i++)
        {
            service.RecordDeath($"Death {i}");
        }

        FailureStatistics stats = service.GetSessionStatistics();
        stats.RecentEvents.Should().HaveCount(10);
        stats.RecentEvents[0].Reason.Should().Be("Death 5");
        stats.RecentEvents[9].Reason.Should().Be("Death 14");
    }

    [Fact]
    public void MemoryBounds_EventsTrimmedWhenExceedingMax()
    {
        FeatureFlagsOptions flags = new()
        {
            FailureAnalytics = new FailureAnalyticsOptions
            {
                Enabled = true,
                MaxEventsInMemory = 10,
                FlushIntervalMinutes = 60,
                MaxPersistedEvents = 100,
                RetentionDays = 30
            }
        };

        using FailureAnalytics service = CreateService(flags);

        // Add more events than the max
        for (int i = 0; i < 20; i++)
        {
            service.RecordDeath($"Death {i}");
        }

        FailureStatistics stats = service.GetSessionStatistics();

        // Events should be trimmed to MaxEventsInMemory
        stats.EventsByType[FailureType.Death].Should().BeLessOrEqualTo(10);

        // The oldest events should have been removed, keeping the newest
        stats.RecentEvents.Should().AllSatisfy(e =>
        {
            int deathNumber = int.Parse(e.Reason.Split(' ')[1]);
            deathNumber.Should().BeGreaterOrEqualTo(10,
                "oldest events should be trimmed first");
        });
    }

    [Fact]
    public void HotZoneDetection_GroupsNearbyFailures()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        // Record multiple failures at nearby positions (same 10-yard grid cell)
        // PlayerReader position is fixed, so all events will be at the same position
        service.RecordDeath("Death at pos A");
        service.RecordDeath("Death at pos B");
        service.RecordDeath("Death at pos C");

        FailureStatistics stats = service.GetSessionStatistics();

        // With 3 events at the same position, there should be a hot zone
        // (threshold is >= 2 events in same grid cell)
        stats.HotZones.Should().NotBeEmpty("3 failures at same position should create a hot zone");
        stats.HotZones[0].FailureCount.Should().BeGreaterOrEqualTo(2);
        stats.HotZones[0].PrimaryType.Should().Be(FailureType.Death);
    }

    [Fact]
    public void RecordedEvent_ContainsPlayerPosition()
    {
        using FailureAnalytics service = CreateService(_enabledFlags);

        service.RecordDeath("Test death");

        FailureStatistics stats = service.GetSessionStatistics();
        FailureEvent evt = stats.RecentEvents[0];

        // Position comes from PlayerReader.WorldPos - verify it's populated
        // The exact values depend on the FakeAddonDataProvider setup
        evt.Zone.Should().Be("Test Zone");
        evt.Level.Should().Be(40);
        evt.MapId.Should().Be(7);
    }

    public void Dispose()
    {
        // Individual tests handle their own cleanup via using statements
    }

    private static FailureAnalytics CreateService(FeatureFlagsOptions flags)
    {
        FeatureFlagService featureFlagService = CreateFeatureFlagService(flags);
        PlayerReader playerReader = CreatePlayerReader();

        FailureAnalyticsEngine engine = new(
            NullLogger<FailureAnalyticsEngine>.Instance,
            featureFlagService,
            playerReader);

        return new FailureAnalytics(
            NullLogger<FailureAnalytics>.Instance,
            featureFlagService,
            engine);
    }

    private static FeatureFlagService CreateFeatureFlagService(FeatureFlagsOptions flags)
    {
        IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(flags);
        IOptions<FeatureFlagServiceOptions> serviceOptions = Options.Create(new FeatureFlagServiceOptions());

        return new FeatureFlagService(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            serviceOptions);
    }

    private static PlayerReader CreatePlayerReader()
    {
        FakeAddonDataProvider provider = new();
        provider.Data[1] = 12_500_00;  // MapX -> 12.5
        provider.Data[2] = 25_500_00;  // MapY -> 25.5
        provider.Data[3] = 157_000;    // Direction
        provider.Data[4] = 77;         // UIMapId
        provider.Data[5] = 40;         // Level

        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        DataConfig dataConfig = new()
        {
            Root = root,
            Exp = "wrath"
        };

        WorldMapAreaDB worldMapAreaDB = new(dataConfig, NullLogger<WorldMapAreaDB>.Instance);
        AreaDB areaDb = (AreaDB)RuntimeHelpers.GetUninitializedObject(typeof(AreaDB));
        AddonBits bits = new();
        SpellInRange spellInRange = new();
        Stance stance = new();

        PlayerReader playerReader = new(provider, worldMapAreaDB, areaDb, bits, spellInRange, stance);
        playerReader.UIMapId.ForceUpdate(77);
        playerReader.Level.ForceUpdate(40);

        SetMapId(playerReader, 7);
        SetWorldMapArea(playerReader, new WorldMapArea { AreaName = "Test Zone", UIMapId = 77, MapID = 7 });

        return playerReader;
    }

    private static void SetMapId(PlayerReader playerReader, int mapId)
    {
        PropertyInfo? property = typeof(PlayerReader).GetProperty(nameof(PlayerReader.MapId));
        property!.SetValue(playerReader, mapId);
    }

    private static void SetWorldMapArea(PlayerReader playerReader, WorldMapArea worldMapArea)
    {
        PropertyInfo? property = typeof(PlayerReader).GetProperty(nameof(PlayerReader.WorldMapArea));
        property!.SetValue(playerReader, worldMapArea);
    }

    private sealed class FakeAddonDataProvider : IAddonDataProvider
    {
        public int[] Data { get; } = new int[256];

        public void UpdateData()
        {
        }

        public void InitFrames(DataFrame[] frames)
        {
        }

        public void Dispose()
        {
        }
    }
}
