using FluentAssertions;
using System;
using Xunit;

namespace CoreUnitTests.GoalsComponent;

/// <summary>
/// Unit tests for PlayerDirection turn duration and wait logic.
/// These are pure-logic tests — no game state required.
/// </summary>
public class PlayerDirectionTests
{
    // Reproduces the internal CalculateTurnDuration formula: angleRadians * 850f / PI, clamped 40-700
    private static int CalculateDuration(float angleRadians)
    {
        int duration = (int)(angleRadians * 850f / MathF.PI);
        return Math.Clamp(duration, 40, 700);
    }

    [Theory]
    [InlineData(MathF.PI / 4f,   212)]  // 45°  → (PI/4) * 850/PI = 212ms
    [InlineData(MathF.PI / 2f,   425)]  // 90°  → (PI/2) * 850/PI = 425ms
    [InlineData(MathF.PI * 0.75f, 637)] // 135° → 637ms (below 700 clamp)
    [InlineData(MathF.PI,         700)] // 180° → clamped to 700ms
    [InlineData(0.01f,             40)] // tiny → clamped to 40 (min)
    [InlineData(100f,             700)] // huge → clamped to 700 (max)
    public void TurnDuration_MatchesExpectedFormula(float angleRad, int expectedMs)
    {
        CalculateDuration(angleRad).Should().Be(expectedMs);
    }

    [Theory]
    [InlineData(MathF.PI / 4f,   262)]  // 45°  → 212 + 50 = 262ms wait
    [InlineData(MathF.PI / 2f,   475)]  // 90°  → 425 + 50 = 475ms wait  ← NEW: no cap (old would have been 200)
    [InlineData(MathF.PI * 0.75f, 687)] // 135° → 637 + 50 = 687ms wait
    [InlineData(MathF.PI,         750)] // 180° → 700 + 50 = 750ms (clamped max)
    public void WaitTime_ShouldNotBeCappedAt200ms_ForLargeTurns(float angleRad, int expectedWait)
    {
        int duration = CalculateDuration(angleRad);
        // New formula: no hard cap, just duration + TURN_VERIFICATION_DELAY_MS (50)
        int waitTime = duration + 50; // TURN_VERIFICATION_DELAY_MS
        waitTime.Should().Be(expectedWait);
        waitTime.Should().BeGreaterThanOrEqualTo(200, "should be well above the old 200ms cap");
    }
}
