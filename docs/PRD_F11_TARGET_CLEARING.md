# PRD: F11 Macro-Based Aggressive Target Clearing

**Date:** 2026-02-06  
**Status:** Implementation-Ready  
**Priority:** P0 (Critical — Complete Operational Deadlock)  
**Estimated Effort:** 6-8 hours

---

## Executive Summary

The bot suffers from a critical target management failure causing complete operational deadlock. When a target cannot be cleared through native mechanisms (`PressClearTarget` using `Alt-Insert` via BindPad/CUSTOM_CLEARTARGET binding), the bot becomes permanently stuck — especially on blacklisted, unreachable, or evading entities. A workaround exists: a `/cleartarget` macro bound to F11 on action bar slot 71 (`MULTIACTIONBAR1BUTTON11`). This PRD specifies modifications to make F11 the **primary** target-clearing mechanism, add aggressive stuck-on-target detection, and eliminate all blocking conditions that prevent recovery.

---

## Root Cause Analysis

### Why `PressClearTarget` Fails

The current `ClearTarget` keybind chain is:

1. `ClassConfigurationBaseActions.ClearTarget` → `BindingID.CUSTOM_CLEARTARGET` → default `Alt-Insert`
2. This requires **BindPad addon** to create a secure macro button that executes `/cleartarget`
3. BindPad must be installed, configured, and the binding must resolve at runtime

**Failure modes identified in code:**
- `ConfigurableInput.PressClearTarget()` ([ConfigurableInput.cs](Core/Input/ConfigurableInput.cs#L216-L227)): If `ClearTarget.ConsoleKey == ConsoleKey.NoName`, the method returns immediately with an error log — **silent failure**, target persists
- The `Alt-Insert` modifier key combination may fail if WoW doesn't have proper focus or the modifier isn't held long enough
- BindPad addon may not have set up the macro correctly for the current character/session

### Why the Bot Deadlocks

The GOAP planner evaluates world state every tick ([GoapAgent.cs](Core/GOAP/GoapAgent.cs#L297-L340)). Key state bits:
- `GoapKey.hastarget` — set when `bits.Target()` is true
- `GoapKey.targetisalive` — set when target exists AND not dead

When `PressClearTarget` fails:
1. **BlacklistTargetGoal** ([BlacklistTargetGoal.cs](Core/Goals/BlacklistTargetGoal.cs)): `CanRun()` returns true (has blacklisted target), `OnEnter()` calls `PressClearTarget()` → fails → target persists → GOAP re-selects this goal → infinite loop
2. **FollowRouteGoal** ([FollowRouteGoal.cs](Core/Goals/FollowRouteGoal.cs#L241-L268)): Has a 3-tier fallback (PressClearTarget → ESC → /cleartarget via ExecGameCommand), but if all fail, it logs a warning and continues — the target still blocks subsequent goals
3. **PullTargetGoal** ([PullTargetGoal.cs](Core/Goals/PullTargetGoal.cs#L152)): On evade/timeout, calls `PressClearTarget()` — no fallback → deadlock
4. **CombatGoal** ([CombatGoal.cs](Core/Goals/CombatGoal.cs#L217-L275)): When target dies or disappears, calls `PressClearTarget()` — no fallback → blocks corpse looting

### The F11 Workaround

F11 is bound to `MULTIACTIONBAR1BUTTON11` (action bar slot 71) in `KeyBindingDefaults.cs`. The user has placed a `/cleartarget` macro in this slot. This is a direct game keybind that:
- Does NOT require BindPad
- Does NOT require modifier keys  
- Is a simple single-key press
- Works reliably through the WoW secure action system

---

## Functional Requirements

| ID | Requirement | Priority | Implementation Notes |
|----|-------------|----------|---------------------|
| FR-1 | Add `PressF11ClearTarget()` method to `ConfigurableInput` that sends F11 keypress | P0 | Direct `ConsoleKey.F11` press, no modifier, no binding resolution |
| FR-2 | Modify `PressClearTarget()` to use F11 as primary mechanism | P0 | Press F11 first, then fall back to existing `ClearTarget` binding |
| FR-3 | Add `ForceAggressiveClearTarget()` method that triple-presses F11 with verification | P0 | Press F11 → wait → check `bits.Target()` → repeat up to 3x → ESC fallback → `/cleartarget` slash command |
| FR-4 | Replace all bare `PressClearTarget()` calls in deadlock-prone paths with `ForceAggressiveClearTarget()` | P0 | BlacklistTargetGoal, PullTargetGoal, CombatGoal, FollowRouteGoal |
| FR-5 | Add stuck-on-target timeout detection to `BlacklistTargetGoal` | P0 | If target persists after OnEnter(), escalate to ForceAggressiveClearTarget + ESC + turn away |
| FR-6 | Add target-stuck watchdog timer to `GoapAgent` | P1 | Track how long current target GUID has been held; if >5s with blacklisted/unreachable, force clear |
| FR-7 | Ensure bot resumes normal operation after forced clear | P0 | Reset combat state, clear stuck detector, re-enter GOAP planning |

## Non-Functional Requirements

| ID | Requirement | Target | Validation Method |
|----|-------------|--------|------------------|
| NFR-1 | F11 press must complete within 50ms | <50ms | KeyPress uses `InputDuration.FastPress` (30ms) |
| NFR-2 | No new allocations in hot paths | 0 bytes | Use existing `Span<T>` patterns, no string interpolation in non-debug |
| NFR-3 | Backward compatible with existing configs | 100% | F11 is additive; existing ClearTarget binding still works as fallback |
| NFR-4 | Solution build passes | 0 errors | `dotnet build MasterOfPuppets.sln` |

---

## Technical Specifications

### Architecture: Target Clearing Cascade

```
ForceAggressiveClearTarget()
├── Step 1: PressF11ClearTarget() → wait → check bits.Target()
├── Step 2: PressF11ClearTarget() → wait → check bits.Target()  
├── Step 3: PressF11ClearTarget() → wait → check bits.Target()
├── Step 4: PressESC() → wait → check bits.Target()
├── Step 5: PressClearTarget() [original Alt-Insert] → wait → check bits.Target()
├── Step 6: ExecGameCommand("/cleartarget") → wait → check bits.Target()
└── Step 7: Log critical error, trigger ScreenCapture, send AbortEvent
```

### Affected Files Summary

| File | Changes |
|------|---------|
| `Core/Input/ConfigurableInput.cs` | Add `PressF11ClearTarget()`, add `ForceAggressiveClearTarget()`, modify `PressClearTarget()` |
| `Core/Goals/BlacklistTargetGoal.cs` | Use `ForceAggressiveClearTarget()`, add timeout/retry logic |
| `Core/Goals/CombatGoal.cs` | Replace bare `PressClearTarget()` with `ForceAggressiveClearTarget()` |
| `Core/Goals/PullTargetGoal.cs` | Replace bare `PressClearTarget()` with `ForceAggressiveClearTarget()` |
| `Core/Goals/FollowRouteGoal.cs` | Replace multi-step fallback with `ForceAggressiveClearTarget()` |
| `Core/Goals/LootGoal.cs` | Use `ForceAggressiveClearTarget()` in `ClearTargetIfNeeded()` |
| `Core/Goals/SkinningGoal.cs` | Use `ForceAggressiveClearTarget()` in `ClearTargetIfExists()` |
| `Core/Goals/FleeGoal.cs` | Use `ForceAggressiveClearTarget()` |
| `Core/GoalsComponent/CombatTracker.cs` | Use `ForceAggressiveClearTarget()` |
| `Core/GoalsComponent/ReactCastError.cs` | Use `ForceAggressiveClearTarget()` for LOS error |

---

## Proven Patterns Applied

### Pattern 1: Direct F11 Key Press  
**Source:** `WowProcessInput.PressRandom(ConsoleKey, int, CancellationToken)` — proven in `ExecGameCommand.Run()`, `PressESC()`, and many other direct key presses throughout the codebase.  
**Implementation:** `input.PressRandom(ConsoleKey.F11, InputDuration.FastPress, token)` — bypasses all binding resolution, modifier keys, and BindPad dependencies.

### Pattern 2: Verify-After-Press  
**Source:** `SkinningGoal.ClearTargetIfExists()` ([SkinningGoal.cs](Core/Goals/SkinningGoal.cs#L332-L370)) and `FollowRouteGoal.Update()` ([FollowRouteGoal.cs](Core/Goals/FollowRouteGoal.cs#L243-L268))  
**Implementation:** After each press, call `wait.Update()` then check `bits.Target()`. This pattern is used throughout the codebase to verify game state changed after input.

### Pattern 3: Escalating Fallback Chain  
**Source:** `StuckDetector.EscalateUnstuckState()` ([StuckDetector.cs](Core/GoalsComponent/StuckDetector.cs#L278-L298))  
**Implementation:** Similar escalation model: try simplest fix first (F11), then progressively more aggressive (ESC, Alt-Insert, /cleartarget slash command).

---

## Implementation Tasks

## Phase 1: Core Input Methods (Est. 1.5 hours)

### Task 1.1: Add `PressF11ClearTarget()` to `ConfigurableInput`
**File:** `Core/Input/ConfigurableInput.cs`  
**Location:** After `PressClearTarget()` method (line ~227)

```csharp
    public void PressF11ClearTarget(CancellationToken token = default)
    {
        logger.LogDebug("[PressClearTarget ] F11 macro key pressed");
        input.PressRandom(ConsoleKey.F11, InputDuration.FastPress, token);
    }
```

**Acceptance Criteria:**
- [x] Method exists and compiles
- [x] Sends `ConsoleKey.F11` with `FastPress` duration
- [x] No modifier key dependency

---

### Task 1.2: Add `ForceAggressiveClearTarget()` to `ConfigurableInput`
**File:** `Core/Input/ConfigurableInput.cs`  
**Dependencies:** Requires `Wait` and `AddonBits` to be available. Since `ConfigurableInput` doesn't currently hold these, this method should be added as a **standalone utility** or placed on a new helper. However, examining the codebase pattern, goals already have access to `wait` and `bits`. Therefore, place this as a **static helper method** or as a method on `ConfigurableInput` that accepts `wait` and `bits` as parameters.

**Best approach:** Add the method to `ConfigurableInput` with parameters:

```csharp
    /// <summary>
    /// Aggressively clears the current target using F11 macro as primary mechanism,
    /// with escalating fallbacks. Returns true if target was successfully cleared.
    /// </summary>
    public bool ForceAggressiveClearTarget(Wait wait, AddonBits bits, ExecGameCommand? execGameCommand = null, CancellationToken token = default)
    {
        // Step 1-3: F11 macro (primary, most reliable)
        for (int attempt = 0; attempt < 3; attempt++)
        {
            PressF11ClearTarget(token);
            wait.Update();
            if (!bits.Target())
            {
                logger.LogInformation("[ClearTarget      ] Cleared via F11 (attempt {Attempt})", attempt + 1);
                return true;
            }
        }

        // Step 4: ESC key
        PressESC(token);
        wait.Update();
        if (!bits.Target())
        {
            logger.LogInformation("[ClearTarget      ] Cleared via ESC");
            return true;
        }

        // Step 5: Original ClearTarget binding (Alt-Insert)
        PressClearTarget(token);
        wait.Update();
        if (!bits.Target())
        {
            logger.LogInformation("[ClearTarget      ] Cleared via ClearTarget binding");
            return true;
        }

        // Step 6: Slash command fallback
        if (execGameCommand != null)
        {
            execGameCommand.Run("/cleartarget", logMessage: null);
            wait.Update();
            if (!bits.Target())
            {
                logger.LogInformation("[ClearTarget      ] Cleared via /cleartarget command");
                return true;
            }
        }

        // Step 7: All methods failed
        logger.LogError("[ClearTarget      ] FAILED: All target clearing methods exhausted! Target persists.");
        return false;
    }
```

**Acceptance Criteria:**
- [x] Method cascades through F11 → ESC → Alt-Insert → /cleartarget
- [x] Returns `true`/`false` indicating success
- [x] Logs which method succeeded
- [x] Logs critical error if all fail

---

### Task 1.3: Modify `PressClearTarget()` to use F11 as primary
**File:** `Core/Input/ConfigurableInput.cs`  
**Current code (lines 216-227):**

```csharp
    public void PressClearTarget(CancellationToken token = default)
    {
        // Debug logging to troubleshoot binding issues
        if (ClearTarget.ConsoleKey == ConsoleKey.NoName)
        {
            logger.LogError($"[PressClearTarget] FAILED: ClearTarget.ConsoleKey is NoName! Binding not resolved. Key='{ClearTarget.Key}', BindingID={ClearTarget.BindingID}");
            return;
        }

        logger.LogDebug($"[PressClearTarget] Pressing {ClearTarget.ConsoleKey} (Key='{ClearTarget.Key}', BindingID={ClearTarget.BindingID})");
        PressRandom(ClearTarget, token);
    }
```

**New code:**

```csharp
    public void PressClearTarget(CancellationToken token = default)
    {
        // Primary: F11 macro keybind (most reliable, no BindPad dependency)
        PressF11ClearTarget(token);

        // Fallback: original ClearTarget binding (Alt-Insert via BindPad)
        if (ClearTarget.ConsoleKey != ConsoleKey.NoName)
        {
            PressRandom(ClearTarget, token);
        }
    }
```

**Acceptance Criteria:**
- [x] F11 is pressed first on every `PressClearTarget()` call
- [x] Original binding still fires as backup (if resolved)
- [x] No `return` on `NoName` — F11 still fires even if binding is broken

---

## Phase 2: Fix Deadlock-Prone Goals (Est. 3 hours)

### Task 2.1: Fix `BlacklistTargetGoal` — Most Critical
**File:** `Core/Goals/BlacklistTargetGoal.cs`  
**Problem:** `OnEnter()` calls `PressClearTarget()` once and exits. If it fails, GOAP re-selects this goal infinitely.

**Current code (lines 32-43):**

```csharp
    public override void OnEnter()
    {
        if (playerReader.PetTarget() ||
            playerReader.IsCasting() ||
            bits.Any_AutoAttack())
        {
            input.PressStopAttack();
        }

        input.PressClearTarget();
        wait.Update();
    }
```

**New code — add ExecGameCommand dependency and aggressive clearing:**

The constructor needs `ExecGameCommand` added. Full replacement:

```csharp
public sealed class BlacklistTargetGoal : GoapGoal
{
    public override float Cost => 2;

    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly ConfigurableInput input;
    private readonly Wait wait;
    private readonly IBlacklist targetBlacklist;
    private readonly ExecGameCommand execGameCommand;

    public BlacklistTargetGoal(PlayerReader playerReader,
        AddonBits bits,
        ConfigurableInput input,
        IBlacklist blacklist,
        Wait wait,
        ExecGameCommand execGameCommand)
        : base(nameof(BlacklistTargetGoal))
    {
        this.playerReader = playerReader;
        this.bits = bits;
        this.input = input;
        this.targetBlacklist = blacklist;
        this.wait = wait;
        this.execGameCommand = execGameCommand;
    }

    public override bool CanRun()
    {
        return bits.Target() && targetBlacklist.Is();
    }

    public override void OnEnter()
    {
        if (playerReader.PetTarget() ||
            playerReader.IsCasting() ||
            bits.Any_AutoAttack())
        {
            input.PressStopAttack();
            wait.Update();
        }

        // Aggressive F11-based target clearing to prevent deadlock
        if (!input.ForceAggressiveClearTarget(wait, bits, execGameCommand))
        {
            // Final emergency: turn away to break facing and prevent re-engagement
            input.TurnRandomDir(500);
            wait.Update();
        }
    }
}
```

**DI Registration Impact:** `ExecGameCommand` is already registered as a singleton ([DependencyInjection.cs](Core/DependencyInjection.cs#L314)). The `BlacklistTargetGoal` is created by `GoalFactory` — need to verify it can resolve `ExecGameCommand`.

**Verification:** Search for `BlacklistTargetGoal` construction in `GoalFactory`.

---

### Task 2.2: Fix `CombatGoal` Target Clearing
**File:** `Core/Goals/CombatGoal.cs`  
**Problem:** Lines 211-228 call `PressClearTarget()` without fallback when target dies or disappears.

**Changes needed at lines 211-228 and 240-245, 270-275:**

Replace all bare `input.PressClearTarget()` calls in this file with `input.ForceAggressiveClearTarget(wait, bits)`. The `CombatGoal` doesn't have `ExecGameCommand` — add it as a constructor parameter.

**Constructor addition:**
```csharp
    private readonly ExecGameCommand execGameCommand;
```
Add `ExecGameCommand execGameCommand` parameter, assign in constructor.

**Replace pattern (4 locations):**

| Line | Current | Replacement |
|------|---------|-------------|
| ~217 | `input.PressClearTarget();` | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |
| ~228 | `input.PressClearTarget();` | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |
| ~244 | `input.PressClearTarget();` | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |
| ~275 | `input.PressClearTarget();` | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |

---

### Task 2.3: Fix `PullTargetGoal` Target Clearing
**File:** `Core/Goals/PullTargetGoal.cs`  
**Problem:** Lines 152, 201, 212 call bare `PressClearTarget()`.

**Constructor:** Already has many dependencies. Add `ExecGameCommand execGameCommand`.

**Replace pattern (3 locations):**

| Line | Context | Replacement |
|------|---------|-------------|
| ~152 | Pull timeout | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |
| ~201 | Pull prevention | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |
| ~212 | Evading mob | `input.ForceAggressiveClearTarget(wait, bits, execGameCommand);` |

---

### Task 2.4: Simplify `FollowRouteGoal` Target Clearing
**File:** `Core/Goals/FollowRouteGoal.cs`  
**Problem:** Lines 241-268 have a manual 3-tier fallback. Replace with `ForceAggressiveClearTarget()`.

**Current code (lines 241-268):**
```csharp
        if (bits.Target() && bits.Target_Dead())
        {
            Log("Has target but its dead.");
            input.PressClearTarget();
            wait.Update();

            if (bits.Target())
            {
                input.PressESC();
                wait.Update();
                input.PressClearTarget();
                wait.Update();
                if (!bits.Target()) { return; }
                execGameCommand.Run("/cleartarget", logMessage: null);
                wait.Update();
                if (!bits.Target()) { return; }
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogWarning($"Unable to clear target! Check Bindpad settings!");
            }
        }
```

**New code:**
```csharp
        if (bits.Target() && bits.Target_Dead())
        {
            Log("Has target but its dead.");
            if (!input.ForceAggressiveClearTarget(wait, bits, execGameCommand))
            {
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogWarning("Unable to clear dead target after all attempts!");
            }
        }
```

**Also fix Thread_LookingForTarget (line ~311):**
```csharp
                    input.PressClearTarget();
```
→
```csharp  
                    input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
```

---

### Task 2.5: Fix `LootGoal.ClearTargetIfNeeded()`
**File:** `Core/Goals/LootGoal.cs`  
**Problem:** Lines 215-240 have manual fallback. Replace with `ForceAggressiveClearTarget()`.

Also fix all other bare `input.PressClearTarget()` occurrences at lines 375, 387, 398, 419.

---

### Task 2.6: Fix `SkinningGoal.ClearTargetIfExists()`
**File:** `Core/Goals/SkinningGoal.cs`  
**Problem:** Lines 332-370 have manual 3-tier fallback. Replace with `ForceAggressiveClearTarget()`.

---

### Task 2.7: Fix Remaining Goals (Completed)

**Status (2026-02-06):** Completed by migrating remaining deadlock-prone clear-target paths to aggressive clearing.

| File | Context | Status |
|------|---------|--------|
| `Core/Goals/FleeGoal.cs` | OnExit target clear | ✅ Updated |
| `Core/Goals/AssistFocusGoal.cs` | OnExit target clear | ✅ Updated |
| `Core/Goals/FollowFocusGoal.cs` | OnExit target clear | ✅ Updated |
| `Core/Goals/MailGoal.cs` | Resume target clear | ✅ Updated |
| `Core/Goals/TargetFocusTargetGoal.cs` | OnExit target clear | ✅ Updated |
| `Core/Goals/TargetPetTargetGoal.cs` | Dead/invalid target clear | ✅ Updated |
| `Core/GoalsComponent/CombatTracker.cs` | Acquire-target fallback clear | ✅ Updated |
| `Core/GoalsComponent/ReactCastError.cs` | LOS non-combat clear | ✅ Updated |
| `Core/Goals/SkinningGoal.cs` | Alive last-target clear fallback | ✅ Updated |

`ForceAggressiveClearTarget(wait, bits, execGameCommand?)` is now the default in these paths, with F11-first fallback cascade and success/failure logging.

---

## Phase 3: DI and GoalFactory Updates (Est. 1 hour)

### Task 3.1: Verify GoalFactory resolves ExecGameCommand

**File:** Find where `BlacklistTargetGoal`, `CombatGoal`, and `PullTargetGoal` are constructed.

```bash
grep -rn "BlacklistTargetGoal\|new CombatGoal\|new PullTargetGoal" Core/
```

Since these use constructor injection via DI, adding `ExecGameCommand` as a parameter should auto-resolve as long as it's registered (it is, in `DependencyInjection.cs` line 314).

**Verification:**
```bash
dotnet build MasterOfPuppets.sln
```

---

## Phase 4: Testing and Verification (Est. 1.5 hours)

### Task 4.1: Build Verification
```bash
dotnet build MasterOfPuppets.sln
```
**Acceptance:** Zero errors.

**Status (2026-02-06):** ✅ Passed (`dotnet build MasterOfPuppets.sln`, `dotnet test CoreUnitTests`, `dotnet test FrontendUnitTests`).

### Task 4.2: Runtime Verification Checklist
- [ ] Start BlazorServer
- [ ] Target a mob manually, verify F11 clears it
- [ ] Enable bot, verify it clears blacklisted targets within 1-2 seconds
- [ ] Verify bot doesn't get stuck on dead targets
- [ ] Verify bot resumes route after clearing target
- [ ] Check logs for `[ClearTarget      ]` messages showing which method succeeded

**Status (2026-02-06):** Deferred in this environment because no live WoW client/process was available. `dotnet run --project CoreTests` was attempted and failed while WoW was absent (`WowScreenDXGI` image initialization received width `0`), so live target-clear validation remains pending on a machine with WoW running.

### Task 4.3: Regression Check
- [ ] Normal combat cycle works (pull → fight → loot → move)
- [ ] Looting still clears dead targets
- [ ] Skinning still clears targets after gathering
- [ ] PullTarget timeout still works

**Status (2026-02-06):** Deferred with Task 4.2 because regression checks require an active WoW session.

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| F11 slot doesn't have `/cleartarget` macro | Medium | High | F11 is additive; original binding still fires as fallback in cascade |
| F11 key conflicts with WoW UI binding | Low | Medium | F11 defaults to `MULTIACTIONBAR1BUTTON11` which is the correct slot |
| ExecGameCommand DI resolution fails | Low | High | It's already registered as singleton; verify with build |
| Triple-press F11 triggers game throttle | Very Low | Low | `FastPress` duration (30ms) with `wait.Update()` gap (~100ms) between presses |
| Aggressive clearing interferes with combat | Low | Medium | `ForceAggressiveClearTarget()` is only called in explicit "need clear" paths, not during active combat targeting |

---

## Out of Scope

- Modifying the WoW addon (DataToColor/BindPad) — this is a C# bot-side fix only
- Changing the F11 keybind to a different key — user has already configured F11
- Adding new GOAP goals — we modify existing ones
- Addressing root cause of BindPad binding resolution failures — F11 bypasses this entirely
- Target clearing during active combat engagement — only for stuck/blacklisted/dead scenarios

---

## Rollback Procedure

All changes are additive. To rollback:
1. Revert `ConfigurableInput.cs` — remove `PressF11ClearTarget()` and `ForceAggressiveClearTarget()`
2. Revert `PressClearTarget()` to original implementation
3. Revert all goal files to use bare `input.PressClearTarget()`
4. Remove `ExecGameCommand` constructor parameter additions

Git rollback: `git revert HEAD` after single commit.

---

## Verification Commands

```bash
# Full build
dotnet build MasterOfPuppets.sln

# Run tests (if any affected)
dotnet test

# Run specific benchmarks
dotnet run --project Benchmarks -c Release -- --filter "*CombatRotation*"
```
