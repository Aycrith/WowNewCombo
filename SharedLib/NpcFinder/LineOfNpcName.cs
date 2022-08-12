namespace SharedLib.NpcFinder
{
    public readonly struct LineOfNpcName
    {
        public readonly int XStart;
        public readonly int Y;
        public readonly int XEnd;

        public readonly int XCenter => XStart + ((XEnd - XStart) / 2);

        public LineOfNpcName(int xStart, int xend, int y)
        {
            this.XStart = xStart;
            this.Y = y;
            this.XEnd = xend;
        }
    }
}