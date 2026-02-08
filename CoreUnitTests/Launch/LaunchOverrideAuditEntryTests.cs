using System;

using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchOverrideAuditEntryTests
{
    [Fact]
    public void LaunchOverrideAuditEntry_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        LaunchSubsystem subsystem = LaunchSubsystem.Addons;
        const string action = "EnableOverride";
        const bool enabled = true;
        const string reason = "Manual bypass for testing";
        const string source = "UserInterface";

        // Act
        var entry = new LaunchOverrideAuditEntry(
            timestamp, subsystem, action, enabled, reason, source);

        // Assert
        entry.TimestampUtc.Should().Be(timestamp);
        entry.Subsystem.Should().Be(subsystem);
        entry.Action.Should().Be(action);
        entry.Enabled.Should().Be(enabled);
        entry.Reason.Should().Be(reason);
        entry.Source.Should().Be(source);
    }

    [Fact]
    public void LaunchOverrideAuditEntry_WithNullSubsystem_AcceptsNull()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        // Act
        var entry = new LaunchOverrideAuditEntry(
            timestamp, null, "Action", true, "Reason", "Source");

        // Assert
        entry.Subsystem.Should().BeNull();
    }

    [Fact]
    public void LaunchOverrideAuditEntry_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new LaunchOverrideAuditEntry(
            DateTimeOffset.UtcNow,
            LaunchSubsystem.Addons,
            "OriginalAction",
            false,
            "Original Reason",
            "Source");

        // Act
        var modified = original with { Action = "ModifiedAction" };

        // Assert
        modified.Action.Should().Be("ModifiedAction");
        modified.TimestampUtc.Should().Be(original.TimestampUtc);
        modified.Subsystem.Should().Be(original.Subsystem);
        modified.Enabled.Should().Be(original.Enabled);
    }

    [Fact]
    public void LaunchOverrideAuditEntry_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var entry1 = new LaunchOverrideAuditEntry(
            timestamp,             LaunchSubsystem.Frames, "Enable", true, "Test", "UI");
        var entry2 = new LaunchOverrideAuditEntry(
            timestamp,             LaunchSubsystem.Frames, "Enable", true, "Test", "UI");

        // Assert
        entry1.Should().Be(entry2);
        (entry1 == entry2).Should().BeTrue();
        entry1.GetHashCode().Should().Be(entry2.GetHashCode());
    }

    [Fact]
    public void LaunchOverrideAuditEntry_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var entry1 = new LaunchOverrideAuditEntry(
            timestamp,             LaunchSubsystem.Addons, "Enable", true, "Test", "UI");
        var entry2 = new LaunchOverrideAuditEntry(
            timestamp, LaunchSubsystem.Addons, "Disable", true, "Test", "UI");

        // Assert
        entry1.Should().NotBe(entry2);
        (entry1 != entry2).Should().BeTrue();
    }

    [Fact]
    public void LaunchOverrideAuditEntry_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var entry = new LaunchOverrideAuditEntry(
            timestamp,             LaunchSubsystem.Navigation, "Test", false, "Reason", "System");

        // Act
        var (ts, subsys, action, enabled, reason, source) = entry;

        // Assert
        ts.Should().Be(timestamp);
        subsys.Should().Be(LaunchSubsystem.Navigation);
        action.Should().Be("Test");
        enabled.Should().BeFalse();
        reason.Should().Be("Reason");
        source.Should().Be("System");
    }

    [Theory]
    [InlineData(LaunchSubsystem.Addons)]
    [InlineData(LaunchSubsystem.Frames)]
    [InlineData(LaunchSubsystem.WoWProcess)]
    [InlineData(LaunchSubsystem.Navigation)]
    [InlineData(LaunchSubsystem.KeyBindings)]
    public void LaunchOverrideAuditEntry_AllSubsystemValues_Accepted(LaunchSubsystem subsystem)
    {
        // Arrange & Act
        var entry = new LaunchOverrideAuditEntry(
            DateTimeOffset.UtcNow, subsystem, "Action", true, "Reason", "Source");

        // Assert
        entry.Subsystem.Should().Be(subsystem);
    }

    [Fact]
    public void LaunchOverrideAuditEntry_ToString_ContainsAction()
    {
        // Arrange
        var entry = new LaunchOverrideAuditEntry(
            DateTimeOffset.UtcNow, null, "OverrideAction", true, "Test", "Source");

        // Act
        string result = entry.ToString();

        // Assert
        result.Should().Contain("OverrideAction");
    }
}
