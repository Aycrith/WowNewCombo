namespace Core
{
    public enum PowerType
    {
        HealtCost = 0,
        None = 1,
        Mana = 2,
        Rage = 3,
        Focus = 4,
        Energy = 5
    }

    public static class PowerType_Extension
    {
        public static string ToStringF(this PowerType value) => value switch
        {
            PowerType.HealtCost => nameof(PowerType.HealtCost),
            PowerType.None => nameof(PowerType.None),
            PowerType.Mana => nameof(PowerType.Mana),
            PowerType.Rage => nameof(PowerType.Rage),
            PowerType.Focus => nameof(PowerType.Focus),
            PowerType.Energy => nameof(PowerType.Energy),
            _ => nameof(PowerType.HealtCost)
        };
    }
}
