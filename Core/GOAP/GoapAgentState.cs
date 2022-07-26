
namespace Core.GOAP
{
    public class GoapAgentState
    {
        public bool ShouldConsumeCorpse { get; set; }
        public bool NeedLoot { get; set; }
        public bool NeedGather { get; set; }
        public int LootableCorpseCount { get; set; }
        public int GatherableCorpseCount { get; set; }
        public int ConsumableCorpseCount { get; set; }
        public int LastCombatKillCount { get; set; }
        public bool Gathering { get; set; }
    }
}
