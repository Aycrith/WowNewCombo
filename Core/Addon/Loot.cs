namespace Core;

public enum LootStatus
{
    CORPSE = 0,
    READY = 1,
    CLOSED = 2
}

public static class Loot_Extensions
{
    public static string ToStringF(this LootStatus value) => value switch
    {
        LootStatus.CORPSE => nameof(LootStatus.CORPSE),
        LootStatus.READY => nameof(LootStatus.READY),
        LootStatus.CLOSED => nameof(LootStatus.CLOSED),
        _ => throw new System.NotImplementedException(),
    };
}

public static class Loot
{
    public const int LOOTFRAME_AUTOLOOT_DELAY = 300;

    public const int LOOT_RESET_UPDATE_COUNT = 5;
}