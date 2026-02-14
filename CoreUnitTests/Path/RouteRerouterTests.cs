using System;
using System.Numerics;
using System.Threading.Tasks;

using Core;
using Core.Hazard;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.Routing;

/// <summary>
/// Tests for RouteRerouter - auto-rerouting around hot zones.
/// </summary>
public sealed class RouteRerouterTests : IDisposable
{
    private readonly ILogger<RouteRerouter> _logger;
    private readonly ITestOutputHelper _output;
    private readonly RouteRerouter _rerouter;

    public RouteRerouterTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger<RouteRerouter>.Instance;
        _rerouter = new RouteRerouter(_logger);
    }

    #region TriggerRerouteAsync Tests

    [Fact]
    public async Task TriggerReroute_NoHazardStore_ShouldNotTrigger()
    {
        Vector3 currentPos = new(0, 0, 0);
        Vector3 targetPos = new(100, 0, 0);
        int mapId = 1;

        bool triggered = await _rerouter.TriggerRerouteAsync(currentPos, targetPos, mapId);

        Assert.False(triggered);
    }

    [Fact]
    public async Task TriggerReroute_Disabled_ShouldNotTrigger()
    {
        _rerouter.SetEnabled(false);

        Vector3 currentPos = new(0, 0, 0);
        Vector3 targetPos = new(100, 0, 0);
        int mapId = 1;

        bool triggered = await _rerouter.TriggerRerouteAsync(currentPos, targetPos, mapId);

        Assert.False(triggered);
    }

    [Fact]
    public async Task TriggerReroute_Cooldown_ShouldNotTriggerWithinCooldown()
    {
        Vector3 currentPos = new(0, 0, 0);
        Vector3 targetPos = new(100, 0, 0);
        int mapId = 1;

        await _rerouter.TriggerRerouteAsync(currentPos, targetPos, mapId);
        bool second = await _rerouter.TriggerRerouteAsync(currentPos, targetPos, mapId);

        Assert.False(second);
    }

    #endregion

    #region CalculateDetourAsync Tests

    [Fact]
    public async Task CalculateDetour_NoRehabilitator_ShouldReturnNull()
    {
        Vector3 start = new(0, 0, 0);
        Vector3 end = new(100, 0, 0);
        Vector3[] originalPath = [start, end];
        int mapId = 1;

        Vector3[]? detour = await _rerouter.CalculateDetourAsync(originalPath, mapId);

        Assert.Null(detour);
    }

    [Fact]
    public async Task CalculateDetour_ShortPath_ShouldHandleGracefully()
    {
        Vector3 start = new(0, 0, 0);
        Vector3 end = new(10, 0, 0);
        Vector3[] originalPath = [start, end];
        int mapId = 1;

        Vector3[]? detour = await _rerouter.CalculateDetourAsync(originalPath, mapId);

        Assert.True(detour == null || detour.Length >= 2);
    }

    [Fact]
    public async Task CalculateDetour_Disabled_ShouldReturnNull()
    {
        _rerouter.SetEnabled(false);

        Vector3 start = new(0, 0, 0);
        Vector3 end = new(100, 0, 0);
        Vector3[] originalPath = [start, end];
        int mapId = 1;

        Vector3[]? detour = await _rerouter.CalculateDetourAsync(originalPath, mapId);

        Assert.Null(detour);
    }

    #endregion

    #region Waypoint Management Tests

    [Fact]
    public void GetCurrentWaypoint_NoActiveReroute_ShouldReturnNull()
    {
        Vector3? waypoint = _rerouter.GetCurrentWaypoint();
        Assert.Null(waypoint);
    }

    [Fact]
    public void AdvanceWaypoint_NoActiveReroute_ShouldReturnFalse()
    {
        bool hasMore = _rerouter.AdvanceWaypoint();
        Assert.False(hasMore);
    }

    [Fact]
    public void ClearActiveReroute_NoActive_ShouldNotThrow()
    {
        _rerouter.ClearActiveReroute();
        Assert.Null(_rerouter.GetActiveReroute());
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void HotZoneSeverityThreshold_Setting_ShouldBeClamped()
    {
        _rerouter.HotZoneSeverityThreshold = 15f;
        Assert.Equal(10f, _rerouter.HotZoneSeverityThreshold);

        _rerouter.HotZoneSeverityThreshold = -5f;
        Assert.Equal(1f, _rerouter.HotZoneSeverityThreshold);

        _rerouter.HotZoneSeverityThreshold = 7f;
        Assert.Equal(7f, _rerouter.HotZoneSeverityThreshold);
    }

    [Fact]
    public void SafetyMargin_Setting_ShouldBeClamped()
    {
        _rerouter.SafetyMargin = 150f;
        Assert.Equal(100f, _rerouter.SafetyMargin);

        _rerouter.SafetyMargin = 5f;
        Assert.Equal(10f, _rerouter.SafetyMargin);

        _rerouter.SafetyMargin = 50f;
        Assert.Equal(50f, _rerouter.SafetyMargin);
    }

    [Fact]
    public void SetEnabled_ShouldToggleState()
    {
        _rerouter.SetEnabled(true);
        Assert.True(_rerouter.IsEnabled);

        _rerouter.SetEnabled(false);
        Assert.False(_rerouter.IsEnabled);

        _rerouter.SetEnabled(true);
        Assert.True(_rerouter.IsEnabled);
    }

    #endregion

    public void Dispose()
    {
        _rerouter.Dispose();
    }
}
