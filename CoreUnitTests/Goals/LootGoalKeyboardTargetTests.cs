using Core.Goals;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

public sealed class LootGoalKeyboardTargetTests
{
    [Fact]
    public void ClassifyKeyboardLootTarget_WhenTargetIsPet_ReturnsPetTarget()
    {
        LootGoal.KeyboardLootTargetIssue issue = LootGoal.ClassifyKeyboardLootTarget(
            hasTarget: true,
            targetDead: false,
            targetGuid: 42,
            petGuid: 42);

        issue.Should().Be(LootGoal.KeyboardLootTargetIssue.PetTarget);
    }

    [Fact]
    public void ClassifyKeyboardLootTarget_WhenTargetIsAliveNonPet_ReturnsAliveTarget()
    {
        LootGoal.KeyboardLootTargetIssue issue = LootGoal.ClassifyKeyboardLootTarget(
            hasTarget: true,
            targetDead: false,
            targetGuid: 42,
            petGuid: 84);

        issue.Should().Be(LootGoal.KeyboardLootTargetIssue.AliveTarget);
    }

    [Fact]
    public void ClassifyKeyboardLootTarget_WhenTargetIsDeadNonPet_ReturnsNone()
    {
        LootGoal.KeyboardLootTargetIssue issue = LootGoal.ClassifyKeyboardLootTarget(
            hasTarget: true,
            targetDead: true,
            targetGuid: 42,
            petGuid: 84);

        issue.Should().Be(LootGoal.KeyboardLootTargetIssue.None);
    }

    [Fact]
    public void ShouldSkipKeyboardRetryAfterPetRefusal_WhenPetWasRefused_ReturnsTrue()
    {
        bool shouldSkip = LootGoal.ShouldSkipKeyboardRetryAfterPetRefusal(petTargetRefusedThisWindow: true);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetryLastTargetAfterTargetClear_WhenTargetWasCleared_ReturnsFalse()
    {
        bool shouldRetry = LootGoal.ShouldRetryLastTargetAfterTargetClear(targetClearedThisWindow: true);

        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptTrackedCorpseCandidateAfterTargetClear_WhenCandidateExists_ReturnsTrue()
    {
        bool shouldAttempt = LootGoal.ShouldAttemptTrackedCorpseCandidateAfterTargetClear(
            targetClearedThisWindow: true,
            hasTrackedCorpseCandidate: true);

        shouldAttempt.Should().BeTrue();
    }

    [Fact]
    public void ShouldAttemptTrackedCorpseCandidateAfterTargetClear_WhenNoCandidateExists_ReturnsFalse()
    {
        bool shouldAttempt = LootGoal.ShouldAttemptTrackedCorpseCandidateAfterTargetClear(
            targetClearedThisWindow: true,
            hasTrackedCorpseCandidate: false);

        shouldAttempt.Should().BeFalse();
    }

    [Fact]
    public void ShouldTryDirectTrackedCorpseRecovery_WhenPetWasRefusedAndCandidateExists_ReturnsTrue()
    {
        bool shouldAttempt = LootGoal.ShouldTryDirectTrackedCorpseRecovery(
            petTargetRefusedThisWindow: true,
            hasTrackedCorpseCandidate: true);

        shouldAttempt.Should().BeTrue();
    }

    [Fact]
    public void ShouldTryDirectTrackedCorpseRecovery_WhenPetWasNotRefused_ReturnsFalse()
    {
        bool shouldAttempt = LootGoal.ShouldTryDirectTrackedCorpseRecovery(
            petTargetRefusedThisWindow: false,
            hasTrackedCorpseCandidate: true);

        shouldAttempt.Should().BeFalse();
    }

    [Fact]
    public void ShouldTrySecondaryTrackedCorpseCandidate_WhenPrimaryFailedAndSecondaryExists_ReturnsTrue()
    {
        bool shouldAttempt = LootGoal.ShouldTrySecondaryTrackedCorpseCandidate(
            primaryCandidateFailed: true,
            hasSecondaryCandidate: true,
            unresolvedTrackedCorpseCount: 2);

        shouldAttempt.Should().BeTrue();
    }

    [Fact]
    public void ShouldTrySecondaryTrackedCorpseCandidate_WhenNoSecondaryExists_ReturnsFalse()
    {
        bool shouldAttempt = LootGoal.ShouldTrySecondaryTrackedCorpseCandidate(
            primaryCandidateFailed: true,
            hasSecondaryCandidate: false,
            unresolvedTrackedCorpseCount: 2);

        shouldAttempt.Should().BeFalse();
    }

    [Fact]
    public void ShouldTrySecondaryTrackedCorpseCandidate_WhenOnlyOneCorpseRemains_ReturnsFalse()
    {
        bool shouldAttempt = LootGoal.ShouldTrySecondaryTrackedCorpseCandidate(
            primaryCandidateFailed: true,
            hasSecondaryCandidate: true,
            unresolvedTrackedCorpseCount: 1);

        shouldAttempt.Should().BeFalse();
    }

    [Fact]
    public void ShouldContinuePassivePetClearWait_WhenPetStillBlockingAndNoCorpseSignals_ReturnsTrue()
    {
        bool shouldContinue = LootGoal.ShouldContinuePassivePetClearWait(
            lootWindowOpen: false,
            hasEligibleCorpseTarget: false,
            corpseNameVisible: false,
            petStillBlocking: true);

        shouldContinue.Should().BeTrue();
    }

    [Fact]
    public void ShouldContinuePassivePetClearWait_WhenCorpseTargetBecomesValid_ReturnsFalse()
    {
        bool shouldContinue = LootGoal.ShouldContinuePassivePetClearWait(
            lootWindowOpen: false,
            hasEligibleCorpseTarget: true,
            corpseNameVisible: false,
            petStillBlocking: true);

        shouldContinue.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetryDirectCorpseProbe_WhenAttemptBudgetRemainsAndCandidateStillInRange_ReturnsTrue()
    {
        bool shouldRetry = LootGoal.ShouldRetryDirectCorpseProbe(
            attemptsUsed: 1,
            maxAttempts: 2,
            corpseCandidateStillInRange: true,
            lootWindowOpen: false);

        shouldRetry.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetryDirectCorpseProbe_WhenAttemptBudgetExhausted_ReturnsFalse()
    {
        bool shouldRetry = LootGoal.ShouldRetryDirectCorpseProbe(
            attemptsUsed: 2,
            maxAttempts: 2,
            corpseCandidateStillInRange: true,
            lootWindowOpen: false);

        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public void GetLootInteractionTimeoutMs_WhenLatencyIsLow_UsesTimeoutFloor()
    {
        int timeoutMs = LootGoal.GetLootInteractionTimeoutMs(doubleNetworkLatencyMs: 80);

        timeoutMs.Should().Be(400);
    }

    [Fact]
    public void GetLootInteractionTimeoutMs_WhenLatencyIsHigh_UsesLatency()
    {
        int timeoutMs = LootGoal.GetLootInteractionTimeoutMs(doubleNetworkLatencyMs: 650);

        timeoutMs.Should().Be(650);
    }

    [Fact]
    public void ShouldProbeTrackedCorpseBeforeCursorFallback_WhenPetWasRefused_ReturnsFalse()
    {
        bool shouldProbe = LootGoal.ShouldProbeTrackedCorpseBeforeCursorFallback(
            petTargetRefusedThisWindow: true);

        shouldProbe.Should().BeFalse();
    }

    [Fact]
    public void ShouldFaceClosestCorpseBeforeCursorFallback_WhenPetWasRefusedAndCorpseTracked_ReturnsTrue()
    {
        bool shouldFace = LootGoal.ShouldFaceClosestCorpseBeforeCursorFallback(
            petTargetRefusedThisWindow: true,
            corpseLocationCount: 1);

        shouldFace.Should().BeTrue();
    }

    [Fact]
    public void ShouldWaitForPetToClearCorpse_WhenPetWasRefusedAndCorpseTracked_ReturnsTrue()
    {
        bool shouldWait = LootGoal.ShouldWaitForPetToClearCorpse(
            petTargetRefusedThisWindow: true,
            refusedLootTargetGuid: 42,
            petGuid: 42,
            corpseLocationCount: 1);

        shouldWait.Should().BeTrue();
    }

    [Fact]
    public void ShouldWaitForPetToClearCorpse_WhenNoTrackedCorpse_ReturnsFalse()
    {
        bool shouldWait = LootGoal.ShouldWaitForPetToClearCorpse(
            petTargetRefusedThisWindow: true,
            refusedLootTargetGuid: 42,
            petGuid: 42,
            corpseLocationCount: 0);

        shouldWait.Should().BeFalse();
    }

    [Fact]
    public void ShouldWaitForPetToClearCorpse_WhenRefusedTargetIsNotPet_ReturnsFalse()
    {
        bool shouldWait = LootGoal.ShouldWaitForPetToClearCorpse(
            petTargetRefusedThisWindow: true,
            refusedLootTargetGuid: 41,
            petGuid: 42,
            corpseLocationCount: 1);

        shouldWait.Should().BeFalse();
    }

    [Fact]
    public void IsTrackedCorpseCandidateInInteractRange_WhenDistanceWithinFiveYards_ReturnsTrue()
    {
        bool inRange = LootGoal.IsTrackedCorpseCandidateInInteractRange(corpseDistanceYards: 4.9f);

        inRange.Should().BeTrue();
    }

    [Fact]
    public void IsTrackedCorpseCandidateInInteractRange_WhenDistanceBeyondFiveYards_ReturnsFalse()
    {
        bool inRange = LootGoal.IsTrackedCorpseCandidateInInteractRange(corpseDistanceYards: 5.1f);

        inRange.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptLootOpenAfterCorpseAcquire_WhenDeadTargetInRangeWithoutLootWindow_ReturnsTrue()
    {
        bool shouldAttempt = LootGoal.ShouldAttemptLootOpenAfterCorpseAcquire(
            hasTarget: true,
            targetDead: true,
            lootWindowOpen: false,
            inLootRange: true);

        shouldAttempt.Should().BeTrue();
    }

    [Fact]
    public void ShouldAttemptLootOpenAfterCorpseAcquire_WhenLootWindowAlreadyOpen_ReturnsFalse()
    {
        bool shouldAttempt = LootGoal.ShouldAttemptLootOpenAfterCorpseAcquire(
            hasTarget: true,
            targetDead: true,
            lootWindowOpen: true,
            inLootRange: true);

        shouldAttempt.Should().BeFalse();
    }

    [Fact]
    public void ClassifyPetRefusedLootOutcome_WhenLooted_ReturnsLooted()
    {
        LootGoal.PetRefusedLootOutcome outcome = LootGoal.ClassifyPetRefusedLootOutcome(
            looted: true,
            corpseInteractionObserved: true,
            directCorpseCandidateProbeAttempted: true);

        outcome.Should().Be(LootGoal.PetRefusedLootOutcome.Looted);
    }

    [Fact]
    public void ClassifyPetRefusedLootOutcome_WhenCorpseWasNeverFound_ReturnsCorpseNotFound()
    {
        LootGoal.PetRefusedLootOutcome outcome = LootGoal.ClassifyPetRefusedLootOutcome(
            looted: false,
            corpseInteractionObserved: false,
            directCorpseCandidateProbeAttempted: false);

        outcome.Should().Be(LootGoal.PetRefusedLootOutcome.CorpseNotFound);
    }

    [Fact]
    public void ClassifyPetRefusedLootOutcome_WhenInteractFailedAfterCorpseDetection_ReturnsInteractFailed()
    {
        LootGoal.PetRefusedLootOutcome outcome = LootGoal.ClassifyPetRefusedLootOutcome(
            looted: false,
            corpseInteractionObserved: true,
            directCorpseCandidateProbeAttempted: false);

        outcome.Should().Be(LootGoal.PetRefusedLootOutcome.InteractFailed);
    }

    [Fact]
    public void ClassifyPetRefusedLootOutcome_WhenDirectProbeAttemptedWithoutLoot_ReturnsInteractFailed()
    {
        LootGoal.PetRefusedLootOutcome outcome = LootGoal.ClassifyPetRefusedLootOutcome(
            looted: false,
            corpseInteractionObserved: false,
            directCorpseCandidateProbeAttempted: true);

        outcome.Should().Be(LootGoal.PetRefusedLootOutcome.InteractFailed);
    }
}
