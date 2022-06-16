namespace Core
{
    public enum RaceEnum
    {
        None,
        Human,
        Orc,
        Dwarf,
        NightElf,
        Undead,
        Tauren,
        Gnome,
        Troll,
        Goblin,
        BloodElf,
        Draenei
    }

    public static class RaceEnum_Extension
    {
        public static string ToStringF(this RaceEnum value) => value switch
        {
            RaceEnum.None => nameof(RaceEnum.None),
            RaceEnum.Human => nameof(RaceEnum.Human),
            RaceEnum.Orc => nameof(RaceEnum.Orc),
            RaceEnum.Dwarf => nameof(RaceEnum.Dwarf),
            RaceEnum.NightElf => nameof(RaceEnum.NightElf),
            RaceEnum.Undead => nameof(RaceEnum.Undead),
            RaceEnum.Tauren => nameof(RaceEnum.Tauren),
            RaceEnum.Gnome => nameof(RaceEnum.Gnome),
            RaceEnum.Troll => nameof(RaceEnum.Troll),
            RaceEnum.Goblin => nameof(RaceEnum.Goblin),
            RaceEnum.BloodElf => nameof(RaceEnum.BloodElf),
            RaceEnum.Draenei => nameof(RaceEnum.Draenei),
            _ => nameof(RaceEnum.None)
        };
    }
}