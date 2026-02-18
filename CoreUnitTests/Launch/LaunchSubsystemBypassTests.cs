using System;

using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchSubsystemBypassTests
{
    [Fact]
    public void LaunchSubsystemBypass_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        const bool enabled = true;
        const string reason = "Manual override for testing";
        const string source = "UserInterface";

        // Act
        var bypass = new LaunchSubsystemBypass(enabled, reason, timestamp, source);

        // Assert
        bypass.Enabled.Should().Be(enabled);
        bypass.Reason.Should().Be(reason);
        bypass.TimestampUtc.Should().Be(timestamp);
        bypass.Source.Should().Be(source);
    }

    [Fact]
    public void LaunchSubsystemBypass_CreateDisabled_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        // Act
        var bypass = new LaunchSubsystemBypass(false, "Not needed", timestamp, "System");

        // Assert
        bypass.Enabled.Should().BeFalse();
    }

    [Fact]
    public void LaunchSubsystemBypass_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new LaunchSubsystemBypass(
            true, "Original reason", DateTimeOffset.UtcNow, "Source");

        // Act
        var modified = original with { Enabled = false };

        // Assert
        modified.Enabled.Should().BeFalse();
        modified.Reason.Should().Be(original.Reason);
        modified.TimestampUtc.Should().Be(original.TimestampUtc);
    }

    [Fact]
    public void LaunchSubsystemBypass_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var bypass1 = new LaunchSubsystemBypass(true, "Test", timestamp, "UI");
        var bypass2 = new LaunchSubsystemBypass(true, "Test", timestamp, "UI");

        // Assert
        bypass1.Should().Be(bypass2);
        (bypass1 == bypass2).Should().BeTrue();
        bypass1.GetHashCode().Should().Be(bypass2.GetHashCode());
    }

    [Fact]
    public void LaunchSubsystemBypass_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var bypass1 = new LaunchSubsystemBypass(true, "Reason1", timestamp, "UI");
        var bypass2 = new LaunchSubsystemBypass(true, "Reason2", timestamp, "UI");

        // Assert
        bypass1.Should().NotBe(bypass2);
        (bypass1 != bypass2).Should().BeTrue();
    }

    [Fact]
    public void LaunchSubsystemBypass_Equality_DifferentTimestamps_AreNotEqual()
    {
        // Arrange
        var bypass1 = new LaunchSubsystemBypass(true, "Test", DateTimeOffset.UtcNow, "UI");
        var bypass2 = new LaunchSubsystemBypass(true, "Test", DateTimeOffset.UtcNow.AddMinutes(1), "UI");

        // Assert
        bypass1.Should().NotBe(bypass2);
    }

    [Fact]
    public void LaunchSubsystemBypass_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var bypass = new LaunchSubsystemBypass(false, "Bypass reason", timestamp, "System");

        // Act
        var (enabled, reason, ts, source) = bypass;

        // Assert
        enabled.Should().BeFalse();
        reason.Should().Be("Bypass reason");
        ts.Should().Be(timestamp);
        source.Should().Be("System");
    }

    [Fact]
    public void LaunchSubsystemBypass_ToString_ContainsEnabledStatus()
    {
        // Arrange
        var bypass = new LaunchSubsystemBypass(true, "Test", DateTimeOffset.UtcNow, "Source");

        // Act
        string result = bypass.ToString();

        // Assert
        result.Should().Contain("True");
    }

    [Fact]
    public void LaunchSubsystemBypass_WithLongReason_StoresCorrectly()
    {
        // Arrange
        string longReason = "This is a very long reason that explains why the bypass was enabled " +
                           "and what circumstances led to this decision being made by the user.";

        // Act
        var bypass = new LaunchSubsystemBypass(true, longReason, DateTimeOffset.UtcNow, "DetailedSource");

        // Assert
        bypass.Reason.Should().Be(longReason);
    }
}
