# Phase 3: Coverage Gap Analysis Report

Generated: 2026-02-08 09:36:04

## Executive Summary

| Metric | Value |
|--------|-------|
| Classes with gaps | 504 |
| Total uncovered methods | 3098 |
| Test stubs generated | 2222 |

## Coverage Gaps by Priority

### Priority 1: __LoggerMessageGenerator

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Enumerate` |  |

### Priority 2: Core.Goals.AssistFocusGoal

- **Package:** Core
- **File:** Core\Goals\AssistFocusGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `OnEnter` |  |
| 4 | `OnExit` |  |
| 5 | `Update` |  |
| 6 | `.ctor` |  |

### Priority 3: Core.Goals.ApproachTargetGoal

- **Package:** Core
- **File:** Core\Goals\ApproachTargetGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `get_ApproachDurationMs` |  |
| 3 | `OnGoapEvent` |  |
| 4 | `OnEnter` |  |
| 5 | `OnExit` |  |
| 6 | `Update` |  |
| 7 | `NonCombatApproach` |  |
| 8 | `SetNextStuckTimeCheck` |  |
| 9 | `RandomJump` |  |
| 10 | `HasValidSoftInteract` |  |
| ... | *and 2 more* | |

### Priority 4: Core.Goals.AdhocNPCGoal

- **Package:** Core
- **File:** Core\Goals\AdhocNPCGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 28

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `MapRoute` |  |
| 3 | `PathingRoute` |  |
| 4 | `HasNext` |  |
| 5 | `NextMapPoint` |  |
| 6 | `get_LastActive` |  |
| 7 | `Dispose` |  |
| 8 | `CanRun` |  |
| 9 | `OnGoapEvent` |  |
| 10 | `Resume` |  |
| ... | *and 18 more* | |

### Priority 5: Core.Goals.AdhocGoal

- **Package:** Core
- **File:** Core\Goals\AdhocGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `OnEnter` |  |
| 4 | `Update` |  |
| 5 | `Interrupt` |  |
| 6 | `.ctor` |  |

### Priority 6: Core.Goals.TargetFinder

- **Package:** Core
- **File:** Core\GoalsComponent\TargetFinder.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_ElapsedMs` |  |
| 2 | `Reset` |  |
| 3 | `Search` |  |
| 4 | `LookForTarget` |  |
| 5 | `.ctor` |  |

### Priority 7: Core.Goals.StopMoving

- **Package:** Core
- **File:** Core\GoalsComponent\StopMoving.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Dispose` |  |
| 2 | `Stop` |  |
| 3 | `StopForward` |  |
| 4 | `StopTurn` |  |
| 5 | `.ctor` |  |

### Priority 8: Core.Goals.SafeSpotCollector

- **Package:** Core
- **File:** Core\GoalsComponent\SafeSpotCollector.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_MapLocations` |  |
| 2 | `Dispose` |  |
| 3 | `Update` |  |
| 4 | `Reduce` |  |
| 5 | `.ctor` |  |

### Priority 9: Core.Goals.NpcNameTargetingLocations

- **Package:** Core
- **File:** Core\GoalsComponent\NpcNameTargetingLocations.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Targeting` |  |
| 2 | `get_FindBy` |  |
| 3 | `.ctor` |  |

### Priority 10: Core.Goals.NpcNameTargeting

- **Package:** Core
- **File:** Core\GoalsComponent\NpcNameTargeting.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_NpcCount` |  |
| 2 | `get_Targeting` |  |
| 3 | `get_locFindBy` |  |
| 4 | `Dispose` |  |
| 5 | `ChangeNpcType` |  |
| 6 | `Reset` |  |
| 7 | `WaitForUpdate` |  |
| 8 | `FoundAny` |  |
| 9 | `AcquireNonBlacklisted` |  |
| 10 | `FindBy` |  |
| ... | *and 1 more* | |

### Priority 11: Core.Goals.PathResult

- **Package:** Core
- **File:** Core\GoalsComponent\Navigation\PathResult.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 12: Core.Goals.PathRequest

- **Package:** Core
- **File:** Core\GoalsComponent\Navigation\PathRequest.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 13: Core.Goals.Navigation/HeadingHistoryEntry

- **Package:** Core
- **File:** Core\GoalsComponent\Navigation.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Heading` |  |

### Priority 14: Core.Goals.Navigation

- **Package:** Core
- **File:** Core\GoalsComponent\Navigation.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 35

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `set_TotalRoute` |  |
| 2 | `get_LastActive` |  |
| 3 | `get_SimplifyRouteToWaypoint` |  |
| 4 | `Dispose` |  |
| 5 | `Update` |  |
| 6 | `Update` |  |
| 7 | `Resume` |  |
| 8 | `Stop` |  |
| 9 | `StopMovement` |  |
| 10 | `HasWaypoint` |  |
| ... | *and 25 more* | |

### Priority 15: Core.Goals.CursorScan

- **Package:** Core
- **File:** Core\GoalsComponent\CursorScan.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Dispose` |  |
| 2 | `Find` |  |
| 3 | `FindFrom` |  |
| 4 | `FindAny` |  |
| 5 | `FindAnyFrom` |  |
| 6 | `.ctor` |  |

### Priority 16: Core.Goals.CastResult_Extension

- **Package:** Core
- **File:** Core\GoalsComponent\CastResult.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 17: Core.Goals.CastingHandlerInterruptWatchdog

- **Package:** Core
- **File:** Core\GoalsComponent\CastingHandlerInterruptWatchdog.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Dispose` |  |
| 2 | `Watchdog` |  |
| 3 | `Set` |  |
| 4 | `.ctor` |  |

### Priority 18: Core.Goals.CastingHandler/<>c__DisplayClass27_0

- **Package:** Core
- **File:** Core\GoalsComponent\CastingHandler.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<WaitCurrentAction>g__Interrupt|0` |  |

### Priority 19: Core.Goals.CastingHandler

- **Package:** Core
- **File:** Core\GoalsComponent\CastingHandler.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 26

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SpellInQueue` |  |
| 2 | `_GCD` |  |
| 3 | `PressKeyAction` |  |
| 4 | `CastInstantSuccessful` |  |
| 5 | `WaitCurrentAction` |  |
| 6 | `CastInstant` |  |
| 7 | `CastCastbar` |  |
| 8 | `IsBandage` |  |
| 9 | `WaitTilUIErrorTimeChange` |  |
| 10 | `WaitTillNoLongerCastingOrChanneling` |  |
| ... | *and 16 more* | |

### Priority 20: Core.GOAP.GoapPlanner/Node

- **Package:** Core
- **File:** Core\GOAP\GoapPlanner.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 21: Core.GOAP.GoapPlanner

- **Package:** Core
- **File:** Core\GOAP\GoapPlanner.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Plan` |  |
| 2 | `BuildGraph` |  |
| 3 | `InState` |  |
| 4 | `InState` |  |
| 5 | `PopulateState` |  |
| 6 | `.cctor` |  |

### Priority 22: Core.GOAP.GoapKey_Extension

- **Package:** Core
- **File:** Core\GOAP\GoapKey.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringTrue` |  |
| 2 | `ToStringFalse` |  |
| 3 | `ToStringF` |  |

### Priority 23: Core.GOAP.GoapAgentState

- **Package:** Core
- **File:** Core\GOAP\GoapAgentState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_ShouldConsumeCorpse` |  |
| 2 | `get_LootableCorpseCount` |  |
| 3 | `get_GatherableCorpseCount` |  |
| 4 | `get_ConsumableCorpseCount` |  |
| 5 | `get_LastCombatKillCount` |  |
| 6 | `get_Gathering` |  |
| 7 | `get_RecentlyLooted` |  |

### Priority 24: Core.GOAP.GoapAgent

- **Package:** Core
- **File:** Core\GOAP\GoapAgent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 23

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Active` |  |
| 2 | `set_Active` |  |
| 3 | `get_WorldState` |  |
| 4 | `get_SessionStat` |  |
| 5 | `get_State` |  |
| 6 | `get_AvailableGoals` |  |
| 7 | `get_Plan` |  |
| 8 | `get_CurrentGoal` |  |
| 9 | `Dispose` |  |
| 10 | `GoapThread` |  |
| ... | *and 13 more* | |

### Priority 25: Core.GOAP.SkinCorpseEvent

- **Package:** Core
- **File:** Core\GOAP\Events\SkinCorpseEvent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_MapLoc` |  |
| 2 | `get_Radius` |  |
| 3 | `get_NpcId` |  |
| 4 | `.ctor` |  |

### Priority 26: Core.GOAP.ScreenCaptureEvent

- **Package:** Core
- **File:** Core\GOAP\Events\ScreenCaptureEvent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.cctor` |  |

### Priority 27: Core.GOAP.RemoveClosestPoi

- **Package:** Core
- **File:** Core\GOAP\Events\RemoveClosestPoi.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |
| 2 | `.ctor` |  |

### Priority 28: Core.GOAP.GoapStateEvent

- **Package:** Core
- **File:** Core\GOAP\Events\GoapStateEvent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Key` |  |
| 2 | `get_Value` |  |
| 3 | `.ctor` |  |

### Priority 29: Core.Goals.BlacklistTargetGoal

- **Package:** Core
- **File:** Core\Goals\BlacklistTargetGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `OnEnter` |  |
| 4 | `.ctor` |  |

### Priority 30: Core.Goals.CombatGoal

- **Package:** Core
- **File:** Core\Goals\CombatGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnGoapEvent` |  |
| 3 | `ResetCooldowns` |  |
| 4 | `OnEnter` |  |
| 5 | `OnExit` |  |
| 6 | `Update` |  |
| 7 | `GetMobCount` |  |
| 8 | `FindPossibleThreats` |  |
| 9 | `GetCorpseLocation` |  |
| 10 | `DealWithSoftInteract` |  |
| ... | *and 2 more* | |

### Priority 31: Core.Goals.CombatGoal/<>c__DisplayClass21_0

- **Package:** Core
- **File:** Core\Goals\CombatGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<Update>g__interrupt|0` |  |

### Priority 32: Core.Goals.CombatGoal/<>c__DisplayClass21_1

- **Package:** Core
- **File:** Core\Goals\CombatGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<Update>g__interrupt|1` |  |

### Priority 33: Core.Extensions.Phase1ServiceCollectionExtensions

- **Package:** Core
- **File:** Core\Extensions\Phase1ServiceCollectionExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AddPhase1Features` |  |
| 2 | `AddObjectPool` |  |

### Priority 34: Core.Extensions.HumanizationServiceCollectionExtensions

- **Package:** Core
- **File:** Core\Extensions\HumanizationServiceCollectionExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AddHumanizationServices` |  |

### Priority 35: Core.FeatureFlags.MonitoringThresholds

- **Package:** Core
- **File:** Core\FeatureFlags\FeatureFlagsOptions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_PathfindingLatencyWarningMs` |  |
| 2 | `get_PathfindingLatencyCriticalMs` |  |
| 3 | `get_HazardClusterCountWarning` |  |
| 4 | `get_HazardClusterCountCritical` |  |
| 5 | `get_LLMLatencyWarningMs` |  |
| 6 | `get_LLMLatencyCriticalMs` |  |
| 7 | `get_StuckRecoveryAttemptsWarning` |  |
| 8 | `get_StuckRecoveryAttemptsCritical` |  |
| 9 | `get_MemoryUsageMBWarning` |  |
| 10 | `get_MemoryUsageMBCritical` |  |
| ... | *and 2 more* | |

### Priority 36: Core.FeatureFlags.MonitoringOptions

- **Package:** Core
- **File:** Core\FeatureFlags\FeatureFlagsOptions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Enabled` |  |
| 2 | `get_MetricsIntervalSeconds` |  |
| 3 | `get_Thresholds` |  |

### Priority 37: Core.GoalsComponent.BreadcrumbStats

- **Package:** Core
- **File:** Core\GoalsComponent\BreadcrumbTracker.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Count` |  |
| 2 | `get_MaxSize` |  |
| 3 | `get_TotalRecorded` |  |
| 4 | `get_TotalSkipped` |  |
| 5 | `get_TotalDistance` |  |
| 6 | `get_OldestTimestamp` |  |
| 7 | `get_NewestTimestamp` |  |

### Priority 38: Core.Goals.WrongZoneGoal

- **Package:** Core
- **File:** Core\Goals\WrongZoneGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `get_LastActive` |  |
| 3 | `CanRun` |  |
| 4 | `Update` |  |
| 5 | `HasBeenActiveRecently` |  |
| 6 | `.ctor` |  |

### Priority 39: Core.Goals.WalkToCorpseGoal

- **Package:** Core
- **File:** Core\Goals\WalkToCorpseGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `get_LastActive` |  |
| 3 | `MapRoute` |  |
| 4 | `PathingRoute` |  |
| 5 | `HasNext` |  |
| 6 | `NextMapPoint` |  |
| 7 | `Dispose` |  |
| 8 | `OnGoapEvent` |  |
| 9 | `OnEnter` |  |
| 10 | `OnExit` |  |
| ... | *and 5 more* | |

### Priority 40: Core.Goals.WaitGoal

- **Package:** Core
- **File:** Core\Goals\WaitGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnEnter` |  |
| 3 | `Update` |  |
| 4 | `.ctor` |  |

### Priority 41: Core.Goals.WaitForGatheringGoal

- **Package:** Core
- **File:** Core\Goals\WaitForGatheringGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnEnter` |  |
| 3 | `OnExit` |  |
| 4 | `Update` |  |
| 5 | `CheckCastStarted` |  |
| 6 | `.ctor` |  |

### Priority 42: Core.Goals.CastState_Extension

- **Package:** Core
- **File:** Core\Goals\WaitForGatheringGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 43: Core.Goals.TargetPetTargetGoal

- **Package:** Core
- **File:** Core\Goals\TargetPetTargetGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `Update` |  |
| 4 | `.ctor` |  |

### Priority 44: Core.Goals.TargetLastDeadGoal

- **Package:** Core
- **File:** Core\Goals\TargetLastDeadGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `Update` |  |
| 3 | `.ctor` |  |

### Priority 45: Core.Goals.TargetFocusTargetGoal

- **Package:** Core
- **File:** Core\Goals\TargetFocusTargetGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `OnEnter` |  |
| 4 | `Update` |  |
| 5 | `OnExit` |  |
| 6 | `CanPull` |  |
| 7 | `.ctor` |  |

### Priority 46: Core.GOAP.FollowRouteChanged

- **Package:** Core
- **File:** Core\GOAP\Events\FollowRouteChanged.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.cctor` |  |

### Priority 47: Core.Goals.SkinningGoal

- **Package:** Core
- **File:** Core\Goals\SkinningGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 23

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `Dispose` |  |
| 3 | `CanRun` |  |
| 4 | `OnGoapEvent` |  |
| 5 | `OnEnter` |  |
| 6 | `OnExit` |  |
| 7 | `ExitSuccess` |  |
| 8 | `ExitInterruptOrFailed` |  |
| 9 | `ClearTargetIfExists` |  |
| 10 | `WhileNotCastingInteract` |  |
| ... | *and 13 more* | |

### Priority 48: Core.Goals.PullTargetGoal

- **Package:** Core
- **File:** Core\Goals\PullTargetGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `get_PullDurationMs` |  |
| 3 | `OnEnter` |  |
| 4 | `OnExit` |  |
| 5 | `OnGoapEvent` |  |
| 6 | `Update` |  |
| 7 | `DefaultApproach` |  |
| 8 | `ConditionalApproach` |  |
| 9 | `PullPrevention` |  |
| 10 | `EligibleEnemySoftTargetExists` |  |
| ... | *and 2 more* | |

### Priority 49: Core.Goals.ParallelGoal

- **Package:** Core
- **File:** Core\Goals\ParallelGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `None` |  |
| 3 | `CanRun` |  |
| 4 | `OnEnter` |  |
| 5 | `Update` |  |
| 6 | `OnExit` |  |
| 7 | `Cast` |  |
| 8 | `Execute` |  |
| 9 | `.ctor` |  |

### Priority 50: Core.Goals.NullGoal

- **Package:** Core
- **File:** Core\Goals\NullGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `.ctor` |  |

### Priority 51: Core.Goals.MailGoal

- **Package:** Core
- **File:** Core\Goals\MailGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 30

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `MapRoute` |  |
| 3 | `PathingRoute` |  |
| 4 | `HasNext` |  |
| 5 | `NextMapPoint` |  |
| 6 | `get_LastActive` |  |
| 7 | `Dispose` |  |
| 8 | `CanRun` |  |
| 9 | `OnGoapEvent` |  |
| 10 | `Resume` |  |
| ... | *and 20 more* | |

### Priority 52: Core.Goals.LootGoal

- **Package:** Core
- **File:** Core\Goals\LootGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 28

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnEnter` |  |
| 3 | `WaitForLosingTarget` |  |
| 4 | `CheckInventoryFull` |  |
| 5 | `TryLoot` |  |
| 6 | `HandleSuccessfulLoot` |  |
| 7 | `GatherCorpseIfNeeded` |  |
| 8 | `HandleFailedLoot` |  |
| 9 | `CleanUpAfterLooting` |  |
| 10 | `ClearTargetIfNeeded` |  |
| ... | *and 18 more* | |

### Priority 53: Core.Goals.GoapGoal

- **Package:** Core
- **File:** Core\Goals\GoapGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 14

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Preconditions` |  |
| 2 | `get_Effects` |  |
| 3 | `get_Keys` |  |
| 4 | `set_Keys` |  |
| 5 | `get_Name` |  |
| 6 | `get_DisplayName` |  |
| 7 | `SendGoapEvent` |  |
| 8 | `CanRun` |  |
| 9 | `OnEnter` |  |
| 10 | `OnExit` |  |
| ... | *and 4 more* | |

### Priority 54: Core.Goals.FollowRouteGoal

- **Package:** Core
- **File:** Core\Goals\FollowRouteGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 30

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `get_mapRoute` |  |
| 4 | `set_mapRoute` |  |
| 5 | `get_LastActive` |  |
| 6 | `MapRoute` |  |
| 7 | `PathingRoute` |  |
| 8 | `HasNext` |  |
| 9 | `NextMapPoint` |  |
| 10 | `Dispose` |  |
| ... | *and 20 more* | |

### Priority 55: Core.Goals.FollowFocusGoal

- **Package:** Core
- **File:** Core\Goals\FollowFocusGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnEnter` |  |
| 3 | `OnExit` |  |
| 4 | `Update` |  |
| 5 | `.ctor` |  |

### Priority 56: Core.Goals.FleeGoal

- **Package:** Core
- **File:** Core\Goals\FleeGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `get_LastActive` |  |
| 3 | `MapRoute` |  |
| 4 | `PathingRoute` |  |
| 5 | `HasNext` |  |
| 6 | `NextMapPoint` |  |
| 7 | `CanRun` |  |
| 8 | `OnEnter` |  |
| 9 | `OnExit` |  |
| 10 | `Update` |  |
| ... | *and 1 more* | |

### Priority 57: Core.Goals.DismountSubGoal

- **Package:** Core
- **File:** Core\Goals\DismountSubGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `Update` |  |
| 3 | `.ctor` |  |

### Priority 58: Core.Goals.CorpseConsumedGoal

- **Package:** Core
- **File:** Core\Goals\CorpseConsumedGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnEnter` |  |
| 3 | `.ctor` |  |

### Priority 59: Core.Goals.ConsumeCorpseGoal

- **Package:** Core
- **File:** Core\Goals\ConsumeCorpseGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `OnEnter` |  |
| 3 | `.ctor` |  |

### Priority 60: Core.Goals.ConditionalWaitGoal

- **Package:** Core
- **File:** Core\Goals\ConditionalWaitGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Cost` |  |
| 2 | `CanRun` |  |
| 3 | `OnEnter` |  |
| 4 | `Update` |  |
| 5 | `.ctor` |  |

### Priority 61: Core.Goals.PullTargetGoal/<>c__DisplayClass29_0

- **Package:** Core
- **File:** Core\Goals\PullTargetGoal.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<Update>g__interrupt|0` |  |

### Priority 62: Core.Extensions.CircuitBreakerFactory

- **Package:** Core
- **File:** Core\Extensions\Phase1ServiceCollectionExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetOrCreate` |  |
| 2 | `.ctor` |  |

### Priority 63: Core.GOAP.CorpseEvent

- **Package:** Core
- **File:** Core\GOAP\Events\CorpseEvent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_MapLoc` |  |
| 2 | `get_Radius` |  |
| 3 | `get_PlayerFacing` |  |
| 4 | `get_PlayerLocation` |  |
| 5 | `.ctor` |  |

### Priority 64: Core.Hazard.HazardServiceCollectionExtensions

- **Package:** Core
- **File:** Core\Hazard\HazardServiceCollectionExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AddHazardAvoidance` |  |

### Priority 65: Core.LLM.HybridLLMDecisionService

- **Package:** Core
- **File:** Core\LLM\HybridLLMDecisionService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `OnGoapEvent` |  |
| 2 | `HandleLLMDecision` |  |
| 3 | `IsNoPlanEvent` |  |
| 4 | `BuildGameStateContext` |  |
| 5 | `Dispose` |  |
| 6 | `.ctor` |  |

### Priority 66: Core.Minimap.MinimapRowOperation

- **Package:** Core
- **File:** Core\Minimap\MinimapRowOperation.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetRequiredBufferLength` |  |
| 2 | `Invoke` |  |
| 3 | `<Invoke>g__IsValidSquareLocation|15_0` |  |
| 4 | `<Invoke>g__IsMatch|15_1` |  |
| 5 | `.ctor` |  |

### Priority 67: Core.PathOptimization.PathSimplificationResult

- **Package:** Core
- **File:** Core\Navigation\PathSimplifier.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_SimplifiedPath` |  |
| 2 | `get_OriginalCount` |  |
| 3 | `get_SimplifiedCount` |  |
| 4 | `get_OriginalLength` |  |
| 5 | `get_SimplifiedLength` |  |
| 6 | `get_ReductionPercent` |  |
| 7 | `Create` |  |

### Priority 68: Core.PathOptimization.PathSimplifier

- **Package:** Core
- **File:** Core\Navigation\PathSimplifier.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Simplify` |  |
| 2 | `Simplify` |  |
| 3 | `CalculateReduction` |  |
| 4 | `SuggestTolerance` |  |
| 5 | `RamerDouglasPeucker` |  |
| 6 | `PerpendicularDistance` |  |
| 7 | `CalculatePathLength` |  |
| 8 | `ValidateSimplification` |  |

### Priority 69: Core.Performance.PooledObjectRental`1

- **Package:** Core
- **File:** Core\Performance\ObjectPool.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Value` |  |
| 2 | `Dispose` |  |
| 3 | `.ctor` |  |

### Priority 70: Core.Performance.ObjectPoolExtensions

- **Package:** Core
- **File:** Core\Performance\ObjectPool.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RentDisposable` |  |

### Priority 71: Core.Performance.ObjectPoolStats

- **Package:** Core
- **File:** Core\Performance\ObjectPool.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_CurrentSize` |  |
| 2 | `get_MaxSize` |  |
| 3 | `get_RentCount` |  |
| 4 | `get_ReturnCount` |  |
| 5 | `get_CreateCount` |  |
| 6 | `get_DiscardCount` |  |
| 7 | `get_HitRate` |  |
| 8 | `.ctor` |  |

### Priority 72: Core.Performance.ObjectPool`1

- **Package:** Core
- **File:** Core\Performance\ObjectPool.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Count` |  |
| 2 | `get_MaxSize` |  |
| 3 | `get_RentCount` |  |
| 4 | `get_ReturnCount` |  |
| 5 | `get_CreateCount` |  |
| 6 | `get_DiscardCount` |  |
| 7 | `get_HitRate` |  |
| 8 | `Rent` |  |
| 9 | `Return` |  |
| 10 | `Clear` |  |
| ... | *and 3 more* | |

### Priority 73: Core.Services.ProcessCleanupService

- **Package:** Core
- **File:** Core\Services\ProcessCleanupService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `StartAsync` |  |
| 2 | `StopAsync` |  |
| 3 | `OnApplicationStopping` |  |
| 4 | `PerformCleanup` |  |
| 5 | `Dispose` |  |
| 6 | `.ctor` |  |

### Priority 74: Core.Session.LocalGrindSessionDAO/<LoadAsync>d__3

- **Package:** Core
- **File:** Core\Session\LocalGrindSessionDAO.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 75: Core.Session.LocalGrindSessionDAO/<>c

- **Package:** Core
- **File:** Core\Session\LocalGrindSessionDAO.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<LoadAsync>b__3_1` |  |
| 2 | `<LoadAsync>b__3_2` |  |

### Priority 76: Core.Session.LocalGrindSessionDAO/<<LoadAsync>b__3_0>d

- **Package:** Core
- **File:** Core\Session\LocalGrindSessionDAO.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 77: Core.Session.LocalGrindSessionDAO

- **Package:** Core
- **File:** Core\Session\LocalGrindSessionDAO.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Save` |  |
| 2 | `.ctor` |  |

### Priority 78: Core.Session.GrindSessionHandler

- **Package:** Core
- **File:** Core\Session\GrindSessionHandler.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Start` |  |
| 2 | `Stop` |  |
| 3 | `Save` |  |
| 4 | `PeriodicSave` |  |
| 5 | `.ctor` |  |

### Priority 79: Core.Session.GrindSession

- **Package:** Core
- **File:** Core\Session\GrindSession.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 19

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_ExpList` |  |
| 2 | `get_SessionId` |  |
| 3 | `get_PathName` |  |
| 4 | `get_PlayerClass` |  |
| 5 | `get_SessionStart` |  |
| 6 | `get_SessionStartToLocalTime` |  |
| 7 | `get_SessionEnd` |  |
| 8 | `get_SessionEndToLocalTime` |  |
| 9 | `get_TotalTimeInMinutes` |  |
| 10 | `get_LevelFrom` |  |
| ... | *and 9 more* | |

### Priority 80: Core.Session.ExperienceProvider

- **Package:** Core
- **File:** Core\Session\ExperienceProvider.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Get` |  |
| 2 | `.cctor` |  |

### Priority 81: Core.Startup.WoWProcessLauncher/<WaitForProcessAsync>d__15

- **Package:** Core
- **File:** Core\Startup\WoWProcessLauncher.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 82: Core.Startup.WoWProcessLauncher/<WaitForCharacterInWorldAsync>d__16

- **Package:** Core
- **File:** Core\Startup\WoWProcessLauncher.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 83: Core.Startup.WoWProcessLauncher/<LaunchAsync>d__14

- **Package:** Core
- **File:** Core\Startup\WoWProcessLauncher.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 84: Core.Startup.WoWProcessLauncher

- **Package:** Core
- **File:** Core\Startup\WoWProcessLauncher.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Status` |  |
| 2 | `get_CurrentProcess` |  |
| 3 | `FindExistingProcess` |  |
| 4 | `IsRunning` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 85: Core.Startup.WoWPathFinder

- **Package:** Core
- **File:** Core\Startup\WoWPathFinder.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `FindInstallation` |  |
| 2 | `FindAllInstallations` |  |
| 3 | `ValidateAndCreateInstallation` |  |
| 4 | `FindFromRegistry` |  |
| 5 | `FindFromBattleNetConfig` |  |
| 6 | `.ctor` |  |
| 7 | `.cctor` |  |

### Priority 86: Core.Startup.StartupStateSnapshot

- **Package:** Core
- **File:** Core\Startup\StartupState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 10

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_CurrentStage` |  |
| 2 | `get_StatusMessage` |  |
| 3 | `get_WoWPath` |  |
| 4 | `get_IsWoWRunning` |  |
| 5 | `get_IsNavigationServerRunning` |  |
| 6 | `get_AddonsValidated` |  |
| 7 | `get_FramesConfigured` |  |
| 8 | `get_IsReady` |  |
| 9 | `get_ElapsedTime` |  |
| 10 | `.ctor` |  |

### Priority 87: Core.Startup.StartupState

- **Package:** Core
- **File:** Core\Startup\StartupState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 26

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_CurrentStage` |  |
| 2 | `set_CurrentStage` |  |
| 3 | `get_StatusMessage` |  |
| 4 | `set_StatusMessage` |  |
| 5 | `get_WoWInstallation` |  |
| 6 | `set_WoWInstallation` |  |
| 7 | `get_WoWProcess` |  |
| 8 | `set_WoWProcess` |  |
| 9 | `get_NavigationProcess` |  |
| 10 | `set_NavigationProcess` |  |
| ... | *and 16 more* | |

### Priority 88: Core.Startup.WoWInstallation

- **Package:** Core
- **File:** Core\Startup\StartupState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Path` |  |
| 2 | `get_ExecutablePath` |  |
| 3 | `get_ExecutableName` |  |
| 4 | `get_Version` |  |
| 5 | `get_HasDataToColorAddon` |  |
| 6 | `get_HasSecureButtonsXml` |  |
| 7 | `.ctor` |  |

### Priority 89: Core.Startup.StartupResult

- **Package:** Core
- **File:** Core\Startup\StartupStage.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_IsSuccess` |  |
| 2 | `get_FinalStage` |  |
| 3 | `get_Message` |  |
| 4 | `get_TotalDuration` |  |
| 5 | `get_StageResults` |  |
| 6 | `CreateSuccess` |  |
| 7 | `CreateFailure` |  |
| 8 | `.ctor` |  |

### Priority 90: Core.Startup.StageCompletedEventArgs

- **Package:** Core
- **File:** Core\Startup\StartupStage.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Stage` |  |
| 2 | `get_Result` |  |
| 3 | `get_Duration` |  |
| 4 | `.ctor` |  |

### Priority 91: Core.Startup.StageResult

- **Package:** Core
- **File:** Core\Startup\StartupStage.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Type` |  |
| 2 | `get_Message` |  |
| 3 | `get_Exception` |  |
| 4 | `get_IsSuccess` |  |
| 5 | `get_CanContinue` |  |
| 6 | `get_ShouldRetry` |  |
| 7 | `get_IsWaiting` |  |
| 8 | `Success` |  |
| 9 | `Skipped` |  |
| 10 | `Warning` |  |
| ... | *and 5 more* | |

### Priority 92: Core.LLM.HybridLLMDecisionService/<>c__DisplayClass10_0/<<QueryLLMWithCircuitBreaker>b__0>d

- **Package:** Core
- **File:** Core\LLM\HybridLLMDecisionService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 93: Core.LLM.HybridLLMDecisionService/<ExecuteAsync>d__8

- **Package:** Core
- **File:** Core\LLM\HybridLLMDecisionService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 94: Core.LLM.HybridLLMDecisionService/<QueryLLMWithCircuitBreaker>d__10

- **Package:** Core
- **File:** Core\LLM\HybridLLMDecisionService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 95: Core.LLM.LLMDecision

- **Package:** Core
- **File:** Core\LLM\ILLMClient.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_SuggestedAction` |  |
| 2 | `get_Reasoning` |  |
| 3 | `get_Confidence` |  |
| 4 | `get_Metadata` |  |
| 5 | `.ctor` |  |

### Priority 96: Core.Hazard.HazardAnalyticsBackgroundService/<RunSaveLoop>d__10

- **Package:** Core
- **File:** Core\Hazard\HazardAnalyticsBackgroundService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 97: Core.Hazard.HazardAnalyticsBackgroundService/<RunClusteringLoop>d__9

- **Package:** Core
- **File:** Core\Hazard\HazardAnalyticsBackgroundService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 98: Core.Hazard.HazardAnalyticsBackgroundService/<LoadExistingHazards>d__8

- **Package:** Core
- **File:** Core\Hazard\HazardAnalyticsBackgroundService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 99: Core.Hazard.HazardAnalyticsBackgroundService/<ExecuteAsync>d__7

- **Package:** Core
- **File:** Core\Hazard\HazardAnalyticsBackgroundService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 100: Core.Hazard.HazardAnalyticsBackgroundService

- **Package:** Core
- **File:** Core\Hazard\HazardAnalyticsBackgroundService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 101: Core.Humanization.ScheduledBreakService

- **Package:** Core
- **File:** Core\Humanization\ScheduledBreakService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_IsOnBreak` |  |
| 2 | `get_RemainingBreakTime` |  |
| 3 | `StartAsync` |  |
| 4 | `StopAsync` |  |
| 5 | `SkipBreak` |  |
| 6 | `ResetSession` |  |
| 7 | `OnTick` |  |
| 8 | `Dispose` |  |
| 9 | `.ctor` |  |

### Priority 102: Core.Humanization.HumanizedMousePath

- **Package:** Core
- **File:** Core\Humanization\HumanizedMousePath.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `BuildPath` |  |
| 2 | `BuildPath` |  |
| 3 | `BuildPathInternal` |  |
| 4 | `BuildControlPoints` |  |
| 5 | `EaseInOutQuad` |  |

### Priority 103: Core.Humanization.MetricsSnapshot

- **Package:** Core
- **File:** Core\Humanization\HumanizationMetrics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_SessionDuration` |  |
| 2 | `get_TotalKeyPresses` |  |
| 3 | `get_SessionKeyPresses` |  |
| 4 | `get_AverageKeyHoldTimeMs` |  |
| 5 | `get_TotalReactionDelays` |  |
| 6 | `get_AverageReactionDelayMs` |  |
| 7 | `get_TotalWaypointDelays` |  |
| 8 | `get_AverageWaypointDelayMs` |  |
| 9 | `get_TotalMouseMovements` |  |
| 10 | `get_SessionMouseMovements` |  |
| ... | *and 5 more* | |

### Priority 104: Core.Humanization.TimingSample

- **Package:** Core
- **File:** Core\Humanization\HumanizationMetrics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Timestamp` |  |

### Priority 105: Core.Humanization.HumanizationMetrics

- **Package:** Core
- **File:** Core\Humanization\HumanizationMetrics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RecordKeyPress` |  |
| 2 | `RecordReactionDelay` |  |
| 3 | `RecordWaypointDelay` |  |
| 4 | `RecordMouseMovement` |  |
| 5 | `RecordBreakStart` |  |
| 6 | `RecordBreakEnd` |  |
| 7 | `GetSnapshot` |  |
| 8 | `GetRecentAverageKeyHoldTime` |  |
| 9 | `GetRecentAverageReactionDelay` |  |
| 10 | `AddSample` |  |
| ... | *and 2 more* | |

### Priority 106: Core.Humanization.RiskFactor

- **Package:** Core
- **File:** Core\Humanization\DetectionRiskAnalyzer.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |

### Priority 107: Core.Humanization.RiskAssessment

- **Package:** Core
- **File:** Core\Humanization\DetectionRiskAnalyzer.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_OverallScore` |  |
| 2 | `get_Level` |  |
| 3 | `get_Factors` |  |
| 4 | `get_Timestamp` |  |
| 5 | `get_Recommendations` |  |

### Priority 108: Core.Humanization.DetectionRiskAnalyzer

- **Package:** Core
- **File:** Core\Humanization\DetectionRiskAnalyzer.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AnalyzeRisk` |  |
| 2 | `AnalyzeTimingRegularity` |  |
| 3 | `AnalyzeInputSecurityCoverage` |  |
| 4 | `AnalyzeSessionDuration` |  |
| 5 | `AnalyzeActionDensity` |  |
| 6 | `CalculateStandardDeviation` |  |
| 7 | `GenerateRecommendations` |  |
| 8 | `.ctor` |  |

### Priority 109: Core.Hazard.LocalHazardDAO/<LoadAllAsync>d__7

- **Package:** Core
- **File:** Core\Hazard\LocalHazardDAO.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 110: Core.Launch.LaunchReadinessService/<WaitForGreenAsync>d__12

- **Package:** Core
- **File:** Core\Launch\LaunchReadinessService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 111: Core.Launch.LaunchReadinessSnapshot

- **Package:** Core
- **File:** Core\Launch\LaunchReadinessModels.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_IsLaunchReady` |  |
| 2 | `get_CanStartBot` |  |
| 3 | `get_TimestampUtc` |  |
| 4 | `get_Checks` |  |
| 5 | `get_Overrides` |  |
| 6 | `.ctor` |  |

### Priority 112: Core.Launch.LaunchOverrideSnapshot

- **Package:** Core
- **File:** Core\Launch\LaunchReadinessModels.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_AllowStartWithWarnings` |  |
| 2 | `get_EmergencyBypassAll` |  |
| 3 | `get_Bypasses` |  |
| 4 | `.ctor` |  |

### Priority 113: Core.Launch.LaunchSubsystemBypass

- **Package:** Core
- **File:** Core\Launch\LaunchReadinessModels.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Enabled` |  |
| 2 | `get_Reason` |  |
| 3 | `get_TimestampUtc` |  |
| 4 | `get_Source` |  |
| 5 | `.ctor` |  |

### Priority 114: Core.Launch.LaunchSubsystemCheck

- **Package:** Core
- **File:** Core\Launch\LaunchReadinessModels.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Subsystem` |  |
| 2 | `get_Status` |  |
| 3 | `get_Title` |  |
| 4 | `get_Message` |  |
| 5 | `get_IsRequired` |  |
| 6 | `get_IsBlocking` |  |
| 7 | `get_TimestampUtc` |  |
| 8 | `get_FixHint` |  |
| 9 | `get_NavigateTo` |  |
| 10 | `get_IsOverridden` |  |
| ... | *and 1 more* | |

### Priority 115: Core.Launch.LaunchOverrideAuditEntry

- **Package:** Core
- **File:** Core\Launch\LaunchOverrideState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_TimestampUtc` |  |
| 2 | `get_Subsystem` |  |
| 3 | `get_Action` |  |
| 4 | `get_Enabled` |  |
| 5 | `get_Reason` |  |
| 6 | `get_Source` |  |
| 7 | `.ctor` |  |

### Priority 116: Core.Launch.LaunchOverrideState

- **Package:** Core
- **File:** Core\Launch\LaunchOverrideState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_AllowStartWithWarnings` |  |
| 2 | `get_EmergencyBypassAll` |  |
| 3 | `Snapshot` |  |
| 4 | `GetAudit` |  |
| 5 | `IsBypassed` |  |
| 6 | `TryGetBypass` |  |
| 7 | `Reset` |  |
| 8 | `SetAllowStartWithWarnings` |  |
| 9 | `SetEmergencyBypassAll` |  |
| 10 | `SetBypass` |  |
| ... | *and 3 more* | |

### Priority 117: Core.Launch.LaunchOptions

- **Package:** Core
- **File:** Core\Launch\LaunchOptions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 10

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_EvaluateTimeoutMs` |  |
| 2 | `get_AddonHandshakeMaxStalenessMs` |  |
| 3 | `get_AddonHandshakeTimeoutSeconds` |  |
| 4 | `get_NavigationHandshakeTimeoutSeconds` |  |
| 5 | `get_WoWAndAddonsTimeoutSeconds` |  |
| 6 | `get_FramesTimeoutSeconds` |  |
| 7 | `get_ProfileTimeoutSeconds` |  |
| 8 | `get_RouteTimeoutSeconds` |  |
| 9 | `get_KeybindsTimeoutSeconds` |  |
| 10 | `get_SlowCheckThresholdMs` |  |

### Priority 118: Core.Launch.LaunchAutoFixService/<ApplyRecommendedFixesAsync>d__11

- **Package:** Core
- **File:** Core\Launch\LaunchAutoFixService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 119: Core.Launch.LaunchAutoFixService

- **Package:** Core
- **File:** Core\Launch\LaunchAutoFixService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `EnsureAddonConfigRecommended` |  |
| 2 | `DetectInstalledAddonMismatch` |  |
| 3 | `TryReadInstalledCellSize` |  |
| 4 | `TryFixKeyBindings` |  |
| 5 | `.ctor` |  |

### Priority 120: Core.Launch.LaunchAutoFixResult

- **Package:** Core
- **File:** Core\Launch\LaunchAutoFixService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Success` |  |
| 2 | `get_RequiresRestart` |  |
| 3 | `get_Steps` |  |
| 4 | `.ctor` |  |

### Priority 121: Core.Launch.LaunchAutoFixStep

- **Package:** Core
- **File:** Core\Launch\LaunchAutoFixService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |
| 2 | `get_Status` |  |
| 3 | `get_Message` |  |
| 4 | `.ctor` |  |

### Priority 122: Core.Launch.BotStartGuard

- **Package:** Core
- **File:** Core\Launch\BotStartGuard.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 24

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Evaluate` |  |
| 2 | `CreateSnapshot` |  |
| 3 | `ApplyOverrides` |  |
| 4 | `IsBypassed` |  |
| 5 | `GetTimeoutTitle` |  |
| 6 | `GetTimeoutNavigateTo` |  |
| 7 | `Timed` |  |
| 8 | `InvalidateAddonValidation` |  |
| 9 | `PrimeAddonValidation` |  |
| 10 | `CheckNavigation` |  |
| ... | *and 14 more* | |

### Priority 123: Core.LLM.NullLLMClient

- **Package:** Core
- **File:** Core\LLM\NullLLMClient.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_IsAvailable` |  |
| 2 | `QueryAsync` |  |
| 3 | `.ctor` |  |

### Priority 124: Core.Launch.LaunchReadinessService

- **Package:** Core
- **File:** Core\Launch\LaunchReadinessService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_LastSnapshot` |  |
| 2 | `Evaluate` |  |
| 3 | `OnOverridesChanged` |  |
| 4 | `.ctor` |  |

### Priority 125: Core.Startup.StartupOrchestrator/<WaitForCharacterAsync>d__29

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 126: Core.Extensions.RegexExtension

- **Package:** Core
- **File:** Core\Extensions\RegexExtension.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Replace` |  |
| 2 | `ReplaceNamedGroup` |  |

### Priority 127: Core.Diagnostics.BindingDiagnostics

- **Package:** Core
- **File:** Core\Diagnostics\BindingDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RunDiagnostics` |  |
| 2 | `CheckClearTargetConfiguration` |  |
| 3 | `CheckGameBindings` |  |
| 4 | `CheckTargetState` |  |
| 5 | `TestTargetClearing` |  |
| 6 | `CheckBlacklistStatus` |  |
| 7 | `.ctor` |  |

### Priority 128: SharedLib.WorldMapAreaDB/<>c__DisplayClass15_0

- **Package:** SharedLib
- **File:** SharedLib\Data\WorldMapAreaDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<GetWorldMapArea>g__ContainsWorldPosAndMapId|0` |  |
| 2 | `<GetWorldMapArea>g__ByUIMapId|1` |  |

### Priority 129: SharedLib.TalentTreeElement

- **Package:** SharedLib
- **File:** SharedLib\Data\TalentTreeElement.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_TierID` |  |
| 2 | `get_ColumnIndex` |  |
| 3 | `get_TabID` |  |
| 4 | `get_SpellIds` |  |

### Priority 130: SharedLib.TalentTab

- **Package:** SharedLib
- **File:** SharedLib\Data\TalentTab.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Id` |  |
| 2 | `get_OrderIndex` |  |
| 3 | `get_ClassMask` |  |

### Priority 131: SharedLib.SubZoneArea

- **Package:** SharedLib
- **File:** SharedLib\Data\SubZoneArea.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Id` |  |
| 2 | `get_Min` |  |
| 3 | `get_Max` |  |
| 4 | `get_Center` |  |
| 5 | `get_MaxRange` |  |
| 6 | `Contains` |  |

### Priority 132: SharedLib.Spell

- **Package:** SharedLib
- **File:** SharedLib\Data\Spell.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Id` |  |
| 2 | `get_Name` |  |
| 3 | `get_Level` |  |

### Priority 133: SharedLib.Item

- **Package:** SharedLib
- **File:** SharedLib\Data\Item.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Entry` |  |
| 2 | `get_Name` |  |
| 3 | `get_Quality` |  |
| 4 | `get_SellPrice` |  |
| 5 | `get_TextureId` |  |

### Priority 134: SharedLib.Creature

- **Package:** SharedLib
- **File:** SharedLib\Data\Creature.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Entry` |  |
| 2 | `get_Name` |  |
| 3 | `get_SubName` |  |
| 4 | `get_Faction` |  |
| 5 | `get_MinLevel` |  |
| 6 | `get_MaxLevel` |  |
| 7 | `get_Rank` |  |
| 8 | `get_NpcFlag` |  |
| 9 | `get_SkinLoot` |  |
| 10 | `get_Family` |  |
| ... | *and 1 more* | |

### Priority 135: SharedLib.ClientVersion_Extension

- **Package:** SharedLib
- **File:** SharedLib\AddonDataProviderType\ClientVersion.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 136: PPather.Data.SphereEventArgs

- **Package:** PPather
- **File:** PPather\Data\SphereEventArgs.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Location` |  |
| 2 | `get_Colour` |  |
| 3 | `get_Name` |  |
| 4 | `.ctor` |  |

### Priority 137: PPather.Data.ScoreLoc

- **Package:** PPather
- **File:** PPather\Data\ScoreLoc.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Loc` |  |
| 2 | `get_Range` |  |
| 3 | `get_Score` |  |
| 4 | `.ctor` |  |

### Priority 138: PPather.Data.LinesEventArgs

- **Package:** PPather
- **File:** PPather\Data\LinesEventArgs.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Locations` |  |
| 2 | `get_Colour` |  |
| 3 | `get_Name` |  |
| 4 | `.ctor` |  |

### Priority 139: PPather.Data.LineArgs

- **Package:** PPather
- **File:** PPather\Data\LineArgs.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |
| 2 | `get_Spots` |  |
| 3 | `get_Colour` |  |
| 4 | `get_MapId` |  |
| 5 | `.ctor` |  |

### Priority 140: PPather.Extensions.BinaryReaderExtensions

- **Package:** PPather
- **File:** PPather\Extensions\BinaryReaderExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ReadVector3` |  |
| 2 | `ReadVector3_XZY` |  |
| 3 | `EOF` |  |

### Priority 141: PPather.Graph.SpotExtensions

- **Package:** PPather
- **File:** PPather\Graph\SpotExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToVecArray` |  |

### Priority 142: PPather.Graph.SearchLocation

- **Package:** PPather
- **File:** PPather\Graph\SearchLocation.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Location` |  |
| 2 | `get_Description` |  |
| 3 | `.ctor` |  |

### Priority 143: PPather.Graph.Path

- **Package:** PPather
- **File:** PPather\Graph\Path.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_locations` |  |
| 2 | `get_GetFirst` |  |
| 3 | `get_GetLast` |  |
| 4 | `Add` |  |
| 5 | `.ctor` |  |

### Priority 144: PPather.Graph.GraphChunk

- **Package:** PPather
- **File:** PPather\Graph\GraphChunk.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 10

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Clear` |  |
| 2 | `LocalCoords` |  |
| 3 | `GetSpot2D` |  |
| 4 | `GetSpot` |  |
| 5 | `AddSpot` |  |
| 6 | `GetAllSpots` |  |
| 7 | `Index` |  |
| 8 | `Load` |  |
| 9 | `Save` |  |
| 10 | `.ctor` |  |

### Priority 145: PPather.Triangles.GameV2.Wdt

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Wdt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Mver` |  |
| 2 | `Mphd` |  |
| 3 | `Main` |  |
| 4 | `.ctor` |  |

### Priority 146: PPather.Triangles.GameV2.MAIN

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Wdt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Item` |  |

### Priority 147: PPather.Triangles.GameV2.Tri

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Tri.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 148: PPather.Triangles.GameV2.Structure

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Structure.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 16

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Mutex` |  |
| 2 | `get_Verts` |  |
| 3 | `get_Tris` |  |
| 4 | `get_TriTypes` |  |
| 5 | `get_bbMin` |  |
| 6 | `get_bbMax` |  |
| 7 | `GetVertsFlat` |  |
| 8 | `GetTrisFlat` |  |
| 9 | `GetAreaIds` |  |
| 10 | `AddVert` |  |
| ... | *and 6 more* | |

### Priority 149: PPather.Triangles.GameV2.MH2O

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\MH2O.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Item` |  |
| 2 | `GetLiquidCellOffset` |  |
| 3 | `GetLiquidCell` |  |
| 4 | `GetInstance` |  |
| 5 | `GetAttributes` |  |
| 6 | `GetRenderMask` |  |
| 7 | `GetLiquidHeight` |  |
| 8 | `.cctor` |  |

### Priority 150: PPather.Triangles.GameV2.Adt

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Adt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 19

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Data` |  |
| 2 | `get_Size` |  |
| 3 | `Mver` |  |
| 4 | `Mhdr` |  |
| 5 | `GetSub` |  |
| 6 | `Mcin` |  |
| 7 | `Mh2o` |  |
| 8 | `Mmdx` |  |
| 9 | `Mmid` |  |
| 10 | `Mddf` |  |
| ... | *and 9 more* | |

### Priority 151: PPather.Triangles.GameV2.MCIN

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Adt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Item` |  |

### Priority 152: PPather.Triangles.GameV2.MCNK

- **Package:** PPather
- **File:** PPather\Triangles\GameV2\Adt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToVector3` |  |
| 2 | `IsHole` |  |

### Priority 153: PPather.Triangles.Triangle`1

- **Package:** PPather
- **File:** PPather\Triangles\Data\Triangle.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_V0` |  |

### Priority 154: PPather.TriangleType_Ext

- **Package:** PPather
- **File:** PPather\Triangles\Data\TriangleType.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Has` |  |
| 2 | `ToIndex` |  |

### Priority 155: SharedLib.ModifierKeyExtensions

- **Package:** SharedLib
- **File:** SharedLib\ModifierKey.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `FromEncodedValue` |  |
| 2 | `ToEncodedValue` |  |
| 3 | `ToPrefix` |  |
| 4 | `ParseKeyString` |  |

### Priority 156: SharedLib.NpcResetEvent

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\INpcResetEvent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_WaitHandle` |  |
| 2 | `Dispose` |  |
| 3 | `ChangeReset` |  |
| 4 | `ChangeSet` |  |
| 5 | `.ctor` |  |

### Priority 157: SharedLib.StartupClientVersion

- **Package:** SharedLib
- **File:** SharedLib\StartupConfig\StartupClientVersion.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Version` |  |
| 2 | `get_Path` |  |
| 3 | `DetectAnniversaryPath` |  |
| 4 | `.ctor` |  |

### Priority 158: SharedLib.StartupConfigDiagnostics

- **Package:** SharedLib
- **File:** SharedLib\StartupConfig\StartupConfigDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Enabled` |  |
| 2 | `.ctor` |  |

### Priority 159: WinAPI.NaturalStringComparer

- **Package:** WinAPI
- **File:** WinAPI\StringOrderUtil.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Compare` |  |

### Priority 160: WinAPI.NativeMethods

- **Package:** WinAPI
- **File:** WinAPI\NativeMethods.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 16

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MakeLParam` |  |
| 2 | `GetVirtualKeyForCharacter` |  |
| 3 | `IsLayoutDependentKey` |  |
| 4 | `GetCharacterForUSKey` |  |
| 5 | `MakeKeyDownLParam` |  |
| 6 | `MakeKeyUpLParam` |  |
| 7 | `GetScanCode` |  |
| 8 | `IsExtendedKey` |  |
| 9 | `IsWindowedMode` |  |
| 10 | `GetPosition` |  |
| ... | *and 6 more* | |

### Priority 161: WinAPI.ExecutablePath

- **Package:** WinAPI
- **File:** WinAPI\ExecutablePath.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Get` |  |
| 2 | `GetViaOpenProcess` |  |
| 3 | `GetViaMainModule` |  |

### Priority 162: WinAPI.SafeNativeMethods

- **Package:** WinAPI
- **File:** WinAPI\obj\Debug\net10.0\Microsoft.Interop.LibraryImportGenerator\Microsoft.Interop.LibraryImportGenerator\LibraryImports.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `StrCmpLogicalW` |  |

### Priority 163: WinAPI.NativeMethods

- **Package:** WinAPI
- **File:** WinAPI\obj\Debug\net10.0\Microsoft.Interop.LibraryImportGenerator\Microsoft.Interop.LibraryImportGenerator\LibraryImports.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetCursorInfo` |  |
| 2 | `DrawIconEx` |  |
| 3 | `DrawIcon` |  |
| 4 | `SetForegroundWindow` |  |
| 5 | `PostMessage` |  |
| 6 | `SetCursorPos` |  |
| 7 | `GetCursorPos` |  |
| 8 | `GetWindowThreadProcessId` |  |
| 9 | `GetClientRect` |  |
| 10 | `ClientToScreen` |  |
| ... | *and 2 more* | |

### Priority 164: WinAPI.ExecutablePath

- **Package:** WinAPI
- **File:** WinAPI\obj\Debug\net10.0\Microsoft.Interop.LibraryImportGenerator\Microsoft.Interop.LibraryImportGenerator\LibraryImports.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `OpenProcess` |  |
| 2 | `CloseHandle` |  |
| 3 | `QueryFullProcessImageNameW` |  |

### Priority 165: SharedLib.Converters.Vector4Converter

- **Package:** SharedLib
- **File:** SharedLib\Converters\Vector4Converter.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CanConvert` |  |
| 2 | `Read` |  |
| 3 | `Write` |  |

### Priority 166: SharedLib.Data.NpcFlagsExtensions

- **Package:** SharedLib
- **File:** SharedLib\Data\NpcFlags.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |
| 2 | `Has` |  |

### Priority 167: SharedLib.Data.FactionTemplate

- **Package:** SharedLib
- **File:** SharedLib\Data\FactionTemplate.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Id` |  |
| 2 | `get_FriendGroup` |  |

### Priority 168: SharedLib.Extensions.VectorExt

- **Package:** SharedLib
- **File:** SharedLib\Extensions\VectorExt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `FromList` |  |
| 2 | `MapDistanceXYTo` |  |
| 3 | `MapDistanceXY` |  |
| 4 | `WorldDistanceXYTo` |  |
| 5 | `WorldDistanceXY` |  |
| 6 | `ShortenRouteFromLocation` |  |
| 7 | `GetClosestPointOnLineSegment` |  |
| 8 | `TotalDistance` |  |
| 9 | `Deconstruct` |  |
| 10 | `Deconstruct` |  |
| ... | *and 1 more* | |

### Priority 169: SharedLib.Extensions.RectangleExt

- **Package:** SharedLib
- **File:** SharedLib\Extensions\RectangleExt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Centre` |  |
| 2 | `Max` |  |
| 3 | `BottomCentre` |  |

### Priority 170: SharedLib.Extensions.PointExt

- **Package:** SharedLib
- **File:** SharedLib\Extensions\PointExt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Scale` |  |
| 2 | `Scale` |  |
| 3 | `SqrDistance` |  |

### Priority 171: SharedLib.Extensions.ImageSharpRectangleExt

- **Package:** SharedLib
- **File:** SharedLib\Extensions\ImageSharpRectangleExt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Centre` |  |
| 2 | `Max` |  |
| 3 | `BottomCentre` |  |

### Priority 172: PPather.SearchParam

- **Package:** PPather
- **File:** PPather\Search\SearchParam.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Continent` |  |
| 2 | `get_SearchType` |  |
| 3 | `get_From` |  |
| 4 | `get_To` |  |

### Priority 173: SharedLib.Extensions.ImageSharpPointExt

- **Package:** SharedLib
- **File:** SharedLib\Extensions\ImageSharpPointExt.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Scale` |  |
| 2 | `Scale` |  |
| 3 | `SqrDistance` |  |

### Priority 174: SharedLib.Extensions.EnumExtensions

- **Package:** SharedLib
- **File:** SharedLib\Extensions\EnumExtension.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetFlags` |  |
| 2 | `GetIndividualFlags` |  |
| 3 | `GetFlags` |  |

### Priority 175: SharedLib.NpcFinder.SearchMode_Extension

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\SearchMode.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 176: SharedLib.NpcFinder.NpcPositionComparer

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\NpcPositionComparer.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Compare` |  |
| 2 | `.ctor` |  |

### Priority 177: SharedLib.NpcFinder.NpcPosition

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\NpcPosition.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Empty` |  |
| 2 | `.ctor` |  |
| 3 | `.ctor` |  |
| 4 | `.cctor` |  |

### Priority 178: SharedLib.NpcFinder.NpcNames_Extension

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\NpcNames.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |
| 2 | `HasFlagF` |  |

### Priority 179: SharedLib.NpcFinder.NpcNameFinder

- **Package:** SharedLib
- **File:** SharedLib\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogTypeChanged` |  |
| 2 | `.cctor` |  |

### Priority 180: SharedLib.NpcFinder.NpcNameFinder

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\NpcNameFinder.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 49

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `set_nameType` |  |
| 2 | `get_Npcs` |  |
| 3 | `get_NpcCount` |  |
| 4 | `set_AddCount` |  |
| 5 | `set_TargetCount` |  |
| 6 | `get_MobsVisible` |  |
| 7 | `get_PotentialAddsExist` |  |
| 8 | `_PotentialAddsExist` |  |
| 9 | `get_LastPotentialAddsSeen` |  |
| 10 | `get_WidthDiff` |  |
| ... | *and 39 more* | |

### Priority 181: SharedLib.NpcFinder.LineSegmentOperation

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\LineSegmentOperation.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetRequiredBufferLength` |  |
| 2 | `Invoke` |  |
| 3 | `.ctor` |  |

### Priority 182: SharedLib.NpcFinder.LineSegment

- **Package:** SharedLib
- **File:** SharedLib\NpcFinder\LineSegment.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_XStart` |  |
| 2 | `get_XEnd` |  |
| 3 | `get_XCenter` |  |
| 4 | `.ctor` |  |

### Priority 183: SharedLib.StartupConfigReader

- **Package:** SharedLib
- **File:** SharedLib\StartupConfig\StartupConfigReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Type` |  |
| 2 | `get_ReaderType` |  |
| 3 | `.ctor` |  |

### Priority 184: SharedLib.StartupConfigPid

- **Package:** SharedLib
- **File:** SharedLib\StartupConfig\StartupConfigPid.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Id` |  |
| 2 | `.ctor` |  |

### Priority 185: SharedLib.StartupConfigPathing

- **Package:** SharedLib
- **File:** SharedLib\StartupConfig\StartupConfigPathing.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Type` |  |
| 2 | `get_Mode` |  |
| 3 | `get_hostv1` |  |
| 4 | `get_portv1` |  |
| 5 | `get_hostv3` |  |
| 6 | `get_portv3` |  |
| 7 | `get_PathVisualizer` |  |
| 8 | `.ctor` |  |
| 9 | `.ctor` |  |

### Priority 186: SharedLib.StartupConfigNpcOverlay

- **Package:** SharedLib
- **File:** SharedLib\StartupConfig\StartupConfigNpcOverlay.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Enabled` |  |
| 2 | `get_ShowTargeting` |  |
| 3 | `get_ShowSkinning` |  |
| 4 | `get_ShowTargetVsAdd` |  |
| 5 | `.ctor` |  |

### Priority 187: SharedLib.Extensions.EnumExtensions/<GetFlagValues>d__3

- **Package:** SharedLib
- **File:** SharedLib\Extensions\EnumExtension.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 188: Core.Extensions.ServiceCollectionExtension

- **Package:** Core
- **File:** Core\Extensions\ServiceCollectionExtension.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ForwardSingleton` |  |
| 2 | `ForwardSingleton` |  |
| 3 | `ForwardSingleton` |  |
| 4 | `<ForwardSingleton>g__GetRequired|0_0` |  |
| 5 | `<ForwardSingleton>g__GetRequired|2_0` |  |
| 6 | `<ForwardSingleton>g__GetRequired|3_0` |  |

### Priority 189: PPather.Search

- **Package:** PPather
- **File:** PPather\Search\Search.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_PathGraph` |  |
| 2 | `get_MapId` |  |
| 3 | `get_From` |  |
| 4 | `get_Target` |  |
| 5 | `Clear` |  |
| 6 | `CreateWorldLocation` |  |
| 7 | `GetZValueAt` |  |
| 8 | `CreatePathGraph` |  |
| 9 | `DoSearch` |  |
| 10 | `GetAreaIdAndZ` |  |
| ... | *and 2 more* | |

### Priority 190: PPather.MeshFactory

- **Package:** PPather
- **File:** PPather\Search\MeshFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CreatePoints` |  |
| 2 | `CreateTriangles` |  |

### Priority 191: Game.InputWindowsNative

- **Package:** Game
- **File:** Game\Input\InputWindowsNative.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 24

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `DelayTime` |  |
| 2 | `TranslateKeyForLayout` |  |
| 3 | `PressModifiersDownPostMessage` |  |
| 4 | `ReleaseModifiersUpPostMessage` |  |
| 5 | `PressModifiersDownHybrid` |  |
| 6 | `ReleaseModifiersUpHybrid` |  |
| 7 | `SendModifierKey` |  |
| 8 | `CreateModifierInput` |  |
| 9 | `EnsureForegroundFocus` |  |
| 10 | `EmitWmCharIfPrintable` |  |
| ... | *and 14 more* | |

### Priority 192: Core.Addon.GameObject

- **Package:** Core
- **File:** Core\Addon\GameObjects\GameObject.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `IsHerb` |  |
| 2 | `IsMineral` |  |
| 3 | `IsMailbox` |  |

### Priority 193: Core.Addon.ConfigAddonReader

- **Package:** Core
- **File:** Core\Addon\ConfigAddonReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_AvgUpdateLatency` |  |
| 2 | `get_TargetName` |  |
| 3 | `get_DataReady` |  |
| 4 | `FullReset` |  |
| 5 | `Update` |  |
| 6 | `UpdateUI` |  |
| 7 | `SessionReset` |  |
| 8 | `.ctor` |  |

### Priority 194: Core.CombatRotation.TankRoleStrategy

- **Package:** Core
- **File:** Core\CombatRotation\TankRoleStrategy.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_RoleName` |  |
| 2 | `ScoreAbility` |  |
| 3 | `.ctor` |  |

### Priority 195: Core.CombatRotation.ScoreConditionRuntime

- **Package:** Core
- **File:** Core\CombatRotation\ScoreConditionRuntime.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Requirement` |  |
| 2 | `get_Bonus` |  |

### Priority 196: Core.CombatRotation.RotationMetricsCollector

- **Package:** Core
- **File:** Core\CombatRotation\RotationMetricsCollector.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 10

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_CurrentSession` |  |
| 2 | `RecordOptimizedTick` |  |
| 3 | `RecordFallbackTick` |  |
| 4 | `RecordCastAttempt` |  |
| 5 | `StartAsync` |  |
| 6 | `StopAsync` |  |
| 7 | `FlushMetrics` |  |
| 8 | `Dispose` |  |
| 9 | `.ctor` |  |
| 10 | `.cctor` |  |

### Priority 197: Core.CombatRotation.RoleStrategyHelpers

- **Package:** Core
- **File:** Core\CombatRotation\RoleStrategyHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `EvaluateScoreConditions` |  |

### Priority 198: Core.CombatRotation.HealerRoleStrategy

- **Package:** Core
- **File:** Core\CombatRotation\HealerRoleStrategy.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_RoleName` |  |
| 2 | `ScoreAbility` |  |
| 3 | `CalculateTriageBonus` |  |
| 4 | `CalculateManaEfficiencyBonus` |  |
| 5 | `CalculateHoTBonus` |  |
| 6 | `CalculatePreventionBonus` |  |
| 7 | `IsPowerfulCooldown` |  |
| 8 | `.ctor` |  |

### Priority 199: Core.CombatRotation.CombatRotationServiceExtensions

- **Package:** Core
- **File:** Core\CombatRotation\CombatRotationServiceExtensions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AddCombatRotationOptimizer` |  |

### Priority 200: Core.CombatRotation.AbilityClassifier

- **Package:** Core
- **File:** Core\CombatRotation\AbilityClassifier.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Classify` |  |
| 2 | `ContainsOrdinalIgnoreCase` |  |
| 3 | `.cctor` |  |

### Priority 201: Core.Database.TalentDB

- **Package:** Core
- **File:** Core\Database\TalentDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LoadJsonSafe` |  |
| 2 | `GetTalentTreesForClass` |  |
| 3 | `Update` |  |
| 4 | `.ctor` |  |

### Priority 202: Core.Database.SpellDB

- **Package:** Core
- **File:** Core\Database\SpellDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Spells` |  |
| 2 | `.ctor` |  |

### Priority 203: Core.Database.NpcSearchResult

- **Package:** Core
- **File:** Core\Database\NpcSearchResult.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Creature` |  |

### Priority 204: Core.Database.MailboxDB

- **Package:** Core
- **File:** Core\Database\MailboxDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetNearestMailbox` |  |
| 2 | `GetMailboxesWithinRange` |  |
| 3 | `EnsureMailboxesLoaded` |  |
| 4 | `LoadMailboxes` |  |
| 5 | `.ctor` |  |

### Priority 205: Core.Database.ItemDB/<GetFoodTextures>d__17

- **Package:** Core
- **File:** Core\Database\ItemDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 206: Core.Database.ItemDB/<GetDrinkTextures>d__18

- **Package:** Core
- **File:** Core\Database\ItemDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 207: Core.Database.ItemDB

- **Package:** Core
- **File:** Core\Database\ItemDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 10

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_EmptyItem` |  |
| 2 | `get_Items` |  |
| 3 | `get_FoodIds` |  |
| 4 | `get_DrinkIds` |  |
| 5 | `LoadIntArraySafe` |  |
| 6 | `TryGetTexture` |  |
| 7 | `GetItemIconName` |  |
| 8 | `GetItemIconUrl` |  |
| 9 | `.ctor` |  |
| 10 | `.cctor` |  |

### Priority 208: Core.Database.IconDB

- **Package:** Core
- **File:** Core\Database\IconDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 18

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Region` |  |
| 2 | `set_Region` |  |
| 3 | `get_IconToSpells` |  |
| 4 | `get_IconNames` |  |
| 5 | `GetSpellIds` |  |
| 6 | `SpellUsesTexture` |  |
| 7 | `SpellNameUsesTexture` |  |
| 8 | `GetBaseSpellName` |  |
| 9 | `GetSpellNamesForDisplay` |  |
| 10 | `TryGetIconName` |  |
| ... | *and 8 more* | |

### Priority 209: Core.Database.FactionTemplateDB

- **Package:** Core
- **File:** Core\Database\FactionTemplateDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Factions` |  |
| 2 | `.ctor` |  |

### Priority 210: Core.Database.CreatureDB

- **Package:** Core
- **File:** Core\Database\CreatureDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Entries` |  |
| 2 | `.ctor` |  |

### Priority 211: Core.Database.AreaDB

- **Package:** Core
- **File:** Core\Database\AreaDB.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `set_NpcWorldLocations` |  |
| 2 | `set_CurrentArea` |  |
| 3 | `set_CurrentWorldMapArea` |  |
| 4 | `set_Hitbox` |  |
| 5 | `Dispose` |  |
| 6 | `Update` |  |
| 7 | `ReadArea` |  |
| 8 | `GetByNpcFlag` |  |
| 9 | `GetNearestNpcs` |  |
| 10 | `FriendlyToPlayer` |  |
| ... | *and 3 more* | |

### Priority 212: Core.Diagnostics.DiagnosticCheck

- **Package:** Core
- **File:** Core\Diagnostics\SystemDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |

### Priority 213: Core.Diagnostics.DiagnosticResult

- **Package:** Core
- **File:** Core\Diagnostics\SystemDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |
| 2 | `get_Status` |  |
| 3 | `get_Message` |  |
| 4 | `get_Recommendation` |  |
| 5 | `get_Details` |  |
| 6 | `get_Exception` |  |

### Priority 214: Core.Diagnostics.DiagnosticReport

- **Package:** Core
- **File:** Core\Diagnostics\SystemDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Timestamp` |  |
| 2 | `get_OverallStatus` |  |
| 3 | `get_IsHealthy` |  |
| 4 | `get_Checks` |  |

### Priority 215: Core.Diagnostics.SystemDiagnostics/<RunFullDiagnosticsAsync>d__3

- **Package:** Core
- **File:** Core\Diagnostics\SystemDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 216: Core.Diagnostics.SystemDiagnostics/<CheckNavigationServerAsync>d__5

- **Package:** Core
- **File:** Core\Diagnostics\SystemDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 217: Core.Diagnostics.SystemDiagnostics

- **Package:** Core
- **File:** Core\Diagnostics\SystemDiagnostics.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RegisterCheck` |  |
| 2 | `CheckWoWProcess` |  |
| 3 | `CheckAddonInstallation` |  |
| 4 | `CheckPortStatus` |  |
| 5 | `GetProcessUsingPort` |  |
| 6 | `LogResult` |  |
| 7 | `DetermineOverallStatus` |  |
| 8 | `.ctor` |  |

### Priority 218: Game.WowProcessInput

- **Package:** Game
- **File:** Game\Input\WowProcessInput.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 26

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_ForwardKey` |  |
| 2 | `get_BackwardKey` |  |
| 3 | `get_TurnLeftKey` |  |
| 4 | `get_TurnRightKey` |  |
| 5 | `get_InteractMouseover` |  |
| 6 | `get_InteractMouseoverModifier` |  |
| 7 | `get_InteractMouseoverPress` |  |
| 8 | `Reset` |  |
| 9 | `KeyDown` |  |
| 10 | `KeyUp` |  |
| ... | *and 16 more* | |

### Priority 219: Game.WowProcessInput

- **Package:** Game
- **File:** Game\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogKeyDown` |  |
| 2 | `LogKeyUp` |  |
| 3 | `LogKeyPressFixed` |  |
| 4 | `LogKeyPressRandom` |  |
| 5 | `LogKeyPressRandomWithModifier` |  |
| 6 | `LogMoveKeyDown` |  |
| 7 | `LogMoveKeyUp` |  |
| 8 | `LogMoveKeyPress` |  |
| 9 | `.cctor` |  |

### Priority 220: Game.WowProcessInput/__LogKeyPressRandomWithModifierStruct

- **Package:** Game
- **File:** Game\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 221: Game.WowProcessInput/__LogKeyPressRandomWithModifierStruct/<GetEnumerator>d__10

- **Package:** Game
- **File:** Game\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 222: PPather.DrawWorldPathRequest

- **Package:** PPather
- **File:** PPather\Data\DrawPathDtos.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_mapId` |  |

### Priority 223: PPather.DrawMapPathRequest

- **Package:** PPather
- **File:** PPather\Data\DrawPathDtos.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_uiMapId` |  |

### Priority 224: StormDll.StormDllx86

- **Package:** PPather
- **File:** PPather\obj\Debug\net10.0\Microsoft.Interop.LibraryImportGenerator\Microsoft.Interop.LibraryImportGenerator\LibraryImports.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SFileOpenArchive` |  |
| 2 | `SFileCloseArchive` |  |
| 3 | `SFileReadFile` |  |
| 4 | `SFileCloseFile` |  |
| 5 | `SFileGetFileSize` |  |
| 6 | `SFileSetFilePointer` |  |
| 7 | `SFileOpenFileEx` |  |

### Priority 225: StormDll.StormDllx64

- **Package:** PPather
- **File:** PPather\obj\Debug\net10.0\Microsoft.Interop.LibraryImportGenerator\Microsoft.Interop.LibraryImportGenerator\LibraryImports.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SFileOpenArchive` |  |
| 2 | `SFileCloseArchive` |  |
| 3 | `SFileReadFile` |  |
| 4 | `SFileCloseFile` |  |
| 5 | `SFileGetFileSize` |  |
| 6 | `SFileSetFilePointer` |  |
| 7 | `SFileOpenFileEx` |  |

### Priority 226: StormDll.MpqFileStream

- **Package:** PPather
- **File:** PPather\StormDll\MpqFileStream.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_CanRead` |  |
| 2 | `get_CanSeek` |  |
| 3 | `get_CanWrite` |  |
| 4 | `get_Length` |  |
| 5 | `get_Position` |  |
| 6 | `set_Position` |  |
| 7 | `Flush` |  |
| 8 | `Read` |  |
| 9 | `Read` |  |
| 10 | `Seek` |  |
| ... | *and 5 more* | |

### Priority 227: StormDll.ArchiveSet

- **Package:** PPather
- **File:** PPather\StormDll\ArchiveSet.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetStream` |  |
| 2 | `Exists` |  |
| 3 | `Close` |  |
| 4 | `Dispose` |  |
| 5 | `.ctor` |  |

### Priority 228: StormDll.Archive

- **Package:** PPather
- **File:** PPather\StormDll\Archive.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ParseFileLines` |  |
| 2 | `IsOpen` |  |
| 3 | `HasFile` |  |
| 4 | `HasFile` |  |
| 5 | `SFileCloseArchive` |  |
| 6 | `Dispose` |  |
| 7 | `GetStream` |  |
| 8 | `GetStream` |  |
| 9 | `SFileReadFile` |  |
| 10 | `SFileCloseFile` |  |
| ... | *and 5 more* | |

### Priority 229: WowTriangles.Utils

- **Package:** PPather
- **File:** PPather\Triangles\Utils.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SegmentTriangleIntersect` |  |
| 2 | `PointDistanceToSegment` |  |
| 3 | `GetTriangleNormal` |  |
| 4 | `PointDistanceToTriangle` |  |
| 5 | `TriangleBoxIntersect` |  |
| 6 | `TriangleBoxIntersect_SIMD` |  |
| 7 | `AxesIntersectTriangleBox` |  |
| 8 | `TriangleVerticesInsideBox` |  |
| 9 | `TrianglePlaneIntersectBox` |  |
| 10 | `Min3` |  |
| ... | *and 3 more* | |

### Priority 230: WowTriangles.MPQTriangleSupplier

- **Package:** PPather
- **File:** PPather\Triangles\MPQTriangleSupplier.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 17

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Clear` |  |
| 2 | `GetArchiveNames` |  |
| 3 | `GetChunkData` |  |
| 4 | `GetChunkCoordIndex` |  |
| 5 | `GetChunkCoord1` |  |
| 6 | `GetChunkIndex` |  |
| 7 | `GetTriangles` |  |
| 8 | `AddTriangles` |  |
| 9 | `AddTriangles` |  |
| 10 | `AddTrianglesGroupDoodads` |  |
| ... | *and 7 more* | |

### Priority 231: WowTriangles.ChunkEventArgs

- **Package:** PPather
- **File:** PPather\Triangles\ChunkEventArgs.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_GridX` |  |
| 2 | `get_GridY` |  |
| 3 | `.ctor` |  |

### Priority 232: Wmo.WmoRootFile

- **Package:** PPather
- **File:** PPather\Triangles\Game\WmoRootFile.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Load` |  |
| 2 | `HandleMOHD` |  |
| 3 | `HandleMODS` |  |
| 4 | `HandleMODD` |  |
| 5 | `HandleMODN` |  |
| 6 | `HandleMOGI` |  |

### Priority 233: Wmo.WMOManager

- **Package:** PPather
- **File:** PPather\Triangles\Game\WMOManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Load` |  |
| 2 | `.ctor` |  |

### Priority 234: Wmo.WMOInstance

- **Package:** PPather
- **File:** PPather\Triangles\Game\WMOInstance.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 235: PPather.PPatherService

- **Package:** PPather
- **File:** PPather\Search\PPatherService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 27

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_search` |  |
| 2 | `get_Initialised` |  |
| 3 | `get_IsSearching` |  |
| 4 | `get_SearchFrom` |  |
| 5 | `get_SearchTo` |  |
| 6 | `get_ClosestLocation` |  |
| 7 | `get_PeekLocation` |  |
| 8 | `get_TestPoints` |  |
| 9 | `get_BlockedPoints` |  |
| 10 | `Reset` |  |
| ... | *and 17 more* | |

### Priority 236: Wmo.WmoGroupFile

- **Package:** PPather
- **File:** PPather\Triangles\Game\WmoGroupFile.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Load` |  |
| 2 | `HandleMOPY` |  |
| 3 | `HandleMOVI` |  |
| 4 | `HandleMOVT` |  |
| 5 | `GetLiquidTypeId` |  |
| 6 | `HandleMLIQ` |  |
| 7 | `HandleMOGP` |  |

### Priority 237: Wmo.WDT

- **Package:** PPather
- **File:** PPather\Triangles\Game\WDT.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 238: Wmo.ModelManager

- **Package:** PPather
- **File:** PPather\Triangles\Game\ModelManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Load` |  |
| 2 | `.ctor` |  |

### Priority 239: Wmo.ModelInstance

- **Package:** PPather
- **File:** PPather\Triangles\Game\ModelInstance.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.ctor` |  |

### Priority 240: Wmo.ModelFile

- **Package:** PPather
- **File:** PPather\Triangles\Game\ModelFile.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Read` |  |
| 2 | `ReadBoundingVertices` |  |
| 3 | `ReadBoundingTriangles` |  |
| 4 | `ReadVertices` |  |

### Priority 241: Wmo.Model

- **Package:** PPather
- **File:** PPather\Triangles\Game\Model.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 242: Wmo.MapTileFile

- **Package:** PPather
- **File:** PPather\Triangles\Game\MapTileFile.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_EmptyMH2OData1` |  |
| 2 | `get_EmptyLiquidData` |  |
| 3 | `Read` |  |
| 4 | `HandleMH2O` |  |
| 5 | `HandleMCIN` |  |
| 6 | `HandleMDDF` |  |
| 7 | `HandleMODF` |  |
| 8 | `ReadMapChunk` |  |
| 9 | `HandleChunkMCVT` |  |
| 10 | `HandleChunkMCLQ` |  |
| ... | *and 1 more* | |

### Priority 243: Wmo.MapTile

- **Package:** PPather
- **File:** PPather\Triangles\Game\MapTile.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 244: Wmo.MapChunk

- **Package:** PPather
- **File:** PPather\Triangles\Game\MapChunk.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `IsHole` |  |
| 2 | `.ctor` |  |
| 3 | `.cctor` |  |

### Priority 245: Wmo.Manager`1

- **Package:** PPather
- **File:** PPather\Triangles\Game\Manager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Clear` |  |
| 2 | `AddAndLoadIfNeeded` |  |
| 3 | `.ctor` |  |

### Priority 246: Wmo.LiquidData

- **Package:** PPather
- **File:** PPather\Triangles\Game\LiquidData.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 247: Wmo.ChunkReader

- **Package:** PPather
- **File:** PPather\Triangles\Game\ChunkReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ExtractString` |  |
| 2 | `ExtractFileNames` |  |

### Priority 248: MockWoWClient.GameState.Item

- **Package:** MockWoWClient
- **File:** MockWoWClient\GameState\Entities.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Id` |  |
| 2 | `get_Name` |  |
| 3 | `get_Quality` |  |
| 4 | `get_IsSoulbound` |  |
| 5 | `get_IsQuestItem` |  |

### Priority 249: Game.WowProcess

- **Package:** Game
- **File:** Game\WoWProcess\WowProcess.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 17

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_FileVersion` |  |
| 2 | `get_Path` |  |
| 3 | `get_Id` |  |
| 4 | `set_Id` |  |
| 5 | `get_ProcessName` |  |
| 6 | `get_MainWindowHandle` |  |
| 7 | `get_IsRunning` |  |
| 8 | `get_IsConfigurationMode` |  |
| 9 | `Create` |  |
| 10 | `PollForProcess` |  |
| ... | *and 7 more* | |

### Priority 250: Wmo.WDTFile

- **Package:** PPather
- **File:** PPather\Triangles\Game\WDTFile.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LoadMapTile` |  |
| 2 | `HandleMODF` |  |
| 3 | `HandleMAIN` |  |
| 4 | `.ctor` |  |

### Priority 251: Core.Startup.StartupOrchestrator/<ValidateAddonsAsync>d__26

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 252: Core.Startup.StartupOrchestrator/<StartNavigationServerAsync>d__27

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 253: Core.Startup.StartupOrchestrator/<RunAsync>d__21

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 254: Core.ActionBarTextureReader

- **Package:** Core
- **File:** Core\Addon\ActionBarTextureReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Count` |  |
| 2 | `get_IsInitialized` |  |
| 3 | `get_SlotTextures` |  |
| 4 | `Update` |  |
| 5 | `Reset` |  |
| 6 | `TryGetTexture` |  |
| 7 | `HasAction` |  |
| 8 | `FindSlotsByTexture` |  |
| 9 | `FindSlotByTexture` |  |
| 10 | `FindSlotByTextures` |  |
| ... | *and 3 more* | |

### Priority 255: Core.ActionBarMacroReader

- **Package:** Core
- **File:** Core\Addon\ActionBarMacroReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Count` |  |
| 2 | `get_IsInitialized` |  |
| 3 | `get_SlotMacroHashes` |  |
| 4 | `Update` |  |
| 5 | `Reset` |  |
| 6 | `TryGetMacroHash` |  |
| 7 | `HasMacro` |  |
| 8 | `FindSlotByMacroName` |  |
| 9 | `ComputeDJB2Hash24` |  |
| 10 | `DecodeMacro` |  |
| ... | *and 2 more* | |

### Priority 256: Core.NullAddonDataProvider

- **Package:** Core
- **File:** Core\AddonDataProvider\NullAddonDataProvider.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Data` |  |
| 2 | `InitFrames` |  |
| 3 | `UpdateData` |  |
| 4 | `Dispose` |  |
| 5 | `.ctor` |  |

### Priority 257: Core.UnitRace_Extension

- **Package:** Core
- **File:** Core\AddonComponent\UnitRace.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 258: Core.UnitClassification_Extension

- **Package:** Core
- **File:** Core\AddonComponent\UnitClassification.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |
| 2 | `HasFlagF` |  |

### Priority 259: Core.UnitClass_Extension

- **Package:** Core
- **File:** Core\AddonComponent\UnitClass.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 260: Core.UI_ERROR_Extensions

- **Package:** Core
- **File:** Core\AddonComponent\UI_ERROR.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 261: Core.TargetDebuffStatus

- **Package:** Core
- **File:** Core\AddonComponent\TargetDebuffStatus.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 40

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `ToString` |  |
| 3 | `Shadow_Word_Pain` |  |
| 4 | `Holy_Fire` |  |
| 5 | `Vampiric_Embrace` |  |
| 6 | `Silence` |  |
| 7 | `Shackle_Undead` |  |
| 8 | `Demoralizing_Roar` |  |
| 9 | `Faerie_Fire` |  |
| 10 | `Rip` |  |
| ... | *and 30 more* | |

### Priority 262: Core.SchoolMask_Extension

- **Package:** Core
- **File:** Core\AddonComponent\SchoolMask.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |
| 2 | `HasValue` |  |

### Priority 263: Core.PowerType_Extension

- **Package:** Core
- **File:** Core\AddonComponent\PowerType.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 264: Core.PlayerFaction_Extension

- **Package:** Core
- **File:** Core\AddonComponent\PlayerFaction.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 265: Core.MissType_Extensions

- **Package:** Core
- **File:** Core\AddonComponent\MissType.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 266: Core.Mask

- **Package:** Core
- **File:** Core\AddonComponent\Mask.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.cctor` |  |

### Priority 267: Core.LevelTracker

- **Package:** Core
- **File:** Core\AddonComponent\LevelTracker.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_TimeToLevel` |  |
| 2 | `get_PredictedLevelUpTime` |  |
| 3 | `Dispose` |  |
| 4 | `PlayerExp_Changed` |  |
| 5 | `PlayerLevel_Changed` |  |
| 6 | `UpdateExpPerHour` |  |
| 7 | `.ctor` |  |

### Priority 268: Core.GuidType_Extensions

- **Package:** Core
- **File:** Core\AddonComponent\GuidType.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 269: Core.Form_Extension

- **Package:** Core
- **File:** Core\AddonComponent\Form.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 270: Core.BuffStatus`1

- **Package:** Core
- **File:** Core\AddonComponent\BuffStatus.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 92

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `Food` |  |
| 3 | `Drink` |  |
| 4 | `Well_Fed` |  |
| 5 | `Mana_Regeneration` |  |
| 6 | `Clearcasting` |  |
| 7 | `Fortitude` |  |
| 8 | `Inner_Fire` |  |
| 9 | `Renew` |  |
| 10 | `Shield` |  |
| ... | *and 82 more* | |

### Priority 271: Core.AuraCount

- **Package:** Core
- **File:** Core\AddonComponent\AuraCount.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Hash` |  |
| 2 | `get_PlayerDebuff` |  |
| 3 | `get_PlayerBuff` |  |
| 4 | `get_TargetDebuff` |  |
| 5 | `get_TargetBuff` |  |
| 6 | `ToString` |  |
| 7 | `.ctor` |  |

### Priority 272: Core.ActionBarSlotValidator

- **Package:** Core
- **File:** Core\Actionbar\ActionBarSlotValidator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 14

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Validate` |  |
| 2 | `Validate` |  |
| 3 | `ValidateWithDetails` |  |
| 4 | `ValidateAndLog` |  |
| 5 | `ValidateClassConfig` |  |
| 6 | `GetIssueCount` |  |
| 7 | `GetIssues` |  |
| 8 | `GetActualSlot` |  |
| 9 | `FormToStanceActionBar` |  |
| 10 | `IsSpellOnActionBar` |  |
| ... | *and 4 more* | |

### Priority 273: Core.ActionBarIssue

- **Package:** Core
- **File:** Core\Actionbar\ActionBarSlotValidator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_KeyAction` |  |
| 2 | `get_Status` |  |
| 3 | `get_CanResolve` |  |
| 4 | `get_SpellName` |  |
| 5 | `get_Slot` |  |

### Priority 274: Core.SlotValidationResult

- **Package:** Core
- **File:** Core\Actionbar\ActionBarSlotValidator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Slot` |  |
| 2 | `get_ExpectedSpell` |  |
| 3 | `get_ActualTextureId` |  |
| 4 | `get_PossibleSpells` |  |
| 5 | `get_Status` |  |

### Priority 275: Core.ActionBarPopulator/ActionBarSlotItem

- **Package:** Core
- **File:** Core\Actionbar\ActionBarPopulator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |
| 2 | `get_KeyAction` |  |
| 3 | `get_IsItem` |  |
| 4 | `.ctor` |  |

### Priority 276: Core.ActionBarPopulator

- **Package:** Core
- **File:** Core\Actionbar\ActionBarPopulator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Execute` |  |
| 2 | `AddUnique` |  |
| 3 | `ScriptBuilder` |  |
| 4 | `Place` |  |
| 5 | `.ctor` |  |

### Priority 277: Core.ActionBarCostReader

- **Package:** Core
- **File:** Core\Actionbar\ActionBarCostReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_DefaultCost` |  |
| 2 | `get_Data` |  |
| 3 | `get_Count` |  |
| 4 | `Update` |  |
| 5 | `Reset` |  |
| 6 | `Get` |  |
| 7 | `.ctor` |  |
| 8 | `.cctor` |  |

### Priority 278: Core.ActionBarCost

- **Package:** Core
- **File:** Core\Actionbar\ActionBarCostReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_PowerType` |  |

### Priority 279: Core.ActionBarCooldownReader/Data

- **Package:** Core
- **File:** Core\Actionbar\ActionBarCooldownReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_End` |  |
| 2 | `.ctor` |  |

### Priority 280: Core.ActionBarCooldownReader

- **Package:** Core
- **File:** Core\Actionbar\ActionBarCooldownReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `Reset` |  |
| 3 | `Get` |  |
| 4 | `.ctor` |  |

### Priority 281: Core.AddonConfig

- **Package:** Core
- **File:** Core\Addon\AddonConfig.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Version` |  |
| 2 | `get_Author` |  |
| 3 | `get_CellSize` |  |
| 4 | `get_Title` |  |
| 5 | `get_Command` |  |
| 6 | `get_CommandFlush` |  |
| 7 | `get_CommandBindings` |  |
| 8 | `get_CommandNumberKeys` |  |
| 9 | `get_CommandActions` |  |
| 10 | `IsDefault` |  |
| ... | *and 5 more* | |

### Priority 282: Core.AddonReader

- **Package:** Core
- **File:** Core\Addon\AddonReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_DataReady` |  |
| 2 | `get_GlobalTime` |  |
| 3 | `get_DataProvider` |  |
| 4 | `get_TargetName` |  |
| 5 | `get_MouseOverName` |  |
| 6 | `set_AvgUpdateLatency` |  |
| 7 | `Update` |  |
| 8 | `SessionReset` |  |
| 9 | `FullReset` |  |
| 10 | `UpdateUI` |  |
| ... | *and 1 more* | |

### Priority 283: Core.AuraTimeReader`1

- **Package:** Core
- **File:** Core\Addon\AuraTimeReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `Reset` |  |
| 3 | `GetRemainingTimeMs` |  |
| 4 | `GetTotalTimeMs` |  |
| 5 | `.ctor` |  |

### Priority 284: Core.AuraTimeReader`1/Data

- **Package:** Core
- **File:** Core\Addon\AuraTimeReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_StartTime` |  |
| 2 | `get_DurationSec` |  |
| 3 | `get_End` |  |
| 4 | `.ctor` |  |

### Priority 285: Core.AddonValidationResult

- **Package:** Core
- **File:** Core\Configurator\AddonValidator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Errors` |  |
| 2 | `get_Warnings` |  |
| 3 | `get_Successes` |  |
| 4 | `get_IsValid` |  |
| 5 | `get_HasWarnings` |  |
| 6 | `AddError` |  |
| 7 | `AddWarning` |  |
| 8 | `AddSuccess` |  |
| 9 | `GetSummary` |  |

### Priority 286: Core.AddonValidator

- **Package:** Core
- **File:** Core\Configurator\AddonValidator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_WowPath` |  |
| 2 | `get_AddonsBasePath` |  |
| 3 | `get_WtfPath` |  |
| 4 | `Validate` |  |
| 5 | `ValidateDataToColorAddon` |  |
| 6 | `ValidateRequiredAddons` |  |
| 7 | `CheckForBrokenSymlinks` |  |
| 8 | `ValidateAddOnsTxt` |  |
| 9 | `ValidateSingleAddOnsTxt` |  |
| 10 | `ParseAddOnsTxt` |  |
| ... | *and 5 more* | |

### Priority 287: Core.AddonMaintenanceResult

- **Package:** Core
- **File:** Core\Configurator\AddonInstaller.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Success` |  |
| 2 | `get_AddonInstalled` |  |
| 3 | `get_LegacyBindPadRemoved` |  |
| 4 | `get_BrokenSymlinksRemoved` |  |
| 5 | `get_ErrorMessage` |  |
| 6 | `GetSummary` |  |

### Priority 288: Core.AddonInstaller

- **Package:** Core
- **File:** Core\Configurator\AddonInstaller.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 17

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_WowPath` |  |
| 2 | `get_AddonsBasePath` |  |
| 3 | `get_WtfPath` |  |
| 4 | `EnsureAddonInstalled` |  |
| 5 | `InstallAddon` |  |
| 6 | `EnableAddonForAllCharacters` |  |
| 7 | `DisableEnabledMissingAddOns` |  |
| 8 | `DisableEnabledMissingAddOnsInFile` |  |
| 9 | `EnableAddonInFile` |  |
| 10 | `DisableAddonForAllCharacters` |  |
| ... | *and 7 more* | |

### Priority 289: Core.AddonConfigurator

- **Package:** Core
- **File:** Core\Configurator\AddonConfigurator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 27

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Config` |  |
| 2 | `get_AddonBasePath` |  |
| 3 | `get_DefaultAddonPath` |  |
| 4 | `get_FinalAddonPath` |  |
| 5 | `Installed` |  |
| 6 | `IsDefault` |  |
| 7 | `Validate` |  |
| 8 | `Install` |  |
| 9 | `TryInstall` |  |
| 10 | `DeleteAddon` |  |
| ... | *and 17 more* | |

### Priority 290: Core.ConfigBotController

- **Package:** Core
- **File:** Core\ConfigBotController.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 21

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_GoapAgent` |  |
| 2 | `get_RouteInfo` |  |
| 3 | `get_SelectedClassFilename` |  |
| 4 | `get_SelectedPathFilename` |  |
| 5 | `get_ClassConfig` |  |
| 6 | `get_IsBotActive` |  |
| 7 | `get_AvgScreenLatency` |  |
| 8 | `get_AvgNPCLatency` |  |
| 9 | `Dispose` |  |
| 10 | `AddonThread` |  |
| ... | *and 11 more* | |

### Priority 291: Core.KeyReader

- **Package:** Core
- **File:** Core\ClassConfig\KeyReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 30

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_TextureReader` |  |
| 2 | `get_MacroReader` |  |
| 3 | `get_IconDB` |  |
| 4 | `get_SpellBookReader` |  |
| 5 | `get_ItemDB` |  |
| 6 | `get_EquipmentReader` |  |
| 7 | `get_DefaultBindings` |  |
| 8 | `get_ConsoleKeyToWoWKey` |  |
| 9 | `BuildConsoleKeyToWoWKey` |  |
| 10 | `ReadKey` |  |
| ... | *and 20 more* | |

### Priority 292: Core.KeyBindingDefaults

- **Package:** Core
- **File:** Core\ClassConfig\KeyBindingDefaults.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetBySlot` |  |
| 2 | `GetByKeyName` |  |
| 3 | `GetByBindingID` |  |
| 4 | `.cctor` |  |

### Priority 293: Core.KeyBinding

- **Package:** Core
- **File:** Core\ClassConfig\KeyBindingDefaults.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_BindingID` |  |
| 2 | `get_KeyName` |  |
| 3 | `get_ConsoleKey` |  |
| 4 | `get_WoWKey` |  |
| 5 | `get_KeyId` |  |
| 6 | `get_Slot` |  |
| 7 | `get_Modifier` |  |

### Priority 294: Core.ChatReader

- **Package:** Core
- **File:** Core\Chat\ChatReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Messages` |  |
| 2 | `Update` |  |
| 3 | `.ctor` |  |

### Priority 295: Core.ChatMessageEntry

- **Package:** Core
- **File:** Core\Chat\ChatReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Time` |  |

### Priority 296: Core.BotController/<>c__DisplayClass69_0

- **Package:** Core
- **File:** Core\BotController.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<RemotePathingThread>g__OnProfileLoaded|0` |  |

### Priority 297: Core.BotController

- **Package:** Core
- **File:** Core\BotController.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 34

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_IsBotActive` |  |
| 2 | `get_SelectedClassFilename` |  |
| 3 | `get_SelectedPathFilename` |  |
| 4 | `get_ClassConfig` |  |
| 5 | `get_GoapAgent` |  |
| 6 | `get_RouteInfo` |  |
| 7 | `get_AvgScreenLatency` |  |
| 8 | `get_AvgNPCLatency` |  |
| 9 | `ObservePlayerIdentity` |  |
| 10 | `OnTextureChanged` |  |
| ... | *and 24 more* | |

### Priority 298: Core.ActionBarBits`1

- **Package:** Core
- **File:** Core\Actionbar\ActionBarBits.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `Is` |  |
| 3 | `get_Any` |  |
| 4 | `get_Count` |  |
| 5 | `.ctor` |  |

### Priority 299: Core.BindingID_Extension

- **Package:** Core
- **File:** Core\BindingID.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 300: Core.BagReader/<>c__DisplayClass29_0

- **Package:** Core
- **File:** Core\Bag\BagReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<ReadInventory>g__Exists|0` |  |

### Priority 301: Core.BagReader/<>c__DisplayClass28_1

- **Package:** Core
- **File:** Core\Bag\BagReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<ReadBagMeta>g__RemoveByIndex|0` |  |

### Priority 302: Core.BagReader

- **Package:** Core
- **File:** Core\Bag\BagReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 29

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_BagItems` |  |
| 2 | `get_Bags` |  |
| 3 | `set_Hash` |  |
| 4 | `set_HashNewOrStackGain` |  |
| 5 | `Dispose` |  |
| 6 | `Update` |  |
| 7 | `ReadBagMeta` |  |
| 8 | `ReadInventory` |  |
| 9 | `BagItemCount` |  |
| 10 | `get_SlotCount` |  |
| ... | *and 19 more* | |

### Priority 303: Core.BagItem

- **Package:** Core
- **File:** Core\Bag\BagItem.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 17

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Bag` |  |
| 2 | `get_Slot` |  |
| 3 | `get_Count` |  |
| 4 | `get_LastCount` |  |
| 5 | `get_Item` |  |
| 6 | `get_LastUpdated` |  |
| 7 | `get_IsTradable` |  |
| 8 | `get_IsSoulbound` |  |
| 9 | `get_IsLocked` |  |
| 10 | `get_HasNoValue` |  |
| ... | *and 7 more* | |

### Priority 304: Core.BagChangeTracker

- **Package:** Core
- **File:** Core\Bag\BagChangeTracker.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Dispose` |  |
| 2 | `Reader_DataChanged` |  |
| 3 | `.ctor` |  |

### Priority 305: Core.Bag

- **Package:** Core
- **File:** Core\Bag\Bag.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Item` |  |
| 2 | `get_BagType` |  |
| 3 | `get_SlotCount` |  |
| 4 | `get_FreeSlot` |  |

### Priority 306: Core.NamesAttribute

- **Package:** Core
- **File:** Core\Attribute\NamesAttribute.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Values` |  |
| 2 | `.ctor` |  |

### Priority 307: Core.SpellBookReader

- **Package:** Core
- **File:** Core\Addon\SpellBookReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 11

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_SpellDB` |  |
| 2 | `get_Count` |  |
| 3 | `get_Hash` |  |
| 4 | `get_SpellIds` |  |
| 5 | `Update` |  |
| 6 | `Reset` |  |
| 7 | `Has` |  |
| 8 | `TryGetValue` |  |
| 9 | `GetId` |  |
| 10 | `KnowsSpell` |  |
| ... | *and 1 more* | |

### Priority 308: Core.Loot_Extensions

- **Package:** Core
- **File:** Core\Addon\Loot.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 309: Core.BindingMismatch

- **Package:** Core
- **File:** Core\Addon\KeyBindingsReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_BindingId` |  |
| 2 | `get_ExpectedKey` |  |
| 3 | `get_ExpectedModifier` |  |
| 4 | `get_ActualKey` |  |
| 5 | `get_ActualModifier` |  |

### Priority 310: Core.KeyBindingsReader

- **Package:** Core
- **File:** Core\Addon\KeyBindingsReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Count` |  |
| 2 | `get_IsInitialized` |  |
| 3 | `get_Bindings` |  |
| 4 | `get_SecondaryBindings` |  |
| 5 | `Update` |  |
| 6 | `Reset` |  |
| 7 | `TryGetBinding` |  |
| 8 | `TryGetSecondaryBinding` |  |
| 9 | `BindingMatches` |  |
| 10 | `GetMismatches` |  |
| ... | *and 2 more* | |

### Priority 311: Core.IReader

- **Package:** Core
- **File:** Core\Addon\IReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Reset` |  |

### Priority 312: Core.GatherSpells

- **Package:** Core
- **File:** Core\Addon\GatherSpells.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.cctor` |  |

### Priority 313: Core.BagReader/<>c__DisplayClass38_0

- **Package:** Core
- **File:** Core\Bag\BagReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<HasItem>g__ById|0` |  |

### Priority 314: Core.ValidationMessage

- **Package:** Core
- **File:** Core\Configurator\AddonValidator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Title` |  |

### Priority 315: AnTCP.Client.Objects.AnTcpResponse

- **Package:** Core
- **File:** Core\PPather\Objects\AnTcpResponse.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Data` |  |
| 2 | `get_Length` |  |
| 3 | `get_Type` |  |
| 4 | `As` |  |
| 5 | `AsArray` |  |
| 6 | `Pointer` |  |
| 7 | `.ctor` |  |

### Priority 316: Core.Goals.GoapGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RegexGoalName` |  |

### Priority 317: Core.Goals.CastingHandler

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 31

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogAfterCastWaitSwing` |  |
| 2 | `LogInstantBaseAction` |  |
| 3 | `LogInstantInput` |  |
| 4 | `LogInstantInputFailed` |  |
| 5 | `LogInstantUsableChange` |  |
| 6 | `LogCastbarBaseAction` |  |
| 7 | `LogCastbarInput` |  |
| 8 | `LogCastbarUsableChange` |  |
| 9 | `LogVisibleAfterCastWaitCastbar` |  |
| 10 | `LogVisibleAfterCastWaitCastbarInterrupted` |  |
| ... | *and 21 more* | |

### Priority 318: Core.GOAP.GoapAgent

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogActiveKillDetected` |  |
| 2 | `LogInactiveKillDetected` |  |
| 3 | `LogNewGoal` |  |
| 4 | `LogNewEmptyGoal` |  |
| 5 | `LogReactionDelay` |  |
| 6 | `.cctor` |  |

### Priority 319: Core.ScreenCapture

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogScreenCapture` |  |
| 2 | `.cctor` |  |

### Priority 320: Core.RequirementFactory

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogUserDefinedValue` |  |
| 2 | `LogProcessing` |  |
| 3 | `LogUnknown` |  |
| 4 | `LogSetPathEnd` |  |
| 5 | `.cctor` |  |

### Priority 321: Core.ConfigurableInput/__LogKeyActionPressRandomStruct/<GetEnumerator>d__11

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 322: Core.ConfigurableInput/__LogKeyActionPressRandomStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 323: Core.ConfigurableInput/__LogBaseActionPressRandomStruct/<GetEnumerator>d__11

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 324: Core.ConfigurableInput/__LogBaseActionPressRandomStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 325: Core.ConfigurableInput

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogBaseActionPressRandom` |  |
| 2 | `LogKeyActionPressRandom` |  |

### Priority 326: Core.PlayerDirection

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogTurnSuccess` |  |
| 2 | `LogDebugClose` |  |
| 3 | `LogDebugSetDirection` |  |
| 4 | `.cctor` |  |

### Priority 327: Core.MountHandler

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogCastStarted` |  |
| 2 | `LogCastEnded` |  |
| 3 | `LogIsMounted` |  |
| 4 | `LogUnstealthingForTravel` |  |
| 5 | `LogUnstealthSuccess` |  |
| 6 | `LogUnstealthTimeout` |  |
| 7 | `LogStealthKeyNotFound` |  |
| 8 | `.cctor` |  |

### Priority 328: Core.CombatTracker

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogEnteredCombat` |  |
| 2 | `LogLeftCombat` |  |
| 3 | `.cctor` |  |

### Priority 329: Core.Blacklist`1

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 10

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogPlayerOrPet` |  |
| 2 | `LogTagged` |  |
| 3 | `LogLevelHigh` |  |
| 4 | `LogLevelLow` |  |
| 5 | `LogNoExperienceGain` |  |
| 6 | `LogNameMatch` |  |
| 7 | `LogClassification` |  |
| 8 | `LogEvade` |  |
| 9 | `LogPetTarget` |  |
| 10 | `.cctor` |  |

### Priority 330: Core.WaitKeyActions

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogAddedWait` |  |
| 2 | `.cctor` |  |

### Priority 331: Core.KeyAction

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogPath` |  |
| 2 | `LogFormRequired` |  |
| 3 | `LogInputActionbar` |  |
| 4 | `LogInputNonActionbar` |  |
| 5 | `LogInputNoValidKey` |  |
| 6 | `LogPowerCostChange` |  |
| 7 | `.cctor` |  |

### Priority 332: Core.ClassConfiguration

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogInitBind` |  |
| 2 | `LogInitKeyActions` |  |
| 3 | `.cctor` |  |

### Priority 333: Core.BotController

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogProfileLoadedTime` |  |
| 2 | `LogProfileLoaded` |  |
| 3 | `.cctor` |  |

### Priority 334: Core.BagChangeTracker

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogItemUpdate` |  |
| 2 | `LogItemRemove` |  |
| 3 | `LogItemNew` |  |
| 4 | `.cctor` |  |

### Priority 335: Core.KeyBindingsReader

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogBindingReceived` |  |
| 2 | `LogBindingsInitialized` |  |
| 3 | `LogSecondaryBindingReceived` |  |
| 4 | `LogBindingRemoved` |  |
| 5 | `LogDebugSlotValue` |  |
| 6 | `LogDebugWaitingForBindings` |  |
| 7 | `LogDebugZeroReadStats` |  |
| 8 | `.cctor` |  |

### Priority 336: Core.ActionBarTextureReader

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogTextureReceived` |  |
| 2 | `LogTexturesInitialized` |  |
| 3 | `LogTextureCleared` |  |
| 4 | `.cctor` |  |

### Priority 337: Core.ActionBarMacroReader

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogMacroReceived` |  |
| 2 | `LogMacrosInitialized` |  |
| 3 | `LogMacroCleared` |  |
| 4 | `.cctor` |  |

### Priority 338: Core.ActionBarSlotValidator/__LogMismatchStruct/<GetEnumerator>d__10

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 339: Core.ActionBarSlotValidator/__LogMismatchStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 340: Core.ActionBarSlotValidator/__LogEmptySlotStruct/<GetEnumerator>d__9

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 341: Core.ActionBarSlotValidator/__LogEmptySlotStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 342: Core.ActionBarSlotValidator

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogValidationSkipped` |  |
| 2 | `LogTexturesNotReady` |  |
| 3 | `LogEmptySlot` |  |
| 4 | `LogMismatch` |  |
| 5 | `LogValidationComplete` |  |
| 6 | `.cctor` |  |

### Priority 343: Core.ActionBarCooldownReader

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogInvalidSlotIndex` |  |
| 2 | `LogCooldownUpdate` |  |
| 3 | `.cctor` |  |

### Priority 344: Core.Goals.CastingHandler/__LogInstantUsableChangeStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 345: Core.Goals.CastingHandler/__LogInstantUsableChangeStruct/<GetEnumerator>d__14

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 346: Core.Goals.CastingHandler/__LogCastbarUsableChangeStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 347: Core.Goals.CastingHandler/__LogCastbarUsableChangeStruct/<GetEnumerator>d__15

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 348: Core.AddonValidator

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RegexInterfaceVersion` |  |

### Priority 349: Core.AddonConfigurator

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `RegexTitle` |  |
| 2 | `RegexCellSize` |  |

### Priority 350: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__Utilities

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `IndexOfAnyUpper` |  |
| 2 | `.cctor` |  |

### Priority 351: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexGoalName_3/RunnerFactory/Runner

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Scan` |  |
| 2 | `TryFindNextPossibleStartingPosition` |  |

### Priority 352: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexGoalName_3/RunnerFactory

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CreateInstance` |  |

### Priority 353: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexGoalName_3

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.cctor` |  |

### Priority 354: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexInterfaceVersion_2/RunnerFactory/Runner

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Scan` |  |
| 2 | `TryFindNextPossibleStartingPosition` |  |
| 3 | `TryMatchAtCurrentPosition` |  |
| 4 | `<TryMatchAtCurrentPosition>g__UncaptureUntil|2_0` |  |

### Priority 355: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexInterfaceVersion_2/RunnerFactory

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CreateInstance` |  |

### Priority 356: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexInterfaceVersion_2

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.cctor` |  |

### Priority 357: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexCellSize_1/RunnerFactory/Runner

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Scan` |  |
| 2 | `TryFindNextPossibleStartingPosition` |  |
| 3 | `TryMatchAtCurrentPosition` |  |
| 4 | `<TryMatchAtCurrentPosition>g__UncaptureUntil|2_0` |  |

### Priority 358: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexCellSize_1/RunnerFactory

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CreateInstance` |  |

### Priority 359: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexCellSize_1

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.cctor` |  |

### Priority 360: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexTitle_0/RunnerFactory/Runner

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Scan` |  |
| 2 | `TryFindNextPossibleStartingPosition` |  |
| 3 | `TryMatchAtCurrentPosition` |  |

### Priority 361: AnTCP.Client.AnTcpClient

- **Package:** Core
- **File:** Core\PPather\Client\AnTcpClient.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Ip` |  |
| 2 | `get_IsConnected` |  |
| 3 | `get_Port` |  |
| 4 | `get_Client` |  |
| 5 | `get_Reader` |  |
| 6 | `get_Stream` |  |
| 7 | `Connect` |  |
| 8 | `Disconnect` |  |
| 9 | `Send` |  |
| 10 | `SendBytes` |  |
| ... | *and 2 more* | |

### Priority 362: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexTitle_0/RunnerFactory

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CreateInstance` |  |

### Priority 363: Core.Goals.WaitForGatheringGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogState` |  |
| 2 | `LogOnEnter` |  |
| 3 | `LogFailed` |  |
| 4 | `LogSuccessMining` |  |
| 5 | `.cctor` |  |

### Priority 364: Core.Goals.SkinningGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogWarnWindowStillOpen` |  |
| 2 | `LogFoundNpcNameCount` |  |
| 3 | `LogWarnUnableToTarget` |  |
| 4 | `LogReachedCorpse` |  |
| 5 | `LogCastStartedOrInterrupted` |  |
| 6 | `LogCastingState` |  |
| 7 | `LogAwaitCastbarFinish` |  |
| 8 | `LogWarnGatherFailed` |  |
| 9 | `LogWarnOutOfAttempts` |  |
| 10 | `LogLootSuccess` |  |
| ... | *and 2 more* | |

### Priority 365: Core.Goals.MailGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogFoundMailbox` |  |
| 2 | `LogMailStateTransition` |  |
| 3 | `.cctor` |  |

### Priority 366: Core.Goals.LootGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogLootSuccess` |  |
| 2 | `LogLootFailed` |  |
| 3 | `LogFoundNpcNameCount` |  |
| 4 | `LogReachedCorpse` |  |
| 5 | `LogShouldGather` |  |
| 6 | `LogLostTarget` |  |
| 7 | `LogKeyboardLootFailed` |  |
| 8 | `LogWarnWindowStillOpen` |  |
| 9 | `.cctor` |  |

### Priority 367: Core.Goals.CorpseConsumedGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogConsumed` |  |
| 2 | `.cctor` |  |

### Priority 368: Core.Goals.ConsumeCorpseGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogConsume` |  |
| 2 | `.cctor` |  |

### Priority 369: Core.Goals.ApproachTargetGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogPreventExtraPull` |  |
| 2 | `.cctor` |  |

### Priority 370: Core.Goals.AdhocNPCGoal/__LogFoundCloesestNPCByTypeStruct/<GetEnumerator>d__10

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 371: Core.Goals.AdhocNPCGoal/__LogFoundCloesestNPCByTypeStruct

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToString` |  |
| 2 | `get_Count` |  |
| 3 | `get_Item` |  |
| 4 | `System.Collections.IEnumerable.GetEnumerator` |  |
| 5 | `.ctor` |  |
| 6 | `.cctor` |  |

### Priority 372: Core.Goals.AdhocNPCGoal

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogFoundCloesestNPCByType` |  |
| 2 | `LogFoundPotentialNPCByType` |  |
| 3 | `.cctor` |  |

### Priority 373: Core.Goals.NpcNameTargeting

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogBlacklistAdded` |  |
| 2 | `LogFoundTarget` |  |
| 3 | `.cctor` |  |

### Priority 374: Core.Goals.Navigation

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogPathfinderFailed` |  |
| 2 | `LogPathfinderSuccess` |  |
| 3 | `LogClearRouteToWaypointStuck` |  |
| 4 | `LogV1ClearRouteToWaypoint` |  |
| 5 | `LogV1KeepRouteToWaypoint` |  |
| 6 | `LogV1ClearRouteToWaypointTooFar` |  |
| 7 | `LogWaypointDelay` |  |
| 8 | `.cctor` |  |

### Priority 375: Core.Goals.CursorScan

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\Microsoft.Extensions.Logging.Generators\Microsoft.Extensions.Logging.Generators.LoggerMessageGenerator\LoggerMessage.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `LogScanStart` |  |
| 2 | `LogScanFound` |  |
| 3 | `LogScanNotFound` |  |
| 4 | `.cctor` |  |

### Priority 376: System.Text.RegularExpressions.Generated.<RegexGenerator_g>F16AAA2F8F43AD575BAE600990F7445497EE2C096D6F9FAAED1B15EF88C88C008__RegexTitle_0

- **Package:** Core
- **File:** Core\obj\Debug\net10.0\System.Text.RegularExpressions.Generator\System.Text.RegularExpressions.Generator.RegexGenerator\RegexGenerator.g.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.cctor` |  |

### Priority 377: Core.FrameConfigurator

- **Package:** Core
- **File:** Core\Configurator\FrameConfigurator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 24

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_DataFrameMeta` |  |
| 2 | `get_DataFrames` |  |
| 3 | `get_Saved` |  |
| 4 | `get_AddonNotVisible` |  |
| 5 | `get_StatusMessage` |  |
| 6 | `get_PreFlightFailed` |  |
| 7 | `get_ValidationResult` |  |
| 8 | `get_MetaPixelXOffset` |  |
| 9 | `get_ImageMimeType` |  |
| 10 | `get_ImageBase64` |  |
| ... | *and 14 more* | |

### Priority 378: Core.FrameConfigurator/<>c__DisplayClass70_0

- **Package:** Core
- **File:** Core\Configurator\FrameConfigurator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<DoConfig>g__cropSize|0` |  |

### Priority 379: Core.FrameConfigurator/<StartAutoConfigAsync>d__76

- **Package:** Core
- **File:** Core\Configurator\FrameConfigurator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 380: Core.WowScreenDXGI

- **Package:** Core
- **File:** Core\WoWScreen\WowScreenDXGI.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 24

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Enabled` |  |
| 2 | `get_EnablePostProcess` |  |
| 3 | `get_MinimapEnabled` |  |
| 4 | `get_ScreenRect` |  |
| 5 | `get_ScreenImage` |  |
| 6 | `get_MiniMapRect` |  |
| 7 | `get_MiniMapImage` |  |
| 8 | `get_Data` |  |
| 9 | `get_TextBuilder` |  |
| 10 | `Dispose` |  |
| ... | *and 14 more* | |

### Priority 381: Core.NullWowScreen

- **Package:** Core
- **File:** Core\WoWScreen\NullWowScreen.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Enabled` |  |
| 2 | `get_MinimapEnabled` |  |
| 3 | `get_EnablePostProcess` |  |
| 4 | `get_ScreenImage` |  |
| 5 | `get_ScreenRect` |  |
| 6 | `get_MiniMapImage` |  |
| 7 | `get_MiniMapRect` |  |
| 8 | `GetPosition` |  |
| 9 | `GetRectangle` |  |
| 10 | `PostProcess` |  |
| ... | *and 5 more* | |

### Priority 382: Core.WApi

- **Package:** Core
- **File:** Core\WowheadAPI\WApi.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_BaseUrl` |  |
| 2 | `get_BaseUIMapUrl` |  |
| 3 | `get_NpcId` |  |
| 4 | `get_ItemId` |  |
| 5 | `get_SpellId` |  |
| 6 | `GetMapImage` |  |
| 7 | `.ctor` |  |

### Priority 383: Core.TalentReader

- **Package:** Core
- **File:** Core\Talents\TalentReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Count` |  |
| 2 | `get_Talents` |  |
| 3 | `get_Spells` |  |
| 4 | `Update` |  |
| 5 | `Reset` |  |
| 6 | `HasTalent` |  |
| 7 | `.ctor` |  |

### Priority 384: Core.SessionStat

- **Package:** Core
- **File:** Core\Session\SessionStats\SessionStat.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 19

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Deaths` |  |
| 2 | `get_Kills` |  |
| 3 | `get_StartTime` |  |
| 4 | `get_VendoredOrRepairedRecently` |  |
| 5 | `_Deaths` |  |
| 6 | `_Kills` |  |
| 7 | `get_Seconds` |  |
| 8 | `_Seconds` |  |
| 9 | `get_Minutes` |  |
| 10 | `_Minutes` |  |
| ... | *and 9 more* | |

### Priority 385: Core.ScreenCaptureCleaner

- **Package:** Core
- **File:** Core\ScreenCapture\ScreenCaptureCleaner.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |

### Priority 386: Core.ScreenCapture

- **Package:** Core
- **File:** Core\ScreenCapture\ScreenCapture.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Dispose` |  |
| 2 | `Thread` |  |
| 3 | `Request` |  |
| 4 | `.ctor` |  |

### Priority 387: Core.NoScreenCapture

- **Package:** Core
- **File:** Core\ScreenCapture\NoScreenCapture.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Request` |  |
| 2 | `.ctor` |  |

### Priority 388: Core.InfixToPostfix

- **Package:** Core
- **File:** Core\RPN\InfixToPostfix.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Convert` |  |
| 2 | `<Convert>g__IsSpecial|0_0` |  |
| 3 | `<Convert>g__IsOperator|0_1` |  |
| 4 | `<Convert>g__OperatorPriority|0_2` |  |

### Priority 389: Core.RequirementFactory/<>c__DisplayClass87_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateArithmetic>g___constValue|0` |  |
| 2 | `<CreateArithmetic>g__msg|1` |  |
| 3 | `<CreateArithmetic>g__m|2` |  |
| 4 | `<CreateArithmetic>g__e|3` |  |
| 5 | `<CreateArithmetic>g__g|4` |  |
| 6 | `<CreateArithmetic>g__l|5` |  |
| 7 | `<CreateArithmetic>g__ge|6` |  |
| 8 | `<CreateArithmetic>g__le|7` |  |

### Priority 390: Core.RequirementFactory/<>c__DisplayClass78_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateSpellInRange>g__f|1` |  |
| 2 | `<CreateSpellInRange>g__s|2` |  |

### Priority 391: Core.RequirementFactory/<>c__DisplayClass77_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateBagItem>g__f|1` |  |
| 2 | `<CreateBagItem>g__s|2` |  |

### Priority 392: Core.RequirementFactory/<>c__DisplayClass76_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateNpcId>g__f|1` |  |
| 2 | `<CreateNpcId>g__s|2` |  |

### Priority 393: Core.RequirementFactory/<>c__DisplayClass75_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateTrigger>g__f|1` |  |
| 2 | `<CreateTrigger>g__s|2` |  |

### Priority 394: Core.RequirementFactory/<>c__DisplayClass74_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateTalent>g__f|1` |  |
| 2 | `<CreateTalent>g__s|2` |  |

### Priority 395: Core.RequirementFactory/<>c__DisplayClass73_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateSpell>g__f|1` |  |
| 2 | `<CreateSpell>g__s|2` |  |

### Priority 396: Core.RequirementFactory/<>c__DisplayClass72_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateRace>g__f|1` |  |
| 2 | `<CreateRace>g__s|2` |  |

### Priority 397: Core.RequirementFactory/<>c__DisplayClass71_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateForm>g__f|1` |  |
| 2 | `<CreateForm>g__s|2` |  |

### Priority 398: Core.RequirementFactory/<>c__DisplayClass70_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateTargetCastingSpell>g__f|1` |  |
| 2 | `<CreateTargetCastingSpell>g__s|2` |  |

### Priority 399: Core.RequirementFactory/<>c__DisplayClass68_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddGameCooldown>g__f|0` |  |
| 2 | `<AddGameCooldown>g__s|1` |  |

### Priority 400: Core.RequirementFactory/<>c__DisplayClass67_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateActionCurrent>g__f|0` |  |

### Priority 401: Core.RequirementFactory/<>c__DisplayClass66_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateActionCanRun>g__f|0` |  |
| 2 | `<CreateActionCanRun>g__s|1` |  |

### Priority 402: Core.RequirementFactory/<>c__DisplayClass65_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateActionUsable>g__CanDoFormChange|0` |  |
| 2 | `<CreateActionUsable>g__f|1` |  |
| 3 | `<CreateActionUsable>g__s|2` |  |

### Priority 403: Core.RequirementFactory/<>c__DisplayClass64_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<CreateRequirement>g__s|0` |  |

### Priority 404: Core.RequirementFactory/<>c__DisplayClass63_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddSpellSchool>g__f|0` |  |
| 2 | `<AddSpellSchool>g__s|1` |  |

### Priority 405: Core.RequirementFactory/<>c__DisplayClass61_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddCharge>g__f|0` |  |
| 2 | `<AddCharge>g__s|1` |  |

### Priority 406: Core.RequirementFactory/<>c__DisplayClass60_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddKeyActionCooldown>g__f|0` |  |
| 2 | `<AddKeyActionCooldown>g__s|1` |  |

### Priority 407: Core.Testing.PlayerStateSnapshot

- **Package:** Core
- **File:** Core\Testing\PlayerStateSnapshot.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 34

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_MapX` |  |
| 2 | `get_MapY` |  |
| 3 | `get_Direction` |  |
| 4 | `get_UIMapId` |  |
| 5 | `get_Level` |  |
| 6 | `get_Health` |  |
| 7 | `get_HealthMax` |  |
| 8 | `get_HealthPercent` |  |
| 9 | `get_PowerCurrent` |  |
| 10 | `get_PowerMax` |  |
| ... | *and 24 more* | |

### Priority 408: Core.Testing.TestHelpers

- **Package:** Core
- **File:** Core\Testing\TestHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MeasureTime` |  |
| 2 | `CreateCheck` |  |
| 3 | `CreateBoolCheck` |  |
| 4 | `CreateRangeCheck` |  |

### Priority 409: Core.Testing.TestHelpers/<>c__DisplayClass1_0`1

- **Package:** Core
- **File:** Core\Testing\TestHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<WaitForValueChange>b__0` |  |

### Priority 410: Core.Testing.TestHelpers/<MeasureTimeAsync>d__4

- **Package:** Core
- **File:** Core\Testing\TestHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 411: Core.Startup.StartupOrchestrator/<LaunchWoWAsync>d__28

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 412: Core.Startup.StartupOrchestrator/<IsPathingApiHealthyAsync>d__34

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 413: Core.Startup.StartupOrchestrator/<InitializeAsync>d__24

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 414: Core.Startup.StartupOrchestrator/<ExecuteStageAsync>d__22

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 415: Core.Startup.StartupOrchestrator/<ConfigureFramesAsync>d__30

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 416: Core.Startup.StartupOrchestrator/<CleanupPathingApiPortIfStaleAsync>d__33

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 417: Core.Startup.StartupOrchestrator/<>c

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<RunAsync>b__21_0` |  |
| 2 | `<ValidateAddonsAsync>b__26_0` |  |

### Priority 418: Core.Startup.StartupOrchestrator

- **Package:** Core
- **File:** Core\Startup\StartupOrchestrator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_State` |  |
| 2 | `CreateFailureResult` |  |
| 3 | `DiscoverWoWAsync` |  |
| 4 | `FinalValidationAsync` |  |
| 5 | `GetAddonCommand` |  |
| 6 | `IsLocalHost` |  |
| 7 | `.ctor` |  |

### Priority 419: Core.Startup.StartupOptions

- **Package:** Core
- **File:** Core\Startup\StartupOptions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 16

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_AutoLaunchWoW` |  |
| 2 | `get_AutoStartNavigationServer` |  |
| 3 | `get_AutoConfigureFrames` |  |
| 4 | `get_AutoOpenBrowser` |  |
| 5 | `get_EnableHealthMonitoring` |  |
| 6 | `get_HealthCheckIntervalSeconds` |  |
| 7 | `get_WoWLaunchTimeoutSeconds` |  |
| 8 | `get_WaitForCharacterTimeoutSeconds` |  |
| 9 | `get_FrameConfigMaxRetries` |  |
| 10 | `get_FrameConfigRetryDelaySeconds` |  |
| ... | *and 6 more* | |

### Priority 420: Core.Startup.PortCleanupUtility

- **Package:** Core
- **File:** Core\Startup\PortCleanupUtility.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `TryTerminateProcessHoldingPort` |  |
| 2 | `GetListeningProcessIds` |  |

### Priority 421: Core.Startup.NavigationServerManager/<VerifyApiRespondsAsync>d__36

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 422: Core.Startup.NavigationServerManager/<StopServerAsync>d__38

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 423: Core.Startup.NavigationServerManager/<StopAsync>d__31

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 424: Core.RequirementFactory/<>c__DisplayClass59_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddMinComboPoints>g__f|0` |  |
| 2 | `<AddMinComboPoints>g__s|1` |  |

### Priority 425: Core.Startup.NavigationServerManager/<StartServerAsync>d__37

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 426: Core.Startup.NavigationServerManager/<MonitorServerAsync>d__40

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 427: Core.Startup.NavigationServerManager/<EnsureRunningAsync>d__32

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 428: Core.Startup.NavigationServerManager

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Status` |  |
| 2 | `get_Port` |  |
| 3 | `get_IsInstalled` |  |
| 4 | `get_LastOutputSnapshot` |  |
| 5 | `get_LastErrorSnapshot` |  |
| 6 | `CanAttemptRestart` |  |
| 7 | `ResetRestartAttempts` |  |
| 8 | `IsHealthyAsync` |  |
| 9 | `StartMonitoring` |  |
| 10 | `Dispose` |  |
| ... | *and 3 more* | |

### Priority 429: Core.Startup.HealthMonitor/<PerformHealthCheckAsync>d__9

- **Package:** Core
- **File:** Core\Startup\HealthMonitor.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 430: Core.Startup.HealthMonitor/<ExecuteAsync>d__8

- **Package:** Core
- **File:** Core\Startup\HealthMonitor.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 431: Core.Startup.HealthMonitor/<CheckNavigationServerHealthAsync>d__11

- **Package:** Core
- **File:** Core\Startup\HealthMonitor.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 432: Core.Startup.HealthMonitor

- **Package:** Core
- **File:** Core\Startup\HealthMonitor.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CheckWoWHealthAsync` |  |
| 2 | `.ctor` |  |

### Priority 433: Core.Talents.Talent

- **Package:** Core
- **File:** Core\Talents\Talent.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Hash` |  |
| 2 | `get_TabNum` |  |
| 3 | `get_TierNum` |  |
| 4 | `get_ColumnNum` |  |
| 5 | `get_CurrentRank` |  |
| 6 | `get_Name` |  |
| 7 | `ToString` |  |

### Priority 434: Core.Testing.TestCheck

- **Package:** Core
- **File:** Core\Testing\TestResult.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 8

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Name` |  |
| 2 | `get_Passed` |  |
| 3 | `get_Expected` |  |
| 4 | `get_Actual` |  |
| 5 | `get_Message` |  |
| 6 | `Pass` |  |
| 7 | `Fail` |  |
| 8 | `.ctor` |  |

### Priority 435: Core.Testing.TestResult

- **Package:** Core
- **File:** Core\Testing\TestResult.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Success` |  |
| 2 | `get_TestName` |  |
| 3 | `get_Timestamp` |  |
| 4 | `get_Duration` |  |
| 5 | `get_Checks` |  |
| 6 | `get_Data` |  |
| 7 | `get_Error` |  |
| 8 | `get_Message` |  |
| 9 | `Pass` |  |
| 10 | `Fail` |  |
| ... | *and 2 more* | |

### Priority 436: Core.Testing.TestHelpers/<WaitForValueChange>d__1`1

- **Package:** Core
- **File:** Core\Testing\TestHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 437: Core.Testing.TestHelpers/<WaitForCondition>d__0

- **Package:** Core
- **File:** Core\Testing\TestHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 438: Core.Testing.TestHelpers/<RetryUntilSuccess>d__2

- **Package:** Core
- **File:** Core\Testing\TestHelpers.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 439: Core.Startup.NavigationServerManager/<StartAsync>d__30

- **Package:** Core
- **File:** Core\Startup\NavigationServerManager.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 440: Core.RequirementFactory/<>c__DisplayClass57_2

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddMinPower>g__fCostWithForm|2` |  |
| 2 | `<AddMinPower>g__sCostWithForm|3` |  |
| 3 | `<AddMinPower>g__fCostWithoutForm|4` |  |
| 4 | `<AddMinPower>g__sCostWithoutForm|5` |  |

### Priority 441: Core.RequirementFactory/<>c__DisplayClass57_1

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddMinPower>g__formChangeCost|1` |  |

### Priority 442: Core.RequirementFactory/<>c__DisplayClass55_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<AddTargetIsCasting>g__f|0` |  |

### Priority 443: Core.MailGossipStateExtensions

- **Package:** Core
- **File:** Core\Gossip\MailGossipState.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 444: Core.GossipReader

- **Package:** Core
- **File:** Core\Gossip\GossipReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 18

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `set_Count` |  |
| 2 | `get_Gossips` |  |
| 3 | `get_Ready` |  |
| 4 | `GossipStart` |  |
| 5 | `GossipEnd` |  |
| 6 | `MerchantWindowOpened` |  |
| 7 | `MerchantWindowClosed` |  |
| 8 | `MerchantWindowSelling` |  |
| 9 | `MerchantWindowSellingFinished` |  |
| 10 | `GossipStartOrMerchantWindowOpened` |  |
| ... | *and 8 more* | |

### Priority 445: Core.Gossip_Extension

- **Package:** Core
- **File:** Core\Gossip\Gossip.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 446: Core.GoalFactory

- **Package:** Core
- **File:** Core\GoalsFactory\GoalFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Create` |  |
| 2 | `ResolveLootAndSkin` |  |
| 3 | `ResolveAdhocGoals` |  |
| 4 | `ResolveAdhocNPCGoal` |  |
| 5 | `ResolveWaitGoal` |  |
| 6 | `ResolveMailGoal` |  |
| 7 | `ResolvePetClass` |  |
| 8 | `ResolveFollowRouteGoal` |  |
| 9 | `AddFleeGoal` |  |
| 10 | `RelativeFilePath` |  |
| ... | *and 2 more* | |

### Priority 447: Core.Wait

- **Package:** Core
- **File:** Core\GoalsComponent\Wait.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `Update` |  |
| 3 | `Update` |  |
| 4 | `Fixed` |  |
| 5 | `Till` |  |
| 6 | `Until` |  |
| 7 | `UntilCount` |  |
| 8 | `Until` |  |
| 9 | `Until` |  |
| 10 | `UntilWithoutRepeat` |  |
| ... | *and 3 more* | |

### Priority 448: Core.TimeToKill

- **Package:** Core
- **File:** Core\GoalsComponent\TimeToKill.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `set_Time` |  |
| 2 | `get_Time` |  |
| 3 | `Dispose` |  |
| 4 | `Update` |  |
| 5 | `Reset` |  |
| 6 | `.ctor` |  |

### Priority 449: Core.ReactCastError

- **Package:** Core
- **File:** Core\GoalsComponent\ReactCastError.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Do` |  |
| 2 | `WaitForCooldown` |  |
| 3 | `<Do>g__WaitDebuffChange|12_1` |  |
| 4 | `<Do>g__OutOfRange|12_3` |  |
| 5 | `<Do>g__MinRangeChanges|12_5` |  |
| 6 | `<WaitForCooldown>g__WaitCooldown|13_0` |  |
| 7 | `.ctor` |  |

### Priority 450: Core.PlayerDirection

- **Package:** Core
- **File:** Core\GoalsComponent\PlayerDirection.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 15

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SetDirection` |  |
| 2 | `SetDirection` |  |
| 3 | `SetDirection` |  |
| 4 | `SetDirectionWithVerification` |  |
| 5 | `WaitForTurn` |  |
| 6 | `CalculateAngleDifference` |  |
| 7 | `CalculateTurnDuration` |  |
| 8 | `TurnAmount` |  |
| 9 | `TurnDuration` |  |
| 10 | `GetDirectionKeyToPress` |  |
| ... | *and 5 more* | |

### Priority 451: Core.MountHandler

- **Package:** Core
- **File:** Core\GoalsComponent\MountHandler.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 16

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CanMount` |  |
| 2 | `MeetsMountUnlockRequirement` |  |
| 3 | `MountUp` |  |
| 4 | `ShouldMount` |  |
| 5 | `ShouldMount` |  |
| 6 | `Dismount` |  |
| 7 | `IsMounted` |  |
| 8 | `CastDetected` |  |
| 9 | `MountedOrNotCastingOrValidTargetOrEnteredCombat` |  |
| 10 | `HasValidTarget` |  |
| ... | *and 6 more* | |

### Priority 452: Core.DruidMountHandler

- **Package:** Core
- **File:** Core\GoalsComponent\DruidMountHandler.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CanMount` |  |
| 2 | `Dismount` |  |
| 3 | `IsMounted` |  |
| 4 | `MountUp` |  |
| 5 | `ShouldMount` |  |
| 6 | `OptimizeTravelSpeed` |  |
| 7 | `.ctor` |  |

### Priority 453: Core.CombatTracker

- **Package:** Core
- **File:** Core\GoalsComponent\CombatTracker.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Started` |  |
| 2 | `Dispose` |  |
| 3 | `Update` |  |
| 4 | `AcquiredTarget` |  |
| 5 | `PlayerOrPetHasTarget` |  |
| 6 | `Log` |  |
| 7 | `.ctor` |  |

### Priority 454: Core.NoBlacklist

- **Package:** Core
- **File:** Core\GoalsComponent\Blacklist\NoBlacklist.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Is` |  |

### Priority 455: Core.BlacklistTarget

- **Package:** Core
- **File:** Core\GoalsComponent\Blacklist\BlacklistSource\BlacklistTarget.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 14

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_UnitGuid` |  |
| 2 | `get_UnitId` |  |
| 3 | `get_UnitName` |  |
| 4 | `get_UnitLevel` |  |
| 5 | `get_UnitClassification` |  |
| 6 | `Exists` |  |
| 7 | `UnitTarget_PlayerOrPet` |  |
| 8 | `Unit_Dead` |  |
| 9 | `Unit_Hostile` |  |
| 10 | `Unit_Player` |  |
| ... | *and 4 more* | |

### Priority 456: Core.ConfigurableInput

- **Package:** Core
- **File:** Core\Input\ConfigurableInput.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 40

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Reset` |  |
| 2 | `StartForward` |  |
| 3 | `StopForward` |  |
| 4 | `StartBackward` |  |
| 5 | `StopBackward` |  |
| 6 | `SetKeyState` |  |
| 7 | `TurnRandomDir` |  |
| 8 | `PressRandom` |  |
| 9 | `PressFixed` |  |
| 10 | `PressRandom` |  |
| ... | *and 30 more* | |

### Priority 457: Core.BlacklistMouseOver

- **Package:** Core
- **File:** Core\GoalsComponent\Blacklist\BlacklistSource\BlacklistMouseOver.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 14

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_UnitGuid` |  |
| 2 | `get_UnitId` |  |
| 3 | `get_UnitName` |  |
| 4 | `get_UnitLevel` |  |
| 5 | `get_UnitClassification` |  |
| 6 | `Exists` |  |
| 7 | `UnitTarget_PlayerOrPet` |  |
| 8 | `Unit_Dead` |  |
| 9 | `Unit_Hostile` |  |
| 10 | `Unit_Player` |  |
| ... | *and 4 more* | |

### Priority 458: Core.MountUnlockOptions

- **Package:** Core
- **File:** Core\Features\MountUnlockOptions.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_EnforceTbcMountLevelRequirement` |  |
| 2 | `get_TbcMountUnlockLevel` |  |
| 3 | `get_AutoUnstealthForTravel` |  |

### Priority 459: Core.ExecGameCommand

- **Package:** Core
- **File:** Core\ExecGameCommand\ExecGameCommand.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Run` |  |
| 2 | `Run` |  |
| 3 | `.ctor` |  |

### Priority 460: Core.InventorySlotId_Extension

- **Package:** Core
- **File:** Core\Equipments\InventorySlotId.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 461: Core.EquipmentReader

- **Package:** Core
- **File:** Core\Equipments\EquipmentReader.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Items` |  |
| 2 | `Update` |  |
| 3 | `ToStringList` |  |
| 4 | `RangedWeapon` |  |
| 5 | `HasItem` |  |
| 6 | `GetId` |  |
| 7 | `.ctor` |  |

### Priority 462: Core.FrontendUpdate

- **Package:** Core
- **File:** Core\Environment\FrontendUpdate.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `.ctor` |  |

### Priority 463: Core.DependencyInjection

- **Package:** Core
- **File:** Core\DependencyInjection.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 12

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AddAddonComponents` |  |
| 2 | `AddStartupIoC` |  |
| 3 | `AddCoreFrontend` |  |
| 4 | `AddCoreConfiguration` |  |
| 5 | `AddCoreNormal` |  |
| 6 | `AddCoreBase` |  |
| 7 | `AddWoWProcess` |  |
| 8 | `AddWoWProcess` |  |
| 9 | `GetScreenCapture` |  |
| 10 | `GetAddonDataProvider` |  |
| ... | *and 2 more* | |

### Priority 464: Core.FrameConfig

- **Package:** Core
- **File:** Core\DataFrame\FrameConfig.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 19

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetPath` |  |
| 2 | `GetResolutionPath` |  |
| 3 | `GetSourceResolutionPath` |  |
| 4 | `FindProjectDirectory` |  |
| 5 | `Exists` |  |
| 6 | `ExistsForResolution` |  |
| 7 | `ListResolutionConfigs` |  |
| 8 | `IsValid` |  |
| 9 | `TryActivateForResolution` |  |
| 10 | `Load` |  |
| ... | *and 9 more* | |

### Priority 465: Core.DataFrameMeta

- **Package:** Core
- **File:** Core\DataFrame\DataFrameMeta.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 9

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Empty` |  |
| 2 | `get_Hash` |  |
| 3 | `get_Spacing` |  |
| 4 | `get_Sizes` |  |
| 5 | `get_Rows` |  |
| 6 | `get_Count` |  |
| 7 | `EstimatedSize` |  |
| 8 | `.ctor` |  |
| 9 | `.cctor` |  |

### Priority 466: Core.DataFrameConfig

- **Package:** Core
- **File:** Core\DataFrame\DataFrameConfig.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Version` |  |
| 2 | `get_AddonVersion` |  |
| 3 | `get_Rect` |  |
| 4 | `get_Meta` |  |
| 5 | `get_Frames` |  |
| 6 | `.ctor` |  |

### Priority 467: Core.ImageHashing

- **Package:** Core
- **File:** Core\Cursor\ImageHashing.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AverageHash` |  |
| 2 | `Similarity` |  |

### Priority 468: Core.CursorType_Extension

- **Package:** Core
- **File:** Core\Cursor\CursorType.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `ToStringF` |  |

### Priority 469: Core.CursorClassifier

- **Package:** Core
- **File:** Core\Cursor\CursorClassifier.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Dispose` |  |
| 2 | `Classify` |  |
| 3 | `.ctor` |  |
| 4 | `.cctor` |  |

### Priority 470: Core.FrameConfigurator/<StartAutoConfigWithRetriesAsync>d__77

- **Package:** Core
- **File:** Core\Configurator\FrameConfigurator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 471: Core.Blacklist`1

- **Package:** Core
- **File:** Core\GoalsComponent\Blacklist\Blacklist.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Is` |  |
| 2 | `.ctor` |  |

### Priority 472: WowheadDB.Node

- **Package:** WowheadDB
- **File:** WowheadDB\Herb\Node.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_MapCoords` |  |

### Priority 473: Core.ConfigurableInput

- **Package:** Core
- **File:** Core\Input\ConfigurableInputClassConfig.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 23

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_KeyboardOnly` |  |
| 2 | `get_ForwardKey` |  |
| 3 | `get_BackwardKey` |  |
| 4 | `get_TurnLeftKey` |  |
| 5 | `get_TurnRightKey` |  |
| 6 | `get_StrafeLeftKey` |  |
| 7 | `get_StrafeRightKey` |  |
| 8 | `get_Jump` |  |
| 9 | `get_Interact` |  |
| 10 | `get_InteractMouseOver` |  |
| ... | *and 13 more* | |

### Priority 474: Core.NullMailSettingsService

- **Package:** Core
- **File:** Core\Mail\NullMailSettingsService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SetRecipient` |  |
| 2 | `AddExclusion` |  |
| 3 | `RemoveExclusion` |  |
| 4 | `GetExclusions` |  |
| 5 | `SetExclusions` |  |

### Priority 475: Core.RequirementFactory/<>c__DisplayClass51_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<BindMailRequirements>g__HasMailableItems|0` |  |
| 2 | `<BindMailRequirements>g__HasExcessGold|1` |  |
| 3 | `<BindMailRequirements>g__HasMailWork|2` |  |

### Priority 476: Core.RequirementFactory/<>c__DisplayClass50_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<BindPathSettingsBoolVariables>g__AnyPathFinished|0` |  |

### Priority 477: Core.RequirementFactory/<>c__DisplayClass49_1

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<BindMinCost>g__get_i|1` |  |

### Priority 478: Core.RequirementFactory/<>c__DisplayClass49_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<BindMinCost>g__get|0` |  |

### Priority 479: Core.RequirementFactory/<>c__DisplayClass48_0

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<BindCooldown>g__get|0` |  |

### Priority 480: Core.RequirementFactory/<>c__DisplayClass46_1

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<InitUserDefinedIntVariables>g__f|0` |  |
| 2 | `<InitUserDefinedIntVariables>g__l|1` |  |
| 3 | `<InitUserDefinedIntVariables>g__l|2` |  |
| 4 | `<InitUserDefinedIntVariables>g__l|3` |  |
| 5 | `<InitUserDefinedIntVariables>g__l|4` |  |
| 6 | `<InitUserDefinedIntVariables>g__l|5` |  |

### Priority 481: Core.RequirementFactory

- **Package:** Core
- **File:** Core\Requirement\RequirementFactory.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 65

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `AddAura` |  |
| 2 | `LastTargetDodgeMs` |  |
| 3 | `MainHandSwing` |  |
| 4 | `RangedSwing` |  |
| 5 | `Init` |  |
| 6 | `Init` |  |
| 7 | `Process` |  |
| 8 | `InitUserDefinedIntVariables` |  |
| 9 | `InitAutoBinds` |  |
| 10 | `BindCooldown` |  |
| ... | *and 55 more* | |

### Priority 482: Core.Requirement

- **Package:** Core
- **File:** Core\Requirement\Requirement.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `False` |  |
| 2 | `Default` |  |
| 3 | `get_HasRequirement` |  |
| 4 | `get_LogMessage` |  |
| 5 | `get_VisibleIfHasRequirement` |  |

### Priority 483: Core.RequirementExt/<>c__DisplayClass2_0

- **Package:** Core
- **File:** Core\Requirement\Requirement.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<Negate>g__Negated|0` |  |
| 2 | `<Negate>g__Message|1` |  |

### Priority 484: Core.RequirementExt/<>c__DisplayClass1_0

- **Package:** Core
- **File:** Core\Requirement\Requirement.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<And>g__CombinedReq|0` |  |
| 2 | `<And>g__Message|1` |  |

### Priority 485: Core.RequirementExt/<>c__DisplayClass0_0

- **Package:** Core
- **File:** Core\Requirement\Requirement.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<Or>g__CombinedReq|0` |  |
| 2 | `<Or>g__Message|1` |  |

### Priority 486: Core.RequirementExt

- **Package:** Core
- **File:** Core\Requirement\Requirement.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Or` |  |
| 2 | `And` |  |
| 3 | `Negate` |  |

### Priority 487: Core.RemotePathingAPIV3

- **Package:** Core
- **File:** Core\PPather\RemotePathingAPIV3.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 13

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_IsConnected` |  |
| 2 | `Dispose` |  |
| 3 | `DrawLines` |  |
| 4 | `DrawSphere` |  |
| 5 | `FindMapRoute` |  |
| 6 | `FindWorldRoute` |  |
| 7 | `ApplyZHint` |  |
| 8 | `TryWithFallbackZ` |  |
| 9 | `UpdateZHint` |  |
| 10 | `PingServer` |  |
| ... | *and 3 more* | |

### Priority 488: Core.MailSettingsService

- **Package:** Core
- **File:** Core\Mail\MailSettingsService.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 6

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `SetRecipient` |  |
| 2 | `AddExclusion` |  |
| 3 | `RemoveExclusion` |  |
| 4 | `GetExclusions` |  |
| 5 | `SetExclusions` |  |
| 6 | `.ctor` |  |

### Priority 489: Core.RemotePathingAPI/<DrawSphere>d__12

- **Package:** Core
- **File:** Core\PPather\RemotePathingAPI.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 490: Core.RemotePathingAPI/<>c

- **Package:** Core
- **File:** Core\PPather\RemotePathingAPI.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `<DrawLines>b__11_0` |  |

### Priority 491: Core.RemotePathingAPI

- **Package:** Core
- **File:** Core\PPather\RemotePathingAPI.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Client` |  |
| 2 | `get_Options` |  |
| 3 | `Dispose` |  |
| 4 | `FindMapRoute` |  |
| 5 | `FindWorldRoute` |  |
| 6 | `PingServer` |  |
| 7 | `.ctor` |  |

### Priority 492: Core.NoPathVisualizer

- **Package:** Core
- **File:** Core\PPather\NoPathVisualizer.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_Client` |  |
| 2 | `get_Options` |  |
| 3 | `Dispose` |  |
| 4 | `DrawLines` |  |
| 5 | `DrawSphere` |  |

### Priority 493: Core.LocalPathingApi

- **Package:** Core
- **File:** Core\PPather\LocalPathingApi.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `DrawLines` |  |
| 2 | `DrawSphere` |  |
| 3 | `FindMapRoute` |  |
| 4 | `FindWorldRoute` |  |
| 5 | `.ctor` |  |

### Priority 494: Core.PathSimplify

- **Package:** Core
- **File:** Core\Path\Simplify\PathSimplify.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetSquareSegmentDistance` |  |
| 2 | `RadialDistance` |  |
| 3 | `DouglasPeucker` |  |
| 4 | `Simplify` |  |

### Priority 495: Core.RouteInfo

- **Package:** Core
- **File:** Core\Path\RouteInfo.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 28

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_RouteSrc` |  |
| 2 | `get_Route` |  |
| 3 | `get_RouteToWaypoint` |  |
| 4 | `MostRecent` |  |
| 5 | `get_PoiList` |  |
| 6 | `Dispose` |  |
| 7 | `SetRouteSource` |  |
| 8 | `UpdateRoute` |  |
| 9 | `SetMargin` |  |
| 10 | `SetCanvasSize` |  |
| ... | *and 18 more* | |

### Priority 496: Core.RouteInfoPoi

- **Package:** Core
- **File:** Core\Path\RouteInfo.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 2

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `.ctor` |  |
| 2 | `.ctor` |  |

### Priority 497: Core.PointEstimator

- **Package:** Core
- **File:** Core\Path\PointEstimator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `GetMapPos` |  |

### Priority 498: Core.DirectionCalculator

- **Package:** Core
- **File:** Core\Path\DirectionCalculator.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 3

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `CalculateMapHeading` |  |
| 2 | `ToNormalRadian` |  |
| 3 | `ToNormalRadianNoFlip` |  |

### Priority 499: Core.PathDrawer

- **Package:** Core
- **File:** Core\PathDrawer\PathDrawer.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 5

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Execute` |  |
| 2 | `ValidMapCoordinates` |  |
| 3 | `CalculateBounds` |  |
| 4 | `DownloadBitmap` |  |
| 5 | `DrawPath` |  |

### Priority 500: Core.NpcNameOverlay

- **Package:** Core
- **File:** Core\Overlay\NpcNameOverlay.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 7

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Finalize` |  |
| 2 | `Dispose` |  |
| 3 | `Dispose` |  |
| 4 | `SetupGraphics` |  |
| 5 | `DestroyGraphics` |  |
| 6 | `DrawGraphics` |  |
| 7 | `.ctor` |  |

### Priority 501: Core.MinimapNodeFinder

- **Package:** Core
- **File:** Core\Minimap\MinimapNodeFinder.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 0%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `Update` |  |
| 2 | `FindYellowPoints` |  |
| 3 | `ScorePoints` |  |
| 4 | `.ctor` |  |

### Priority 502: Core.MinimapNodeEventArgs

- **Package:** Core
- **File:** Core\Minimap\MinimapNodeEventArgs.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 4

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_X` |  |
| 2 | `get_Y` |  |
| 3 | `get_Amount` |  |
| 4 | `.ctor` |  |

### Priority 503: Core.RemotePathingAPI/<DrawLines>d__11

- **Package:** Core
- **File:** Core\PPather\RemotePathingAPI.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `MoveNext` |  |

### Priority 504: WowheadDB.NPC

- **Package:** WowheadDB
- **File:** WowheadDB\NPC\NPC.cs
- **Line Coverage:** 0%
- **Branch Coverage:** 100%
- **Uncovered Methods:** 1

#### Top Uncovered Methods

| # | Method | Line |
|---|--------|------|
| 1 | `get_MapCoords` |  |

## Implementation Checklist

- [ ] Review generated test stubs
- [ ] Add proper test data and mocks
- [ ] Implement happy path tests
- [ ] Add edge case and error handling tests
- [ ] Run tests and verify coverage improvement
- [ ] Refactor tests for clarity and maintainability

## Recommendations

1. **Start with Priority 1** classes - they have the most significant coverage gaps
2. **Focus on public methods** - internal/private methods can be tested via public API
3. **Use MockWoWClient** for integration-style tests
4. **Property-based testing** - consider using FsCheck for complex input validation
5. **Document complex logic** - add comments explaining business rules in tests

---
*Auto-generated by Phase 3: Self-Improving Test Generation*

