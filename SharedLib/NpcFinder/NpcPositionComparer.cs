using System.Collections.Generic;
using System.Drawing;

using SharedLib.Extensions;

namespace SharedLib.NpcFinder;

internal sealed class NpcPositionComparer : IComparer<NpcPosition>
{
    private readonly IBitmapProvider bitmapProvider;

    public NpcPositionComparer(IBitmapProvider bitmapProvider)
    {
        this.bitmapProvider = bitmapProvider;
    }

    public int Compare(NpcPosition x, NpcPosition y)
    {
        Point origin = bitmapProvider.Rect.Centre();
        float dx = PointExt.SqrDistance(origin, x.ClickPoint);
        float dy = PointExt.SqrDistance(origin, y.ClickPoint);

        return dx.CompareTo(dy);
    }
}
