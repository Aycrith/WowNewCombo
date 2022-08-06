using System.Collections.Generic;

namespace PPather.Triangles.Data
{
    public class SparseMatrix3D<T>
    {
        private readonly Dictionary<(int, int, int), T> dict = new();

        public T Get(int x, int y, int z)
        {
            dict.TryGetValue((x, y, z), out T r);
            return r;
        }

        public bool ContainsKey(int x, int y, int z)
        {
            return dict.ContainsKey((x, y, z));
        }

        public void Add(int x, int y, int z, T val)
        {
            dict[(x, y, z)] = val;
        }

        public void Remove(int x, int y, int z)
        {
            dict.Remove((x, y, z));
        }
    }
}
