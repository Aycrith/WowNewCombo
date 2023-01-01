using WowTriangles;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PPather;

public static class MeshFactory
{
    public static Vector3[] CreatePoints(TriangleCollection collection)
    {
        Vector3[] points = new Vector3[collection.VertexCount];
        var span = CollectionsMarshal.AsSpan(collection.Vertecies);
        for (int i = 0; i < span.Length; i++)
        {
            points[i] = span[i];
        }

        return points;
    }

    public static IEnumerable<int> CreateTriangles(TriangleType modelType, TriangleCollection tc)
    {
        List<int> triangles = new();
        for (int i = 0; i < tc.TriangleCount; i++)
        {
            tc.GetTriangle(i, out int v0, out int v1, out int v2, out TriangleType flags);
            if (flags != modelType)
                continue;

            triangles.Add(v0);
            triangles.Add(v1);
            triangles.Add(v2);
        }
        return triangles;
    }
}
