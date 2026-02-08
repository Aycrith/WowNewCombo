using Core;
using Game;
using MockWoWClient.Contracts;
using MockWoWClient.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Integration test that validates MockWowScreen can act as both IWowScreen and IAddonDataProvider.
/// This bridges the gap between the mock client and the real bot's screen reading interfaces.
/// </summary>
public sealed class BotIntegrationTest : TestScenarioBase
{
    private MockWowScreenAddonDataProvider _screenProvider = null!;

    public override string ScenarioName => "Bot Integration";
    public override string ScenarioDescription => "Validates MockWowScreen implements both IWowScreen and IAddonDataProvider for bot integration";

    public BotIntegrationTest(ITestOutputHelper output) : base(output)
    {
    }

    public override Task InitializeAsync()
    {
        base.InitializeAsync();

        // Create the screen provider that implements both interfaces
        _screenProvider = new MockWowScreenAddonDataProvider(MockClient);

        return Task.CompletedTask;
    }

    public override async Task DisposeAsync()
    {
        _screenProvider?.Dispose();
        await base.DisposeAsync();
    }

    [Fact]
    public void Implements_BothInterfaces()
    {
        // Assert
        Assert.IsAssignableFrom<IWowScreen>(_screenProvider);
        Assert.IsAssignableFrom<IAddonDataProvider>(_screenProvider);
        Assert.IsAssignableFrom<IDisposable>(_screenProvider);
    }

    [Fact]
    public void IWowScreen_Properties_ReturnCorrectValues()
    {
        // Act & Assert
        Assert.True(_screenProvider.Enabled);
        Assert.True(_screenProvider.MinimapEnabled);
        Assert.False(_screenProvider.EnablePostProcess);

        // ScreenRect should match renderer dimensions
        var screenRect = _screenProvider.ScreenRect;
        Assert.Equal(MockClient.Renderer.Width, screenRect.Width);
        Assert.Equal(MockClient.Renderer.Height, screenRect.Height);
    }

    [Fact]
    public void Update_CapturesScreenImage()
    {
        // Act
        _screenProvider.Update();

        // Assert
        Assert.NotNull(_screenProvider.ScreenImage);
        Assert.Equal(MockClient.Renderer.Width, _screenProvider.ScreenImage.Width);
        Assert.Equal(MockClient.Renderer.Height, _screenProvider.ScreenImage.Height);
        
        // Verify image has actual pixel data (not empty)
        var pixel = _screenProvider.ScreenImage[0, 0];
        // Frame 0 should be black (0, 0, 0)
        Assert.Equal(0, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
    }

    [Fact]
    public void Update_TriggersOnChangedEvent()
    {
        // Arrange
        bool eventFired = false;
        _screenProvider.OnChanged += () => eventFired = true;

        // Act
        _screenProvider.Update();

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void WaitForUpdate_WithRetries_Succeeds()
    {
        // Act
        bool success = _screenProvider.WaitForUpdate(maxAttempts: 5, delayMs: 10);

        // Assert
        Assert.True(success);
        Assert.NotNull(_screenProvider.ScreenImage);
    }

    [Fact]
    public void IAddonDataProvider_ImplementsAllMethods()
    {
        // Arrange
        var frames = CreateDataFrames();
        _screenProvider.InitFrames(frames);

        // Set up game state with known values BEFORE calling Update
        const int expectedHealthMax = 500;
        MockClient.GameState.Player.HealthMax = expectedHealthMax;
        MockClient.GameState.Player.Health = 350;

        // Update the screen (this will call FrameMapper.UpdateFrames())
        _screenProvider.Update();

        // Act - UpdateData should not throw
        _screenProvider.UpdateData();

        // Assert - Data array should exist with correct size
        Assert.NotNull(_screenProvider.Data);
        Assert.Equal(324, _screenProvider.Data.Length);

        // Assert - Validation frames should have correct values
        Assert.Equal(0, _screenProvider.Data[0]); // Frame 0 is always black
        Assert.Equal(2000001, _screenProvider.Data[323]); // Frame 323 is validation marker

        // Assert - Our set value should be correctly decoded
        Assert.Equal(expectedHealthMax, _screenProvider.Data[10]);

        // Assert - GetInt should return correct values
        var intVal = _screenProvider.GetInt(0);
        Assert.Equal(0, intVal); // Frame 0 is black

        var healthVal = _screenProvider.GetInt(10);
        Assert.Equal(expectedHealthMax, healthVal);

        var validationVal = _screenProvider.GetInt(323);
        Assert.Equal(2000001, validationVal);

        // Assert - GetFixed should return correct float values
        var fixedVal = _screenProvider.GetFixed(1);
        // Frame 1 should have been set by FrameMapper based on game state
        Assert.True(fixedVal >= 0f); // Should be a valid float

        // Assert - GetString should return a string (or empty if no data)
        var stringVal = _screenProvider.GetString(16);
        Assert.NotNull(stringVal); // Should not be null
    }

    [Fact]
    public void DataDecoding_RoundTripsGameStateValues()
    {
        // This test validates that game state values encode/decode correctly
        // It sets known values on the game state, renders them to pixels,
        // and verifies the decoded values match the originals.

        // Arrange
        var frames = CreateDataFrames();
        _screenProvider.InitFrames(frames);

        // Set up game state with known values BEFORE calling Update
        const int expectedHealthMax = 500;
        const int expectedHealthCurrent = 350;
        const int expectedPowerMax = 200;
        const int expectedPowerCurrent = 150;
        const int expectedLevel = 25;
        const int expectedGold = 123;
        const int expectedCopper = 4567;

        // Set values on the game state (FrameMapper will read these and set renderer)
        MockClient.GameState.Player.HealthMax = expectedHealthMax;
        MockClient.GameState.Player.Health = expectedHealthCurrent;
        MockClient.GameState.Player.PowerMax = expectedPowerMax;
        MockClient.GameState.Player.Power = expectedPowerCurrent;
        MockClient.GameState.Player.Level = expectedLevel;
        MockClient.GameState.Player.Copper = expectedGold * 10000 + expectedCopper; // Total copper

        // Act - Update triggers FrameMapper which reads game state and sets renderer
        _screenProvider.Update();
        _screenProvider.UpdateData();

        // Assert - Verify validation frames
        Assert.Equal(0, _screenProvider.Data[0]);
        Assert.Equal(2000001, _screenProvider.Data[323]);

        // Assert - Verify game state values round-tripped correctly
        Assert.Equal(expectedHealthMax, _screenProvider.Data[10]);
        Assert.Equal(expectedHealthCurrent, _screenProvider.Data[11]);
        Assert.Equal(expectedPowerMax, _screenProvider.Data[12]);
        Assert.Equal(expectedPowerCurrent, _screenProvider.Data[13]);
        Assert.Equal(expectedLevel, _screenProvider.Data[5]);
        Assert.Equal(expectedCopper, _screenProvider.Data[44]); // Copper portion
        Assert.Equal(expectedGold, _screenProvider.Data[45]);

        // Assert - Verify via GetInt method
        Assert.Equal(expectedHealthMax, _screenProvider.GetInt(10));
        Assert.Equal(expectedHealthCurrent, _screenProvider.GetInt(11));
        Assert.Equal(expectedPowerMax, _screenProvider.GetInt(12));
        Assert.Equal(expectedPowerCurrent, _screenProvider.GetInt(13));
        Assert.Equal(expectedLevel, _screenProvider.GetInt(5));
    }

    /// <summary>
    /// Creates the standard 324 DataFrames with correct pixel positions.
    /// Matches the PixelGridRenderer layout exactly.
    /// </summary>
    private static DataFrame[] CreateDataFrames()
    {
        var frames = new DataFrame[324];
        const int rows = 50;
        const int cellSize = 4;

        for (int i = 0; i < 324; i++)
        {
            // Frame positions match PixelGridRenderer layout (top-left of each cell)
            int row = i % rows;      // 0-49
            int col = i / rows;      // 0-6 (7 columns for 324 frames)
            
            // Pixel coordinates (top-left corner of 4x4 cell)
            int x = col * cellSize;
            int y = row * cellSize;
            
            frames[i] = new DataFrame(i, x, y);
        }
        return frames;
    }

    /// <summary>
    /// Bridges MockWowScreen with IAddonDataProvider, implementing both interfaces
    /// like the real WowScreenDXGI does.
    /// </summary>
    private sealed class MockWowScreenAddonDataProvider : IWowScreen, IAddonDataProvider
    {
        private readonly global::MockWoWClient.MockWoWClient _client;
        private readonly MockWowScreen _screen;
        private DataFrame[] _frames = [];
        private Image<Bgra32>? _addonImage;

        public int[] Data { get; private set; } = [];

        // IWowScreen properties
        public bool Enabled { get; set; } = true;
        public bool MinimapEnabled { get; set; } = true;
        public bool EnablePostProcess { get; set; }
        public Image<Bgra32> ScreenImage => _screen.ScreenImage;
        public Rectangle ScreenRect => _screen.ScreenRect;
        public Image<Bgra32> MiniMapImage => _screen.MiniMapImage;
        public Rectangle MiniMapRect => _screen.MiniMapRect;
        public event Action? OnChanged
        {
            add => _screen.OnChanged += value;
            remove => _screen.OnChanged -= value;
        }

        public MockWowScreenAddonDataProvider(global::MockWoWClient.MockWoWClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _screen = new MockWowScreen(client);
        }

        public void Update()
        {
            if (!Enabled) return;

            // Update game state first
            _client.FrameMapper.UpdateFrames();

            // Capture screen
            _screen.Update();

            // Update the addon image from screen
            UpdateAddonImage();
        }

        public bool WaitForUpdate(int maxAttempts = 10, int delayMs = 50)
        {
            return _screen.WaitForUpdate(maxAttempts, delayMs);
        }

        public void PostProcess()
        {
            _screen.PostProcess();
        }

        public void GetPosition(ref Point point)
        {
            _screen.GetPosition(ref point);
        }

        public void GetRectangle(out Rectangle rect)
        {
            _screen.GetRectangle(out rect);
        }

        public void InitFrames(DataFrame[] frames)
        {
            _frames = frames;
            Data = new int[frames.Length];

            // Calculate addon image size based on frame positions
            int maxX = 0, maxY = 0;
            foreach (var frame in frames)
            {
                maxX = Math.Max(maxX, frame.X);
                maxY = Math.Max(maxY, frame.Y);
            }

            // Ensure minimum size and add margin for cell size
            _addonImage = new Image<Bgra32>(maxX + 4, maxY + 4);
        }

    public void UpdateData()
    {
        if (_frames.Length <= 2 || _addonImage == null) return;

        // Update the addon image from current screen
        UpdateAddonImage();

        // Decode pixel data using the same method as real implementation
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

            // Copy relevant portion of screen to addon image
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
    }
}
