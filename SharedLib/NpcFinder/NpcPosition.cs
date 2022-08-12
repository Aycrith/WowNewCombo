using System.Drawing;
using System;

namespace SharedLib.NpcFinder
{
    public readonly struct NpcPosition : IEquatable<NpcPosition>
    {
        public static readonly NpcPosition Empty = new(Point.Empty, Point.Empty, 0, 0);

        public readonly Rectangle Rect;
        public readonly Point ClickPoint;

        public NpcPosition(Point min, Point max, float yOffset, float heightMul)
        {
            Rect = new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
            ClickPoint = new(min.X + (Rect.Width / 2), (int)(max.Y + yOffset + (Rect.Height * heightMul)));
        }

        public NpcPosition(Rectangle rect, float yOffset, float heightMul)
        {
            Rect = rect;
            ClickPoint = new(rect.Left + (rect.Width / 2), (int)(rect.Bottom + yOffset + (rect.Height * heightMul)));
        }

        public bool Equals(NpcPosition other)
        {
            return Rect == other.Rect;
        }
    }
}