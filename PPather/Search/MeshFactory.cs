using System.Collections.Generic;
using System.Linq;
using WowTriangles;
using System.Numerics;

namespace PPather
{
    public class MeshFactory
    {
        public static Vector3[] CreatePointList(List<TriangleCollection> m_TriangleCollection)
        {
            Vector3[] points = new Vector3[m_TriangleCollection.Sum(tc => tc.VertexCount())];
            int vertextNumber = 0;

            for (int t = 0; t < m_TriangleCollection.Count; t++)
            {
                for (int i = 0; i < m_TriangleCollection[t].VertexCount(); i++)
                {
                    m_TriangleCollection[t].GetVertex(i, out float x, out float y, out float z);
                    points[vertextNumber] = new(x, y, z);
                    vertextNumber++;
                }
            }

            return points;
        }

        public static int[] CreateTrianglesList(int modelType, List<TriangleCollection> m_TriangleCollection)
        {
            int triangleCount = m_TriangleCollection.Sum(tc => tc.TriangleCount());
            int[] triangles = new int[triangleCount * 3];

            int triangleNumber = 0;
            int vertexOffset = 0;
            for (int t = 0; t < m_TriangleCollection.Count; t++)
            {
                for (int i = 0; i < m_TriangleCollection[t].TriangleCount(); i++)
                {
                    m_TriangleCollection[t].GetTriangle(i, out int v0, out int v1, out int v2, out int flags, out int sequence);
                    if (flags == modelType || modelType == -1)
                    {
                        triangles[triangleNumber * 3] = v0 + vertexOffset;
                        triangles[triangleNumber * 3 + 1] = v1 + vertexOffset;
                        triangles[triangleNumber * 3 + 2] = v2 + vertexOffset;
                    }
                    else
                    {
                        triangles[triangleNumber * 3] = -1;
                        triangles[triangleNumber * 3 + 1] = -1;
                        triangles[triangleNumber * 3 + 2] = -1;
                    }
                    triangleNumber++;
                }
                vertexOffset += m_TriangleCollection[t].VertexCount();
            }
            return triangles.Where(t => t != -1).ToArray();
        }
    }
}
