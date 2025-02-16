/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using Microsoft.Extensions.Logging;

using PPather.Triangles.Data;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static WowTriangles.Utils;

namespace WowTriangles;

public sealed class TriangleMatrix
{
    private const float resolution = 8.0f;
    private const float halfResoltion = resolution / 2f;

    private const int CellCapacity = 4096;
    private const int ACount = 1024;

    private readonly SparseFloatMatrix2D<List<int>> matrix;

    public int Count => matrix.Count;

    [SkipLocalsInit]
    public TriangleMatrix(TriangleCollection tc)
    {
        matrix = new SparseFloatMatrix2D<List<int>>(resolution, CellCapacity);

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        for (int i = 0; i < tc.TriangleCount; i++)
        {
            tc.GetTriangleVertices(i,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z);

            float minx = Min3(v0.X, v1.X, v2.X);
            float maxx = Max3(v0.X, v1.X, v2.X);
            float miny = Min3(v0.Y, v1.Y, v2.Y);
            float maxy = Max3(v0.Y, v1.Y, v2.Y);

            Vector3 box_center;
            Vector3 box_halfsize;
            box_halfsize.X = halfResoltion;
            box_halfsize.Y = halfResoltion;
            box_halfsize.Z = 1E6f;

            int startx = matrix.LocalToGrid(minx);
            int endx = matrix.LocalToGrid(maxx);
            int starty = matrix.LocalToGrid(miny);
            int endy = matrix.LocalToGrid(maxy);

            for (int x = startx; x <= endx; x++)
            {
                for (int y = starty; y <= endy; y++)
                {
                    float grid_x = matrix.GridToLocal(x);
                    float grid_y = matrix.GridToLocal(y);
                    box_center.X = grid_x + halfResoltion;
                    box_center.Y = grid_y + halfResoltion;
                    box_center.Z = 0;

                    if (!TriangleBoxIntersect(v0, v1, v2, box_center, box_halfsize))
                    {
                        continue;
                    }

                    int key = matrix.GetKey(grid_x, grid_y);
                    ref List<int> list = ref CollectionsMarshal.GetValueRefOrAddDefault(matrix.Dict, key, out bool exists);
                    if (!exists)
                    {
                        list = new List<int>(ACount);
                        matrix.Add(key, list);
                    }
                    list.Add(i);
                }
            }
        }
    }

    public void Clear()
    {
        foreach (List<int> list in matrix.GetAllElements())
        {
            list.Clear();
        }

        matrix.Clear();
    }

    [SkipLocalsInit]
    public ReadOnlySpan<int> GetAllCloseTo(float x, float y, float range)
    {
        (ReadOnlyMemory<List<int>> close, int count, int totalCount) =
            matrix.GetAllInSquare(x - range, y - range, x + range, y + range);

        return GetAsSpan(close, count, totalCount);
    }

    [SkipLocalsInit]
    public ReadOnlySpan<int> GetAllInSquare(float x0, float y0, float x1, float y1)
    {
        (ReadOnlyMemory<List<int>> close,
            int count,
            int totalCount) =
            matrix.GetAllInSquare(x0, y0, x1, y1);

        return GetAsSpan(close, count, totalCount);
    }

    [SkipLocalsInit]
    private static ReadOnlySpan<int> GetAsSpan(
        ReadOnlyMemory<List<int>> close, int count, int totalCount)
    {
        var pooler = ArrayPool<int>.Shared;
        int[] output = pooler.Rent(totalCount);
        Span<int> toSpan = output.AsSpan();

        int c = 0;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<int> fromSpan = CollectionsMarshal.AsSpan(close.Span[i]);
            fromSpan.CopyTo(toSpan.Slice(c, fromSpan.Length));
            c += fromSpan.Length;
        }

        pooler.Return(output);
        return new(output, 0, c);
    }
}