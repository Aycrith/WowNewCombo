# AI Capability Internalization Guide

Captured on: **2026-03-05**

## Alias Invocation Grammar

1. cap:<alias>
2. use capability <alias>
3. run <alias> workflow

## What You Can Ask by Name

| Alias | Category | Status | Function |
|---|---|---|---|
| agent-orchestration-sprint | Agent orchestration and autonomy | native | Gated multi-step engineering sprint. |
| repo-context-scout | Search and retrieval | native | Map repository structure and dependency paths. |
| cross-platform-capability-audit | Docs and knowledge generation | native | Audit capabilities across platforms with scoring. |
| code-refactor-safety-pass | Code analysis and refactoring | native | Apply structural refactors with safety checks. |
| test-regression-hardening | Test generation and execution | native | Generate tests and run regression loops. |
| shell-investigation-loop | Shell and filesystem control | native | Use iterative shell diagnostics and implementation loops. |
| browser-ui-repro | Browser and UI control | partial | Reproduce UI issues and gather browser evidence. |
| mcp-integration-setup | API and external integrations (including MCP/tool ecosystems) | composed | Set up and validate MCP/tool integrations. |
| api-integration-hardening | API and external integrations (including MCP/tool ecosystems) | native | Harden API integrations with resilience patterns. |
| debug-root-cause-sprint | Debugging and observability | native | Rapid root-cause analysis and fix validation. |
| docs-knowledge-pack | Docs and knowledge generation | native | Generate implementation and validation documentation. |
| ci-failure-triage | Workflow automation and CI/CD hooks | composed | Triage CI failures and implement fixes. |
| dependency-risk-audit | Code analysis and refactoring | native | Assess dependency vulnerabilities and update risk. |
| migration-planning-pack | Workflow automation and CI/CD hooks | native | Build migration plan with rollback and risk tiers. |
| pr-review-sweep | Code analysis and refactoring | composed | Run severity-first PR review and remediation hints. |
| issue-reproducer-loop | Debugging and observability | native | Convert issue reports into deterministic repro loops. |
| memory-capture-brief | Context and memory systems | native | Capture decisions, assumptions, and unresolved risks. |
| safety-gated-execution | Safety, approval, and permission models | native | Run risky operations with explicit gates and checks. |
| performance-optimization-pass | Debugging and observability | native | Optimize hot paths and validate performance gains. |
| platform-parity-compare | Docs and knowledge generation | native | Compare platform capability parity for a workflow. |
| workflow-automation-blueprint | Workflow automation and CI/CD hooks | native | Design reusable automation blueprints and runbooks. |

## Category Reference

## Agent orchestration and autonomy
- **agent-orchestration-sprint**: Gated multi-step engineering sprint. Example: cap:agent-orchestration-sprint. Status: native.

## API and external integrations (including MCP/tool ecosystems)
- **mcp-integration-setup**: Set up and validate MCP/tool integrations. Example: cap:mcp-integration-setup. Status: composed.
- **api-integration-hardening**: Harden API integrations with resilience patterns. Example: cap:api-integration-hardening. Status: native.

## Browser and UI control
- **browser-ui-repro**: Reproduce UI issues and gather browser evidence. Example: cap:browser-ui-repro. Status: partial.

## Code analysis and refactoring
- **code-refactor-safety-pass**: Apply structural refactors with safety checks. Example: cap:code-refactor-safety-pass. Status: native.
- **dependency-risk-audit**: Assess dependency vulnerabilities and update risk. Example: cap:dependency-risk-audit. Status: native.
- **pr-review-sweep**: Run severity-first PR review and remediation hints. Example: cap:pr-review-sweep. Status: composed.

## Context and memory systems
- **memory-capture-brief**: Capture decisions, assumptions, and unresolved risks. Example: cap:memory-capture-brief. Status: native.

## Debugging and observability
- **debug-root-cause-sprint**: Rapid root-cause analysis and fix validation. Example: cap:debug-root-cause-sprint. Status: native.
- **issue-reproducer-loop**: Convert issue reports into deterministic repro loops. Example: cap:issue-reproducer-loop. Status: native.
- **performance-optimization-pass**: Optimize hot paths and validate performance gains. Example: cap:performance-optimization-pass. Status: native.

## Docs and knowledge generation
- **cross-platform-capability-audit**: Audit capabilities across platforms with scoring. Example: cap:cross-platform-capability-audit. Status: native.
- **docs-knowledge-pack**: Generate implementation and validation documentation. Example: cap:docs-knowledge-pack. Status: native.
- **platform-parity-compare**: Compare platform capability parity for a workflow. Example: cap:platform-parity-compare. Status: native.

## Safety, approval, and permission models
- **safety-gated-execution**: Run risky operations with explicit gates and checks. Example: cap:safety-gated-execution. Status: native.

## Search and retrieval
- **repo-context-scout**: Map repository structure and dependency paths. Example: cap:repo-context-scout. Status: native.

## Shell and filesystem control
- **shell-investigation-loop**: Use iterative shell diagnostics and implementation loops. Example: cap:shell-investigation-loop. Status: native.

## Test generation and execution
- **test-regression-hardening**: Generate tests and run regression loops. Example: cap:test-regression-hardening. Status: native.

## Workflow automation and CI/CD hooks
- **ci-failure-triage**: Triage CI failures and implement fixes. Example: cap:ci-failure-triage. Status: composed.
- **migration-planning-pack**: Build migration plan with rollback and risk tiers. Example: cap:migration-planning-pack. Status: native.
- **workflow-automation-blueprint**: Design reusable automation blueprints and runbooks. Example: cap:workflow-automation-blueprint. Status: native.


## Status Legend

1. native: Directly executable with current tools/skills.
2. composed: Executable by chaining current tools/skills.
3. partial: Some steps need manual handling or extra infra.
4. external-only: Not internalizable with current boundaries.

## Best Alias Per Workflow

| Workflow | Recommended alias | Rationale |
|---|---|---|
| Audit | cross-platform-capability-audit | Best for source-backed platform comparisons. |
| Refactor | code-refactor-safety-pass | Best for safe structural changes. |
| Testing | test-regression-hardening | Best for regression-focused test loops. |
| Debug | debug-root-cause-sprint | Best for fast diagnosis and validation. |
| CI | ci-failure-triage | Best for failing check triage. |
| Docs | docs-knowledge-pack | Best for implementation summaries. |
| Integration | mcp-integration-setup | Best for tool ecosystem wiring. |
| Triage | issue-reproducer-loop | Best for repro-first bug resolution. |

## 20 Routing Validation Prompts

1. cap:agent-orchestration-sprint
2. cap:repo-context-scout
3. cap:cross-platform-capability-audit
4. cap:code-refactor-safety-pass
5. cap:test-regression-hardening
6. cap:shell-investigation-loop
7. cap:browser-ui-repro
8. cap:mcp-integration-setup
9. cap:api-integration-hardening
10. cap:debug-root-cause-sprint
11. cap:docs-knowledge-pack
12. cap:ci-failure-triage
13. cap:dependency-risk-audit
14. cap:migration-planning-pack
15. cap:pr-review-sweep
16. cap:issue-reproducer-loop
17. cap:memory-capture-brief
18. cap:safety-gated-execution
19. cap:performance-optimization-pass
20. cap:platform-parity-compare
