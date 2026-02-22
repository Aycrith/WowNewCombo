using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.GOAP;
using Core.LLM;
using Core.Resilience;

using CoreUnitTests.TestHelpers;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace CoreUnitTests.LLM;

public sealed class HybridLLMDecisionServiceTests : IDisposable
{
    private readonly FeatureFlagsOptions _enabledFlags;
    private readonly FeatureFlagsOptions _disabledFlags;
    private readonly FakeLLMClient _llmClient;

    public HybridLLMDecisionServiceTests()
    {
        _enabledFlags = new FeatureFlagsOptions
        {
            HybridLLMDecision = new HybridLLMDecisionOptions
            {
                Enabled = true,
                ConfidenceThreshold = 0.6f,
                MaxLatencyMs = 2000,
                CacheDecisionsSeconds = 5
            },
            CircuitBreaker = new CircuitBreakerOptions
            {
                LLMThreshold = 3,
                LLMCooldownSeconds = 120
            }
        };

        _disabledFlags = new FeatureFlagsOptions
        {
            HybridLLMDecision = new HybridLLMDecisionOptions
            {
                Enabled = false
            },
            CircuitBreaker = new CircuitBreakerOptions
            {
                LLMThreshold = 3,
                LLMCooldownSeconds = 120
            }
        };

        _llmClient = new FakeLLMClient();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabledByFeatureFlag_ReturnsImmediately()
    {
        using HybridLLMDecisionService service = CreateService(_disabledFlags);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        Task executeTask = StartService(service, cts.Token);

        // The service should complete immediately when disabled, not wait for cancellation
        await Task.WhenAny(executeTask, Task.Delay(500));
        executeTask.IsCompleted.Should().BeTrue("service should return immediately when disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenGoapAgentIsNull_ReturnsImmediately()
    {
        using HybridLLMDecisionService service = CreateService(_enabledFlags, goapAgent: null);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        Task executeTask = StartService(service, cts.Token);

        await Task.WhenAny(executeTask, Task.Delay(500));
        executeTask.IsCompleted.Should().BeTrue("service should return immediately when goapAgent is null");
    }

    // Note: OnGoapEvent tests have been moved to HybridLlmEventListenerTests
    // since event handling is now the responsibility of HybridLlmEventListener

    // Cache testing has been moved to HybridLlmDecisionEngineTests
    // since the cache is now managed by the engine, not the service

    [Fact]
    public void Constructor_InitializesCircuitBreaker()
    {
        // Verifies that the service can be constructed without throwing
        using HybridLLMDecisionService service = CreateService(_enabledFlags);

        // If we get here, the circuit breaker was initialized successfully
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_AcceptsNullGoapAgent()
    {
        // GoapAgent is optional - verify no exception on construction
        using HybridLLMDecisionService service = CreateService(_enabledFlags, goapAgent: null);
        service.Should().NotBeNull();
    }

    private HybridLLMDecisionService CreateService(
        FeatureFlagsOptions flags,
        GoapAgent? goapAgent = null)
    {
        IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(flags);

        // Create a real CircuitBreaker so the engine constructor guard is satisfied
        CircuitBreaker<LLMDecision> circuitBreaker = new(
            NullLogger.Instance,
            serviceName: "LLM",
            failureThreshold: flags.CircuitBreaker.LLMThreshold,
            cooldownPeriod: TimeSpan.FromSeconds(flags.CircuitBreaker.LLMCooldownSeconds),
            fallback: () => new LLMDecision("NoAction", "Circuit open", 0.0f));

        // Create the engine (which now contains the business logic)
        HybridLlmDecisionEngine engine = new(
            NullLogger<HybridLlmDecisionEngine>.Instance,
            _llmClient,
            monitor,
            circuitBreaker: circuitBreaker,
            goapAgent: goapAgent);

        return new HybridLLMDecisionService(
            NullLogger<HybridLLMDecisionService>.Instance,
            engine,
            monitor);
    }

    /// <summary>
    /// Invokes the protected ExecuteAsync method via the BackgroundService StartAsync path.
    /// </summary>
    private static async Task StartService(HybridLLMDecisionService service, CancellationToken ct)
    {
        try
        {
            await service.StartAsync(ct);

            // For BackgroundService, StartAsync starts ExecuteAsync in the background.
            // We need to await ExecuteTask to observe completion.
            PropertyInfo? executeTaskProp = typeof(Microsoft.Extensions.Hosting.BackgroundService)
                .GetProperty("ExecuteTask", BindingFlags.NonPublic | BindingFlags.Instance);

            if (executeTaskProp?.GetValue(service) is Task executeTask)
            {
                await executeTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    public void Dispose()
    {
        // Cleanup handled by individual test using statements
    }

    private sealed class FakeLLMClient : ILLMClient
    {
        private int _queryCount;

        public int QueryCount => _queryCount;
        public bool IsAvailable => true;

        public Task<LLMDecision> QueryAsync(string context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _queryCount);
            return Task.FromResult(new LLMDecision("TestAction", "TestReasoning", 0.9f));
        }
    }
}
