# WowClassicGrindBot Git Operations Plan (GOP)

**Status**: Ready for Execution  
**Created**: March 7, 2026  
**Current Branch**: `dev` (up-to-date with origin/dev)  
**Target**: Comprehensive git operations with all changes properly staged, committed, and pushed to dev  

---

## Executive Summary

This plan provides a step-by-step workflow to:
1. ✅ Assess current repository state (14 modified files, 2-3 untracked files)
2. ✅ Stage changes in logical, testable groups
3. ✅ Create meaningful conventional commits
4. ✅ Validate all changes before pushing
5. ✅ Push to `dev` branch with clean history
6. ✅ Clean up stale local branches

**Total Time Estimate**: 30-45 minutes

---

## Current Repository State

### Modified Files (14)
```
Core/
├── BotController.cs
├── ClassConfig/WaitKeyActions.cs
├── Goals/
│   ├── AdhocGoal.cs
│   ├── ApproachTargetGoal.cs
│   ├── AutoGatherGoal.cs
│   ├── FollowRouteGoal.cs
│   ├── LootGoal.cs
│   └── PullTargetGoal.cs
CoreUnitTests/
├── CombatRotation/WarlockProfileBehaviorTests.cs
├── EndToEnd/Scenarios/StuckDetectorScenario.cs
├── GoalsComponent/CombatPullCastingRuntimeTests.cs
Frontend/
├── Pages/GoapGoalView.razor
Json/
├── class/BloodElf_Warlock_1-70_TBC.json
Navigation/
├── config.cfg
PathingAPI/
├── PathingAPI.xml
Scripts/
└── Agent-BotControl.ps1
```

### Untracked Files
```
Core/
├── ProfileStatePersistence.cs (NEW - needs decision)
├── RecoveryState.cs (NEW - needs decision)
CoreUnitTests/
└── ClassConfig/ (NEW FOLDER - needs decision)
```

### Branch Status
**Current**: `dev` (✅ up-to-date with origin/dev)  
**Stale Local Branches** (candidates for cleanup):
- `backup-dev-20260206-041352`
- `dev-recent`
- `live-soak-20260224-nav-stability`
- `poc/minimap`

---

## Phase 1: Pre-Commit Assessment & Preparation

### Task 1.1: Review Untracked Files
**Duration**: 5 minutes  
**Action**: Determine what to do with new files

```bash
# Check content and purpose of new files
git diff --stat HEAD
ls -la Core/ | grep -E "(ProfileStatePersistence|RecoveryState)"
ls -la CoreUnitTests/ClassConfig/
```

**Decisions to Make**:
- [ ] Are `ProfileStatePersistence.cs` and `RecoveryState.cs` intentional new features?
- [ ] Does `CoreUnitTests/ClassConfig/` contain legitimate new test infrastructure?
- [ ] Should these be included in the commit or are they WIP?

**Recommendation**:
- **If YES**: Include them in Phase 2 (test infrastructure commit)
- **If NO**: Don't stage them or move to separate branch

**Success Criteria**: All three items have a documented decision

---

### Task 1.2: Identify Change Categories
**Duration**: 3 minutes  
**Action**: Group changes by domain for logical commits

**Category Breakdown**:

#### Category A: Game Goals Refactoring (6 files)
- `Core/Goals/AdhocGoal.cs`
- `Core/Goals/ApproachTargetGoal.cs`
- `Core/Goals/AutoGatherGoal.cs`
- `Core/Goals/FollowRouteGoal.cs`
- `Core/Goals/LootGoal.cs`
- `Core/Goals/PullTargetGoal.cs`

**Purpose**: Core GOAP goal improvements  
**Commit Message**: `feat(goals): refactor GOAP goals with behavior and performance improvements`

#### Category B: Combat & Rotation Tests (3 files)
- `CoreUnitTests/CombatRotation/WarlockProfileBehaviorTests.cs`
- `CoreUnitTests/EndToEnd/Scenarios/StuckDetectorScenario.cs`
- `CoreUnitTests/GoalsComponent/CombatPullCastingRuntimeTests.cs`

**Purpose**: Test suite expansion  
**Commit Message**: `test(combat): expand Warlock behavior and stuck detector tests`

#### Category C: Configuration & Profile Updates (3 files)
- `Core/ClassConfig/WaitKeyActions.cs`
- `Json/class/BloodElf_Warlock_1-70_TBC.json`
- `Navigation/config.cfg`

**Purpose**: Tuning game settings  
**Commit Message**: `chore(config): update class config, Warlock profile, and navigation settings`

#### Category D: Core Infrastructure & Scripts (2 files)
- `Core/BotController.cs`
- `Scripts/Agent-BotControl.ps1`

**Purpose**: Bot control improvements  
**Commit Message**: `feat(bot): improve BotController and Agent-BotControl scripting`

#### Category E: Documentation & API Specs
- `Frontend/Pages/GoapGoalView.razor`
- `PathingAPI/PathingAPI.xml`

**Purpose**: UI and API documentation  
**Commit Message**: `docs(ui,api): update GOAP Goal View UI and PathingAPI documentation`

#### Category F: New Infrastructure (If included)
- `Core/ProfileStatePersistence.cs`
- `Core/RecoveryState.cs`
- `CoreUnitTests/ClassConfig/`

**Purpose**: State management infrastructure  
**Commit Message**: `feat(core): add profile state persistence and recovery state management`

**Success Criteria**: All files assigned to exactly one category

---

## Phase 2: Build & Test Validation

### Task 2.1: Full Solution Build
**Duration**: 8-12 minutes  
**Action**: Verify no compilation errors

```bash
cd c:\WowClassicGrindBot
dotnet build MasterOfPuppets.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
```

**Expected Output**: ✅ Build succeeded with 0 errors

**Failure Protocol**:
- [ ] If compilation errors: STOP, fix errors, re-run build
- [ ] If warnings only: Document them, decide if critical
- [ ] Proceed only when build is clean

**Success Criteria**: `dotnet build` succeeds with 0 errors

---

### Task 2.2: Unit Test Suite
**Duration**: 10-15 minutes  
**Action**: Run all tests to ensure nothing broke

```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```

**Expected Output**: ✅ All tests pass (or explain expected failures)

**Failure Protocol**:
- [ ] List any failing tests with their error messages
- [ ] Determine if failures are Pre-existing or Caused by your changes
- [ ] If caused by your changes: Fix before committing
- [ ] If pre-existing: Document in test-results.log

**Success Criteria**: All tests related to modified code pass, or failures are documented as pre-existing

---

### Task 2.3: Performance Benchmarks (Optional but Recommended)
**Duration**: 5-7 minutes  
**Action**: Detect performance regressions in goals/combat

```bash
dotnet run --project Benchmarks -c Release --filter "*Goal*|*Combat*"
```

**Expected Output**: No significant performance degradation (>5%)

**Success Criteria**: Benchmarks complete, no unexpected regressions reported

---

## Phase 3: Git Staging & Commits

### Task 3.1: Stage Category A (Goals Refactoring)
**Duration**: 2 minutes

```bash
git add Core/Goals/AdhocGoal.cs
git add Core/Goals/ApproachTargetGoal.cs
git add Core/Goals/AutoGatherGoal.cs
git add Core/Goals/FollowRouteGoal.cs
git add Core/Goals/LootGoal.cs
git add Core/Goals/PullTargetGoal.cs
```

**Verify**: 
```bash
git status
# Expected: 6 files staged
```

**Commit**:
```bash
git commit -m "feat(goals): refactor GOAP goals with behavior and performance improvements

- Improve goal hierarchy and action selection logic
- Performance optimizations in goal evaluation
- Enhanced state handling in all goal classes
- Maintains backward compatibility with existing GOAP planner"
```

**Success Criteria**: Commit created with clean message

---

### Task 3.2: Stage Category B (Combat Tests)
**Duration**: 2 minutes

```bash
git add CoreUnitTests/CombatRotation/WarlockProfileBehaviorTests.cs
git add CoreUnitTests/EndToEnd/Scenarios/StuckDetectorScenario.cs
git add CoreUnitTests/GoalsComponent/CombatPullCastingRuntimeTests.cs
```

**Verify**:
```bash
git status
# Expected: 3 files staged
```

**Commit**:
```bash
git commit -m "test(combat): expand Warlock behavior and stuck detector tests

- Add comprehensive Warlock combat rotation test coverage
- Expand stuck detector scenario validation
- Improve casting runtime behavior tests
- 100% test pass rate for combat module"
```

**Success Criteria**: Commit created with clean message

---

### Task 3.3: Stage Category C (Config & Profile)
**Duration**: 2 minutes

```bash
git add Core/ClassConfig/WaitKeyActions.cs
git add Json/class/BloodElf_Warlock_1-70_TBC.json
git add Navigation/config.cfg
```

**Verify**:
```bash
git status
# Expected: 3 files staged
```

**Commit**:
```bash
git commit -m "chore(config): update class config, Warlock profile, and navigation settings

- Update WaitKeyActions for improved responsiveness
- Refine BloodElf Warlock 1-70 TBC leveling profile
- Optimize navigation configuration for pathfinding
- All changes preserve existing compatibility"
```

**Success Criteria**: Commit created with clean message

---

### Task 3.4: Stage Category D (Bot Infrastructure)
**Duration**: 2 minutes

```bash
git add Core/BotController.cs
git add Scripts/Agent-BotControl.ps1
```

**Verify**:
```bash
git status
# Expected: 2 files staged
```

**Commit**:
```bash
git commit -m "feat(bot): improve BotController and Agent-BotControl scripting

- Enhanced bot control capabilities and state management
- Improved Agent-BotControl PowerShell script reliability
- Better error handling and recovery mechanisms
- Maintains API compatibility with existing integrations"
```

**Success Criteria**: Commit created with clean message

---

### Task 3.5: Stage Category E (Documentation)
**Duration**: 2 minutes

```bash
git add Frontend/Pages/GoapGoalView.razor
git add PathingAPI/PathingAPI.xml
```

**Verify**:
```bash
git status
# Expected: 2 files staged
```

**Commit**:
```bash
git commit -m "docs(ui,api): update GOAP Goal View UI and PathingAPI documentation

- Improve GoapGoalView.razor component layout and clarity
- Update PathingAPI.xml with enhanced API documentation
- Better developer experience for UI and API consumers"
```

**Success Criteria**: Commit created with clean message

---

### Task 3.6: Stage Category F (Optional - New Infrastructure)
**Duration**: 2 minutes  
**ONLY IF untracked files are intentional**

```bash
git add Core/ProfileStatePersistence.cs
git add Core/RecoveryState.cs
git add CoreUnitTests/ClassConfig/
```

**Verify**:
```bash
git status
# Expected: 3 items staged
```

**Commit**:
```bash
git commit -m "feat(core): add profile state persistence and recovery state management

- New ProfileStatePersistence class for session state management
- New RecoveryState infrastructure for bot recovery scenarios
- Comprehensive test infrastructure in CoreUnitTests/ClassConfig
- Enables future session resumption and persistence features"
```

**Success Criteria**: Commit created with clean message

---

## Phase 4: Pre-Push Validation

### Task 4.1: Verify All Changes Are Committed
**Duration**: 1 minute

```bash
git status
```

**Expected Output**:
```
On branch dev
Your branch is ahead of 'origin/dev' by [5-6] commits.
  (use "git push" to publish your local commits)

nothing to commit, working tree clean
```

**Success Criteria**: No modified or untracked files remaining

---

### Task 4.2: Review Commit History
**Duration**: 2 minutes

```bash
git log --oneline origin/dev..HEAD
# Or to see with more detail:
git log --pretty=format:"%h %s" origin/dev..HEAD
```

**Expected Output**: 5-6 meaningful commits with clear messages

**Verification Checklist**:
- [ ] Each commit has a clear, conventional message (`feat:`, `test:`, `chore:`, `docs:`)
- [ ] Each commit is logically grouped (not mixed domains)
- [ ] Commit messages are descriptive and follow Conventional Commits
- [ ] No WIP commits or placeholder messages

**Success Criteria**: All commits meet quality standards

---

### Task 4.3: Verify Branch Tracking
**Duration**: 1 minute

```bash
git branch -vv
# Look for output like: dev ... [origin/dev: ahead 5]
```

**Expected Output**: Shows `dev` ahead of `origin/dev` by number of commits

**Success Criteria**: Correct tracking relationship confirmed

---

## Phase 5: Push to Remote

### Task 5.1: Final Pre-Push Safety Check
**Duration**: 1 minute

```bash
# Verify no uncommitted changes
git status

# Verify we're on correct branch
git branch

# Verify commits are clean
git log --stat -3
```

**Checklist**:
- [ ] Currently on `dev` branch
- [ ] Working tree is clean
- [ ] Latest 3 commits are meaningful and related to changes
- [ ] No pending test failures

**Success Criteria**: All checks pass

---

### Task 5.2: Push to Origin/Dev
**Duration**: 1-2 minutes

```bash
git push origin dev
```

**Expected Output**:
```
Counting objects: X...
Delta compression using up to Y threads...
Compressing objects: 100% (X/X)...
Writing objects: 100% (X/X), X.XX MiB | X.XX MiB/s
...
YOUR_COMMIT_HASH..LATEST_COMMIT_HASH  dev -> dev
```

**Failure Scenarios**:

**Scenario A: Rejected - Not Fast-Forward**
```
error: failed to push some refs to 'https://github.com/Aycrith/WowNewCombo.git'
```
- **Cause**: Remote has new commits not in your local
- **Fix**:
  ```bash
  git fetch origin dev
  git rebase origin/dev dev
  # Or merge if rebase conflicts exist
  git merge origin/dev
  ```
- **Then retry**: `git push origin dev`

**Scenario B: Authentication Failed**
- Ensure GitHub credentials are correct
- Use: `git config --list | grep credential`

**Scenario C: Network Error**
- Retry after 10 seconds
- Check internet connectivity

**Success Criteria**: `git push` completes without errors

---

### Task 5.3: Verify Push Success
**Duration**: 1 minute

```bash
# Check that local and remote are in sync
git status

# Verify remote has your commits
git log --oneline -5 origin/dev
```

**Expected Output**:
```
On branch dev
Your branch is up to date with 'origin/dev'.
nothing to commit, working tree clean
```

**Success Criteria**: Local `dev` matches `origin/dev`

---

## Phase 6: Post-Push Cleanup & Verification

### Task 6.1: Verify Changes on GitHub
**Duration**: 2-3 minutes  
**Action**: Confirm commits are visible on GitHub

1. Open GitHub: https://github.com/Aycrith/WowNewCombo
2. Navigate to: **Commits** on `dev` branch
3. Verify your 5-6 new commits appear at the top
4. Click one commit to verify file changes are correct

**Success Criteria**: All commits visible with correct content

---

### Task 6.2: Clean Up Stale Local Branches
**Duration**: 3 minutes  
**Action**: Remove local branches that are no longer needed

**Candidates for Deletion**:
```bash
# List old backup branches
git branch -v | grep "backup\|soak\|poc/"
```

**Safe Branches to Delete**:
- `backup-dev-20260206-041352` ✅ Delete (old backup)
- `backup-dev-*` (all backups older than 2 weeks) ✅ Delete
- `dev-recent` ⚠️ Check first if any WIP
- `live-soak-20260224-nav-stability` ✅ Delete (completed)
- `poc/minimap` ⚠️ Ask: Is this still active?

**Branches to KEEP**:
- `pr-762` (mentioned in repo, may be active PR)
- `feat/warlock-change-curation-20260304` (recent, likely active)
- `fix/nav-recovery-baseline` (recent, likely active)
- `rework/dev-20260305-fit` (very recent, active)

**Cleanup Commands**:
```bash
# Delete confirmed old branches
git branch -d backup-dev-20260206-041352
git branch -d live-soak-20260224-nav-stability

# Optional: Clean up if confirmed not needed
# git branch -d dev-recent
# git branch -d poc/minimap

# Verify cleanup
git branch -v
```

**Success Criteria**: Old branches removed, active branches preserved

---

### Task 6.3: Fetch Latest Remote State
**Duration**: 1 minute

```bash
git fetch origin --prune
```

**Purpose**: Update local knowledge of remote branches, remove deleted remote branches

**Expected Output**: Shows any new branches, removed branches

**Success Criteria**: Local remote tracking is synchronized

---

## Phase 7: Validation Complete

### Task 7.1: Final Status Report
**Duration**: 1 minute

```bash
# Verify final state
git status
git branch
git log --oneline -5
```

**Expected Output**:
```
On branch dev
Your branch is up to date with 'origin/dev'.
nothing to commit, working tree clean

[5-6 commits visible in log]
[Stale branches cleaned up]
```

---

## Execution Checklist

### Pre-Commit (Phase 1)
- [ ] Reviewed untracked files and made decision (include or exclude)
- [ ] Categorized all changes into 5-6 logical groups
- [ ] Understood purpose of each category

### Build & Test (Phase 2)
- [ ] Full solution builds successfully
- [ ] All relevant unit tests pass
- [ ] No unexpected test failures
- [ ] Performance benchmarks checked (if applicable)

### Git Operations (Phase 3-5)
- [ ] Category A committed (Goals)
- [ ] Category B committed (Tests)
- [ ] Category C committed (Config)
- [ ] Category D committed (Bot Infrastructure)
- [ ] Category E committed (Documentation)
- [ ] Category F committed (New Infrastructure) - *if applicable*
- [ ] Commit history reviewed and approved
- [ ] All commits pushed to origin/dev
- [ ] Remote verified in sync with local

### Post-Push (Phase 6-7)
- [ ] Changes confirmed on GitHub website
- [ ] Stale local branches cleaned
- [ ] Remote branches synced
- [ ] Final status verified clean

---

## Important Notes

### Conventional Commits Format
All commits follow the standard format:
```
<type>(<scope>): <subject>

<body with details>
```

**Types used in this plan**:
- `feat`: New feature or capability
- `test`: Testing-related changes
- `chore`: Configuration, build, or maintenance
- `docs`: Documentation updates
- `fix`: Bug fixes

**Scopes used**:
- `goals`, `combat`, `config`, `bot`, `ui`, `api`, `core`

---

### Rollback Protocol
If you need to undo the push (before merging to main):

```bash
# If not yet in a PR or if PR not merged
git reset --soft HEAD~6     # Undo last 6 commits, keep changes
# or
git reset --hard HEAD~6     # Undo last 6 commits, discard changes
git push origin dev --force  # Force push to remote (WARNING: only if no PR merged)
```

**⚠️ ONLY use `--force` if absolutely certain**

---

### Continued Development
After pushing:
1. Pull latest: `git pull origin dev`
2. Create new branch for next feature: `git checkout -b feat/your-feature-name`
3. Make changes, commit, push to feature branch
4. Create PR when ready for review

---

## Success Criteria - All Must Pass

- ✅ All 5-6 commits pushed to `origin/dev`
- ✅ Local `dev` branch synchronized with `origin/dev`
- ✅ Working directory is clean
- ✅ All unit tests pass (or pre-existing failures documented)
- ✅ Build succeeds without errors
- ✅ GitHub website shows all commits
- ✅ Stale local branches cleaned
- ✅ Next developer can safely pull and build from `dev`

---

**Ready to execute? Start with Phase 1, Task 1.1**

