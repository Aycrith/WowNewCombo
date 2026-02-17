using Core.Goals;
using FluentAssertions;
using System;
using Xunit;

namespace CoreUnitTests.GoalsComponent;

/// <summary>
/// Tests for OscillationDetector and HumanizedMover movement components.
/// </summary>
public class MovementTests
{
    #region OscillationDetector - Initial State

    [Fact]
    public void OscillationDetector_NewInstance_IsNotOscillating()
    {
        // Arrange & Act
        OscillationDetector detector = new();

        // Assert
        detector.IsOscillating.Should().BeFalse();
        detector.OscillationCount.Should().Be(0);
    }

    #endregion

    #region OscillationDetector - Normal Movement (No Oscillation)

    [Fact]
    public void OscillationDetector_SteadyHeading_DoesNotDetectOscillation()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - same heading repeatedly
        for (int i = 0; i < 15; i++)
        {
            detector.TrackHeading(1.0f);
        }

        // Assert
        detector.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void OscillationDetector_GradualTurn_DoesNotDetectOscillation()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - smooth gradual heading change
        for (int i = 0; i < 15; i++)
        {
            detector.TrackHeading(0.1f * i);
        }

        // Assert
        detector.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void OscillationDetector_SmallJitter_DoesNotDetectOscillation()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - small angle changes below MIN_ANGLE_CHANGE_FOR_OSCILLATION (0.2f)
        for (int i = 0; i < 15; i++)
        {
            float heading = 1.0f + (i % 2 == 0 ? 0.05f : -0.05f);
            detector.TrackHeading(heading);
        }

        // Assert
        detector.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void OscillationDetector_TooFewEntries_DoesNotDetectOscillation()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - add fewer entries than HEADING_HISTORY_SIZE (12)
        for (int i = 0; i < 5; i++)
        {
            float heading = i % 2 == 0 ? 3.0f : -3.0f;
            detector.TrackHeading(heading);
        }

        // Assert - not enough history to detect
        detector.IsOscillating.Should().BeFalse();
    }

    #endregion

    #region OscillationDetector - Detection of Back-and-Forth Movement

    [Fact]
    public void OscillationDetector_RapidBackAndForth_DetectsOscillation()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - rapid alternation between two widely separated headings
        // This creates direction reversals on every step
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 0.0f : MathF.PI;
            detector.TrackHeading(heading);
        }

        // Assert
        detector.IsOscillating.Should().BeTrue();
        detector.OscillationCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void OscillationDetector_AlternatingLargeAngles_DetectsOscillation()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - alternating between two headings with large angle difference
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 1.0f : -1.0f;
            detector.TrackHeading(heading);
        }

        // Assert
        detector.IsOscillating.Should().BeTrue();
    }

    [Fact]
    public void OscillationDetector_MultipleOscillationBursts_IncrementsCount()
    {
        // Arrange
        OscillationDetector detector = new();

        // Act - first oscillation burst
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 0.0f : MathF.PI;
            detector.TrackHeading(heading);
        }

        int countAfterFirst = detector.OscillationCount;

        // Reset and do another burst
        detector.Reset();
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 0.5f : -0.5f;
            detector.TrackHeading(heading);
        }

        // Assert - count should have increased beyond the first burst
        detector.OscillationCount.Should().BeGreaterThan(countAfterFirst);
    }

    #endregion

    #region OscillationDetector - Reset Behavior

    [Fact]
    public void OscillationDetector_Reset_ClearsOscillatingState()
    {
        // Arrange
        OscillationDetector detector = new();

        // Trigger oscillation first
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 0.0f : MathF.PI;
            detector.TrackHeading(heading);
        }
        detector.IsOscillating.Should().BeTrue();

        // Act
        detector.Reset();

        // Assert
        detector.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void OscillationDetector_Reset_AllowsNewDetectionAfterReset()
    {
        // Arrange
        OscillationDetector detector = new();

        // Trigger oscillation
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 0.0f : MathF.PI;
            detector.TrackHeading(heading);
        }

        // Reset
        detector.Reset();
        detector.IsOscillating.Should().BeFalse();

        // Act - add smooth movement after reset
        for (int i = 0; i < 15; i++)
        {
            detector.TrackHeading(0.1f * i);
        }

        // Assert - smooth movement should not trigger oscillation
        detector.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void OscillationDetector_Reset_PreservesOscillationCount()
    {
        // Arrange
        OscillationDetector detector = new();

        // Trigger oscillation to increment count
        for (int i = 0; i < 15; i++)
        {
            float heading = i % 2 == 0 ? 0.0f : MathF.PI;
            detector.TrackHeading(heading);
        }
        int countBeforeReset = detector.OscillationCount;

        // Act
        detector.Reset();

        // Assert - OscillationCount is not reset by Reset()
        // (Reset only clears headingHistory and IsOscillating)
        detector.OscillationCount.Should().Be(countBeforeReset);
    }

    #endregion

    #region HumanizedMover - Construction

    [Fact]
    public void HumanizedMover_NullProvider_CanBeConstructed()
    {
        // Act - should not throw with null provider
        HumanizedMover mover = new(null);

        // Assert - just verify construction succeeds
        mover.Should().NotBeNull();
    }

    [Fact]
    public void HumanizedMover_ApplyWaypointDelay_NullProvider_DoesNotThrow()
    {
        // Arrange
        HumanizedMover mover = new(null);
        using System.Threading.CancellationTokenSource cts = new();

        // Act & Assert - should not throw
        mover.ApplyWaypointDelay(cts.Token);
    }

    #endregion
}
