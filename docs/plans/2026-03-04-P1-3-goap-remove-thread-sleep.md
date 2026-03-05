# P1-3: Remove Thread.Sleep(2) from GOAP Loop

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove `Thread.Sleep(2)` calls at lines 292 and 356 of `GoapAgent.cs` to allow the GOAP loop to run at its natural rate, governed only by `WaitHandle.WaitAny()`.

**Priority:** P1 — HIGH performance

**Estimated time:** 2 minutes

---

## Context

### Current code (`Core/GOAP/GoapAgent.cs`)

**Line 292** (inside hysteresis check):
```csharp
Thread.Sleep(2);
```

**Line 356** (inside main GOAP loop):
```csharp
Thread.Sleep(2);
```

### Why this is a problem

`Thread.Sleep(n)` on Windows uses the OS multimedia timer, which has a default resolution of **~15.6ms**. Calling `Thread.Sleep(2)` does NOT sleep for 2ms — it sleeps for up to 15.6ms. This means the GOAP loop is artificially capped at **~64 Hz** regardless of how fast the underlying work completes.

The intended rate is ~500 Hz (one planning tick per 2ms). The `WaitHandle.WaitAny(waitHandles)` call in the loop (lines ~263-266) already provides efficient blocking: it returns immediately when the `DataReady` signal fires (from `AddonReader.Update()`). Adding `Thread.Sleep(2)` before or after this adds unnecessary latency.

### Game loop timing

```
Frame N:
  1. Screen capture (DXGI) → ~5ms
  2. AddonReader.Update() fires DataReady event
  3. GOAP thread wakes from WaitHandle.WaitAny()
  4. GoapAgent.GoapThread() executes planning + goal.Update()
  5. Thread.Sleep(2) ← THIS ADDS UP TO 15.6ms EXTRA LATENCY
  6. WaitHandle.WaitAny(waitHandles) blocks until next frame
```

By removing `Thread.Sleep(2)`, step 5 disappears and the thread immediately returns to waiting for the next frame.

---

## File

**`C:/WowClassicGrindBot/Core/GOAP/GoapAgent.cs`**

---

## Step 1: Confirm exact lines

```bash
grep -n "Thread.Sleep" Core/GOAP/GoapAgent.cs
```
Expected output:
```
292:            Thread.Sleep(2);
356:            Thread.Sleep(2);
```

## Step 2: Read surrounding context for each Sleep

Read lines 285-300 and 350-365 to understand what each Sleep guards. Common reasons for Sleep in a game loop:
- **Yield to let screenshot thread run** — handled by WaitHandle, not needed
- **Prevent tight loop on error** — if there's error handling around it, keep it only on the error path
- **Rate limiting** — WaitHandle already does this

## Step 3: Remove both Thread.Sleep(2) calls

Delete line 292: `Thread.Sleep(2);`
Delete line 356: `Thread.Sleep(2);`

## Step 4: Build
```bash
dotnet build Core
```

## Step 5: Run tests
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```
**Expected:** No regressions. The Sleep was not tested.

## Step 6: Commit
```bash
git add Core/GOAP/GoapAgent.cs
git commit -m "perf(goap): remove Thread.Sleep(2) - WaitHandle.WaitAny already yields between frames"
```

---

## If CPU spikes after removal

During manual bot testing, if CPU usage increases noticeably:

**Option A (preferred):** The WaitHandle loop is the correct solution — if CPU is high, the issue is that `DataReady` is firing too frequently. Investigate `AddonReader.Update()` rate.

**Option B (fallback):** Replace with `Thread.Yield()` which surrenders the timeslice to same-priority threads without the OS timer penalty:
```csharp
Thread.Yield(); // Not Thread.Sleep(2)
```

**Option C (last resort):** Use `SpinWait.SpinUntil(() => false, 1)` which uses a 1ms busy-wait — still better than the 15.6ms Sleep penalty.

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| CPU usage increases | Low | WaitHandle blocks efficiently; only matters if DataReady fires at >500 Hz |
| Timing-sensitive tests fail | Low | The 3 skipped timing tests were already known; no new timing issues expected |
| Goal execution rate too fast causing input spam | Very Low | Input dampening via `BurstDampener` and `KeyRepeatTimer` handles rate limiting downstream |
