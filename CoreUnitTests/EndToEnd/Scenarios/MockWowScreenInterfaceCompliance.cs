using Game;
using MockWoWClient;
using MockWoWClient.Rendering;
using SharedLib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Verifies that MockWowScreen properly implements all required interfaces.
/// </summary>
public sealed class MockWowScreenInterfaceCompliance : TestScenarioBase
{
    private MockWowScreen _screen = null!;

    public override string ScenarioName => "MockWowScreen Interface Compliance";
    public override string ScenarioDescription => "Verifies MockWowScreen implements IWowScreen, IRectProvider, IScreenImageProvider, IMinimapImageProvider";

    public MockWowScreenInterfaceCompliance(ITestOutputHelper output) : base(output)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _screen = new MockWowScreen(MockClient);
    }

    [Fact]
    public void Implements_IWowScreen()
    {
        // Assert
        Assert.IsAssignableFrom<IWowScreen>(_screen);
        Assert.IsAssignableFrom<IRectProvider>(_screen);
        Assert.IsAssignableFrom<IScreenImageProvider>(_screen);
        Assert.IsAssignableFrom<IMinimapImageProvider>(_screen);
        Assert.IsAssignableFrom<IDisposable>(_screen);
    }

    [Fact]
    public void IWowScreen_Properties_ShouldWork()
    {
        // Test Enabled property
        Assert.True(_screen.Enabled);
        _screen.Enabled = false;
        Assert.False(_screen.Enabled);
        _screen.Enabled = true;

        // Test MinimapEnabled property
        Assert.True(_screen.MinimapEnabled);
        _screen.MinimapEnabled = false;
        Assert.False(_screen.MinimapEnabled);

        // Test EnablePostProcess property
        Assert.False(_screen.EnablePostProcess);
        _screen.EnablePostProcess = true;
        Assert.True(_screen.EnablePostProcess);
    }

    [Fact]
    public void IRectProvider_GetPosition_ShouldReturnOrigin()
    {
        // Act
        var point = new Point(100, 100); // Start with non-zero
        _screen.GetPosition(ref point);

        // Assert
        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
    }

    [Fact]
    public void IRectProvider_GetRectangle_ShouldMatchRendererSize()
    {
        // Act
        _screen.GetRectangle(out var rect);

        // Assert
        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(MockClient.Renderer.Width, rect.Width);
        Assert.Equal(MockClient.Renderer.Height, rect.Height);
    }

    [Fact]
    public void IScreenImageProvider_Properties_ShouldWork()
    {
        // ScreenRect property should return the correct dimensions
        var screenRect = _screen.ScreenRect;
        Assert.Equal(MockClient.Renderer.Width, screenRect.Width);
        Assert.Equal(MockClient.Renderer.Height, screenRect.Height);

        // ScreenImage should throw before first update
        Assert.Throws<InvalidOperationException>(() => _screen.ScreenImage);
    }

    [Fact]
    public void IMinimapImageProvider_Properties_ShouldWork()
    {
        // MiniMapRect should have placeholder values
        var miniMapRect = _screen.MiniMapRect;
        Assert.True(miniMapRect.Width > 0);
        Assert.True(miniMapRect.Height > 0);

        // MiniMapImage should throw before first update
        Assert.Throws<InvalidOperationException>(() => _screen.MiniMapImage);
    }

    [Fact]
    public void Update_ShouldCaptureScreenImage()
    {
        // Act
        _screen.Update();

        // Assert - should no longer throw
        var image = _screen.ScreenImage;
        Assert.NotNull(image);
        Assert.Equal(MockClient.Renderer.Width, image.Width);
        Assert.Equal(MockClient.Renderer.Height, image.Height);
    }

    [Fact]
    public void WaitForUpdate_ShouldSucceed()
    {
        // Act
        bool result = _screen.WaitForUpdate(maxAttempts: 10, delayMs: 10);

        // Assert
        Assert.True(result);
        Assert.NotNull(_screen.ScreenImage);
    }

    [Fact]
    public void OnChanged_Event_ShouldFire()
    {
        // Arrange
        bool eventFired = false;
        _screen.OnChanged += () => eventFired = true;

        // Act
        _screen.Update();

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void PostProcess_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        _screen.PostProcess();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        _screen.Update();
        var imageBefore = _screen.ScreenImage;
        Assert.NotNull(imageBefore);

        // Act & Assert - should complete without exception
        _screen.Dispose();
    }

    [Fact]
    public void FullWorkflow_SimulateBotUsage()
    {
        // This test simulates how the real bot uses IWowScreen

        // Step 1: Bot checks if screen is enabled
        Assert.True(_screen.Enabled);

        // Step 2: Bot waits for update
        bool updated = _screen.WaitForUpdate(maxAttempts: 5, delayMs: 10);
        Assert.True(updated);

        // Step 3: Bot reads screen dimensions
        _screen.GetRectangle(out var rect);
        Assert.True(rect.Width > 0);
        Assert.True(rect.Height > 0);

        // Step 4: Bot accesses screen image
        var image = _screen.ScreenImage;
        Assert.NotNull(image);

        // Step 5: Bot processes the image (simulated)
        _screen.PostProcess();

        // Step 6: Bot listens for changes
        int changeCount = 0;
        _screen.OnChanged += () => changeCount++;

        // Update multiple times
        _screen.Update();
        _screen.Update();

        Assert.Equal(2, changeCount);
    }
}
