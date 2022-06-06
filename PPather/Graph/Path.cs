/*
  This file is part of ppather.

    PPather is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PPather is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with ppather.  If not, see <http://www.gnu.org/licenses/>.

*/

using System.Collections.Generic;
using System.Numerics;

namespace PPather.Graph
{
    public class Path
    {
        public List<Vector3> locations { get; set; } = new();

        public int Count => locations.Count;

        public Vector3 GetLast => locations[^1];

        public Vector3 this[int index] => locations[index];

        public Path(List<Spot> steps)
        {
            foreach (Spot s in steps)
            {
                Add(s.Loc);
            }
        }

        public void Add(Vector3 l)
        {
            locations.Add(l);
        }
    }
}