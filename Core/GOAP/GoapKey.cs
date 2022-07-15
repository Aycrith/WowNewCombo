namespace Core.GOAP
{
    public enum GoapKey
    {
        hastarget,
        dangercombat,
        targetisalive,
        targettargetsus,
        incombat,
        pethastarget,
        ismounted,
        withinpullrange,
        incombatrange,
        pulled,
        isdead,
        shouldloot,
        shouldskin,
        newtarget,
        producedcorpse,
        consumecorpse,
        isswimming,
        itemsbroken,
        gathering,
        targethostile,
        hasfocus,
        focushastarget,
    }

    public static class GoapKey_Extension
    {
        private static readonly string unknown = "Unknown";

        private static string ToStringTrue(GoapKey value) => value switch
        {
            GoapKey.hastarget => "Target",
            GoapKey.dangercombat => "Danger",
            GoapKey.targetisalive => "Target alive",
            GoapKey.targettargetsus => "Targets us",
            GoapKey.incombat => "Combat",
            GoapKey.pethastarget => "Pet target",
            GoapKey.ismounted => "Mounted",
            GoapKey.withinpullrange => "Pull range",
            GoapKey.incombatrange => "Combat range",
            GoapKey.pulled => "Pulled",
            GoapKey.isdead => "Dead",
            GoapKey.shouldloot => "Loot",
            GoapKey.shouldskin => "Skin",
            GoapKey.newtarget => "New target",
            GoapKey.producedcorpse => "Killing blow",
            GoapKey.consumecorpse => "Corpse nearby",
            GoapKey.isswimming => "Swimming",
            GoapKey.itemsbroken => "Broken",
            GoapKey.gathering => "Gathering",
            GoapKey.hasfocus => "Focus",
            GoapKey.focushastarget => "Focus Target",
            GoapKey.targethostile => "Target Hostile",
            _ => unknown
        };

        private static string ToStringFalse(GoapKey value) => value switch
        {
            GoapKey.hastarget => "!Target",
            GoapKey.dangercombat => "!Danger",
            GoapKey.targetisalive => "!Target alive",
            GoapKey.targettargetsus => "!Targets us",
            GoapKey.incombat => "!Combat",
            GoapKey.pethastarget => "!Pet target",
            GoapKey.ismounted => "!Mounted",
            GoapKey.withinpullrange => "!Pull range",
            GoapKey.incombatrange => "!Combat range",
            GoapKey.pulled => "!Pulled",
            GoapKey.isdead => "!Dead",
            GoapKey.shouldloot => "!Loot",
            GoapKey.shouldskin => "!Skin",
            GoapKey.newtarget => "!New target",
            GoapKey.producedcorpse => "!Killing blow",
            GoapKey.consumecorpse => "!Corpse nearby",
            GoapKey.isswimming => "!Swimming",
            GoapKey.itemsbroken => "!Broken",
            GoapKey.gathering => "!Gathering",
            GoapKey.hasfocus => "!Focus",
            GoapKey.focushastarget => "!Focus Target",
            GoapKey.targethostile => "!Target Hostile",
            _ => unknown
        };

        public static string ToStringF(this GoapKey key, bool state)
        {
            return state ? ToStringTrue(key) : ToStringFalse(key);
        }
    }
}