using System.Drawing;
using System;
using SharedLib.Extensions;

namespace SharedLib.NpcFinder;

public readonly struct NpcPosition : IEquatable<NpcPosition>
{
    private static readonly NpcPosition empty = new(Point.Empty, Point.Empty, 0, 0);
    public static ref readonly NpcPosition Empty => ref empty;

    public readonly Rectangle Rect;
    public readonly Point ClickPoint;

    public NpcPosition(Point min, Point max, int yOffset, float heightMul)
    {
        Rect = new(min.X, min.Y, max.X - min.X, max.Y - min.Y);

        ClickPoint = Rect.BottomCentre();
        ClickPoint.Offset(0, yOffset + (int)(Rect.Height * heightMul));
    }

    public NpcPosition(Rectangle rect, int yOffset, float heightMul)
    {
        Rect = rect;
        ClickPoint = Rect.BottomCentre();
        ClickPoint.Offset(0, yOffset + (int)(Rect.Height * heightMul));
    }

    public bool Equals(NpcPosition other)
    {
        return Rect == other.Rect;
    }
}