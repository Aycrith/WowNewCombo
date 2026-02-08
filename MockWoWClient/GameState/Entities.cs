using System.Numerics;

namespace MockWoWClient.GameState;

/// <summary>
/// Base entity class for all game entities.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public Vector3 Position { get; set; }
    public float Direction { get; set; } // 0-360 degrees
    public int Health { get; set; }
    public int HealthMax { get; set; }
    public bool IsDead => Health <= 0;
    public EntityState State { get; set; } = EntityState.Idle;

    /// <summary>
    /// Health percentage (0-100).
    /// </summary>
    public int HealthPercent => HealthMax > 0 ? (Health * 100) / HealthMax : 0;

    /// <summary>
    /// Updates the entity state.
    /// </summary>
    public abstract void Update(TimeSpan deltaTime);

    /// <summary>
    /// Deals damage to this entity.
    /// </summary>
    public void TakeDamage(int damage)
    {
        Health = Math.Max(0, Health - damage);
        if (Health <= 0)
        {
            State = EntityState.Dead;
        }
    }

    /// <summary>
    /// Heals this entity.
    /// </summary>
    public void Heal(int amount)
    {
        if (!IsDead)
        {
            Health = Math.Min(HealthMax, Health + amount);
        }
    }

    /// <summary>
    /// Resets the entity to initial state.
    /// </summary>
    public virtual void Reset()
    {
        Health = HealthMax;
        State = EntityState.Idle;
    }
}

/// <summary>
/// Represents the player character.
/// </summary>
public class PlayerEntity : Entity
{
    // Primary attributes
    public string ClassName { get; set; } = "Warrior";
    public string Race { get; set; } = "Human";
    public int Experience { get; set; }
    public int ExperienceMax { get; set; } = 400;

    // Power (Mana/Rage/Energy)
    public int Power { get; set; }
    public int PowerMax { get; set; } = 100;
    public PowerType PowerType { get; set; } = PowerType.Rage;

    // Combat state
    public bool InCombat { get; set; }
    public bool HasTarget { get; set; }
    public bool IsCasting { get; set; }
    public float CastProgress { get; set; }
    public int CastingSpellId { get; set; }
    public int RemainingCastTimeMs { get; set; }

    // Boolean flags
    public bool IsMounted { get; set; }
    public bool IsStealthed { get; set; }
    public bool IsSwimming { get; set; }
    public bool IsFlying { get; set; }
    public bool IsMoving { get; set; }
    public bool IsFalling { get; set; }
    public bool IsAutoAttacking { get; set; }

    // Combo points (for Rogues/Druids)
    public int ComboPoints { get; set; }

    // Swing timers (in milliseconds)
    public int MainHandSwingTime { get; set; }
    public int MainHandSwingProgress { get; set; }
    public int OffHandSwingTime { get; set; }
    public int OffHandSwingProgress { get; set; }

    // Position history for breadcrumb tracking
    public Queue<Vector3> PositionHistory { get; } = new();
    public const int MaxPositionHistory = 50;

    // Money (in copper)
    public int Copper { get; set; }
    public int Gold => Copper / 10000;
    public int Silver => (Copper % 10000) / 100;

    // Inventory
    public List<Item> Inventory { get; } = [];

    // Action bar
    public ActionBarState[] ActionBars { get; } = new ActionBarState[120];

    public PlayerEntity()
    {
        Level = 1;
        Health = 100;
        HealthMax = 100;
        Position = Vector3.Zero;
        Direction = 0;

        for (int i = 0; i < ActionBars.Length; i++)
        {
            ActionBars[i] = new ActionBarState();
        }
    }

    public override void Update(TimeSpan deltaTime)
    {
        // Update swing timers
        if (MainHandSwingTime > 0)
        {
            MainHandSwingProgress += (int)deltaTime.TotalMilliseconds;
            if (MainHandSwingProgress >= MainHandSwingTime)
            {
                MainHandSwingProgress = 0;
                // Auto-attack damage would be applied here
            }
        }

        // Update casting
        if (IsCasting && RemainingCastTimeMs > 0)
        {
            RemainingCastTimeMs -= (int)deltaTime.TotalMilliseconds;
            if (RemainingCastTimeMs <= 0)
            {
                CompleteCast();
            }
        }

        // Record position for breadcrumb tracking
        if (PositionHistory.Count == 0 || 
            Vector3.Distance(PositionHistory.Last(), Position) >= 5.0f)
        {
            PositionHistory.Enqueue(Position);
            if (PositionHistory.Count > MaxPositionHistory)
            {
                PositionHistory.Dequeue();
            }
        }

        // Clear moving flag if not updated externally
        IsMoving = false;
    }

    public void StartCast(int spellId, int castTimeMs)
    {
        IsCasting = true;
        CastingSpellId = spellId;
        RemainingCastTimeMs = castTimeMs;
        State = EntityState.Casting;
    }

    public void CompleteCast()
    {
        IsCasting = false;
        RemainingCastTimeMs = 0;
        State = InCombat ? EntityState.Attacking : EntityState.Idle;
    }

    public void InterruptCast()
    {
        IsCasting = false;
        RemainingCastTimeMs = 0;
        State = InCombat ? EntityState.Attacking : EntityState.Idle;
    }

    public override void Reset()
    {
        base.Reset();
        InCombat = false;
        HasTarget = false;
        IsCasting = false;
        ComboPoints = 0;
        PositionHistory.Clear();
        Experience = 0;
        Copper = 0;
        Inventory.Clear();
    }
}

/// <summary>
/// Represents the current target.
/// </summary>
public class TargetEntity : Entity
{
    public bool IsHostile { get; set; }
    public new bool IsDead => Health <= 0;
    public bool IsTagged { get; set; }
    public bool IsPlayerControlled { get; set; }
    public bool IsTrivial { get; set; }
    public bool IsInCombat { get; set; }

    // Target's target
    public Entity? TargetOfTarget { get; set; }

    // Classification
    public TargetClassification Classification { get; set; } = TargetClassification.Normal;

#pragma warning disable CA1822 // TargetEntity.Update is intentionally empty
    public override void Update(TimeSpan deltaTime)
    {
        // Target entities are mostly static or controlled by NPC logic
    }
#pragma warning restore CA1822
}

/// <summary>
/// Represents an NPC (Non-Player Character).
/// </summary>
public class NpcEntity : TargetEntity
{
    public NpcType NpcType { get; set; } = NpcType.Humanoid;
    public bool HasBeenLooted { get; set; }
    public DateTime DeathTime { get; set; }
    public TimeSpan CorpseDespawnTime { get; set; } = TimeSpan.FromMinutes(5);
    public bool HasExpired => IsDead && DateTime.UtcNow > DeathTime + CorpseDespawnTime;

    public List<Item> LootTable { get; set; } = [];

    public override void Update(TimeSpan deltaTime)
    {
        base.Update(deltaTime);

        if (IsDead && DeathTime == default)
        {
            DeathTime = DateTime.UtcNow;
        }
    }

    public List<Item> GenerateLoot()
    {
        return LootTable.ToList();
    }
}

/// <summary>
/// Represents a lootable corpse.
/// </summary>
public class CorpseEntity
{
    public Guid Id { get; }
    public Guid NpcId { get; }
    public string NpcName { get; }
    public Vector3 Position { get; }
    public TimeSpan ElapsedTime { get; private set; }
    public TimeSpan DespawnTime { get; }
    public bool HasBeenLooted { get; set; }
    public bool HasExpired => ElapsedTime >= DespawnTime;
    public List<Item> Loot { get; }

    public CorpseEntity(NpcEntity npc)
    {
        Id = Guid.NewGuid();
        NpcId = npc.Id;
        NpcName = npc.Name;
        Position = npc.Position;
        ElapsedTime = TimeSpan.Zero;
        DespawnTime = npc.CorpseDespawnTime;
        Loot = npc.GenerateLoot();
    }

    public void Update(TimeSpan deltaTime)
    {
        ElapsedTime += deltaTime;
    }
}

/// <summary>
/// Power types for different classes.
/// </summary>
public enum PowerType
{
    Mana,
    Rage,
    Energy,
    Focus,
    Runes,
    RunicPower
}

/// <summary>
/// NPC types.
/// </summary>
public enum NpcType
{
    Humanoid,
    Beast,
    Dragonkin,
    Demon,
    Elemental,
    Undead,
    Mechanical,
    Giant,
    Critter
}

/// <summary>
/// Target classification.
/// </summary>
public enum TargetClassification
{
    Normal = 0,
    Elite = 1,
    Rare = 2,
    RareElite = 3,
    Boss = 4
}

/// <summary>
/// Action bar state.
/// </summary>
public class ActionBarState
{
    public int SpellId { get; set; }
    public string? SpellName { get; set; }
    public bool IsUsable { get; set; } = true;
    public int Cost { get; set; }
    public int CooldownRemaining { get; set; }
    public bool HasRange { get; set; }
    public bool InRange { get; set; } = true;
}

/// <summary>
/// Item definition.
/// </summary>
public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ItemQuality Quality { get; set; } = ItemQuality.Common;
    public bool IsSoulbound { get; set; }
    public bool IsQuestItem { get; set; }
}

/// <summary>
/// Item quality levels.
/// </summary>
public enum ItemQuality
{
    Poor = 0,
    Common = 1,
    Uncommon = 2,
    Rare = 3,
    Epic = 4,
    Legendary = 5
}
