# Skills Integration Guide — Autonomous Code Improvement Suite

**Version**: 1.0.0  
**Last Updated**: 2024-01-15  
**Total Skills**: 18 (13 general + 5 planning/improvement)

---

## 📋 Executive Summary

This document describes the complete **Autonomous Code Improvement Suite** — a collection of 18 specialized skills that work together to explore, analyze, and systematically improve any software project.

**Key Outcomes**:
- 🔍 **Comprehensive Code Analysis** — Evaluate quality, architecture, security, performance, technical debt
- 📊 **Quantified Health Scores** — Objective 0-100 scores across 6 dimensions with trend analysis
- 🗺️ **Actionable Roadmaps** — Sprint-by-sprint improvement plans with ROI prioritization
- 🤖 **Autonomous Operation** — Skills coordinate automatically via hand-off patterns
- 📈 **Continuous Improvement** — Track progress, adjust plans, measure impact

---

## 🎯 Skill Ecosystem Overview

### Core Philosophy: **Analyze → Prioritize → Plan → Execute → Measure → Iterate**

```
┌─────────────────────────────────────────────────────────────┐
│                   USER REQUEST                              │
│   "Audit my codebase and create improvement plan"          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
        ┌────────────────────────────────┐
        │   1. EXPLORATION PHASE         │
        │   Skills: context-scout        │
        └────────────┬───────────────────┘
                     │
                     ▼
        ┌────────────────────────────────┐
        │   2. ANALYSIS PHASE            │
        │   Skills:                      │
        │   - codebase-auditor          │
        │   - technical-debt-analyzer   │
        │   - architecture-analyzer     │
        │   - performance-profiler      │
        │   - dependency-analyzer       │
        └────────────┬───────────────────┘
                     │
                     ▼
        ┌────────────────────────────────┐
        │   3. SCORING PHASE             │
        │   Skills: code-health-scorer   │
        └────────────┬───────────────────┘
                     │
                     ▼
        ┌────────────────────────────────┐
        │   4. PLANNING PHASE            │
        │   Skills: improvement-roadmap  │
        └────────────┬───────────────────┘
                     │
                     ▼
        ┌────────────────────────────────┐
        │   5. EXECUTION PHASE           │
        │   Skills: refactoring-expert,  │
        │           migration-planner,   │
        │           deployment-strategist│
        └────────────┬───────────────────┘
                     │
                     ▼
        ┌────────────────────────────────┐
        │   6. MEASUREMENT PHASE         │
        │   Skills: code-health-scorer   │
        │   (repeat to measure impact)   │
        └────────────────────────────────┘
```

---

## 📦 Complete Skills Inventory

### **Category 1: Exploration & Discovery**
| Skill | Purpose | Lines | Location |
|-------|---------|-------|----------|
| **context-scout** | Codebase exploration, pattern discovery, dependency mapping | 3,500 | `~/.config/opencode/skills/context-scout/` |

### **Category 2: Analysis & Assessment**
| Skill | Purpose | Lines | Location |
|-------|---------|-------|----------|
| **codebase-auditor** | Comprehensive code quality analysis across 6 dimensions | 4,200 | `~/.config/opencode/skills/codebase-auditor/` |
| **technical-debt-analyzer** | Identify, quantify, and prioritize technical debt with ROI | 4,500 | `~/.config/opencode/skills/technical-debt-analyzer/` |
| **architecture-analyzer** | Evaluate SOLID principles, detect anti-patterns, generate C4 diagrams | 4,800 | `~/.config/opencode/skills/architecture-analyzer/` |
| **performance-profiler** | Identify bottlenecks, algorithmic complexity, optimization opportunities | 3,200 | `~/.config/opencode/skills/performance-profiler/` |
| **dependency-analyzer** | Security vulnerabilities, package updates, license compliance | 2,800 | `~/.config/opencode/skills/dependency-analyzer/` |

### **Category 3: Planning & Measurement**
| Skill | Purpose | Lines | Location |
|-------|---------|-------|----------|
| **code-health-scorer** | Calculate 0-100 health scores, track trends, benchmark | 4,600 | `~/.config/opencode/skills/code-health-scorer/` |
| **improvement-roadmap** | Sprint-by-sprint improvement plans with prioritization | 5,200 | `~/.config/opencode/skills/improvement-roadmap/` |

### **Category 4: Execution & Migration**
| Skill | Purpose | Lines | Location |
|-------|---------|-------|----------|
| **refactoring-expert** | Large-scale structural improvements, design pattern application | 2,700 | `~/.config/opencode/skills/refactoring-expert/` |
| **migration-planner** | Framework/language upgrade planning with rollback strategies | 2,500 | `~/.config/opencode/skills/migration-planner/` |
| **deployment-strategist** | CI/CD pipelines, blue-green/canary deployment strategies | 2,600 | `~/.config/opencode/skills/deployment-strategist/` |
| **api-integrator** | Third-party API integration best practices | 2,400 | `~/.config/opencode/skills/api-integrator/` |

### **Category 5: Infrastructure & Operations**
| Skill | Purpose | Lines | Location |
|-------|---------|-------|----------|
| **database-designer** | Schema design, normalization, query optimization | 1,800 | `~/.config/opencode/skills/database-designer/` |
| **logging-telemetry** | Structured logging, observability, OpenTelemetry | 1,600 | `~/.config/opencode/skills/logging-telemetry/` |
| **backup-recovery** | Disaster recovery planning, RPO/RTO strategies | 1,500 | `~/.config/opencode/skills/backup-recovery/` |

### **Category 6: Project-Specific (WowClassicGrindBot)**
| Skill | Purpose | Lines | Location |
|-------|---------|-------|----------|
| **goap-designer** | GOAP goal creation, debugging "NO PLAN" errors | 3,100 | `C:/WowClassicGrindBot/.opencode/skills/goap-designer/` |
| **combat-rotation-optimizer** | Spell rotation tuning, DPS optimization | 2,400 | `C:/WowClassicGrindBot/.opencode/skills/combat-rotation-optimizer/` |
| **pathfinding-analyzer** | PPather navigation, stuck detection, path optimization | 2,200 | `C:/WowClassicGrindBot/.opencode/skills/pathfinding-analyzer/` |

**Total**: 55,500 lines of documentation across 18 skills

---

## 🔗 Integration Patterns

### Pattern 1: Comprehensive Code Audit Workflow

**Trigger**: User asks "Audit my codebase and create improvement plan"

**Flow**:
```
1. context-scout
   ├─> Explore codebase structure
   ├─> Identify key modules, dependencies
   └─> Generate architectural context
        │
        ▼
2. codebase-auditor (orchestrator)
   ├─> Calls: technical-debt-analyzer
   │   └─> Returns: 47 debt items, ROI scores
   ├─> Calls: architecture-analyzer
   │   └─> Returns: SOLID scores, anti-patterns
   ├─> Calls: performance-profiler
   │   └─> Returns: 4 bottlenecks, optimization opportunities
   ├─> Calls: dependency-analyzer
   │   └─> Returns: 3 critical vulnerabilities
   └─> Consolidates: 104 issues prioritized by severity
        │
        ▼
3. code-health-scorer
   ├─> Receives: All analysis data
   ├─> Calculates: 6 dimension scores (quality, arch, testing, security, perf, maint)
   └─> Generates: Overall health 68/100, trend analysis
        │
        ▼
4. improvement-roadmap
   ├─> Receives: Prioritized issues + health scores
   ├─> Prioritizes: ROI-based ranking
   ├─> Sequences: Dependency-aware ordering
   └─> Generates: 6-sprint roadmap with tasks
        │
        ▼
5. refactoring-expert (for specific items)
   ├─> Receives: "Refactor OrderManager god class"
   └─> Returns: Step-by-step migration plan
```

**Output**: Complete audit report + executable roadmap + health dashboard

---

### Pattern 2: Focused Improvement (Single Issue)

**Trigger**: User asks "How do I fix the OrderManager god class?"

**Flow**:
```
1. architecture-analyzer
   ├─> Detects: OrderManager violates SRP (1800 LOC, 85 methods)
   ├─> Recommends: Split into OrderService, PaymentService, NotificationService
   └─> Hands off to: refactoring-expert
        │
        ▼
2. refactoring-expert
   ├─> Receives: OrderManager refactoring request
   ├─> Designs: 6-week migration plan (extract services incrementally)
   ├─> Includes: Rollback strategy, feature flags
   └─> Hands off to: improvement-roadmap
        │
        ▼
3. improvement-roadmap
   ├─> Receives: Refactoring plan
   ├─> Allocates: Sprints 26-29 (30% capacity for refactoring)
   └─> Tracks: Progress, adjusts if velocity changes
```

**Output**: Detailed refactoring plan integrated into sprint schedule

---

### Pattern 3: Continuous Improvement Loop

**Trigger**: Automated weekly health check

**Flow**:
```
1. code-health-scorer (weekly automation)
   ├─> Collects: Latest metrics from CI/CD
   ├─> Calculates: Current health scores
   ├─> Detects: Trends (improving/degrading)
   └─> Alerts: If health decreases
        │
        ▼
2. improvement-roadmap (if degradation detected)
   ├─> Analyzes: Which dimension degraded?
   ├─> Adjusts: Next sprint priorities
   └─> Notifies: Team and leadership
        │
        ▼
3. codebase-auditor (monthly deep dive)
   ├─> Re-runs: Full analysis
   ├─> Compares: Current vs. baseline
   └─> Reports: Progress to stakeholders
```

**Output**: Continuous monitoring with automatic course correction

---

## 🎭 Usage Scenarios

### Scenario 1: New Project Audit

**Context**: Just inherited a legacy codebase, need to understand health and create improvement plan.

**Commands**:
```
User: "Run a comprehensive audit of this codebase"

Agent:
1. Invokes: context-scout (understand structure)
2. Invokes: codebase-auditor (full analysis)
   └─> Automatically calls: technical-debt-analyzer, architecture-analyzer, etc.
3. Invokes: code-health-scorer (calculate baseline)
4. Invokes: improvement-roadmap (generate 3-month plan)

Output:
- Health score: 68/100
- 104 issues identified
- Top 10 priorities with ROI
- 6-sprint roadmap
```

**Time**: ~30 minutes for analysis (automated)

---

### Scenario 2: Pre-Release Quality Gate

**Context**: About to release major version, want to ensure code quality is acceptable.

**Commands**:
```
User: "Check if we're ready to release. Run quality gates."

Agent:
1. Invokes: code-health-scorer (current health)
2. Invokes: dependency-analyzer (security check)
3. Invokes: test-strategist (coverage analysis)
4. Invokes: performance-profiler (latency check)

Output:
✅ Health score: 82/100 (Good)
✅ Zero critical vulnerabilities
⚠️ Test coverage: 72% (below 80% target)
✅ P95 latency: 180ms (meets < 200ms SLA)

Recommendation: Ship, but prioritize test coverage in next sprint
```

**Time**: ~10 minutes for analysis

---

### Scenario 3: Tech Debt Sprint Planning

**Context**: Dedicated sprint for technical debt repayment, need prioritized backlog.

**Commands**:
```
User: "Help me plan a tech debt sprint. We have 50 story points available."

Agent:
1. Invokes: technical-debt-analyzer (get debt inventory)
2. Invokes: improvement-roadmap (prioritize + allocate)

Output:
Sprint 24 Tech Debt Plan:
- IMP-001: Add payment tests (8 pts, ROI: 30)
- IMP-005: Update vulnerable libraries (3 pts, ROI: 16.7)
- IMP-004: Extract validation logic (2 pts, ROI: 18)
Total: 13 pts allocated (26% capacity)

Remaining 37 pts: Feature work
Expected Savings: 120 hours/year
```

**Time**: ~5 minutes for planning

---

### Scenario 4: Architecture Migration

**Context**: Planning migration from monolith to microservices.

**Commands**:
```
User: "Create a plan to migrate from monolith to microservices"

Agent:
1. Invokes: architecture-analyzer (current state assessment)
2. Invokes: migration-planner (design target architecture)
3. Invokes: improvement-roadmap (18-month roadmap)

Output:
Phase 1: Extract PaymentService (Sprints 1-4)
Phase 2: Extract InventoryService (Sprints 5-8)
Phase 3: Extract OrderService (Sprints 9-12)
...
Total: 18 months, Strangler Fig pattern
Risk mitigation: Saga pattern, distributed tracing, service mesh
```

**Time**: ~20 minutes for planning

---

### Scenario 5: Quarterly Health Review

**Context**: Quarterly engineering review with leadership.

**Commands**:
```
User: "Generate executive dashboard for Q1 health review"

Agent:
1. Invokes: code-health-scorer (trend analysis)
2. Invokes: improvement-roadmap (Q1 outcomes report)

Output:
Q1 2024 Engineering Health Report

Overall Health: 68 → 79 (+11 points) ✅
Investment: 104 hours
ROI: 5.8× (annual savings: 600 hours)

Key Wins:
- Security: All critical vulnerabilities patched
- Performance: Search 68% faster
- Testing: Payment coverage 0% → 85%

Next Quarter Focus: Microservices extraction, test coverage to 80%
```

**Time**: ~5 minutes to generate

---

## 🧩 Skill Relationship Matrix

| Skill | Sends Data To | Receives Data From |
|-------|---------------|-------------------|
| **context-scout** | codebase-auditor, architecture-analyzer | — |
| **codebase-auditor** | code-health-scorer, improvement-roadmap | context-scout, tech-debt-analyzer, arch-analyzer, perf-profiler, dep-analyzer |
| **technical-debt-analyzer** | codebase-auditor, code-health-scorer, improvement-roadmap | — |
| **architecture-analyzer** | codebase-auditor, code-health-scorer, refactoring-expert | context-scout |
| **performance-profiler** | codebase-auditor, code-health-scorer | — |
| **dependency-analyzer** | codebase-auditor, code-health-scorer | — |
| **code-health-scorer** | improvement-roadmap | codebase-auditor, tech-debt-analyzer, arch-analyzer, test-strategist |
| **improvement-roadmap** | refactoring-expert, migration-planner | codebase-auditor, code-health-scorer, tech-debt-analyzer, arch-analyzer |
| **refactoring-expert** | — | architecture-analyzer, improvement-roadmap |
| **migration-planner** | — | improvement-roadmap |

---

## 📊 Metrics & Success Criteria

### Skill Suite Effectiveness

**Leading Indicators** (Measure immediately):
- **Audit Completion Time**: < 30 minutes for large codebase (1M+ LOC)
- **Recommendation Quality**: 90%+ of recommendations actionable
- **Coverage**: 100% of critical code paths analyzed
- **Accuracy**: 85%+ agreement with manual expert review

**Lagging Indicators** (Measure over time):
- **Health Score Improvement**: +10 points per quarter average
- **Adoption Rate**: 80%+ of recommended improvements executed
- **ROI Realization**: Predicted savings within 20% of actual
- **Team Satisfaction**: 4/5+ rating on improvement process

### Business Outcomes

**Engineering Velocity**:
- **Lead Time**: 30% reduction in time from commit to deploy
- **Change Failure Rate**: 50% reduction in production bugs
- **Deployment Frequency**: 2× increase

**Code Quality**:
- **Bug Density**: 40% reduction (bugs per 1K LOC)
- **Technical Debt**: 50% reduction in debt interest (hours/year)
- **Test Coverage**: Consistent 80%+ across all modules

**Team Health**:
- **Developer Satisfaction**: 4/5+ (codebase is improving)
- **Onboarding Time**: 50% reduction for new engineers
- **Retention**: Improved (engineers enjoy working in healthy codebase)

---

## 🚀 Getting Started Guide

### Step 1: Initial Setup (One-Time)

```bash
# Verify skills are installed
ls ~/.config/opencode/skills/

# Expected output:
# codebase-auditor/
# technical-debt-analyzer/
# architecture-analyzer/
# code-health-scorer/
# improvement-roadmap/
# context-scout/
# performance-profiler/
# dependency-analyzer/
# ... (18 total)
```

### Step 2: Run First Audit

**Command**:
```
"Run a comprehensive code audit and create an improvement roadmap"
```

**What Happens**:
1. ✅ Agent invokes `context-scout` → understands codebase structure
2. ✅ Agent invokes `codebase-auditor` → orchestrates full analysis
3. ✅ Agent invokes `code-health-scorer` → calculates baseline health
4. ✅ Agent invokes `improvement-roadmap` → generates 3-month plan

**Expected Output** (30 minutes later):
```markdown
# Code Health Assessment Complete

Overall Health: 68/100 (⚠️ Fair)

Breakdown:
- Quality: 85/100 ✅
- Architecture: 72/100 ⚠️
- Testing: 68/100 ⚠️
- Security: 90/100 ✅
- Performance: 75/100 ⚠️
- Maintainability: 60/100 🔥

Issues Found: 104 (12 critical, 34 high, 58 medium)

Top Priorities:
1. Add PaymentProcessor tests (ROI: 30)
2. Refactor OrderManager god class (ROI: 9.3)
3. Optimize product search query (ROI: 11.8)

Q1 Roadmap: 6 sprints, 9 improvements planned
Expected Outcome: 68 → 79 health score (+11 points)

[Full report available in: audit-2024-01-15/]
```

### Step 3: Review & Customize

**Review Recommendations**:
- Open `audit-2024-01-15/REPORT.md`
- Check prioritization (ROI scores)
- Validate dependencies (sequencing)

**Customize (Optional)**:
```
"Adjust roadmap priorities: Make security the top priority"

Agent:
- Re-ranks improvements with security weight increased
- Regenerates roadmap with security items in Sprint 1
```

### Step 4: Execute & Track

**Integrate with Sprint Planning**:
```
"Create JIRA tickets for Sprint 24 improvements"

Agent:
- Converts roadmap items to user stories
- Adds acceptance criteria
- Links dependencies
```

**Track Progress**:
```
"Show health dashboard"

Agent:
- Invokes code-health-scorer
- Generates HTML dashboard with trends
- Posts summary to Slack
```

---

## 🛠️ Customization Guide

### Adjusting Health Score Weights

**Default Weights**:
```typescript
const defaultWeights = {
  quality: 0.25,
  architecture: 0.20,
  testing: 0.20,
  security: 0.15,
  performance: 0.10,
  maintainability: 0.10
};
```

**Custom for Security-Critical App**:
```typescript
const fintechWeights = {
  security: 0.30,      // ← Increased
  quality: 0.20,
  testing: 0.20,
  architecture: 0.15,
  performance: 0.10,
  maintainability: 0.05
};
```

### Adding Custom Dimensions

**Example: Add "Accessibility" Dimension**

1. **Update code-health-scorer**:
```csharp
public record HealthScore(
    // ... existing dimensions ...
    double Accessibility  // ← New
);

private double CalculateAccessibilityScore(RawMetrics m)
{
    // WCAG 2.1 compliance, ARIA attributes, keyboard nav, etc.
    var wcagScore = m.WcagCompliance * 100;
    var ariaScore = m.AriaAttributesPercent;
    var keyboardScore = m.KeyboardNavigablePercent;
    
    return (wcagScore + ariaScore + keyboardScore) / 3.0;
}
```

2. **Update weights**:
```typescript
const weights = {
  quality: 0.20,
  architecture: 0.15,
  testing: 0.15,
  security: 0.15,
  performance: 0.10,
  maintainability: 0.10,
  accessibility: 0.15  // ← New
};
```

### Project-Specific Skills

**When to Create**:
- Project uses unique technology (e.g., GOAP for game AI)
- Domain-specific patterns (e.g., combat rotations for MMO bots)
- Custom frameworks (e.g., PPather for pathfinding)

**Location**:
```
C:/YourProject/.opencode/skills/
├── domain-specific-skill-1/
├── domain-specific-skill-2/
└── domain-specific-skill-3/
```

**Example**: See WowClassicGrindBot skills (goap-designer, combat-rotation-optimizer, pathfinding-analyzer)

---

## 📚 Best Practices

### 1. **Start Small, Iterate**
Don't try to fix everything at once.

```markdown
❌ BAD:
"Fix all 104 issues this sprint"
Result: Burnout, incomplete work, low quality

✅ GOOD:
"Fix top 3 highest-ROI items this sprint (10 story points)"
Result: Sustainable pace, measurable progress
```

### 2. **Trust the ROI Scores**
ROI-based prioritization is data-driven, not gut feel.

```markdown
Issue A: "Clean up variable names" (ROI: 2)
Issue B: "Add payment tests" (ROI: 30)

Don't prioritize A because "it's quick." Do B because impact is 15× higher.
```

### 3. **Celebrate Progress**
Code health improvements are invisible to users but critical.

```markdown
Sprint Retrospective:
🎉 Health improved: 68 → 71 (+3 points)
🎉 Payment test coverage: 0% → 85%
🎉 Search performance: 68% faster

Recognize: Alice (testing), Bob (performance)
```

### 4. **Automate Tracking**
Don't manually run audits every sprint.

```yaml
# .github/workflows/weekly-health-check.yml
schedule:
  - cron: '0 0 * * 0' # Sunday midnight

jobs:
  health-check:
    steps:
      - run: node scripts/calculate-health.js
      - run: post-to-slack.sh
```

### 5. **Adjust Based on Context**
Default settings are starting points, not gospel.

```markdown
Startup (move fast):
- Target health: 70+
- Improvement allocation: 10%

Mature product (stability critical):
- Target health: 85+
- Improvement allocation: 25%
```

---

## 🐛 Troubleshooting

### Issue: "Analysis takes too long (> 1 hour)"

**Cause**: Very large codebase (5M+ LOC) or slow CI environment

**Solutions**:
1. **Sample**: Analyze subset of codebase (e.g., core modules only)
2. **Cache**: Save analysis results, only re-analyze changed files
3. **Parallelize**: Run analysis skills concurrently
4. **Optimize Tools**: Use faster static analysis tools

### Issue: "Health score seems inaccurate"

**Cause**: Weights don't match project priorities

**Solutions**:
1. **Calibrate**: Compare against manual expert review
2. **Adjust Weights**: Increase weight for critical dimensions
3. **Custom Metrics**: Add project-specific sub-metrics
4. **Baseline**: Run on known-good codebase to validate

### Issue: "Roadmap recommendations not followed"

**Cause**: Team doesn't buy into improvement process

**Solutions**:
1. **Transparency**: Show ROI calculations, explain priorities
2. **Incremental**: Start with quick wins (high ROI, low effort)
3. **Celebrate**: Publicize successes, show before/after metrics
4. **Mandate**: Get leadership support for 20% improvement allocation

---

## 📖 Further Reading

### Skill Documentation
- Each skill has comprehensive SKILL.md with workflows, examples, best practices
- See `~/.config/opencode/skills/[skill-name]/SKILL.md`

### Related Concepts
- **Technical Debt**: Martin Fowler's Technical Debt Quadrant
- **SOLID Principles**: Robert C. Martin's "Clean Code"
- **DORA Metrics**: DevOps Research and Assessment metrics
- **SPACE Framework**: Developer productivity measurement

### Industry Benchmarks
- **SonarQube Quality Gates**: https://docs.sonarqube.org/latest/user-guide/quality-gates/
- **CISQ Standards**: https://www.it-cisq.org/standards/
- **State of DevOps Report**: https://cloud.google.com/devops/state-of-devops

---

## 🎓 Training & Onboarding

### For New Team Members

**Week 1**: Understanding
- Read this integration guide
- Review 1-2 skill docs (start with codebase-auditor, improvement-roadmap)
- Run sample audit on test repository

**Week 2**: Hands-On
- Run audit on real project
- Review generated roadmap with team
- Pick 1 improvement item to execute

**Week 3**: Contributing
- Suggest improvements to skill recommendations
- Customize weights for your project context
- Create project-specific skill (if needed)

### For Leadership

**Key Questions Answered**:
- **"Is our code healthy?"** → Health score dashboard (0-100)
- **"Are we improving?"** → Trend analysis (improving/degrading/stable)
- **"What should we prioritize?"** → ROI-ranked improvement list
- **"What's the ROI?"** → Investment hours vs. annual savings
- **"When will we be done?"** → Forecast based on current velocity

**Review Cadence**:
- **Weekly**: Automated Slack update (health score, top alert)
- **Monthly**: Executive dashboard review (30-min meeting)
- **Quarterly**: Strategic planning (roadmap for next quarter)

---

## 🤝 Contributing to Skills

### Reporting Issues
Found a bug or inaccuracy in a skill?

```markdown
Create issue at: [Your issue tracker]

Title: "[skill-name] Issue description"
Body:
- Skill version: 1.0.0
- Context: What were you trying to do?
- Expected: What should have happened?
- Actual: What actually happened?
- Logs: Include relevant output
```

### Suggesting Improvements

```markdown
Create issue at: [Your issue tracker]

Title: "[skill-name] Enhancement: Brief description"
Body:
- Use case: Why is this needed?
- Proposal: How should it work?
- Examples: Show before/after
- Impact: Who benefits?
```

### Creating New Skills

**Follow Template**:
```markdown
---
name: skill-name
version: 1.0.0
description: Brief purpose
author: Your Name
tags: [relevant, tags]
triggers: [keyword, phrases]
integrations: [related, skills]
---

## Purpose
[What problem does this solve?]

## When to Use This Skill
[Clear usage guidelines]

## Workflows
[Step-by-step processes]

## Code Examples
[Concrete, runnable examples]

## Best Practices
[Dos and don'ts]

## Anti-Patterns
[Common mistakes]

## Integration with Other Skills
[How it connects to ecosystem]
```

---

## 📜 License

All skills in this suite: **MIT License**

Free for commercial and personal use. Attribution appreciated but not required.

---

## 🙏 Acknowledgments

**Inspired By**:
- Martin Fowler (Refactoring, Technical Debt)
- Robert C. Martin (Clean Code, SOLID)
- Michael Feathers (Working Effectively with Legacy Code)
- DORA DevOps Research Team
- SonarSource (SonarQube quality model)

---

**Last Updated**: 2024-01-15  
**Maintainer**: OpenCode Skills Team  
**Version**: 1.0.0  
**Status**: Production Ready ✅

---

## 🎯 Quick Reference Card

### Common Commands

```bash
# Full audit + roadmap
"Run comprehensive code audit and create improvement plan"

# Quick health check
"What's the current health score?"

# Focused analysis
"Analyze architecture quality"
"Find all technical debt"
"Check for security vulnerabilities"

# Planning
"Plan next sprint improvements"
"Create 3-month improvement roadmap"

# Tracking
"Show health trend over last 6 months"
"Generate executive dashboard"
"Compare to industry benchmarks"

# Specific fixes
"How do I refactor the OrderManager class?"
"Create migration plan to microservices"
```

### Expected Response Times

| Task | Time | Automation |
|------|------|------------|
| Health score calculation | 2 min | ✅ CI/CD |
| Full codebase audit | 30 min | ✅ Scheduled |
| Improvement roadmap | 10 min | Manual |
| Executive dashboard | 5 min | ✅ Auto-generated |
| Refactoring plan | 15 min | Manual |

### Success Metrics Dashboard

```
┌─────────────────────────────────────────┐
│  Overall Health: 76/100 (⚠️ Fair)       │
│  Trend: ✅ +3 points/sprint             │
│  Target: 80/100 by Q1 end               │
└─────────────────────────────────────────┘

┌────────┬────────┬────────┬───────┐
│ Quality│  Arch  │ Testing│Security│
│  85/100│ 72/100 │ 68/100 │ 90/100 │
│   ✅    │   ⚠️   │   ⚠️   │   ✅   │
└────────┴────────┴────────┴───────┘

Next Sprint Focus:
1. Add payment tests (ROI: 30)
2. Fix layering violations (ROI: 14)
3. Optimize search (ROI: 11.8)
```

---

**End of Integration Guide**
