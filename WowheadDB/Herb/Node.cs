using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
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

        [JsonIgnore]
        public List<Vector3> points => VectorExt.FromList(coords);
    }
}
