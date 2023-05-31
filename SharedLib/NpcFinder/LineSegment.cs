namespace SharedLib.NpcFinder;

public readonly struct LineSegment
{
    public readonly int XStart;
    public readonly int Y;
    public readonly int XEnd;
    public readonly int XCenter;

    public LineSegment(int xStart, int xEnd, int y)
    {
        this.XStart = xStart;
        this.Y = y;
        this.XEnd = xEnd;
        XCenter = XStart + ((XEnd - XStart) / 2);
    }
}