using System;

using Core.Resilience;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Resilience;

public class CircuitBreakerStatsTests
{
    [Fact]
    public void CircuitBreakerStats_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        const CircuitState state = CircuitState.Closed;
        const int failureCount = 3;
        const int failureThreshold = 5;
        const long totalRequests = 1000;
        const long successfulRequests = 950;
        const long failedRequests = 50;
        const long rejectedRequests = 0;
        DateTime lastFailure = DateTime.UtcNow.AddMinutes(-5);
        TimeSpan cooldownRemaining = TimeSpan.FromSeconds(30);

        // Act
        var stats = new CircuitBreakerStats(state, failureCount, failureThreshold, totalRequests, successfulRequests, failedRequests, rejectedRequests, lastFailure, cooldownRemaining);

        // Assert
        stats.State.Should().Be(state);
        stats.FailureCount.Should().Be(failureCount);
        stats.FailureThreshold.Should().Be(failureThreshold);
        stats.TotalRequests.Should().Be(totalRequests);
        stats.SuccessfulRequests.Should().Be(successfulRequests);
        stats.FailedRequests.Should().Be(failedRequests);
        stats.RejectedRequests.Should().Be(rejectedRequests);
        stats.LastFailure.Should().Be(lastFailure);
        stats.CooldownRemaining.Should().Be(cooldownRemaining);
    }

    [Fact]
    public void CircuitBreakerStats_CreateOpenState_StoresCorrectly()
    {
        // Arrange & Act
        var stats = new CircuitBreakerStats(CircuitState.Open, 5, 5, 100, 90, 5, 10, DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Assert
        stats.State.Should().Be(CircuitState.Open);
        stats.FailureCount.Should().Be(5);
    }

    [Fact]
    public void CircuitBreakerStats_CreateHalfOpenState_StoresCorrectly()
    {
        // Arrange & Act
        var stats = new CircuitBreakerStats(CircuitState.HalfOpen, 2, 5, 200, 190, 2, 0, DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // Assert
        stats.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void CircuitBreakerStats_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new CircuitBreakerStats(CircuitState.Closed, 0, 5, 100, 100, 0, 0, DateTime.MinValue, TimeSpan.Zero);

        // Act
        var modified = original with { FailureCount = 3 };

        // Assert
        modified.FailureCount.Should().Be(3);
        modified.State.Should().Be(original.State);
        modified.FailureThreshold.Should().Be(original.FailureThreshold);
    }

    [Fact]
    public void CircuitBreakerStats_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTime lastFailure = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        TimeSpan cooldown = TimeSpan.FromSeconds(30);

        var stats1 = new CircuitBreakerStats(CircuitState.Closed, 2, 5, 1000, 980, 20, 0, lastFailure, cooldown);
        var stats2 = new CircuitBreakerStats(CircuitState.Closed, 2, 5, 1000, 980, 20, 0, lastFailure, cooldown);

        // Assert
        stats1.Should().Be(stats2);
        (stats1 == stats2).Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerStats_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var stats1 = new CircuitBreakerStats(CircuitState.Closed, 2, 5, 1000, 980, 20, 0, DateTime.UtcNow, TimeSpan.Zero);
        var stats2 = new CircuitBreakerStats(CircuitState.Closed, 3, 5, 1000, 980, 20, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Assert
        stats1.Should().NotBe(stats2);
        (stats1 != stats2).Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerStats_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        const CircuitState state = CircuitState.Open;
        const int failureCount = 5;
        const int failureThreshold = 5;
        const long totalRequests = 500;
        const long successfulRequests = 450;
        const long failedRequests = 50;
        const long rejectedRequests = 25;
        DateTime lastFailure = DateTime.UtcNow.AddMinutes(-1);
        TimeSpan cooldown = TimeSpan.FromMinutes(2);

        var stats = new CircuitBreakerStats(state, failureCount, failureThreshold, totalRequests, successfulRequests, failedRequests, rejectedRequests, lastFailure, cooldown);

        // Act
        var (resultState, resultFailureCount, resultFailureThreshold, resultTotalRequests, resultSuccessfulRequests, resultFailedRequests, resultRejectedRequests, resultLastFailure, resultCooldown) = stats;

        // Assert
        resultState.Should().Be(state);
        resultFailureCount.Should().Be(failureCount);
        resultFailureThreshold.Should().Be(failureThreshold);
        resultTotalRequests.Should().Be(totalRequests);
        resultSuccessfulRequests.Should().Be(successfulRequests);
        resultFailedRequests.Should().Be(failedRequests);
        resultRejectedRequests.Should().Be(rejectedRequests);
        resultLastFailure.Should().Be(lastFailure);
        resultCooldown.Should().Be(cooldown);
    }

    [Theory]
    [InlineData(CircuitState.Closed)]
    [InlineData(CircuitState.Open)]
    [InlineData(CircuitState.HalfOpen)]
    public void CircuitBreakerStats_AllStates_Accepted(CircuitState state)
    {
        // Arrange & Act
        var stats = new CircuitBreakerStats(state, 0, 5, 100, 100, 0, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Assert
        stats.State.Should().Be(state);
    }

    [Fact]
    public void CircuitBreakerStats_SuccessRate_CanBeCalculated()
    {
        // Arrange
        var stats = new CircuitBreakerStats(CircuitState.Closed, 0, 5, 1000, 950, 50, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Act
        double successRate = (double)stats.SuccessfulRequests / stats.TotalRequests;

        // Assert
        successRate.Should().Be(0.95);
    }

    [Fact]
    public void CircuitBreakerStats_ZeroCooldown_StoredCorrectly()
    {
        // Arrange & Act
        var stats = new CircuitBreakerStats(CircuitState.Closed, 0, 5, 100, 100, 0, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Assert
        stats.CooldownRemaining.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void CircuitBreakerStats_HistoricalLastFailure_StoredCorrectly()
    {
        // Arrange
        DateTime past = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var stats = new CircuitBreakerStats(CircuitState.Open, 5, 5, 100, 50, 50, 0, past, TimeSpan.FromMinutes(5));

        // Assert
        stats.LastFailure.Should().Be(past);
    }

    [Fact]
    public void CircuitBreakerStats_ToString_ContainsState()
    {
        // Arrange
        var stats = new CircuitBreakerStats(CircuitState.Open, 5, 5, 100, 50, 50, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Act
        string result = stats.ToString();

        // Assert
        result.Should().Contain("Open");
    }

    [Fact]
    public void CircuitBreakerStats_LargeCounts_StoresCorrectly()
    {
        // Arrange & Act
        var stats = new CircuitBreakerStats(CircuitState.Closed, int.MaxValue, int.MaxValue, long.MaxValue, long.MaxValue, 0, 0, DateTime.MaxValue, TimeSpan.MaxValue);

        // Assert
        stats.TotalRequests.Should().Be(long.MaxValue);
        stats.SuccessfulRequests.Should().Be(long.MaxValue);
    }

    [Fact]
    public void CircuitBreakerStats_IsAtThreshold_ReturnsExpected()
    {
        // Arrange
        var stats = new CircuitBreakerStats(CircuitState.Closed, 5, 5, 100, 95, 5, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Act & Assert
        stats.FailureCount.Should().Be(stats.FailureThreshold);
    }

    [Fact]
    public void CircuitBreakerStats_RejectedRequests_Tracking()
    {
        // Arrange & Act
        var stats = new CircuitBreakerStats(CircuitState.Open, 5, 5, 1000, 500, 5, 495, DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Assert
        stats.RejectedRequests.Should().Be(495);
    }
}
