using System;

using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.AddonComponent;

public class GuidTypeExtensionTests
{
    [Theory]
    [InlineData(GuidType.Creature, "Creature")]
    [InlineData(GuidType.Pet, "Pet")]
    [InlineData(GuidType.GameObject, "GameObject")]
    [InlineData(GuidType.Vehicle, "Vehicle")]
    [InlineData(GuidType.Unknown, "Unknown")]
    public void ToStringF_AllTypes_ReturnCorrectNames(GuidType guidType, string expected)
    {
        // Act
        string result = guidType.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStringF_AllValues_HaveCorrectNames()
    {
        // Arrange & Act
        var allValues = Enum.GetValues<GuidType>();

        // Assert
        foreach (GuidType guidType in allValues)
        {
            string result = guidType.ToStringF();
            result.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ToStringF_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        GuidType invalidValue = (GuidType)999;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidValue.ToStringF());
    }
}
