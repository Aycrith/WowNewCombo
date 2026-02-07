namespace Core;

/// <summary>
/// Tactical classification of abilities for rotation optimization.
/// Used by role strategies to apply role-specific scoring logic.
/// Cached in KeyAction at profile load time to avoid string allocations.
/// </summary>
public enum AbilityType
{
    /// <summary>
    /// Default classification - no special handling.
    /// </summary>
    Other,

    // === DPS Ability Types ===

    /// <summary>
    /// Primary damage abilities (Shadow Bolt, Sinister Strike, etc.)
    /// </summary>
    Damage,

    /// <summary>
    /// Execute phase abilities (Execute, Eviscerate at <20% HP)
    /// </summary>
    Execute,

    /// <summary>
    /// Damage over time effects (Corruption, Rupture, Serpent Sting)
    /// </summary>
    DoT,

    /// <summary>
    /// Direct damage finishers that consume combo points/resources
    /// </summary>
    Finisher,

    /// <summary>
    /// Combo point or resource builders
    /// </summary>
    Builder,

    /// <summary>
    /// Buff abilities that enhance damage (Slice and Dice, Battle Shout)
    /// </summary>
    DamageBuff,

    /// <summary>
    /// Cooldown abilities for burst damage (Bestial Wrath, Adrenaline Rush)
    /// </summary>
    DamageCooldown,

    /// <summary>
    /// Multi-target or area of effect damage
    /// </summary>
    AoE,

    /// <summary>
    /// Instant cast abilities that can be weaved between swings
    /// </summary>
    Instant,

    /// <summary>
    /// Abilities that queue on next melee swing (Heroic Strike, Cleave)
    /// </summary>
    OnNextSwing,

    // === Tank Ability Types ===

    /// <summary>
    /// Survival cooldowns (Shield Wall, Last Stand, Icebound Fortitude)
    /// </summary>
    SurvivalCooldown,

    /// <summary>
    /// Taunt abilities to redirect threat (Taunt, Mocking Blow)
    /// </summary>
    Taunt,

    /// <summary>
    /// Threat-generating abilities for AoE (Thunder Clap, Consecration)
    /// </summary>
    AoEThreat,

    /// <summary>
    /// Single-target threat abilities (Shield Slam, Heroic Strike when tanking)
    /// </summary>
    SingleTargetThreat,

    /// <summary>
    /// Defensive stance or form abilities
    /// </summary>
    DefensiveStance,

    /// <summary>
    /// Interrupt abilities (Shield Bash, Kick, Counterspell)
    /// </summary>
    Interrupt,

    /// <summary>
    /// Abilities that generate rage or other tank resources
    /// </summary>
    RageGeneration,

    // === Healer Ability Types ===

    /// <summary>
    /// Direct single-target heals (Flash Heal, Healing Wave)
    /// </summary>
    DirectHeal,

    /// <summary>
    /// Healing over time effects (Renew, Rejuvenation)
    /// </summary>
    HoT,

    /// <summary>
    /// Area of effect heals (Chain Heal, Prayer of Healing)
    /// </summary>
    AoEHeal,

    /// <summary>
    /// Emergency heals for critical health targets
    /// </summary>
    EmergencyHeal,

    /// <summary>
    /// Damage prevention (Power Word: Shield, Sacred Shield)
    /// </summary>
    DamagePrevention,

    /// <summary>
    /// Mana regeneration or efficiency abilities (Life Tap, Innervate)
    /// </summary>
    ManaRegeneration,

    /// <summary>
    /// Healing buffs that increase output (Tree of Life, Inner Focus)
    /// </summary>
    HealingBuff,

    /// <summary>
    /// Dispel or cleansing abilities
    /// </summary>
    Dispel,

    /// <summary>
    /// Combat resurrection abilities
    /// </summary>
    CombatResurrection,

    // === Utility Ability Types ===

    /// <summary>
    /// Crowd control (Polymorph, Fear, Sap)
    /// </summary>
    CrowdControl,

    /// <summary>
    /// Movement abilities (Blink, Sprint, Intervene)
    /// </summary>
    Movement,

    /// <summary>
    /// Stealth or invisibility
    /// </summary>
    Stealth,

    /// <summary>
    /// Consumables (potions, healthstones, food)
    /// </summary>
    Consumable,

    /// <summary>
    /// Auto-attack toggle
    /// </summary>
    AutoAttack,

    /// <summary>
    /// Movement toward target
    /// </summary>
    Approach
}
