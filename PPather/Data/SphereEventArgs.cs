using PPather.Graph;
using System.Numerics;

namespace PPather.Data
{
    public class SphereEventArgs
    {
        public Vector4 Location { get; private set; }
        public int Colour { get; private set; }
        public string Name { get; private set; }

        public SphereEventArgs(string name, Vector4 location, int colour)
        {
            this.Name = name;
            this.Location = location;
            this.Colour = colour;
        }

        public Vertex Vertex => Vertex.Create(Location.X, Location.Y, Location.Z + 1);
    }
}