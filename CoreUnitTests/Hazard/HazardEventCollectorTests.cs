using Core;
using Core.Database;
using Core.FeatureFlags;
using Core.Hazard;
using CoreUnitTests.TestHelpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SharedLib;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Xunit;

namespace CoreUnitTests.Hazard;

public sealed class HazardEventCollectorTests
{
    [Fact]
    public void HandleStuckDetected_RecordsStuckEventWithMappedFields()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        StuckDetector stuckDetector = CreateUninitializedStuckDetector();
        CombatLog combatLog = new(new AddonBits());
        PlayerReader playerReader = CreatePlayerReader();

        using HazardEventCollector collector = new(
            NullLogger<HazardEventCollector>.Instance,
            featureFlags,
            store,
            stuckDetector,
            combatLog,
            playerReader);

        DateTime now = DateTime.UtcNow;
        StuckEventData data = new()
        {
            Position = new Vector3(11f, 22f, 33f),
            MapX = 12.5f,
            MapY = 25.5f,
            MapId = 42,
            UIMapId = 84,
            Zone = "TestZone",
            DurationMs = 1234,
            AttemptCount = 3,
            Timestamp = now
        };

        InvokePrivateHandler(collector, "HandleStuckDetected", data);

        IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(42);
        HazardEvent captured = Assert.Single(events);

        Assert.Equal(HazardEventType.Stuck, captured.Type);
        Assert.Equal(data.Position, captured.WorldPosition);
        Assert.Equal(data.MapX, captured.MapX);
        Assert.Equal(data.MapY, captured.MapY);
        Assert.Equal(data.MapId, captured.MapId);
        Assert.Equal(data.UIMapId, captured.UIMapId);
        Assert.Equal(data.Zone, captured.Zone);
        Assert.Equal((int)data.DurationMs, captured.DurationMs);
        Assert.Equal(data.AttemptCount, captured.AttemptCount);
        Assert.Equal(now, captured.Timestamp);
    }

    [Fact]
    public void HandlePlayerDeath_RecordsDeathEvent()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        StuckDetector stuckDetector = CreateUninitializedStuckDetector();
        CombatLog combatLog = new(new AddonBits());
        PlayerReader playerReader = CreatePlayerReader();

        using HazardEventCollector collector = new(
            NullLogger<HazardEventCollector>.Instance,
            featureFlags,
            store,
            stuckDetector,
            combatLog,
            playerReader);

        InvokePrivateHandler(collector, "HandlePlayerDeath");

        IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(7);
        HazardEvent captured = Assert.Single(events);
        Assert.Equal(HazardEventType.Death, captured.Type);
        Assert.Equal(7, captured.MapId);
        Assert.Equal(77, captured.UIMapId);
    }

    [Fact]
    public void HandleTargetEvade_RecordsEvadeEvent()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        StuckDetector stuckDetector = CreateUninitializedStuckDetector();
        CombatLog combatLog = new(new AddonBits());
        PlayerReader playerReader = CreatePlayerReader();

        using HazardEventCollector collector = new(
            NullLogger<HazardEventCollector>.Instance,
            featureFlags,
            store,
            stuckDetector,
            combatLog,
            playerReader);

        InvokePrivateHandler(collector, "HandleTargetEvade");

        IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(7);
        HazardEvent captured = Assert.Single(events);
        Assert.Equal(HazardEventType.TargetEvade, captured.Type);
        Assert.Equal(7, captured.MapId);
        Assert.Equal(77, captured.UIMapId);
    }

    [Fact]
    public void Handlers_DoNotRecord_WhenFeatureFlagDisabled()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: false);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        StuckDetector stuckDetector = CreateUninitializedStuckDetector();
        CombatLog combatLog = new(new AddonBits());
        PlayerReader playerReader = CreatePlayerReader();

        using HazardEventCollector collector = new(
            NullLogger<HazardEventCollector>.Instance,
            featureFlags,
            store,
            stuckDetector,
            combatLog,
            playerReader);

        InvokePrivateHandler(collector, "HandlePlayerDeath");
        InvokePrivateHandler(collector, "HandleTargetEvade");
        InvokePrivateHandler(collector, "HandleStuckDetected", new StuckEventData { MapId = 7, UIMapId = 77 });

        Assert.Empty(store.GetEventsSnapshot(7));
    }

    [Fact]
    public void Dispose_UnsubscribesFromAllEventSources()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        StuckDetector stuckDetector = CreateUninitializedStuckDetector();
        CombatLog combatLog = new(new AddonBits());
        PlayerReader playerReader = CreatePlayerReader();

        HazardEventCollector collector = new(
            NullLogger<HazardEventCollector>.Instance,
            featureFlags,
            store,
            stuckDetector,
            combatLog,
            playerReader);

        Assert.Equal(1, GetEventSubscriberCount(stuckDetector, "OnStuckDetected"));
        Assert.Equal(1, GetEventSubscriberCount(combatLog, "PlayerDeath"));
        Assert.Equal(1, GetEventSubscriberCount(combatLog, "TargetEvade"));

        collector.Dispose();

        Assert.Equal(0, GetEventSubscriberCount(stuckDetector, "OnStuckDetected"));
        Assert.Equal(0, GetEventSubscriberCount(combatLog, "PlayerDeath"));
        Assert.Equal(0, GetEventSubscriberCount(combatLog, "TargetEvade"));
    }

    [Fact]
    public void HandleStuckDetected_DeduplicatesNearIdenticalEvents()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        StuckDetector stuckDetector = CreateUninitializedStuckDetector();
        CombatLog combatLog = new(new AddonBits());
        PlayerReader playerReader = CreatePlayerReader();

        using HazardEventCollector collector = new(
            NullLogger<HazardEventCollector>.Instance,
            featureFlags,
            store,
            stuckDetector,
            combatLog,
            playerReader);

        DateTime now = DateTime.UtcNow;
        StuckEventData data = new()
        {
            Position = new Vector3(100f, 100f, 0f),
            MapX = 50f,
            MapY = 50f,
            MapId = 7,
            UIMapId = 77,
            Zone = "TestZone",
            DurationMs = 3000,
            AttemptCount = 1,
            Timestamp = now
        };

        InvokePrivateHandler(collector, "HandleStuckDetected", data);
        InvokePrivateHandler(collector, "HandleStuckDetected", data);

        IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(7);
        Assert.Single(events);
    }

    private static void InvokePrivateHandler(HazardEventCollector collector, string methodName, params object[] args)
    {
        MethodInfo? method = typeof(HazardEventCollector).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(collector, args);
    }

    private static int GetEventSubscriberCount(object target, string eventName)
    {
        Type type = target.GetType();
        FieldInfo? field = type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? type.GetField($"<{eventName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Delegate? delegates = field.GetValue(target) as Delegate;
        return delegates?.GetInvocationList().Length ?? 0;
    }

    private static StuckDetector CreateUninitializedStuckDetector()
    {
        // Constructor bypass avoids a large DI setup; this remains reflection-fragile by design.
        return (StuckDetector)RuntimeHelpers.GetUninitializedObject(typeof(StuckDetector));
    }

    private static PlayerReader CreatePlayerReader()
    {
        FakeAddonDataProvider provider = new();
        provider.Data[1] = 12_500_00;  // MapX -> 12.5
        provider.Data[2] = 25_500_00;  // MapY -> 25.5
        provider.Data[3] = 157_000;    // Direction
        provider.Data[4] = 77;         // UIMapId
        provider.Data[5] = 40;         // Level
        provider.Data[46] = 10801;     // Race/Class/Version packed value

        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        DataConfig dataConfig = new()
        {
            Root = root,
            Exp = "wrath"
        };

        WorldMapAreaDB worldMapAreaDB = new(dataConfig, NullLogger<WorldMapAreaDB>.Instance);
        // Constructor bypass avoids heavyweight database initialization for test-only map metadata wiring.
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
        Assert.NotNull(property);
        property.SetValue(playerReader, mapId);
    }

    private static void SetWorldMapArea(PlayerReader playerReader, WorldMapArea worldMapArea)
    {
        PropertyInfo? property = typeof(PlayerReader).GetProperty(nameof(PlayerReader.WorldMapArea));
        Assert.NotNull(property);
        property.SetValue(playerReader, worldMapArea);
    }

    private static FeatureFlagService CreateFeatureFlagService(bool enabled)
    {
        FeatureFlagsOptions options = new()
        {
            HazardAvoidance = new HazardAvoidanceOptions
            {
                Enabled = enabled,
                HazardCostMultiplier = 10f,
                DBSCANEpsilon = 25f,
                DBSCANMinPoints = 2,
                MaxEventsBeforePrune = 1000
            }
        };

        IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(options);
        FeatureFlagServiceOptions serviceOptions = new()
        {
            ConfigFilePath = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"), "runtime_feature_flags.json")
        };

        return new FeatureFlagService(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(serviceOptions));
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
