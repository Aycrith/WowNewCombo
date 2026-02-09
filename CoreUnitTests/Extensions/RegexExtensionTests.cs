using System.Text.RegularExpressions;

using Core.Extensions;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Extensions;

public class RegexExtensionTests
{
    [Fact]
    public void Replace_NamedGroup_ReplacesCorrectly()
    {
        // Arrange - Pattern that matches only "World" specifically
        string input = "Hello World";
        Regex regex = new(@"(?<word>World)");
        string groupName = "word";
        string replacement = "Universe";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("Hello Universe");
    }

    [Fact]
    public void Replace_MultipleMatches_ReplacesFirstOccurrence()
    {
        // Arrange
        string input = "The quick brown fox";
        Regex regex = new(@"(?<animal>fox)");
        string groupName = "animal";
        string replacement = "dog";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("The quick brown dog");
    }

    [Fact]
    public void Replace_NoMatch_ReturnsOriginalString()
    {
        // Arrange
        string input = "Hello World";
        Regex regex = new(@"(?<missing>xyz)");
        string groupName = "missing";
        string replacement = "replacement";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("Hello World");
    }

    [Fact]
    public void Replace_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        string input = "";
        Regex regex = new(@"(?<word>\w+)");
        string groupName = "word";
        string replacement = "test";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Replace_PhoneNumber_ReplacesAreaCode()
    {
        // Arrange
        string input = "Call me at (555) 123-4567";
        Regex regex = new(@"\((?<areaCode>\d{3})\)");
        string groupName = "areaCode";
        string replacement = "888";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("Call me at (888) 123-4567");
    }

    [Fact]
    public void Replace_Date_ReplacesMonth()
    {
        // Arrange
        string input = "2024-01-15";
        Regex regex = new(@"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})");
        string groupName = "month";
        string replacement = "12";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("2024-12-15");
    }

    [Fact]
    public void Replace_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        string input = "Price: $100";
        Regex regex = new(@"\$(?<amount>\d+)");
        string groupName = "amount";
        string replacement = "500";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("Price: $500");
    }

    [Fact]
    public void Replace_WhitespaceGroup_ReplacesCorrectly()
    {
        // Arrange
        string input = "word1 word2 word3";
        Regex regex = new(@"(?<first>word1)");
        string groupName = "first";
        string replacement = "replaced";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("replaced word2 word3");
    }

    [Fact]
    public void Replace_Email_ReplacesDomain()
    {
        // Arrange
        string input = "user@olddomain.com";
        Regex regex = new(@"@(?<domain>\w+\.\w+)");
        string groupName = "domain";
        string replacement = "newdomain.com";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("user@newdomain.com");
    }

    [Fact]
    public void Replace_Url_ReplacesProtocol()
    {
        // Arrange
        string input = "http://example.com";
        Regex regex = new(@"(?<protocol>https?):");
        string groupName = "protocol";
        string replacement = "https";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("https://example.com");
    }

    [Fact]
    public void Replace_WithEmptyReplacement_ClearsGroup()
    {
        // Arrange
        string input = "prefix-content-suffix";
        Regex regex = new(@"-(?<middle>\w+)-");
        string groupName = "middle";
        string replacement = "";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert - The group content is replaced with empty string
        // Full match "-content-" becomes "--" after removing "content"
        result.Should().Be("prefix--suffix");
    }

    [Fact]
    public void Replace_MultipleConsecutiveGroups_ReplacesCorrectly()
    {
        // Arrange - Pattern matches "ABC123" and "DEF456" separately
        string input = "ABC123DEF456";
        Regex regex = new(@"(?<letters>[A-Z]+)(?<digits>\d+)");
        string groupName = "letters";
        string replacement = "XYZ";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert - Both "ABC" and "DEF" get replaced, giving "XYZ123XYZ456"
        result.Should().Be("XYZ123XYZ456");
    }

    [Fact]
    public void Replace_CaseSensitive_RespectsRegexOptions()
    {
        // Arrange - IgnoreCase means pattern matches all case variations
        string input = "Hello HELLO hello";
        Regex regex = new(@"(?<word>HELLO)", RegexOptions.IgnoreCase);
        string groupName = "word";
        string replacement = "World";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert - All three matches (Hello, HELLO, hello) get replaced
        result.Should().Be("World World World");
    }

    [Fact]
    public void Replace_InvalidGroupName_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        string input = "Hello World";
        Regex regex = new(@"(?<word>\w+)");
        string groupName = "invalid";
        string replacement = "test";

        // Act & Assert - Should throw when group name doesn't exist
        // Implementation throws ArgumentOutOfRangeException from String.Remove
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            input.Replace(regex, groupName, replacement));
    }

    [Fact]
    public void Replace_LongText_PerformsEfficiently()
    {
        // Arrange
        string input = new string('a', 10000) + "TARGET" + new string('b', 10000);
        Regex regex = new(@"(?<marker>TARGET)");
        string groupName = "marker";
        string replacement = "REPLACED";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Contain("REPLACED");
        result.Should().NotContain("TARGET");
    }

    [Fact]
    public void Replace_WithUnicode_HandlesCorrectly()
    {
        // Arrange - Note: \w in .NET matches Unicode word characters by default
        // So both "Hello" and "世界" are matched by (?<greeting>\w+)
        string input = "Hello 世界";
        Regex regex = new(@"(?<greeting>\w+)");
        string groupName = "greeting";
        string replacement = "Bonjour";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert - Both "Hello" and "世界" get replaced (\w matches Unicode)
        result.Should().Be("Bonjour Bonjour");
    }

    [Fact]
    public void Replace_WithNewlines_HandlesCorrectly()
    {
        // Arrange
        string input = "Line1\nLine2\nLine3";
        Regex regex = new(@"(?<line>Line2)");
        string groupName = "line";
        string replacement = "REPLACED";

        // Act
        string result = input.Replace(regex, groupName, replacement);

        // Assert
        result.Should().Be("Line1\nREPLACED\nLine3");
    }
}
