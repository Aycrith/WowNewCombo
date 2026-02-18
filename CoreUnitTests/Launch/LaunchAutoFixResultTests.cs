using System;
using System.Collections.Generic;

using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchAutoFixResultTests
{
    [Fact]
    public void LaunchAutoFixResult_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        List<LaunchAutoFixStep> steps =
        [
            new("Step 1", LaunchAutoFixStatus.Applied, "First step completed"),
            new("Step 2", LaunchAutoFixStatus.Skipped, "Second step skipped")
        ];

        // Act
        var result = new LaunchAutoFixResult(true, false, steps);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiresRestart.Should().BeFalse();
        result.Steps.Should().HaveCount(2);
        result.Steps[0].Name.Should().Be("Step 1");
        result.Steps[1].Name.Should().Be("Step 2");
    }

    [Fact]
    public void LaunchAutoFixResult_CreateWithFailure_StoresCorrectly()
    {
        // Arrange
        List<LaunchAutoFixStep> steps =
        [
            new("Step 1", LaunchAutoFixStatus.Failed, "Configuration error")
        ];

        // Act
        var result = new LaunchAutoFixResult(false, true, steps);

        // Assert
        result.Success.Should().BeFalse();
        result.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public void LaunchAutoFixResult_WithEmptySteps_StoresEmptyList()
    {
        // Arrange
        List<LaunchAutoFixStep> steps = [];

        // Act
        var result = new LaunchAutoFixResult(true, false, steps);

        // Assert
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public void LaunchAutoFixResult_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new LaunchAutoFixResult(
            false,
            false,
            new List<LaunchAutoFixStep>());

        // Act
        var modified = original with { Success = true };

        // Assert
        modified.Success.Should().BeTrue();
        modified.RequiresRestart.Should().Be(original.RequiresRestart);
        modified.Steps.Should().BeEquivalentTo(original.Steps);
    }

    [Fact]
    public void LaunchAutoFixResult_Equality_SameValues_AreEqual()
    {
        // Arrange
        var steps = new List<LaunchAutoFixStep>
        {
            new("Config", LaunchAutoFixStatus.Applied, "OK")
        };

        var result1 = new LaunchAutoFixResult(true, false, steps);
        var result2 = new LaunchAutoFixResult(true, false, steps);

        // Assert
        result1.Should().Be(result2);
        (result1 == result2).Should().BeTrue();
    }

    [Fact]
    public void LaunchAutoFixResult_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var steps = new List<LaunchAutoFixStep>
        {
            new("Config", LaunchAutoFixStatus.Applied, "OK")
        };

        var result1 = new LaunchAutoFixResult(true, false, steps);
        var result2 = new LaunchAutoFixResult(false, false, steps);

        // Assert
        result1.Should().NotBe(result2);
        (result1 != result2).Should().BeTrue();
    }

    [Fact]
    public void LaunchAutoFixResult_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        List<LaunchAutoFixStep> steps =
        [
            new("Test", LaunchAutoFixStatus.Applied, "Done")
        ];

        var result = new LaunchAutoFixResult(true, true, steps);

        // Act
        var (success, requiresRestart, resultSteps) = result;

        // Assert
        success.Should().BeTrue();
        requiresRestart.Should().BeTrue();
        resultSteps.Should().HaveCount(1);
    }

    [Fact]
    public void LaunchAutoFixResult_Equality_DifferentSteps_AreNotEqual()
    {
        // Arrange
        var steps1 = new List<LaunchAutoFixStep>
        {
            new("Step 1", LaunchAutoFixStatus.Applied, "OK")
        };

        var steps2 = new List<LaunchAutoFixStep>
        {
            new("Step 1", LaunchAutoFixStatus.Failed, "OK")
        };

        var result1 = new LaunchAutoFixResult(true, false, steps1);
        var result2 = new LaunchAutoFixResult(true, false, steps2);

        // Assert
        result1.Should().NotBe(result2);
    }
}
