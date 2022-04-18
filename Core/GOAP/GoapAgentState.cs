
namespace Core.GOAP
{
    public class GoapAgentState
    {
        public bool ShouldConsumeCorpse { get; set; }
        public bool NeedLoot { get; set; }
        public bool NeedSkin { get; set; }
        public int LastCombatKillCount { get; set; }
    }
}
