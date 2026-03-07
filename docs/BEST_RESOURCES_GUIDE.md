# WowClassicGrindBot - Best Resources & Configuration Guide

This comprehensive guide provides the best resources, configurations, combat rotations, and routes for your WowClassicGrindBot installation.

---

## Table of Contents

1. [Class Profiles Overview](#class-profiles-overview)
2. [Best Combat Rotations by Class](#best-combat-rotations-by-class)
3. [TBC Leveling Routes (60-70)](#tbc-leveling-routes-60-70)
4. [Classic Leveling Routes (1-60)](#classic-leveling-routes-1-60)
5. [Farming Routes](#farming-routes)
6. [Class Configuration Examples](#class-configuration-examples)
7. [Requirements Reference](#requirements-reference)
8. [Best Practices](#best-practices)
9. [Advanced Configurations](#advanced-configurations)

---

## Class Profiles Overview

Your installation includes **84 class profiles** covering all classes and level ranges:

### Available Classes & Profiles

| Class | Profiles | Level Ranges |
|-------|----------|--------------|
| **Death Knight** | 3 | 70 (Unholy), 80 (Frost/Unholy) |
| **Druid** | 12 | 1-6, 10-32 (Bear/Cat), 60, 80 (Moonkin) |
| **Hunter** | 9 | 1-70 (BM focus, Pet Pull variants) |
| **Mage** | 10 | 1-60 (Arcane, Fire, Frost variants) |
| **Paladin** | 9 | 1-70 (Retribution) |
| **Priest** | 2 | 1, 10 |
| **Rogue** | 4 | 1-20 |
| **Shaman** | 8 | 1-60 (Elemental variants) |
| **Warlock** | 8 | 1-66 (Demo Pet Pull, Shard Farm) |
| **Warrior** | 11 | 1-63 (Arms, Stances) |

---

## Best Combat Rotations by Class

### Hunter (Beast Mastery) - Recommended for Leveling

The Hunter is the **best solo class** for grinding due to pet tanking. Here's the optimal rotation:

```json
{
  "ClassName": "Hunter",
  "Skin": true,
  "UseMount": true,
  "PathThereAndBack": true,
  "NPCMaxLevels_Above": 3,
  "PathFilename": "60_Hellfire Peninsula_Ravager.json",
  
  "Pull": {
    "Sequence": [
      {
        "Name": "Concussive Shot",
        "Key": "9",
        "BeforeCastStop": true,
        "Requirements": [
          "HasRangedWeapon",
          "!InMeleeRange",
          "HasAmmo"
        ]
      }
    ]
  },
  
  "Combat": {
    "Sequence": [
      {
        "Name": "Mend Pet",
        "Key": "N7",
        "Cooldown": 15000,
        "Requirements": ["Has Pet", "PetHealth% < 50"]
      },
      {
        "Name": "Intimidation",
        "Key": "N4",
        "WhenUsable": true,
        "Requirements": ["Has Pet", "TargetHealth% < 70 || TargetCastingSpell"]
      },
      {
        "Name": "Bestial Wrath",
        "Key": "N2",
        "WhenUsable": true,
        "Requirements": ["!InMeleeRange", "HasRangedWeapon", "HasAmmo", "Has Pet", "TargetHealth% > 85"]
      },
      {
        "Name": "Rapid Fire",
        "Key": "N3",
        "WhenUsable": true,
        "Requirements": ["!InMeleeRange", "HasRangedWeapon", "HasAmmo", "TargetHealth% > 75"]
      },
      {
        "Name": "Steady Shot",
        "Key": "6",
        "WhenUsable": true,
        "HasCastBar": true,
        "Charge": 2,
        "Cooldown": 5000,
        "Requirements": ["!InMeleeRange", "HasRangedWeapon", "HasAmmo", "LastAutoShotMs < 500"]
      },
      {
        "Name": "Auto Shot",
        "Key": "3",
        "Item": true,
        "Requirements": ["!InMeleeRange", "HasRangedWeapon", "!AutoShot", "HasAmmo"]
      },
      {
        "Name": "Raptor Strike",
        "Key": "4",
        "WhenUsable": true,
        "AfterCastWaitSwing": true,
        "AfterCastStepBack": 1000,
        "Requirements": ["MainHandSwing > -400", "InMeleeRange", "!AutoShot"]
      }
    ]
  },
  
  "Adhoc": {
    "Sequence": [
      {"Name": "Aspect of the Hawk", "Key": "5", "Requirement": "!Aspect of the Hawk"},
      {"Name": "feedpet", "Key": "N5", "Cooldown": 20000, "Requirements": ["Has Pet", "!Pet Happy"]},
      {"Name": "sumpet", "Key": "N6", "HasCastBar": true, "Cooldown": 4000, "Requirement": "!Has Pet"}
    ]
  },
  
  "Parallel": {
    "Sequence": [
      {"Name": "Food", "Key": "-", "Requirement": "Health% < 40"},
      {"Name": "Drink", "Key": "=", "Requirement": "Mana% < 40"}
    ]
  }
}
```

### Warlock (Demonology Pet Pull) - Excellent for TBC

The Warlock with Voidwalker/Felguard is extremely efficient:

```json
{
  "ClassName": "Warlock",
  "IntVariables": {
    "DOT_MIN_HEALTH%": 35,
    "Item_Soul_Shard": 6265,
    "TAP_MIN_MANA%": 30
  },
  
  "Combat": {
    "Sequence": [
      {
        "Name": "Curse of Agony",
        "Key": "3",
        "Requirements": ["TargetHealth% > DOT_MIN_HEALTH%", "!Curse of Agony"]
      },
      {
        "Name": "Corruption",
        "Key": "4",
        "Requirements": ["TargetHealth% > DOT_MIN_HEALTH%", "!Corruption"]
      },
      {
        "Name": "Immolate",
        "Key": "5",
        "HasCastBar": true,
        "Requirements": ["TargetHealth% > DOT_MIN_HEALTH%", "!Immolate"]
      },
      {
        "Name": "Shadow Bolt",
        "Key": "2",
        "HasCastBar": true,
        "Requirement": "TargetHealth% > 20"
      },
      {"Name": "AutoAttack", "Requirement": "!AutoAttacking"}
    ]
  },
  
  "Adhoc": {
    "Sequence": [
      {"Name": "Demon Armor", "Key": "6", "Requirement": "!Demon Armor"},
      {
        "Name": "Life Tap",
        "Key": "N9",
        "InCombat": "i dont care",
        "Charge": 2,
        "Requirements": ["!Casting", "Health% > TAP_MIN_MANA%", "Mana% < TAP_MIN_MANA%"]
      },
      {"Name": "Soul Shard", "Key": "9", "HasCastBar": true, "Requirements": ["TargetHealth% < 36", "!BagItem:6265:3"]}
    ]
  }
}
```

### Mage (Frost/Arcane) - AoE Grinding Potential

```json
{
  "ClassName": "Mage",
  
  "Combat": {
    "Sequence": [
      {
        "Name": "Ice Barrier",
        "Key": "F1",
        "WhenUsable": true,
        "Requirement": "!Ice Barrier"
      },
      {
        "Name": "Frost Nova",
        "Key": "6",
        "WhenUsable": true,
        "Requirements": ["InMeleeRange", "MobCount >= 1"]
      },
      {
        "Name": "Frostbolt",
        "Key": "2",
        "HasCastBar": true,
        "Requirement": "TargetHealth% > 20"
      },
      {
        "Name": "Fire Blast",
        "Key": "4",
        "WhenUsable": true,
        "Requirement": "TargetHealth% <= 20"
      },
      {"Name": "AutoAttack", "Requirement": "!AutoAttacking"}
    ]
  },
  
  "Adhoc": {
    "Sequence": [
      {"Name": "Frost Armor", "Key": "3", "Requirement": "!Frost Armor && !Ice Armor"},
      {"Name": "Arcane Intellect", "Key": "5", "Requirement": "!Arcane Intellect"},
      {"Name": "Conjure Water", "Key": "N1", "Requirement": "DrinkCount < 10"},
      {"Name": "Conjure Food", "Key": "N2", "Requirement": "FoodCount < 10"}
    ]
  },
  
  "Flee": {
    "Sequence": [
      {"Name": "Flee", "Requirement": "MobCount > 1 && Health% < 50"},
      {"Name": "Frost Nova", "Key": "6", "Requirement": "InMeleeRange"},
      {"Name": "Blink", "Key": "F3", "Requirement": "!InMeleeRange"}
    ]
  }
}
```

### Paladin (Retribution) - Self-Healing Tank

```json
{
  "ClassName": "Paladin",
  
  "Combat": {
    "Sequence": [
      {
        "Name": "Seal of Command",
        "Key": "1",
        "WhenUsable": true,
        "Requirement": "!Seal of Command && !Seal of Blood"
      },
      {
        "Name": "Judgement",
        "Key": "2",
        "WhenUsable": true,
        "Requirements": ["Seal of Command || Seal of Blood", "!Judgement of Any"]
      },
      {
        "Name": "Crusader Strike",
        "Key": "3",
        "WhenUsable": true,
        "Requirement": "InMeleeRange"
      },
      {
        "Name": "Flash of Light",
        "Key": "6",
        "HasCastBar": true,
        "WhenUsable": true,
        "Requirements": ["Health% < 50", "TargetHealth% > 20", "MobCount < 2"]
      },
      {"Name": "AutoAttack", "Requirement": "!AutoAttacking"}
    ]
  },
  
  "Adhoc": {
    "Sequence": [
      {"Name": "Blessing of Might", "Key": "4", "Requirement": "!Blessing of Might"},
      {"Name": "Righteous Fury", "Key": "7", "Requirement": "!Righteous Fury"}
    ]
  }
}
```

---

## TBC Leveling Routes (60-70)

Your installation includes **39 TBC paths** organized by zone:

### Hellfire Peninsula (58-63)
| File | Level Range | Description |
|------|-------------|-------------|
| `58-60 Felspark Ravine.json` | 58-60 | Imp demons, easy pulls |
| `59-61 the legion front.json` | 59-61 | Legion demons |
| `60-63 Some Ravine Thing Imp.json` | 60-63 | More imps |
| `60-64 birds.json` | 60-64 | Ravagers and birds |
| `60-64 Felspark Ravine.json` | 60-64 | Extended Felspark route |

### Zangarmarsh (60-65)
| File | Level Range | Description |
|------|-------------|-------------|
| `60-62 Bees.json` | 60-62 | Bees near Sporeggar |
| `60-64 Dead mire.json` | 60-64 | Dead Mire fungal giants |
| `60-65 Ragestone.json` | 60-65 | Ragestone elementals |
| `61-65 Ogre-Bees.json` | 61-65 | Mixed ogres and bees |
| `62-64 Sporeggar lake.json` | 62-64 | Near Sporeggar for rep |

### Nagrand (63-68) - **BEST TBC GRINDING ZONE**
| File | Level Range | Description |
|------|-------------|-------------|
| `63-65.json` | 63-65 | General Nagrand beasts |
| `64-67 Neutral Mobs.json` | 64-67 | Talbuks, clefthoofs |
| `64-67 Ogres.json` | 64-67 | Boulderfist ogres |
| `65-66 talbuks.json` | 65-66 | Talbuk farming |
| `65-70 Oshugun.json` | 65-70 | **RECOMMENDED** - Best XP/hour |

### Blade's Edge Mountains (65-68)
| File | Level Range | Description |
|------|-------------|-------------|
| `65-67.json` | 65-67 | General grinding |
| `67 Scalewing Serpent.json` | 67 | Serpent farming |
| `67-70 aldor rep.json` | 67-70 | Aldor reputation grinding |
| `67-70 Tunnel.json` | 67-70 | Tunnel beasts |

### Netherstorm (68-70)
| File | Level Range | Description |
|------|-------------|-------------|
| `68-69 Swiftwing Shredder.json` | 68-69 | Flying beasts |
| `68-70 Ruins of Farahlon.json` | 68-70 | Ruins farming |

### Terokkar Forest (62-66)
| File | Level Range | Description |
|------|-------------|-------------|
| `62-64.json` | 62-64 | Talonsworn forest |
| `64-66 Fire Rep Glide.json` | 64-66 | Fire elementals |
| `70-Terokkar Forest_Barrier_Hill.json` | 70 | Level 70 gold farming |

---

## Classic Leveling Routes (1-60)

### Alliance Routes by Race

#### Human (Elwynn Forest → Westfall → Duskwood)
```
Level 1-4:   01-04_Elwynn Forest_Northshire Valley.json
Level 5-9:   05-09_Elwynn Forest_Farms Tour.json
Level 10-14: 10-14_Westfall.json
Level 15-20: 15-20_Westfall_Longshore.json
Level 18-22: 18-21_Westfall.json → 19-22_Duskwood_Ryu's.json
Level 22-30: 23-27 Darkshire.json → 25-30 Vul Gol Ogre Mound.json
```

#### Dwarf/Gnome (Dun Morogh → Loch Modan → Wetlands)
```
Level 1-4:   1-4_Dun Morogh.json
Level 4-10:  4-6_Dun Morogh.json → 6-10_Dun Morogh.json
Level 10-16: 10-12_Loch Modan.json → 12-16_Loch Modan.json
Level 16-24: 14-18_Loch Modan_East.json → 20-24_Wetlands.json
Level 24-30: 24-30_Arathi Highlands.json
```

#### Night Elf (Teldrassil → Darkshore)
```
Level 1-6:   01-6_Teldrassil_Nightelf.json
Level 6-11:  06-09_Teldrassil_Lake Al'Ameth.json
Level 11-18: 11-14_Darkshore_Ameth'Aran Ruins.json → 15-18_Darkshore_Twilight Vale.json
```

### Horde Routes by Race

#### Orc/Troll (Durotar → The Barrens)
```
Level 1-5:   01-04_Durotar_Valley of Trials.json
Level 5-10:  05-08_Durotar_big.json
Level 10-16: 10-14_The Barrens.json
Level 16-23: 14-17_The Barrens_The Merchant Coast.json → 17-23_The Barrens_Agama'gor.json
```

#### Tauren (Mulgore → The Barrens)
```
Level 1-10:  01-10_Mulgore.json
Level 10-20: 12-14_The Barrens_Crossroads Circle.json → 17-20_The Barrens_South.json
```

#### Undead (Tirisfal Glades → Silverpine Forest)
```
Level 1-10:  01-04_Tirisfal Glades_Deathknell.json → 07-10_Tirisfal Glades.json
Level 10-19: 11-14_Tirisfal Glades_Balnir Farmstead.json → 14-19_Silverpine Forest_Fenris.json
```

### Shared Mid-Level Routes (30-60)

| Level Range | Zone | Best Route |
|-------------|------|------------|
| 30-35 | Thousand Needles | `30-35 Shimmering Flats.json` |
| 35-40 | Arathi Highlands | `35-37 Boulderfist Hall.json` |
| 38-43 | Badlands | `39-42_Badlands.json` |
| 42-48 | Tanaris | `42-45 Hyenas Basalisks Scorpids.json` |
| 47-52 | Felwood | `48-50 Deadwood Village.json` |
| 50-55 | Un'Goro Crater | `50-54 Lakkari Tar Pits.json` |
| 52-57 | Winterspring | `54-57 Lake Kel Theril.json` |
| 55-60 | Eastern Plaguelands | `57-60 Browman Mill.json` |

---

## Farming Routes

### Gathering Routes

Your installation includes specialized gathering paths in `_herb` and `_vein` folders:

```
Json/path/_herb/  - Herbalism routes by zone
Json/path/_vein/  - Mining routes by zone
```

### Gold Farming Routes (Level 60+)

| Route | Zone | Target | Est. Gold/Hour |
|-------|------|--------|----------------|
| `60_Silithus_scorpid.json` | Silithus | Scorpids | 30-50g |
| `70-Terokkar Forest_Barrier_Hill.json` | Terokkar | Birds | 80-120g |
| `65-70 Oshugun.json` | Nagrand | Clefthoofs | 60-100g |

---

## Requirements Reference

### Health/Mana Conditions
```json
"Health% < 50"           // Player health below 50%
"TargetHealth% < 20"     // Target health for execute
"Mana% < 30"             // Low mana for drinks
"PetHealth% < 50"        // Pet needs healing
```

### Combat State Conditions
```json
"InMeleeRange"           // Within 5 yards
"!InMeleeRange"          // At range
"TargetCastingSpell"     // Target is casting
"AutoAttacking"          // Auto attack active
"!AutoShot"              // Not auto-shooting
"MobCount > 1"           // Multiple mobs
```

### Buff/Debuff Conditions
```json
"!Frost Armor"           // Missing buff
"Corruption"             // Has debuff on target
"Has Pet"                // Pet is alive
"Mounted"                // On a mount
"BagFull"                // Inventory full
```

### Resource Conditions
```json
"Rage > 50"              // Warrior rage
"Energy >= 40"           // Rogue/Cat energy
"Combo Point > 4"        // Combo points
"HasAmmo"                // Hunter has ammo
"BagItem:6265:3"         // Has 3+ soul shards
```

### Class-Specific Forms
```json
"Form:Druid_Cat"         // Druid in cat form
"Form:Warrior_BattleStance"  // Warrior stance
"Form:Rogue_Stealth"     // Rogue stealthed
```

---

## Best Practices

### 1. Path Design Tips

```
✅ DO:
- Keep paths away from cliffs and water
- Avoid elite mob patrol routes
- End grind paths near vendor path starts
- Use "PathThereAndBack": true for efficiency
- Test paths manually first

❌ DON'T:
- Path through tight spaces with obstacles
- Cross faction areas (PvP risk)
- Path too close to quest NPCs
- Make paths with sharp turns
```

### 2. Combat Rotation Priority

```
1. Survival abilities (heals when low)
2. Pet maintenance (pet classes)
3. Cooldowns on strong mobs (TargetHealth% > 85)
4. DoTs on mobs that will live long enough
5. Main rotation abilities
6. Auto-attack as fallback
```

### 3. NPC (Vendor) Setup

```json
"NPC": {
  "Sequence": [
    {
      "Name": "Repair",
      "Key": "C",
      "Requirement": "Items Broken",
      "PathFilename": "Vendor_Path.json",
      "Cost": 6
    },
    {
      "Name": "Sell",
      "Key": "C", 
      "Requirements": ["BagFull", "BagGreyItem"],
      "PathFilename": "Vendor_Path.json",
      "Cost": 6
    }
  ]
}
```

### 4. Mail Configuration (New Feature!)

```json
{
  "Mail": true,
  "MailConfig": {
    "RecipientName": "BankAlt",
    "MinimumGoldToKeep": 50000,     // 5 gold in copper
    "MinimumItemQuality": 2,        // Green and above
    "SendGold": true,
    "SendItems": true,
    "ExcludedItemIds": [6948]       // Hearthstone
  }
}
```

---

## Advanced Configurations

### Multi-Path Level Progression

Automatically switch paths based on level:

```json
"Paths": [
  {
    "PathFilename": "_pack/60-70/Hellfire Peninsula/58-60 Felspark Ravine.json",
    "Requirements": ["Level < 62"]
  },
  {
    "PathFilename": "_pack/60-70/Zangarmarsh/60-64.json",
    "Requirements": ["Level >= 62", "Level < 65"]
  },
  {
    "PathFilename": "_pack/60-70/Nagrand/65-70 Oshugun.json"
    // No requirements = fallback for level 65+
  }
]
```

### Variables for DRY Configuration

```json
"IntVariables": {
  "DOT_MIN_HEALTH%": 35,
  "HEAL_THRESHOLD": 50,
  "COOLDOWN_HEALTH%": 85,
  "Item_Soul_Shard": 6265,
  "ITEM_ARROW": 2512,
  "MIN_COUNT_ARROW": 200
}
```

Then use in requirements:
```json
"Requirement": "TargetHealth% > DOT_MIN_HEALTH%"
"Requirement": "Health% < HEAL_THRESHOLD"
```

### Flee Configuration for Safety

```json
"Flee": {
  "Sequence": [
    {
      "Name": "Flee",
      "Requirement": "MobCount > 2 || TargetElite || Health% < 20"
    },
    {
      "Name": "Vanish",
      "Key": "F5",
      "WhenUsable": true,
      "Requirement": "MobCount > 1"
    }
  ]
}
```

### Interrupt Enemy Casts

```json
{
  "Name": "Earth Shock",
  "Key": "3",
  "WhenUsable": true,
  "UseWhenTargetIsCasting": true,
  "Requirements": ["TargetCastingSpell"]
}
```

---

## Quick Start Recommendations

### Best Class for New Users: **Hunter**
- Pet tanks all damage
- Minimal downtime
- Works with any path
- Profiles available for all levels

### Best TBC Grinding Zone: **Nagrand**
- Dense mob spawns
- High XP per kill
- Good vendor access
- Multiple level-appropriate routes

### Recommended Starting Profile
1. Copy `Hunter_62.json` as your template
2. Modify `PathFilename` to your desired route
3. Adjust ability keys to match your keybinds
4. Add NPC vendor path for your zone

### Essential Macros

```lua
-- Vendor macro (bind to key in NPC config)
/target [vendor name]
/stopmacro [noexists]
/script InteractUnit("target")

-- Feed Pet (Hunter)
/cast Feed Pet
/use [your food item]

-- Summon Pet (Hunter)
/cast Call Pet
```

---

## Resources & Links

- **GitHub Repository**: https://github.com/Xian55/WowClassicGrindBot
- **Wiki**: https://github.com/Xian55/WowClassicGrindBot/wiki
- **Issues/Support**: https://github.com/Xian55/WowClassicGrindBot/issues
- **175+ Community Forks**: Check forks for additional profiles and routes

---

*Guide created for WowClassicGrindBot TBC Anniversary Edition*
*Last Updated: Based on dev branch with latest features including Mail system*
