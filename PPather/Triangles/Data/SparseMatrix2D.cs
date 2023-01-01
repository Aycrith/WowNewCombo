using System.Collections.Generic;

namespace PPather.Triangles.Data;

public class SparseMatrix2D<T>
{
    private readonly Dictionary<int, T> dict;

    public int Count => dict.Count;

    public SparseMatrix2D(int initialCapacity)
    {
        dict = new(initialCapacity);
    }

    public bool ContainsKey(int x, int y)
    {
        return dict.ContainsKey((y << 16) ^ x);
    }

    public bool TryGetValue(int x, int y, out T r)
    {
        return dict.TryGetValue((y << 16) ^ x, out r);
    }

    public void Add(int x, int y, T val)
    {
        dict[(y << 16) ^ x] = val;
    }

    public void Remove(int x, int y)
    {
        dict.Remove((y << 16) ^ x);
    }

    public void Clear()
    {
        dict.Clear();
    }

    public ICollection<T> GetAllElements()
    {
        return dict.Values;
    }
}
