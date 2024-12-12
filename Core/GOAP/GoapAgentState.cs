using System.Collections.Generic;
using System.Numerics;

namespace Core.GOAP;

public sealed class GoapAgentState
{
    public bool ShouldConsumeCorpse { get; set; }
    public int LootableCorpseCount { get; set; }
    public int GatherableCorpseCount { get; set; }
    public int ConsumableCorpseCount { get; set; }
    public int LastCombatKillCount { get; set; }
    public bool Gathering { get; set; }

    public Stack<Vector3> SafeLocations { get; } = new();
}
