using System.Collections.Generic;
using System.Numerics;

namespace PPather.Data
{
    public class LinesEventArgs
    {
        public List<Vector4> Locations { get; set; }
        public int Colour { get; set; }
        public string Name { get; set; }

        public LinesEventArgs(string name, List<Vector4> locations, int colour)
        {
            this.Name = name;
            this.Locations = locations;
            this.Colour = colour;
        }
    }
}