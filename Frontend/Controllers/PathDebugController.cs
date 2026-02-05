using Core.FeatureFlags;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using PPather;
using PPather.Graph;
using PPath = PPather.Graph.Path;

using SharedLib;
using SharedLib.Converters;

using System;
using System.Numerics;
using System.Text.Json;

namespace Frontend.Controllers;

#region Request/Response DTOs

public sealed record DebugPathRequest(
    float FromX,
    float FromY,
    float FromZ,
    float ToX,
    float ToY,
    float ToZ,
    SearchStrategy Strategy = SearchStrategy.A_Star_With_Model_Avoidance,
    bool? StartIndoors = null);

public sealed record DebugPathCompareRequest(
    float FromX,
    float FromY,
    float FromZ,
    float ToX,
    float ToY,
    float ToZ,
    SearchStrategy HazardStrategy = SearchStrategy.A_Star_With_Model_Avoidance,
    SearchStrategy BaselineStrategy = SearchStrategy.A_Star,
    bool? StartIndoors = null);

public sealed record DebugPathResponse(
    int MapId,
    SearchStrategy Strategy,
    int PointCount,
    float MaxHazardCost,
    Vector3[] Points);

public sealed record DebugPathCompareResponse(
    int MapId,
    SearchStrategy HazardStrategy,
    SearchStrategy BaselineStrategy,
    DebugPathResponse HazardPath,
    DebugPathResponse BaselinePath);

#endregion

[ApiController]
[Route("api/debug/path")]
public sealed class PathDebugController : ControllerBase
{
    private readonly ILogger<PathDebugController> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly PPatherService patherService;
    private readonly IHazardProvider hazardProvider;

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Converters =
        {
            new Vector3Converter(true)
        }
    };

    public PathDebugController(
        ILogger<PathDebugController> logger,
        FeatureFlagService featureFlags,
        PPatherService patherService,
        IHazardProvider hazardProvider)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.patherService = patherService;
        this.hazardProvider = hazardProvider;
    }

    /// <summary>
    /// POST /api/debug/path/{mapId}/route
    /// Computes a local PPather route between two world positions and reports hazard costs along the returned path.
    /// Requires DebugMode=true.
    /// </summary>
    [HttpPost("{mapId:int}/route")]
    [ProducesResponseType(typeof(DebugPathResponse), StatusCodes.Status200OK, "application/json")]
    public IActionResult Route(int mapId, [FromBody] DebugPathRequest request)
    {
        if (!featureFlags.Current.DebugMode)
        {
            return StatusCode(403, "DebugMode must be enabled to use path debug endpoints.");
        }

        try
        {
            DebugPathResponse response = ComputeRoute(mapId, request.FromX, request.FromY, request.FromZ, request.ToX, request.ToY, request.ToZ, request.Strategy, request.StartIndoors);
            return new JsonResult(response, jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PathDebug        ] Route failed (mapId={MapId})", mapId);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    /// POST /api/debug/path/{mapId}/compare
    /// Computes two PPather routes between the same endpoints (hazard vs baseline strategy) to help validate avoidance.
    /// Requires DebugMode=true.
    /// </summary>
    [HttpPost("{mapId:int}/compare")]
    [ProducesResponseType(typeof(DebugPathCompareResponse), StatusCodes.Status200OK, "application/json")]
    public IActionResult Compare(int mapId, [FromBody] DebugPathCompareRequest request)
    {
        if (!featureFlags.Current.DebugMode)
        {
            return StatusCode(403, "DebugMode must be enabled to use path debug endpoints.");
        }

        try
        {
            DebugPathResponse hazard = ComputeRoute(mapId, request.FromX, request.FromY, request.FromZ, request.ToX, request.ToY, request.ToZ, request.HazardStrategy, request.StartIndoors);
            DebugPathResponse baseline = ComputeRoute(mapId, request.FromX, request.FromY, request.FromZ, request.ToX, request.ToY, request.ToZ, request.BaselineStrategy, request.StartIndoors);

            DebugPathCompareResponse response = new(
                MapId: mapId,
                HazardStrategy: request.HazardStrategy,
                BaselineStrategy: request.BaselineStrategy,
                HazardPath: hazard,
                BaselinePath: baseline);

            return new JsonResult(response, jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PathDebug        ] Compare failed (mapId={MapId})", mapId);
            return StatusCode(500, ex.Message);
        }
    }

    private DebugPathResponse ComputeRoute(int mapId, float fromX, float fromY, float fromZ, float toX, float toY, float toZ, SearchStrategy strategy, bool? startIndoors)
    {
        patherService.Initialise(mapId);

        Vector4 from = new(fromX, fromY, fromZ, mapId);
        Vector4 to = new(toX, toY, toZ, mapId);

        // Ensure any required Z adjustment is applied when caller doesn't supply it.
        if (from.Z == 0 || to.Z == 0)
        {
            // If MPQs are missing this will simply return Vector4.Zero and the search will fail below.
            // Still useful for diagnostics.
            from = patherService.ToWorldZ(uiMap: 0, x: from.X, y: from.Y, z: from.Z, startIndoors);
            to = patherService.ToWorldZ(uiMap: 0, x: to.X, y: to.Y, z: to.Z, startIndoors);
        }

        patherService.SetLocations(from, to);

        PPath? path = patherService.DoSearch(strategy);
        if (path == null)
        {
            throw new InvalidOperationException("Path search failed. Check that MPQ data is installed and the coordinates are valid for the map.");
        }

        Vector3[] points = path.locations.Count == 0
            ? Array.Empty<Vector3>()
            : path.locations.ToArray();

        float maxHazard = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            float cost = hazardProvider.GetHazardCost(points[i], mapId);
            if (cost > maxHazard)
            {
                maxHazard = cost;
            }
        }

        return new DebugPathResponse(
            MapId: mapId,
            Strategy: strategy,
            PointCount: points.Length,
            MaxHazardCost: maxHazard,
            Points: points);
    }
}
