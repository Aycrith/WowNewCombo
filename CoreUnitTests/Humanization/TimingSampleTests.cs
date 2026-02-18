using Core.Humanization;
using FluentAssertions;
using System;
using Xunit;

namespace CoreUnitTests.Humanization;

/// <summary>
/// Tests for the TimingSample record.
/// </summary>
public class TimingSampleTests
{
    #region Construction Tests

    [Fact]
    public void TimingSample_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 15, 10, 30, 0);

        // Act
        var sample = new TimingSample(timestamp, 100, "ActionContext");

        // Assert
        sample.Timestamp.Should().Be(timestamp);
        sample.DurationMs.Should().Be(100);
        sample.Context.Should().Be("ActionContext");
    }

    [Fact]
    public void TimingSample_NullContext_Allowed()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var sample = new TimingSample(timestamp, 50, null);

        // Assert
        sample.Context.Should().BeNull();
    }

    [Fact]
    public void TimingSample_ZeroDuration_Allowed()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var sample = new TimingSample(timestamp, 0, "Instant");

        // Assert
        sample.DurationMs.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void TimingSample_VariousDurations_Accepted(int duration)
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var sample = new TimingSample(timestamp, duration, "Test");

        // Assert
        sample.DurationMs.Should().Be(duration);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void TimingSample_Equality_SameValues_AreEqual()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0);
        var sample1 = new TimingSample(timestamp, 100, "Context");
        var sample2 = new TimingSample(timestamp, 100, "Context");

        // Assert
        sample1.Should().Be(sample2);
        sample1.GetHashCode().Should().Be(sample2.GetHashCode());
    }

    [Fact]
    public void TimingSample_Equality_DifferentTimestamps_AreNotEqual()
    {
        // Arrange
        var sample1 = new TimingSample(new DateTime(2026, 1, 1), 100, "Context");
        var sample2 = new TimingSample(new DateTime(2026, 1, 2), 100, "Context");

        // Assert
        sample1.Should().NotBe(sample2);
    }

    [Fact]
    public void TimingSample_Equality_DifferentDurations_AreNotEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var sample1 = new TimingSample(timestamp, 100, "Context");
        var sample2 = new TimingSample(timestamp, 200, "Context");

        // Assert
        sample1.Should().NotBe(sample2);
    }

    [Fact]
    public void TimingSample_Equality_DifferentContexts_AreNotEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var sample1 = new TimingSample(timestamp, 100, "Context1");
        var sample2 = new TimingSample(timestamp, 100, "Context2");

        // Assert
        sample1.Should().NotBe(sample2);
    }

    #endregion

    #region Deconstruction Tests

    [Fact]
    public void TimingSample_Deconstruct_ExtractsAllValues()
    {
        // Arrange
        var timestamp = new DateTime(2026, 6, 15, 14, 30, 0);
        var sample = new TimingSample(timestamp, 250, "TestContext");

        // Act
        var (ts, duration, context) = sample;

        // Assert
        ts.Should().Be(timestamp);
        duration.Should().Be(250);
        context.Should().Be("TestContext");
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void TimingSample_With_CreatesNewInstance()
    {
        // Arrange
        var original = new TimingSample(DateTime.UtcNow, 100, "Original");

        // Act
        var modified = original with { DurationMs = 200 };

        // Assert
        modified.Should().NotBe(original);
        modified.DurationMs.Should().Be(200);
        original.DurationMs.Should().Be(100);
    }

    [Fact]
    public void TimingSample_With_PreservesOtherValues()
    {
        // Arrange
        var timestamp = new DateTime(2026, 1, 1);
        var original = new TimingSample(timestamp, 100, "Context");

        // Act
        var modified = original with { Context = "NewContext" };

        // Assert
        modified.Timestamp.Should().Be(original.Timestamp);
        modified.DurationMs.Should().Be(original.DurationMs);
        modified.Context.Should().Be("NewContext");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TimingSample_NegativeDuration_Allowed()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var sample = new TimingSample(timestamp, -50, "Negative");

        // Assert
        sample.DurationMs.Should().Be(-50);
    }

    [Fact]
    public void TimingSample_LargeDuration_Accepted()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var sample = new TimingSample(timestamp, int.MaxValue, "Large");

        // Assert
        sample.DurationMs.Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData("KeyPress")]
    [InlineData("MouseClick")]
    [InlineData("SpellCast")]
    [InlineData("Movement")]
    [InlineData("")]
    public void TimingSample_VariousContexts_Accepted(string context)
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var sample = new TimingSample(timestamp, 100, context);

        // Assert
        sample.Context.Should().Be(context);
    }

    #endregion
}
