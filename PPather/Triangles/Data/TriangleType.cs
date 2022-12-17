namespace PPather
{
    [System.Flags]
    public enum TriangleType : byte
    {
        Terrain = 0,
        Water = 1,
        Object = 2,
        Model = 4,
    }

    public static class TriangleType_Ext
    {
        public static bool Has(this TriangleType flags, TriangleType flag)
        {
            return (flags & flag) != 0;
        }
    }

}
