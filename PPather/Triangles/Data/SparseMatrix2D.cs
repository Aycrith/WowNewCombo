using System.Collections;
using System.Collections.Generic;

namespace PPather.Triangles.Data
{
    public class SparseMatrix2D<T> : IEnumerable<T>
    {
        private readonly Dictionary<(int, int), T> dict;

        public int Count => dict.Count;

        public SparseMatrix2D(int initialCapacity)
        {
            dict = new(initialCapacity);
        }

        public bool ContainsKey(int x, int y)
        {
            return dict.ContainsKey((x, y));
        }

        public bool TryGetValue(int x, int y, out T r)
        {
            return dict.TryGetValue((x, y), out r);
        }

        public void Add(int x, int y, T val)
        {
            dict[(x, y)] = val;
        }

        public void Remove(int x, int y)
        {
            dict.Remove((x, y));
        }

        public ICollection<T> GetAllElements()
        {
            return dict.Values;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dict.GetEnumerator();
        }
    }

}
