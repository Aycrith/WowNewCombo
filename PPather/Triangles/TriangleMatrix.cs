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
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static WowTriangles.Utils;

namespace WowTriangles;

public sealed class TriangleMatrix
{
    private const float resolution = 6.0f;
    private readonly SparseFloatMatrix2D<List<int>> matrix;

    [SkipLocalsInit]
    public TriangleMatrix(TriangleCollection tc, ILogger logger)
    {
        long pre = Stopwatch.GetTimestamp();

        matrix = new SparseFloatMatrix2D<List<int>>(resolution, 8096);

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

                    if (!TriangleBoxIntersect(v0, v1, v2, box_center, box_halfsize))
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
            logger.LogTrace($"Mesh [||,||] Bounds: " +
                $"[{tc.Min.X:F4}, {tc.Min.Y:F4}] " +
                $"[{tc.Max.X:F4}, {tc.Max.Y:F4}] - " +
                $"{tc.TriangleCount} tri - " +
                $"{tc.VertexCount} ver - " +
                $"{matrix.Count} c - " +
                $"{(Stopwatch.GetElapsedTime(pre)).TotalMilliseconds}ms");
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
    public ReadOnlySpan<int> GetAllCloseTo(float x, float y, float distance)
    {
        (ReadOnlyMemory<List<int>> close,
            int count,
            int totalCount) =
            matrix.GetAllInSquare(
                x - distance, y - distance, x + distance, y + distance);

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