using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Game;
using SharedLib;

namespace MockWoWClient.Rendering;

/// <summary>
/// Implements IWowScreen for the MockWoWClient.
/// This allows the bot to read from the simulated client instead of real WoW.
/// </summary>
public sealed class MockWowScreen : IWowScreen
{
    private readonly MockWoWClient _client;
    private Image<Bgra32>? _lastScreenImage;
    private bool _enabled = true;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool MinimapEnabled { get; set; } = true;
    public bool EnablePostProcess { get; set; }

    public Image<Bgra32> ScreenImage => _lastScreenImage ?? throw new InvalidOperationException("Screen not updated yet. Call Update() first.");

    public Rectangle ScreenRect => new(0, 0, _client.Renderer.Width, _client.Renderer.Height);

    public Image<Bgra32> MiniMapImage => ScreenImage; // For simplicity, return same image

    public Rectangle MiniMapRect => new(0, 0, 100, 100); // Placeholder

    public event Action? OnChanged;

    public MockWowScreen(MockWoWClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public void Update()
    {
        if (!Enabled)
            return;

        try
        {
            // Capture screen from MockWoWClient
            var image = _client.CaptureScreen();

            // Dispose old image if exists
            _lastScreenImage?.Dispose();
            _lastScreenImage = image;

            // Notify subscribers
            OnChanged?.Invoke();
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Error updating screen: {ex.Message}");
        }
    }

    public void PostProcess()
    {
        // No post-processing needed for mock
    }

    public bool WaitForUpdate(int maxAttempts = 10, int delayMs = 50)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Update();

            if (_lastScreenImage != null)
            {
                return true;
            }

            Thread.Sleep(delayMs);
        }

        return false;
    }

    public void GetPosition(ref Point point)
    {
        // Mock client always at origin
        point = new Point(0, 0);
    }

    public void GetRectangle(out Rectangle rect)
    {
        rect = ScreenRect;
    }

    public void Dispose()
    {
        _lastScreenImage?.Dispose();
    }
}
