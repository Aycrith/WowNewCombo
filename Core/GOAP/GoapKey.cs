using System.Collections.Generic;

namespace Core.GOAP
{
    public enum GoapKey
    {
        hastarget = 10,
        dangercombat = 11,
        targetisalive = 20,
        incombat = 30,
        pethastarget = 31,
        withinpullrange = 40,
        incombatrange = 50,
        pulled = 60,
        isdead = 80,
        shouldloot = 90,
        shouldskin = 92,
        newtarget = 110,
        fighting = 120,
        producedcorpse = 130,
        consumecorpse = 131,
        corpselocation = 999,
        abort = 140,
        resume = 141,
        isalive = 170,
        isswimming = 180,
        itemsbroken = 190,
        gathering = 200,
        wowscreen = 9999,
    }

    public static class GoapKeyDescription
    {
        private static readonly string unknown = "Unknown";

        private static readonly Dictionary<KeyValuePair<GoapKey, bool>, string> table = new()
        {
            { new(GoapKey.hastarget, true), "Target" },
            { new(GoapKey.hastarget, false), "!Target" },

            { new(GoapKey.dangercombat, true), "Danger" },
            { new(GoapKey.dangercombat, false), "!Danger" },

            { new(GoapKey.targetisalive, true), "Target alive" },
            { new(GoapKey.targetisalive, false), "Target dead" },

            { new(GoapKey.incombat, true), "Combat" },
            { new(GoapKey.incombat, false), "!Combat" },

            { new(GoapKey.pethastarget, true), "Pet target" },
            { new(GoapKey.pethastarget, false), "!Pet target" },

            { new(GoapKey.withinpullrange, true), "Pull range" },
            { new(GoapKey.withinpullrange, false), "!Pull range" },

            { new(GoapKey.incombatrange, true), "Combat range" },
            { new(GoapKey.incombatrange, false), "!combat range" },

            { new(GoapKey.pulled, true), "Pulled" },
            { new(GoapKey.pulled, false), "!Pulled" },

            { new(GoapKey.isdead, true), "Dead" },
            { new(GoapKey.isdead, false), "Alive" },

            { new(GoapKey.shouldloot, true), "Loot" },
            { new(GoapKey.shouldloot, false), "!Loot" },

            { new(GoapKey.shouldskin, true), "Skin" },
            { new(GoapKey.shouldskin, false), "!Skin" },

            { new(GoapKey.newtarget, true), "New target" },
            { new(GoapKey.newtarget, false), "!New target" },

            { new(GoapKey.fighting, true), "Fighting" },
            { new(GoapKey.fighting, false), "!Fighting" },

            { new(GoapKey.producedcorpse, true), "Killing blow" },
            { new(GoapKey.producedcorpse, false), "!Killing blow" },

            { new(GoapKey.consumecorpse, true), "Corpse nearby" },
            { new(GoapKey.consumecorpse, false), "!Corpse nearby" },

            { new(GoapKey.abort, true), "Abort" },
            { new(GoapKey.abort, false), "!Abort" },

            { new(GoapKey.isswimming, true), "Swimming" },
            { new(GoapKey.isswimming, false), "!Swimming" },

            { new(GoapKey.itemsbroken, true), "Broken" },
            { new(GoapKey.itemsbroken, false), "!Broken" },

            { new(GoapKey.gathering, true), "Gathering" },
            { new(GoapKey.gathering, false), "!Gathering" },
        };

        public static string ToString(GoapKey key, bool state)
        {
            return table.TryGetValue(new(key, state), out var text) ? text : unknown;
        }
    }
}