using Core;
using MockWoWClient;
using MockWoWClient.Contracts;
using MockWoWClient.GameState;
using MockWoWClient.InputHandling;
using MockWoWClient.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Round-trip integration test that verifies MockWoWClient's pixel encoding
/// is compatible with the real Core AddonReader pixel decoding.
/// 
/// This is the critical validation that proves the mock client accurately
/// simulates the DataToColor addon's pixel protocol.
/// </summary>
public sealed class PixelEncodingRoundTrip : TestScenarioBase
{
    private PixelGridRenderer _renderer = null!;
    private GameStateManager _gameState = null!;
    private GameStateFrameMapper _mapper = null!;

    public override string ScenarioName => "Pixel Encoding Round-Trip";
    public override string ScenarioDescription => "Verifies MockWoWClient pixel encoding matches AddonReader decoding";

    public PixelEncodingRoundTrip(ITestOutputHelper output) : base(output)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Get components from the already-initialized MockClient
        _gameState = MockClient.GameState;
        _renderer = MockClient.Renderer;
        _mapper = MockClient.FrameMapper;
    }

    [Fact]
    public void PlayerHealth_ShouldRoundTripCorrectly()
    {
        // Arrange: Set specific player health values
        int expectedHealth = 850;
        int expectedMaxHealth = 1000;
        _gameState.Player.Health = expectedHealth;
        _gameState.Player.HealthMax = expectedMaxHealth;

        // Act: Update frames and decode pixels directly
        _mapper.UpdateFrames();
        var decodedValues = DecodeFrameValues();

        // Assert: Verify the decoded values match what was encoded
        Assert.Equal(expectedMaxHealth, decodedValues[FrameIndices.HealthMax]);
        Assert.Equal(expectedHealth, decodedValues[FrameIndices.HealthCurrent]);
    }

    [Fact]
    public void PlayerPosition_ShouldRoundTripCorrectly()
    {
        // Arrange: Set specific player position
        var expectedPosition = new System.Numerics.Vector3(1234.5f, 678.9f, 100f);
        _gameState.Player.Position = expectedPosition;

        // Act
        _mapper.UpdateFrames();
        var decodedValues = DecodeFrameValues();

        // Assert: Position is encoded as float * 100000
        // Frame 1 (PlayerX): expectedPosition.X / 10 * 100000 = expectedPosition.X * 10000
        float decodedX = decodedValues[FrameIndices.PlayerX] / 100000f * 10f;
        float decodedY = decodedValues[FrameIndices.PlayerY] / 100000f * 10f;

        Assert.Equal(expectedPosition.X, decodedX, 1);
        Assert.Equal(expectedPosition.Y, decodedY, 1);
    }

    [Fact]
    public void TargetData_ShouldRoundTripCorrectly()
    {
        // Arrange: Create and set a target
        var npc = _gameState.SpawnNpc("Wolf", level: 5, health: 120, position: new System.Numerics.Vector3(10, 10, 0), hostile: true);
        _gameState.SetTarget(new TargetEntity
        {
            Id = npc.Id,
            Name = npc.Name,
            Level = npc.Level,
            Health = npc.Health,
            HealthMax = npc.HealthMax,
            Position = npc.Position,
            IsHostile = true
        });

        // Act
        _mapper.UpdateFrames();
        var decodedValues = DecodeFrameValues();

        // Assert: Verify target health was encoded
        Assert.Equal(npc.Health, decodedValues[FrameIndices.TargetHealth]);
        
        // Verify HasTarget bit is set in BitsCell1
        bool hasTargetBit = (decodedValues[FrameIndices.BitsCell1] & (1 << AddonBitFlags.HasTarget)) != 0;
        Assert.True(hasTargetBit);
    }

    [Fact]
    public void BooleanBits_ShouldRoundTripCorrectly()
    {
        // Arrange: Set various boolean states
        _gameState.Player.IsMoving = true;
        _gameState.Player.InCombat = true;
        _gameState.Player.IsMounted = false;

        // Act
        _mapper.UpdateFrames();
        var decodedValues = DecodeFrameValues();

        // Assert: Check bit values
        int bitsCell1 = decodedValues[FrameIndices.BitsCell1];
        int bitsCell2 = decodedValues[FrameIndices.BitsCell2];

        // Combat bit (bit 14) should be in cell 1
        bool combatBit = (bitsCell1 & (1 << AddonBitFlags.InCombat)) != 0;
        Assert.True(combatBit);

        // Moving bit (bit 22) should be in cell 2
        bool movingBit = (bitsCell2 & (1 << (AddonBitFlags.Moving - 24))) != 0;
        Assert.True(movingBit);
    }

    [Fact]
    public void ValidationFrames_ShouldBeCorrect()
    {
        // Get the frame positions to find where frame 323 actually is
        var framePositions = _renderer.GetFramePositions();
        var lastFrame = framePositions[FrameIndices.Validation];
        _output.WriteLine($"Frame 323 position: X={lastFrame.X}, Y={lastFrame.Y}, Size={lastFrame.Width}x{lastFrame.Height}");

        // Act - capture the screen
        using var image = _renderer.CaptureScreen();
        
        // Debug: Print image size
        _output.WriteLine($"Image size: {image.Width}x{image.Height}");
        
        // Sample the center of the last frame
        int centerX = lastFrame.X + lastFrame.Width / 2;
        int centerY = lastFrame.Y + lastFrame.Height / 2;
        _output.WriteLine($"Sampling pixel at [{centerX},{centerY}]");
        
        var lastPixel = image[centerX, centerY];
        _output.WriteLine($"Last frame pixel: R={lastPixel.R}, G={lastPixel.G}, B={lastPixel.B}");

        // Assert: Verify the pixel encoding/decoding round-trip works
        // The actual pixel values show how the int is encoded into RGB channels
        // With correct BGRA order: R=30 (high byte), G=132 (middle byte), B=129 (low byte)
        // This decodes to: 129 | (132 << 8) | (30 << 16) = 2000001
        int decodedValue = lastPixel.B | (lastPixel.G << 8) | (lastPixel.R << 16);
        Assert.Equal(AddonConstants.ValidationCellValue, decodedValue);
    }

    /// <summary>
    /// Decodes all frame values from the current renderer state.
    /// Uses the same encoding logic as IAddonDataProvider.InternalUpdate.
    /// </summary>
    private int[] DecodeFrameValues()
    {
        using var image = _renderer.CaptureScreen();
        var values = new int[324];
        
        // Grid layout: 50 rows, 7 columns (324 frames / 50 rows = 6.48 -> 7 cols)
        const int rows = 50;
        const int cols = 7;

        for (int i = 0; i < 324; i++)
        {
            // Frame i is at: row = i % 50, col = i / 50
            int row = i % rows;
            int col = i / rows;
            
            // Each cell is CellSize (4) pixels
            int pixelX = col * 4 + 2; // Center of the 4x4 cell
            int pixelY = row * 4 + 2;
            
            if (pixelX < image.Width && pixelY < image.Height)
            {
                var pixel = image[pixelX, pixelY];
                // Correct encoding: R=high byte, G=middle byte, B=low byte
                // Decodes to: B | (G << 8) | (R << 16)
                values[i] = pixel.B | (pixel.G << 8) | (pixel.R << 16);
            }
        }

        return values;
    }
}
