using System.Numerics;

namespace Core.PPather
{
    public class SphereArgs
    {
        public string Name { get; init; } = string.Empty;
        public Vector3 Spot { get; init; }
        public int Colour { get; init; }
        public int MapId { get; init; }
    }
}
