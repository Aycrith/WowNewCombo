using Game;
using System;
using System.Drawing;

namespace CoreTests
{
    public class MockWoWScreen : IWowScreen
    {
        public bool Enabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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
}
