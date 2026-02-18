using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Core.CombatRotation;

/// <summary>
/// Service for classifying abilities by name to determine their tactical role.
/// Called once at profile load time to cache AbilityType in KeyAction,
/// avoiding string allocations during the hot path scoring.
/// </summary>
public static class AbilityClassifier
{
    // Primary lookup table: exact name matches with O(1) performance
    // Using FrozenDictionary for zero-allocation lookups after initialization
    private static readonly FrozenDictionary<string, AbilityType> abilityMap;

    static AbilityClassifier()
    {
        var map = new Dictionary<string, AbilityType>(StringComparer.OrdinalIgnoreCase)
        {
            // === Healer Abilities ===
            // Direct heals
            ["Flash Heal"] = AbilityType.DirectHeal,
            ["Greater Heal"] = AbilityType.DirectHeal,
            ["Heal"] = AbilityType.DirectHeal,
            ["Healing Touch"] = AbilityType.DirectHeal,
            ["Healing Wave"] = AbilityType.DirectHeal,
            ["Lesser Healing Wave"] = AbilityType.DirectHeal,
            ["Holy Light"] = AbilityType.DirectHeal,
            ["Flash of Light"] = AbilityType.DirectHeal,
            ["Nourish"] = AbilityType.DirectHeal,

            // HoTs
            ["Renew"] = AbilityType.HoT,
            ["Rejuvenation"] = AbilityType.HoT,
            ["Regrowth"] = AbilityType.HoT,
            ["Lifebloom"] = AbilityType.HoT,
            ["Wild Growth"] = AbilityType.HoT,
            ["Riptide"] = AbilityType.HoT,

            // AoE heals
            ["Chain Heal"] = AbilityType.AoEHeal,
            ["Prayer of Healing"] = AbilityType.AoEHeal,
            ["Healing Stream Totem"] = AbilityType.AoEHeal,
            ["Tranquility"] = AbilityType.AoEHeal,
            ["Divine Hymn"] = AbilityType.AoEHeal,
            ["Hymn of Hope"] = AbilityType.AoEHeal,

            // Damage prevention
            ["Power Word: Shield"] = AbilityType.DamagePrevention,
            ["Sacred Shield"] = AbilityType.DamagePrevention,
            ["Ice Barrier"] = AbilityType.DamagePrevention,
            ["Sacrifice"] = AbilityType.DamagePrevention,

            // Mana regeneration
            ["Life Tap"] = AbilityType.ManaRegeneration,
            ["Innervate"] = AbilityType.ManaRegeneration,
            ["Mana Tide Totem"] = AbilityType.ManaRegeneration,
            ["Evocation"] = AbilityType.ManaRegeneration,
            ["Replenishment"] = AbilityType.ManaRegeneration,

            // Healing buffs
            ["Tree of Life"] = AbilityType.HealingBuff,
            ["Inner Focus"] = AbilityType.HealingBuff,
            ["Divine Favor"] = AbilityType.HealingBuff,
            ["Serendipity"] = AbilityType.HealingBuff,

            // Emergency heals
            ["Swiftmend"] = AbilityType.EmergencyHeal,
            ["Nature's Swiftness"] = AbilityType.EmergencyHeal,
            ["Lay on Hands"] = AbilityType.EmergencyHeal,

            // Combat resurrection
            ["Resurrection"] = AbilityType.CombatResurrection,
            ["Rebirth"] = AbilityType.CombatResurrection,
            ["Soulstone Resurrection"] = AbilityType.CombatResurrection,

            // Dispels
            ["Dispel Magic"] = AbilityType.Dispel,
            ["Cleanse"] = AbilityType.Dispel,
            ["Abolish Disease"] = AbilityType.Dispel,
            ["Remove Curse"] = AbilityType.Dispel,
            ["Cure Disease"] = AbilityType.Dispel,
            ["Purify"] = AbilityType.Dispel,
            ["Purify Spirit"] = AbilityType.Dispel,

            // === Tank Abilities ===
            // Survival cooldowns
            ["Shield Wall"] = AbilityType.SurvivalCooldown,
            ["Last Stand"] = AbilityType.SurvivalCooldown,
            ["Survival Instincts"] = AbilityType.SurvivalCooldown,
            ["Guardian Spirit"] = AbilityType.SurvivalCooldown,
            ["Pain Suppression"] = AbilityType.SurvivalCooldown,
            ["Icebound Fortitude"] = AbilityType.SurvivalCooldown,
            ["Anti-Magic Shell"] = AbilityType.SurvivalCooldown,
            ["Bone Shield"] = AbilityType.SurvivalCooldown,
            ["Vampiric Blood"] = AbilityType.SurvivalCooldown,
            ["Unbreakable Armor"] = AbilityType.SurvivalCooldown,
            ["Shield Block"] = AbilityType.SurvivalCooldown,
            ["Shield Barrier"] = AbilityType.SurvivalCooldown,
            ["Enraged Regeneration"] = AbilityType.SurvivalCooldown,
            ["Barkskin"] = AbilityType.SurvivalCooldown,
            ["Ironbark"] = AbilityType.SurvivalCooldown,
            ["Divine Protection"] = AbilityType.SurvivalCooldown,
            ["Ardent Defender"] = AbilityType.SurvivalCooldown,
            ["Guardian of Ancient Kings"] = AbilityType.SurvivalCooldown,

            // Taunts
            ["Taunt"] = AbilityType.Taunt,
            ["Mocking Blow"] = AbilityType.Taunt,
            ["Challenging Shout"] = AbilityType.Taunt,
            ["Challenging Roar"] = AbilityType.Taunt,
            ["Righteous Defense"] = AbilityType.Taunt,
            ["Hand of Reckoning"] = AbilityType.Taunt,
            ["Dark Command"] = AbilityType.Taunt,
            ["Death Grip"] = AbilityType.Taunt,
            ["Growl"] = AbilityType.Taunt,

            // Single-target threat
            ["Shield Slam"] = AbilityType.SingleTargetThreat,
            ["Devastate"] = AbilityType.SingleTargetThreat,
            ["Revenge"] = AbilityType.SingleTargetThreat,
            ["Sunder Armor"] = AbilityType.SingleTargetThreat,
            ["Heroic Strike"] = AbilityType.SingleTargetThreat,

            // AoE threat
            ["Thunder Clap"] = AbilityType.AoEThreat,
            ["Demoralizing Shout"] = AbilityType.AoEThreat,
            ["Consecration"] = AbilityType.AoEThreat,
            ["Hammer of the Righteous"] = AbilityType.AoEThreat,
            ["Avenger's Shield"] = AbilityType.AoEThreat,
            ["Swipe"] = AbilityType.AoEThreat,
            ["Maul"] = AbilityType.AoEThreat,
            ["Blood Boil"] = AbilityType.AoEThreat,
            ["Heart Strike"] = AbilityType.AoEThreat,
            ["Death and Decay"] = AbilityType.AoEThreat,
            ["Unholy Blight"] = AbilityType.AoEThreat,
            ["Howling Blast"] = AbilityType.AoEThreat,

            // Defensive stances
            ["Defensive Stance"] = AbilityType.DefensiveStance,
            ["Bear Form"] = AbilityType.DefensiveStance,
            ["Dire Bear Form"] = AbilityType.DefensiveStance,
            ["Tank Form"] = AbilityType.DefensiveStance,

            // === DPS Abilities ===
            // Execute phase
            ["Execute"] = AbilityType.Execute,
            ["Drain Soul"] = AbilityType.Execute,
            ["Eviscerate"] = AbilityType.Execute,

            // Finishers
            ["Kidney Shot"] = AbilityType.Finisher,
            ["Envenom"] = AbilityType.Finisher,
            ["Rupture"] = AbilityType.Finisher,
            ["Expose Armor"] = AbilityType.Finisher,
            ["Slice and Dice"] = AbilityType.Finisher,
            ["Recuperate"] = AbilityType.Finisher,
            ["Savage Roar"] = AbilityType.Finisher,
            ["Rip"] = AbilityType.Finisher,
            ["Ferocious Bite"] = AbilityType.Finisher,
            ["Maim"] = AbilityType.Finisher,
            ["Primal Wrath"] = AbilityType.Finisher,
            ["Feral Frenzy"] = AbilityType.Finisher,
            ["Mutilate"] = AbilityType.Finisher,
            ["Dispatch"] = AbilityType.Finisher,
            ["Between the Eyes"] = AbilityType.Finisher,

            // Builders
            ["Sinister Strike"] = AbilityType.Builder,
            ["Hemorrhage"] = AbilityType.Builder,
            ["Backstab"] = AbilityType.Builder,
            ["Ambush"] = AbilityType.Builder,
            ["Garrote"] = AbilityType.Builder,
            ["Cheap Shot"] = AbilityType.Builder,
            ["Gouge"] = AbilityType.Builder,
            ["Shiv"] = AbilityType.Builder,
            ["Fan of Knives"] = AbilityType.Builder,
            ["Mangle"] = AbilityType.Builder,
            ["Shred"] = AbilityType.Builder,
            ["Rake"] = AbilityType.Builder,
            ["Lacerate"] = AbilityType.Builder,
            ["Claw"] = AbilityType.Builder,
            ["Ravage"] = AbilityType.Builder,
            ["Pounce"] = AbilityType.Builder,
            ["Mutilate"] = AbilityType.Builder,

            // DoTs
            ["Corruption"] = AbilityType.DoT,
            ["Curse of Agony"] = AbilityType.DoT,
            ["Curse of Doom"] = AbilityType.DoT,
            ["Unstable Affliction"] = AbilityType.DoT,
            ["Siphon Life"] = AbilityType.DoT,
            ["Immolate"] = AbilityType.DoT,
            ["Living Bomb"] = AbilityType.DoT,
            ["Ignite"] = AbilityType.DoT,
            ["Rupture"] = AbilityType.DoT,
            ["Deadly Poison"] = AbilityType.DoT,
            ["Wound Poison"] = AbilityType.DoT,
            ["Instant Poison"] = AbilityType.DoT,
            ["Crippling Poison"] = AbilityType.DoT,
            ["Mind-numbing Poison"] = AbilityType.DoT,
            ["Anesthetic Poison"] = AbilityType.DoT,
            ["Venomous Wound"] = AbilityType.DoT,
            ["Deep Wounds"] = AbilityType.DoT,
            ["Rend"] = AbilityType.DoT,
            ["Serpent Sting"] = AbilityType.DoT,
            ["Viper Sting"] = AbilityType.DoT,
            ["Wyvern Sting"] = AbilityType.DoT,
            ["Moonfire"] = AbilityType.DoT,
            ["Sunfire"] = AbilityType.DoT,
            ["Insect Swarm"] = AbilityType.DoT,
            ["Toxic Blade"] = AbilityType.DoT,
            ["Serrated Bone Spike"] = AbilityType.DoT,
            ["Crimson Tempest"] = AbilityType.DoT,

            // Damage buffs
            ["Battle Shout"] = AbilityType.DamageBuff,
            ["Commanding Shout"] = AbilityType.DamageBuff,
            ["Trueshot Aura"] = AbilityType.DamageBuff,
            ["Aspect of the Hawk"] = AbilityType.DamageBuff,
            ["Aspect of the Wild"] = AbilityType.DamageBuff,
            ["Arcane Power"] = AbilityType.DamageBuff,
            ["Icy Veins"] = AbilityType.DamageBuff,
            ["Combustion"] = AbilityType.DamageBuff,
            ["Metamorphosis"] = AbilityType.DamageBuff,
            ["Demon Soul"] = AbilityType.DamageBuff,
            ["Soulburn"] = AbilityType.DamageBuff,
            ["Shadowform"] = AbilityType.DamageBuff,
            ["Vampiric Embrace"] = AbilityType.DamageBuff,
            ["Inner Fire"] = AbilityType.DamageBuff,
            ["Power Word: Fortitude"] = AbilityType.DamageBuff,
            ["Divine Spirit"] = AbilityType.DamageBuff,
            ["Shadow Protection"] = AbilityType.DamageBuff,
            ["Shadowguard"] = AbilityType.DamageBuff,
            ["Lightning Shield"] = AbilityType.DamageBuff,
            ["Water Shield"] = AbilityType.DamageBuff,
            ["Earth Shield"] = AbilityType.DamageBuff,
            ["Rockbiter Weapon"] = AbilityType.DamageBuff,
            ["Flametongue Weapon"] = AbilityType.DamageBuff,
            ["Frostbrand Weapon"] = AbilityType.DamageBuff,
            ["Windfury Weapon"] = AbilityType.DamageBuff,
            ["Grace of Air"] = AbilityType.DamageBuff,
            ["Strength of Earth"] = AbilityType.DamageBuff,
            ["Mana Spring"] = AbilityType.DamageBuff,
            ["Seal of Command"] = AbilityType.DamageBuff,
            ["Seal of Righteousness"] = AbilityType.DamageBuff,
            ["Seal of Vengeance"] = AbilityType.DamageBuff,
            ["Seal of Corruption"] = AbilityType.DamageBuff,
            ["Seal of Wisdom"] = AbilityType.DamageBuff,
            ["Seal of Light"] = AbilityType.DamageBuff,
            ["Blessing of Might"] = AbilityType.DamageBuff,
            ["Blessing of Wisdom"] = AbilityType.DamageBuff,
            ["Blessing of Kings"] = AbilityType.DamageBuff,
            ["Blessing of Sanctuary"] = AbilityType.DamageBuff,
            ["Concentration Aura"] = AbilityType.DamageBuff,
            ["Devotion Aura"] = AbilityType.DamageBuff,
            ["Retribution Aura"] = AbilityType.DamageBuff,
            ["Crusader Aura"] = AbilityType.DamageBuff,
            ["Aspect of the Pack"] = AbilityType.DamageBuff,
            ["Aspect of the Cheetah"] = AbilityType.DamageBuff,

            // Damage cooldowns
            ["Bestial Wrath"] = AbilityType.DamageCooldown,
            ["Rapid Fire"] = AbilityType.DamageCooldown,
            ["Adrenaline Rush"] = AbilityType.DamageCooldown,
            ["Killing Spree"] = AbilityType.DamageCooldown,
            ["Shadow Blades"] = AbilityType.DamageCooldown,
            ["Vendetta"] = AbilityType.DamageCooldown,
            ["Cold Blood"] = AbilityType.DamageCooldown,
            ["Blade Flurry"] = AbilityType.DamageCooldown,
            ["Death Wish"] = AbilityType.DamageCooldown,
            ["Recklessness"] = AbilityType.DamageCooldown,
            ["Retaliation"] = AbilityType.DamageCooldown,
            ["Sweeping Strikes"] = AbilityType.DamageCooldown,
            ["Avatar"] = AbilityType.DamageCooldown,
            ["Bloodbath"] = AbilityType.DamageCooldown,
            ["Bladestorm"] = AbilityType.DamageCooldown,
            ["Rampage"] = AbilityType.DamageCooldown,
            ["Battle Cry"] = AbilityType.DamageCooldown,
            ["Heroic Leap"] = AbilityType.DamageCooldown,
            ["Storm Bolt"] = AbilityType.DamageCooldown,
            ["Shockwave"] = AbilityType.DamageCooldown,
            ["Dragon Roar"] = AbilityType.DamageCooldown,
            ["Rallying Cry"] = AbilityType.DamageCooldown,
            ["Cold Snap"] = AbilityType.DamageCooldown,
            ["Presence of Mind"] = AbilityType.DamageCooldown,
            ["Time Warp"] = AbilityType.DamageCooldown,
            ["Heroism"] = AbilityType.DamageCooldown,
            ["Bloodlust"] = AbilityType.DamageCooldown,
            ["Ancient Hysteria"] = AbilityType.DamageCooldown,
            ["Metamorphosis"] = AbilityType.DamageCooldown,
            ["Demon Soul"] = AbilityType.DamageCooldown,
            ["Summon Infernal"] = AbilityType.DamageCooldown,
            ["Summon Doomguard"] = AbilityType.DamageCooldown,
            ["Soulburn"] = AbilityType.DamageCooldown,
            ["Shadowburn"] = AbilityType.DamageCooldown,
            ["Chaos Bolt"] = AbilityType.DamageCooldown,
            ["Shadowfury"] = AbilityType.DamageCooldown,
            ["Demonic Empowerment"] = AbilityType.DamageCooldown,
            ["Power Infusion"] = AbilityType.DamageCooldown,
            ["Fear Ward"] = AbilityType.DamageCooldown,
            ["Shadowfiend"] = AbilityType.DamageCooldown,
            ["Symbol of Hope"] = AbilityType.DamageCooldown,
            ["Rapture"] = AbilityType.DamageCooldown,
            ["Archangel"] = AbilityType.DamageCooldown,
            ["Apotheosis"] = AbilityType.DamageCooldown,
            ["Incarnation"] = AbilityType.DamageCooldown,
            ["Celestial Alignment"] = AbilityType.DamageCooldown,
            ["Warrior of Elune"] = AbilityType.DamageCooldown,
            ["Force of Nature"] = AbilityType.DamageCooldown,
            ["Avenging Wrath"] = AbilityType.DamageCooldown,
            ["Divine Storm"] = AbilityType.DamageCooldown,
            ["Templar's Verdict"] = AbilityType.DamageCooldown,
            ["Exorcism"] = AbilityType.DamageCooldown,
            ["Ascendance"] = AbilityType.DamageCooldown,
            ["Spiritwalker's Grace"] = AbilityType.DamageCooldown,
            ["Fire Elemental"] = AbilityType.DamageCooldown,
            ["Earth Elemental"] = AbilityType.DamageCooldown,
            ["Storm Elemental"] = AbilityType.DamageCooldown,
            ["Feral Spirit"] = AbilityType.DamageCooldown,

            // Direct damage (single-target nukes)
            ["Shadow Bolt"] = AbilityType.Damage,
            ["Fireball"] = AbilityType.Damage,
            ["Frostbolt"] = AbilityType.Damage,
            ["Arcane Missiles"] = AbilityType.Damage,
            ["Frostfire Bolt"] = AbilityType.Damage,
            ["Wrath"] = AbilityType.Damage,
            ["Starfire"] = AbilityType.Damage,
            ["Starsurge"] = AbilityType.Damage,
            ["Pyroblast"] = AbilityType.Damage,
            ["Mind Blast"] = AbilityType.Damage,
            ["Mind Flay"] = AbilityType.Damage,
            ["Smite"] = AbilityType.Damage,
            ["Holy Fire"] = AbilityType.Damage,
            ["Slam"] = AbilityType.Damage,
            ["Mortal Strike"] = AbilityType.Damage,
            ["Bloodthirst"] = AbilityType.Damage,
            ["Whirlwind"] = AbilityType.Damage,
            ["Overpower"] = AbilityType.Damage,
            ["Victory Rush"] = AbilityType.Damage,
            ["Impending Victory"] = AbilityType.Damage,
            ["Colossus Smash"] = AbilityType.Damage,
            ["Siegebreaker"] = AbilityType.Damage,
            ["Fire Blast"] = AbilityType.Damage,
            ["Scorch"] = AbilityType.Damage,
            ["Ice Lance"] = AbilityType.Damage,

            // AoE damage
            ["Multishot"] = AbilityType.AoE,
            ["Volley"] = AbilityType.AoE,
            ["Barrage"] = AbilityType.AoE,
            ["Explosive Shot"] = AbilityType.AoE,
            ["Arcane Explosion"] = AbilityType.AoE,
            ["Flamestrike"] = AbilityType.AoE,
            ["Blizzard"] = AbilityType.AoE,
            ["Cone of Cold"] = AbilityType.AoE,
            ["Frost Nova"] = AbilityType.AoE,
            ["Fire Nova"] = AbilityType.AoE,
            ["Chain Lightning"] = AbilityType.AoE,
            ["Magma Totem"] = AbilityType.AoE,
            ["Searing Totem"] = AbilityType.AoE,
            ["Flametongue Totem"] = AbilityType.AoE,
            ["Thunderstorm"] = AbilityType.AoE,
            ["Earthquake"] = AbilityType.AoE,
            ["Hellfire"] = AbilityType.AoE,
            ["Rain of Fire"] = AbilityType.AoE,
            ["Seed of Corruption"] = AbilityType.AoE,
            ["Shadowfury"] = AbilityType.AoE,
            ["Whirlwind"] = AbilityType.AoE,
            ["Cleave"] = AbilityType.AoE,
            ["Bladestorm"] = AbilityType.AoE,
            ["Ravager"] = AbilityType.AoE,
            ["Divine Storm"] = AbilityType.AoE,
            ["Consecration"] = AbilityType.AoE,
            ["Holy Wrath"] = AbilityType.AoE,
            ["Hurricane"] = AbilityType.AoE,
            ["Starfall"] = AbilityType.AoE,
            ["Lunar Strike"] = AbilityType.AoE,
            ["Solar Wrath"] = AbilityType.AoE,
            ["Stellar Flare"] = AbilityType.AoE,
            ["Shooting Stars"] = AbilityType.AoE,
            ["Fury of Elune"] = AbilityType.AoE,
            ["New Moon"] = AbilityType.AoE,
            ["Half Moon"] = AbilityType.AoE,
            ["Full Moon"] = AbilityType.AoE,

            // On-next-swing abilities
            ["Maul"] = AbilityType.OnNextSwing,
            ["Runic Strike"] = AbilityType.OnNextSwing,

            // Interrupts
            ["Kick"] = AbilityType.Interrupt,
            ["Shield Bash"] = AbilityType.Interrupt,
            ["Pummel"] = AbilityType.Interrupt,
            ["Counterspell"] = AbilityType.Interrupt,
            ["Mind Freeze"] = AbilityType.Interrupt,
            ["Skull Bash"] = AbilityType.Interrupt,
            ["Solar Beam"] = AbilityType.Interrupt,
            ["Silence"] = AbilityType.Interrupt,
            ["Wind Shear"] = AbilityType.Interrupt,
            ["Rebuke"] = AbilityType.Interrupt,
            ["Fist of Justice"] = AbilityType.Interrupt,
            ["Hammer of Justice"] = AbilityType.Interrupt,
            ["Interrupt"] = AbilityType.Interrupt,

            // Crowd control
            ["Polymorph"] = AbilityType.CrowdControl,
            ["Sap"] = AbilityType.CrowdControl,
            ["Blind"] = AbilityType.CrowdControl,
            ["Fear"] = AbilityType.CrowdControl,
            ["Howl of Terror"] = AbilityType.CrowdControl,
            ["Death Coil"] = AbilityType.CrowdControl,
            ["Seduction"] = AbilityType.CrowdControl,
            ["Banish"] = AbilityType.CrowdControl,
            ["Enslave Demon"] = AbilityType.CrowdControl,
            ["Freezing Trap"] = AbilityType.CrowdControl,
            ["Scatter Shot"] = AbilityType.CrowdControl,
            ["Entangling Roots"] = AbilityType.CrowdControl,
            ["Cyclone"] = AbilityType.CrowdControl,
            ["Hibernate"] = AbilityType.CrowdControl,
            ["Turn Evil"] = AbilityType.CrowdControl,
            ["Repentance"] = AbilityType.CrowdControl,
            ["Stun"] = AbilityType.CrowdControl,
            ["Freeze"] = AbilityType.CrowdControl,
            ["Ring of Frost"] = AbilityType.CrowdControl,
            ["Deep Freeze"] = AbilityType.CrowdControl,

            // Rage generation
            ["Bloodrage"] = AbilityType.RageGeneration,
            ["Berserker Rage"] = AbilityType.RageGeneration,

            // Movement
            ["Charge"] = AbilityType.Movement,
            ["Intercept"] = AbilityType.Movement,
            ["Blink"] = AbilityType.Movement,
            ["Sprint"] = AbilityType.Movement,
            ["Dash"] = AbilityType.Movement,
            ["Intervene"] = AbilityType.Movement,
            ["Disengage"] = AbilityType.Movement,

            // Stealth
            ["Stealth"] = AbilityType.Stealth,
            ["Vanish"] = AbilityType.Stealth,
            ["Prowl"] = AbilityType.Stealth,
            ["Shadowmeld"] = AbilityType.Stealth,

            // Special actions
            ["Auto Attack"] = AbilityType.AutoAttack,
            ["Approach"] = AbilityType.Approach,

            // Druid forms
            ["Cat Form"] = AbilityType.DamageBuff,
            ["Travel Form"] = AbilityType.Movement,
            ["Flight Form"] = AbilityType.Movement,
            ["Swift Flight Form"] = AbilityType.Movement,
            ["Aquatic Form"] = AbilityType.Movement,

            // Warlock utility
            ["Hunter's Mark"] = AbilityType.DamageBuff,
            ["Create Soulstone"] = AbilityType.Other,
            ["Ritual of Summoning"] = AbilityType.Other,
            ["Soulstone"] = AbilityType.Other,
        };

        abilityMap = map.ToFrozenDictionary();
    }

    /// <summary>
    /// Classifies an ability by its name and returns the appropriate AbilityType.
    /// This method is called during profile loading, not during combat ticks.
    /// Uses O(1) dictionary lookup for exact matches, then falls back to
    /// pattern matching for generic categories (potions, bandages, etc.).
    /// </summary>
    public static AbilityType Classify(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName))
            return AbilityType.Other;

        // Primary: O(1) exact match lookup using FrozenDictionary
        // OrdinalIgnoreCase comparison avoids allocation (no ToLowerInvariant needed)
        if (abilityMap.TryGetValue(abilityName, out AbilityType type))
            return type;

        // Fallback: Pattern matching for generic consumables and utilities
        // These use substring matching because they match broad categories
        ReadOnlySpan<char> nameSpan = abilityName.AsSpan();

        // Consumables - anything containing these keywords
        if (ContainsOrdinalIgnoreCase(nameSpan, "Potion"))
            return AbilityType.Consumable;

        if (ContainsOrdinalIgnoreCase(nameSpan, "Healthstone"))
            return AbilityType.Consumable;

        if (ContainsOrdinalIgnoreCase(nameSpan, "Bandage"))
            return AbilityType.Consumable;

        // Food/Drink buffs
        if (ContainsOrdinalIgnoreCase(nameSpan, "Food") ||
            ContainsOrdinalIgnoreCase(nameSpan, "Drink"))
            return AbilityType.Consumable;

        // Default: Unknown/unclassified ability
        return AbilityType.Other;
    }

    /// <summary>
    /// Case-insensitive substring search using ReadOnlySpan<char>.
    /// Zero-allocation alternative to string.Contains with StringComparison.
    /// </summary>
    private static bool ContainsOrdinalIgnoreCase(ReadOnlySpan<char> haystack, string needle)
    {
        return haystack.IndexOf(needle.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
