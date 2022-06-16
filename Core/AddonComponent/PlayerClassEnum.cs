namespace Core
{
    public enum PlayerClassEnum
    {
        None,
        Warrior,
        Paladin,
        Hunter,
        Rogue,
        Priest,
        DeathKnight,
        Shaman,
        Mage,
        Warlock,
        Monk,
        Druid,
        DemonHunter
    }

    public static class PlayerClassEnum_Extension
    {
        public static string ToStringF(this PlayerClassEnum value) => value switch
        {
            PlayerClassEnum.None => nameof(PlayerClassEnum.None),
            PlayerClassEnum.Warrior => nameof(PlayerClassEnum.Warrior),
            PlayerClassEnum.Paladin => nameof(PlayerClassEnum.Paladin),
            PlayerClassEnum.Hunter => nameof(PlayerClassEnum.Hunter),
            PlayerClassEnum.Rogue => nameof(PlayerClassEnum.Rogue),
            PlayerClassEnum.Priest => nameof(PlayerClassEnum.Priest),
            PlayerClassEnum.DeathKnight => nameof(PlayerClassEnum.DeathKnight),
            PlayerClassEnum.Shaman => nameof(PlayerClassEnum.Shaman),
            PlayerClassEnum.Mage => nameof(PlayerClassEnum.Mage),
            PlayerClassEnum.Warlock => nameof(PlayerClassEnum.Warlock),
            PlayerClassEnum.Monk => nameof(PlayerClassEnum.Monk),
            PlayerClassEnum.Druid => nameof(PlayerClassEnum.Druid),
            PlayerClassEnum.DemonHunter => nameof(PlayerClassEnum.DemonHunter),
            _ => nameof(PlayerClassEnum.None)
        };
    }
}