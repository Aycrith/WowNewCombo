using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.AddonComponent;

public class SchoolMaskExtensionTests
{
    [Theory]
    [InlineData(SchoolMask.Physical, "Physical")]
    [InlineData(SchoolMask.Holy, "Holy")]
    [InlineData(SchoolMask.Fire, "Fire")]
    [InlineData(SchoolMask.Nature, "Nature")]
    [InlineData(SchoolMask.Frost, "Frost")]
    [InlineData(SchoolMask.Shadow, "Shadow")]
    [InlineData(SchoolMask.Arcane, "Arcane")]
    [InlineData(SchoolMask.None, "None")]
    public void ToStringF_AllSchools_ReturnCorrectNames(SchoolMask schoolMask, string expected)
    {
        // Act
        string result = schoolMask.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStringF_AllValues_HaveCorrectNames()
    {
        // Arrange & Act
        var allValues = System.Enum.GetValues<SchoolMask>();

        // Assert
        foreach (SchoolMask schoolMask in allValues)
        {
            string result = schoolMask.ToStringF();
            result.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(SchoolMask.Physical, SchoolMask.Physical, true)]
    [InlineData(SchoolMask.Fire, SchoolMask.Fire, true)]
    [InlineData(SchoolMask.Fire, SchoolMask.Physical, false)]
    [InlineData(SchoolMask.Fire, SchoolMask.Nature, false)]
    [InlineData(SchoolMask.None, SchoolMask.Physical, false)]
    public void HasValue_SingleFlag_ReturnsExpected(SchoolMask value, SchoolMask flag, bool expected)
    {
        // Act
        bool result = value.HasValue(flag);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SchoolMask.Fire | SchoolMask.Frost, SchoolMask.Fire, true)]
    [InlineData(SchoolMask.Fire | SchoolMask.Frost, SchoolMask.Frost, true)]
    [InlineData(SchoolMask.Fire | SchoolMask.Frost, SchoolMask.Nature, false)]
    [InlineData(SchoolMask.Fire | SchoolMask.Frost | SchoolMask.Shadow, SchoolMask.Fire, true)]
    public void HasValue_CombinedFlags_ReturnsExpected(SchoolMask value, SchoolMask flag, bool expected)
    {
        // Act
        bool result = value.HasValue(flag);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SchoolMask.Physical)]
    [InlineData(SchoolMask.Holy)]
    [InlineData(SchoolMask.Fire)]
    [InlineData(SchoolMask.Nature)]
    [InlineData(SchoolMask.Frost)]
    [InlineData(SchoolMask.Shadow)]
    [InlineData(SchoolMask.Arcane)]
    public void HasValue_SingleFlagWithItself_ReturnsTrue(SchoolMask schoolMask)
    {
        // Act
        bool result = schoolMask.HasValue(schoolMask);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(SchoolMask.Physical, "Physical")]
    [InlineData(SchoolMask.Fire, "Fire")]
    [InlineData(SchoolMask.Frost, "Frost")]
    [InlineData(SchoolMask.Shadow, "Shadow")]
    public void DamageSchools_HaveCorrectNames(SchoolMask schoolMask, string expected)
    {
        // Act
        string result = schoolMask.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void HasValue_NoneFlag_ReturnsFalse()
    {
        // Arrange
        SchoolMask value = SchoolMask.Physical | SchoolMask.Fire;

        // Act
        bool result = value.HasValue(SchoolMask.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValue_AllSchoolsCombined()
    {
        // Arrange
        SchoolMask allSchools = SchoolMask.Physical | SchoolMask.Holy | SchoolMask.Fire |
                                SchoolMask.Nature | SchoolMask.Frost | SchoolMask.Shadow | SchoolMask.Arcane;

        // Act & Assert
        allSchools.HasValue(SchoolMask.Physical).Should().BeTrue();
        allSchools.HasValue(SchoolMask.Fire).Should().BeTrue();
        allSchools.HasValue(SchoolMask.Shadow).Should().BeTrue();
    }
}
