namespace Core
{
    public enum SchoolMask
    {
        None = 0,
        Physical = 1,
        Holy = 2,
        Fire = 4,
        Nature = 8,
        Frost = 16,
        Shadow = 32,
        Arcane = 64,
    }

    public static class SchoolMask_Extension
    {
        public static string ToStringF(this SchoolMask value) => value switch
        {
            SchoolMask.None => nameof(SchoolMask.None),
            SchoolMask.Physical => nameof(SchoolMask.Physical),
            SchoolMask.Holy => nameof(SchoolMask.Holy),
            SchoolMask.Fire => nameof(SchoolMask.Fire),
            SchoolMask.Nature => nameof(SchoolMask.Nature),
            SchoolMask.Frost => nameof(SchoolMask.Frost),
            SchoolMask.Shadow => nameof(SchoolMask.Shadow),
            SchoolMask.Arcane => nameof(SchoolMask.Arcane),
            _ => nameof(SchoolMask.None)
        };
    }
}