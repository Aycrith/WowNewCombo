# Next Agent Start Here

## 🎯 You Have a Job: Autonomous Live Testing & Feature Re-enablement

**Current Status:** Navigation recovery baseline is complete, tested, and ready for you to validate in live testing.

**Your Goal:** Execute autonomous testing, validate baseline, re-enable features incrementally, and create PR to merge to dev.

**Time Required:** 30-45 minutes (fully autonomous)

**Starting Point:** Branch `fix/nav-recovery-baseline` is current and ready.

---

## 📋 Three Ways to Start

### Option 1: Auto Pilot (Recommended)
```bash
/ralph "Conduct autonomous live testing and feature re-enablement:
1. Deploy recovery baseline
2. Validate baseline stability (4+ hour simulation)
3. Re-enable features incrementally
4. Create PR with full test report
5. Document findings

Follow AUTONOMOUS_AGENT_INSTRUCTIONS.md phases 1-7
Request human intervention only for critical blockers" 5
```

### Option 2: Multi-Agent Coordination
```bash
/orchestrate autonomous-testing-phase "Follow AUTONOMOUS_AGENT_INSTRUCTIONS.md phases 1-7"
```

### Option 3: Manual Execution
1. Read **AUTONOMOUS_AGENT_INSTRUCTIONS.md** completely
2. Execute Phase 1: Deployment & Setup
3. Execute Phase 2: Baseline Validation
4. Execute Phase 3: Feature Re-enablement (4 features)
5. Execute Phase 4: Analysis & Metrics
6. Execute Phase 5: PR Creation & Merge
7. Execute Phase 6-7: Issues & Documentation

---

## 📚 Documentation You Need

**Read in This Order:**

1. **AUTONOMOUS_AGENT_INSTRUCTIONS.md** (START HERE - 854 lines)
   - Your complete execution guide with decision trees
   - 7 phases explained with success criteria
   - What to do if things fail
   - Quick-start commands at bottom

2. **EXECUTIVE_SUMMARY.md** (281 lines)
   - High-level overview of what was done
   - Why this fixes navigation failures
   - Q&A for common questions

3. **LIVE_CLIENT_TEST_READINESS.md** (267 lines)
   - Feature impact analysis
   - Test results already achieved
   - What to expect during testing

4. **BRANCH_COMPARISON_RECOVERY_VS_MAIN.md** (365 lines)
   - Detailed file-by-file changes
   - Risk assessment for each change
   - Rollback options if needed

5. **LIVE_TEST_PRE_FLIGHT_CHECKLIST.md** (374 lines)
   - Reference for verification items
   - Deployment procedure
   - Success criteria checklist

---

## 🎬 Quick Start

### If Using /ralph (Recommended):

```bash
/ralph "Conduct autonomous live testing and feature re-enablement following AUTONOMOUS_AGENT_INSTRUCTIONS.md:

Phase 1: Deploy baseline (verify build/tests)
Phase 2: Validate baseline (4hr simulation, metrics)
Phase 3: Re-enable features (CombatRotationOptimizer → StuckRecoveryV2 → HazardAvoidance → Detours)
Phase 4: Analyze & audit
Phase 5: Create PR
Phase 6: Debug if needed
Phase 7: Update documentation

Decision tree at each phase: proceed or rollback
Request human intervention only for critical blockers
Target: Complete PR to dev with all features validated" 5
```

### If Starting Manually:

```bash
# 1. Verify state
git status                                    # Clean?
git branch                                    # On fix/nav-recovery-baseline?
dotnet build MasterOfPuppets.sln --nologo   # 0 errors?
dotnet test CoreUnitTests --verbosity quiet # 1716+ passing?

# 2. Start Phase 1
# Read AUTONOMOUS_AGENT_INSTRUCTIONS.md Phase 1
# Follow the steps and decision tree

# 3. Continue through all 7 phases
# See AUTONOMOUS_AGENT_INSTRUCTIONS.md for complete guide
```

---

## ✅ What Success Looks Like

When you're done:

```
✅ Build: 0 errors, 0 warnings
✅ Tests: 1716+ passing, 0 failed
✅ Baseline: Stability validated (goal oscillations <10/4hr)
✅ Features: All 4 re-enabled and tested
✅ PR: Created and merged to dev
✅ Docs: Updated (CLAUDE.md, memory)
✅ Status: COMPLETE
```

---

## 🚨 If Something Goes Wrong

### You Have Tools:

- `/superpowers:systematic-debugging` — Diagnose failures
- `/audit-category [name]` — Code quality checks
- `/code-review [file]` — Security validation
- `/commit` — Git workflow automation

### Decision Points:

- **Build fails?** → Debug, fix, retry
- **Tests fail?** → Debug root cause, fix, retry
- **Metrics don't meet targets?** → Adjust parameters, retry
- **Feature causes regression?** → Disable, log issue, continue
- **Unresolvable issue?** → Create GitHub issue with details, request human help

---

## 📊 Expected Metrics

You're looking for these numbers:

| Metric | Target | How to Measure |
|--------|--------|---|
| Goal oscillations | <10 per 4hr | Run simulation, count transitions |
| Stuck false positives | <5% | Run tests, check error log |
| Goal stability | >95% | Run simulation, measure uptime |
| Test passing | 1716+ | `dotnet test CoreUnitTests` |
| Build | 0 errors | `dotnet build --nologo` |
| Code audit | No critical | `/audit-category security` |

If metrics don't meet targets → Adjust thresholds and retry

---

## 🎯 Decision Tree Summary

```
START
  ├─ Verify build/tests pass
  ├─ If fail → Debug & retry
  ├─ Deploy baseline
  ├─ Validate stability (4hr sim)
  │  ├─ If fail → Adjust thresholds, retry
  │  └─ If pass → Re-enable features
  ├─ Re-enable Feature 1: CombatRotationOptimizer
  │  ├─ Tests pass? → Feature 2
  │  └─ Fail? → Disable, log issue, Feature 2
  ├─ Re-enable Feature 2: StuckRecoveryV2
  │  ├─ Tests pass? → Feature 3
  │  └─ Fail? → Disable, log issue, Feature 3
  ├─ Re-enable Feature 3: HazardAvoidance
  │  ├─ Tests pass? → Feature 4
  │  └─ Fail? → Disable, log issue, Feature 4
  ├─ Re-enable Feature 4: Dynamic Detours
  │  ├─ Tests pass? → Create PR
  │  └─ Fail? → Disable, log issue, Create PR
  ├─ Create PR with test report
  ├─ Monitor CI & merge
  ├─ Update documentation
  └─ COMPLETE ✅
```

---

## 🤖 You Are Fully Equipped

Everything you need is documented:

- ✅ Detailed execution guide (AUTONOMOUS_AGENT_INSTRUCTIONS.md)
- ✅ Decision trees for each phase
- ✅ Success criteria clearly defined
- ✅ Tools available for any issues
- ✅ Human escalation points if needed
- ✅ Git commands for PR workflow
- ✅ Metrics to validate

**No human intervention needed unless you hit a critical blocker.**

---

## 🚀 Get Started Now

```bash
# Pick your approach:

# Option 1: Full auto-pilot
/ralph "Execute autonomous testing per AUTONOMOUS_AGENT_INSTRUCTIONS.md" 5

# Option 2: Manual execution
# Read AUTONOMOUS_AGENT_INSTRUCTIONS.md Phase 1, then start

# Either way: You have everything you need to succeed ✅
```

---

## 📞 Support Resources

If you get stuck:

1. Check **AUTONOMOUS_AGENT_INSTRUCTIONS.md** Phase 6 (Issues & Debugging)
2. Use `/superpowers:systematic-debugging` to diagnose
3. Create GitHub issue if you can't resolve autonomously
4. Request human intervention for unresolvable blockers

**Remember:** You can autonomously fix almost anything. Only escalate if truly stuck.

---

## Summary

**You:** AI agent
**Your job:** Validate navigation baseline, re-enable features, create PR
**Your guide:** AUTONOMOUS_AGENT_INSTRUCTIONS.md (854 lines, 7 phases)
**Your tools:** All Claude skills + git/dotnet commands
**Your authority:** Make all decisions except code review approvals
**Your timeline:** 30-45 minutes to completion
**Your success criteria:** PR merged to dev with all tests passing

**Go ahead and start. You've got this. 🚀**

