# P0-1: Upgrade Pre-release NuGet Packages to Stable

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace `Newtonsoft.Json 13.0.5-beta1` and `Serilog.Expressions 5.0.1-dev-00182` with their latest stable equivalents in `Directory.Packages.props`.

**Priority:** P0 — CRITICAL (pre-release packages must not ship in production)

**Estimated time:** 3 minutes

**Zero regressions required.** Both upgrades are API-compatible with the pre-release builds.

---

## Context

`Directory.Packages.props` is the single source of truth for all NuGet package versions in the solution (centralized package management). Two packages on line 20 and 32 carry pre-release version suffixes:

```xml
<!-- Line 20 -->
<PackageVersion Include="Serilog.Expressions" Version="5.0.1-dev-00182" />

<!-- Line 32 -->
<PackageVersion Include="Newtonsoft.Json" Version="13.0.5-beta1" />
```

**Latest stable versions confirmed (2026-03-04):**
- `Newtonsoft.Json` → **13.0.4** (released 2025-09-16, 51.4M downloads)
- `Serilog.Expressions` → **5.0.0** (released 2024-06-13, 36.5M downloads)

`Serilog.Expressions 5.0.0` is the promoted stable build of the dev-00182 prerelease. There are no filter-expression syntax changes. `Newtonsoft.Json 13.0.4` is a patch release over 13.0.3 — zero API changes from beta1.

---

## File to Modify

**`C:/WowClassicGrindBot/Directory.Packages.props`**

Full current content (relevant lines):
```xml
<PackageVersion Include="Serilog.Expressions" Version="5.0.1-dev-00182" />  <!-- line 20 -->
<PackageVersion Include="Newtonsoft.Json" Version="13.0.5-beta1" />          <!-- line 32 -->
```

---

## Implementation Steps

### Step 1: Verify available stable versions
```bash
dotnet list package --outdated
```
Confirm `Newtonsoft.Json` and `Serilog.Expressions` appear in the outdated list.

### Step 2: Edit Directory.Packages.props

**Change line 20** from:
```xml
<PackageVersion Include="Serilog.Expressions" Version="5.0.1-dev-00182" />
```
To:
```xml
<PackageVersion Include="Serilog.Expressions" Version="5.0.0" />
```

**Change line 32** from:
```xml
<PackageVersion Include="Newtonsoft.Json" Version="13.0.5-beta1" />
```
To:
```xml
<PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
```

### Step 3: Build
```bash
dotnet build MasterOfPuppets.sln
```
**Expected:** 0 errors, 0 warnings about pre-release packages.

### Step 4: Full test suite
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```
**Expected:** 1720/1723 CoreUnitTests + 29/29 FrontendUnitTests still passing. No regressions.

### Step 5: Verify no pre-release packages remain
```bash
dotnet list package --include-prerelease | grep -i "preview\|beta\|alpha\|dev\|rc"
```
**Expected:** No output (or only acceptable pre-release entries not related to these two packages).

### Step 6: Commit
```bash
git add Directory.Packages.props
git commit -m "chore(deps): upgrade Newtonsoft.Json 13.0.4 and Serilog.Expressions 5.0.0 to stable"
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| API break in Newtonsoft.Json | Very Low | 13.0.4 is a patch; same public API as beta1 |
| Serilog expression syntax changed | Very Low | 5.0.0 is the promoted build of dev-00182 |
| Other packages depend on old version | Very Low | CPM (central package management) enforces single version |

**If build fails:** Run `grep -rn "Serilog.Expressions" --include="*.cs"` to find usages and check for API changes in the Serilog.Expressions 5.0.0 release notes at https://github.com/serilog/serilog-expressions/releases.

---

## Verification

```bash
# Confirm clean build
dotnet build MasterOfPuppets.sln 2>&1 | tail -5

# Confirm test baseline maintained
dotnet test MasterOfPuppets.sln --verbosity minimal 2>&1 | tail -10
```
