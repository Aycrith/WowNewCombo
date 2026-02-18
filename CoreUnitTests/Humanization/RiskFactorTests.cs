using Core.Humanization;
using FluentAssertions;
using Xunit;

namespace CoreUnitTests.Humanization;

/// <summary>
/// Tests for the RiskFactor record.
/// </summary>
public class RiskFactorTests
{
    #region Construction Tests

    [Fact]
    public void RiskFactor_CreateWithValues_StoresCorrectly()
    {
        // Arrange & Act
        var factor = new RiskFactor("TestFactor", 0.75, "Test description");

        // Assert
        factor.Name.Should().Be("TestFactor");
        factor.Score.Should().Be(0.75);
        factor.Description.Should().Be("Test description");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(0.25)]
    [InlineData(0.99)]
    public void RiskFactor_VariousScores_Accepted(double score)
    {
        // Arrange & Act
        var factor = new RiskFactor("Name", score, "Description");

        // Assert
        factor.Score.Should().Be(score);
    }

    [Fact]
    public void RiskFactor_EmptyStrings_Allowed()
    {
        // Arrange & Act
        var factor = new RiskFactor("", 0.5, "");

        // Assert
        factor.Name.Should().BeEmpty();
        factor.Description.Should().BeEmpty();
    }

    [Fact]
    public void RiskFactor_LongStrings_Accepted()
    {
        // Arrange
        var longName = new string('N', 200);
        var longDesc = new string('D', 1000);

        // Act
        var factor = new RiskFactor(longName, 0.5, longDesc);

        // Assert
        factor.Name.Should().HaveLength(200);
        factor.Description.Should().HaveLength(1000);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void RiskFactor_Equality_SameValues_AreEqual()
    {
        // Arrange
        var factor1 = new RiskFactor("Test", 0.5, "Desc");
        var factor2 = new RiskFactor("Test", 0.5, "Desc");

        // Assert
        factor1.Should().Be(factor2);
        factor1.GetHashCode().Should().Be(factor2.GetHashCode());
    }

    [Fact]
    public void RiskFactor_Equality_DifferentNames_AreNotEqual()
    {
        // Arrange
        var factor1 = new RiskFactor("Test1", 0.5, "Desc");
        var factor2 = new RiskFactor("Test2", 0.5, "Desc");

        // Assert
        factor1.Should().NotBe(factor2);
    }

    [Fact]
    public void RiskFactor_Equality_DifferentScores_AreNotEqual()
    {
        // Arrange
        var factor1 = new RiskFactor("Test", 0.7, "Desc");
        var factor2 = new RiskFactor("Test", 0.8, "Desc");

        // Assert
        factor1.Should().NotBe(factor2);
    }

    [Fact]
    public void RiskFactor_Equality_DifferentDescriptions_AreNotEqual()
    {
        // Arrange
        var factor1 = new RiskFactor("Test", 0.5, "Desc1");
        var factor2 = new RiskFactor("Test", 0.5, "Desc2");

        // Assert
        factor1.Should().NotBe(factor2);
    }

    #endregion

    #region Deconstruction Tests

    [Fact]
    public void RiskFactor_Deconstruct_ExtractsAllValues()
    {
        // Arrange
        var factor = new RiskFactor("MyFactor", 0.85, "High risk");

        // Act
        var (name, score, desc) = factor;

        // Assert
        name.Should().Be("MyFactor");
        score.Should().Be(0.85);
        desc.Should().Be("High risk");
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void RiskFactor_With_CreatesNewInstance()
    {
        // Arrange
        var original = new RiskFactor("Original", 0.5, "Desc");

        // Act
        var modified = original with { Score = 0.9 };

        // Assert
        modified.Should().NotBe(original);
        modified.Score.Should().Be(0.9);
        original.Score.Should().Be(0.5);
    }

    [Fact]
    public void RiskFactor_With_PreservesOtherValues()
    {
        // Arrange
        var original = new RiskFactor("Name", 0.5, "Desc");

        // Act
        var modified = original with { Description = "New description" };

        // Assert
        modified.Name.Should().Be(original.Name);
        modified.Score.Should().Be(original.Score);
        modified.Description.Should().Be("New description");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RiskFactor_ZeroScore_Accepted()
    {
        // Arrange & Act
        var factor = new RiskFactor("Safe", 0.0, "No risk");

        // Assert
        factor.Score.Should().Be(0.0);
    }

    [Fact]
    public void RiskFactor_MaxScore_Accepted()
    {
        // Arrange & Act
        var factor = new RiskFactor("Critical", 1.0, "Maximum risk");

        // Assert
        factor.Score.Should().Be(1.0);
    }

    [Theory]
    [InlineData("RepetitiveAction", 0.3, "User is spamming same action")]
    [InlineData("PatternDetected", 0.7, "Bot-like pattern identified")]
    [InlineData("Reported", 0.9, "User reported by others")]
    [InlineData("ManualReview", 0.5, "Requires manual review")]
    public void RiskFactor_VariousRiskTypes_Accepted(string name, double score, string desc)
    {
        // Arrange & Act
        var factor = new RiskFactor(name, score, desc);

        // Assert
        factor.Name.Should().Be(name);
        factor.Score.Should().Be(score);
        factor.Description.Should().Be(desc);
    }

    [Fact]
    public void RiskFactor_SpecialCharacters_Accepted()
    {
        // Arrange
        var name = "Risk: Pattern[0x01]";
        var desc = "Detected 'suspicious' behavior (98% confidence)";

        // Act
        var factor = new RiskFactor(name, 0.75, desc);

        // Assert
        factor.Name.Should().Contain("Pattern");
        factor.Description.Should().Contain("suspicious");
    }

    #endregion
}
