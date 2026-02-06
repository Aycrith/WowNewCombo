using Game.Input.Security;

using Xunit;

namespace CoreUnitTests.Input;

/// <summary>
/// Unit tests for VirtualKeyToChar mapping utility.
/// </summary>
public class VirtualKeyToCharTests
{
    [Theory]
    [InlineData(0x30, '0')]
    [InlineData(0x31, '1')]
    [InlineData(0x32, '2')]
    [InlineData(0x33, '3')]
    [InlineData(0x34, '4')]
    [InlineData(0x35, '5')]
    [InlineData(0x36, '6')]
    [InlineData(0x37, '7')]
    [InlineData(0x38, '8')]
    [InlineData(0x39, '9')]
    public void Map_Digits_ReturnsCorrectChar(int vk, char expected)
    {
        char? result = VirtualKeyToChar.Map(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x41, 'a')]
    [InlineData(0x42, 'b')]
    [InlineData(0x43, 'c')]
    [InlineData(0x5A, 'z')]
    public void Map_Letters_ReturnsLowercase(int vk, char expected)
    {
        char? result = VirtualKeyToChar.Map(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x20, ' ')]
    [InlineData(0xBA, ';')]
    [InlineData(0xBB, '=')]
    [InlineData(0xBC, ',')]
    [InlineData(0xBD, '-')]
    [InlineData(0xBE, '.')]
    [InlineData(0xBF, '/')]
    [InlineData(0xC0, '`')]
    [InlineData(0xDB, '[')]
    [InlineData(0xDC, '\\')]
    [InlineData(0xDD, ']')]
    [InlineData(0xDE, '\'')]
    public void Map_SymbolKeys_ReturnsCorrectChar(int vk, char expected)
    {
        char? result = VirtualKeyToChar.Map(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x70)] // F1
    [InlineData(0x71)] // F2
    [InlineData(0x7B)] // F12
    [InlineData(0x25)] // Left arrow
    [InlineData(0x26)] // Up arrow
    [InlineData(0x27)] // Right arrow
    [InlineData(0x28)] // Down arrow
    [InlineData(0x1B)] // Escape
    [InlineData(0x09)] // Tab
    [InlineData(0x0D)] // Enter
    [InlineData(0x10)] // Shift
    [InlineData(0x11)] // Control
    [InlineData(0x12)] // Alt
    [InlineData(0x2D)] // Insert
    [InlineData(0x2E)] // Delete
    public void Map_NonPrintableKeys_ReturnsNull(int vk)
    {
        char? result = VirtualKeyToChar.Map(vk);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(0x30, true)]  // '0'
    [InlineData(0x41, true)]  // 'A'
    [InlineData(0x20, true)]  // Space
    [InlineData(0xBA, true)]  // ';'
    [InlineData(0x70, false)] // F1
    [InlineData(0x25, false)] // Left arrow
    [InlineData(0x10, false)] // Shift
    public void IsPrintable_ReturnsCorrectValue(int vk, bool expected)
    {
        bool result = VirtualKeyToChar.IsPrintable(vk);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x30, "0")]
    [InlineData(0x20, "Space")]
    [InlineData(0x70, "F1")]
    [InlineData(0x25, "Left")]
    [InlineData(0x10, "VK_10")]
    public void GetDisplayName_ReturnsCorrectName(int vk, string expected)
    {
        string result = VirtualKeyToChar.GetDisplayName(vk);
        Assert.Equal(expected, result);
    }
}
