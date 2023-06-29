using Game;
using System;
using System.Drawing;

namespace CoreTests;

public class MockWoWScreen : IWowScreen
{
    public bool Enabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Bitmap Bitmap => throw new NotImplementedException();

    public Rectangle Rect => throw new NotImplementedException();

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
}
