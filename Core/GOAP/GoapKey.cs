namespace Core.GOAP
{
    public enum GoapKey
    {
        hastarget = 10,
        dangercombat = 11,
        targetisalive = 20,
        targettargetsus = 21,
        incombat = 30,
        pethastarget = 31,
        ismounted = 32,
        withinpullrange = 40,
        incombatrange = 50,
        pulled = 60,
        isdead = 80,
        shouldloot = 90,
        shouldskin = 92,
        newtarget = 110,
        producedcorpse = 130,
        consumecorpse = 131,
        corpselocation = 999,
        abort = 140,
        resume = 141,
        isswimming = 180,
        itemsbroken = 190,
        gathering = 200
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