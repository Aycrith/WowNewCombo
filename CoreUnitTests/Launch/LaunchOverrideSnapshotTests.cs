using System;
using System.Collections.Generic;

using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchOverrideSnapshotTests
{
    [Fact]
    public void LaunchOverrideSnapshot_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        Dictionary<LaunchSubsystem, LaunchSubsystemBypass> bypasses = new()
        {
            [LaunchSubsystem.Addons] = new LaunchSubsystemBypass(
                true, "Testing", DateTimeOffset.UtcNow, "Test")
        };

        // Act
        var snapshot = new LaunchOverrideSnapshot(
            AllowStartWithWarnings: true,
            EmergencyBypassAll: false,
            Bypasses: bypasses);

        // Assert
        snapshot.AllowStartWithWarnings.Should().BeTrue();
        snapshot.EmergencyBypassAll.Should().BeFalse();
        snapshot.Bypasses.Should().HaveCount(1);
        snapshot.Bypasses.Should().ContainKey(LaunchSubsystem.Addons);
    }

    [Fact]
    public void LaunchOverrideSnapshot_CreateWithNoBypasses_StoresEmptyDictionary()
    {
        // Arrange
        Dictionary<LaunchSubsystem, LaunchSubsystemBypass> bypasses = new();

        // Act
        var snapshot = new LaunchOverrideSnapshot(
            AllowStartWithWarnings: false,
            EmergencyBypassAll: false,
            Bypasses: bypasses);

        // Assert
        snapshot.Bypasses.Should().BeEmpty();
    }

    [Fact]
    public void LaunchOverrideSnapshot_CreateWithEmergencyBypass_StoresCorrectly()
    {
        // Act
        var snapshot = new LaunchOverrideSnapshot(
            AllowStartWithWarnings: false,
            EmergencyBypassAll: true,
            Bypasses: new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        // Assert
        snapshot.EmergencyBypassAll.Should().BeTrue();
        snapshot.AllowStartWithWarnings.Should().BeFalse();
    }

    [Fact]
    public void LaunchOverrideSnapshot_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new LaunchOverrideSnapshot(
            AllowStartWithWarnings: false,
            EmergencyBypassAll: false,
            Bypasses: new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        // Act
        var modified = original with { AllowStartWithWarnings = true };

        // Assert
        modified.AllowStartWithWarnings.Should().BeTrue();
        modified.EmergencyBypassAll.Should().Be(original.EmergencyBypassAll);
        modified.Bypasses.Should().BeEquivalentTo(original.Bypasses);
    }

    [Fact]
    public void LaunchOverrideSnapshot_Equality_SameValues_AreEqual()
    {
        // Arrange
        Dictionary<LaunchSubsystem, LaunchSubsystemBypass> bypasses = new()
        {
            [LaunchSubsystem.WoWProcess] = new LaunchSubsystemBypass(
                true, "Test", DateTimeOffset.UtcNow, "Source")
        };

        var snapshot1 = new LaunchOverrideSnapshot(true, false, bypasses);
        var snapshot2 = new LaunchOverrideSnapshot(true, false, bypasses);

        // Assert
        snapshot1.Should().Be(snapshot2);
        (snapshot1 == snapshot2).Should().BeTrue();
    }

    [Fact]
    public void LaunchOverrideSnapshot_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var snapshot1 = new LaunchOverrideSnapshot(
            true, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());
        var snapshot2 = new LaunchOverrideSnapshot(
            false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        // Assert
        snapshot1.Should().NotBe(snapshot2);
        (snapshot1 != snapshot2).Should().BeTrue();
    }

    [Fact]
    public void LaunchOverrideSnapshot_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        Dictionary<LaunchSubsystem, LaunchSubsystemBypass> bypasses = new()
        {
            [LaunchSubsystem.KeyBindings] = new LaunchSubsystemBypass(
                false, "Reason", DateTimeOffset.UtcNow, "System")
        };

        var snapshot = new LaunchOverrideSnapshot(true, false, bypasses);

        // Act
        var (allowWarnings, emergencyBypass, resultBypasses) = snapshot;

        // Assert
        allowWarnings.Should().BeTrue();
        emergencyBypass.Should().BeFalse();
        resultBypasses.Should().HaveCount(1);
    }

    [Fact]
    public void LaunchOverrideSnapshot_WithMultipleBypasses_StoresAll()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Dictionary<LaunchSubsystem, LaunchSubsystemBypass> bypasses = new()
        {
            [LaunchSubsystem.Addons] = new LaunchSubsystemBypass(true, "Test", now, "UI"),
            [LaunchSubsystem.Frames] = new LaunchSubsystemBypass(true, "Test2", now, "UI"),
            [LaunchSubsystem.WoWProcess] = new LaunchSubsystemBypass(false, "Test3", now, "System")
        };

        // Act
        var snapshot = new LaunchOverrideSnapshot(true, false, bypasses);

        // Assert
        snapshot.Bypasses.Should().HaveCount(3);
        snapshot.Bypasses[LaunchSubsystem.Addons].Enabled.Should().BeTrue();
        snapshot.Bypasses[LaunchSubsystem.Frames].Reason.Should().Be("Test2");
        snapshot.Bypasses[LaunchSubsystem.WoWProcess].Enabled.Should().BeFalse();
    }

    [Fact]
    public void LaunchOverrideSnapshot_ToString_ContainsPropertyNames()
    {
        // Arrange
        var snapshot = new LaunchOverrideSnapshot(
            true, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        // Act
        string result = snapshot.ToString();

        // Assert
        result.Should().Contain("AllowStartWithWarnings");
        result.Should().Contain("EmergencyBypassAll");
    }
}
