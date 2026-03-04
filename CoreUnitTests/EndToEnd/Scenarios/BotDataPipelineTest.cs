using Core;
using Core.Database;
using Game;
using Microsoft.Extensions.DependencyInjection;
using MockWoWClient;
using MockWoWClient.Contracts;
using MockWoWClient.GameState;
using MockWoWClient.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Phase 2: Full bot data pipeline integration test.
/// Validates that MockWoWClient game state flows through the complete pipeline:
/// MockWoWClient → IAddonDataProvider → AddonReader → PlayerReader
/// </summary>
public sealed class BotDataPipelineTest : TestScenarioBase
{
    private MockWowScreenAddonDataProvider _screenProvider = null!;

    public override string ScenarioName => "Bot Data Pipeline";
    public override string ScenarioDescription => "Validates full game state pipeline from MockWoWClient through AddonReader to PlayerReader";

    public BotDataPipelineTest(ITestOutputHelper output) : base(output) { }

    public override Task InitializeAsync()
    {
        base.InitializeAsync();
        _screenProvider = new MockWowScreenAddonDataProvider(MockClient);
        return Task.CompletedTask;
    }

    public override async Task DisposeAsync()
    {
        _screenProvider?.Dispose();
        await base.DisposeAsync();
    }

    [Fact]
    public void PlayerReader_Health_ShouldMatchGameState()
    {
        // Arrange: Set specific health values on the player
        const int expectedHealth = 350;
        const int expectedMaxHealth = 500;

        MockClient.GameState.Player.Health = expectedHealth;
        MockClient.GameState.Player.HealthMax = expectedMaxHealth;

        // Act: Update screen and decode data
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert: Direct frame access
        Assert.Equal(expectedMaxHealth, _screenProvider.GetInt(10)); // HealthMax frame
        Assert.Equal(expectedHealth, _screenProvider.GetInt(11));    // HealthCurrent frame
    }

    [Fact]
    public void PlayerReader_Position_ShouldMatchGameState()
    {
        // Arrange: Set specific position
        var expectedPosition = new Vector3(1234.5f, 678.9f, 100f);
        MockClient.GameState.Player.Position = expectedPosition;

        // Act
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert: Position is encoded as (value / 10) * 100000
        // Frame 1 (X): expectedPosition.X / 10 * 100000
        float expectedXEncoded = (expectedPosition.X / 10f) * 100000f;
        float expectedYEncoded = (expectedPosition.Y / 10f) * 100000f;

        var actualX = _screenProvider.GetInt(1); // PlayerX frame
        var actualY = _screenProvider.GetInt(2); // PlayerY frame

        Assert.Equal((int)expectedXEncoded, actualX);
        Assert.Equal((int)expectedYEncoded, actualY);
    }

    [Fact]
    public void PlayerReader_Level_ShouldMatchGameState()
    {
        // Arrange
        const int expectedLevel = 25;
        MockClient.GameState.Player.Level = expectedLevel;

        // Act
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert
        Assert.Equal(expectedLevel, _screenProvider.GetInt(5)); // PlayerLevel frame
    }

    [Fact]
    public void TargetData_ShouldMatchGameState()
    {
        // Arrange: Create and target an NPC
        var npc = MockClient.GameState.SpawnNpc("Test Wolf", 5, 200, new Vector3(10, 10, 0), hostile: true);
        MockClient.GameState.SetTarget(new TargetEntity
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
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert: Target health
        Assert.Equal(npc.Health, _screenProvider.GetInt(18)); // TargetHealth frame

        // Assert: HasTarget bit is set
        var bitsCell1 = _screenProvider.GetInt(8); // BitsCell1
        bool hasTargetBit = (bitsCell1 & (1 << AddonBitFlags.HasTarget)) != 0;
        Assert.True(hasTargetBit, "HasTarget bit should be set when targeting an NPC");
    }

    [Fact]
    public void CombatState_ShouldUpdateBits()
    {
        // Arrange: Set combat state
        MockClient.GameState.Player.InCombat = true;
        MockClient.GameState.Player.IsMoving = true;
        MockClient.GameState.Player.IsMounted = false;

        // Act
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert: Check BitsCell1 for combat state
        var bitsCell1 = _screenProvider.GetInt(8);
        bool inCombatBit = (bitsCell1 & (1 << AddonBitFlags.InCombat)) != 0;
        Assert.True(inCombatBit, "InCombat bit should be set");

        // Assert: Check BitsCell2 for movement
        var bitsCell2 = _screenProvider.GetInt(9);
        bool isMovingBit = (bitsCell2 & (1 << (AddonBitFlags.Moving - 24))) != 0;
        Assert.True(isMovingBit, "IsMoving bit should be set");

        // Assert: Mounted bit should not be set
        bool isMountedBit = (bitsCell2 & (1 << (AddonBitFlags.Mounted - 24))) != 0;
        Assert.False(isMountedBit, "IsMounted bit should not be set");
    }

    [Fact]
    public void FullGameState_RoundTripsAllValues()
    {
        // Arrange: Set comprehensive game state
        MockClient.GameState.Player.Level = 30;
        MockClient.GameState.Player.Health = 750;
        MockClient.GameState.Player.HealthMax = 1000;
        MockClient.GameState.Player.Power = 250;
        MockClient.GameState.Player.PowerMax = 300;
        MockClient.GameState.Player.Copper = 1234567; // 123 gold, 45 silver, 67 copper
        MockClient.GameState.Player.Position = new Vector3(1200f, 1300f, 50f); // Must be < 1677.72 to fit in 24 bits
        MockClient.GameState.Player.Direction = 45f;
        MockClient.GameState.Player.InCombat = true;
        MockClient.GameState.Player.IsMoving = false;

        var npc = MockClient.GameState.SpawnNpc("Dungeon Boss", 20, 5000, new Vector3(20, 20, 0), hostile: true);
        MockClient.GameState.SetTarget(new TargetEntity
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
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert: All values round-trip correctly
        Assert.True(30 == _screenProvider.GetInt(5), "Level should match");
        Assert.True(1000 == _screenProvider.GetInt(10), "HealthMax should match");
        Assert.True(750 == _screenProvider.GetInt(11), "Health should match");
        Assert.True(300 == _screenProvider.GetInt(12), "PowerMax should match");
        Assert.True(250 == _screenProvider.GetInt(13), "Power should match");
        Assert.True(4567 == _screenProvider.GetInt(44), "Copper should match (remainder)");
        Assert.True(123 == _screenProvider.GetInt(45), "Gold should match");
        Assert.True(5000 == _screenProvider.GetInt(18), "TargetHealth should match");

        // Assert: Position
        int expectedX = (int)((1200f / 10f) * 100000f);
        int expectedY = (int)((1300f / 10f) * 100000f);
        Assert.True(expectedX == _screenProvider.GetInt(1), "PlayerX should match");
        Assert.True(expectedY == _screenProvider.GetInt(2), "PlayerY should match");

        // Assert: Bits
        var bitsCell1 = _screenProvider.GetInt(8);
        Assert.True((bitsCell1 & (1 << AddonBitFlags.InCombat)) != 0, "InCombat bit should be set");
        Assert.True((bitsCell1 & (1 << AddonBitFlags.HasTarget)) != 0, "HasTarget bit should be set");

        var bitsCell2 = _screenProvider.GetInt(9);
        Assert.False((bitsCell2 & (1 << (AddonBitFlags.Moving - 24))) != 0, "IsMoving bit should not be set");
    }

    [Fact]
    public void GlobalTime_ShouldUpdateEachFrame()
    {
        // Arrange
        var initialTick = MockClient.Renderer.GlobalTick;

        // Act: Update multiple times
        _screenProvider.Update();
        _screenProvider.UpdateData();
        var tick1 = MockClient.Renderer.GlobalTick;

        // Simulate time passing
        MockClient.Advance(TimeSpan.FromMilliseconds(100));

        _screenProvider.Update();
        _screenProvider.UpdateData();
        var tick2 = MockClient.Renderer.GlobalTick;

        // Assert: GlobalTick should have incremented
        Assert.True(tick2 > tick1, "GlobalTick should increment over time");
    }

    /// <summary>
    /// Mock implementation that bridges MockWoWClient with IAddonDataProvider
    /// </summary>
    private sealed class MockWowScreenAddonDataProvider : IWowScreen, IAddonDataProvider
    {
        private readonly global::MockWoWClient.MockWoWClient _client;
        private readonly MockWowScreen _screen;
        private DataFrame[] _frames = [];
        private Image<Bgra32>? _addonImage;

        public int[] Data { get; private set; } = [];

        public bool Enabled { get; set; } = true;
        public bool MinimapEnabled { get; set; } = true;
        public bool EnablePostProcess { get; set; }
        public Image<Bgra32> ScreenImage => _screen.ScreenImage;
        public Rectangle ScreenRect => _screen.ScreenRect;
        public Image<Bgra32> MiniMapImage => _screen.MiniMapImage;
        public Rectangle MiniMapRect => _screen.MiniMapRect;
        public SharedLib.MinimapSettings MinimapSettings => _screen.MinimapSettings;
        public event Action? OnChanged
        {
            add => _screen.OnChanged += value;
            remove => _screen.OnChanged -= value;
        }

        public MockWowScreenAddonDataProvider(global::MockWoWClient.MockWoWClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _screen = new MockWowScreen(client);
            InitFrames(CreateDataFrames());
        }

        public void Update()
        {
            if (!Enabled) return;
            _client.FrameMapper.UpdateFrames();
            _screen.Update();
            UpdateAddonImage();
        }

        public bool WaitForUpdate(int maxAttempts = 10, int delayMs = 50)
        {
            return _screen.WaitForUpdate(maxAttempts, delayMs);
        }

        public void PostProcess() => _screen.PostProcess();

        public void GetPosition(ref Point point) => _screen.GetPosition(ref point);

        public void GetRectangle(out Rectangle rect) => _screen.GetRectangle(out rect);

        public void InitFrames(DataFrame[] frames)
        {
            _frames = frames;
            Data = new int[frames.Length];

            int maxX = 0, maxY = 0;
            foreach (var frame in frames)
            {
                maxX = Math.Max(maxX, frame.X);
                maxY = Math.Max(maxY, frame.Y);
            }
            _addonImage = new Image<Bgra32>(maxX + 4, maxY + 4);
        }

        public void UpdateData()
        {
            if (_frames.Length <= 2 || _addonImage == null) return;
            UpdateAddonImage();
            IAddonDataProvider.InternalUpdate(_addonImage, _frames.AsSpan(), Data.AsSpan());
        }

        public int GetInt(int index)
        {
            if ((uint)index >= (uint)Data.Length) return 0;
            return Data[index];
        }

        public float GetFixed(int index)
        {
            if ((uint)index >= (uint)Data.Length) return 0f;
            return Data[index] / 100000f;
        }

        public string GetString(int index)
        {
            if ((uint)index >= (uint)Data.Length) return string.Empty;
            int color = Data[index];
            if ((uint)color > 999999) return string.Empty;

            Span<char> buffer = stackalloc char[3];
            int count = 0;

            int n1 = color / 10000;
            int n2 = color / 100 % 100;
            int n3 = color % 100;

            if (n1 > 0) buffer[count++] = (char)n1;
            if (n2 > 0) buffer[count++] = (char)n2;
            if (n3 > 0) buffer[count++] = (char)n3;

            return buffer[..count].ToString();
        }

        private void UpdateAddonImage()
        {
            if (_addonImage == null) return;
            var screenImage = _screen.ScreenImage;
            int width = Math.Min(_addonImage.Width, screenImage.Width);
            int height = Math.Min(_addonImage.Height, screenImage.Height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _addonImage[x, y] = screenImage[x, y];
                }
            }
        }

        public void Dispose()
        {
            _addonImage?.Dispose();
            _screen.Dispose();
        }

        private static DataFrame[] CreateDataFrames()
        {
            var frames = new DataFrame[324];
            const int rows = 50;
            const int cellSize = 4;

            for (int i = 0; i < 324; i++)
            {
                int row = i % rows;
                int col = i / rows;
                int x = col * cellSize;
                int y = row * cellSize;
                frames[i] = new DataFrame(i, x, y);
            }
            return frames;
        }
    }
}
