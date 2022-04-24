using System;
using System.Collections.Generic;
using System.Text;

namespace WowheadDB
{
    public class Area
    {
        public List<NPC> flightmaster;
        public List<NPC> innkeeper;
        public List<NPC> repair;
        public List<NPC> vendor;
        public List<NPC> trainer;
        public Dictionary<string, List<Node>> herb;
        public Dictionary<string, List<Node>> vein;

        public List<int> skinnable;
    }
}