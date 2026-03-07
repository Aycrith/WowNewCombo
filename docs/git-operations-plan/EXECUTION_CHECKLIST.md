# Git Operations Execution Checklist

**Repository**: WowClassicGrindBot  
**Branch**: `dev`  
**Target**: Push all changes to origin/dev  
**Start Time**: ___________  
**End Time**: ___________  

---

## SECTION 1: PRE-EXECUTION (5 min)

### Decision: Untracked Files
```
Untracked files found:
- Core/ProfileStatePersistence.cs
- Core/RecoveryState.cs
- CoreUnitTests/ClassConfig/
```

**DECISION**: 
- [ ] INCLUDE (intentional new features)
- [ ] EXCLUDE (work in progress, don't commit)

---

## SECTION 2: BUILD & TEST VALIDATION (12 min)

### Build Phase
```
Command: dotnet build MasterOfPuppets.sln /property:GenerateFullPaths=true
```

**Result**:
- [ ] ✅ BUILD SUCCEEDED (0 errors)
- [ ] ❌ BUILD FAILED (stop, fix errors, restart)

**Errors found (if any)**:
```
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
```

---

### Test Phase
```
Command: dotnet test MasterOfPuppets.sln --verbosity minimal
```

**Result**:
- [ ] ✅ ALL TESTS PASSED
- [ ] ⚠️ TESTS FAILED (check if pre-existing)

**Failed tests (if any)**:
```
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
```

**Pre-existing failure?** 
- [ ] Yes (document above, proceed)
- [ ] No (stop, fix code, re-test)

---

## SECTION 3: GIT STAGING & COMMITS (12 min)

### Commit 1: Goals Refactoring
```
Files: 6 Core/Goals/*.cs
Message: feat(goals): refactor GOAP goals...
```
- [ ] Staged: `git add Core/Goals/*.cs`
- [ ] Committed: ✅
- [ ] Hash: ___________________________

---

### Commit 2: Combat Tests
```
Files: 3 CoreUnitTests/*Tests.cs
Message: test(combat): expand Warlock behavior...
```
- [ ] Staged: ✅
- [ ] Committed: ✅
- [ ] Hash: ___________________________

---

### Commit 3: Config & Profile
```
Files: 3 (WaitKeyActions, Warlock profile, nav config)
Message: chore(config): update class config...
```
- [ ] Staged: ✅
- [ ] Committed: ✅
- [ ] Hash: ___________________________

---

### Commit 4: Bot Infrastructure
```
Files: 2 (BotController.cs, Agent-BotControl.ps1)
Message: feat(bot): improve BotController...
```
- [ ] Staged: ✅
- [ ] Committed: ✅
- [ ] Hash: ___________________________

---

### Commit 5: Documentation
```
Files: 2 (GoapGoalView.razor, PathingAPI.xml)
Message: docs(ui,api): update GOAP Goal View...
```
- [ ] Staged: ✅
- [ ] Committed: ✅
- [ ] Hash: ___________________________

---

### Commit 6: New Infrastructure (IF APPLICABLE)
```
Files: 3 (ProfileStatePersistence, RecoveryState, ClassConfig/)
Message: feat(core): add profile state persistence...
Applies: [ ] Only if decision above = INCLUDE
```
- [ ] Staged: ✅
- [ ] Committed: ✅
- [ ] Hash: ___________________________

---

## SECTION 4: PRE-PUSH VALIDATION (3 min)

### Working Tree Status
```
Command: git status
Expected: working tree clean, X commits ahead of origin/dev
```
- [ ] ✅ Working tree is clean
- [ ] ✅ No modified files
- [ ] ✅ No untracked files (except if intentional)

---

### Commit History Review
```
Command: git log --oneline origin/dev..HEAD
```
- [ ] ✅ 5 commits visible (or 6 if including new infrastructure)
- [ ] ✅ All messages follow conventional commit format
- [ ] ✅ No WIP or placeholder commits
- [ ] ✅ Commits are logical and grouped

**Sample commit messages**:
```
[1] feat(goals): refactor GOAP goals...
[2] test(combat): expand Warlock behavior...
[3] chore(config): update class config...
[4] feat(bot): improve BotController...
[5] docs(ui,api): update GOAP Goal View...
[6] feat(core): add profile state persistence... [IF INCLUDED]
```

---

### Branch Status
```
Command: git branch -vv
Expected: dev ... [origin/dev: ahead 5] or ahead 6
```
- [ ] ✅ On correct branch (`dev`, not other branch)
- [ ] ✅ Correct tracking (`origin/dev`)
- [ ] ✅ Correct ahead count (5 or 6)

---

## SECTION 5: PUSH TO REMOTE (2 min)

### Pre-Push Final Check
```
Commands: git status && git branch
```
- [ ] ✅ Currently on `dev` branch
- [ ] ✅ Working tree clean
- [ ] ✅ Ready to push

---

### Execute Push
```
Command: git push origin dev
```

**Push result**:
- [ ] ✅ PUSH SUCCEEDED
- [ ] ❌ PUSH FAILED (see error below)

**If failed, error message**:
```
_________________________________________________________________
_________________________________________________________________

Type of failure:
[ ] Not fast-forward (run: git fetch && git rebase)
[ ] Authentication (check credentials)
[ ] Network (retry after 10 sec)
[ ] Other: _________________________________________________
```

**Recovery action taken**:
```
_________________________________________________________________
_________________________________________________________________
```

---

### Verify Push Success
```
Command: git status
Expected: Your branch is up to date with 'origin/dev'
```
- [ ] ✅ Push confirmed successful
- [ ] ✅ Local and remote in sync

---

## SECTION 6: POST-PUSH VERIFICATION (5 min)

### GitHub Website Verification
1. Open: https://github.com/Aycrith/WowNewCombo
2. Navigate to `Commits` on `dev` branch
3. Check that new commits appear

**Verification**:
- [ ] ✅ All 5-6 commits visible on GitHub
- [ ] ✅ Latest commit is from this operation
- [ ] ✅ File changes look correct
- [ ] ✅ No unexpected changes included

---

### Clean Up Stale Branches
```
Branches to delete:
- backup-dev-20260206-041352 (old backup)
- live-soak-20260224-nav-stability (completed)
```

**Cleanup status**:
- [ ] ✅ Old branches identified
- [ ] ✅ Verified safe to delete
- [ ] ✅ Branches deleted: `git branch -d [name]`
- [ ] ✅ Remote synced: `git fetch origin --prune`

**Branches deleted**:
```
_________________________________________________________________
_________________________________________________________________
```

---

### Final Status
```
Command: git status && git log --oneline -5
```
- [ ] ✅ Working tree clean
- [ ] ✅ Latest 5 commits shown
- [ ] ✅ No stale branches lingering
- [ ] ✅ All changes pushed

---

## SECTION 7: SUCCESS CRITERIA (Final Checklist)

- [ ] ✅ All 5-6 commits pushed to `origin/dev`
- [ ] ✅ `git status` shows: "Your branch is up to date with 'origin/dev'"
- [ ] ✅ Working directory is clean (no modified files)
- [ ] ✅ All unit tests passed (or pre-existing failures documented)
- [ ] ✅ All commits visible on GitHub website
- [ ] ✅ Stale local branches cleaned
- [ ] ✅ No errors or warnings in any command
- [ ] ✅ Ready for next developer to pull and build from `dev`

---

## NOTES & OBSERVATIONS

**Any issues encountered**:
```
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
_________________________________________________________________
```

**Performance notes**:
```
Build time: ____ min
Test time: ____ min
Commit time: ____ min
Push time: ____ sec
Total time: ____ min
```

**Team communication** (if applicable):
```
[ ] Notified team of changes in dev
[ ] Documented changes in [team channel]
[ ] Noted any breaking changes
[ ] Noted any dependencies for next step
```

---

## SIGN-OFF

**Executed By**: ________________________  
**Date/Time**: ________________________  
**All Criteria Met**: [ ] YES  [ ] NO  

**If NO, issues to resolve**:
```
_________________________________________________________________
_________________________________________________________________
```

---

**Status**: ⏳ IN PROGRESS / ✅ COMPLETE / ❌ FAILED

