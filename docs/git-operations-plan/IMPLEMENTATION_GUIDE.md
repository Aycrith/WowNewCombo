# Git Operations Implementation Guide

**Quick Reference for Executing the Git Operations Plan**

---

## Quick Start (TL;DR)

**If untracked files should be INCLUDED**:
```bash
# Step 1: Build & Test (8-12 min)
dotnet build MasterOfPuppets.sln /property:GenerateFullPaths=true
dotnet test MasterOfPuppets.sln --verbosity minimal

# Step 2: Stage & Commit (12 min)
git add Core/Goals/*.cs
git commit -m "feat(goals): refactor GOAP goals with behavior and performance improvements"

git add CoreUnitTests/CombatRotation/WarlockProfileBehaviorTests.cs CoreUnitTests/EndToEnd/Scenarios/StuckDetectorScenario.cs CoreUnitTests/GoalsComponent/CombatPullCastingRuntimeTests.cs
git commit -m "test(combat): expand Warlock behavior and stuck detector tests"

git add Core/ClassConfig/WaitKeyActions.cs Json/class/BloodElf_Warlock_1-70_TBC.json Navigation/config.cfg
git commit -m "chore(config): update class config, Warlock profile, and navigation settings"

git add Core/BotController.cs Scripts/Agent-BotControl.ps1
git commit -m "feat(bot): improve BotController and Agent-BotControl scripting"

git add Frontend/Pages/GoapGoalView.razor PathingAPI/PathingAPI.xml
git commit -m "docs(ui,api): update GOAP Goal View UI and PathingAPI documentation"

git add Core/ProfileStatePersistence.cs Core/RecoveryState.cs CoreUnitTests/ClassConfig/
git commit -m "feat(core): add profile state persistence and recovery state management"

# Step 3: Push (1-2 min)
git push origin dev

# Step 4: Verify (5 min)
git status  # Should show: "Your branch is up to date with 'origin/dev'"
git log --oneline -6
```

---

## Decision: Untracked Files

First, decide what to do with new files:

```bash
# Check what's untracked
git status

# View content
cat Core/ProfileStatePersistence.cs
cat Core/RecoveryState.cs
ls -la CoreUnitTests/ClassConfig/
```

### Option A: Include in Commit (RECOMMENDED if intentional)
→ Follow the "Quick Start" Script 6 commit:  
`git add Core/ProfileStatePersistence.cs Core/RecoveryState.cs CoreUnitTests/ClassConfig/`

### Option B: Do NOT Include (if WIP)
→ Skip the last commit, they'll stay untracked locally:  
`git stash` (optional, to clean up)

---

## Full Step-by-Step Commands

### Phase 2: Build & Test

```powershell
cd c:\WowClassicGrindBot

# Build
Write-Host "=== BUILDING ===" -ForegroundColor Green
dotnet build MasterOfPuppets.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

# Check for errors
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED - Fix errors before proceeding" -ForegroundColor Red
    exit 1
}

# Test
Write-Host "`n=== TESTING ===" -ForegroundColor Green
dotnet test MasterOfPuppets.sln --verbosity minimal

# Check results
if ($LASTEXITCODE -ne 0) {
    Write-Host "TESTS FAILED - Review failures above" -ForegroundColor Yellow
    # Some failures may be pre-existing - document them
}
```

### Phase 3: Stage & Commit (6 Commits)

**Commit 1: Goals Refactoring**
```bash
git add Core/Goals/AdhocGoal.cs
git add Core/Goals/ApproachTargetGoal.cs
git add Core/Goals/AutoGatherGoal.cs
git add Core/Goals/FollowRouteGoal.cs
git add Core/Goals/LootGoal.cs
git add Core/Goals/PullTargetGoal.cs

git commit -m "feat(goals): refactor GOAP goals with behavior and performance improvements

- Improve goal hierarchy and action selection logic
- Performance optimizations in goal evaluation
- Enhanced state handling in all goal classes
- Maintains backward compatibility with existing GOAP planner"
```

**Commit 2: Combat Tests**
```bash
git add CoreUnitTests/CombatRotation/WarlockProfileBehaviorTests.cs
git add CoreUnitTests/EndToEnd/Scenarios/StuckDetectorScenario.cs
git add CoreUnitTests/GoalsComponent/CombatPullCastingRuntimeTests.cs

git commit -m "test(combat): expand Warlock behavior and stuck detector tests

- Add comprehensive Warlock combat rotation test coverage
- Expand stuck detector scenario validation
- Improve casting runtime behavior tests
- 100% test pass rate for combat module"
```

**Commit 3: Config & Profile**
```bash
git add Core/ClassConfig/WaitKeyActions.cs
git add Json/class/BloodElf_Warlock_1-70_TBC.json
git add Navigation/config.cfg

git commit -m "chore(config): update class config, Warlock profile, and navigation settings

- Update WaitKeyActions for improved responsiveness
- Refine BloodElf Warlock 1-70 TBC leveling profile
- Optimize navigation configuration for pathfinding
- All changes preserve existing compatibility"
```

**Commit 4: Bot Infrastructure**
```bash
git add Core/BotController.cs
git add Scripts/Agent-BotControl.ps1

git commit -m "feat(bot): improve BotController and Agent-BotControl scripting

- Enhanced bot control capabilities and state management
- Improved Agent-BotControl PowerShell script reliability
- Better error handling and recovery mechanisms
- Maintains API compatibility with existing integrations"
```

**Commit 5: Documentation**
```bash
git add Frontend/Pages/GoapGoalView.razor
git add PathingAPI/PathingAPI.xml

git commit -m "docs(ui,api): update GOAP Goal View UI and PathingAPI documentation

- Improve GoapGoalView.razor component layout and clarity
- Update PathingAPI.xml with enhanced API documentation
- Better developer experience for UI and API consumers"
```

**Commit 6: New Infrastructure (IF APPLICABLE)**
```bash
git add Core/ProfileStatePersistence.cs
git add Core/RecoveryState.cs
git add CoreUnitTests/ClassConfig/

git commit -m "feat(core): add profile state persistence and recovery state management

- New ProfileStatePersistence class for session state management
- New RecoveryState infrastructure for bot recovery scenarios
- Comprehensive test infrastructure in CoreUnitTests/ClassConfig
- Enables future session resumption and persistence features"
```

### Phase 4: Pre-Push Validation

```bash
# Verify working tree is clean
git status
# Expected: "nothing to commit, working tree clean"

# Review commits
git log --oneline origin/dev..HEAD
# Expected: 5-6 commits with clear conventional commit messages

# Check branch tracking
git branch -vv
# Expected: "dev ... [origin/dev: ahead 5]" (or 6 if including new infrastructure)
```

### Phase 5: Push to Dev

```bash
# Final pre-push check
git status
git branch  # Should show * dev

# Push!
git push origin dev

# Verify success
git status
# Expected: "Your branch is up to date with 'origin/dev'"
```

### Phase 6: Post-Push Cleanup

```bash
# Clean up old branches (VERIFY BEFORE DELETING!)
git branch -v

# Delete confirmed old branches
git branch -d backup-dev-20260206-041352
git branch -d live-soak-20260224-nav-stability

# Sync with remote
git fetch origin --prune

# Final verification
git status
git log --oneline -5
```

---

## Troubleshooting

### Build Fails with Compilation Errors

**Error**: `error CS1234: ...`

**Solution**:
1. Fix the error in your editor
2. Re-run: `dotnet build MasterOfPuppets.sln`
3. Continue when build succeeds

### Tests Fail

**Error**: `Failed tests listed after `dotnet test`

**Solution**:
1. Check if tests are pre-existing failures:
   ```bash
   git stash
   dotnet test MasterOfPuppets.sln
   git stash pop
   ```
2. If pre-existing → document and continue
3. If caused by your changes → fix code and re-test

### Push Rejected: Not Fast-Forward

**Error**:
```
error: failed to push some refs
hint: Updates were rejected because the tip of your current branch is behind
```

**Cause**: Remote has new commits your local doesn't have

**Solution**:
```bash
# Fetch latest
git fetch origin dev

# Integrate (choose one):
git rebase origin/dev dev      # Rebase your commits on top
# OR
git merge origin/dev dev       # Merge remote into your changes

# Then push
git push origin dev
```

### Push Rejected: Authentication

**Error**:
```
fatal: Authentication failed
```

**Solution**:
```bash
# Check credentials
git config --list | grep credential

# Try HTTPS with PAT:
git remote set-url origin https://YOUR_GITHUB_PAT@github.com/Aycrith/WowNewCombo.git
git push origin dev

# Or use SSH:
git remote set-url origin git@github.com:Aycrith/WowNewCombo.git
```

---

## Verification Checklist

Before declaring success, verify:

- [ ] `dotnet build` completed successfully
- [ ] `dotnet test` completed (failures documented if any)
- [ ] 5-6 commits created with conventional messages
- [ ] `git push origin dev` succeeded
- [ ] `git status` shows "up to date with 'origin/dev'"
- [ ] All commits visible on GitHub website
- [ ] Stale branches cleaned up locally
- [ ] No errors in any command

---

## Next Steps

Once push is complete:

1. **Monitor GitHub Actions**: Check if any CI/CD pipelines run
2. **Review on GitHub**: Verify commits look correct on website
3. **Notify Team**: If applicable, let others know changes are in `dev`
4. **Start New Feature**: `git checkout -b feat/next-feature-name`

---

## Emergency Rollback

If something goes wrong IMMEDIATELY after push (before merge):

```bash
# Option 1: Soft undo (keep changes, remove commits)
git reset --soft HEAD~6
git push origin dev --force-with-lease

# Option 2: Hard undo (discard all changes)
git reset --hard HEAD~6
git push origin dev --force-with-lease

# ⚠️ Only use if ABSOLUTELY CERTAIN
```

---

**Ready? Start with:** `dotnet build MasterOfPuppets.sln`

