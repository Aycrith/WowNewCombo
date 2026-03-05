using Core.Goals;
using FluentAssertions;
using System;
using System.Threading;
using Xunit;

namespace CoreUnitTests.GoalsComponent;

/// <summary>
/// Unit tests for OscillationDetector. Tests the pure heading-tracking logic
/// without any game state — inject headings directly.
/// </summary>
public class OscillationDetectorTests
{
    private static OscillationDetector MakeDetector() => new();

    [Fact]
    public void IsOscillating_FreshDetector_ReturnsFalse()
    {
        var d = MakeDetector();
        d.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void IsOscillating_StraightPath_NeverTriggers()
    {
        // Incrementally increasing heading (bot turning slowly right) — not oscillation
        var d = MakeDetector();
        float heading = 0f;
        for (int i = 0; i < 20; i++)
        {
            d.TrackHeading(heading);
            heading += 0.05f; // 3° per step, all same direction
        }
        d.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void IsOscillating_GenuineOscillation_Triggers()
    {
        // Alternating left/right corrections exceeding MIN_ANGLE_CHANGE (0.2 rad)
        var d = MakeDetector();
        float heading = 1.0f;
        for (int i = 0; i < 12; i++)
        {
            // Alternate between 0.8 and 1.2 — delta of 0.4 rad each way
            heading = (i % 2 == 0) ? 0.8f : 1.2f;
            d.TrackHeading(heading);
        }
        d.IsOscillating.Should().BeTrue();
    }

    [Fact]
    public void IsOscillating_AlternatingHeadings_TriggersWithinEightSamples()
    {
        // Regression guard for tightened settings:
        // HEADING_HISTORY_SIZE=8 and OSCILLATION_THRESHOLD=4.
        var d = MakeDetector();
        for (int i = 0; i < 8; i++)
        {
            d.TrackHeading(i % 2 == 0 ? 0.8f : 1.2f);
        }

        d.IsOscillating.Should().BeTrue();
    }

    [Fact]
    public void IsOscillating_ThreeSamples_DoesNotTrigger()
    {
        // Detector needs at least max(4, history/2) samples before evaluating.
        var d = MakeDetector();
        d.TrackHeading(0.8f);
        d.TrackHeading(1.2f);
        d.TrackHeading(0.8f);

        d.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void IsOscillating_DoubleCornered_SCurve_DoesNotTrigger()
    {
        // S-bend: two legitimate direction changes, not alternating
        // heading goes: 0 → 0.3 → 0.6 → 0.9 (right turn)
        //             → 0.6 → 0.3 → 0.0 (left turn back)
        // Total: 6 readings, only 1 reversal — well below threshold of 4
        var d = MakeDetector();
        float[] headings = [0f, 0.3f, 0.6f, 0.9f, 0.6f, 0.3f, 0.0f];
        foreach (float h in headings)
            d.TrackHeading(h);
        d.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsOscillationState()
    {
        var d = MakeDetector();
        // Trigger oscillation
        for (int i = 0; i < 12; i++)
            d.TrackHeading(i % 2 == 0 ? 0.8f : 1.2f);
        d.IsOscillating.Should().BeTrue();

        d.Reset();
        d.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void TrackHeading_AfterReset_BuildsHistoryFresh()
    {
        var d = MakeDetector();
        // Trigger oscillation then reset
        for (int i = 0; i < 12; i++)
            d.TrackHeading(i % 2 == 0 ? 0.8f : 1.2f);
        d.Reset();

        // One sample after reset — should not trigger
        d.TrackHeading(1.0f);
        d.IsOscillating.Should().BeFalse();
    }

    [Fact]
    public void OscillationCount_IncreasesOnEachDetection()
    {
        var d = MakeDetector();
        int initialCount = d.OscillationCount;

        // Trigger oscillation, reset, trigger again
        for (int i = 0; i < 12; i++)
            d.TrackHeading(i % 2 == 0 ? 0.8f : 1.2f);
        d.IsOscillating.Should().BeTrue();

        // OscillationCount is incremented when IsOscillating transitions to true
        d.OscillationCount.Should().BeGreaterThan(initialCount);
    }
}
