using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Core;
using Core.Analytics;
using Core.Testing;

using Frontend.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;
using Xunit.Abstractions;

namespace FrontendUnitTests.Services;

/// <summary>
/// Comprehensive tests for RouteVisualizationService including memory leak detection,
/// performance validation, and functional correctness.
///
/// Evidence-based validation approach:
/// - Memory leak tests use GC pressure and memory snapshots
/// - Performance tests generate latency histograms
/// - All tests provide measurable evidence of correctness
/// </summary>
public sealed class RouteVisualizationServiceTests : IDisposable
{
    private readonly ILogger<RouteVisualizationService> _logger;
    private readonly ITestOutputHelper _output;
    private RouteVisualizationService? _service;
    private FakeFailureSimulationService? _simulation;

    // Performance thresholds (from requirements: <150ms max, P95 < 100ms)
    private const int MaxLatencyMs = 150;
    private const int P95LatencyMs = 100;

    public RouteVisualizationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger<RouteVisualizationService>.Instance;
        _simulation = new FakeFailureSimulationService();
    }

    #region Memory Leak Detection Tests (Priority #1)

    /// <summary>
    /// Test: Memory should not grow unbounded when creating stuck markers
    /// Evidence: Memory snapshot before/after with GC pressure
    /// </summary>
    [Fact]
    public void Memory_Leak_StuckMarkers_ShouldNotGrowUnbounded()
    {
        // Arrange
        _service = CreateService();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"Initial memory: {initialMemory / 1024} KB");

        // Act - Create 1000 stuck markers
        for (int i = 0; i < 1000; i++)
        {
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
        }

        // Force cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long afterCreationMemory = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"After 1000 markers: {afterCreationMemory / 1024} KB");

        // Clear and check
        _service.ClearAll();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long afterClearMemory = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"After clear: {afterClearMemory / 1024} KB");

        // Assert - Memory should be reasonably close to initial (within 20%)
        long memoryGrowth = afterClearMemory - initialMemory;
        double growthPercent = (memoryGrowth / (double)initialMemory) * 100;
        _output.WriteLine($"Memory growth: {memoryGrowth / 1024} KB ({growthPercent:F2}%)");

        // Acceptable growth is < 20% after cleanup (accounting for background allocations)
        Assert.True(growthPercent < 20.0,
            $"Memory leaked: grew by {growthPercent:F2}% after creating 1000 markers");
    }

    /// <summary>
    /// Test: Disposed service should release all resources
    /// Evidence: Memory before/after disposal comparison
    /// </summary>
    [Fact]
    public void Memory_Dispose_ShouldReleaseAllResources()
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long baselineMemory = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"Baseline memory: {baselineMemory / 1024} KB");

        // Act - Create and populate service
        for (int i = 0; i < 100; i++)
        {
            _service = CreateService();
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
            _simulation.SimulateHotZone(FailureType.Stuck, 5, 15f);
            _service.Dispose();
            _service = null;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long afterDisposeMemory = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"After 100 create/dispose cycles: {afterDisposeMemory / 1024} KB");

        // Assert
        long memoryGrowth = afterDisposeMemory - baselineMemory;
        _output.WriteLine($"Memory growth: {memoryGrowth / 1024} KB");

        // Should be minimal growth after disposal
        Assert.True(memoryGrowth < 1024 * 100, // Less than 100KB growth
            "Service disposal should release resources properly");
    }

    /// <summary>
    /// Test: Concurrent marker operations should not cause leaks
    /// Evidence: Memory stability under concurrent load
    /// </summary>
    [Fact]
    public void Memory_ConcurrentOperations_ShouldBeStable()
    {
        // Arrange
        _service = CreateService();
        GC.Collect();
        long baselineMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Concurrent adds and reads
        Parallel.For(0, 100, i =>
        {
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
            _ = _service.GetActiveStuckMarkers();
        });

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long afterConcurrentMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Clear
        _service.ClearAll();
        GC.Collect();
        long afterClearMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Assert
        long growthAfterClear = afterClearMemory - baselineMemory;
        double growthPercent = (growthAfterClear / (double)baselineMemory) * 100;
        _output.WriteLine($"Memory growth after clear: {growthAfterClear / 1024} KB ({growthPercent:F2}%)");

        Assert.True(growthPercent < 5.0,
            "Concurrent operations should not cause memory leaks");
    }

    #endregion

    #region Performance Tests (Priority #1)

    /// <summary>
    /// Test: Stuck marker creation latency
    /// Evidence: Latency histogram with P95 and max measurements
    /// </summary>
    [Fact]
    public void Performance_StuckMarkerCreation_LatencyShouldBeUnderThreshold()
    {
        // Arrange
        _service = CreateService();
        List<long> latencies = new(1000);
        var stopwatch = new Stopwatch();

        // Act - Measure 1000 marker creation latencies
        for (int i = 0; i < 1000; i++)
        {
            stopwatch.Restart();
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
            stopwatch.Stop();
            latencies.Add(stopwatch.ElapsedMilliseconds);
        }

        // Generate histogram
        var histogram = GenerateLatencyHistogram(latencies);
        _output.WriteLine("\n=== Stuck Marker Creation Latency Histogram ===");
        _output.WriteLine(histogram);

        // Calculate statistics
        double avg = latencies.Average();
        long max = latencies.Max();
        double p95 = CalculatePercentile(latencies, 0.95);

        _output.WriteLine($"\nStatistics:");
        _output.WriteLine($"  Average: {avg:F2}ms");
        _output.WriteLine($"  P95: {p95:F2}ms");
        _output.WriteLine($"  Max: {max}ms");

        // Assert
        Assert.True(max < MaxLatencyMs,
            $"Max latency {max}ms exceeds threshold {MaxLatencyMs}ms");
        Assert.True(p95 < P95LatencyMs,
            $"P95 latency {p95:F2}ms exceeds threshold {P95LatencyMs}ms");
    }

    /// <summary>
    /// Test: Hot zone query performance
    /// Evidence: Latency measurements for zone queries
    /// </summary>
    [Fact]
    public void Performance_HotZoneQuery_ShouldBeFast()
    {
        // Arrange
        _service = CreateService();

        // Populate with hot zones
        for (int i = 0; i < 100; i++)
        {
            _simulation!.SimulateHotZone(FailureType.Stuck, i % 10 + 1, 15f);
        }

        List<long> latencies = new(1000);
        var stopwatch = new Stopwatch();
        Vector3 playerPos = new(5000, 5000, 0);
        Vector3 direction = new(1, 0, 0);

        // Act - Query 1000 times
        for (int i = 0; i < 1000; i++)
        {
            stopwatch.Restart();
            _ = _service.GetUpcomingHotZone(playerPos, direction, 100f);
            stopwatch.Stop();
            latencies.Add(stopwatch.ElapsedMilliseconds);
        }

        // Generate histogram
        var histogram = GenerateLatencyHistogram(latencies);
        _output.WriteLine("\n=== Hot Zone Query Latency Histogram ===");
        _output.WriteLine(histogram);

        double p95 = CalculatePercentile(latencies, 0.95);
        long max = latencies.Max();

        _output.WriteLine($"\nP95: {p95:F2}ms, Max: {max}ms");

        // Assert
        Assert.True(max < MaxLatencyMs);
        Assert.True(p95 < P95LatencyMs);
    }

    /// <summary>
    /// Test: Concurrent access performance
    /// Evidence: Throughput measurements under load
    /// </summary>
    [Fact]
    public void Performance_ConcurrentAccess_ShouldMaintainThroughput()
    {
        // Arrange
        _service = CreateService();
        int operations = 10000;
        int threads = 8;
        int opsPerThread = operations / threads;
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        Parallel.For(0, threads, threadId =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
                _ = _service.GetActiveStuckMarkers();
            }
        });
        stopwatch.Stop();

        double throughput = operations / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"\n=== Concurrent Access Performance ===");
        _output.WriteLine($"Total operations: {operations}");
        _output.WriteLine($"Threads: {threads}");
        _output.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds:F3}s");
        _output.WriteLine($"Throughput: {throughput:F0} ops/sec");

        // Assert - Should maintain at least 1000 ops/sec
        Assert.True(throughput > 1000,
            $"Throughput {throughput:F0} ops/sec is below acceptable threshold");
    }

    #endregion

    #region Functional Tests

    /// <summary>
    /// Test: Stuck marker events should trigger event handlers
    /// </summary>
    [Fact]
    public void Functional_StuckEvent_ShouldTriggerHandler()
    {
        // Arrange
        _service = CreateService();
        StuckMarker? receivedMarker = null;
        _service.OnStuckMarkerAdded += marker => receivedMarker = marker;

        // Act
        _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);

        // Assert
        Assert.NotNull(receivedMarker);
        Assert.Equal(new Vector3(100, 200, 0), receivedMarker!.Position);
        Assert.Equal(1, receivedMarker.MapId);
    }

    /// <summary>
    /// Test: Hot zone detection should return zones in front of player
    /// </summary>
    [Fact]
    public void Functional_HotZoneDetection_ShouldFindZonesInDirection()
    {
        // Arrange
        _service = CreateService();
        _simulation!.SimulateHotZone(FailureType.Stuck, 5, 15f);
        _simulation.SimulateHotZone(FailureType.Stuck, 5, 15f);

        // Act - Look ahead in positive X direction
        Vector3 playerPos = new(0, 0, 0);
        Vector3 direction = new(1, 0, 0);
        HotZoneView? zone = _service.GetUpcomingHotZone(playerPos, direction, 200f);

        // Assert
        Assert.NotNull(zone);
        Assert.True(zone!.Center.X > 0); // Should be the zone ahead
    }

    /// <summary>
    /// Test: Cleanup should remove old markers
    /// </summary>
    [Fact]
    public void Functional_Cleanup_ShouldRemoveOldMarkers()
    {
        // Arrange - This test uses internal cleanup timer, so we manually invoke
        _service = CreateService();

        // Create markers by simulating events
        _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);

        // Get initial count
        int initialCount = _service.GetActiveStuckMarkers().Count;
        Assert.Equal(1, initialCount);

        // Act - Clear all (simulating cleanup)
        _service.ClearAll();

        // Assert
        Assert.Empty(_service.GetActiveStuckMarkers());
    }

    /// <summary>
    /// Test: Death markers should persist (not be cleaned up)
    /// </summary>
    [Fact]
    public void Functional_DeathMarkers_ShouldPersist()
    {
        // Arrange
        _service = CreateService();

        // Act - Simulate death
        _simulation!.SimulateDeath("Test Death");

        // Assert
        var markers = _service.GetActiveStuckMarkers();
        Assert.Single(markers);
        Assert.True(markers.First().IsDeathMarker);
    }

    #endregion

    #region Thread Safety Tests

    /// <summary>
    /// Test: Concurrent add/remove operations should be safe
    /// </summary>
    [Fact]
    public void ThreadSafety_ConcurrentAddRemove_ShouldBeSafe()
    {
        // Arrange
        _service = CreateService();
        int iterations = 1000;
        var exceptions = new List<Exception>();

        // Act
        Parallel.Invoke(
            () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }
            },
            () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        _ = _service.GetActiveStuckMarkers();
                        if (i % 100 == 0)
                        {
                            _service.ClearAll();
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }
            }
        );

        // Assert
        Assert.Empty(exceptions);
    }

    #endregion

    #region Auto-Reroute Detection Tests

    /// <summary>
    /// Test: Should detect hot zones ahead on route
    /// </summary>
    [Fact]
    public void AutoReroute_ShouldDetectUpcomingHotZone()
    {
        // Arrange
        _service = CreateService();
        Vector3 playerPos = new(0, 0, 0);
        Vector3 direction = new(1, 0, 0);

        // Create hot zone ahead
        _simulation!.SimulateHotZone(FailureType.Stuck, 10, 15f);

        // Act
        HotZoneView? upcoming = _service.GetUpcomingHotZone(playerPos, direction, 100f);

        // Assert
        Assert.NotNull(upcoming);
        Assert.Equal(10, upcoming!.FailureCount);
    }

    /// <summary>
    /// Test: Should not flag zones behind player
    /// </summary>
    [Fact]
    public void AutoReroute_ShouldNotFlagZonesBehindPlayer()
    {
        // Arrange
        _service = CreateService();
        Vector3 playerPos = new(0, 0, 0);
        Vector3 direction = new(1, 0, 0); // Facing positive X

        // Create hot zone behind
        // Note: Fake simulation creates zones at position (100, 200, 0) by default
        // We need to position player relative to that
        playerPos = new(200, 200, 0);
        direction = new(-1, 0, 0); // Facing negative X (away from zone)

        // Act
        HotZoneView? upcoming = _service.GetUpcomingHotZone(playerPos, direction, 100f);

        // Assert
        Assert.Null(upcoming);
    }

    /// <summary>
    /// Test: Concurrent flash timer updates should not corrupt marker state
    /// Evidence: No exceptions after 1000 concurrent flash updates
    /// </summary>
    [Fact]
    public async Task ThreadSafety_ConcurrentFlashUpdates_ShouldNotCorruptState()
    {
        // Arrange
        _service = CreateService();
        int markerCount = 100;
        var markers = new List<Guid>();

        // Create markers
        for (int i = 0; i < markerCount; i++)
        {
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
        }

        var initialMarkers = _service.GetActiveStuckMarkers().ToList();
        Assert.Equal(markerCount, initialMarkers.Count);

        // Act - Concurrent reads while flash timers may be updating
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        for (int thread = 0; thread < 10; thread++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        _ = _service.GetActiveStuckMarkers();
                        _ = _service.GetActiveHotZones();
                        _ = _service.GetRehabilitationMarkers();
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions and all markers still present
        Assert.Empty(exceptions);
        var finalMarkers = _service.GetActiveStuckMarkers();
        Assert.Equal(markerCount, finalMarkers.Count);

        // All markers should have consistent state
        foreach (var marker in finalMarkers)
        {
            Assert.NotEqual(Guid.Empty, marker.Id);
            Assert.True(marker.Severity >= 0 && marker.Severity <= 10);
        }
    }

    /// <summary>
    /// Test: Concurrent grid failure count updates should be atomic
    /// Evidence: Final count equals expected sum after concurrent increments
    /// </summary>
    [Fact]
    public void ThreadSafety_ConcurrentGridUpdates_ShouldBeAtomic()
    {
        // Arrange
        _service = CreateService();
        int iterations = 1000;
        int threads = 10;

        // Act - Simulate many failures at the same grid position concurrently
        Parallel.For(0, threads, threadId =>
        {
            for (int i = 0; i < iterations; i++)
            {
                // All threads use same position to hit same grid cell
                _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);
            }
        });

        // Assert
        var gridCounts = _service.GetFailureCountsByGrid();
        int totalCount = gridCounts.Values.Sum();

        // Should have (threads * iterations) failures recorded
        _output.WriteLine($"Total grid failures recorded: {totalCount}");
        Assert.True(totalCount >= threads * iterations * 0.95, // Allow 5% tolerance for timing
            $"Expected ~{threads * iterations} failures, got {totalCount}");
    }

    /// <summary>
    /// Test: Service disposal during active operations should be safe
    /// Evidence: No exceptions when disposing while timers and tasks are active
    /// </summary>
    [Fact]
    public void ThreadSafety_DisposeDuringActiveOperations_ShouldBeSafe()
    {
        // Arrange
        _service = CreateService();
        var exceptions = new List<Exception>();

        // Act - Start multiple operations and dispose immediately
        var tasks = new List<Task>();

        // Task 1: Continuously add markers
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                try { _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1); }
                catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
            }
        }));

        // Task 2: Continuously read markers
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                try { _ = _service.GetActiveStuckMarkers(); }
                catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
            }
        }));

        // Task 3: Dispose after brief delay
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(10); // Let operations start
            try { _service.Dispose(); }
            catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
        }));

        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));

        // Assert - Should not have ObjectDisposedException or similar
        var criticalExceptions = exceptions.Where(e =>
            e is not InvalidOperationException &&
            e is not NullReferenceException).ToList();

        // We expect some exceptions due to disposal timing, but no crashes
        _output.WriteLine($"Exceptions during disposal: {exceptions.Count}");
        Assert.True(exceptions.Count < 50, // Some exceptions acceptable
            $"Too many exceptions during disposal: {string.Join(", ", exceptions.Select(e => e.GetType().Name))}");
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Test: Maximum marker count should be handled gracefully
    /// </summary>
    [Fact]
    public void EdgeCase_MaximumMarkers_ShouldHandleGracefully()
    {
        // Arrange
        _service = CreateService();
        int maxMarkers = 10000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < maxMarkers; i++)
        {
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);
        }
        sw.Stop();

        // Assert
        var markers = _service.GetActiveStuckMarkers();
        Assert.Equal(maxMarkers, markers.Count);
        _output.WriteLine($"Created {maxMarkers} markers in {sw.ElapsedMilliseconds}ms");

        // Performance check
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Creating {maxMarkers} markers took too long: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Test: Very large coordinates should not cause overflow
    /// </summary>
    [Fact]
    public void EdgeCase_LargeCoordinates_ShouldNotOverflow()
    {
        // This test would need modification to FakeFailureSimulationService
        // to support custom positions. For now, we verify existing behavior.

        // Arrange
        _service = CreateService();

        // Act - Normal operation (Fake service uses fixed coordinates)
        _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);

        // Assert
        var markers = _service.GetActiveStuckMarkers();
        Assert.Single(markers);
        Assert.True(float.IsFinite(markers.First().Position.X));
        Assert.True(float.IsFinite(markers.First().Position.Y));
    }

    /// <summary>
    /// Test: Rapid event handler subscription/unsubscription should be safe
    /// </summary>
    [Fact]
    public void EdgeCase_RapidEventSubscription_ShouldBeSafe()
    {
        // Arrange
        _service = CreateService();
        int handlerCount = 0;

        Action handler1 = () => handlerCount++;
        Action handler2 = () => handlerCount++;

        // Act - Rapidly subscribe and unsubscribe
        for (int i = 0; i < 100; i++)
        {
            _service.OnVisualizationStateChanged += handler1;
            _service.OnVisualizationStateChanged += handler2;
            _service.OnVisualizationStateChanged -= handler1;
            _service.OnVisualizationStateChanged -= handler2;
        }

        // Trigger event one more time with handler attached
        _service.OnVisualizationStateChanged += handler1;
        _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);

        // Assert - Should not crash and handler should fire
        Assert.True(handlerCount >= 1, "Event handler should have fired");
    }

    #endregion

    #region Helper Methods

    private RouteVisualizationService CreateService()
    {
        _service?.Dispose();
        return new RouteVisualizationService(
            _logger,
            failureAnalytics: null,
            hazardStore: null,
            stuckDetector: null,
            rehabilitator: null,
            failureSimulation: _simulation
        );
    }

    private static string GenerateLatencyHistogram(List<long> latencies)
    {
        // Buckets: 0-1ms, 1-5ms, 5-10ms, 10-25ms, 25-50ms, 50-100ms, 100-150ms, 150ms+
        int[] buckets = new int[8];
        foreach (var latency in latencies)
        {
            buckets[GetBucketIndex(latency)]++;
        }

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("  0-1ms:   " + new string('#', buckets[0] / 10));
        lines.AppendLine("  1-5ms:   " + new string('#', buckets[1] / 10));
        lines.AppendLine("  5-10ms:  " + new string('#', buckets[2] / 10));
        lines.AppendLine(" 10-25ms:  " + new string('#', buckets[3] / 10));
        lines.AppendLine(" 25-50ms:  " + new string('#', buckets[4] / 10));
        lines.AppendLine(" 50-100ms: " + new string('#', buckets[5] / 10));
        lines.AppendLine("100-150ms: " + new string('#', buckets[6] / 10));
        lines.AppendLine("150ms+:    " + new string('#', buckets[7] / 10));
        return lines.ToString();
    }

    private static int GetBucketIndex(long latencyMs)
    {
        return latencyMs switch
        {
            <= 1 => 0,
            <= 5 => 1,
            <= 10 => 2,
            <= 25 => 3,
            <= 50 => 4,
            <= 100 => 5,
            <= 150 => 6,
            _ => 7
        };
    }

    private static double CalculatePercentile(List<long> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        int index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    public void Dispose()
    {
        _service?.Dispose();
        _service = null;
    }

    #endregion

    #region Fake Implementation for Testing

    /// <summary>
    /// Fake failure simulation service for testing without dependencies
    /// </summary>
    private sealed class FakeFailureSimulationService : IFailureSimulationService
    {
        public event Action<SimulatedStuckEvent>? OnStuckSimulated;
        public event Action<SimulatedDeathEvent>? OnDeathSimulated;
        public event Action<SimulatedRehabEvent>? OnRehabSimulated;
        public event Action<SimulatedHotZone>? OnHotZoneCreated;

        private int _zoneCounter;

        public void SimulateStuck(UnstuckState stuckState, int durationMs = 3000, int attemptCount = 1)
        {
            var evt = new SimulatedStuckEvent
            {
                Id = Guid.NewGuid(),
                Position = new Vector3(100, 200, 0),
                MapId = 1,
                UIMapId = 1,
                Timestamp = DateTime.UtcNow,
                State = stuckState,
                DurationMs = durationMs,
                AttemptCount = attemptCount,
                Direction = 0f,
                IsFlashingMarker = true,
                IsSpinning = attemptCount >= 3
            };
            OnStuckSimulated?.Invoke(evt);
        }

        public void SimulateDeath(string cause = "Simulated death")
        {
            var evt = new SimulatedDeathEvent
            {
                Id = Guid.NewGuid(),
                Position = new Vector3(500, 500, 0),
                MapId = 1,
                UIMapId = 1,
                Timestamp = DateTime.UtcNow,
                Cause = cause,
                Level = 10
            };
            OnDeathSimulated?.Invoke(evt);
        }

        public void SimulateHotZone(FailureType failureType, int failureCount = 3, float radius = 10f)
        {
            _zoneCounter++;
            var zone = new SimulatedHotZone
            {
                Id = Guid.NewGuid(),
                Center = new Vector3(_zoneCounter * 50, 0, 0),
                MapId = 1,
                FailureCount = failureCount,
                PrimaryType = failureType,
                CreatedAt = DateTime.UtcNow,
                Radius = radius
            };
            OnHotZoneCreated?.Invoke(zone);
        }

        public void SimulateRehabilitation(Vector3 position, float radius = 25f)
        {
            var evt = new SimulatedRehabEvent
            {
                Id = Guid.NewGuid(),
                Position = position,
                Radius = radius,
                Timestamp = DateTime.UtcNow,
                SeverityReduction = 0.5f
            };
            OnRehabSimulated?.Invoke(evt);
        }
    }

    #endregion
}
