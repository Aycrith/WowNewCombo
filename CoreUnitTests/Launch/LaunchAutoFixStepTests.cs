using Core.Launch;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Launch;

public class LaunchAutoFixStepTests
{
    [Theory]
    [InlineData("addon_config.json", LaunchAutoFixStatus.Applied, "Created default addon_config.json")]
    [InlineData("DataToColor install", LaunchAutoFixStatus.Skipped, "Addon already present")]
    [InlineData("Frame auto-config", LaunchAutoFixStatus.Failed, "Configuration failed")]
    public void LaunchAutoFixStep_CreateWithValues_StoresCorrectly(
        string name,
        LaunchAutoFixStatus status,
        string message)
    {
        // Act
        var step = new LaunchAutoFixStep(name, status, message);

        // Assert
        step.Name.Should().Be(name);
        step.Status.Should().Be(status);
        step.Message.Should().Be(message);
    }

    [Fact]
    public void LaunchAutoFixStep_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new LaunchAutoFixStep("Test Step", LaunchAutoFixStatus.Applied, "Original message");

        // Act
        var modified = original with { Message = "Modified message" };

        // Assert
        modified.Name.Should().Be(original.Name);
        modified.Status.Should().Be(original.Status);
        modified.Message.Should().Be("Modified message");
    }

    [Fact]
    public void LaunchAutoFixStep_Equality_SameValues_AreEqual()
    {
        // Arrange
        var step1 = new LaunchAutoFixStep("Addon Config", LaunchAutoFixStatus.Applied, "Success");
        var step2 = new LaunchAutoFixStep("Addon Config", LaunchAutoFixStatus.Applied, "Success");

        // Assert
        step1.Should().Be(step2);
        (step1 == step2).Should().BeTrue();
        step1.GetHashCode().Should().Be(step2.GetHashCode());
    }

    [Fact]
    public void LaunchAutoFixStep_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var step1 = new LaunchAutoFixStep("Config", LaunchAutoFixStatus.Applied, "OK");
        var step2 = new LaunchAutoFixStep("Config", LaunchAutoFixStatus.Failed, "OK");

        // Assert
        step1.Should().NotBe(step2);
        (step1 != step2).Should().BeTrue();
    }

    [Fact]
    public void LaunchAutoFixStep_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        var step = new LaunchAutoFixStep("Test", LaunchAutoFixStatus.Skipped, "Message");

        // Act
        var (name, status, message) = step;

        // Assert
        name.Should().Be("Test");
        status.Should().Be(LaunchAutoFixStatus.Skipped);
        message.Should().Be("Message");
    }

    [Theory]
    [InlineData(LaunchAutoFixStatus.Skipped)]
    [InlineData(LaunchAutoFixStatus.Applied)]
    [InlineData(LaunchAutoFixStatus.Failed)]
    public void LaunchAutoFixStep_AllStatusValues_Accepted(LaunchAutoFixStatus status)
    {
        // Arrange & Act
        var step = new LaunchAutoFixStep("Test", status, "Message");

        // Assert
        step.Status.Should().Be(status);
    }

    [Fact]
    public void LaunchAutoFixStep_ToString_ContainsName()
    {
        // Arrange
        var step = new LaunchAutoFixStep("Config Step", LaunchAutoFixStatus.Applied, "Done");

        // Act
        string result = step.ToString();

        // Assert
        result.Should().Contain("Config Step");
    }
}
