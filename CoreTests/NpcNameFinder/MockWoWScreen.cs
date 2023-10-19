using Game;
using System;
using System.Drawing;

namespace CoreTests;

internal sealed class MockWoWScreen : IWowScreen
{
    public bool Enabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Bitmap Bitmap => throw new NotImplementedException();

    public Rectangle Rect => throw new NotImplementedException();

    public nint ProcessHwnd => throw new NotImplementedException();

    public Bitmap MiniMapBitmap => throw new NotImplementedException();

    public Rectangle MiniMapRect => throw new NotImplementedException();

    public bool EnablePostProcess { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

#pragma warning disable CS0414 // The field 'MockWoWScreen.OnScreenChanged' is assigned but its value is never used
#pragma warning disable CS8632 // The field 'MockWoWScreen.OnScreenChanged' is assigned but its value is never used
    public event Action OnScreenChanged;
#pragma warning restore CS8632 // The field 'MockWoWScreen.OnScreenChanged' is assigned but its value is never used
#pragma warning restore CS0414 // The field 'MockWoWScreen.OnScreenChanged' is assigned but its value is never used

    public void AddDrawAction(Action<Graphics> g)
    {
        throw new NotImplementedException();
    }

    public Bitmap GetBitmap(int width, int height)
    {
        throw new NotImplementedException();
    }

    public Color GetColorAt(Point point)
    {
        throw new NotImplementedException();
    }

    public void GetPosition(ref Point point)
    {
        throw new NotImplementedException();
    }

    public void GetRectangle(out Rectangle rect)
    {
        throw new NotImplementedException();
    }

    public void Update() { }

    public void UpdateMinimapBitmap() { }

    public void Dispose()
    {
    }

    public void DrawBitmapTo(Graphics g)
    {
        throw new NotImplementedException();
    }

    public void PostProcess()
    {
        throw new NotImplementedException();
    }
}
