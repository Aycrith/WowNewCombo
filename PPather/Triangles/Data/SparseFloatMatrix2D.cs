using System;
using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

using static System.MathF;

namespace PPather.Triangles.Data;

public sealed class SparseFloatMatrix2D<T> : SparseMatrix2D<T> where T : IList
{
    private const float offset = 100000f;
    private readonly float gridSize;

    public SparseFloatMatrix2D(float gridSize) : base(0)
    {
        this.gridSize = gridSize;
    }

    public SparseFloatMatrix2D(float gridSize, int initialCapazity)
        : base(initialCapazity)
    {
        this.gridSize = gridSize;
    }

    public float GridToLocal(int grid)
    {
        return (grid * gridSize) - offset;
    }

    public int LocalToGrid(float f)
    {
        return (int)((f + offset) / gridSize);
    }

    [SkipLocalsInit]
    public (ReadOnlyMemory<T>, int, int) GetAllInSquare(float min_x, float min_y,
                                  float max_x, float max_y)
    {
        int sx = LocalToGrid(min_x);
        int ex = LocalToGrid(max_x);

        int sy = LocalToGrid(min_y);
        int ey = LocalToGrid(max_y);

        var pooler = ArrayPool<T>.Shared;
        T[] array = pooler.Rent((int)Ceiling(
            ((ex - sx + 1) * gridSize) +
            ((ey - sy + 1) * gridSize)));

        int i = 0;
        int totalCount = 0;
        for (int x = sx; x <= ex; x++)
        {
            for (int y = sy; y <= ey; y++)
            {
                if (base.TryGetValue(x, y, out T t))
                {
                    totalCount += t.Count;
                    array[i++] = t;
                }
            }
        }

        pooler.Return(array);
        return (array, i, totalCount);
    }

    public bool TryGetValue(float x, float y, out T t)
    {
        return base.TryGetValue(LocalToGrid(x), LocalToGrid(y), out t);
    }

    public void Add(float x, float y, T val)
    {
        base.Add(LocalToGrid(x), LocalToGrid(y), val);
    }
}
