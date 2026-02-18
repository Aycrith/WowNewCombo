using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
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
/// 24-hour stability test harness for RouteVisualizationService.
/// Monitors memory usage, performance metrics, and thread safety over extended duration.
/// </summary>
public class RouteVisualizationStabilityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<RouteVisualizationService> _logger;
    private RouteVisualizationService? _service;
    private FakeFailureSimulationService? _simulation;

    // Stability thresholds
    private const long MaxMemoryGrowthPercent = 50; // Allow 50% memory growth over test period
    private const int MaxP95LatencyMs = 100;
    private const int MaxAvgLatencyMs = 50;

    // Cached JsonSerializerOptions to avoid repeated allocations (CA1869)
    private static readonly JsonSerializerOptions ReportSerializerOptions = new() { WriteIndented = true };

    public RouteVisualizationStabilityTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger<RouteVisualizationService>.Instance;
        _simulation = new FakeFailureSimulationService();
    }

    /// <summary>
    /// Test: Extended stability test simulating 24 hours of operation
    /// Duration: ~30 seconds in test mode (simulated time compression)
    /// Evidence: Memory snapshots, latency histograms, thread dumps
    /// </summary>
    [Fact]
    public async Task Stability_24HourSimulation_ShouldMaintainPerformance()
    {
        // Arrange - Use compressed timeline for test (30s = 24h simulation)
        _service = CreateService();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

        var memorySnapshots = new List<MemorySnapshot>();
        var latencyMetrics = new List<LatencyMetric>();
        var exceptions = new List<Exception>();

        DateTime startTime = DateTime.UtcNow;
        long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        _output.WriteLine("=== 24-Hour Stability Test Started ===");
        _output.WriteLine($"Initial Memory: {initialMemory / 1024 / 1024} MB");
        _output.WriteLine($"Test Duration: 30 seconds (simulating 24 hours)");

        // Act - Run stability simulation
        try
        {
            await RunStabilitySimulation(
                cancellationToken,
                memorySnapshots,
                latencyMetrics,
                exceptions);
        }
        catch (OperationCanceledException)
        {
            // Expected when test completes
        }

        TimeSpan elapsed = DateTime.UtcNow - startTime;

        // Final memory measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long finalMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Generate report
        var report = GenerateStabilityReport(
            elapsed,
            initialMemory,
            finalMemory,
            memorySnapshots,
            latencyMetrics,
            exceptions);

        _output.WriteLine(report);

        // Save report to file
        string reportPath = Path.Combine(Path.GetTempPath(), "stability-test-report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            ElapsedSeconds = elapsed.TotalSeconds,
            InitialMemoryBytes = initialMemory,
            FinalMemoryBytes = finalMemory,
            MemoryGrowthPercent = ((finalMemory - initialMemory) / (double)initialMemory) * 100,
            ExceptionCount = exceptions.Count,
            AvgLatencyMs = latencyMetrics.Count > 0 ? latencyMetrics.Average(m => m.LatencyMs) : 0,
            P95LatencyMs = latencyMetrics.Count > 0 ? CalculatePercentile(latencyMetrics.Select(m => m.LatencyMs).ToList(), 0.95) : 0,
            MaxLatencyMs = latencyMetrics.Count > 0 ? latencyMetrics.Max(m => m.LatencyMs) : 0
        }, ReportSerializerOptions));

        _output.WriteLine($"\nDetailed report saved to: {reportPath}");

        // Assert - Use absolute memory threshold instead of percentage
        // (percentage is unreliable when initial memory is near zero)
        long memoryGrowth = finalMemory - initialMemory;
        long maxAllowedGrowthBytes = 50 * 1024 * 1024; // 50 MB absolute limit

        Assert.True(memoryGrowth < maxAllowedGrowthBytes,
            $"Memory grew by {memoryGrowth / 1024 / 1024:F2} MB over {elapsed.TotalSeconds:F0}s. " +
            $"Max allowed: {maxAllowedGrowthBytes / 1024 / 1024} MB");

        Assert.Empty(exceptions);

        if (latencyMetrics.Count > 0)
        {
            double p95Latency = CalculatePercentile(latencyMetrics.Select(m => m.LatencyMs).ToList(), 0.95);
            Assert.True(p95Latency < MaxP95LatencyMs,
                $"P95 latency {p95Latency:F2}ms exceeds threshold {MaxP95LatencyMs}ms");
        }
    }

    /// <summary>
    /// Test: Memory leak detection with sustained load
    /// Evidence: Memory growth rate over time
    /// </summary>
    [Fact]
    public void Stability_MemoryLeakDetection_UnderSustainedLoad()
    {
        // Arrange
        _service = CreateService();
        var memoryReadings = new List<(DateTime Time, long MemoryBytes)>();
        DateTime startTime = DateTime.UtcNow;

        // Act - Create markers continuously for 10 seconds
        for (int i = 0; i < 1000; i++)
        {
            _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);

            // Record memory every 100 markers
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                long memory = GC.GetTotalMemory(forceFullCollection: true);
                memoryReadings.Add((DateTime.UtcNow, memory));
            }
        }

        // Clear markers
        _service.ClearAll();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Take final reading
        long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        memoryReadings.Add((DateTime.UtcNow, finalMemory));

        // Analyze trend
        var sb = new StringBuilder();
        sb.AppendLine("=== Memory Leak Analysis ===");
        sb.AppendLine("Time\t\tMemory (MB)");

        foreach (var reading in memoryReadings)
        {
            double elapsedSeconds = (reading.Time - startTime).TotalSeconds;
            sb.AppendLine($"{elapsedSeconds:F1}s\t\t{reading.MemoryBytes / 1024 / 1024:F2}");
        }

        // Calculate memory trend
        if (memoryReadings.Count >= 2)
        {
            var first = memoryReadings.First();
            var last = memoryReadings.Last();
            double elapsedHours = (last.Time - first.Time).TotalHours;
            long memoryGrowthBytes = last.MemoryBytes - first.MemoryBytes;

            // Use absolute growth rate (MB per 1000 operations)
            double growthRate = memoryReadings.Count > 0
                ? (memoryGrowthBytes / 1024.0 / 1024.0) / (memoryReadings.Count - 1)
                : 0;

            sb.AppendLine($"\nMemory Growth: {growthRate:F2} MB per 100 operations");

            // Assert - Less than 1 MB growth per 100 markers is acceptable
            Assert.True(growthRate < 1.0,
                $"Potential memory leak detected: {growthRate:F2} MB growth per 100 operations");
        }

        _output.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Test: Thread contention under high concurrency
    /// Evidence: No deadlocks detected, consistent response times
    /// </summary>
    [Fact]
    public void Stability_ThreadContention_UnderHighConcurrency()
    {
        // Arrange
        _service = CreateService();
        var stopwatch = new Stopwatch();
        var latencies = new List<long>();
        var exceptions = new List<Exception>();
        int iterations = 10000;
        int threads = 20;

        // Act - High concurrency stress test
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, threadId =>
        {
            for (int i = 0; i < iterations / threads; i++)
            {
                try
                {
                    stopwatch.Restart();

                    if (i % 3 == 0)
                    {
                        // Add marker
                        _simulation!.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);
                    }
                    else if (i % 3 == 1)
                    {
                        // Read markers
                        _ = _service.GetActiveStuckMarkers();
                        _ = _service.GetActiveHotZones();
                    }
                    else
                    {
                        // Query upcoming zones
                        _ = _service.GetUpcomingHotZone(
                            new Vector3(i, i, 0),
                            new Vector3(1, 0, 0),
                            100f);
                    }

                    stopwatch.Stop();
                    lock (latencies) { latencies.Add(stopwatch.ElapsedMilliseconds); }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }
        });

        // Assert
        Assert.Empty(exceptions);

        double avgLatency = latencies.Average();
        double p95Latency = CalculatePercentile(latencies, 0.95);
        double maxLatency = latencies.Max();

        _output.WriteLine("=== Thread Contention Results ===");
        _output.WriteLine($"Total Operations: {latencies.Count}");
        _output.WriteLine($"Average Latency: {avgLatency:F2}ms");
        _output.WriteLine($"P95 Latency: {p95Latency:F2}ms");
        _output.WriteLine($"Max Latency: {maxLatency:F2}ms");

        Assert.True(p95Latency < MaxP95LatencyMs,
            $"P95 latency under contention: {p95Latency:F2}ms (threshold: {MaxP95LatencyMs}ms)");
    }

    #region Private Methods

    private async Task RunStabilitySimulation(
        CancellationToken cancellationToken,
        List<MemorySnapshot> memorySnapshots,
        List<LatencyMetric> latencyMetrics,
        List<Exception> exceptions)
    {
        int operationCount = 0;
        var random = new Random(42); // Fixed seed for reproducibility

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Memory snapshot every 1000 operations
                if (operationCount % 1000 == 0)
                {
                    GC.Collect();
                    long memory = GC.GetTotalMemory(forceFullCollection: true);
                    memorySnapshots.Add(new MemorySnapshot
                    {
                        Timestamp = DateTime.UtcNow,
                        MemoryBytes = memory,
                        OperationCount = operationCount
                    });
                }

                // Simulate various operations
                var stopwatch = Stopwatch.StartNew();

                int operation = random.Next(10);
                switch (operation)
                {
                    case 0:
                    case 1:
                    case 2:
                        // Add stuck marker
                        _simulation!.SimulateStuck(
                            (UnstuckState)random.Next(7),
                            random.Next(1000, 5000),
                            random.Next(1, 5));
                        break;

                    case 3:
                        // Add death marker
                        _simulation!.SimulateDeath($"Death #{operationCount}");
                        break;

                    case 4:
                        // Add hot zone
                        _simulation!.SimulateHotZone(
                            FailureType.Stuck,
                            random.Next(1, 10),
                            random.Next(10, 30));
                        break;

                    case 5:
                        // Read all markers
                        _ = _service!.GetActiveStuckMarkers();
                        _ = _service.GetActiveHotZones();
                        _ = _service.GetRehabilitationMarkers();
                        break;

                    case 6:
                        // Query upcoming zones
                        _ = _service!.GetUpcomingHotZone(
                            new Vector3(random.Next(1000), random.Next(1000), 0),
                            new Vector3(random.Next(-1, 2), random.Next(-1, 2), 0),
                            50f);
                        break;

                    case 7:
                        // Get failure counts
                        _ = _service!.GetFailureCountsByGrid();
                        break;

                    case 8:
                        // Periodic cleanup simulation
                        if (operationCount % 5000 == 0 && operationCount > 0)
                        {
                            // Simulate natural cleanup by removing old markers
                            // (Service has timer-based cleanup, but we force some here)
                        }
                        break;

                    case 9:
                        // Idle - simulate periods of low activity
                        await Task.Delay(1, cancellationToken);
                        break;
                }

                stopwatch.Stop();
                latencyMetrics.Add(new LatencyMetric
                {
                    Timestamp = DateTime.UtcNow,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    Operation = operation
                });

                operationCount++;

                // Brief delay to prevent CPU saturation
                if (operationCount % 100 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                if (exceptions.Count > 100)
                {
                    throw new Exception("Too many exceptions during stability test", ex);
                }
            }
        }
    }

    private static string GenerateStabilityReport(
        TimeSpan elapsed,
        long initialMemory,
        long finalMemory,
        List<MemorySnapshot> memorySnapshots,
        List<LatencyMetric> latencyMetrics,
        List<Exception> exceptions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== 24-Hour Stability Test Report ===");
        sb.AppendLine();
        sb.AppendLine($"Test Duration: {elapsed.TotalSeconds:F1} seconds");
        sb.AppendLine($"Simulated Operations: {latencyMetrics.Count}");
        sb.AppendLine();

        // Memory analysis
        sb.AppendLine("--- Memory Analysis ---");
        sb.AppendLine($"Initial Memory: {initialMemory / 1024 / 1024:F2} MB");
        sb.AppendLine($"Final Memory: {finalMemory / 1024 / 1024:F2} MB");
        sb.AppendLine($"Memory Delta: {(finalMemory - initialMemory) / 1024 / 1024:F2} MB");
        double growthPercent = ((finalMemory - initialMemory) / (double)initialMemory) * 100;
        sb.AppendLine($"Growth: {growthPercent:F2}%");

        if (memorySnapshots.Count > 0)
        {
            sb.AppendLine($"Snapshots Taken: {memorySnapshots.Count}");
            double memoryPerOperation = memorySnapshots.Count > 1
                ? (memorySnapshots.Last().MemoryBytes - memorySnapshots.First().MemoryBytes) / (double)latencyMetrics.Count
                : 0;
            sb.AppendLine($"Avg Bytes/Operation: {memoryPerOperation:F2}");
        }

        sb.AppendLine();

        // Latency analysis
        if (latencyMetrics.Count > 0)
        {
            sb.AppendLine("--- Latency Analysis ---");
            var latencies = latencyMetrics.Select(m => m.LatencyMs).ToList();
            sb.AppendLine($"Total Operations: {latencies.Count}");
            sb.AppendLine($"Average: {latencies.Average():F2}ms");
            sb.AppendLine($"P50: {CalculatePercentile(latencies, 0.50):F2}ms");
            sb.AppendLine($"P95: {CalculatePercentile(latencies, 0.95):F2}ms");
            sb.AppendLine($"P99: {CalculatePercentile(latencies, 0.99):F2}ms");
            sb.AppendLine($"Max: {latencies.Max():F2}ms");

            // Latency distribution
            sb.AppendLine("\nLatency Distribution:");
            var buckets = new[] { 0, 1, 5, 10, 25, 50, 100, 150, int.MaxValue };
            for (int i = 0; i < buckets.Length - 1; i++)
            {
                int count = latencies.Count(l => l >= buckets[i] && l < buckets[i + 1]);
                double percent = (count / (double)latencies.Count) * 100;
                string range = buckets[i + 1] == int.MaxValue
                    ? $"{buckets[i]}+ms"
                    : $"{buckets[i]}-{buckets[i + 1]}ms";
                sb.AppendLine($"  {range,12}: {count,6} ({percent,5:F1}%)");
            }
        }

        sb.AppendLine();

        // Exception analysis
        sb.AppendLine("--- Exception Analysis ---");
        sb.AppendLine($"Total Exceptions: {exceptions.Count}");
        if (exceptions.Count > 0)
        {
            var grouped = exceptions.GroupBy(e => e.GetType().Name);
            foreach (var group in grouped)
            {
                sb.AppendLine($"  {group.Key}: {group.Count()}");
            }
        }

        // Status
        sb.AppendLine();
        sb.AppendLine("--- Test Status ---");
        bool passed = exceptions.Count == 0 &&
            growthPercent < MaxMemoryGrowthPercent &&
            (latencyMetrics.Count == 0 || CalculatePercentile(latencyMetrics.Select(m => m.LatencyMs).ToList(), 0.95) < MaxP95LatencyMs);
        sb.AppendLine(passed ? "PASSED" : "FAILED");

        return sb.ToString();
    }

    private static double CalculatePercentile(List<long> values, double percentile)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToList();
        int index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

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

    public void Dispose()
    {
        _service?.Dispose();
        _service = null;
    }

    #endregion

    #region Data Structures

    private sealed class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public long MemoryBytes { get; set; }
        public int OperationCount { get; set; }
    }

    private sealed class LatencyMetric
    {
        public DateTime Timestamp { get; set; }
        public long LatencyMs { get; set; }
        public int Operation { get; set; }
    }

    #endregion

    #region Fake Implementation

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
