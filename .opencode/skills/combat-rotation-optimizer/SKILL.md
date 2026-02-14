---
name: combat-rotation-optimizer
description: |
  **PROJECT-SPECIFIC SKILL FOR WOW CLASSIC GRIND BOT**
  Combat rotation optimization expert for WowClassicGrindBot. Analyzes spell priorities,
  cast sequences, resource management (mana/energy/rage), and DPS optimization.
  Use for debugging rotation issues, adding new spells, or optimizing class-specific rotations.
allowed-tools: Read, Edit, Grep, Glob, Bash
trigger-keywords: combat, rotation, spell, ability, cast, dps, mana, optimization, cooldown
---

# Combat Rotation Optimizer (WowClassicGrindBot)

Expert combat rotation specialist for WowClassicGrindBot, focusing on spell prioritization, cast sequencing, resource management, and class-specific DPS optimization.

## When to Use

- ⚔️ **Rotation debugging** — Spells not casting or wrong priority
- 🎯 **New spell/ability** — Add new spells to rotation
- 📊 **DPS optimization** — Improve damage output
- 💧 **Resource management** — Mana, energy, rage efficiency
- 🔄 **Cast sequence** — Optimize ability order
- 🐛 **Rotation bugs** — Spells casting at wrong times
- 🧪 **Class support** — Add/improve class rotations

## Architecture Overview

### Key Components

**Location:** `Core/ClassConfiguration/*.json` and `Core/Combat/`

```
ClassConfiguration/
  ├── Mage.json         - Mage spell priorities
  ├── Warrior.json      - Warrior ability priorities
  ├── Priest.json       - Priest spell priorities
  └── ...               - Other classes

Core/Combat/
  ├── RotationOptimizer.cs  - Main rotation engine
  ├── CastingHandler.cs     - Spell casting logic
  ├── CombatUtil.cs         - Combat utilities
  └── Requirements/         - Spell requirement checks
```

### Rotation Configuration (JSON)

**Example:** `ClassConfiguration/Mage.json`

```json
{
  "ClassName": "Mage",
  "Combat": {
    "Enabled": true,
    "Spells": [
      {
        "Name": "Frost Bolt",
        "Key": "1",
        "Rank": 1,
        "CastTime": 2500,
        "ManaCost": 140,
        "School": "Frost",
        "Requirements": {
          "TargetHealth": {"Min": 1, "Max": 100},
          "PlayerMana": {"Min": 150},
          "InCombat": true,
          "TargetInRange": true
        },
        "Priority": 100
      },
      {
        "Name": "Fire Blast",
        "Key": "2",
        "Rank": 1,
        "CastTime": 0,
        "ManaCost": 90,
        "Cooldown": 8000,
        "Requirements": {
          "TargetHealth": {"Min": 1, "Max": 100},
          "PlayerMana": {"Min": 95},
          "InCombat": true
        },
        "Priority": 90
      },
      {
        "Name": "Arcane Missiles",
        "Key": "3",
        "CastTime": 5000,
        "Channeled": true,
        "ManaCost": 315,
        "Requirements": {
          "TargetHealth": {"Min": 30, "Max": 100},
          "PlayerMana": {"Min": 320},
          "Buff": "Arcane Power"
        },
        "Priority": 80
      }
    ]
  }
}
```

### Spell Priority System

**How it works:**

1. **RotationOptimizer** reads spell configuration
2. Filters spells by requirements (range, mana, cooldown)
3. Sorts by priority (highest first)
4. Returns highest-priority castable spell
5. **CastingHandler** executes the spell

**Code:** `Core/Combat/RotationOptimizer.cs`

```csharp
public class RotationOptimizer(ILogger logger, ClassConfiguration config)
{
    public SpellAbility? GetBestAbility(IPlayerReader player, IAddonReader addon)
    {
        var availableSpells = config.Combat.Spells
            .Where(spell => CanCast(spell, player, addon))
            .OrderByDescending(spell => spell.Priority)
            .ToList();
        
        if (availableSpells.Count == 0)
        {
            Log.Debug("[RotationOptimizer] No spells available");
            return null;
        }
        
        var best = availableSpells.First();
        Log.Debug("[RotationOptimizer] Selected {SpellName} (Priority: {Priority})", 
            best.Name, best.Priority);
        
        return best;
    }
    
    private bool CanCast(SpellAbility spell, IPlayerReader player, IAddonReader addon)
    {
        // Check requirements
        if (spell.Requirements.PlayerMana != null)
        {
            if (player.Mana < spell.Requirements.PlayerMana.Min)
                return false;
        }
        
        if (spell.Requirements.TargetHealth != null)
        {
            var targetHp = addon.TargetHealthPercentage;
            if (targetHp < spell.Requirements.TargetHealth.Min || 
                targetHp > spell.Requirements.TargetHealth.Max)
                return false;
        }
        
        if (spell.Requirements.TargetInRange && !player.IsTargetInRange())
            return false;
        
        if (spell.Cooldown > 0 && !IsCooldownReady(spell))
            return false;
        
        if (spell.Requirements.Buff != null && !player.HasBuff(spell.Requirements.Buff))
            return false;
        
        return true;
    }
}
```

## Common Issues

### Issue 1: Spell Never Casts

**Symptom:** Rotation configured but spell doesn't cast

**Debugging:**
```csharp
// Add logging to CanCast()
Log.Debug("[RotationOptimizer] {SpellName} check - Mana:{PlayerMana}/{Required}, InRange:{InRange}", 
    spell.Name, player.Mana, spell.ManaCost, player.IsTargetInRange());
```

**Common causes:**
- ❌ Mana requirement too high
- ❌ Target out of range
- ❌ Cooldown not ready
- ❌ Missing buff requirement
- ❌ Wrong key binding

**Solution:**
```json
// Check requirements are reasonable
{
  "Name": "Frost Bolt",
  "ManaCost": 140,
  "Requirements": {
    "PlayerMana": {"Min": 150}  // Must have 10+ mana buffer
  }
}
```

### Issue 2: Wrong Spell Priority

**Symptom:** Low-damage spell casts before high-damage spell

**Solution:**
```json
// Adjust priorities (higher = cast first)
{
  "Spells": [
    {
      "Name": "Pyroblast",  // High damage
      "Priority": 100
    },
    {
      "Name": "Fire Blast",  // Medium damage
      "Priority": 90
    },
    {
      "Name": "Scorch",  // Low damage filler
      "Priority": 80
    }
  ]
}
```

### Issue 3: Mana Starvation

**Symptom:** Bot runs out of mana every fight

**Solution:**
```json
// Add low-mana filler spells
{
  "Name": "Wand Attack",
  "Key": "9",
  "ManaCost": 0,
  "Requirements": {
    "PlayerMana": {"Max": 200}  // Only if low mana
  },
  "Priority": 10  // Low priority (use as last resort)
}

// Or add mana threshold to expensive spells
{
  "Name": "Arcane Missiles",
  "Requirements": {
    "PlayerMana": {"Min": 500}  // Don't use if low mana
  }
}
```

## Adding a New Spell

### Step 1: Test Spell in Game

1. Bind spell to action bar (e.g., key "3")
2. Test cast time, mana cost, range
3. Note any requirements (buffs, debuffs, procs)

### Step 2: Add to Configuration

**File:** `ClassConfiguration/YourClass.json`

```json
{
  "Name": "New Spell",
  "Key": "3",
  "Rank": 1,
  "CastTime": 2500,
  "ManaCost": 200,
  "School": "Fire",
  "Requirements": {
    "TargetHealth": {"Min": 1, "Max": 100},
    "PlayerMana": {"Min": 210},
    "InCombat": true,
    "TargetInRange": true
  },
  "Priority": 95,
  "Description": "High-damage nuke"
}
```

### Step 3: Test in Combat

1. Start bot
2. Engage enemy
3. Watch logs for spell selection:
   ```
   [RotationOptimizer] Selected New Spell (Priority: 95)
   [CastingHandler   ] Casting New Spell on key 3
   ```

### Step 4: Tune Priority

**Experiment with priorities:**
- 100: Always cast if available (openers, finishers)
- 90-99: High priority (main rotation)
- 80-89: Medium priority (filler)
- 10-79: Low priority (resource conservation)
- 1-9: Last resort (wand, auto-attack)

## Optimization Strategies

### Resource Management

**Mana Classes (Mage, Priest, Warlock):**
```json
// High-mana rotation (>60% mana)
{
  "Name": "Expensive Nuke",
  "Requirements": {"PlayerMana": {"Min": 500}}
}

// Low-mana rotation (<40% mana)
{
  "Name": "Cheap Filler",
  "Requirements": {"PlayerMana": {"Max": 300}}
}
```

**Energy Classes (Rogue):**
```json
// Spend energy efficiently
{
  "Name": "Sinister Strike",
  "Requirements": {"PlayerEnergy": {"Min": 45}}  // Wait for energy
}

{
  "Name": "Eviscerate",
  "Requirements": {
    "ComboPoints": {"Min": 5},  // Spend at 5 CP
    "TargetHealth": {"Min": 1, "Max": 40}  // Finisher
  }
}
```

### Cooldown Management

```json
// Use cooldown on pull
{
  "Name": "Arcane Power",
  "Cooldown": 180000,  // 3 minutes
  "Requirements": {
    "InCombat": true,
    "TargetHealth": {"Min": 90}  // Use early in fight
  },
  "Priority": 100
}

// Use cooldown as execute
{
  "Name": "Execute",
  "Requirements": {
    "TargetHealth": {"Max": 20}  // Only below 20%
  }
}
```

### Buff Management

```json
// Self-buff before combat
{
  "Name": "Mage Armor",
  "Requirements": {
    "InCombat": false,
    "MissingBuff": "Mage Armor"
  },
  "Priority": 100
}

// Proc-based spell
{
  "Name": "Pyroblast",
  "Requirements": {
    "Buff": "Hot Streak"  // Only cast with proc
  },
  "Priority": 100
}
```

## Best Practices

### ✅ Do This

- **Priority spacing** — Use 5-10 point gaps for flexibility
- **Mana buffers** — Require 10+ mana above cost
- **Test in isolation** — Disable other spells to test new one
- **Log rotation decisions** — Debug why spells cast/don't cast
- **Use target health** — Different spells for different HP ranges
- **Cooldown tracking** — Track cooldowns per spell
- **Buff requirements** — Leverage procs and buffs

### ❌ Avoid This

- **All same priority** — Unpredictable rotation
- **No mana buffer** — Constant "not enough mana" errors
- **Exact mana costs** — Small variations break rotation
- **Ignoring cooldowns** — Spamming spells on CD
- **No filler spells** — Standing idle waiting for mana
- **Hardcoded timings** — Latency makes exact timing unreliable

## Integration with Other Skills

**→ context-scout** — Find existing rotation patterns
**→ performance-profiler** — Optimize rotation loop performance
**→ goap-designer** — Integrate with CombatGoal
**→ code-reviewer** — Review rotation logic for bugs

---

**Remember:** Good rotations are adaptive. Use requirements and priorities to handle different combat scenarios (low mana, low HP, cooldowns available, etc.).
