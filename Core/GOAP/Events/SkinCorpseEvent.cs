using System;
using System.Numerics;

namespace Core.GOAP
{
    public class SkinCorpseEvent : GoapEventArgs
    {
        public const string NAME = "Skin";
        public const string COLOR = "white";

        public Vector3 Location { get; }
        public double Radius { get; }
        public int NpcId { get; }

        public SkinCorpseEvent(Vector3 location, double radius, int npcId)
        {
            Location = location;
            Radius = Math.Max(1, radius);
            NpcId = npcId;
        }
    }
}
