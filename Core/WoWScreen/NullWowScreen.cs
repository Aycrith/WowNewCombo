using Game;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using System;

namespace Core;

/// <summary>
/// A null implementation of IWowScreen for use when WoW is not running.
/// This allows the bot UI to load in configuration mode.
/// </summary>
public sealed class NullWowScreen : IWowScreen
{
    private static readonly Rectangle EmptyRect = new(0, 0, 1, 1);
    
    public bool Enabled { get; set; }
    public bool MinimapEnabled { get; set; }
    public bool EnablePostProcess { get; set; }
    
    public Image<Bgra32> ScreenImage { get; }
    public Rectangle ScreenRect => EmptyRect;
    
    public Image<Bgra32> MiniMapImage { get; }
    public Rectangle MiniMapRect => EmptyRect;

#pragma warning disable CS0067 // Event is never used - required by IWowScreen interface
    public event Action? OnChanged;
#pragma warning restore CS0067

    public NullWowScreen()
    {
        // Create minimal 1x1 images to satisfy the interface
        ScreenImage = new Image<Bgra32>(1, 1);
        MiniMapImage = new Image<Bgra32>(1, 1);
    }

    public void GetPosition(ref Point point)
    {
        point = new Point(0, 0);
    }

    public void GetRectangle(out Rectangle rect)
    {
        rect = EmptyRect;
    }

    public void PostProcess()
    {
        // No-op in configuration mode
    }

    public void Update()
    {
        // No-op in configuration mode
    }

    public bool WaitForUpdate(int maxAttempts = 10, int delayMs = 50)
    {
        // No-op in configuration mode - always return false
        return false;
    }

    public void Dispose()
    {
        ScreenImage?.Dispose();
        MiniMapImage?.Dispose();
    }
}
