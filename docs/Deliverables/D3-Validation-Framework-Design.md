# Deliverable 3: Validation Framework Design

**Date:** 2026-02-06
**Scope:** Self-contained validation and testing framework for offline verification of Navigation, Combat, and Input modules
**Constraints:** No live WoW client. All validation via static analysis, mock interfaces, and automated tests.

---

## Executive Summary

This document specifies a validation framework consisting of 4 layers:

1. **Roslyn Static Analyzers** — Compile-time detection of ArrayPool use-after-return and IDisposable violations
2. **Mock Interface Layer** — Offline simulation of WoW client interactions for unit/integration testing
3. **JSON Profile Schema Validation** — Structural validation of class profiles and path files
4. **Health Check Dashboard Enhancement** — Runtime observability for navigation, combat, and system health

Each layer is designed to be incrementally implementable. The Roslyn analyzers alone would have prevented 7 of the 8 Critical issues in Deliverable 1.

---

## Layer 1: Roslyn Static Analyzers

### 1.1 Analyzer: ArrayPool Use-After-Return Detection

**Analyzer ID:** `WCG001`
**Severity:** Error
**Category:** Reliability

**Detection Pattern:**
```
IF a local variable `arr` is assigned from `ArrayPool<T>.Shared.Rent()`
AND `pooler.Return(arr)` is called
AND `arr` is referenced AFTER the `Return()` call (in any expression)
THEN report diagnostic WCG001
```

**Implementation Approach:**

```csharp
// File: Analyzers/ArrayPoolUseAfterReturnAnalyzer.cs
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArrayPoolUseAfterReturnAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WCG001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "ArrayPool array used after Return",
        messageFormat: "Array '{0}' is used after being returned to ArrayPool. " +
                       "Copy data before calling Return().",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Returning an array to ArrayPool<T>.Shared then using it " +
                     "(via Span, ReadOnlySpan, ArraySegment, or direct access) " +
                     "causes a race condition where another thread can overwrite the data.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private void AnalyzeInvocation(OperationAnalysisContext context)
    {
        // 1. Detect calls to ArrayPool<T>.Return(array)
        // 2. Identify the local variable passed to Return()
        // 3. Walk forward in the containing block
        // 4. Flag any subsequent reference to that variable
        //    (including .AsSpan(), new ReadOnlySpan(), new ArraySegment(), indexer access)
    }
}
```

**Test Cases:**

| Test | Input Pattern | Expected |
|------|--------------|----------|
| Basic use-after-return | `pool.Return(arr); return arr.AsSpan();` | WCG001 |
| Correct: copy first | `Array.Copy(arr, result, n); pool.Return(arr); return result;` | No diagnostic |
| ArraySegment wrap | `pool.Return(arr); return new ArraySegment<T>(arr);` | WCG001 |
| ReadOnlySpan constructor | `pool.Return(arr); return new ReadOnlySpan<T>(arr);` | WCG001 |
| Conditional return | `if (x) { pool.Return(arr); } return arr;` | WCG001 |
| No return call | `var arr = pool.Rent(n); return arr.AsSpan();` | No diagnostic (leak, but different rule) |

**Files this would catch:** All 7 instances from Deliverable 1 (Issues 1.1-1.7).

---

### 1.2 Analyzer: IDisposable Field Not Disposed

**Analyzer ID:** `WCG002`
**Severity:** Warning
**Category:** Reliability

**Detection Pattern:**
```
IF a class implements IDisposable
AND has a field of a type that implements IDisposable
AND the Dispose() method does not call Dispose() on that field
THEN report diagnostic WCG002
```

**Specific Sub-Rules:**

| Sub-ID | Pattern | Message |
|--------|---------|---------|
| `WCG002a` | `CancellationTokenSource` field not disposed | "CancellationTokenSource '{0}' is cancelled but never disposed. Call Dispose() to release the WaitHandle." |
| `WCG002b` | `ManualResetEventSlim` field not disposed | "ManualResetEventSlim '{0}' is never disposed. Its internal WaitHandle leaks an OS kernel object." |
| `WCG002c` | `Thread` field not joined | "Thread '{0}' is started but never joined in Dispose(). The thread may access disposed objects." |

**Files this would catch:** Issues 2.1 (BotController), 2.2 (GoapAgent), 2.3 (RemotePathingAPIV3).

---

### 1.3 Analyzer: Process Handle Leak

**Analyzer ID:** `WCG003`
**Severity:** Warning
**Category:** Reliability

**Detection Pattern:**
```
IF Process.GetProcesses() or Process.GetProcessesByName() is called
AND the returned array elements are not ALL disposed
THEN report diagnostic WCG003
```

**Files this would catch:** Issues 4.1 (WowInput), 4.2 (WoWProcessLauncher).

---

### 1.4 Project Structure

```
Analyzers/
├── Analyzers.csproj                           # netstandard2.0 (Roslyn requirement)
├── ArrayPoolUseAfterReturnAnalyzer.cs         # WCG001
├── DisposableFieldAnalyzer.cs                 # WCG002
├── ProcessHandleLeakAnalyzer.cs               # WCG003
├── Properties/
│   └── AnalyzerReleases.Shipped.md
└── Analyzers.Test/
    ├── Analyzers.Test.csproj
    ├── ArrayPoolUseAfterReturnTests.cs
    ├── DisposableFieldAnalyzerTests.cs
    └── ProcessHandleLeakTests.cs
```

**Integration:** Add `<ProjectReference>` from `Core.csproj`, `PPather.csproj`, `PathingAPI.csproj` with `OutputItemType="Analyzer"` to enable compile-time checking.

---

## Layer 2: Mock Interface Layer

### 2.1 Interface Inventory

The bot interacts with WoW through these key interfaces. Mock implementations enable offline testing of all business logic.

| Interface | Purpose | Location | Mock Strategy |
|-----------|---------|----------|---------------|
| `IPlayerReader` | Reads player state (health, mana, position, buffs) | `Core/` | Record-based replay from JSON snapshots |
| `IAddonReader` | Reads addon pixel data (all game state) | `Core/` | Pre-recorded frame sequences |
| `IBitmapProvider` | Provides screen captures | `Core/` | Static images loaded from disk |
| `IWowScreen` | Screen capture interface | `Core/` | Returns test images |
| `IInput` | Keyboard/mouse input injection | `Core/` | Action log recorder (no actual input) |
| `IWowProcess` | WoW process handle and window info | `Game/` | Stub returning fake PID/HWND |
| `IMouseInput` | Mouse movement and clicks | `SharedLib/` | Position recorder |
| `IKeyInput` | Keyboard input | `SharedLib/` | Key log recorder |

### 2.2 Mock Design: IPlayerReader

```csharp
// File: CoreTests/Mocks/MockPlayerReader.cs
namespace CoreTests.Mocks;

/// <summary>
/// Replays pre-recorded player state snapshots for deterministic testing.
/// Snapshots can be loaded from JSON files or constructed programmatically.
/// </summary>
public sealed class MockPlayerReader : IPlayerReader
{
    private readonly PlayerSnapshot[] _snapshots;
    private int _frameIndex;

    public MockPlayerReader(PlayerSnapshot[] snapshots)
    {
        _snapshots = snapshots;
        _frameIndex = 0;
    }

    /// <summary>
    /// Load snapshots from a JSON file for replay-based testing.
    /// </summary>
    public static MockPlayerReader FromFile(string jsonPath)
    {
        string json = File.ReadAllText(jsonPath);
        PlayerSnapshot[] snapshots = JsonSerializer.Deserialize<PlayerSnapshot[]>(json)!;
        return new MockPlayerReader(snapshots);
    }

    /// <summary>
    /// Create a single-frame mock for unit testing specific states.
    /// </summary>
    public static MockPlayerReader SingleState(Action<PlayerSnapshotBuilder> configure)
    {
        PlayerSnapshotBuilder builder = new();
        configure(builder);
        return new MockPlayerReader([builder.Build()]);
    }

    // IPlayerReader implementation reads from current snapshot
    public int HealthPercent => Current.HealthPercent;
    public int ManaPercent => Current.ManaPercent;
    public int TargetHealthPercent => Current.TargetHealthPercent;
    public Vector3 MapPos => Current.MapPos;
    public float Direction => Current.Direction;
    public bool HasTarget => Current.HasTarget;
    public bool IsCasting => Current.IsCasting;
    public bool IsInCombat => Current.IsInCombat;
    // ... all other properties

    public void AdvanceFrame() => _frameIndex = Math.Min(_frameIndex + 1, _snapshots.Length - 1);
    private PlayerSnapshot Current => _snapshots[_frameIndex];
}

/// <summary>
/// Immutable snapshot of player state at a single point in time.
/// </summary>
public sealed record PlayerSnapshot(
    int HealthPercent,
    int ManaPercent,
    int TargetHealthPercent,
    Vector3 MapPos,
    float Direction,
    bool HasTarget,
    bool IsCasting,
    bool IsInCombat,
    bool IsInMeleeRange,
    int PlayerLevel,
    int TargetLevel,
    UnitClassification TargetClassification,
    // ... all other state fields
    FrozenDictionary<string, bool> Buffs,
    FrozenDictionary<string, bool> TargetDebuffs
);

/// <summary>
/// Builder for constructing test snapshots with sensible defaults.
/// </summary>
public sealed class PlayerSnapshotBuilder
{
    private int _healthPercent = 100;
    private int _manaPercent = 100;
    private int _targetHealthPercent = 100;
    private Vector3 _mapPos = new(0, 0, 0);
    private bool _hasTarget = false;
    // ... defaults for all fields

    public PlayerSnapshotBuilder WithHealth(int percent) { _healthPercent = percent; return this; }
    public PlayerSnapshotBuilder WithMana(int percent) { _manaPercent = percent; return this; }
    public PlayerSnapshotBuilder WithTarget(int healthPercent) { _hasTarget = true; _targetHealthPercent = healthPercent; return this; }
    public PlayerSnapshotBuilder AtPosition(float x, float y, float z) { _mapPos = new(x, y, z); return this; }
    public PlayerSnapshotBuilder InCombat() { /* ... */ return this; }
    public PlayerSnapshotBuilder WithBuff(string name) { /* ... */ return this; }
    public PlayerSnapshotBuilder WithTargetDebuff(string name) { /* ... */ return this; }

    public PlayerSnapshot Build() => new(
        _healthPercent, _manaPercent, _targetHealthPercent,
        _mapPos, /* ... all other fields with defaults ... */
    );
}
```

### 2.3 Mock Design: IInput (Action Logger)

```csharp
// File: CoreTests/Mocks/MockInput.cs
namespace CoreTests.Mocks;

/// <summary>
/// Records all input actions for assertion in tests.
/// No actual keyboard/mouse input is sent.
/// </summary>
public sealed class MockInput : IInput
{
    private readonly List<InputAction> _actions = [];

    public ReadOnlySpan<InputAction> Actions => CollectionsMarshal.AsSpan(_actions);

    public void PressKey(ConsoleKey key)
    {
        _actions.Add(new InputAction(InputActionType.KeyPress, key.ToString(), DateTime.UtcNow));
    }

    public void KeyPressSleep(ConsoleKey key, int milliseconds, CancellationToken ct)
    {
        _actions.Add(new InputAction(InputActionType.KeyPressDuration, key.ToString(), DateTime.UtcNow)
        {
            DurationMs = milliseconds
        });
    }

    // ... all IInput methods

    // Assertion helpers
    public bool WasKeyPressed(ConsoleKey key) =>
        _actions.Any(a => a.Type == InputActionType.KeyPress && a.Key == key.ToString());

    public int KeyPressCount(ConsoleKey key) =>
        _actions.Count(a => a.Key == key.ToString());

    public void AssertSequence(params ConsoleKey[] expectedKeys)
    {
        var actualKeys = _actions
            .Where(a => a.Type is InputActionType.KeyPress or InputActionType.KeyPressDuration)
            .Select(a => Enum.Parse<ConsoleKey>(a.Key))
            .ToArray();
        Assert.Equal(expectedKeys, actualKeys);
    }

    public void Clear() => _actions.Clear();
}

public sealed record InputAction(InputActionType Type, string Key, DateTime Timestamp)
{
    public int DurationMs { get; init; }
}

public enum InputActionType { KeyPress, KeyPressDuration, MouseMove, MouseClick }
```

### 2.4 Test Scenarios Using Mocks

#### Scenario A: Combat Rotation Under Stress

```csharp
[Fact]
public void CombatGoal_LowHealth_PrioritizesHealOverDamage()
{
    // Arrange
    MockPlayerReader player = MockPlayerReader.SingleState(b => b
        .WithHealth(25)           // Low health
        .WithMana(80)             // Enough mana
        .WithTarget(60)           // Target alive
        .InCombat());

    MockInput input = new();

    // Configure a Priest-like rotation
    KeyAction heal = new() { Name = "Flash Heal", Key = "3", Cost = 18,
        Requirement = "Health% < 40" };
    KeyAction smite = new() { Name = "Smite", Key = "1", Cost = 12,
        Requirement = "TargetHealth% > 0" };

    // Act — evaluate which action should be chosen
    // (Use RequirementFactory to compile requirements, then evaluate)
    bool healUsable = EvaluateRequirements(heal.Requirement, player);
    bool smiteUsable = EvaluateRequirements(smite.Requirement, player);

    // Assert
    Assert.True(healUsable, "Heal should be usable at 25% health");
    Assert.True(smiteUsable, "Smite should also be usable");
    // In the actual rotation, heal appears BEFORE smite in the sequence,
    // so it should be chosen first (first-match-wins)
}
```

#### Scenario B: GOAP Planner Finds Valid Goal

```csharp
[Fact]
public void GoapPlanner_WithTarget_SelectsCombatGoal()
{
    // Arrange
    MockPlayerReader player = MockPlayerReader.SingleState(b => b
        .WithHealth(100)
        .WithTarget(100)
        .InCombat());

    // Build minimal GOAP goal set
    var goals = CreateMinimalGoalSet(player);

    // Act
    GoapGoal? selectedGoal = GoapPlanner.Plan(goals, GetWorldState(player));

    // Assert
    Assert.NotNull(selectedGoal);
    Assert.IsType<CombatGoal>(selectedGoal);
}
```

#### Scenario C: Pathfinding Returns Valid Route

```csharp
[Fact]
public void LocalPathfinding_ReturnsNonEmptyRoute()
{
    // Arrange — use local pathfinding (no remote server needed)
    var pather = CreateLocalPather();
    Vector3 from = new(-896, -3770, 11);   // Barrens, Ratchet
    Vector3 to = new(-441, -2596, 96);     // Barrens, Crossroads

    // Act
    Vector3[] route = pather.FindMapRoute(uiMap: 1413, from, to);

    // Assert
    Assert.NotEmpty(route);
    Assert.Equal(from.X, route[0].X, precision: 10);
    Assert.Equal(to.X, route[^1].X, precision: 50);  // Allow tolerance
}
```

---

## Layer 3: JSON Profile Schema Validation

### 3.1 Class Profile Schema

Class profiles in `Json/class/` are critical configuration. A single typo can crash the bot or cause incorrect behavior. Formal schema validation catches errors before runtime.

**Schema Definition (JSON Schema Draft 2020-12):**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://wowclassicgrindbot.local/schemas/class-profile.json",
  "title": "Class Profile",
  "description": "WoW Classic Grind Bot class profile defining combat rotation, pathing, and NPC interactions",
  "type": "object",
  "required": ["ClassName", "PathFilename"],
  "properties": {
    "ClassName": {
      "type": "string",
      "enum": ["Warrior", "Paladin", "Hunter", "Rogue", "Priest", "Shaman",
               "Mage", "Warlock", "Druid", "Death Knight"]
    },
    "Mode": {
      "type": "string",
      "enum": ["Grind", "CorpseRun", "AttendedGather", "AttendedGrind", "AssistFocus"],
      "default": "Grind"
    },
    "PathFilename": {
      "type": "string",
      "pattern": "^[\\w\\-\\./_\\\\]+\\.json$"
    },
    "PathThereAndBack": { "type": "boolean", "default": true },
    "Loot": { "type": "boolean", "default": true },
    "Skin": { "type": "boolean", "default": false },
    "UseMount": { "type": "boolean", "default": false },
    "NPCMaxLevels_Above": { "type": "integer", "minimum": 0, "maximum": 10 },
    "NPCMaxLevels_Below": { "type": "integer", "minimum": 0, "maximum": 15 },
    "Blacklist": {
      "type": "array",
      "items": { "type": "string" }
    },
    "Pull": { "$ref": "#/$defs/ActionSequence" },
    "Combat": { "$ref": "#/$defs/ActionSequence" },
    "Adhoc": { "$ref": "#/$defs/ActionSequence" },
    "NPC": { "$ref": "#/$defs/ActionSequence" }
  },
  "$defs": {
    "ActionSequence": {
      "type": "object",
      "properties": {
        "Sequence": {
          "type": "array",
          "items": { "$ref": "#/$defs/KeyAction" }
        }
      }
    },
    "KeyAction": {
      "type": "object",
      "required": ["Name"],
      "properties": {
        "Name": { "type": "string", "minLength": 1 },
        "Key": { "type": "string", "maxLength": 10 },
        "WhenUsable": { "type": "boolean" },
        "HasCastBar": { "type": "boolean" },
        "Cost": { "type": "integer", "minimum": 0 },
        "Cooldown": { "type": "integer", "minimum": 0 },
        "Requirement": { "type": "string" },
        "Requirements": {
          "type": "array",
          "items": { "type": "string" }
        },
        "AfterCastWaitCastbar": { "type": "boolean" },
        "BeforeCastStop": { "type": "boolean" },
        "StopBeforeCast": { "type": "boolean" },
        "Item": { "type": "boolean" },
        "UseWhenTargetIsCasting": { "type": "boolean" },
        "InCombat": { "type": "string", "enum": ["true", "false"] },
        "School": {
          "type": "string",
          "enum": ["Physical", "Holy", "Fire", "Nature", "Frost", "Shadow", "Arcane"]
        }
      }
    }
  }
}
```

### 3.2 Requirement Syntax Validator

Requirements are string expressions parsed by `RequirementFactory.cs`. A standalone validator catches syntax errors without running the bot.

**Valid Requirement Patterns:**
```
Health% < 40          → Comparison: {Property} {Operator} {Value}
TargetHealth% > 50    → Comparison with Target prefix
!BuffName             → Boolean negation
SpellInRange:0        → Parameterized check
InMeleeRange          → Boolean flag
HasTarget             → Boolean flag
MobCount < 2          → Numeric comparison
MainHandSwing > -400  → Negative value comparison
BagFull               → Boolean flag
Durability% < 35      → Percentage comparison
npcID:1234            → NPC ID check
Form:2                → Druid form check
Race:Undead           → Race check
```

**Validator Implementation:**
```csharp
public static class RequirementValidator
{
    private static readonly FrozenSet<string> BooleanFlags = new HashSet<string>
    {
        "HasTarget", "TargetAlive", "InMeleeRange", "BagFull", "HasPet",
        "IsMounted", "IsSwimming", "IsInCombat", "IsCasting", "HasRangedWeapon",
        "AutoAttacking", "HasFocus", "FocusAlive", "Drinking", "Eating",
        "IsTargetDead", "Swimming", "Falling"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ComparisonProperties = new HashSet<string>
    {
        "Health%", "Mana%", "TargetHealth%", "TargetMana%", "Energy%", "Rage%",
        "RunicPower%", "MobCount", "MinRange", "MaxRange", "Durability%",
        "FoodCount", "DrinkCount", "MainHandSwing", "BagCount", "PlayerLevel"
    }.ToFrozenSet();

    public static ValidationResult Validate(string requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement))
            return ValidationResult.Error("Empty requirement");

        // Negation prefix
        string trimmed = requirement.TrimStart('!');

        // Boolean flag check
        if (BooleanFlags.Contains(trimmed))
            return ValidationResult.Ok();

        // Parameterized checks (SpellInRange:0, npcID:1234, Form:2, Race:Undead)
        if (trimmed.Contains(':'))
        {
            string[] parts = trimmed.Split(':', 2);
            return ValidateParameterized(parts[0], parts[1]);
        }

        // Comparison expression (Health% < 40)
        string[] tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 3)
        {
            return ValidateComparison(tokens[0], tokens[1], tokens[2]);
        }

        // Buff/Debuff name (implicit boolean check)
        if (trimmed.Length > 0 && char.IsLetter(trimmed[0]))
            return ValidationResult.Ok();  // Assumed buff name

        return ValidationResult.Error($"Unrecognized requirement syntax: '{requirement}'");
    }
}
```

### 3.3 Validation Test Runner

```csharp
// File: CoreTests/ProfileValidation/ProfileValidationTests.cs
[Theory]
[MemberData(nameof(GetAllProfilePaths))]
public void ClassProfile_IsValid(string profilePath)
{
    // Arrange
    string json = File.ReadAllText(profilePath);
    JsonSchema schema = JsonSchema.FromFile("schemas/class-profile.json");

    // Act
    EvaluationResults results = schema.Evaluate(JsonNode.Parse(json));

    // Assert
    Assert.True(results.IsValid,
        $"Profile {Path.GetFileName(profilePath)} failed validation:\n" +
        string.Join("\n", results.Errors.Select(e => $"  {e.Path}: {e.Message}")));
}

[Theory]
[MemberData(nameof(GetAllRequirements))]
public void Requirement_HasValidSyntax(string requirement, string profileName, string actionName)
{
    // Act
    ValidationResult result = RequirementValidator.Validate(requirement);

    // Assert
    Assert.True(result.IsValid,
        $"Invalid requirement '{requirement}' in {profileName}.{actionName}: {result.Error}");
}

public static IEnumerable<object[]> GetAllProfilePaths()
{
    foreach (string path in Directory.GetFiles("Json/class", "*.json", SearchOption.AllDirectories))
        yield return [path];
}

public static IEnumerable<object[]> GetAllRequirements()
{
    foreach (string path in Directory.GetFiles("Json/class", "*.json", SearchOption.AllDirectories))
    {
        var profile = JsonSerializer.Deserialize<ClassConfiguration>(File.ReadAllText(path));
        string name = Path.GetFileNameWithoutExtension(path);

        foreach (var seq in new[] { profile?.Pull, profile?.Combat, profile?.Adhoc, profile?.NPC })
        {
            if (seq?.Sequence == null) continue;
            foreach (var action in seq.Sequence)
            {
                if (!string.IsNullOrEmpty(action.Requirement))
                    yield return [action.Requirement, name, action.Name];
                if (action.Requirements != null)
                    foreach (var req in action.Requirements)
                        yield return [req, name, action.Name];
            }
        }
    }
}
```

---

## Layer 4: Health Check Dashboard Enhancement

### 4.1 Enhanced `/api/health` Response Schema

**Current state:** Returns hardcoded `"OK"` status (see D1 Issue 3.3).

**Proposed schema:**

```json
{
  "Status": "Degraded",
  "TimestampUtc": "2026-02-06T20:19:39Z",
  "App": {
    "Name": "BlazorServer",
    "Version": "1.0.0",
    "ProcessId": 12345,
    "Uptime": "02:15:30",
    "ThreadCount": 42
  },
  "Navigation": {
    "Status": "Connected",
    "ServerProcess": {
      "Pid": 6789,
      "IsRunning": true,
      "Port": 47110,
      "UptimeSeconds": 8100
    },
    "RemotePathing": {
      "IsConnected": true,
      "LatencyMs": 12,
      "FallbackActive": false,
      "FailureCount": 0
    },
    "CircuitBreaker": {
      "State": "Closed",
      "FailureCount": 0,
      "LastFailure": null,
      "CooldownRemainingMs": 0
    }
  },
  "Combat": {
    "Status": "Active",
    "CurrentGoal": "CombatGoal",
    "GoalHistory": ["FollowRouteGoal", "ApproachTargetGoal", "CombatGoal"],
    "RotationOptimizer": {
      "Enabled": true,
      "TotalCasts": 145,
      "SuccessRate": 0.92
    },
    "SessionStats": {
      "Kills": 47,
      "Deaths": 0,
      "XPPerHour": 12500
    }
  },
  "Input": {
    "Status": "Ready",
    "WoWProcess": {
      "Pid": 11111,
      "IsRunning": true,
      "WindowTitle": "World of Warcraft",
      "HasFocus": false
    },
    "LastInputTimestamp": "2026-02-06T20:19:38Z"
  },
  "FeatureFlags": {
    "Humanization": true,
    "HazardAvoidance": true,
    "CombatRotationOptimizer": true,
    "CircuitBreaker": true,
    "HybridLLMDecision": false,
    "GlobalKillSwitch": false
  },
  "Diagnostics": {
    "WarningCount": 2,
    "Warnings": [
      "HybridPather: Remote unavailable, using local fallback since 20:15:00",
      "ScheduledBreak: Next break in 12 minutes"
    ]
  }
}
```

### 4.2 Status Aggregation Logic

```csharp
/// <summary>
/// Aggregates component health into an overall system status.
/// </summary>
public static string DetermineOverallStatus(
    bool wowProcessRunning,
    bool navServerRunning,
    bool remotePathingConnected,
    CircuitBreakerState cbState,
    bool goapHasPlan)
{
    if (!wowProcessRunning)
        return "Critical";

    if (!navServerRunning || cbState == CircuitBreakerState.Open)
        return "Degraded";

    if (!remotePathingConnected || !goapHasPlan)
        return "Warning";

    return "Healthy";
}
```

### 4.3 Health Check Verification Tests

```csharp
[Fact]
public void HealthStatus_WowProcessDown_ReturnsCritical()
{
    string status = HealthAggregator.DetermineOverallStatus(
        wowProcessRunning: false,
        navServerRunning: true,
        remotePathingConnected: true,
        cbState: CircuitBreakerState.Closed,
        goapHasPlan: true);

    Assert.Equal("Critical", status);
}

[Fact]
public void HealthStatus_NavServerDown_ReturnsDegraded()
{
    string status = HealthAggregator.DetermineOverallStatus(
        wowProcessRunning: true,
        navServerRunning: false,
        remotePathingConnected: false,
        cbState: CircuitBreakerState.Closed,
        goapHasPlan: true);

    Assert.Equal("Degraded", status);
}

[Fact]
public void HealthStatus_AllHealthy_ReturnsHealthy()
{
    string status = HealthAggregator.DetermineOverallStatus(
        wowProcessRunning: true,
        navServerRunning: true,
        remotePathingConnected: true,
        cbState: CircuitBreakerState.Closed,
        goapHasPlan: true);

    Assert.Equal("Healthy", status);
}
```

---

## Layer 5: Module-Specific Validation Coverage

### 5.1 Navigation Module Validation

| Test | What It Validates | Mock Dependencies |
|------|------------------|-------------------|
| Local pathfinding returns non-empty route | `LocalPathingApi.FindMapRoute()` produces valid waypoints | None (uses MPQ data files) |
| Path simplification preserves endpoints | `PathSimplify.Simplify()` keeps first/last points | None (pure function) |
| GraphChunk serialization roundtrip | Save → Load produces identical spots | None (file I/O) |
| HybridPather fallback on disconnect | Falls back to local pathing when remote unavailable | `MockAnTcpClient` (returns disconnected) |
| NavServerManager restart on crash | Detects process exit and restarts | `MockProcess` (simulates crash) |
| ArrayPool fix verification | Verify copy-before-return pattern works | None (pure function) |

### 5.2 Combat Module Validation

| Test | What It Validates | Mock Dependencies |
|------|------------------|-------------------|
| Requirement evaluation (all operators) | `<`, `>`, `==`, `!=`, `<=`, `>=` with all property types | `MockPlayerReader` |
| Buff/debuff detection | `!Battle Shout` evaluates correctly | `MockPlayerReader` with buff state |
| Combat sequence first-match | First usable ability in sequence is selected | `MockPlayerReader` + `MockInput` |
| GOAP goal selection | Planner selects correct goal for combat state | `MockPlayerReader` |
| Rotation optimizer scoring | DpsRoleStrategy scores abilities correctly | Mock ability data |
| KeyAction cooldown tracking | Ability is skipped while on cooldown | `MockPlayerReader` with time simulation |

### 5.3 Input Module Validation

| Test | What It Validates | Mock Dependencies |
|------|------------------|-------------------|
| ConfigurableInput clear target sequence | Alt-Insert → ESC → F11 → /cleartarget order | `MockInput` + `MockExecGameCommand` |
| Key binding resolution | KeyReader 5-priority pipeline resolves correctly | `MockAddonReader` (binding data) |
| Input action logging | All key presses are recorded | `MockInput` |
| Humanized input timing | Gaussian delay distribution is within bounds | `MockInput` with timestamp recording |

---

## Implementation Roadmap

| Phase | Scope | Effort | Dependencies |
|-------|-------|--------|-------------|
| Phase A | Roslyn Analyzer WCG001 (ArrayPool) | 2-3 days | Roslyn SDK knowledge |
| Phase B | Mock IPlayerReader + PlayerSnapshotBuilder | 1-2 days | Existing IPlayerReader interface |
| Phase C | JSON Profile Schema + RequirementValidator | 1-2 days | JSON Schema library |
| Phase D | Enhanced `/api/health` endpoint | 1 day | Existing HealthController |
| Phase E | Navigation module tests (6 tests) | 1-2 days | Phase B mocks |
| Phase F | Combat module tests (6 tests) | 1-2 days | Phase B mocks |
| Phase G | Input module tests (4 tests) | 1 day | Phase B mocks |
| Phase H | Roslyn Analyzers WCG002, WCG003 | 2 days | Phase A experience |

**Total estimated effort:** 10-15 days for complete framework.
**Minimum viable validation:** Phases A + C (3-5 days) — catches all 7 ArrayPool bugs and validates all 100+ class profiles.

---

*End of Deliverable 3*
