# Developer Handoff: Ranged Combat System Critical Failures

**Date**: 2026-03-02  
**Status**: UNRESOLVED — 3 critical issues blocking warlock leveling  
**Character**: Blood Elf Warlock, Level 6, TBC Classic Anniversary  
**Profile**: `Json/class/BloodElf_Warlock_1-70_TBC.json`  
**WoW PID**: 31540 (may change on restart)  
**Bot UI**: http://localhost:5000  
**PathingAPI**: http://localhost:5001  

---

## Upstream Reference Repository

> **CRITICAL**: This project (`Aycrith/WowNewCombo`, branch `dev`) is a heavily modified fork of the original **Xian55/WowClassicGrindBot**.
>
> **Upstream repo**: https://github.com/Xian55/WowClassicGrindBot/
>
> The upstream repo contains the **last known working implementations** of PullTargetGoal, CastingHandler, CombatGoal, and class profiles. When investigating issues below, **always compare the current fork's code against the upstream original** to understand what changed and what may have broken.

### Key Upstream Files for Comparison

| Component | Upstream Path | Fork Path | Notes |
|-----------|--------------|-----------|-------|
| PullTargetGoal | [`Core/Goals/PullTargetGoal.cs`](https://github.com/Xian55/WowClassicGrindBot/blob/main/Core/Goals/PullTargetGoal.cs) | `Core/Goals/PullTargetGoal.cs` | Upstream is 240 lines, fork is 358 — significant additions |
| CastingHandler | [`Core/Goals/CastingHandler.cs`](https://github.com/Xian55/WowClassicGrindBot/blob/main/Core/Goals/CastingHandler.cs) | `Core/GoalsComponent/CastingHandler.cs` | Moved to different folder; 645→994 lines. Major refactor |
| StopMoving | `Core/Goals/StopMoving.cs` | `Core/GoalsComponent/StopMoving.cs` | Path changed |
| Warlock Profiles | [`Json/class/Warlock_1.json`](https://raw.githubusercontent.com/Xian55/WowClassicGrindBot/main/Json/class/Warlock_1.json) through `Warlock_20.json` | `Json/class/BloodElf_Warlock_1-70_TBC.json` | Upstream uses level-tiered profiles; fork uses one mega-profile |
| CombatGoal | `Core/Goals/CombatGoal.cs` | `Core/Goals/CombatGoal.cs` | Fork added RotationOptimizer, BehaviorTree, multi-target |
| ApproachTargetGoal | `Core/Goals/ApproachTargetGoal.cs` | `Core/Goals/ApproachTargetGoal.cs` | Fork added multi-mob detection, gear checks |
| ReactCastError | (inline in CastingHandler) | `Core/GoalsComponent/ReactCastError.cs` | Was inline in upstream; extracted to separate class in fork |

### Critical Upstream vs Fork Differences

#### 1. PullTargetGoal — Upstream Was Simpler and Worked

**Upstream `Pull()` method** (working):
```csharp
// Simple: iterate, cast, accumulate success, then wait for aggro
bool castAny = false;
foreach (var item in Keys)
{
    var success = castingHandler.CastIfReady(item, item.DelayBeforeCast);
    if (success)
    {
        castAny = true;
        if (item.WaitForWithinMeleeRange)
            WaitForWithinMeleeRange(item, success);
    }
}
if (castAny)
{
    wait.Until(1000, () =>
        playerReader.TargetTarget == TargetIsTargettingMe ||
        playerReader.TargetTarget == TargetIsTargettingPet);
}
return playerReader.Bits.PlayerInCombat;
```

**Fork's `Update()` method** (broken): Added `PullPrevention()`, `InterruptWatchdog`, `consecutiveRangedPullFailures`, `CombatTracker`, `targetBlacklist` — the added complexity introduced multiple bugs. The upstream had **no interrupt watchdog for pull casts** — it used `item.DelayBeforeCast` (a simple `Thread.Sleep`) rather than a cancellable token.

#### 2. CastingHandler — Upstream Used Thread.Sleep, Fork Uses CancellationToken

**Upstream `Cast()` signature**: `Cast(KeyAction item, int sleepBeforeCast)`
- `sleepBeforeCast > 0`: Calls `stopMoving.Stop()`, `wait.Update(1)`, `stopMoving.Stop()`, `wait.Update(1)`, then `Thread.Sleep(sleepBeforeCast)` — **blocking, non-cancellable, reliable stop**
- `CastCastbar()` calls `stopMoving.Stop(); wait.Update(1);` **before** pressing the key

**Fork `Cast()` signature**: `Cast(KeyAction item, Func<bool> interrupt)`
- Creates `CancellationToken` from `InterruptWatchdog.Set(interrupt)` for `HasCastBar` spells
- `PreparedForCast()` uses `BeforeCastDelay` with `wait.Until(delay, token)` — **cancellable, and the token IS getting cancelled prematurely**
- The token cancellation causes `BeforeCastDelay` to exit in ~13ms instead of 400ms

#### 3. Warlock Profiles — Upstream Used Level-Tiered Separate Files

**Upstream `Warlock_4.json`** (level 4-7 bracket):
```json
{
  "Pull": {
    "Sequence": [
      { "Name": "Immolate", "Key": "5", "HasCastBar": true, "MinMana": 25 }
    ]
  },
  "Combat": {
    "Sequence": [
      { "Name": "Immolate", "Key": "5", "HasCastBar": true,
        "Requirements": ["TargetHealth% > DOT_MIN_HEALTH%", "!Immolate"], "MinMana": 25 },
      { "Name": "Corruption", "Key": "7", "HasCastBar": true,
        "Requirements": ["TargetHealth% > DOT_MIN_HEALTH%", "!Corruption"], "MinMana": 25 },
      { "Name": "Shadow Bolt", "Key": "2", "HasCastBar": true, "MinMana": 25, "Cooldown": 8000 },
      { "Name": "AutoAttack", "Requirement": "!AutoAttacking" },
      { "Name": "Approach", "Log": false }
    ]
  }
}
```

**Key upstream observations:**
- **Corruption has `"HasCastBar": true`** — at level 4 without Improved Corruption, it HAS a cast bar. The fork's profile is MISSING this flag
- **Shadow Bolt has `"Cooldown": 8000`** in combat (not 500) — this forces wand/melee as filler, not repeated Shadow Bolt spam
- **`AutoAttack`** and **`Approach`** are in the Combat sequence — the fork is missing `AutoAttack` from combat
- Pull uses a single cast (Immolate) — simple, reliable
- **`MinMana`** is used instead of `WhenUsable` + `Cooldown` combos

### How to Compare

```bash
# Clone or browse upstream for reference:
git remote add upstream https://github.com/Xian55/WowClassicGrindBot.git
git fetch upstream main

# Compare specific files:
git diff upstream/main -- Core/Goals/PullTargetGoal.cs
git diff upstream/main -- Core/Goals/CombatGoal.cs

# Or browse directly:
# https://github.com/Xian55/WowClassicGrindBot/blob/main/Core/Goals/PullTargetGoal.cs
# https://github.com/Xian55/WowClassicGrindBot/blob/main/Core/Goals/CastingHandler.cs
# https://raw.githubusercontent.com/Xian55/WowClassicGrindBot/main/Json/class/Warlock_4.json
```

> **WARNING**: The upstream and fork have diverged massively (the upstream was last updated ~4 years ago, the fork is on .NET 10 with C# 14). You cannot cherry-pick upstream code directly. Use it as a **behavioral reference** for how the working system operated.

---

## Issue 1: Melee Engagement Despite Being a Ranged Class

### Problem Statement

The warlock walks into melee range of targets instead of stopping at max spell range (~30 yards) to cast. After the GOAP planner transitions from `ApproachTargetGoal` → `PullTargetGoal`, the character's click-to-move momentum from approach is not cancelled, and when pull casts fail, the `DefaultApproach()` fallback sends the character directly to the mob via `PressApproach()`.

### Observed Behavior (from logs)

```
New Plan = Approach Target → Pull Target → Combat
[ApproachTarget] PressApproach → click-to-move begins
[incombatrange = true] → transition to PullTarget
[PullTarget.OnEnter] StopForward() → but no forward KEY is held, click-to-move continues
Cast Shadow Bolt → SPELL_FAILED_MOVING (character still sliding from click-to-move)
Cast Shadow Bolt → SPELL_FAILED_MOVING
Cast Shadow Bolt → SPELL_FAILED_MOVING
Cast Shadow Bolt → SPELL_FAILED_MOVING (4 failures)
"Ranged pull failed 4x; refusing body-pull and clearing target"
→ OR: DefaultApproach() fires → PressApproach() → walks to melee
```

### Root Cause Analysis

**Primary**: `StopMoving.StopForward()` (lines 46-64 of `Core/GoalsComponent/StopMoving.cs`) only works when the forward/backward key is actively held down. When the character is moving via click-to-move (initiated by `PressApproach()` / `PressInteract()`), the `else` branch does a 2-5ms forward key tap — which is **too brief to cancel click-to-move momentum**.

```csharp
// StopMoving.cs lines 46-64
public void StopForward()
{
    if (!bits.Moving()) return;
    if (input.IsKeyDown(input.ForwardKey))
        input.SetKeyState(input.ForwardKey, false, true);
    else if (input.IsKeyDown(input.BackwardKey))
        input.SetKeyState(input.BackwardKey, false, true);
    else // moving by interact key — THIS IS THE PROBLEM
        input.PressFixed(input.ForwardKey, Random.Shared.Next(2, 5), token);
}
```

**Secondary**: `PullTargetGoal.DefaultApproach()` (lines 299-309) uses `input.PressApproach()` which is an interact key that click-to-moves toward the target. If no ranged spell successfully casts, the approach fallback engages and walks straight to melee.

**Upstream comparison**: The upstream `PullTargetGoal.OnEnter()` called `input.TapApproachKey()` then `stopMoving.Stop()` then `wait.Update(1)` — a face+stop sequence. The upstream `StopMoving.Stop()` was simpler and the character wasn't using click-to-move the same way.

### Prior Fix Attempts

1. Added `bits.Combat()` to approach guard (line ~296) — partially helps but doesn't fix the initial stop failure
2. `PullTargetGoal.OnEnter()` already tries `StopForward()` (line ~120) — but it's the `else` branch that fails

### Investigation Plan

1. Compare upstream `StopMoving.cs` with fork — the upstream file is at `Core/Goals/StopMoving.cs` (404 on GoalsComponent path, try Goals path)
2. Test whether a longer forward key press (50-100ms) reliably cancels click-to-move
3. Check if `input.PressStopAttack()` or `input.PressESC()` cancels click-to-move
4. Evaluate whether `ApproachTargetGoal.OnExit()` (line 92: `input.StopForward(false)`) is sufficient
5. Check if `withInPullRange` GOAP precondition fires at the correct range for ranged classes

### Solution Strategy

**Option A (Minimal):** Make `StopMoving.StopForward()` more aggressive for the interact/click-to-move case — increase the forward key hold to 50-100ms and add a `wait.Until()` to verify `!bits.Moving()` afterward.

**Option B (Structural):** Add an explicit stop-and-verify loop in `PullTargetGoal.OnEnter()`:
```csharp
// Proposed: ensure character is actually stopped before any cast attempt
stopMoving.Stop();
wait.Until(500, () => !bits.Moving());
```

**Option C (From Upstream Pattern):** Reintroduce the upstream's `Thread.Sleep(sleepBeforeCast)` pattern (or equivalent `wait.Fixed()`) that blocks without a cancellable token, ensuring the delay actually completes.

### Acceptance Criteria

- [ ] Character stops at 25-35 yards from target (within pull range, NOT melee)
- [ ] `SPELL_FAILED_MOVING` occurs max 1 time after initial approach (retry succeeds)
- [ ] Character NEVER walks to melee range unless all ranged options are exhausted AND profile explicitly includes Approach
- [ ] `DefaultApproach()` is not called when ranged spells are available but failing due to movement

---

## Issue 2: Range Pull Failure (SPELL_FAILED_MOVING Loop)

### Problem Statement

Every pull attempt results in an unbroken loop of `SPELL_FAILED_MOVING` errors. The character never successfully stops moving before the first cast, and the `ReactCastError` handler for `SPELL_FAILED_MOVING` calls `stopMoving.Stop()` which suffers from the same inadequacy described in Issue 1.

### Observed Behavior (from logs)

```
PullTarget: OnEnter
  > StopForward() — completes, but click-to-move continues
  > Cast Immolate
    > PreparedForCast: BeforeCastFaceTarget → PressFastInteract() (face target)
    > BeforeCastDelay 400ms → stopMoving.Stop() called
    > wait.Until(400, token) → exits in 13ms (token cancelled!)
    > CastCastbar: PressKey → SPELL_FAILED_MOVING
    > React to SPELL_FAILED_MOVING — stopMoving.Stop() (2-5ms tap, insufficient)
  > Cast Shadow Bolt → same SPELL_FAILED_MOVING
  > Cast Shadow Bolt → same
  > Cast Shadow Bolt → same
  > "Ranged pull failed 4x"
```

### Root Cause Analysis

**There are THREE compounding failures:**

#### Failure 1: BeforeCastDelay Token Cancellation

`CastingHandler.PreparedForCast()` (line 519-533 of `CastingHandler.cs`):
```csharp
if (item.BeforeCastDelay > 0)
{
    if (!playerReader.IsCasting() && bits.Moving() && (item.BeforeCastStop || item.HasCastBar))
    {
        stopMoving.Stop();          // 1. Stop attempt (2-5ms tap, fails)
        wait.Update(token);         // 2. Single frame update
    }
    int delay = Random.Shared.Next(item.BeforeCastDelay, item.BeforeCastMaxDelay);
    wait.Until(delay, token);       // 3. Wait 400ms — BUT token is already cancelled!
}
```

The `token` comes from `InterruptWatchdog.Set(interrupt)`. The interrupt function from PullTargetGoal is:
```csharp
bool interrupt() => keyAction.CanBeInterrupted() || PullPrevention();
```

`PullPrevention()` was inverted (returning `true` always) and was fixed, BUT `keyAction.CanBeInterrupted()` may also return `true` under certain conditions. If the interrupt function's return value changes between when the watchdog captures `initialValue` and the next watchdog tick, the token is cancelled.

**Upstream comparison**: The upstream used `Thread.Sleep(sleepBeforeCast)` — a **non-cancellable** blocking sleep. The fork replaced this with `wait.Until(delay, token)` which is cancellable and therefore fragile.

#### Failure 2: StopMoving Inadequacy (Same as Issue 1)

The `stopMoving.Stop()` inside `PreparedForCast()` doesn't actually stop click-to-move. The character is still sliding when the key press fires.

#### Failure 3: PullPrevention() Logic (Previously Fixed but Verify)

`PullPrevention()` was inverted — caused `interrupt()` to always return `true` on first evaluation → watchdog captured `initialValue = true` → if PullPrevention flipped to `false` on next tick, token cancelled. This was fixed to:
```csharp
private bool PullPrevention()
{
    return targetBlacklist.Is() ||
        playerReader.TargetTarget is not
        (UnitsTarget.None or UnitsTarget.Me or UnitsTarget.Pet or UnitsTarget.PartyOrPet);
}
```
**Verify this fix is still in place and working correctly.**

### Prior Fix Attempts

1. **PullPrevention() inversion fix** — Changed `!targetBlacklist.Is()` → `targetBlacklist.Is()` and `is` → `is not`
2. **BeforeCastDelay: 400** added to Immolate and Shadow Bolt in pull sequence
3. **DoT Cooldowns**: Changed from 0 to 1500ms to prevent re-cast spam
4. **ReactCastError SPELL_FAILED_MOVING handler** already calls `stopMoving.Stop()` + `wait.Update()` — but same StopMoving inadequacy

### Investigation Plan

1. **Verify PullPrevention fix is deployed**: Check the built binary log output for "Preventing pulling possible tagged target" — if this still appears, the fix didn't take effect
2. **Test InterruptWatchdog behavior**: Add temporary logging to `CastingHandlerInterruptWatchdog.cs` (set `Log = true` on line 15) and rebuild — this will show when and why the token gets cancelled
3. **Compare `wait.Until()` behavior**: Check `Core/GoalsComponent/Wait.cs` lines 103-114 — the `Until(int, CancellationToken)` method exits when `token.IsCancellationRequested`. Determine if the token is cancelled before or after the `stopMoving.Stop()` call
4. **Test without InterruptWatchdog for pull**: As an experiment, try passing `CancellationToken.None` for pull casts to see if the fundamentals work without the watchdog
5. **Check upstream CastCastbar**: In upstream, `CastCastbar` calls `stopMoving.Stop(); wait.Update(1);` before pressing the key — **the stop happens unconditionally, not gated by a token**

### Solution Strategy

**Option A (Bypass Watchdog for Pull):** In `PullTargetGoal.Update()`, call `castingHandler.Cast()` without the interrupt watchdog by using a non-cancellable approach:
```csharp
// Instead of: bool interrupt() => keyAction.CanBeInterrupted() || PullPrevention();
// Use a simpler approach that doesn't create a cancellable token for pull casts
```

**Option B (Fix StopMoving + Add Verified Stop):** 
1. Make `StopMoving.StopForward()` wait for `!bits.Moving()` with a timeout
2. In `PreparedForCast`, add a verified stop loop before the delay timer

**Option C (Replicate Upstream Pattern):**
Replace the `wait.Until(delay, token)` in `PreparedForCast` with a non-cancellable `wait.Fixed(delay)` or `Thread.Sleep(delay)` for the `BeforeCastDelay` portion specifically. The upstream used `Thread.Sleep(sleepBeforeCast)` and it worked.

**Option D (Comprehensive):** Combine B + C:
1. Fix `StopMoving` to reliably cancel click-to-move
2. Use non-cancellable delay for `BeforeCastDelay`
3. Add a `!bits.Moving()` gate before `PressKeyAction` in `CastCastbar`

### Acceptance Criteria

- [ ] `BeforeCastDelay` waits the full 400ms (not 13ms)
- [ ] `SPELL_FAILED_MOVING` loop never exceeds 2 iterations (1 retry max)
- [ ] First pull cast succeeds at least 80% of the time
- [ ] Log shows `stopMoving.Stop()` call followed by actual stop (verify with `bits.Moving() == false`)
- [ ] Immolate / Shadow Bolt cast bar completes successfully from a dead stop

---

## Issue 3: Combat Rotation Priority Deficiency

### Problem Statement

The combat rotation for a low-level warlock (level 6) is suboptimal: it spams Shadow Bolt (high mana cost, long cast) instead of maintaining Corruption uptime and using Wand (Shoot) as the primary filler. Additionally, Corruption in the profile LACKS the `HasCastBar: true` flag, causing CastingHandler to treat it as instant when it actually has a 2-second cast bar at this level (Improved Corruption talent not yet available).

### Observed Behavior

- Combat rotation casts Shadow Bolt repeatedly until OOM
- Corruption is attempted but fails silently (treated as instant, but actually has cast bar → SPELL_FAILED_MOVING or cast never registers)
- Wand (Shoot) is last priority, only fires when Mana% < 30 in pull, and as dead-last filler in combat
- Mana runs out quickly → character has to eat/drink frequently → slow leveling

### Root Cause Analysis

#### Sub-Issue 3A: Corruption Missing HasCastBar Flag

**Current fork profile** (Combat section):
```json
{
    "Name": "Corruption",
    "Key": "4",
    "WhenUsable": true,
    "BeforeCastFaceTarget": true,
    "Requirement": "!Corruption",
    "Cooldown": 200
}
```

**Upstream Warlock_4.json** (working):
```json
{
    "Name": "Corruption",
    "Key": "7",
    "HasCastBar": true,
    "Requirements": ["TargetHealth% > DOT_MIN_HEALTH%", "!Corruption"],
    "MinMana": 25
}
```

**Key difference**: Upstream has `"HasCastBar": true`. At levels 1-19 (before Improved Corruption talent at level 20+), Corruption has a 2-second cast time. Without `HasCastBar`, CastingHandler's `CastInstant()` path is taken, which does NOT call `stopMoving.Stop()` first. The cast then fails with `SPELL_FAILED_MOVING` and the failure may not be properly detected since instant cast validation differs from castbar validation.

**User note**: The user said "corruption is not instant in classic wow until it is spec'd into - I disabled it for the time being" — but it's still active in the profile (not disabled). The user may have disabled it in-game but forgotten to remove or flag it in JSON.

#### Sub-Issue 3B: Shadow Bolt Overuse as Filler

**Current fork**: Shadow Bolt has `"Cooldown": 500` in combat — fires every 500ms after cast. At level 6, Shadow Bolt costs 25 mana with a 2.5s cast time. With base mana pool ~150, you get about 6 Shadow Bolts before OOM.

**Upstream Warlock_4.json**: Shadow Bolt has `"Cooldown": 8000` — only casts once per 8 seconds, forcing wand usage between casts. This is dramatically more mana-efficient.

#### Sub-Issue 3C: Missing AutoAttack and Approach in Combat

**Upstream combat sequence** includes:
```json
{ "Name": "AutoAttack", "Requirement": "!AutoAttacking" },
{ "Name": "Approach", "Log": false }
```

**Fork combat sequence** is missing both. This means:
- If target runs (feared, fleeing at low health), bot doesn't chase
- Auto-attack doesn't engage as melee fallback if spells fail

#### Sub-Issue 3D: Wand Priority Too Low

For a level 6 warlock, optimal rotation is: Immolate → Corruption (once HasCastBar is fixed) → Wand until dead. Shadow Bolt should only be used as opener or when target is about to die.

Current priority order in combat: Summon Pet → Fear → Drain Life → CoA → Corruption → Shadow Bolt → Shoot. Wand (Shoot) is dead last.

### Prior Fix Attempts

- DoT cooldowns changed from 0 → 1500ms (pull section) / 200ms (combat section) — only addresses re-cast spam, not priority ordering

### Investigation Plan

1. **Confirm Corruption cast time at level 6**: In WoW TBC Classic, Corruption (Rank 1, level 4) has a 2-second cast time. Improved Corruption (Affliction talent) is not available until at least level 10 (1/5 points). At level 6, it definitively has a cast bar.
2. **Check CastingHandler instant vs castbar path**: Read `CastInstant()` and `CastCastbar()` in `CastingHandler.cs` — confirm that `CastInstant` does NOT stop movement, while `CastCastbar` does
3. **Review upstream Warlock_8.json** for the level 8+ bracket: https://raw.githubusercontent.com/Xian55/WowClassicGrindBot/main/Json/class/Warlock_8.json
4. **Test with corrected profile**: Add `HasCastBar: true` to Corruption, increase Shadow Bolt cooldown to 5000-8000ms, reorder to put Shoot higher

### Solution Strategy

**Update the profile `BloodElf_Warlock_1-70_TBC.json`:**

```json
// Recommended Combat sequence for levels 4-19:
{
  "Combat": {
    "Sequence": [
      // Emergency only
      { "Name": "Summon Imp/Voidwalker", ... },
      { "Name": "Fear", "Requirement": "MobCount > 1 && Health% < 30", ... },
      { "Name": "Drain Life", "Requirement": "Health% < 50", ... },
      
      // DoTs — apply once, don't reapply
      { "Name": "Curse of Agony", "Requirement": "Level >= 8 && !Curse of Agony",
        "Cooldown": 1500 },
      { "Name": "Corruption", "HasCastBar": true, "BeforeCastDelay": 400,
        "Requirement": "!Corruption && Level < 20", "Cooldown": 1500 },
      { "Name": "Corruption", /* NO HasCastBar for instant after talent */
        "Requirement": "!Corruption && Level >= 20", "Cooldown": 1500 },
      
      // PRIMARY FILLER: Wand
      { "Name": "Shoot", "Key": "0", "Cooldown": 0 },
      
      // Shadow Bolt only when high mana and nothing else to do
      { "Name": "Shadow Bolt", "HasCastBar": true, "Cooldown": 8000,
        "Requirement": "Mana% > 70" },
      
      // Fallbacks
      { "Name": "AutoAttack", "Requirement": "!AutoAttacking" },
      { "Name": "Approach" }
    ]
  }
}
```

### Acceptance Criteria

- [ ] Corruption with HasCastBar stops movement before casting (no SPELL_FAILED_MOVING)
- [ ] Wand (Shoot) fires as primary filler between DoT applications
- [ ] Shadow Bolt only fires when mana is high (>70%) or as pull opener
- [ ] Mana sustain allows 3+ mob kills before drinking
- [ ] Kill speed improves (DoTs tick + wand vs pure Shadow Bolt spam)

---

## Environment Setup for Testing

### Prerequisites

1. WoW TBC Classic Anniversary client running with DataToColor addon loaded
2. Character logged in, in Eversong Woods starting area

### Start Services

```powershell
# Terminal 1: PathingAPI
cd C:\WowClassicGrindBot
dotnet run --project PathingAPI

# Terminal 2: BlazorServer (bot)
cd C:\WowClassicGrindBot
dotnet run --project BlazorServer
```

### Load Profile and Start Bot

```powershell
# Load warlock profile
Invoke-RestMethod -Uri "http://localhost:5000/api/bot/profile/load" -Method Post `
  -ContentType "application/json" `
  -Body '"BloodElf_Warlock_1-70_TBC"'

# If stuck on "Reading in-game bindings", bypass with:
Invoke-RestMethod -Uri "http://localhost:5000/api/launch/overrides" -Method Post `
  -ContentType "application/json" `
  -Body '{"skipKeyBindingCheck": true}'

# Start bot
Invoke-RestMethod -Uri "http://localhost:5000/api/bot/start" -Method Post
```

### Monitoring

A monitor script exists at `C:\WowClassicGrindBot\monitor-pull.ps1`:
```powershell
# Live tail (last 90 seconds of log, refresh every 3s)
.\monitor-pull.ps1 -Seconds 90

# One-shot (last 50KB)
.\monitor-pull.ps1 -Bytes 50000
```

Log file location: `C:\WowClassicGrindBot\BlazorServer\bin\Debug\net10.0\out*.log` (latest by write time).

### Key Patterns to Grep in Logs

```
SPELL_FAILED_MOVING          # Character tried to cast while still moving
BeforeCastDelay               # Should show full delay (400ms), NOT 13ms
BeforeCastFaceTarget          # Should show face time >0ms
Ranged pull .* failed         # Consecutive failure counter
New Plan                      # GOAP plan changes (transitions between goals)
Preventing pulling            # PullPrevention() triggered (should be rare)
Interrupted!                  # InterruptWatchdog cancellation
```

---

## Files Modified This Session

| File | Change | Status |
|------|--------|--------|
| `Core/Goals/PullTargetGoal.cs` | castAny accumulation fix (line ~229) | Built ✅, untested behavior |
| `Core/Goals/PullTargetGoal.cs` | bits.Combat() approach guard (line ~296) | Built ✅, untested behavior |
| `Core/Goals/PullTargetGoal.cs` | PullPrevention() logic inversion fix (lines 322-331) | Built ✅, untested behavior |
| `Json/class/BloodElf_Warlock_1-70_TBC.json` | DoT cooldowns 0→1500ms, BeforeCastDelay:400 on Immolate+SB | Built ✅, tested — still failing |
| `Core/GoalsComponent/ReactCastError.cs` | ERR_SPELL_OUT_OF_RANGE: StartForward→PressInteract | Built ✅, untested behavior |

---

## Recommended Investigation Order

1. **Enable InterruptWatchdog logging** (`CastingHandlerInterruptWatchdog.cs` line 15: `const bool Log = true;`) — rebuild and observe. This will reveal immediately whether the token is being cancelled and when.

2. **Fix StopMoving for click-to-move** — this is the foundational issue. Without a reliable stop, nothing else can work. Compare with upstream's approach.

3. **Fix Corruption HasCastBar** — add `"HasCastBar": true` to Corruption in both Pull and Combat sections of the JSON profile.

4. **Test with simplified pull sequence** — temporarily reduce Pull to just Shadow Bolt + Approach (like upstream Warlock_1.json) to isolate whether the multi-spell pull sequence itself causes issues.

5. **Rebalance combat rotation** — increase Shadow Bolt cooldown to 8000ms like upstream, move Shoot higher in priority.

6. **Compare upstream StopMoving**: Try fetching https://github.com/Xian55/WowClassicGrindBot/blob/main/Core/Goals/StopMoving.cs — if it 404s, search the upstream tree for `StopMoving` to find its location.

---

## Architecture Quick Reference

### GOAP Plan Flow
```
FollowRouteGoal → ApproachTargetGoal → PullTargetGoal → CombatGoal
     (patrol)       (close distance)     (open with spell)  (kill target)
```

### Casting Flow
```
CombatGoal.Update() / PullTargetGoal.Update()
  → castingHandler.Cast(keyAction, interrupt)
    → interruptWatchdog.Set(interrupt)  → returns CancellationToken
    → PreparedForCast(item, token)
      → BeforeCastFaceTarget: PressFastInteract() + StopForward()
      → BeforeCastDelay: stopMoving.Stop() + wait.Until(delay, token)
    → CastCastbar(item, token) / CastInstant(item, token)
      → PressKey → wait for cast start → wait for cast end
```

### Key Classes
- `PullTargetGoal` → `Core/Goals/PullTargetGoal.cs` (358 lines)
- `CombatGoal` → `Core/Goals/CombatGoal.cs` (410 lines)
- `ApproachTargetGoal` → `Core/Goals/ApproachTargetGoal.cs` (390 lines)
- `CastingHandler` → `Core/GoalsComponent/CastingHandler.cs` (994 lines)
- `StopMoving` → `Core/GoalsComponent/StopMoving.cs` (88 lines)
- `ReactCastError` → `Core/GoalsComponent/ReactCastError.cs` (273 lines)
- `CastingHandlerInterruptWatchdog` → `Core/GoalsComponent/CastingHandlerInterruptWatchdog.cs` (127 lines)
- `Wait` → `Core/GoalsComponent/Wait.cs` (182 lines)
- Warlock Profile → `Json/class/BloodElf_Warlock_1-70_TBC.json` (380 lines)
