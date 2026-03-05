# P0-4: Fix PPatherV2 Liquid Geometry Memory Leak

## STATUS: COMPLETED — commit `6fa6e7094` (2026-03-05)

**What was done:** Confirmed that `Structure terrain = new()` is scoped per tile (inside the outer x,y loop), not shared across tiles. The memory leak concern was a false alarm. Uncommented `adt.GetLiquidVertsAndTris((uint)cx, (uint)cy, terrain)` in `CoreManualTests/PPatherV2/PPatherV2.cs`. Water/ocean navigation geometry is now fully included.
**Tests added:** 0 (manual test only — CoreManualTests are excluded from `dotnet test`).
**Files modified:** `CoreManualTests/PPatherV2/PPatherV2.cs` (1 line uncommented).

---

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Re-enable `GetLiquidVertsAndTris` in PPatherV2 by resolving the memory concern that caused it to be commented out, restoring water/ocean/liquid navigation geometry.

**Priority:** P0 — CRITICAL (liquid geometry silently excluded causes navigation failures near water)

**Estimated time:** 5 minutes

---

## Context

`CoreManualTests/PPatherV2/PPatherV2.cs:229-231`:
```csharp
adt.GetTerrainVertsAndTris((uint)cx, (uint)cy, terrain);
// TODO: fix this memory leak
//adt.GetLiquidVertsAndTris((uint)cx, (uint)cy, terrain);
```

The full loop context (lines 220-240):
```csharp
Structure terrain = new();  // allocated once per tile

for (int a = 0; a < Adt.ADT_CELLS_PER_GRID * Adt.ADT_CELLS_PER_GRID; ++a)
{
    int cx = a % Adt.ADT_CELLS_PER_GRID;  // 0..15
    int cy = a / Adt.ADT_CELLS_PER_GRID;  // 0..15

    adt.GetTerrainVertsAndTris((uint)cx, (uint)cy, terrain);
    // TODO: fix this memory leak
    //adt.GetLiquidVertsAndTris((uint)cx, (uint)cy, terrain);
}

pooler.Return(adtData);
logger.LogInformation($"[{mapName}] [{x},{y}] verts: {terrain.Verts.Count} ...");
terrain.ExportDebugObjFile($"...terrain_{mapName}_{x}_{y}.obj");
```

`GetLiquidVertsAndTris` in `PPather/Triangles/GameV2/Adt.cs` (lines 381-435):
```csharp
public void GetLiquidVertsAndTris(uint x, uint y, Structure structure)
{
    // ... geometry generation, appending to structure.Verts and structure.Tris
    int vertsIndex = structure.Verts.Count;
    structure.Verts.Add(new Vector3(...));  // 4 verts per liquid quad
    structure.Tris.Add(new Tri(...));       // 2 tris per liquid quad
}
```

**Analysis of the "memory leak":** The `Structure terrain = new()` is scoped **per ADT tile** (per x,y outer loop). Within each tile, `GetTerrainVertsAndTris` and `GetLiquidVertsAndTris` accumulate geometry into `terrain.Verts` and `terrain.Tris`. This is **intentional accumulation** — all 256 cells of one tile build up a single `Structure` that then gets exported. After export, `terrain` goes out of scope and is GC'd.

The "memory leak" concern was likely from an older version where `terrain` was shared across tiles (outside the outer x,y loop). **If `terrain = new()` is inside the per-tile loop, there is no leak.**

---

## Files

1. **`C:/WowClassicGrindBot/CoreManualTests/PPatherV2/PPatherV2.cs`** — uncomment the call
2. **`C:/WowClassicGrindBot/PPather/Triangles/GameV2/Adt.cs`** — verify method is correct (read only)

---

## Step 1: Read the outer tile loop to confirm terrain scope

```bash
# Read lines 190-260 to see the outer x,y loop structure
```

Confirm that `Structure terrain = new();` is **inside** the outer tile loop (per-tile allocation), not outside it (which would cause cross-tile accumulation).

**If terrain IS inside the outer loop:** The TODO was a false alarm. Simply uncomment.

**If terrain is OUTSIDE the outer loop:** Add `terrain.Verts.Clear(); terrain.Tris.Clear(); terrain.TriTypes.Clear();` at the start of each tile iteration before calling the methods.

## Step 2: Read Adt.cs GetLiquidVertsAndTris (lines 381-435)

Verify the method does not hold static state or unmanaged resources.

## Step 3: Uncomment the call in PPatherV2.cs (line 231)

Change:
```csharp
adt.GetTerrainVertsAndTris((uint)cx, (uint)cy, terrain);
// TODO: fix this memory leak
//adt.GetLiquidVertsAndTris((uint)cx, (uint)cy, terrain);
```

To:
```csharp
adt.GetTerrainVertsAndTris((uint)cx, (uint)cy, terrain);
adt.GetLiquidVertsAndTris((uint)cx, (uint)cy, terrain);
```

## Step 4: Build to confirm compilation
```bash
dotnet build CoreManualTests
```
**Expected:** 0 errors.

## Step 5: Build full solution
```bash
dotnet build MasterOfPuppets.sln
dotnet test MasterOfPuppets.sln --verbosity minimal
```
**Expected:** No regressions (CoreManualTests are not run by `dotnet test` by default).

## Step 6: Commit
```bash
git add CoreManualTests/PPatherV2/PPatherV2.cs
git commit -m "fix(ppather): re-enable GetLiquidVertsAndTris - terrain is per-tile scoped, no leak exists"
```

---

## If the Memory Leak IS Real

If reading the code reveals `terrain` IS shared across tiles and lists grow unbounded:

```csharp
// Add at start of each tile iteration:
terrain.Verts.Clear();
terrain.Tris.Clear();
if (terrain.TriTypes is not null) terrain.TriTypes.Clear();

adt.GetTerrainVertsAndTris((uint)cx, (uint)cy, terrain);
adt.GetLiquidVertsAndTris((uint)cx, (uint)cy, terrain);
```

And adjust the commit message to:
```
fix(ppather): clear geometry buffers between tiles and re-enable liquid geometry
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Actual memory growth during manual test run | Low | Profile with dotnet-counters after re-enable |
| Liquid geometry causes nav pathfinder errors | Low | Liquid triangles have TriAreaId markers (LIQUID_WATER/OCEAN) for correct avoidance |
| renderMask = null skips most liquid quads | Medium | Understood from code: `if (!isOcean && renderMask != null)` — null mask means all quads render |
