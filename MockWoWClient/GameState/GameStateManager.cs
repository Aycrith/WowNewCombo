using System.Numerics;

namespace MockWoWClient.GameState;

/// <summary>
/// Represents the complete state of the simulated World of Warcraft client.
/// This is the single source of truth for all game data.
/// </summary>
public sealed class GameStateManager
{
    private readonly object _lock = new();
    private readonly SimulationClock _clock;

    // Core entities
    public PlayerEntity Player { get; }
    public TargetEntity? CurrentTarget { get; private set; }
    public List<NpcEntity> Npcs { get; } = [];
    public List<CorpseEntity> Corpses { get; } = [];

    // Zone/Map state
    public int MapId { get; set; } = 0;
    public int UIMapId { get; set; } = 0;

    // Combat state
    public bool InCombat => Player.InCombat;
    public DateTime CombatStartTime { get; private set; }
    public List<CombatEvent> CombatLog { get; } = [];

    // Time
    public DateTime CurrentTime => _clock.CurrentTime;
    public TimeSpan ElapsedTime => _clock.ElapsedTime;
    public float TimeScale => _clock.TimeScale;

    // Events
    public event Action<PlayerEntity>? OnPlayerStateChanged;
    public event Action<TargetEntity?>? OnTargetChanged;
    public event Action<NpcEntity>? OnNpcSpawned;
    public event Action<NpcEntity>? OnNpcDied;
    public event Action<CombatEvent>? OnCombatEvent;

    public GameStateManager(SimulationClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Player = new PlayerEntity();
    }

    /// <summary>
    /// Updates the game state for the current frame.
    /// Called every simulation tick.
    /// </summary>
    public void Update(TimeSpan deltaTime)
    {
        lock (_lock)
        {
            // Update all entities
            Player.Update(deltaTime);
            CurrentTarget?.Update(deltaTime);

            foreach (var npc in Npcs)
            {
                npc.Update(deltaTime);
            }

            // Check for dead NPCs
            foreach (var npc in Npcs.Where(n => n.IsDead && !n.HasBeenLooted).ToList())
            {
                if (!Corpses.Any(c => c.NpcId == npc.Id))
                {
                    var corpse = new CorpseEntity(npc);
                    Corpses.Add(corpse);
                    OnNpcDied?.Invoke(npc);
                }
            }

            // Update corpses
            foreach (var corpse in Corpses.ToList())
            {
                corpse.Update(deltaTime);
                if (corpse.HasExpired)
                {
                    Corpses.Remove(corpse);
                }
            }
        }
    }

    /// <summary>
    /// Sets the current target.
    /// </summary>
    public void SetTarget(TargetEntity? target)
    {
        lock (_lock)
        {
            CurrentTarget = target;
            Player.HasTarget = target != null;
            OnTargetChanged?.Invoke(target);
        }
    }

    /// <summary>
    /// Clears the current target.
    /// </summary>
    public void ClearTarget()
    {
        SetTarget(null);
    }

    /// <summary>
    /// Spawns an NPC at the specified location.
    /// </summary>
    public NpcEntity SpawnNpc(string name, int level, int health, Vector3 position, bool hostile = true)
    {
        lock (_lock)
        {
            var npc = new NpcEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                Level = level,
                Health = health,
                HealthMax = health,
                Position = position,
                IsHostile = hostile
            };

            Npcs.Add(npc);
            OnNpcSpawned?.Invoke(npc);
            return npc;
        }
    }

    /// <summary>
    /// Removes an NPC from the world.
    /// </summary>
    public void RemoveNpc(NpcEntity npc)
    {
        lock (_lock)
        {
            Npcs.Remove(npc);
        }
    }

    /// <summary>
    /// Starts combat with the current target.
    /// </summary>
    public void StartCombat()
    {
        lock (_lock)
        {
            if (CurrentTarget == null || CurrentTarget.IsDead)
                return;

            Player.InCombat = true;
            Player.State = EntityState.Attacking;
            Player.IsMounted = false; // Dismount when entering combat
            CombatStartTime = CurrentTime;

            var combatEvent = new CombatEvent
            {
                Timestamp = CurrentTime,
                Type = CombatEventType.CombatStart,
                Source = Player,
                Target = CurrentTarget
            };

            CombatLog.Add(combatEvent);
            OnCombatEvent?.Invoke(combatEvent);
        }
    }

    /// <summary>
    /// Ends combat.
    /// </summary>
    public void EndCombat()
    {
        lock (_lock)
        {
            Player.InCombat = false;

            var combatEvent = new CombatEvent
            {
                Timestamp = CurrentTime,
                Type = CombatEventType.CombatEnd,
                Source = Player
            };

            CombatLog.Add(combatEvent);
            OnCombatEvent?.Invoke(combatEvent);
        }
    }

    /// <summary>
    /// Records a combat action.
    /// </summary>
    public void RecordCombatAction(string actionName, int damage = 0, bool isCritical = false)
    {
        lock (_lock)
        {
            var combatEvent = new CombatEvent
            {
                Timestamp = CurrentTime,
                Type = CombatEventType.Action,
                ActionName = actionName,
                Source = Player,
                Target = CurrentTarget,
                Damage = damage,
                IsCritical = isCritical
            };

            CombatLog.Add(combatEvent);
            OnCombatEvent?.Invoke(combatEvent);
        }
    }

    /// <summary>
    /// Gets the nearest hostile NPC to the player.
    /// </summary>
    public NpcEntity? GetNearestHostileNpc(float maxDistance = 50f)
    {
        lock (_lock)
        {
            return Npcs
                .Where(n => n.IsHostile && !n.IsDead)
                .OrderBy(n => Vector3.Distance(Player.Position, n.Position))
                .FirstOrDefault(n => Vector3.Distance(Player.Position, n.Position) <= maxDistance);
        }
    }

    /// <summary>
    /// Loots a corpse.
    /// </summary>
    public bool LootCorpse(CorpseEntity corpse)
    {
        lock (_lock)
        {
            if (!Corpses.Contains(corpse) || corpse.HasBeenLooted)
                return false;

            corpse.HasBeenLooted = true;
            return true;
        }
    }

    /// <summary>
    /// Gets the nearest lootable corpse.
    /// </summary>
    public CorpseEntity? GetNearestLootableCorpse(float maxDistance = 10f)
    {
        lock (_lock)
        {
            return Corpses
                .Where(c => !c.HasBeenLooted)
                .OrderBy(c => Vector3.Distance(Player.Position, c.Position))
                .FirstOrDefault(c => Vector3.Distance(Player.Position, c.Position) <= maxDistance);
        }
    }

    /// <summary>
    /// Clears all game state (for test isolation).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Player.Reset();
            CurrentTarget = null;
            Npcs.Clear();
            Corpses.Clear();
            CombatLog.Clear();
        }
    }
}

/// <summary>
/// Entity state enumeration.
/// </summary>
public enum EntityState
{
    Idle,
    Moving,
    Casting,
    Attacking,
    Dead
}

/// <summary>
/// Combat event types.
/// </summary>
public enum CombatEventType
{
    CombatStart,
    CombatEnd,
    Action,
    Damage,
    Heal,
    Death
}

/// <summary>
/// Represents a combat event.
/// </summary>
public class CombatEvent
{
    public DateTime Timestamp { get; set; }
    public CombatEventType Type { get; set; }
    public string? ActionName { get; set; }
    public Entity Source { get; set; } = null!;
    public Entity? Target { get; set; }
    public int Damage { get; set; }
    public bool IsCritical { get; set; }
}
