using WowTriangles;
using System.Numerics;
using System.Buffers;
using System;
using System.Collections.Generic;

namespace PPather;

public static class MeshFactory
{
    public static List<Vector3> CreatePoints(TriangleCollection collection)
    {
        return collection.Vertecies;
    }


    public static int[] CreateTriangles(TriangleType modelType, TriangleCollection tc)
    {
        var pooler = ArrayPool<int>.Shared;
        var triangles = pooler.Rent(tc.TriangleCount * 3);
        int c = 0;

        for (int i = 0; i < tc.TriangleCount; i++)
        {
            tc.GetTriangle(i, out int v0, out int v1, out int v2, out TriangleType flags);
            if (flags != modelType)
                continue;

            triangles[c++] = v0;
            triangles[c++] = v1;
            triangles[c++] = v2;
        }

        pooler.Return(triangles);

        return c == 0
            ? Array.Empty<int>()
            : triangles.AsSpan(0, c).ToArray();
    }
}
