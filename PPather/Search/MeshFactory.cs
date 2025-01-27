using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;

using WowTriangles;

namespace PPather;

public static class MeshFactory
{
    public static List<Vector3> CreatePoints(TriangleCollection collection)
    {
        return collection.Vertecies;
    }


    public static int CreateTriangles(TriangleType modelType, TriangleCollection tc, int[] output)
    {
        int c = 0;

        for (int i = 0; i < tc.TriangleCount; i++)
        {
            tc.GetTriangle(i, out int v0, out int v1, out int v2, out TriangleType flags);
            if (flags != modelType)
                continue;

            output[c++] = v0;
            output[c++] = v1;
            output[c++] = v2;
        }

        return c;
    }
}
