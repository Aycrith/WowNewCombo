using System;
using System.Numerics;

namespace Core.GOAP
{
    public sealed class CorpseEvent : GoapEventArgs
    {
        public const string NAME = "Corpse";
        public const string COLOR = "black";

        public Vector3 MapLoc { get; }
        public float Radius { get; }

        public CorpseEvent(Vector3 location, float radius)
        {
            MapLoc = location;
            Radius = MathF.Max(1, radius);
        }
    }
}
