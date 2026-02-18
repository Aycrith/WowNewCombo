---
name: goap-designer
description: |
  **PROJECT-SPECIFIC SKILL FOR WOW CLASSIC GRIND BOT**
  GOAP (Goal-Oriented Action Planning) expert for designing goals, actions, preconditions,
  and effects. Specialized in the WowClassicGrindBot GOAP architecture. Use for creating
  new goals, debugging GOAP plans, or optimizing goal priorities.
allowed-tools: Read, Edit, Grep, Glob, Bash
trigger-keywords: goap, goal, action, precondition, effect, goapagent, plan, priority
---

# GOAP Designer Skill (WowClassicGrindBot)

Expert GOAP (Goal-Oriented Action Planning) architect specializing in the WowClassicGrindBot's goal system, action planning, and autonomous behavior design.

## When to Use

- 🎯 **New goal creation** — Design new GOAP goals for bot behaviors
- 🔧 **Goal debugging** — Fix "NO PLAN" errors or goal conflicts
- 📊 **Priority tuning** — Optimize goal selection and interrupts
- 🔄 **Action planning** — Design preconditions and effects
- 🧪 **GOAP testing** — Verify goal reachability and plan validity
- 📈 **Goal optimization** — Reduce unnecessary goal switches
- 🏗️ **Goal architecture** — Understand full-mesh event system

## WowClassicGrindBot GOAP Architecture

### Core Components

**Location:** `Core/GOAP/`

```
GoapAgent.cs          - Main planning engine
GoapGoal.cs           - Base goal class (all goals inherit)
GoapAction.cs         - Individual actions (not heavily used)
KeysManager.cs        - Manages preconditions/effects (keys)
GoapEventArgs.cs      - Base class for goal events
IGoapEventListener.cs - Event subscription interface
```

### Goal Structure

**Every goal must implement:**

```csharp
public abstract class GoapGoal : IGoapEventListener
{
    // Priority (higher = more important)
    public abstract float CostOfPerformingAction { get; }
    
    // Can this goal run right now?
    public virtual bool CanRun() => true;
    
    // Preconditions that must be met
    public virtual HashSet<KeyValuePair<string, object>> Preconditions() => [];
    
    // What this goal achieves
    public virtual HashSet<KeyValuePair<string, object>> Effects() => [];
    
    // Main execution logic
    public virtual void Update() { }
    
    // Goal-specific events for full-mesh communication
    public virtual void OnEvent(GoapEventArgs e) { }
}
```

### Example: Combat Goal

**Location:** `Core/Goals/CombatGoal.cs`

```csharp
public sealed class CombatGoal(
    ILogger logger,
    ConfigurableInput input,
    Wait wait,
    IAddonReader addonReader,
    IPlayerReader playerReader,
    CombatUtil combatUtil,
    StopMoving stopMoving,
    IMountHandler mountHandler,
    ClassConfiguration classConfig,
    NpcNameTargeting npcNameTargeting,
    CastingHandler castingHandler,
    RotationOptimizer rotationOptimizer) : GoapGoal(logger), IGoapEventListener
{
    // Priority: 6.0 (high priority - interrupts most other goals)
    public override float CostOfPerformingAction => 6.0f;
    
    // Preconditions: None (can always run if enemy nearby)
    public override HashSet<KeyValuePair<string, object>> Preconditions() => [];
    
    // Effects: Kills enemy
    public override HashSet<KeyValuePair<string, object>> Effects() =>
    [
        new(KeysManager.InCombat, false),  // Combat ends
        new(KeysManager.TargetDead, true)  // Target dies
    ];
    
    // Can run if: Not mounted, enemy in range, rotation enabled
    public override bool CanRun()
    {
        if (mountHandler.IsMounted()) return false;
        if (!classConfig.Combat.Enabled) return false;
        if (!playerReader.HasTarget) return false;
        return playerReader.TargetHealth > 0;
    }
    
    // Main combat logic
    public override void Update()
    {
        if (!playerReader.HasTarget)
        {
            AcquireTarget();
            return;
        }
        
        // Use RotationOptimizer to select best spell
        var bestAbility = rotationOptimizer.GetBestAbility(playerReader, addonReader);
        
        if (bestAbility != null)
        {
            castingHandler.CastSpell(bestAbility);
        }
    }
    
    // Listen to events from other goals
    public override void OnEvent(GoapEventArgs e)
    {
        if (e is PlayerDeathEvent)
        {
            // React to death - return to corpse
        }
        else if (e is LootAvailableEvent lootEvent)
        {
            // Wait for looting to complete
        }
    }
}
```

### Goal Event System (Full-Mesh)

**Every goal can communicate with every other goal:**

```csharp
// Goal publishes event
public class CombatStartedEvent(Vector3 enemyPosition) : GoapEventArgs
{
    public Vector3 EnemyPosition { get; } = enemyPosition;
}

// In CombatGoal.cs
SendGoapEvent(new CombatStartedEvent(enemy.Position));

// All other goals receive it via OnEvent()
// PathGoal might pause pathfinding
// LootGoal might prepare to loot after combat
```

## Common GOAP Issues

### Issue 1: "NO PLAN" Error

**Symptom:**
```
[GoapAgent          ] NO PLAN
```

**Causes:**
1. **Impossible preconditions** — No goal can satisfy requirements
2. **Circular dependencies** — Goal A needs B, Goal B needs A
3. **All goals return `CanRun() == false`** — No valid starting point

**Solution:**
```bash
# Use context-scout to find goals
glob "**/Goals/*.cs"

# Check preconditions
grep "Preconditions\(\)" Core/Goals/*.cs

# Check CanRun() logic
grep "CanRun\(\)" Core/Goals/*.cs

# Verify at least one goal has no preconditions (entry point)
```

### Issue 2: Goal Thrashing

**Symptom:** Bot keeps switching between goals rapidly

**Causes:**
1. **Similar priorities** — Goals compete with similar costs
2. **Toggling preconditions** — Conditions flip-flop each frame
3. **Missing cooldowns** — Goal tries immediately after failing

**Solution:**
```csharp
// Add priority separation (0.1+ difference)
public override float CostOfPerformingAction => 5.5f;  // Not 5.0

// Add cooldown after failure
private DateTime lastAttempt;
public override bool CanRun()
{
    if (DateTime.Now - lastAttempt < TimeSpan.FromSeconds(5))
        return false;
    return base.CanRun();
}
```

### Issue 3: Goal Never Runs

**Symptom:** Specific goal never executes

**Debugging:**
```csharp
// Add logging to CanRun()
public override bool CanRun()
{
    Log.Debug("[MyGoal          ] CanRun check - HasTarget:{HasTarget}, InCombat:{InCombat}", 
        playerReader.HasTarget, playerReader.InCombat);
    return playerReader.HasTarget && !playerReader.InCombat;
}

// Add logging to GoapAgent plan selection (Core/GOAP/GoapAgent.cs)
// Check if goal is even considered
```

## Creating a New Goal

### Step 1: Define Goal Purpose

```markdown
Goal: GatherHerbGoal
Purpose: Detect nearby herbs and gather them
Priority: 4.0 (medium - below combat, above wandering)
Preconditions: Not in combat, not mounted
Effects: HasHerb = true
```

### Step 2: Create Goal Class

**File:** `Core/Goals/GatherHerbGoal.cs`

```csharp
public sealed class GatherHerbGoal(
    ILogger logger,
    IAddonReader addonReader,
    IPlayerReader playerReader,
    StopMoving stopMoving,
    IMountHandler mountHandler,
    KeyAction interactKey) : GoapGoal(logger)
{
    public override float CostOfPerformingAction => 4.0f;
    
    public override HashSet<KeyValuePair<string, object>> Preconditions() =>
    [
        new(KeysManager.InCombat, false),
        new(KeysManager.IsMounted, false)
    ];
    
    public override HashSet<KeyValuePair<string, object>> Effects() =>
    [
        new(KeysManager.HasHerb, true)
    ];
    
    private Vector3? nearestHerbPosition;
    
    public override bool CanRun()
    {
        if (playerReader.InCombat) return false;
        if (mountHandler.IsMounted()) return false;
        
        // Check if herbs visible (from addon data)
        nearestHerbPosition = FindNearestHerb();
        return nearestHerbPosition.HasValue;
    }
    
    public override void Update()
    {
        if (!nearestHerbPosition.HasValue)
        {
            // Lost herb, goal will exit
            return;
        }
        
        // Move to herb
        if (Vector3.Distance(playerReader.Position, nearestHerbPosition.Value) > 5f)
        {
            // Trigger movement (could delegate to PathGoal via event)
            SendGoapEvent(new MoveToPositionEvent(nearestHerbPosition.Value));
            return;
        }
        
        // In range - interact
        stopMoving.Stop();
        interactKey.PressKey();
        
        // Wait for gathering cast
        Wait.For(3000, addonReader.ReadFunc);
    }
    
    private Vector3? FindNearestHerb()
    {
        // Read herb positions from addon
        // Return nearest herb within 40 yards
        return addonReader.GetNearestHerb();
    }
}
```

### Step 3: Register Goal

**File:** `BlazorServer/Extensions/ServiceCollectionExtension.cs` (or HeadlessServer)

```csharp
services.AddSingleton<GatherHerbGoal>();

// Add to goals list in GoapAgent
services.AddSingleton(provider => new GoapAgent(
    provider.GetRequiredService<ILogger<GoapAgent>>(),
    [
        provider.GetRequiredService<WalkToCorpseGoal>(),
        provider.GetRequiredService<CombatGoal>(),
        provider.GetRequiredService<GatherHerbGoal>(),  // NEW
        provider.GetRequiredService<PathGoal>(),
        // ... other goals
    ]
));
```

### Step 4: Add Key (if needed)

**File:** `Core/GOAP/KeysManager.cs`

```csharp
public static class KeysManager
{
    public const string InCombat = "InCombat";
    public const string IsMounted = "IsMounted";
    public const string HasHerb = "HasHerb";  // NEW
    // ... other keys
}
```

## Best Practices

### ✅ Do This

- **Clear priority separation** — 0.1+ difference between similar goals
- **Minimal preconditions** — More preconditions = harder to plan
- **Fast CanRun()** — Called every frame, must be lightweight
- **Use events for communication** — Don't directly access other goals
- **Log state changes** — `[GoalName          ]` format (padded to 18 chars)
- **Handle interruptions** — Goals can be interrupted mid-execution
- **Test in isolation** — Disable other goals to test new goal alone

### ❌ Avoid This

- **Heavy computation in CanRun()** — Cache expensive checks
- **Blocking in Update()** — Use async patterns or yield
- **Circular preconditions** — Goal A needs B, B needs A
- **Too many preconditions** — Makes planning impossible
- **Ignoring events** — Full-mesh means all goals should listen
- **Hardcoded priorities** — Use configuration when possible

## Integration with Other Skills

**→ context-scout** — Find existing goals and patterns
**→ code-reviewer** — Review GOAP logic for correctness
**→ test-strategist** — Create unit tests for goal logic
**→ performance-profiler** — Profile Update() and CanRun() performance

---

**Remember:** GOAP is autonomous decision-making. Goals compete based on priority and feasibility. Design clear preconditions, effects, and priorities for predictable behavior.
