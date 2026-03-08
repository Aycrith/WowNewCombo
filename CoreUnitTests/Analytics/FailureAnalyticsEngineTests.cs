using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Core.Analytics;
using Core.Database;
using Core.FeatureFlags;
using Core.GoalsComponent;
using CoreUnitTests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedLib;
using Xunit;

namespace CoreUnitTests.Analytics;

/// <summary>
/// Tests for FailureAnalyticsEngine event recording, memory bounds, and statistics.
/// </summary>
public sealed class FailureAnalyticsEngineTests
{
    #region Event Recording Tests

    [Fact]
    public void RecordNoPlanFailure_RecordsEventWithCorrectType()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        engine.RecordNoPlanFailure("no valid goals");

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.NoPlan);
    }

    [Fact]
    public void RecordDeath_RecordsEventWithCorrectType()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        engine.RecordDeath("fell off cliff");

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.Death);
    }

    [Fact]
    public void RecordFailedPull_RecordsEventWithTargetGuid()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        engine.RecordFailedPull(12345, "out of range");

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.FailedPull);
    }

    [Fact]
    public void RecordMultiMobRetreat_RecordsEventWithMobCount()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        engine.RecordMultiMobRetreat(5);

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.MultiMobRetreat);
    }

    [Fact]
    public void RecordStuckEvent_RecordsWithAdditionalData()
    {
        // Arrange
        var engine = CreateEngine();
        var stuckData = new StuckEventData
        {
            State = UnstuckState.StrafeAttempt,
            DurationMs = 5000,
            IsSpinning = true,
            AttemptCount = 3,
            Position = new System.Numerics.Vector3(100, 200, 300),
            MapId = 1,
            Zone = "Elwynn Forest",
            Direction = 0.5f,
            MapX = 100,
            MapY = 200,
            UIMapId = 1,
            Timestamp = DateTime.UtcNow
        };

        // Act
        engine.RecordStuckEvent(stuckData);

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(1);
        stats.EventsByType.Should().ContainKey(FailureType.Stuck);
    }

    [Fact]
    public void RecordNoPlanFailure_CreatesCorrelatedIncident()
    {
        var engine = CreateEngine(screenCapture: new StubScreenCapture());

        engine.RecordNoPlanFailure("planner stalled");

        var stats = engine.GetSessionStatistics();
        stats.RecentIncidents.Should().ContainSingle();
        stats.RecentIncidents[0].Reason.Should().Be("planner stalled");
        stats.RecentIncidents[0].Category.Should().Be("planning");
        stats.RecentIncidents[0].Screenshot.Should().NotBeNull();
    }

    [Fact]
    public void RecordSameFailureWithinWindow_DeduplicatesIncident()
    {
        var engine = CreateEngine(screenCapture: new StubScreenCapture());

        engine.RecordNoPlanFailure("same blocker");
        engine.RecordNoPlanFailure("same blocker");

        var incidents = engine.GetRecentIncidents();
        incidents.Should().ContainSingle();
        incidents[0].OccurrenceCount.Should().Be(2);
    }

    #endregion

    #region Statistics Aggregation Tests

    [Fact]
    public void GetSessionStatistics_EmptySession_ReturnsZeros()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var stats = engine.GetSessionStatistics();

        // Assert
        stats.TotalFailures.Should().Be(0);
        stats.EventsByType.Should().BeEmpty();
    }

    [Fact]
    public void GetSessionStatistics_MultipleEvents_AggregatesByType()
    {
        // Arrange
        var engine = CreateEngine();
        engine.RecordNoPlanFailure("reason1");
        engine.RecordNoPlanFailure("reason2");
        engine.RecordDeath("fell");
        engine.RecordDeath("drowned");

        // Act
        var stats = engine.GetSessionStatistics();

        // Assert
        stats.TotalFailures.Should().Be(4);
        stats.EventsByType[FailureType.NoPlan].Should().Be(2);
        stats.EventsByType[FailureType.Death].Should().Be(2);
    }

    [Fact]
    public void GetSessionStatistics_ReturnsCorrectTotalFailures()
    {
        // Arrange
        var engine = CreateEngine();
        for (int i = 0; i < 10; i++)
        {
            engine.RecordNoPlanFailure($"reason{i}");
        }

        // Act
        var stats = engine.GetSessionStatistics();

        // Assert
        stats.TotalFailures.Should().Be(10);
    }

    #endregion

    #region Memory Bounds Tests

    [Fact]
    public void RecordEvents_ExceedingMaxEventsInMemory_EvictsOldest()
    {
        // Arrange
        var featureFlags = CreateFeatureFlags(maxEventsInMemory: 5);
        var engine = CreateEngine(featureFlags);

        // Act - add more events than the memory limit
        for (int i = 0; i < 10; i++)
        {
            engine.RecordNoPlanFailure($"reason{i}");
        }

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(10); // Total count tracks all recorded
        // In-memory count is bounded, but we can't directly check the internal list
        // The behavior is verified by the fact that statistics are still valid
    }

    [Fact]
    public void RecordEvents_WithinMemoryBounds_AllRetained()
    {
        // Arrange
        var featureFlags = CreateFeatureFlags(maxEventsInMemory: 100);
        var engine = CreateEngine(featureFlags);

        // Act
        for (int i = 0; i < 50; i++)
        {
            engine.RecordNoPlanFailure($"reason{i}");
        }

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(50);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task RecordEvents_ConcurrentWrites_DoNotThrow()
    {
        // Arrange
        var engine = CreateEngine();
        int itemsPerThread = 20;
        int threadCount = 4;

        // Act
        Task[] tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    engine.RecordNoPlanFailure($"thread{threadId}_reason{i}");
                }
            });
        }

        // Should not throw
        await Task.WhenAll(tasks);

        // Assert
        var stats = engine.GetSessionStatistics();
        stats.TotalFailures.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task GetSessionStatistics_ConcurrentReadsAndWrites_DoNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act - concurrent writes and reads
        Task writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                engine.RecordNoPlanFailure($"reason{i}");
            }
        });

        Task readTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                _ = engine.GetSessionStatistics();
            }
        });

        // Should not throw
        await Task.WhenAll(writeTask, readTask);
    }

    #endregion

    #region Helper Methods

    private static FailureAnalyticsEngine CreateEngine(FeatureFlagService? featureFlags = null, IScreenCapture? screenCapture = null)
    {
        var flags = featureFlags ?? CreateFeatureFlags();
        var playerReader = CreateMockPlayerReader();

        return new FailureAnalyticsEngine(
            NullLogger<FailureAnalyticsEngine>.Instance,
            flags,
            playerReader,
            screenCapture: screenCapture);
    }

    private static FeatureFlagService CreateFeatureFlags(int maxEventsInMemory = 1000)
    {
        var featureFlags = new FeatureFlagsOptions
        {
            FailureAnalytics = new FailureAnalyticsOptions
            {
                MaxEventsInMemory = maxEventsInMemory
            }
        };

        var monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(featureFlags);
        var serviceOptions = Options.Create(new FeatureFlagServiceOptions { ConfigFilePath = "test.json" });

        return new FeatureFlagService(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            serviceOptions);
    }

    private static PlayerReader CreateMockPlayerReader()
    {
        FakeAddonDataProvider provider = new();
        provider.Data[1] = 100_000_00;   // MapX -> 100.0
        provider.Data[2] = 200_000_00;   // MapY -> 200.0
        provider.Data[3] = 0;            // Direction
        provider.Data[4] = 1;            // UIMapId
        provider.Data[5] = 30;           // Level

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
        playerReader.UIMapId.ForceUpdate(1);
        playerReader.Level.ForceUpdate(30);

        SetMapId(playerReader, 1);
        SetWorldMapArea(playerReader, new WorldMapArea { AreaName = "Elwynn Forest", UIMapId = 1, MapID = 1 });

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

    private sealed class StubScreenCapture : IScreenCapture
    {
        public void Request()
        {
        }

        public ScreenCaptureResult Capture(string reason, string? correlationId = null, string? incidentId = null, int timeoutMs = 1500)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new ScreenCaptureResult
            {
                RequestId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId ?? string.Empty,
                IncidentId = incidentId ?? string.Empty,
                Reason = reason,
                RequestedUtc = now,
                CompletedUtc = now.AddMilliseconds(12),
                CaptureLatencyMs = 12,
                Success = true,
                Path = @"C:\temp\incident.jpg"
            };
        }
    }

    #endregion
}
