using System;
using System.Collections.Generic;

using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchReadinessSnapshotTests
{
    [Fact]
    public void LaunchReadinessSnapshot_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<LaunchSubsystemCheck> checks = new()
        {
            new LaunchSubsystemCheck(
                LaunchSubsystem.Addons,
                LaunchStatus.Ok,
                "Addons",
                "All addons ready",
                true,
                false,
                timestamp)
        };

        LaunchOverrideSnapshot overrides = new(
            false,
            false,
            new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        // Act
        var snapshot = new LaunchReadinessSnapshot(
            IsLaunchReady: true,
            CanStartBot: true,
            TimestampUtc: timestamp,
            Checks: checks,
            Overrides: overrides);

        // Assert
        snapshot.IsLaunchReady.Should().BeTrue();
        snapshot.CanStartBot.Should().BeTrue();
        snapshot.TimestampUtc.Should().Be(timestamp);
        snapshot.Checks.Should().HaveCount(1);
        snapshot.Overrides.Should().Be(overrides);
    }

    [Fact]
    public void LaunchReadinessSnapshot_CreateNotReady_StoresCorrectly()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<LaunchSubsystemCheck> checks = new()
        {
            new LaunchSubsystemCheck(
                LaunchSubsystem.WoWProcess,
                LaunchStatus.Error,
                "WoW",
                "Process not running",
                true,
                true,
                timestamp)
        };

        // Act
        var snapshot = new LaunchReadinessSnapshot(
            false,
            false,
            timestamp,
            checks,
            new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));

        // Assert
        snapshot.IsLaunchReady.Should().BeFalse();
        snapshot.CanStartBot.Should().BeFalse();
    }

    [Fact]
    public void LaunchReadinessSnapshot_CreateWithMultipleChecks_StoresAll()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<LaunchSubsystemCheck> checks = new()
        {
            new LaunchSubsystemCheck(LaunchSubsystem.Addons, LaunchStatus.Ok, "Addons", "Ready", true, false, timestamp),
            new LaunchSubsystemCheck(LaunchSubsystem.WoWProcess, LaunchStatus.Ok, "WoW", "Running", true, false, timestamp),
            new LaunchSubsystemCheck(LaunchSubsystem.Frames, LaunchStatus.Warning, "Frames", "Partial", true, false, timestamp)
        };

        // Act
        var snapshot = new LaunchReadinessSnapshot(
            true, true, timestamp, checks,
            new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));

        // Assert
        snapshot.Checks.Should().HaveCount(3);
    }

    [Fact]
    public void LaunchReadinessSnapshot_With_ModifiesSingleProperty()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var original = new LaunchReadinessSnapshot(
            false,
            false,
            timestamp,
            new List<LaunchSubsystemCheck>(),
            new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));

        // Act
        var modified = original with { IsLaunchReady = true };

        // Assert
        modified.IsLaunchReady.Should().BeTrue();
        modified.CanStartBot.Should().Be(original.CanStartBot);
        modified.TimestampUtc.Should().Be(original.TimestampUtc);
    }

    [Fact]
    public void LaunchReadinessSnapshot_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<LaunchSubsystemCheck> checks = new()
        {
            new LaunchSubsystemCheck(LaunchSubsystem.Addons, LaunchStatus.Ok, "Test", "Msg", true, false, timestamp)
        };

        LaunchOverrideSnapshot overrides = new(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        var snapshot1 = new LaunchReadinessSnapshot(true, true, timestamp, checks, overrides);
        var snapshot2 = new LaunchReadinessSnapshot(true, true, timestamp, checks, overrides);

        // Assert
        snapshot1.Should().Be(snapshot2);
        (snapshot1 == snapshot2).Should().BeTrue();
    }

    [Fact]
    public void LaunchReadinessSnapshot_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var snapshot1 = new LaunchReadinessSnapshot(
            true, false, timestamp, new List<LaunchSubsystemCheck>(),
            new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));
        var snapshot2 = new LaunchReadinessSnapshot(
            false, false, timestamp, new List<LaunchSubsystemCheck>(),
            new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));

        // Assert
        snapshot1.Should().NotBe(snapshot2);
        (snapshot1 != snapshot2).Should().BeTrue();
    }

    [Fact]
    public void LaunchReadinessSnapshot_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<LaunchSubsystemCheck> checks = new()
        {
            new LaunchSubsystemCheck(LaunchSubsystem.KeyBindings, LaunchStatus.Ok, "KB", "Ready", true, false, timestamp)
        };

        LaunchOverrideSnapshot overrides = new(true, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());
        var snapshot = new LaunchReadinessSnapshot(false, false, timestamp, checks, overrides);

        // Act
        var (isReady, canStart, ts, resultChecks, resultOverrides) = snapshot;

        // Assert
        isReady.Should().BeFalse();
        canStart.Should().BeFalse();
        ts.Should().Be(timestamp);
        resultChecks.Should().HaveCount(1);
        resultOverrides.AllowStartWithWarnings.Should().BeTrue();
    }

    [Fact]
    public void LaunchReadinessSnapshot_ReadyWithWarnings_CanStartTrue()
    {
        // Arrange
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        List<LaunchSubsystemCheck> checks = new()
        {
            new LaunchSubsystemCheck(LaunchSubsystem.Frames, LaunchStatus.Warning, "Frames", "Partial config", true, false, timestamp)
        };

        LaunchOverrideSnapshot overrides = new(true, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>());

        // Act
        var snapshot = new LaunchReadinessSnapshot(true, true, timestamp, checks, overrides);

        // Assert
        snapshot.IsLaunchReady.Should().BeTrue();
        snapshot.CanStartBot.Should().BeTrue();
    }

    [Fact]
    public void LaunchReadinessSnapshot_ToString_ContainsPropertyNames()
    {
        // Arrange
        var snapshot = new LaunchReadinessSnapshot(
            true, false, DateTimeOffset.UtcNow, new List<LaunchSubsystemCheck>(),
            new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));

        // Act
        string result = snapshot.ToString();

        // Assert
        result.Should().Contain("IsLaunchReady");
        result.Should().Contain("CanStartBot");
    }
}
