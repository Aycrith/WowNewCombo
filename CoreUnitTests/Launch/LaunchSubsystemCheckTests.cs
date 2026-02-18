using System;

using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchSubsystemCheckTests
{
    [Fact]
    public void LaunchSubsystemCheck_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        // Act
        var check = new LaunchSubsystemCheck(
            LaunchSubsystem.Addons,
            LaunchStatus.Ok,
            "Addon Validation",
            "All addons validated successfully",
            true,
            false,
            timestamp);

        // Assert
        check.Subsystem.Should().Be(LaunchSubsystem.Addons);
        check.Status.Should().Be(LaunchStatus.Ok);
        check.Title.Should().Be("Addon Validation");
        check.Message.Should().Be("All addons validated successfully");
        check.IsRequired.Should().BeTrue();
        check.IsBlocking.Should().BeFalse();
        check.TimestampUtc.Should().Be(timestamp);
        check.FixHint.Should().BeNull();
        check.NavigateTo.Should().BeNull();
        check.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void LaunchSubsystemCheck_CreateWithOptionalValues_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        // Act
        var check = new LaunchSubsystemCheck(
            LaunchSubsystem.Frames,
            LaunchStatus.Warning,
            "Frame Config",
            "Frame configuration incomplete",
            true,
            true,
            timestamp,
            "Run auto-configuration",
            "/configuration",
            true);

        // Assert
        check.Status.Should().Be(LaunchStatus.Warning);
        check.FixHint.Should().Be("Run auto-configuration");
        check.NavigateTo.Should().Be("/configuration");
        check.IsOverridden.Should().BeTrue();
    }

    [Fact]
    public void LaunchSubsystemCheck_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new LaunchSubsystemCheck(
            LaunchSubsystem.Addons,
            LaunchStatus.Pending,
            "Title",
            "Message",
            true,
            false,
            DateTimeOffset.UtcNow);

        // Act
        var modified = original with { Status = LaunchStatus.Ok };

        // Assert
        modified.Status.Should().Be(LaunchStatus.Ok);
        modified.Subsystem.Should().Be(original.Subsystem);
        modified.Title.Should().Be(original.Title);
    }

    [Fact]
    public void LaunchSubsystemCheck_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var check1 = new LaunchSubsystemCheck(
            LaunchSubsystem.WoWProcess,
            LaunchStatus.Ok,
            "WoW",
            "Running",
            true,
            false,
            timestamp);
        var check2 = new LaunchSubsystemCheck(
            LaunchSubsystem.WoWProcess,
            LaunchStatus.Ok,
            "WoW",
            "Running",
            true,
            false,
            timestamp);

        // Assert
        check1.Should().Be(check2);
        (check1 == check2).Should().BeTrue();
        check1.GetHashCode().Should().Be(check2.GetHashCode());
    }

    [Fact]
    public void LaunchSubsystemCheck_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var check1 = new LaunchSubsystemCheck(
            LaunchSubsystem.WoWProcess,
            LaunchStatus.Ok,
            "WoW",
            "Running",
            true,
            false,
            timestamp);
        var check2 = new LaunchSubsystemCheck(
            LaunchSubsystem.WoWProcess,
            LaunchStatus.Error,
            "WoW",
            "Running",
            true,
            false,
            timestamp);

        // Assert
        check1.Should().NotBe(check2);
        (check1 != check2).Should().BeTrue();
    }

    [Fact]
    public void LaunchSubsystemCheck_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var check = new LaunchSubsystemCheck(
            LaunchSubsystem.Navigation,
            LaunchStatus.Warning,
            "Pathing",
            "API slow",
            false,
            false,
            timestamp,
            "Check connection",
            "/pathing",
            false);

        // Act
        var (subsystem, status, title, message, isRequired, isBlocking, ts, fixHint, navigateTo, isOverridden) = check;

        // Assert
        subsystem.Should().Be(LaunchSubsystem.Navigation);
        status.Should().Be(LaunchStatus.Warning);
        title.Should().Be("Pathing");
        message.Should().Be("API slow");
        isRequired.Should().BeFalse();
        isBlocking.Should().BeFalse();
        ts.Should().Be(timestamp);
        fixHint.Should().Be("Check connection");
        navigateTo.Should().Be("/pathing");
        isOverridden.Should().BeFalse();
    }

    [Theory]
    [InlineData(LaunchStatus.Unknown)]
    [InlineData(LaunchStatus.Pending)]
    [InlineData(LaunchStatus.Ok)]
    [InlineData(LaunchStatus.Warning)]
    [InlineData(LaunchStatus.Error)]
    [InlineData(LaunchStatus.Skipped)]
    public void LaunchSubsystemCheck_AllStatusValues_Accepted(LaunchStatus status)
    {
        // Arrange & Act
        var check = new LaunchSubsystemCheck(
            LaunchSubsystem.Addons, status, "Test", "Msg", true, false, DateTimeOffset.UtcNow);

        // Assert
        check.Status.Should().Be(status);
    }

    [Fact]
    public void LaunchSubsystemCheck_ToString_ContainsTitle()
    {
        // Arrange
        var check = new LaunchSubsystemCheck(
            LaunchSubsystem.KeyBindings,
            LaunchStatus.Ok,
            "Key Bindings Check",
            "All bound",
            true,
            false,
            DateTimeOffset.UtcNow);

        // Act
        string result = check.ToString();

        // Assert
        result.Should().Contain("Key Bindings Check");
    }
}
