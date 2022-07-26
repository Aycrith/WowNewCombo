using System;
using System.Numerics;

namespace Core.GOAP
{
    public class CorpseEvent : GoapEventArgs
    {
        public const string NAME = "Corpse";
        public const string COLOR = "black";

        public Vector3 Location { get; }
        public double Radius { get; }

        public CorpseEvent(Vector3 location, double radius)
        {
            Location = location;
            Radius = Math.Max(1, radius);
        }
    }
}
