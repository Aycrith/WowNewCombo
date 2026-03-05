# Warning Reduction Backlog (2026-03-05)

## Baseline and Sprint 1 Outcome
- Baseline capture command: `dotnet clean MasterOfPuppets.sln --nologo -v quiet` then `dotnet build MasterOfPuppets.sln --nologo -v minimal`
- Baseline warning inventory: `41` unique warnings (`82` raw lines in build output).
- After Sprint 1: `32` unique warnings (`64` raw lines).
- Net change: `9` unique warnings resolved, `0` added.

## Sprint 1 Changes Landed
- Removed duplicate using in `Core/GoalsFactory/GoalFactory.cs` (`CS0105`).
- Removed dormant GOAP cache fields from `Core/GOAP/GoapPlanner.cs` (`CS0169`, `CS0414`).
- Removed nullable-context annotation warning in `PPather/Search/PPatherService.cs` (`CS8632`).
- Removed explicit default initializers and activated existing player-state event flow in `MockWoWClient/GameState/GameStateManager.cs` (`CA1805`, `CS0067`).

## Publish Point
- Sprint 1 baseline and audit/hardening batch published to `origin/dev` at commit `fa8b642de` on 2026-03-05.

## Deferred Backlog (Sprint 2+)
- `Core.csproj` nullability: `CS8600` in `LLMClientFactory`.
  - Rationale: requires contract-level nullability decisions around factory return paths.
- `CoreUnitTests.csproj` nullability: `CS8602`, `CS8625`, `CS8629`, `CS8767`.
  - Rationale: test helper signatures and fixtures need coordinated nullable cleanup.
- `Core.csproj` dead fields/events: `CS0649`, `CS0067`.
  - Rationale: most are instrumentation/telemetry placeholders in navigation; remove only after confirming no reflection/diagnostic consumers.
- Analyzer performance/documentation warnings: `CA1859`, `CA1869`, `CA1866`.
  - Rationale: requires API signature and allocation strategy review; not a pure mechanical edit.
- Generated regex migration: `SYSLIB1045` in core and unit tests.
  - Rationale: convert to `[GeneratedRegex]` in controlled batches to avoid behavior drift.

## Sprint 2 Target
- Prioritize nullable warnings with runtime risk (`CS860x`) before style/perf analyzers.
- Keep diagnostics route behavior unchanged and run full test gate after each batch.

## Sprint 2 Outcome (Conservative: Core + CoreUnitTests)
- Baseline (Sprint 2 start): `32` unique warnings.
- After Sprint 2: `20` unique warnings.
- Net change: `12` unique warnings resolved.
- Acceptance target met: `<= 20` unique warnings.

### Fixed Categories
- `CS8600` (Core): resolved in `Core/AI/LLM/LLMClientFactory.cs`.
- `CS8602` (CoreUnitTests): resolved in `CoreUnitTests/GoalsComponent/Blacklist/SmartBlacklistTests.cs`.
- `CS8625` (CoreUnitTests): resolved in
  - `CoreUnitTests/Analytics/FailureAnalyticsEngineTests.cs`
  - `CoreUnitTests/Integration/RouteRerouterVisualizationIntegrationTests.cs`
- `CS8629` (CoreUnitTests): resolved in `CoreUnitTests/Integration/BotFailureScenarioTests.cs`.
- `CS8767` (CoreUnitTests): resolved in `CoreUnitTests/Recovery/NoPlanRecoveryServiceTests.cs`.
- `CS0219` (CoreUnitTests): resolved in `CoreUnitTests/Path/RouteRerouterEvidenceTests.cs`.
- `CA1852` (CoreUnitTests): resolved in `CoreUnitTests/GoalsComponent/FollowRouteGoalRefillTests.cs`.

### Remaining Deferred (Sprint 3)
- `CS0649`, `CS0067` in navigation instrumentation fields/events.
- `CS0162` unreachable code warnings.
- `CA1859`, `CA1869`, `CA1866` analyzer/perf guidance.
- `SYSLIB1045` regex migration to `[GeneratedRegex]`.
