using System.Numerics;

namespace Core.GOAP
{
    public class CorpseEvent : GoapEventArgs
    {
        public Vector3 Location { get; }
        public double Radius { get; }

        public CorpseEvent(Vector3 location, double radius)
        {
            Location = location;
            Radius = radius;
        }
    }
}
