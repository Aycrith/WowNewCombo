namespace PPather;

[System.Flags]
public enum TriangleType : byte
{
    None = 0,
    Terrain = 1,
    Water = 2,
    Object = 4,
    Model = 8
}

public static class TriangleType_Ext
{
    public static bool Has(this TriangleType flags, TriangleType flag)
    {
        return (flags & flag) != 0;
    }
}
