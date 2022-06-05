using PPather.Graph;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PPather.Data
{
    public class LinesEventArgs
    {
        public List<Vector4> Locations { get; private set; }
        public int Colour { get; private set; }
        public string Name { get; private set; }

        public LinesEventArgs(string name, List<Vector4> locations, int colour)
        {
            this.Name = name;
            this.Locations = locations;
            this.Colour = colour;
        }

        //public IEnumerable<Vertex> Lines => Locations.Where(s => s != null).Select(s => Vertex.Create(s.X, s.Y, s.Z+5));
        public IEnumerable<Vertex> Lines => Locations.Select(s => Vertex.Create(s.X, s.Y, s.Z + 5));
    }
}