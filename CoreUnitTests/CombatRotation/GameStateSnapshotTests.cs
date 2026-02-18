using Core.CombatRotation;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public sealed class GameStateSnapshotTests
{
    private static GameStateSnapshot CreateSnapshot(
        int healthPercent = 85,
        int resourcePercent = 60,
        int resourceCurrent = 60,
        int resourceMax = 100,
        int comboPoints = 0,
        int targetHealthPercent = 50,
        int gcdRemainingMs = 0,
        int networkLatencyMs = 50,
        int spellQueueMs = 400,
        int mainHandSwingElapsedMs = 0,
        int mainHandSpeedMs = 2600,
        int mobCount = 1,
        bool inCombat = true,
        bool targetAlive = true,
        bool isTargetCasting = false,
        long tickTimestamp = 0)
    {
        return new GameStateSnapshot(
            healthPercent, resourcePercent, resourceCurrent, resourceMax,
            comboPoints, targetHealthPercent, gcdRemainingMs, networkLatencyMs,
            spellQueueMs, mainHandSwingElapsedMs, mainHandSpeedMs, mobCount,
            inCombat, targetAlive, isTargetCasting, tickTimestamp);
    }

    [Fact]
    public void IsExecutePhase_WhenTargetBelow20_ReturnsTrue()
    {
        GameStateSnapshot snapshot = CreateSnapshot(targetHealthPercent: 19);
        Assert.True(snapshot.IsExecutePhase);
    }

    [Fact]
    public void IsExecutePhase_WhenTargetAt20_ReturnsTrue()
    {
        GameStateSnapshot snapshot = CreateSnapshot(targetHealthPercent: 20);
        Assert.True(snapshot.IsExecutePhase);
    }

    [Fact]
    public void IsExecutePhase_WhenTargetAbove20_ReturnsFalse()
    {
        GameStateSnapshot snapshot = CreateSnapshot(targetHealthPercent: 21);
        Assert.False(snapshot.IsExecutePhase);
    }

    [Fact]
    public void IsExecutePhase_WhenTargetAt0_ReturnsFalse()
    {
        // 0 means no target or dead, not execute phase
        GameStateSnapshot snapshot = CreateSnapshot(targetHealthPercent: 0);
        Assert.False(snapshot.IsExecutePhase);
    }

    [Fact]
    public void ResourceFraction_CalculatesCorrectly()
    {
        GameStateSnapshot snapshot = CreateSnapshot(resourceCurrent: 50, resourceMax: 200);
        Assert.Equal(0.25f, snapshot.ResourceFraction);
    }

    [Fact]
    public void ResourceFraction_WhenMaxZero_ReturnsZero()
    {
        GameStateSnapshot snapshot = CreateSnapshot(resourceCurrent: 50, resourceMax: 0);
        Assert.Equal(0f, snapshot.ResourceFraction);
    }

    [Fact]
    public void ProjectedResourceAtGcdEnd_ForecastsCorrectly()
    {
        GameStateSnapshot snapshot = CreateSnapshot(
            resourceCurrent: 40, resourceMax: 100, gcdRemainingMs: 500);

        // 500ms * 0.1 per ms = 50 regen, 40 + 50 = 90, capped at 100
        int projected = snapshot.ProjectedResourceAtGcdEnd(regenPerMs: 0.1f);
        Assert.Equal(90, projected);
    }

    [Fact]
    public void ProjectedResourceAtGcdEnd_CapsAtMax()
    {
        GameStateSnapshot snapshot = CreateSnapshot(
            resourceCurrent: 90, resourceMax: 100, gcdRemainingMs: 500);

        // 500ms * 0.1 = 50, 90 + 50 = 140, capped at 100
        int projected = snapshot.ProjectedResourceAtGcdEnd(regenPerMs: 0.1f);
        Assert.Equal(100, projected);
    }

    [Fact]
    public void Default_HasZeroValues()
    {
        GameStateSnapshot snapshot = default;

        Assert.Equal(0, snapshot.HealthPercent);
        Assert.Equal(0, snapshot.ResourcePercent);
        Assert.Equal(0, snapshot.ComboPoints);
        Assert.Equal(0, snapshot.MobCount);
        Assert.False(snapshot.InCombat);
        Assert.False(snapshot.IsExecutePhase);
    }
}
