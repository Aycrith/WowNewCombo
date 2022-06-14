using System;
using System.Collections.Generic;

namespace SharedLib.NpcFinder
{
    public class OverlappingNames : IEqualityComparer<NpcPosition>
    {
        private readonly int minWidth;
        private readonly int minHeight;

        public OverlappingNames(int minWidth, int minHeight)
        {
            this.minWidth = minWidth;
            this.minHeight = minHeight;
        }

        public bool Equals(NpcPosition x, NpcPosition y)
        {
            return x.Rect.IntersectsWith(y.Rect) ||
                Math.Abs(x.Rect.X - y.Rect.X) < minWidth ||
                Math.Abs(x.Rect.Y - y.Rect.Y) < minHeight;
        }

        public int GetHashCode(NpcPosition obj)
        {
            return obj.Rect.GetHashCode() ^ obj.ClickPoint.GetHashCode();
        }
    }
}
