namespace Core.GOAP
{
    public enum GoapKey
    {
        none = 0,
        hastarget = 1,
        dangercombat = 2,
        targetisalive = 3,
        targettargetsus = 4,
        incombat = 5,
        pethastarget = 6,
        ismounted = 7,
        withinpullrange = 8,
        incombatrange = 9,
        pulled = 10,
        isdead = 11,
        shouldloot = 12,
        shouldskin = 13,
        newtarget = 14,
        producedcorpse = 15,
        consumecorpse = 16,
        abort = 17,
        resume = 18,
        isswimming = 19,
        itemsbroken = 20,
        gathering = 21,
        targethostile = 22,

        hasfocus = 23,
        focustargetincombat = 24,

        corpselocation = 999,
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
            GoapKey.abort => "Abort",
            GoapKey.isswimming => "Swimming",
            GoapKey.itemsbroken => "Broken",
            GoapKey.gathering => "Gathering",
            GoapKey.hasfocus => "Focus",
            GoapKey.focustargetincombat => "Focus Target Combat",
            GoapKey.targethostile => "Target Hostile",
            GoapKey.corpselocation => throw new System.NotImplementedException(),
            GoapKey.resume => throw new System.NotImplementedException(),
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
            GoapKey.abort => "!Abort",
            GoapKey.isswimming => "!Swimming",
            GoapKey.itemsbroken => "!Broken",
            GoapKey.gathering => "!Gathering",
            GoapKey.hasfocus => "!Focus",
            GoapKey.focustargetincombat => "!Focus Target Combat",
            GoapKey.targethostile => "!Target Hostile",
            GoapKey.corpselocation => throw new System.NotImplementedException(),
            GoapKey.resume => throw new System.NotImplementedException(),
            _ => unknown
        };

        public static string ToStringF(this GoapKey key, bool state)
        {
            return state ? ToStringTrue(key) : ToStringFalse(key);
        }
    }
}