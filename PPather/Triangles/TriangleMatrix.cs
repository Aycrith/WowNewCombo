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
using System.Runtime.InteropServices;

using static WowTriangles.Utils;

namespace WowTriangles;

public sealed class TriangleMatrix
{
    private const float resolution = 6.0f;
    private readonly SparseFloatMatrix2D<List<int>> matrix;

    public TriangleMatrix(TriangleCollection tc, ILogger logger)
    {
        DateTime pre = DateTime.UtcNow;

        matrix = new SparseFloatMatrix2D<List<int>>(resolution);

        Vector3 vertex0;
        Vector3 vertex1;
        Vector3 vertex2;

        for (int i = 0; i < tc.TriangleCount; i++)
        {
            tc.GetTriangleVertices(i,
                    out vertex0.X, out vertex0.Y, out vertex0.Z,
                    out vertex1.X, out vertex1.Y, out vertex1.Z,
                    out vertex2.X, out vertex2.Y, out vertex2.Z);

            float minx = Min3(vertex0.X, vertex1.X, vertex2.X);
            float maxx = Max3(vertex0.X, vertex1.X, vertex2.X);
            float miny = Min3(vertex0.Y, vertex1.Y, vertex2.Y);
            float maxy = Max3(vertex0.Y, vertex1.Y, vertex2.Y);

            Vector3 box_center;
            Vector3 box_halfsize;
            box_halfsize.X = resolution / 2;
            box_halfsize.Y = resolution / 2;
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
                    box_center.X = grid_x + (resolution / 2);
                    box_center.Y = grid_y + (resolution / 2);
                    box_center.Z = 0;

                    if (!TestTriangleBoxIntersect(vertex0, vertex1, vertex2, box_center, box_halfsize))
                    {
                        continue;
                    }

                    if (!matrix.TryGetValue(grid_x, grid_y, out List<int> list))
                    {
                        list = new();
                        matrix.Add(grid_x, grid_y, list);
                    }
                    list.Add(i);
                }
            }
        }

        if (logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace($"Mesh [||,||] Bounds: [{tc.Min.X:F4}, {tc.Min.Y:F4}] [{tc.Max.X:F4}, {tc.Max.Y:F4}] - {tc.TriangleCount} tri - {tc.VertexCount} ver - c {matrix.Count} - {(DateTime.UtcNow - pre).TotalMilliseconds}ms");
    }

    public void Clear()
    {
        foreach (List<int> list in matrix.GetAllElements())
        {
            list.Clear();
        }

        matrix.Clear();
    }

    public ArraySegment<int> GetAllCloseTo(float x, float y, float distance)
    {
        (List<int>[] close, int count) = matrix.GetAllInSquare(x - distance, y - distance, x + distance, y + distance);

        int totalSize = 0;
        for (int i = 0; i < count; i++)
        {
            totalSize += close[i].Count;
        }

        var pooler = ArrayPool<int>.Shared;
        var all = pooler.Rent(totalSize);

        int index = 0;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<int> span = CollectionsMarshal.AsSpan(close[i]);
            for (int j = 0; j < span.Length; j++)
            {
                all[index++] = span[j];
            }
        }

        pooler.Return(all);
        return new ArraySegment<int>(all, 0, index);
    }

    public ArraySegment<int> GetAllInSquare(float x0, float y0, float x1, float y1)
    {
        (List<int>[] close, int count) = matrix.GetAllInSquare(x0, y0, x1, y1);

        int totalSize = 0;
        for (int i = 0; i < count; i++)
        {
            totalSize += close[i].Count;
        }

        var pooler = ArrayPool<int>.Shared;
        var all = pooler.Rent(totalSize);

        int index = 0;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<int> span = CollectionsMarshal.AsSpan(close[i]);
            for (int j = 0; j < span.Length; j++)
            {
                all[index++] = span[j];
            }
        }
        pooler.Return(all);
        return new ArraySegment<int>(all, 0, index);
    }
}