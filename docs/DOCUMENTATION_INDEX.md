# Documentation Index

**Last Updated:** February 5, 2026  
**Purpose:** Central index of all project documentation for quick navigation

---

## Quick Navigation

| Need To... | Start Here |
|-----------|------------|
| **Set up the bot** | [SETUP.md](../SETUP.md) |
| **Understand the architecture** | [SYSTEM_ARCHITECTURE.md](../SYSTEM_ARCHITECTURE.md) |
| **Write code** | [AGENTS.md](../AGENTS.md) |
| **Check what's changed** | [CHANGELOG.md](../CHANGELOG.md) |
| **Troubleshoot issues** | [KNOWN_ISSUES.md](../KNOWN_ISSUES.md) |
| **See future plans** | [MASTER_IMPLEMENTATION_PLAN.md](MASTER_IMPLEMENTATION_PLAN.md) |

---

## Documentation Categories

### 1. Getting Started

| Document | Purpose | Audience |
|----------|---------|----------|
| [README.md](../README.md) | Project overview, features, quick start | All users |
| [SETUP.md](../SETUP.md) | Complete installation guide (5 steps) | New users |
| [QUICK_START_CHECKLIST.md](../QUICK_START_CHECKLIST.md) | 10-minute quick start | Returning users |
| [PATHFINDER_GUIDE.md](../PATHFINDER_GUIDE.md) | V1/V3 pathfinding setup | All users |

### 2. Architecture & Design

| Document | Purpose | Audience |
|----------|---------|----------|
| [SYSTEM_ARCHITECTURE.md](../SYSTEM_ARCHITECTURE.md) | System topology, runtime behavior | Developers |
| [AGENTS.md](../AGENTS.md) | Coding conventions, build commands | Developers |

### 3. Implementation Plans

| Document | Phase | Status |
|----------|-------|--------|
| [PLAN_ALIGNMENT_REVIEW.md](PLAN_ALIGNMENT_REVIEW.md) | Meta-Review | **✅ Comprehensive alignment validation** |
| [MASTER_IMPLEMENTATION_PLAN.md](MASTER_IMPLEMENTATION_PLAN.md) | All phases | Active roadmap |
| [PHASE1_COMPLETION_STATUS.md](PHASE1_COMPLETION_STATUS.md) | Phase 1 | **✅ Complete & Integrated** |
| [PHASE2_IMPLEMENTATION_PLAN.md](PHASE2_IMPLEMENTATION_PLAN.md) | Phase 2 | **✅ Implemented** |
| [PRD_HAZARD_AVOIDANCE_SYSTEM.md](PRD_HAZARD_AVOIDANCE_SYSTEM.md) | Phase 2 | **✅ Implemented (PRD)** |
| [HAZARD_TASKS.md](HAZARD_TASKS.md) | Phase 2 | **✅ Complete (tasks)** |
| [PRD_ANTI_DETECTION_HUMANIZATION.md](PRD_ANTI_DETECTION_HUMANIZATION.md) | Safety | **✅ Implemented (Feb 5, 2026)** |
| [ANTI_DETECTION_TASKS.md](ANTI_DETECTION_TASKS.md) | Safety | **✅ Complete (tasks)** |
| [RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md](RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md) | Phase 3-4 | Implementation-ready |
| [RESEARCH_FEATURES_TASKS.md](RESEARCH_FEATURES_TASKS.md) | Phase 3-4 | Task breakdown (LRU cache ✅) |

### 3.5 Imported Research Materials

| Document | Topic | Purpose |
|----------|-------|---------|
| [warden_technical_analysis.html](ImportedResearch/warden/warden_technical_analysis.html) | Warden Anti-Cheat | Technical deep-dive into detection mechanisms |
| [warden_technical_analysis.pdf](ImportedResearch/warden/warden_technical_analysis.pdf) | Warden Anti-Cheat | PDF version of technical analysis |
| [warden_scholar.csv](ImportedResearch/warden/warden_scholar.csv) | Academic Research | Scholarly papers on anti-cheat systems |

### 4. Operations & Maintenance

| Document | Purpose | Audience |
|----------|---------|----------|
| [CHANGELOG.md](../CHANGELOG.md) | Version history, notable changes | All users |
| [KNOWN_ISSUES.md](../KNOWN_ISSUES.md) | Current issues and workarounds | All users |
| [DIAGNOSTICS_GUIDE.md](../DIAGNOSTICS_GUIDE.md) | Diagnostics system usage | Troubleshooting |
| [COMPREHENSIVE_TEST_PLAN.md](../COMPREHENSIVE_TEST_PLAN.md) | Testing procedures | Developers |

### 5. User-Specific Guides

| Document | Purpose |
|----------|---------|
| [BLOODELF_ROGUE_SETUP_GUIDE.md](../BLOODELF_ROGUE_SETUP_GUIDE.md) | Blood Elf Rogue character setup |
| [LEVEL2_ROGUE_READY.md](../LEVEL2_ROGUE_READY.md) | Level 2 rogue readiness checklist |
| [NEXT_STEPS_OPERATION_READINESS.md](../NEXT_STEPS_OPERATION_READINESS.md) | Post-config operational guide |

### 6. Reference

| Document | Purpose |
|----------|---------|
| [KEYBINDING_SOLUTION.md](../KEYBINDING_SOLUTION.md) | Keybinding automation |
| [LAUNCHER_README.md](../LAUNCHER_README.md) | Launcher documentation |

---

## Archived Documentation

Historical documentation preserved for reference:

### Session Transcripts
| Document | Date | Purpose |
|----------|------|---------|
| [HANDOFF_MANIFEST.md](archived/sessions/HANDOFF_MANIFEST.md) | Feb 2 | Agent handoff file list |
| [HANDOFF_SUMMARY.md](archived/sessions/HANDOFF_SUMMARY.md) | Feb 2 | Agent handoff context |
| [SESSION_PROGRESS_SUMMARY.md](archived/sessions/SESSION_PROGRESS_SUMMARY.md) | Feb 3 | Blood Elf setup session |
| [SESSION_SUMMARY_ARCHITECTURE.md](archived/sessions/SESSION_SUMMARY_ARCHITECTURE.md) | Feb 3 | Architecture analysis session |

### Bug Fix History
| Document | Issue | Resolution |
|----------|-------|------------|
| [BUG_FIX_REPORT.md](archived/bug-fixes/BUG_FIX_REPORT.md) | ActionBarCooldownReader crash | Bounds validation added |
| [CRITICAL_BUG_FIX_FRAME_DETECTION.md](archived/bug-fixes/CRITICAL_BUG_FIX_FRAME_DETECTION.md) | Frame 256+ detection | RGB encoding fix |
| [CHANGELOG_FRAME_DETECTION_FIX.md](archived/bug-fixes/CHANGELOG_FRAME_DETECTION_FIX.md) | Frame detection diagnostics | Enhanced logging |

---

## Document Dependency Map

```
MASTER_IMPLEMENTATION_PLAN.md (Root Roadmap)
├── PHASE1_COMPLETION_STATUS.md (✅ Complete)
│   └── BlazorServer/runtime_feature_flags.json
├── PHASE2_IMPLEMENTATION_PLAN.md (Ready)
│   └── PRD_HAZARD_AVOIDANCE_SYSTEM.md
└── RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md (Future)
    └── RESEARCH_FEATURES_TASKS.md

SYSTEM_ARCHITECTURE.md
└── KNOWN_ISSUES.md (references)

SETUP.md
├── PATHFINDER_GUIDE.md (linked)
├── README.md (linked)
└── CHANGELOG.md (linked)
```

---

## Feature Flag Configuration

All new features are controlled via [`BlazorServer/runtime_feature_flags.json`](../BlazorServer/runtime_feature_flags.json):

| Feature | Phase | Default | Status |
|---------|-------|---------|--------|
| ObjectPooling | 1 | ✅ Enabled | Deployed |
| CircuitBreaker | 1 | ✅ Enabled | Deployed |
| PathSmoothing | 1 | ✅ Enabled | Deployed |
| StuckRecoveryV2 | 1 | ✅ Enabled | Deployed |
| HazardAvoidance | 2 | ❌ Disabled | ✅ Implemented (off by default) |
| AIProfileGenerator | 3 | ❌ Disabled | Future |
| ProfileMarketplace | 3 | ❌ Disabled | Future |
| BehaviorTreeCombat | 3 | ❌ Disabled | Future |
| HybridLLMDecision | 3 | ❌ Disabled | Future |

---

## Terminology Glossary

| Term | Definition |
|------|------------|
| **GOAP** | Goal-Oriented Action Planning - AI decision system |
| **PPather** | Local pathfinding library using MPQ map data |
| **AmeisenNavigation** | RemoteV3 pathfinding server using MMaps |
| **DataToColor** | WoW addon that encodes game state as colored pixels |
| **Feature Flag** | Runtime toggle for enabling/disabling features |
| **Circuit Breaker** | Pattern to prevent cascading failures |
| **Breadcrumb Tracker** | Records player positions for stuck recovery |

---

## Maintenance Notes

### When Adding New Documentation

1. Add entry to appropriate category above
2. Update dependency map if applicable
3. Set proper cross-references to related docs
4. Follow naming convention: `FEATURE_NAME.md` or `PRD_FEATURE_NAME.md`

### When Archiving Documentation

1. Move to `docs/archived/` subdirectory
2. Add entry to Archived Documentation section
3. Update any references in active docs

---

*This index is maintained to ensure documentation coherence and discoverability.*
