using System.Drawing;

namespace SharedLib.NpcFinder
{
    public readonly struct NpcPosition
    {
        public readonly int Left => Rect.Left;
        public readonly int Top => Rect.Top;
        public readonly int Height => Rect.Height;
        public readonly int Width => Rect.Width;

        public readonly Rectangle Rect;
        public readonly Point ClickPoint;

        public readonly bool IsAdd;

        public readonly int screenMid;
        public readonly int screenTargetBuffer;
        public readonly int screenMidBuffer;
        public readonly int screenAddBuffer;

        public NpcPosition(Point min, Point max, int screenWidth, float yOffset, float heightMul)
        {
            Rect = new(min.X, min.Y, max.X - min.X, max.Y - min.Y);

            screenMid = screenWidth / 2;
            screenMidBuffer = screenWidth / 15;
            screenTargetBuffer = screenMidBuffer / 2;
            screenAddBuffer = screenMidBuffer * 3;

            ClickPoint = new(min.X + (Rect.Width / 2), (int)(max.Y + yOffset + (Rect.Height * heightMul)));

            IsAdd = (ClickPoint.X < screenMid - screenTargetBuffer && ClickPoint.X > screenMid - screenAddBuffer) ||
            (ClickPoint.X > screenMid + screenTargetBuffer && ClickPoint.X < screenMid + screenAddBuffer);
        }
    }
}