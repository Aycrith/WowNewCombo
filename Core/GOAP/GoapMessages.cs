using System.Numerics;

namespace Core.GOAP
{
    public class CorpseLocation
    {
        public Vector3 Location { get; }
        public double Radius { get; }

        public CorpseLocation(Vector3 location, double radius)
        {
            Location = location;
            Radius = radius;
        }
    }
}
