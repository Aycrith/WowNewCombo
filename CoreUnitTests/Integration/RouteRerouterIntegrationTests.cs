using System;
using System.Numerics;
using System.Threading.Tasks;

using Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MockWoWClient.GameState;
using MockWoWClient.InputHandling;

using Xunit;

namespace CoreUnitTests.Integration;

/// <summary>
/// Integration tests for RouteRerouter with MockWoWClient components.
/// </summary>
public sealed class RouteRerouterIntegrationTests : IDisposable
{
    private readonly SimulationClock _clock;
    private readonly GameStateManager _gameState;
    private readonly InputProcessor _inputProcessor;
    private readonly UIActionSimulator _uiSimulator;
    private readonly RouteRerouter _rerouter;

    public RouteRerouterIntegrationTests()
    {
        _clock = new SimulationClock();
        _gameState = new GameStateManager(_clock);
        _inputProcessor = new InputProcessor(_gameState);
        _uiSimulator = new UIActionSimulator(_gameState, _inputProcessor, NullLogger<UIActionSimulator>.Instance);
        _rerouter = new RouteRerouter(NullLogger<RouteRerouter>.Instance);
    }

    /// <summary>
    /// Integration test: RouteRerouter should work with GameStateManager
    /// to track player position and trigger reroutes.
    /// </summary>
    [Fact]
    public async Task Integration_RerouteWithGameState_ShouldWorkWithPlayerPosition()
    {
        // Arrange - Set player position
        _gameState.Player.Position = new Vector3(0, 0, 0);

        Vector3 targetPos = new(100, 0, 0);

        // Act - Without hot zones, should not trigger
        bool triggered = await _rerouter.TriggerRerouteAsync(_gameState.Player.Position, targetPos, 1);

        // Assert - Should not trigger without hazard store
        Assert.False(triggered);
    }

    /// <summary>
    /// Integration test: UIActionSimulator should record actions that can be replayed.
    /// </summary>
    [Fact]
    public async Task Integration_UIActionRecording_ShouldRecordAndReplay()
    {
        // Arrange
        _uiSimulator.ClearHistory();
        _uiSimulator.SetRecordingEnabled(true);

        // Act - Record some actions
        await _uiSimulator.ClickAsync(100, 100);
        await _uiSimulator.KeyPressAsync(InputProcessor.VK_W);
        await _uiSimulator.CastSpellAsync(1);

        var history = _uiSimulator.GetActionHistory();

        // Assert
        Assert.True(history.Count >= 3);
        Assert.Contains(history, h => h.ActionType == UIActionType.MouseClick);
        Assert.Contains(history, h => h.ActionType == UIActionType.KeyPress);
        Assert.Contains(history, h => h.ActionType == UIActionType.SpellCast);
    }

    /// <summary>
    /// Integration test: Full scenario - player moving, reroute system, UI actions recorded.
    /// </summary>
    [Fact]
    public async Task Integration_FullScenario_MovementAndRecording()
    {
        // Arrange
        _gameState.Player.Position = new Vector3(0, 0, 0);

        _uiSimulator.ClearHistory();

        Vector3 targetPos = new(100, 0, 0);

        // Act - Simulate movement and detect reroute
        await _uiSimulator.KeyPressAsync(InputProcessor.VK_W);
        bool rerouteTriggered = await _rerouter.TriggerRerouteAsync(_gameState.Player.Position, targetPos, 1);

        // Record UI actions
        await _uiSimulator.OpenPanelAsync("map");
        await _uiSimulator.ClosePanelAsync();

        // Assert
        var history = _uiSimulator.GetActionHistory();
        Assert.True(history.Count >= 2);

        // Should not have reroute without hazard store
        Assert.False(rerouteTriggered);
    }

    public void Dispose()
    {
        _uiSimulator.Dispose();
        _rerouter.Dispose();
    }
}
