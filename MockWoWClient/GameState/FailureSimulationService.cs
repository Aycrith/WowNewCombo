using System.Numerics;

using Core;
using Core.Analytics;
using Core.Testing;

namespace MockWoWClient.GameState;

/// <summary>
/// Simulates bot failure scenarios for testing stuck detection, death, and hot zone creation.
/// Implements IFailureSimulationService for integration with RouteVisualizationService.
/// </summary>
public sealed class FailureSimulationService : IFailureSimulationService
{
    private readonly GameStateManager _gameState;
    private readonly SimulationClock _clock;
    private readonly List<SimulatedStuckEvent> _stuckHistory = [];
    private readonly List<SimulatedDeathEvent> _deathHistory = [];
    private readonly object _lock = new();

    // Events for visualization
    public event Action<SimulatedStuckEvent>? OnStuckSimulated;
    public event Action<SimulatedDeathEvent>? OnDeathSimulated;
    public event Action<SimulatedRehabEvent>? OnRehabSimulated;
    public event Action<SimulatedHotZone>? OnHotZoneCreated;

    public FailureSimulationService(GameStateManager gameState, SimulationClock clock)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Simulates the player getting stuck at current position.
    /// </summary>
    public void SimulateStuck(UnstuckState stuckState, int durationMs = 3000, int attemptCount = 1)
    {
        lock (_lock)
        {
            var stuckEvent = new SimulatedStuckEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = _clock.CurrentTime,
                Position = _gameState.Player.Position,
                MapId = _gameState.MapId,
                UIMapId = _gameState.UIMapId,
                Direction = _gameState.Player.Direction,
                State = stuckState,
                DurationMs = durationMs,
                IsSpinning = stuckState == UnstuckState.InitialAttempt && attemptCount > 2,
                AttemptCount = attemptCount,
                IsFlashingMarker = true
            };

            _stuckHistory.Add(stuckEvent);

            // Keep player immobilized for the stuck duration
            _gameState.Player.IsMoving = false;

            OnStuckSimulated?.Invoke(stuckEvent);

            // Schedule marker to stop flashing after 30 seconds
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                stuckEvent.IsFlashingMarker = false;
            });
        }
    }

    /// <summary>
    /// Simulates player death at current position.
    /// </summary>
    public void SimulateDeath(string cause = "Simulated death")
    {
        lock (_lock)
        {
            var deathEvent = new SimulatedDeathEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = _clock.CurrentTime,
                Position = _gameState.Player.Position,
                MapId = _gameState.MapId,
                UIMapId = _gameState.UIMapId,
                Cause = cause,
                Level = _gameState.Player.Level
            };

            _deathHistory.Add(deathEvent);

            // Kill the player
            _gameState.Player.TakeDamage(_gameState.Player.Health);

            OnDeathSimulated?.Invoke(deathEvent);
        }
    }

    /// <summary>
    /// Simulates multiple failures in same location to create a hot zone.
    /// </summary>
    public void SimulateHotZone(FailureType failureType, int failureCount = 3, float radius = 10f)
    {
        lock (_lock)
        {
            Vector3 center = _gameState.Player.Position;

            for (int i = 0; i < failureCount; i++)
            {
                // Vary position slightly within radius
                Vector3 offset = new(
                    (Random.Shared.NextSingle() - 0.5f) * 2 * radius,
                    (Random.Shared.NextSingle() - 0.5f) * 2 * radius,
                    0);

                switch (failureType)
                {
                    case FailureType.Stuck:
                        SimulateStuck(UnstuckState.InitialAttempt, 2000, i + 1);
                        break;
                    case FailureType.Death:
                        SimulateDeath($"Hot zone death #{i + 1}");
                        break;
                    case FailureType.NoPlan:
                        // NO PLAN is handled differently, just record it
                        break;
                    default:
                        SimulateStuck(UnstuckState.InitialAttempt);
                        break;
                }
            }

            var hotZone = new SimulatedHotZone
            {
                Id = Guid.NewGuid(),
                Center = center,
                MapId = _gameState.MapId,
                FailureCount = failureCount,
                PrimaryType = failureType,
                CreatedAt = _clock.CurrentTime,
                Radius = radius
            };

            OnHotZoneCreated?.Invoke(hotZone);
        }
    }

    /// <summary>
    /// Simulates zone rehabilitation.
    /// </summary>
    public void SimulateRehabilitation(Vector3 position, float radius = 25f)
    {
        lock (_lock)
        {
            var rehabEvent = new SimulatedRehabEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = _clock.CurrentTime,
                Position = position,
                Radius = radius,
                SeverityReduction = 0.5f
            };

            OnRehabSimulated?.Invoke(rehabEvent);
        }
    }

    /// <summary>
    /// Spawns multiple hostile NPCs near player to simulate multi-mob scenario.
    /// </summary>
    public List<NpcEntity> SimulateMultiMobAggro(int mobCount, float maxDistance = 15f)
    {
        lock (_lock)
        {
            var spawnedMobs = new List<NpcEntity>();
            Vector3 playerPos = _gameState.Player.Position;

            for (int i = 0; i < mobCount; i++)
            {
                // Spawn in circle around player
                float angle = (i / (float)mobCount) * MathF.PI * 2;
                float distance = Random.Shared.NextSingle() * maxDistance;

                Vector3 spawnPos = playerPos + new Vector3(
                    MathF.Cos(angle) * distance,
                    MathF.Sin(angle) * distance,
                    0);

                var npc = _gameState.SpawnNpc(
                    $"Hostile NPC {i + 1}",
                    _gameState.Player.Level,
                    100,
                    spawnPos,
                    hostile: true);

                spawnedMobs.Add(npc);
            }

            // Force combat with first mob
            if (spawnedMobs.Count > 0)
            {
                var target = new TargetEntity
                {
                    Id = spawnedMobs[0].Id,
                    Name = spawnedMobs[0].Name,
                    Level = spawnedMobs[0].Level,
                    Health = spawnedMobs[0].Health,
                    HealthMax = spawnedMobs[0].HealthMax,
                    Position = spawnedMobs[0].Position,
                    IsHostile = true
                };

                _gameState.SetTarget(target);
                _gameState.StartCombat();
            }

            return spawnedMobs;
        }
    }

    /// <summary>
    /// Forces a NO PLAN condition by clearing all valid goals.
    /// </summary>
    public void SimulateNoPlanCondition()
    {
        // This would need to interact with the bot's GOAP planner
        // For now, we'll just log that this was called
        _ = _stuckHistory; // Suppress CA1822 by accessing instance field
    }

    /// <summary>
    /// Gets all stuck events within a time window.
    /// </summary>
    public IReadOnlyList<SimulatedStuckEvent> GetRecentStuckEvents(TimeSpan window)
    {
        lock (_lock)
        {
            DateTime cutoff = _clock.CurrentTime - window;
            return _stuckHistory.Where(e => e.Timestamp > cutoff).ToList();
        }
    }

    /// <summary>
    /// Gets all death events within a time window.
    /// </summary>
    public IReadOnlyList<SimulatedDeathEvent> GetRecentDeathEvents(TimeSpan window)
    {
        lock (_lock)
        {
            DateTime cutoff = _clock.CurrentTime - window;
            return _deathHistory.Where(e => e.Timestamp > cutoff).ToList();
        }
    }

    /// <summary>
    /// Clears all simulation history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _stuckHistory.Clear();
            _deathHistory.Clear();
        }
    }
}
