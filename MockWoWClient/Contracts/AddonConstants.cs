using System.Runtime.CompilerServices;

namespace MockWoWClient.Contracts;

/// <summary>
/// Configuration constants matching the DataToColor addon specification.
/// </summary>
public static class AddonConstants
{
    /// <summary>
    /// Total number of pixel frames in the addon.
    /// </summary>
    public const int NumberOfFrames = 324;

    /// <summary>
    /// Number of rows in the pixel grid.
    /// </summary>
    public const int FrameRows = 50;

    /// <summary>
    /// Size of each pixel cell in pixels.
    /// </summary>
    public const int CellSize = 4;

    /// <summary>
    /// Spacing between cells in pixels.
    /// </summary>
    public const int CellSpacing = 0;

    /// <summary>
    /// Validation frame 0 value (always black).
    /// </summary>
    public const int Frame0Value = 0;

    /// <summary>
    /// Frame 322 is the global tick counter.
    /// </summary>
    public const int GlobalTimeCellIndex = 322;

    /// <summary>
    /// Frame 323 is a validation value (2000001).
    /// </summary>
    public const int ValidationCellIndex = 323;
    public const int ValidationCellValue = 2000001;

    /// <summary>
    /// Last frame end marker RGB values.
    /// </summary>
    public const int EndMarkerR = 30;
    public const int EndMarkerG = 132;
    public const int EndMarkerB = 129;
}

/// <summary>
/// Represents a single data frame with position and pixel data.
/// </summary>
public readonly record struct FrameData
{
    public int Index { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Value { get; init; }

    public FrameData(int index, int x, int y, int width, int height, int value)
    {
        Index = index;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Value = value;
    }
}

/// <summary>
/// Encodes integer, float, and string values to RGB pixel format.
/// Matches the Lua addon encoding specification.
/// </summary>
public static class FrameEncoder
{
    /// <summary>
    /// Encodes an integer (0 to 16,777,215) to RGB values.
    /// </summary>
    /// <param name="value">The integer value to encode.</param>
    /// <returns>RGB values as a tuple (R, G, B).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte r, byte g, byte b) EncodeInt(int value)
    {
        byte r = (byte)((value >> 16) & 0xFF);
        byte g = (byte)((value >> 8) & 0xFF);
        byte b = (byte)(value & 0xFF);
        return (r, g, b);
    }

    /// <summary>
    /// Decodes RGB values back to an integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecodeInt(byte r, byte g, byte b)
    {
        return b | (g << 8) | (r << 16);
    }

    /// <summary>
    /// Encodes a float (0 to 9.99999) to RGB values.
    /// Multiplies by 100000 and converts to integer.
    /// </summary>
    /// <param name="value">The float value to encode.</param>
    /// <returns>RGB values as a tuple (R, G, B).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte r, byte g, byte b) EncodeFloat(float value)
    {
        int intValue = (int)(value * 100000f);
        return EncodeInt(intValue);
    }

    /// <summary>
    /// Decodes RGB values back to a float.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DecodeFloat(byte r, byte g, byte b)
    {
        int intValue = DecodeInt(r, g, b);
        return intValue / 100000f;
    }

    /// <summary>
    /// Encodes a string (up to 3 ASCII characters) to RGB values.
    /// </summary>
    /// <param name="text">The text to encode (max 3 characters).</param>
    /// <returns>RGB values as a tuple (R, G, B).</returns>
    public static (byte r, byte g, byte b) EncodeString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return (0, 0, 0);

        int value = 0;
        int length = Math.Min(text.Length, 3);

        for (int i = 0; i < length; i++)
        {
            char c = text[i];
            value += c * (int)Math.Pow(100, 2 - i);
        }

        return EncodeInt(value);
    }

    /// <summary>
    /// Decodes RGB values back to a string (3 characters).
    /// </summary>
    public static string DecodeString(byte r, byte g, byte b)
    {
        int value = DecodeInt(r, g, b);

        char c1 = (char)(value / 10000);
        char c2 = (char)((value / 100) % 100);
        char c3 = (char)(value % 100);

        return $"{c1}{c2}{c3}".TrimEnd('\0');
    }

    /// <summary>
    /// Encodes a frame index (for config mode) to RGB values.
    /// Used when addon is in config mode to show frame positions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte r, byte g, byte b) EncodeFrameIndex(int frameIndex)
    {
        return EncodeInt(frameIndex);
    }
}

/// <summary>
/// Frame index mapping for important game data.
/// Matches the DataToColor addon frame assignments.
/// </summary>
public static class FrameIndices
{
    // Frame 0: Validation marker (always black)
    public const int ValidationFrame = 0;

    // Player position (float * 10)
    public const int PlayerX = 1;
    public const int PlayerY = 2;

    // Direction and map
    public const int Direction = 3;
    public const int UIMapId = 4;

    // Player info
    public const int PlayerLevel = 5;
    public const int CorpseX = 6;
    public const int CorpseY = 7;

    // Boolean bits cells
    public const int BitsCell1 = 8;
    public const int BitsCell2 = 9;

    // Health and power
    public const int HealthMax = 10;
    public const int HealthCurrent = 11;
    public const int PowerMax = 12;
    public const int PowerCurrent = 13;

    // Target info
    public const int TargetNamePart1 = 16;
    public const int TargetNamePart2 = 17;
    public const int TargetHealth = 18;

    // Action bar states
    public const int ActionBarStart = 25;
    public const int ActionBarCount = 10;

    // Money
    public const int MoneyCopper = 44;
    public const int MoneyGold = 45;

    // Race/Class/Version
    public const int RaceClassVersion = 46;

    // Experience
    public const int XPCurrent = 50;
    public const int XPMax = 51;

    // Casting
    public const int CastingSpellId = 53;
    public const int RemainingCastTime = 76;

    // GCD
    public const int GcdRemaining = 95;

    // Network latency
    public const int NetworkLatency = 96;

    // Loot
    public const int LootWindow = 97;

    // Boolean bits cell 3
    public const int BitsCell3 = 100;

    // Soft interact
    public const int SoftInteractGuidStart = 101;

    // Global time cell
    public const int GlobalTime = 322;

    // Validation cell
    public const int Validation = 323;
}

/// <summary>
/// Addon bit flags for boolean game states.
/// Matches the AddonBits implementation in Core.
/// </summary>
public static class AddonBitFlags
{
    // Cell 8 (BitsCell1) - 24 bits
    public const int TargetCombat = 0;
    public const int TargetDead = 1;
    public const int PlayerDead = 2;
    public const int TalentPoint = 3;
    public const int MouseOver = 4;
    public const int TargetHostile = 5;
    public const int HasPet = 6;
    public const int ItemsBroken = 9;
    public const int OnTaxi = 10;
    public const int Swimming = 11;
    public const int InCombat = 14;
    public const int HasTarget = 17;
    public const int Mounted = 18;
    public const int AutoAttack = 20;
    public const int TargetPlayer = 21;
    public const int Falling = 23;

    // Cell 9 (BitsCell2) - 24 bits
    public const int CorpseInRange = 25;
    public const int Indoors = 26;
    public const int Stealthed = 34;
    public const int AutoFollow = 43;
    public const int Flying = 45;
    public const int Moving = 46;
}
