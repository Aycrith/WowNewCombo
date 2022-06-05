using System.Collections.Generic;

namespace PPather.Data
{
    public class LineArgs
    {
        public string Name { get; init; }
        public List<DummyVector3> Spots { get; init; }
        public int Colour { get; init; }
        public int MapId { get; init; }
    }
}