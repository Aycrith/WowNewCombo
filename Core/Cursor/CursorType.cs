namespace Core
{
    public enum CursorType
    {
        None = 0,
        Kill = 1,
        Loot = 2,
        Skin = 4,
        Mine = 5,
        Herb = 6,
        Vendor = 7,
        Repair = 8,
        Innkeeper = 9,
        Quest = 10
    }

    public static class CursorType_Extension
    {
        public static string ToStringF(this CursorType value) => value switch
        {
            CursorType.None => nameof(CursorType.None),
            CursorType.Kill => nameof(CursorType.Kill),
            CursorType.Loot => nameof(CursorType.Loot),
            CursorType.Skin => nameof(CursorType.Skin),
            CursorType.Mine => nameof(CursorType.Mine),
            CursorType.Herb => nameof(CursorType.Herb),
            CursorType.Vendor => nameof(CursorType.Vendor),
            CursorType.Repair => nameof(CursorType.Repair),
            CursorType.Innkeeper => nameof(CursorType.Innkeeper),
            CursorType.Quest => nameof(CursorType.Quest),
            _ => nameof(CursorType.None)
        };
    }
}