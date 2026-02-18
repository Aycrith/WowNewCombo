// Copyright (c) 2025 WowClassicGrindBot Contributors
// Licensed under the MIT License

using System;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MockWoWClient.GameState;

using Xunit;

namespace CoreUnitTests.Integration;

/// <summary>
/// Base class for all integration tests using MockWoWClient.
/// Provides standardized setup, cleanup, and assertion helpers.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected SimulationClock Clock { get; }
    protected GameStateManager GameState { get; }
    protected FailureSimulationService FailureSimulation { get; }
    protected ILoggerFactory LoggerFactory { get; }

    protected IntegrationTestBase()
    {
        Clock = new SimulationClock();
        GameState = new GameStateManager(Clock);
        FailureSimulation = new FailureSimulationService(GameState, Clock);
        LoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
    }

    public virtual void Dispose()
    {
        CleanupTestData();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to clean up test-specific data.
    /// </summary>
    protected virtual void CleanupTestData()
    {
    }

    #region Event Capture Helpers

    /// <summary>
    /// Captures an event of type T from the specified event source.
    /// </summary>
    protected static T CaptureEvent<T>(Action<Action<T>> subscribeAction, Action triggerAction)
        where T : class
    {
        T? captured = null;
        var tcs = new TaskCompletionSource<T>();

        subscribeAction(evt =>
        {
            captured = evt;
            tcs.TrySetResult(evt);
        });

        triggerAction();

        // Wait up to 5 seconds for event
        if (!tcs.Task.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException($"Event of type {typeof(T).Name} was not raised within timeout");
        }

        return captured!;
    }

    /// <summary>
    /// Captures an async event with timeout.
    /// </summary>
    protected static async Task<T> CaptureEventAsync<T>(
        Func<Action<T>, IDisposable> subscribeAction,
        Func<Task> triggerAction,
        TimeSpan? timeout = null)
        where T : class
    {
        T? captured = null;
        var tcs = new TaskCompletionSource<T>();

        using var subscription = subscribeAction(evt => tcs.TrySetResult(evt));
        await triggerAction();

        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        if (await Task.WhenAny(tcs.Task, Task.Delay(actualTimeout)).ConfigureAwait(false) != tcs.Task)
        {
            throw new TimeoutException($"Event of type {typeof(T).Name} was not raised within {actualTimeout}");
        }

        return captured!;
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Asserts that an event was captured.
    /// </summary>
    protected static void AssertEventFired<T>(T? capturedEvent, string? message = null)
        where T : class
    {
        Assert.NotNull(capturedEvent);
        if (!string.IsNullOrEmpty(message))
        {
            Assert.True(true, message);
        }
    }

    /// <summary>
    /// Asserts player is at expected position.
    /// </summary>
    protected void AssertPlayerAt(Vector3 expected, float tolerance = 0.1f)
    {
        var actual = GameState.Player.Position;
        var distance = Vector3.Distance(actual, expected);
        Assert.True(
            distance <= tolerance,
            $"Expected player at {expected}, but was at {actual} (distance: {distance:F2})");
    }

    /// <summary>
    /// Asserts player combat state.
    /// </summary>
    protected void AssertCombatState(bool expectedInCombat)
    {
        Assert.Equal(expectedInCombat, GameState.InCombat);
    }

    #endregion
}
