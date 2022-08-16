namespace SharedLib.NpcFinder
{
    public readonly struct LineSegment
    {
        public readonly int XStart;
        public readonly int Y;
        public readonly int XEnd;
        public readonly int XCenter;

        public LineSegment(int xStart, int xend, int y)
        {
            this.XStart = xStart;
            this.Y = y;
            this.XEnd = xend;
            XCenter = XStart + ((XEnd - XStart) / 2);
        }
    }
}