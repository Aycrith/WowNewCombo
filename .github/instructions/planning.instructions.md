---
applyTo: "docs/**/*.md"
---

# PRD/PRP/Task Generation Instructions

When generating planning documentation (PRDs, PRPs, Task breakdowns) for this project, follow these research-driven standards to ensure implementation-ready output.

## Research Phase Requirements

Before writing any specification:

1. **Codebase Analysis**
   - Search for similar patterns in existing code
   - Identify integration points in `Core/`, `PPather/`, `Frontend/`
   - Review existing tests in `CoreTests/` and `Benchmarks/`

2. **External Research**
   - Fetch official documentation for referenced technologies
   - Cite academic papers for algorithms (author, year, title)
   - Reference proven patterns from Microsoft docs, Red Blob Games, etc.

3. **Pattern Matching**
   - Follow existing patterns in `Core/Goals/` for new goals
   - Match `LocalGrindSessionDAO.cs` pattern for data persistence
   - Use `IHostedService` pattern for background services

## Document Structure

### PRD Sections
- Executive Summary (1 paragraph)
- User Stories (table with ID, Story, Acceptance Criteria)
- Functional Requirements (table with Priority, Implementation Notes)
- Non-Functional Requirements (table with Target, Validation Method)
- Technical Specifications (actual schemas, not descriptions)

### PRP Sections
- Proven Patterns (with source citations and code examples)
- Algorithm Specifications (formulas, complexity, parameters)
- Technology Decisions (matrix comparing alternatives)
- References (numbered, with URLs)

### Task Sections
- Phased breakdown with time estimates
- Specific file paths (`Core/NewFeature/ClassName.cs`)
- Code snippets showing exact changes with context
- Verification commands (`dotnet test --filter "..."`)
- Acceptance criteria checkboxes

## Code Example Standards

All code examples must:
- Be complete and compilable (not fragments)
- Follow project conventions from `AGENTS.md`
- Use file-scoped namespaces
- Include proper using statements
- Show 3-5 lines of context before/after changes

```csharp
// GOOD: Complete, contextual example
namespace Core.NewFeature;

public sealed class HazardAnalyzer
{
    private readonly ILogger<HazardAnalyzer> _logger;
    
    public HazardAnalyzer(ILogger<HazardAnalyzer> logger)
    {
        _logger = logger;
    }
    
    public float CalculateSeverity(HazardEvent evt)
    {
        // Implementation with actual logic
    }
}
```

```csharp
// BAD: Fragment without context
public float CalculateSeverity(...) { /* implement */ }
```

## Quality Checklist

Before finalizing any plan:
- [ ] All file paths are absolute or project-relative
- [ ] All code examples compile
- [ ] All time estimates are provided
- [ ] All verification commands are correct
- [ ] No placeholder values ("TODO", "TBD", "configure as needed")
- [ ] Dependencies listed with versions
- [ ] Rollback procedure documented
