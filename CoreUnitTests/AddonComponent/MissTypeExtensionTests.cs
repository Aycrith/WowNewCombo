using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.AddonComponent;

public class MissTypeExtensionTests
{
    [Theory]
    [InlineData(MissType.NONE, "NONE")]
    [InlineData(MissType.ABSORB, "ABSORB")]
    [InlineData(MissType.BLOCK, "BLOCK")]
    [InlineData(MissType.DEFLECT, "DEFLECT")]
    [InlineData(MissType.DODGE, "DODGE")]
    [InlineData(MissType.EVADE, "EVADE")]
    [InlineData(MissType.IMMUNE, "IMMUNE")]
    [InlineData(MissType.MISS, "MISS")]
    [InlineData(MissType.PARRY, "PARRY")]
    [InlineData(MissType.REFLECT, "REFLECT")]
    [InlineData(MissType.RESIST, "RESIST")]
    public void ToStringF_AllMissTypes_ReturnCorrectNames(MissType missType, string expected)
    {
        // Act
        string result = missType.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStringF_AllValues_HaveCorrectNames()
    {
        // Arrange & Act
        var allValues = System.Enum.GetValues<MissType>();

        // Assert
        foreach (MissType missType in allValues)
        {
            string result = missType.ToStringF();
            result.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(MissType.NONE, false)]
    [InlineData(MissType.MISS, true)]
    [InlineData(MissType.DODGE, true)]
    [InlineData(MissType.PARRY, true)]
    [InlineData(MissType.BLOCK, true)]
    public void CommonMissTypes_ReturnExpectedValues(MissType missType, bool shouldNotBeNone)
    {
        // Act
        string result = missType.ToStringF();

        // Assert
        if (shouldNotBeNone)
        {
            result.Should().NotBe("NONE");
        }
        else
        {
            result.Should().Be("NONE");
        }
    }

    [Theory]
    [InlineData(MissType.IMMUNE, "IMMUNE")]
    [InlineData(MissType.RESIST, "RESIST")]
    [InlineData(MissType.ABSORB, "ABSORB")]
    public void DefenseTypes_HaveCorrectNames(MissType missType, string expected)
    {
        // Act
        string result = missType.ToStringF();

        // Assert
        result.Should().Be(expected);
    }
}
