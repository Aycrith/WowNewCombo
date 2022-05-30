using System.Collections.Generic;

namespace PPather.Data
{
    public class LineArgs
    {
        public LineArgs() { }

        public string Name { get; set; }
        public List<DummyVector3> Spots { get; set; }
        public int Colour { get; set; }
        public int MapId { get; set; }
    }
}