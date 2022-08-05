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

        public bool HasValue(int x, int y)
        {
            return dict.ContainsKey((x, y));
        }

        public T Get(int x, int y)
        {
            T r = default;
            dict.TryGetValue((x, y), out r);
            return r;
        }

        public void Set(int x, int y, T val)
        {
            dict[(x, y)] = val;
        }

        public bool IsSet(int x, int y)
        {
            return HasValue(x, y);
        }

        public void Clear(int x, int y)
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
