using Core;
using FluentAssertions;
using Xunit;

namespace CoreUnitTests;

/// <summary>
/// Tests for the ValidationMessage record.
/// </summary>
public class ValidationMessageTests
{
    #region Construction Tests

    [Fact]
    public void ValidationMessage_CreateWithValues_StoresCorrectly()
    {
        // Arrange & Act
        var message = new ValidationMessage("Error", "Something went wrong");

        // Assert
        message.Title.Should().Be("Error");
        message.Description.Should().Be("Something went wrong");
    }

    [Fact]
    public void ValidationMessage_EmptyStrings_Allowed()
    {
        // Arrange & Act
        var message = new ValidationMessage("", "");

        // Assert
        message.Title.Should().BeEmpty();
        message.Description.Should().BeEmpty();
    }

    [Fact]
    public void ValidationMessage_LongStrings_Accepted()
    {
        // Arrange
        var longTitle = new string('T', 500);
        var longDesc = new string('D', 2000);

        // Act
        var message = new ValidationMessage(longTitle, longDesc);

        // Assert
        message.Title.Should().HaveLength(500);
        message.Description.Should().HaveLength(2000);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void ValidationMessage_Equality_SameValues_AreEqual()
    {
        // Arrange
        var msg1 = new ValidationMessage("Title", "Description");
        var msg2 = new ValidationMessage("Title", "Description");

        // Assert
        msg1.Should().Be(msg2);
        msg1.GetHashCode().Should().Be(msg2.GetHashCode());
    }

    [Fact]
    public void ValidationMessage_Equality_DifferentTitles_AreNotEqual()
    {
        // Arrange
        var msg1 = new ValidationMessage("Title1", "Description");
        var msg2 = new ValidationMessage("Title2", "Description");

        // Assert
        msg1.Should().NotBe(msg2);
    }

    [Fact]
    public void ValidationMessage_Equality_DifferentDescriptions_AreNotEqual()
    {
        // Arrange
        var msg1 = new ValidationMessage("Title", "Desc1");
        var msg2 = new ValidationMessage("Title", "Desc2");

        // Assert
        msg1.Should().NotBe(msg2);
    }

    #endregion

    #region Deconstruction Tests

    [Fact]
    public void ValidationMessage_Deconstruct_ExtractsValues()
    {
        // Arrange
        var message = new ValidationMessage("Title", "Description");

        // Act
        var (title, desc) = message;

        // Assert
        title.Should().Be("Title");
        desc.Should().Be("Description");
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void ValidationMessage_With_CreatesNewInstance()
    {
        // Arrange
        var original = new ValidationMessage("Original", "Description");

        // Act
        var modified = original with { Title = "Modified" };

        // Assert
        modified.Should().NotBe(original);
        modified.Title.Should().Be("Modified");
        modified.Description.Should().Be("Description");
        original.Title.Should().Be("Original");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("Warning", "This is a warning")]
    [InlineData("Error", "This is an error")]
    [InlineData("Info", "This is informational")]
    [InlineData("Success", "Operation completed successfully")]
    public void ValidationMessage_VariousTypes_Accepted(string title, string description)
    {
        // Arrange & Act
        var message = new ValidationMessage(title, description);

        // Assert
        message.Title.Should().Be(title);
        message.Description.Should().Be(description);
    }

    [Fact]
    public void ValidationMessage_SpecialCharacters_Accepted()
    {
        // Arrange
        var title = "Error: Failed to load 'config.json'";
        var desc = "Path: C:\\Users\\Test\\Documents. Check permissions!";

        // Act
        var message = new ValidationMessage(title, desc);

        // Assert
        message.Title.Should().Contain("config.json");
        message.Description.Should().Contain("C:\\Users");
    }

    #endregion
}
