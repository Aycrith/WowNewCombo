using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

using System;
using System.Numerics;

namespace Core.Hazard;

/// <summary>
/// Session-scoped collector that subscribes to bot events and records hazards.
/// </summary>
public sealed class HazardEventCollector : IDisposable
{
    private const float DuplicateStuckDistanceThreshold = 1.25f;
    private const int DuplicateStuckDurationToleranceMs = 250;
    private static readonly TimeSpan DuplicateStuckWindow = TimeSpan.FromSeconds(2);

    private readonly ILogger<HazardEventCollector> logger;
    private readonly FeatureFlagService featureFlagService;
    private readonly HazardZoneStore store;
    private readonly StuckDetector stuckDetector;
    private readonly CombatLog combatLog;
    private readonly PlayerReader playerReader;

    private readonly object duplicateGate = new();
    private HazardEvent? lastStuckEvent;

    public HazardEventCollector(
        ILogger<HazardEventCollector> logger,
        FeatureFlagService featureFlagService,
        HazardZoneStore store,
        StuckDetector stuckDetector,
        CombatLog combatLog,
        PlayerReader playerReader)
    {
        this.logger = logger;
        this.featureFlagService = featureFlagService;
        this.store = store;
        this.stuckDetector = stuckDetector;
        this.combatLog = combatLog;
        this.playerReader = playerReader;

        stuckDetector.OnStuckDetected += HandleStuckDetected;
        combatLog.PlayerDeath += HandlePlayerDeath;
        combatLog.TargetEvade += HandleTargetEvade;

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("[HazardCollector    ] Subscribed to events");
        }
    }

    public void Dispose()
    {
        stuckDetector.OnStuckDetected -= HandleStuckDetected;
        combatLog.PlayerDeath -= HandlePlayerDeath;
        combatLog.TargetEvade -= HandleTargetEvade;
    }

    private void HandleStuckDetected(StuckEventData data)
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return;
        }

        HazardEvent hazardEvent = new()
        {
            WorldPosition = data.Position,
            MapX = data.MapX,
            MapY = data.MapY,
            MapId = data.MapId,
            UIMapId = data.UIMapId,
            Type = HazardEventType.Stuck,
            Timestamp = data.Timestamp,
            Zone = data.Zone,
            DurationMs = (int)Math.Clamp(data.DurationMs, 0, int.MaxValue),
            AttemptCount = data.AttemptCount,
            PlayerClass = playerReader.Class.ToString(),
            PlayerLevel = playerReader.Level.Value
        };

        if (IsDuplicateStuckEvent(hazardEvent))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("[HazardCollector    ] Deduplicated stuck event at {Pos}", hazardEvent.WorldPosition);
            }

            return;
        }

        store.AddEvent(hazardEvent);

        logger.LogWarning(
            "[HazardCollector    ] Stuck at {Pos} ({Zone}) attempt {Attempt}",
            hazardEvent.WorldPosition,
            hazardEvent.Zone,
            hazardEvent.AttemptCount);
    }

    private void HandlePlayerDeath()
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return;
        }

        HazardEvent hazardEvent = new()
        {
            WorldPosition = playerReader.WorldPos,
            MapX = playerReader.MapX,
            MapY = playerReader.MapY,
            MapId = playerReader.MapId,
            UIMapId = playerReader.UIMapId.Value,
            Type = HazardEventType.Death,
            Zone = playerReader.WorldMapArea.AreaName,
            PlayerClass = playerReader.Class.ToString(),
            PlayerLevel = playerReader.Level.Value
        };

        store.AddEvent(hazardEvent);

        logger.LogError(
            "[HazardCollector    ] Death at {Pos} ({Zone})",
            hazardEvent.WorldPosition,
            hazardEvent.Zone);
    }

    private void HandleTargetEvade()
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return;
        }

        HazardEvent hazardEvent = new()
        {
            WorldPosition = playerReader.WorldPos,
            MapX = playerReader.MapX,
            MapY = playerReader.MapY,
            MapId = playerReader.MapId,
            UIMapId = playerReader.UIMapId.Value,
            Type = HazardEventType.TargetEvade,
            Zone = playerReader.WorldMapArea.AreaName,
            PlayerClass = playerReader.Class.ToString(),
            PlayerLevel = playerReader.Level.Value
        };

        store.AddEvent(hazardEvent);

        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "[HazardCollector    ] Target evade at {Pos} ({Zone})",
                hazardEvent.WorldPosition,
                hazardEvent.Zone);
        }
    }

    private bool IsDuplicateStuckEvent(HazardEvent candidate)
    {
        lock (duplicateGate)
        {
            if (lastStuckEvent == null)
            {
                lastStuckEvent = candidate;
                return false;
            }

            bool isRecent = Math.Abs((candidate.Timestamp - lastStuckEvent.Timestamp).TotalMilliseconds) <= DuplicateStuckWindow.TotalMilliseconds;
            bool isSameAttempt = candidate.AttemptCount == lastStuckEvent.AttemptCount;
            bool isSameMap = candidate.MapId == lastStuckEvent.MapId;
            bool isNear = Vector3.Distance(candidate.WorldPosition, lastStuckEvent.WorldPosition) <= DuplicateStuckDistanceThreshold;
            bool similarDuration = Math.Abs(candidate.DurationMs - lastStuckEvent.DurationMs) <= DuplicateStuckDurationToleranceMs;

            if (isRecent && isSameAttempt && isSameMap && isNear && similarDuration)
            {
                return true;
            }

            lastStuckEvent = candidate;
            return false;
        }
    }
}
