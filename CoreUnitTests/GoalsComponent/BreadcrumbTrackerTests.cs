using Core.GoalsComponent;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class BreadcrumbTrackerTests
{
    [Fact]
    public void RecordPosition_RespectsMinDistance_AndCounters()
    {
        BreadcrumbTracker tracker = new(maxSize: 10, minDistance: 5f);

        bool firstRecorded = tracker.RecordPosition(new Vector3(10f, 10f, 0f), mapId: 1);
        bool secondRecorded = tracker.RecordPosition(new Vector3(12f, 10f, 0f), mapId: 1); // distance < 5, should skip
        bool thirdRecorded = tracker.RecordPosition(new Vector3(20f, 10f, 0f), mapId: 1);

        Assert.True(firstRecorded);
        Assert.False(secondRecorded);
        Assert.True(thirdRecorded);

        Assert.Equal(2, tracker.Count);
        Assert.Equal(2, tracker.TotalRecorded);
        Assert.Equal(1, tracker.TotalSkipped);
    }

    [Fact]
    public void RecordPosition_EvictsOldest_WhenCapacityExceeded()
    {
        BreadcrumbTracker tracker = new(maxSize: 3, minDistance: 0f);

        tracker.RecordPosition(new Vector3(1f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(2f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(3f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(4f, 0f, 0f), mapId: 1);

        IReadOnlyList<BreadcrumbEntry> trail = tracker.GetTrail();
        Assert.Equal(3, trail.Count);
        Assert.Equal(new Vector3(2f, 0f, 0f), trail[0].Position);
        Assert.Equal(new Vector3(4f, 0f, 0f), trail[2].Position);
    }

    [Fact]
    public void GetBacktrackPosition_ReturnsExpectedReverseRelativeEntries()
    {
        BreadcrumbTracker tracker = new(maxSize: 10, minDistance: 0f);
        tracker.RecordPosition(new Vector3(1f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(2f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(3f, 0f, 0f), mapId: 1);

        BreadcrumbEntry? latest = tracker.GetBacktrackPosition(1);
        BreadcrumbEntry? twoBack = tracker.GetBacktrackPosition(2);
        BreadcrumbEntry? threeBack = tracker.GetBacktrackPosition(3);
        BreadcrumbEntry? outOfRange = tracker.GetBacktrackPosition(4);

        Assert.NotNull(latest);
        Assert.NotNull(twoBack);
        Assert.NotNull(threeBack);
        Assert.Null(outOfRange);

        Assert.Equal(new Vector3(3f, 0f, 0f), latest.Value.Position);
        Assert.Equal(new Vector3(2f, 0f, 0f), twoBack.Value.Position);
        Assert.Equal(new Vector3(1f, 0f, 0f), threeBack.Value.Position);
    }

    [Fact]
    public void TrailAccessors_RemainConsistent_AfterMixedWrites()
    {
        BreadcrumbTracker tracker = new(maxSize: 5, minDistance: 2f);
        tracker.RecordPosition(new Vector3(0f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(1f, 0f, 0f), mapId: 1); // skipped
        tracker.RecordPosition(new Vector3(3f, 0f, 0f), mapId: 1);
        tracker.RecordPosition(new Vector3(6f, 0f, 0f), mapId: 1);

        BreadcrumbEntry? latest = tracker.GetLatest();
        BreadcrumbEntry? oldest = tracker.GetOldest();
        IReadOnlyList<BreadcrumbEntry> trail = tracker.GetTrail();

        Assert.NotNull(latest);
        Assert.NotNull(oldest);
        Assert.Equal(trail.Count, tracker.Count);
        Assert.Equal(oldest.Value.Position, trail[0].Position);
        Assert.Equal(latest.Value.Position, trail[^1].Position);
    }

    [Fact]
    public async Task ConcurrencySmoke_ConcurrentReadsAndWrites_DoNotThrow_AndKeepInvariants()
    {
        BreadcrumbTracker tracker = new(maxSize: 64, minDistance: 0.5f);
        ConcurrentQueue<Exception> errors = [];

        Task writer = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 4_000; i++)
                {
                    tracker.RecordPosition(new Vector3(i * 0.25f, i * 0.10f, 0f), mapId: 1);
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        Task reader = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 4_000; i++)
                {
                    _ = tracker.GetTrail();
                    _ = tracker.GetBacktrackPosition(1);
                    _ = tracker.GetBacktrackPosition(8);
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        await Task.WhenAll(writer, reader);

        Assert.Empty(errors);
        Assert.InRange(tracker.Count, 0, tracker.MaxSize);
        Assert.True(tracker.TotalRecorded >= tracker.Count);
    }
}
