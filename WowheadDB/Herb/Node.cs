using System.Collections.Generic;
using System.Numerics;
using SharedLib.Extensions;

namespace WowheadDB
{
    public class Node
    {
        public List<List<float>> coords;
        public int level;
        public string name;
        public int type;
        public int id;

        public List<Vector3> points => VectorExt.FromList(coords);
    }
}
