using System.Numerics;
using System.Collections.Generic;

namespace Core.PPather
{
    public class LineArgs
    {
        public string Name { get; init; } = string.Empty;
        public List<Vector3> Spots { get; init; } = new();
        public int Colour { get; init; }
        public int MapId { get; init; }
    }
}
