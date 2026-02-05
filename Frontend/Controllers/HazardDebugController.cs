using Core.FeatureFlags;
using Core.Hazard;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Frontend.Controllers;

#region Response DTOs

public sealed record HazardMapSummary(
    int MapId,
    int EventCount,
    int ClusterCount,
    DateTime LatestEventUtc,
    DateTime LatestClusterIncidentUtc);

public sealed record HazardClusterDebugDto(
    Guid Id,
    int MapId,
    float SeverityScore,
    int EventCount,
    DateTime FirstIncidentUtc,
    DateTime LastIncidentUtc,
    float Radius,
    float CentroidX,
    float CentroidY,
    float CentroidZ);

public sealed record HazardDebugSnapshotResponse(
    DateTime GeneratedAtUtc,
    bool HazardAvoidanceEnabled,
    HazardAvoidanceOptions Options,
    int MapId,
    int TotalEventCount,
    int TotalClusterCount,
    IReadOnlyList<HazardEvent> Events,
    IReadOnlyList<HazardClusterDebugDto> Clusters);

public sealed record HazardInjectRequest(
    float X,
    float Y,
    float Z,
    int UIMapId,
    HazardEventType Type,
    int Count,
    int? AgeMinutes = null,
    string Zone = "Debug",
    float MapX = 0f,
    float MapY = 0f,
    int DurationMs = 0,
    int AttemptCount = 0);

public sealed record HazardInjectResponse(
    int MapId,
    int AddedEvents,
    int TotalEvents);

public sealed record HazardClusterNowResponse(
    int MapId,
    int TotalEvents,
    int ClusterCount,
    float Epsilon,
    int MinPoints,
    int HalfLifeDays);

public sealed record HazardClearResponse(int MapId);

#endregion

/// <summary>
/// Debug endpoints for inspecting hazard events and clusters at runtime.
/// Intended for validating heatmap + hazard path avoidance without attaching a debugger.
/// </summary>
[ApiController]
[Route("api/debug/hazards")]
public sealed class HazardDebugController : ControllerBase
{
    private const int DefaultMaxEvents = 250;
    private const int MaxMaxEvents = 10_000;
    private const int DefaultMaxClusters = 1_000;
    private const int MaxMaxClusters = 50_000;
    private const int DefaultInjectCount = 10;
    private const int MaxInjectCount = 100_000;

    private readonly ILogger<HazardDebugController> logger;
    private readonly HazardZoneStore hazardStore;
    private readonly FeatureFlagService featureFlagService;
    private readonly HazardClusterAnalyzer analyzer;

    public HazardDebugController(
        ILogger<HazardDebugController> logger,
        HazardZoneStore hazardStore,
        FeatureFlagService featureFlagService,
        HazardClusterAnalyzer analyzer)
    {
        this.logger = logger;
        this.hazardStore = hazardStore;
        this.featureFlagService = featureFlagService;
        this.analyzer = analyzer;
    }

    /// <summary>
    /// GET /api/debug/hazards/maps
    /// Returns known map IDs with basic counts so callers can discover available hazard data.
    /// </summary>
    [HttpGet("maps")]
    public ActionResult<IReadOnlyList<HazardMapSummary>> GetKnownMaps()
    {
        IReadOnlyList<int> mapIds = hazardStore.GetKnownMapIds();
        if (mapIds.Count == 0)
        {
            return Ok(Array.Empty<HazardMapSummary>());
        }

        List<HazardMapSummary> summaries = new(mapIds.Count);

        for (int i = 0; i < mapIds.Count; i++)
        {
            int mapId = mapIds[i];

            IReadOnlyList<HazardEvent> events = hazardStore.GetEventsSnapshot(mapId);
            IReadOnlyList<HazardCluster> clusters = hazardStore.GetClustersSnapshot(mapId);

            DateTime latestEventUtc = DateTime.MinValue;
            if (events.Count > 0)
            {
                latestEventUtc = events.Max(e => e.Timestamp);
            }

            DateTime latestClusterIncidentUtc = DateTime.MinValue;
            if (clusters.Count > 0)
            {
                latestClusterIncidentUtc = clusters.Max(c => c.LastIncident);
            }

            summaries.Add(new HazardMapSummary(
                mapId,
                events.Count,
                clusters.Count,
                latestEventUtc,
                latestClusterIncidentUtc));
        }

        return Ok(summaries);
    }

    /// <summary>
    /// POST /api/debug/hazards/{mapId}/inject
    /// Adds synthetic hazard events for UI/pathing validation.
    /// Requires FeatureFlagsOptions.DebugMode = true.
    /// </summary>
    [HttpPost("{mapId:int}/inject")]
    public ActionResult<HazardInjectResponse> InjectEvents(int mapId, [FromBody] HazardInjectRequest request)
    {
        if (!featureFlagService.Current.DebugMode)
        {
            return StatusCode(403, "DebugMode must be enabled to use synthetic hazard endpoints.");
        }

        int count = request.Count <= 0
            ? DefaultInjectCount
            : request.Count;

        count = Math.Clamp(count, 1, MaxInjectCount);

        int uiMapId = request.UIMapId;
        HazardEventType type = request.Type == 0 ? HazardEventType.ManualMarker : request.Type;

        DateTime timestampUtc = DateTime.UtcNow;
        if (request.AgeMinutes.HasValue && request.AgeMinutes.Value > 0)
        {
            timestampUtc = timestampUtc - TimeSpan.FromMinutes(request.AgeMinutes.Value);
        }

        HazardEvent[] events = new HazardEvent[count];

        Vector3 basePos = new(request.X, request.Y, request.Z);

        for (int i = 0; i < events.Length; i++)
        {
            events[i] = new HazardEvent
            {
                WorldPosition = basePos,
                MapX = request.MapX,
                MapY = request.MapY,
                MapId = mapId,
                UIMapId = uiMapId,
                Type = type,
                Timestamp = timestampUtc,
                Zone = request.Zone,
                DurationMs = request.DurationMs,
                AttemptCount = request.AttemptCount
            };
        }

        hazardStore.AddEvents(mapId, events);

        int total = hazardStore.GetEventsSnapshot(mapId).Count;
        return Ok(new HazardInjectResponse(mapId, events.Length, total));
    }

    /// <summary>
    /// POST /api/debug/hazards/{mapId}/cluster
    /// Runs DBSCAN clustering immediately for the given map ID and replaces in-memory clusters.
    /// Requires FeatureFlagsOptions.DebugMode = true.
    /// </summary>
    [HttpPost("{mapId:int}/cluster")]
    public ActionResult<HazardClusterNowResponse> ClusterNow(int mapId)
    {
        if (!featureFlagService.Current.DebugMode)
        {
            return StatusCode(403, "DebugMode must be enabled to use synthetic hazard endpoints.");
        }

        FeatureFlagsOptions flags = featureFlagService.Current;
        HazardAvoidanceOptions options = flags.HazardAvoidance;

        IReadOnlyList<HazardEvent> events = hazardStore.GetEventsSnapshot(mapId);

        List<HazardCluster> clusters = events.Count == 0
            ? []
            : analyzer.RunDBSCAN(
                events,
                options.DBSCANEpsilon,
                options.DBSCANMinPoints,
                options.DecayHalfLifeDays);

        hazardStore.ReplaceClusters(mapId, clusters);

        return Ok(new HazardClusterNowResponse(
            mapId,
            events.Count,
            clusters.Count,
            options.DBSCANEpsilon,
            options.DBSCANMinPoints,
            options.DecayHalfLifeDays));
    }

    /// <summary>
    /// POST /api/debug/hazards/{mapId}/clear
    /// Clears in-memory hazard events and clusters for the given map ID.
    /// Requires FeatureFlagsOptions.DebugMode = true.
    /// </summary>
    [HttpPost("{mapId:int}/clear")]
    public ActionResult<HazardClearResponse> Clear(int mapId)
    {
        if (!featureFlagService.Current.DebugMode)
        {
            return StatusCode(403, "DebugMode must be enabled to use synthetic hazard endpoints.");
        }

        hazardStore.ClearMap(mapId);
        return Ok(new HazardClearResponse(mapId));
    }

    /// <summary>
    /// GET /api/debug/hazards/{mapId}
    /// Returns a snapshot of current hazard events and/or clusters for the provided map.
    /// </summary>
    [HttpGet("{mapId:int}")]
    public ActionResult<HazardDebugSnapshotResponse> GetSnapshot(
        int mapId,
        [FromQuery] bool includeEvents = true,
        [FromQuery] bool includeClusters = true,
        [FromQuery] int maxEvents = DefaultMaxEvents,
        [FromQuery] int maxClusters = DefaultMaxClusters,
        [FromQuery] int? maxAgeMinutes = null,
        [FromQuery] bool mostRecentFirst = true)
    {
        if (maxEvents < 0)
        {
            maxEvents = 0;
        }
        if (maxEvents > MaxMaxEvents)
        {
            maxEvents = MaxMaxEvents;
        }

        if (maxClusters < 0)
        {
            maxClusters = 0;
        }
        if (maxClusters > MaxMaxClusters)
        {
            maxClusters = MaxMaxClusters;
        }

        FeatureFlagsOptions flags = featureFlagService.Current;
        bool hazardEnabled = flags.HazardAvoidance.Enabled;

        IReadOnlyList<HazardEvent> allEvents = includeEvents
            ? hazardStore.GetEventsSnapshot(mapId)
            : Array.Empty<HazardEvent>();

        IReadOnlyList<HazardCluster> allClusters = includeClusters
            ? hazardStore.GetClustersSnapshot(mapId)
            : Array.Empty<HazardCluster>();

        DateTime? minTimestampUtc = null;
        if (maxAgeMinutes.HasValue && maxAgeMinutes.Value > 0)
        {
            minTimestampUtc = DateTime.UtcNow - TimeSpan.FromMinutes(maxAgeMinutes.Value);
        }

        IReadOnlyList<HazardEvent> returnedEvents = includeEvents
            ? SelectEvents(allEvents, maxEvents, minTimestampUtc, mostRecentFirst)
            : Array.Empty<HazardEvent>();

        IReadOnlyList<HazardClusterDebugDto> returnedClusters = includeClusters
            ? SelectClusters(allClusters, maxClusters)
            : Array.Empty<HazardClusterDebugDto>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "[HazardDebug      ] mapId={MapId} Enabled={Enabled} Events={Events}/{TotalEvents} Clusters={Clusters}/{TotalClusters}",
                mapId,
                hazardEnabled,
                returnedEvents.Count,
                includeEvents ? allEvents.Count : 0,
                returnedClusters.Count,
                includeClusters ? allClusters.Count : 0);
        }

        HazardDebugSnapshotResponse response = new(
            GeneratedAtUtc: DateTime.UtcNow,
            HazardAvoidanceEnabled: hazardEnabled,
            Options: flags.HazardAvoidance,
            MapId: mapId,
            TotalEventCount: includeEvents ? allEvents.Count : 0,
            TotalClusterCount: includeClusters ? allClusters.Count : 0,
            Events: returnedEvents,
            Clusters: returnedClusters);

        return Ok(response);
    }

    private static IReadOnlyList<HazardEvent> SelectEvents(
        IReadOnlyList<HazardEvent> events,
        int maxEvents,
        DateTime? minTimestampUtc,
        bool mostRecentFirst)
    {
        if (events.Count == 0 || maxEvents == 0)
        {
            return Array.Empty<HazardEvent>();
        }

        if (minTimestampUtc == null && events.Count <= maxEvents && !mostRecentFirst)
        {
            return events;
        }

        List<HazardEvent> selected = new(Math.Min(maxEvents, events.Count));

        if (mostRecentFirst)
        {
            for (int i = events.Count - 1; i >= 0 && selected.Count < maxEvents; i--)
            {
                HazardEvent e = events[i];
                if (minTimestampUtc.HasValue && e.Timestamp < minTimestampUtc.Value)
                {
                    continue;
                }

                selected.Add(e);
            }
        }
        else
        {
            for (int i = 0; i < events.Count && selected.Count < maxEvents; i++)
            {
                HazardEvent e = events[i];
                if (minTimestampUtc.HasValue && e.Timestamp < minTimestampUtc.Value)
                {
                    continue;
                }

                selected.Add(e);
            }
        }

        return selected.Count == 0
            ? Array.Empty<HazardEvent>()
            : selected;
    }

    private static IReadOnlyList<HazardClusterDebugDto> SelectClusters(IReadOnlyList<HazardCluster> clusters, int maxClusters)
    {
        if (clusters.Count == 0 || maxClusters == 0)
        {
            return Array.Empty<HazardClusterDebugDto>();
        }

        int take = Math.Min(maxClusters, clusters.Count);

        List<HazardClusterDebugDto> dtos = new(take);
        for (int i = 0; i < clusters.Count; i++)
        {
            HazardCluster c = clusters[i];
            dtos.Add(new HazardClusterDebugDto(
                Id: c.Id,
                MapId: c.MapId,
                SeverityScore: c.SeverityScore,
                EventCount: c.EventCount,
                FirstIncidentUtc: c.FirstIncident,
                LastIncidentUtc: c.LastIncident,
                Radius: c.Radius,
                CentroidX: c.Centroid.X,
                CentroidY: c.Centroid.Y,
                CentroidZ: c.Centroid.Z));
        }

        dtos.Sort(static (a, b) => b.SeverityScore.CompareTo(a.SeverityScore));

        if (dtos.Count > take)
        {
            dtos.RemoveRange(take, dtos.Count - take);
        }

        return dtos;
    }
}
