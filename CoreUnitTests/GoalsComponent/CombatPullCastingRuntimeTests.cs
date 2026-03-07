using Core;
using Core.Goals;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class CombatPullCastingRuntimeTests
{
    [Fact]
    public void HasAmbiguousInstantSuccessEvidence_WhenGcdAdvances_ReturnsTrue()
    {
        bool result = CastingHandler.HasAmbiguousInstantSuccessEvidence(
            beforeAuraHash: 10,
            afterAuraHash: 10,
            beforeCastEventTime: 100,
            afterCastEventTime: 100,
            castEvent: UI_ERROR.NONE,
            beforeGcdMs: 0,
            afterGcdMs: 1200,
            beforeCastCount: 10,
            afterCastCount: 11,
            beforeUsable: true,
            afterUsable: false,
            beforeInCombat: false,
            afterInCombat: true,
            beforeTargetHealth: 150,
            afterTargetHealth: 130,
            beforeMissType: 0,
            afterMissType: 0);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAmbiguousInstantSuccessEvidence_WhenGcdTransitionsFromZeroToActive_ReturnsTrue()
    {
        bool result = CastingHandler.HasAmbiguousInstantSuccessEvidence(
            beforeAuraHash: 10,
            afterAuraHash: 10,
            beforeCastEventTime: 100,
            afterCastEventTime: 100,
            castEvent: UI_ERROR.NONE,
            beforeGcdMs: 0,
            afterGcdMs: 1200,
            beforeCastCount: 10,
            afterCastCount: 10,
            beforeUsable: true,
            afterUsable: true,
            beforeInCombat: false,
            afterInCombat: false,
            beforeTargetHealth: 150,
            afterTargetHealth: 150,
            beforeMissType: 0,
            afterMissType: 0);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAmbiguousInstantSuccessEvidence_WhenGcdOnlyCountsDown_ReturnsFalse()
    {
        bool result = CastingHandler.HasAmbiguousInstantSuccessEvidence(
            beforeAuraHash: 10,
            afterAuraHash: 10,
            beforeCastEventTime: 100,
            afterCastEventTime: 100,
            castEvent: UI_ERROR.NONE,
            beforeGcdMs: 1200,
            afterGcdMs: 900,
            beforeCastCount: 10,
            afterCastCount: 10,
            beforeUsable: true,
            afterUsable: true,
            beforeInCombat: false,
            afterInCombat: false,
            beforeTargetHealth: 150,
            afterTargetHealth: 150,
            beforeMissType: 0,
            afterMissType: 0);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasAmbiguousInstantSuccessEvidence_WhenNoSignals_ReturnsFalse()
    {
        bool result = CastingHandler.HasAmbiguousInstantSuccessEvidence(
            beforeAuraHash: 10,
            afterAuraHash: 10,
            beforeCastEventTime: 100,
            afterCastEventTime: 100,
            castEvent: UI_ERROR.NONE,
            beforeGcdMs: 0,
            afterGcdMs: 0,
            beforeCastCount: 10,
            afterCastCount: 10,
            beforeUsable: true,
            afterUsable: true,
            beforeInCombat: false,
            afterInCombat: false,
            beforeTargetHealth: 150,
            afterTargetHealth: 150,
            beforeMissType: 0,
            afterMissType: 0);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasAmbiguousInstantSuccessEvidence_WhenCastEventSuccess_ReturnsTrue()
    {
        bool result = CastingHandler.HasAmbiguousInstantSuccessEvidence(
            beforeAuraHash: 10,
            afterAuraHash: 10,
            beforeCastEventTime: 100,
            afterCastEventTime: 100,
            castEvent: UI_ERROR.CAST_SUCCESS,
            beforeGcdMs: 0,
            afterGcdMs: 0,
            beforeCastCount: 10,
            afterCastCount: 10,
            beforeUsable: true,
            afterUsable: true,
            beforeInCombat: false,
            afterInCombat: false,
            beforeTargetHealth: 150,
            afterTargetHealth: 150,
            beforeMissType: 0,
            afterMissType: 0);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAmbiguousInstantSuccessEvidence_WhenCastCountIncreases_ReturnsTrue()
    {
        bool result = CastingHandler.HasAmbiguousInstantSuccessEvidence(
            beforeAuraHash: 10,
            afterAuraHash: 10,
            beforeCastEventTime: 100,
            afterCastEventTime: 100,
            castEvent: UI_ERROR.NONE,
            beforeGcdMs: 0,
            afterGcdMs: 0,
            beforeCastCount: 10,
            afterCastCount: 11,
            beforeUsable: true,
            afterUsable: true,
            beforeInCombat: false,
            afterInCombat: false,
            beforeTargetHealth: 150,
            afterTargetHealth: 150,
            beforeMissType: 0,
            afterMissType: 0);

        result.Should().BeTrue();
    }

    [Fact]
    public void DecideRangedFailureAction_WhenCasting_ReturnsWaitForCastState()
    {
        PullFailureAction result = PullTargetGoal.DecideRangedFailureAction(
            isCasting: true,
            spellInQueue: false,
            hasTarget: true,
            pullDurationMs: 2000,
            softAbortWindowMs: 6000);

        result.Should().Be(PullFailureAction.WaitForCastState);
    }

    [Fact]
    public void DecideRangedFailureAction_WhenTargetMissing_ReturnsWaitForTargetState()
    {
        PullFailureAction result = PullTargetGoal.DecideRangedFailureAction(
            isCasting: false,
            spellInQueue: false,
            hasTarget: false,
            pullDurationMs: 2000,
            softAbortWindowMs: 6000);

        result.Should().Be(PullFailureAction.WaitForTargetState);
    }

    [Fact]
    public void DecideRangedFailureAction_WithinSoftWindow_ReturnsSoftRetry()
    {
        PullFailureAction result = PullTargetGoal.DecideRangedFailureAction(
            isCasting: false,
            spellInQueue: false,
            hasTarget: true,
            pullDurationMs: 1500,
            softAbortWindowMs: 6000);

        result.Should().Be(PullFailureAction.SoftRetryApproach);
    }

    [Fact]
    public void DecideRangedFailureAction_AfterSoftWindow_ReturnsHardClear()
    {
        PullFailureAction result = PullTargetGoal.DecideRangedFailureAction(
            isCasting: false,
            spellInQueue: false,
            hasTarget: true,
            pullDurationMs: 7000,
            softAbortWindowMs: 6000);

        result.Should().Be(PullFailureAction.HardClearTarget);
    }

    [Fact]
    public void IsLikelyPullSuccess_WhenAnySignalPresent_ReturnsTrue()
    {
        bool result = PullTargetGoal.IsLikelyPullSuccess(
            enteredCombat: false,
            targetHealthDropped: true,
            castStateProgressed: false,
            combatLogProgressed: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsLikelyPullSuccess_WhenNoSignalsPresent_ReturnsFalse()
    {
        bool result = PullTargetGoal.IsLikelyPullSuccess(
            enteredCombat: false,
            targetHealthDropped: false,
            castStateProgressed: false,
            combatLogProgressed: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasRecentCombatProgressSignal_WhenKillCreditRecent_ReturnsTrue()
    {
        bool result = CombatGoal.HasRecentCombatProgressSignal(
            damageDoneElapsedMs: 5_000,
            damageTakenElapsedMs: 5_000,
            nowTick: 20_000,
            lastKillCreditTick: 18_500,
            progressWindowMs: 2_500);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasRecentCombatProgressSignal_WhenAllSignalsStale_ReturnsFalse()
    {
        bool result = CombatGoal.HasRecentCombatProgressSignal(
            damageDoneElapsedMs: 3_000,
            damageTakenElapsedMs: 4_000,
            nowTick: 30_000,
            lastKillCreditTick: 20_000,
            progressWindowMs: 2_500);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRegisterLostTargetBurst_WhenThresholdMetAndCooldownElapsed_ReturnsTrue()
    {
        bool result = CombatGoal.ShouldRegisterLostTargetBurst(
            nowTick: 30_000,
            lastBurstTick: 0,
            lossesWithinBurstWindow: 3,
            burstThreshold: 3,
            burstCooldownMs: 10_000);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRegisterLostTargetBurst_WhenOnCooldown_ReturnsFalse()
    {
        bool result = CombatGoal.ShouldRegisterLostTargetBurst(
            nowTick: 30_000,
            lastBurstTick: 25_000,
            lossesWithinBurstWindow: 3,
            burstThreshold: 3,
            burstCooldownMs: 10_000);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldContinueAmbiguousResolution_WhenEvidenceArrives_ReturnsTrue()
    {
        bool result = CastingHandler.ShouldContinueAmbiguousResolution(
            recheckElapsedMs: 42f,
            tokenCancelled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldContinueAmbiguousResolution_WhenTimedOut_ReturnsFalse()
    {
        bool result = CastingHandler.ShouldContinueAmbiguousResolution(
            recheckElapsedMs: -220f,
            tokenCancelled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldContinueAmbiguousResolution_WhenCancelled_ReturnsFalse()
    {
        bool result = CastingHandler.ShouldContinueAmbiguousResolution(
            recheckElapsedMs: 15f,
            tokenCancelled: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptApproach_WhenRangedPullIsGated_ReturnsFalse()
    {
        bool result = PullTargetGoal.ShouldAttemptApproach(
            castAny: false,
            spellInQueue: false,
            isCasting: false,
            inCombat: false,
            autoShotActive: false,
            withinPullRange: true,
            isInMeleeRange: false,
            hasRunnablePullAction: false,
            hasRangedPullActions: true,
            holdRangedStandoff: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptApproach_WhenAlreadyInMeleeRange_ReturnsTrue()
    {
        bool result = PullTargetGoal.ShouldAttemptApproach(
            castAny: false,
            spellInQueue: false,
            isCasting: false,
            inCombat: false,
            autoShotActive: false,
            withinPullRange: true,
            isInMeleeRange: true,
            hasRunnablePullAction: false,
            hasRangedPullActions: true,
            holdRangedStandoff: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWarlockEffectiveRange_WhenShootOrSpellRangeMissingButTargetWithinThirtyYards_ReturnsTrue()
    {
        bool result = SpellInRange.IsWarlockEffectiveRange(
            shadowBoltInRange: false,
            shootInRange: false,
            minRange: 25,
            maxRange: 30);

        result.Should().BeTrue();
    }

    [Fact]
    public void ApproachTargetGoal_ShouldAdvanceTowardTarget_WhenWarlockAlreadyWithinPullRange_ReturnsFalse()
    {
        bool result = ApproachTargetGoal.ShouldAdvanceTowardTarget(
            holdPullStandoff: true,
            withinPullRange: true,
            inCombatRange: false,
            inCombat: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void PullTargetGoal_ShouldSuppressBodyPullFallback_WhenWarlockIsAlreadyInPullRange_ReturnsTrue()
    {
        bool result = PullTargetGoal.ShouldSuppressBodyPullFallback(
            holdRangedStandoff: true,
            withinPullRange: true,
            isInMeleeRange: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CombatGoal_ShouldApproachCurrentTarget_WhenWarlockAlreadyWithinPullRange_ReturnsFalse()
    {
        bool result = CombatGoal.ShouldApproachCurrentTarget(
            hasTarget: true,
            targetAlive: true,
            targetHostile: true,
            prefersRangedCombat: true,
            holdRangedStandoff: true,
            withinCombatRange: false,
            withinPullRange: true,
            isCasting: false,
            spellInQueue: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCancelMeleeAutoAttackForRangedCombat_WhenWandPathExists_ReturnsTrue()
    {
        bool result = CombatGoal.ShouldCancelMeleeAutoAttackForRangedCombat(
            prefersRangedCombat: true,
            meleeAutoAttacking: true,
            shooting: false,
            inMeleeRange: false,
            withinCombatRange: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCancelMeleeAutoAttackForRangedCombat_WhenActuallyInMeleeRange_ReturnsFalse()
    {
        bool result = CombatGoal.ShouldCancelMeleeAutoAttackForRangedCombat(
            prefersRangedCombat: true,
            meleeAutoAttacking: true,
            shooting: false,
            inMeleeRange: true,
            withinCombatRange: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void AdhocGoal_ShouldSuppressDuringRecovery_WhenNonRecoveryActionAndFoodBuffActive_ReturnsTrue()
    {
        bool result = AdhocGoal.ShouldSuppressDuringRecovery(
            actionName: "Life Tap",
            hasFoodBuff: true,
            hasDrinkBuff: false,
            healthPercent: 80,
            manaPercent: 100);

        result.Should().BeTrue();
    }

    [Fact]
    public void AdhocGoal_ShouldSuppressDuringRecovery_WhenPetSummonAndDrinkBuffActive_ReturnsTrue()
    {
        bool result = AdhocGoal.ShouldSuppressDuringRecovery(
            actionName: "Summon Voidwalker",
            hasFoodBuff: false,
            hasDrinkBuff: true,
            healthPercent: 100,
            manaPercent: 70);

        result.Should().BeTrue();
    }

    [Fact]
    public void AdhocGoal_ShouldSuppressDuringRecovery_WhenFoodAction_ReturnsFalse()
    {
        bool result = AdhocGoal.ShouldSuppressDuringRecovery(
            actionName: "Food",
            hasFoodBuff: true,
            hasDrinkBuff: false,
            healthPercent: 80,
            manaPercent: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public void AdhocGoal_ShouldSuppressDuringRecovery_WhenNoRecoveryBuffs_ReturnsFalse()
    {
        bool result = AdhocGoal.ShouldSuppressDuringRecovery(
            actionName: "Create Healthstone",
            hasFoodBuff: false,
            hasDrinkBuff: false,
            healthPercent: 100,
            manaPercent: 100);

        result.Should().BeFalse();
    }

    [Fact]
    public void AdhocGoal_ShouldSuppressDuringRecovery_WhenBuffActiveButResourceFull_ReturnsFalse()
    {
        bool result = AdhocGoal.ShouldSuppressDuringRecovery(
            actionName: "Life Tap",
            hasFoodBuff: true,
            hasDrinkBuff: false,
            healthPercent: 100,
            manaPercent: 100);

        result.Should().BeFalse();
    }
}
