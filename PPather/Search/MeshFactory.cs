using System.Collections.Generic;
using System.Linq;
using WowTriangles;
using System.Numerics;

namespace PPather
{
    public class MeshFactory
    {
        public static Vector3[] CreatePoints(TriangleCollection collection)
        {
            Vector3[] points = new Vector3[collection.VertexCount];
            for (int i = 0; i < collection.VertexCount; i++)
            {
                collection.GetVertex(i, out float x, out float y, out float z);
                points[i] = new(x, y, z);
            }

            return points;
        }

        public static int[] CreateTriangles(int modelType, TriangleCollection tc)
        {
            int[] triangles = new int[tc.TriangleCount * 3];
            int triangleNumber = 0;
            int vertexOffset = 0;
            for (int i = 0; i < tc.TriangleCount; i++)
            {
                tc.GetTriangle(i, out int v0, out int v1, out int v2, out int flags, out _);
                if (flags == modelType)
                {
                    triangles[(triangleNumber * 3) + 0] = v0 + vertexOffset;
                    triangles[(triangleNumber * 3) + 1] = v1 + vertexOffset;
                    triangles[(triangleNumber * 3) + 2] = v2 + vertexOffset;
                }
                else
                {
                    triangles[(triangleNumber * 3) + 0] = -1;
                    triangles[(triangleNumber * 3) + 1] = -1;
                    triangles[(triangleNumber * 3) + 2] = -1;
                }
                triangleNumber++;
            }
            vertexOffset += tc.VertexCount;
            return triangles.Where(t => t != -1).ToArray();
        }
    }
}
