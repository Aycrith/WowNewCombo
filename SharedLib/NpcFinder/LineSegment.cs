namespace SharedLib.NpcFinder;

public readonly record struct LineSegment
{
    public readonly int X;
    public readonly int Y;

    public readonly int XStart => X & 0xFFFF;
    public readonly int XEnd => X >> 16;
    public readonly int XCenter => XStart + ((XEnd - XStart) / 2);

    public LineSegment(int xStart, int xEnd, int y)
    {
        X = (xEnd << 16) | (xStart & 0xFFFF);
        Y = y;
    }
}