using System;

using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Chat;

public class ChatMessageEntryTests
{
    [Fact]
    public void ChatMessageEntry_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        DateTime time = DateTime.UtcNow;
        const ChatMessageType type = ChatMessageType.Say;
        const string author = "PlayerOne";
        const string message = "Hello world!";

        // Act
        var entry = new ChatMessageEntry(time, type, author, message);

        // Assert
        entry.Time.Should().Be(time);
        entry.Type.Should().Be(type);
        entry.Author.Should().Be(author);
        entry.Message.Should().Be(message);
    }

    [Theory]
    [InlineData(ChatMessageType.Whisper)]
    [InlineData(ChatMessageType.Say)]
    [InlineData(ChatMessageType.Yell)]
    [InlineData(ChatMessageType.Emote)]
    [InlineData(ChatMessageType.Party)]
    public void ChatMessageEntry_AllMessageTypes_Accepted(ChatMessageType type)
    {
        // Arrange & Act
        var entry = new ChatMessageEntry(DateTime.UtcNow, type, "Author", "Message");

        // Assert
        entry.Type.Should().Be(type);
    }

    [Fact]
    public void ChatMessageEntry_WithLongMessage_StoresCorrectly()
    {
        // Arrange
        string longMessage = "This is a very long message that exceeds normal chat length " +
                            "and tests that the record can handle longer strings without issues. " +
                            "It contains multiple sentences and should be stored correctly.";

        // Act
        var entry = new ChatMessageEntry(DateTime.UtcNow, ChatMessageType.Say, "Author", longMessage);

        // Assert
        entry.Message.Should().Be(longMessage);
    }

    [Fact]
    public void ChatMessageEntry_WithEmptyMessage_StoresCorrectly()
    {
        // Act
        var entry = new ChatMessageEntry(DateTime.UtcNow, ChatMessageType.Emote, "Player", "");

        // Assert
        entry.Message.Should().BeEmpty();
    }

    [Fact]
    public void ChatMessageEntry_WithSpecialCharacters_StoresCorrectly()
    {
        // Arrange
        string messageWithSpecialChars = "Hello! @#$%^&*()_+ {}[]|;':\",./<>?";

        // Act
        var entry = new ChatMessageEntry(DateTime.UtcNow, ChatMessageType.Say, "Player", messageWithSpecialChars);

        // Assert
        entry.Message.Should().Be(messageWithSpecialChars);
    }

    [Fact]
    public void ChatMessageEntry_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTime time = DateTime.UtcNow;
        var entry1 = new ChatMessageEntry(time, ChatMessageType.Say, "Player", "Hello");
        var entry2 = new ChatMessageEntry(time, ChatMessageType.Say, "Player", "Hello");

        // Assert
        entry1.Should().Be(entry2);
        (entry1 == entry2).Should().BeTrue();
        entry1.GetHashCode().Should().Be(entry2.GetHashCode());
    }

    [Fact]
    public void ChatMessageEntry_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DateTime time = DateTime.UtcNow;
        var entry1 = new ChatMessageEntry(time, ChatMessageType.Say, "Player1", "Hello");
        var entry2 = new ChatMessageEntry(time, ChatMessageType.Say, "Player2", "Hello");

        // Assert
        entry1.Should().NotBe(entry2);
        (entry1 != entry2).Should().BeTrue();
    }

    [Fact]
    public void ChatMessageEntry_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        DateTime time = DateTime.UtcNow;
        var entry = new ChatMessageEntry(time, ChatMessageType.Whisper, "Sender", "Private message");

        // Act
        var (entryTime, type, author, message) = entry;

        // Assert
        entryTime.Should().Be(time);
        type.Should().Be(ChatMessageType.Whisper);
        author.Should().Be("Sender");
        message.Should().Be("Private message");
    }

    [Fact]
    public void ChatMessageEntry_ToString_ContainsMessage()
    {
        // Arrange
        var entry = new ChatMessageEntry(DateTime.UtcNow, ChatMessageType.Say, "Player", "Test message");

        // Act
        string result = entry.ToString();

        // Assert
        result.Should().Contain("Test message");
    }

    [Fact]
    public void ChatMessageEntry_WithUnicodeCharacters_StoresCorrectly()
    {
        // Arrange
        string unicodeMessage = "Hello 世界! Привет! 🎮";

        // Act
        var entry = new ChatMessageEntry(DateTime.UtcNow, ChatMessageType.Say, "Player", unicodeMessage);

        // Assert
        entry.Message.Should().Be(unicodeMessage);
    }

    [Fact]
    public void ChatMessageEntry_HistoricalTime_StoresCorrectly()
    {
        // Arrange
        DateTime pastTime = new(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var entry = new ChatMessageEntry(pastTime, ChatMessageType.Party, "Player", "Old message");

        // Assert
        entry.Time.Should().Be(pastTime);
    }
}
