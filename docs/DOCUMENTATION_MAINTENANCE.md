# Documentation Maintenance Guide

**Purpose:** Ensure project documentation remains accurate and aligned with implementation status.

**Last Updated:** February 5, 2026

---

## 📋 Documentation Hierarchy

```
MASTER_IMPLEMENTATION_PLAN.md (Root roadmap - high-level phases)
├── PHASE1_COMPLETION_STATUS.md (Detailed status of Phase 1)
├── PHASE2_IMPLEMENTATION_PLAN.md (Detailed status of Phase 2)
│   └── PRD_HAZARD_AVOIDANCE_SYSTEM.md (Detailed PRD for Hazard system)
│   └── HAZARD_TASKS.md (Task breakdown for Hazard system)
├── PRD_ANTI_DETECTION_HUMANIZATION.md (Detailed PRD for Humanization)
│   └── ANTI_DETECTION_TASKS.md (Task breakdown for Anti-Detection)
├── RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md (Future phases 3-4)
│   └── RESEARCH_FEATURES_TASKS.md (Task breakdown for research features)
├── PLAN_ALIGNMENT_REVIEW.md (Cross-document consistency validation)
└── DOCUMENTATION_INDEX.md (Navigation hub - THIS MUST STAY CURRENT)
```

---

## 🔄 Maintenance Workflow

### When Implementing New Features

**Before Starting:**
1. Review relevant PRD/Plan documents
2. Check DOCUMENTATION_INDEX.md for current status
3. Note any discrepancies

**During Implementation:**
1. Mark tasks as "In Progress" in relevant task files
2. Update acceptance criteria if requirements change
3. Document deviations from original plan

**After Completion:**
1. ✅ Mark tasks complete in task files (HAZARD_TASKS.md, ANTI_DETECTION_TASKS.md)
2. ✅ Update Implementation Plan status (PHASE1/2_COMPLETION_STATUS.md)
3. ✅ Update DOCUMENTATION_INDEX.md feature flags table
4. ✅ Update Master Plan if phase completed
5. ✅ Add any deviations to "Notes / Deviations" sections

### When Finding Documentation Errors

**Critical Issues (Blocking/Inaccurate):**
1. Update immediately
2. Add warning callouts with ❌ or ⚠️ symbols
3. Document the correct status
4. Update DOCUMENTATION_INDEX.md

**Minor Issues (Typos/Formatting):**
1. Fix as part of next documentation update
2. Batch multiple small fixes together

---

## 📊 Status Symbols Legend

Use these symbols consistently across all documentation:

| Symbol | Meaning | Usage |
|--------|---------|-------|
| ✅ | Complete, tested, verified | Feature/component fully working |
| ⚠️ | Partial, incomplete, or has issues | Feature partially working or blocked |
| ❌ | Not implemented, missing, or broken | Feature not started or not working |
| ⏳ | In progress | Currently being implemented |
| 🚧 | Blocked/On hold | Waiting for dependencies |

### Status Examples

**Good:**
```markdown
- ✅ Feature X implemented and tested
- ⚠️ Feature Y 80% complete (missing UI integration)
- ❌ Feature Z not started
```

**Bad:**
```markdown
- Feature X done
- Feature Y almost done
- Feature Z pending
```

---

## 📝 Key Files to Keep Updated

### High Priority (Update with every significant change)

1. **DOCUMENTATION_INDEX.md**
   - Feature flag status table
   - Implementation status column
   - Quick navigation accuracy

2. **PHASE1_COMPLETION_STATUS.md** / **PHASE2_IMPLEMENTATION_PLAN.md**
   - Component completion checkboxes
   - Test coverage status
   - Integration status

3. **Task Files** (HAZARD_TASKS.md, ANTI_DETECTION_TASKS.md)
   - Individual task completion
   - Acceptance criteria verification
   - File existence confirmation

### Medium Priority (Update at phase milestones)

4. **MASTER_IMPLEMENTATION_PLAN.md**
   - Phase status overview
   - Cross-cutting concerns validation
   - Feature compatibility matrix

5. **PRD Documents**
   - Implementation notes
   - Deviations from spec
   - Known issues/limitations

### Low Priority (Update periodically)

6. **PLAN_ALIGNMENT_REVIEW.md**
   - Cross-document consistency
   - Dependency mapping
   - Risk assessment updates

---

## 🎯 Documentation Quality Checklist

Before committing documentation changes:

- [ ] All checkboxes reflect actual implementation status
- [ ] No "completed" items that aren't actually done
- [ ] Status symbols used consistently (✅ ⚠️ ❌)
- [ ] DOCUMENTATION_INDEX.md updated with changes
- [ ] Cross-references between documents are accurate
- [ ] File paths in code examples exist
- [ ] Acceptance criteria match actual tests
- [ ] Known issues/gaps documented

---

## 🚨 Common Documentation Anti-Patterns

### ❌ Don't: Mark Everything Complete Prematurely

**Bad:**
```markdown
## Implementation Status
- ✅ All features complete
```

**Good:**
```markdown
## Implementation Status
- ✅ Core functionality complete
- ⚠️ Integration pending (blocked by API changes)
- ❌ Visualization not started
```

### ❌ Don't: Leave Out Critical Gaps

**Bad:**
```markdown
## Phase 2 Complete
All components implemented.
```

**Good:**
```markdown
## Phase 2: 85% Complete
### ✅ Completed (Phases 2.1-2.3, 2.6)
- Data models
- Event collection
- Analytics engine
- Background services

### ❌ Missing (Phases 2.4-2.5) - CRITICAL
- Navigation integration (blocks functionality)
- Visualization layer
```

### ❌ Don't: Use Vague Status

**Bad:**
```markdown
- Feature X: Done
- Feature Y: Almost done
- Feature Z: In progress
```

**Good:**
```markdown
- ✅ Feature X: Implemented, tested, integrated
- ⚠️ Feature Y: 80% complete (missing error handling)
- ⏳ Feature Z: In progress (ETA: 2 days)
```

---

## 🔍 Quarterly Documentation Audit

Every 3 months, perform a comprehensive audit:

1. **Completeness Review**
   - Check all planned features have corresponding docs
   - Verify implementation status is accurate
   - Identify orphaned/obsolete documentation

2. **Accuracy Review**
   - Compare file paths in docs to actual codebase
   - Verify code examples compile
   - Check acceptance criteria match tests

3. **Consistency Review**
   - Cross-reference between related documents
   - Verify status symbols used consistently
   - Check naming conventions

4. **Update PLAN_ALIGNMENT_REVIEW.md**
   - Document any inconsistencies found
   - Note deprecated features
   - Update dependency maps

---

## 📚 Adding New Documentation

When creating new docs:

1. **Follow naming convention:**
   - `PRD_FEATURE_NAME.md` for product requirements
   - `PHASE#_IMPLEMENTATION_PLAN.md` for phase plans
   - `FEATURE_TASKS.md` for task breakdowns

2. **Add to DOCUMENTATION_INDEX.md:**
   - Update appropriate category table
   - Set initial status
   - Add cross-references

3. **Link from parent documents:**
   - Add reference in MASTER_IMPLEMENTATION_PLAN.md
   - Link from related PRDs
   - Update dependency maps

4. **Include these sections:**
   - Executive Summary / Status
   - Implementation checklist with checkboxes
   - File inventory (created/modified)
   - Verification/testing steps
   - Known issues/gaps
   - Completion criteria

---

## 🎓 Best Practices

### 1. Lead with Status

Always put status information at the top:

```markdown
# Feature X Implementation Plan

**Status:** ⚠️ 75% Complete (Missing UI integration)
**Last Updated:** 2026-02-05
**Blocked By:** Issue #123
```

### 2. Use Tables for Complex Status

```markdown
| Component | Status | Tests | Notes |
|-----------|--------|-------|-------|
| Backend | ✅ Complete | ✅ 90% | Fully functional |
| API | ✅ Complete | ✅ 100% | All endpoints tested |
| UI | ❌ Not Started | N/A | Blocked by design |
```

### 3. Document Deviations Immediately

When implementation differs from plan:

```markdown
**Deviation from PRD:**
- **Planned:** Use Core/Hazard/IHazardProvider.cs
- **Actual:** Used SharedLib/IHazardProvider.cs
- **Reason:** Avoid circular reference between Core and PPather
- **Impact:** None - interface location doesn't affect functionality
```

### 4. Keep Task Lists Granular

```markdown
### Phase 1: Foundation ✅
- [x] Create database schema
- [x] Implement data access layer
- [x] Add unit tests

### Phase 2: API ⚠️
- [x] Design API endpoints
- [x] Implement controllers
- [ ] Add authentication ❌ (next sprint)
- [ ] Write API documentation ⏳ (in progress)
```

### 5. Update Dates

Always update "Last Updated" or "Status Date" when making changes:

```markdown
**Status:** ✅ Complete (as of 2026-02-05)
```

---

## 🔗 Quick Reference

### File Locations

```
docs/
├── MASTER_IMPLEMENTATION_PLAN.md    # Root roadmap
├── PHASE1_COMPLETION_STATUS.md      # Phase 1 detailed status
├── PHASE2_IMPLEMENTATION_PLAN.md    # Phase 2 detailed status
├── PRD_HAZARD_AVOIDANCE_SYSTEM.md   # Hazard system PRD
├── PRD_ANTI_DETECTION_HUMANIZATION.md # Humanization PRD
├── HAZARD_TASKS.md                  # Hazard task breakdown
├── ANTI_DETECTION_TASKS.md          # Anti-detection tasks
├── RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md # Phases 3-4
├── RESEARCH_FEATURES_TASKS.md       # Research task breakdown
├── PLAN_ALIGNMENT_REVIEW.md         # Cross-doc validation
├── DOCUMENTATION_INDEX.md           # Navigation hub
└── DOCUMENTATION_MAINTENANCE.md     # This file
```

### Key Principles

1. **Transparency over optimism** - Document gaps honestly
2. **Status at the top** - Don't make people hunt for it
3. **Specific over vague** - Use percentages, dates, concrete criteria
4. **Consistency** - Same symbols, same format, same location
5. **Currency** - Update docs as part of implementation, not after

---

## 📞 Questions?

If unsure about documentation updates:
1. Check this guide for the pattern
2. Look at existing docs for examples
3. When in doubt, be more detailed, not less
4. Mark uncertain items with ⚠️ and explain why

---

*Remember: Documentation is a living artifact. Keep it honest, current, and useful.*
