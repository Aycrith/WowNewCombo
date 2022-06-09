/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using Microsoft.Extensions.Logging;

namespace WowTriangles
{
    /// <summary>
    ///
    /// </summary>
    public class TriangleCollection
    {
        private readonly ILogger logger;
        private readonly TrioArray<float> vertices = new();
        private readonly QuadArray<int> triangles = new();

        private TriangleMatrix matrix;

        public bool changed = true;
        public int LRU;
        public float base_x, base_y;
        public int grid_x, grid_y;

        public float max_x = -1E30f;
        public float max_y = -1E30f;
        public float max_z = -1E30f;

        public float min_x = 1E30f;
        public float min_y = 1E30f;
        public float min_z = 1E30f;

        private float limit_max_x = 1E30f;
        private float limit_max_y = 1E30f;
        private float limit_max_z = 1E30f;

        private float limit_min_x = -1E30f;
        private float limit_min_y = -1E30f;
        private float limit_min_z = -1E30f;

        public int TriangleCount { get; private set; }

        public int VertexCount { get; private set; }

        public TriangleCollection(ILogger logger)
        {
            this.logger = logger;
        }

        public void Clear()
        {
            vertices.SetSize(0);
            triangles.SetSize(0);

            TriangleCount = 0;
            VertexCount = 0;

            changed = true;
        }

        public TriangleMatrix GetTriangleMatrix()
        {
            if (matrix == null)
                matrix = new TriangleMatrix(this, logger);

            return matrix;
        }

        public void CompactVertices()
        {
            bool[] used_indices = new bool[VertexCount];
            int[] old_to_new = new int[VertexCount];

            // check what vertives are used
            for (int i = 0; i < TriangleCount; i++)
            {
                int v0, v1, v2;
                GetTriangle(i, out v0, out v1, out v2);
                used_indices[v0] = true;
                used_indices[v1] = true;
                used_indices[v2] = true;
            }

            // figure out new indices and move
            int sum = 0;
            for (int i = 0; i < used_indices.Length; i++)
            {
                if (used_indices[i])
                {
                    old_to_new[i] = sum;
                    vertices.Get(i, out float x, out float y, out float z);
                    vertices.Set(sum, x, y, z);
                    sum++;
                }
                else
                {
                    old_to_new[i] = -1;
                }
            }

            vertices.SetSize(sum);

            // Change all triangles
            for (int i = 0; i < TriangleCount; i++)
            {
                GetTriangle(i, out int v0, out int v1, out int v2, out int flags, out int sequence);
                triangles.Set(i, old_to_new[v0], old_to_new[v1], old_to_new[v2], flags, sequence);
            }
            VertexCount = sum;
        }

        public void SetLimits(float min_x, float min_y, float min_z,
                              float max_x, float max_y, float max_z)
        {
            limit_max_x = max_x;
            limit_max_y = max_y;
            limit_max_z = max_z;

            limit_min_x = min_x;
            limit_min_y = min_y;
            limit_min_z = min_z;
        }

        public void GetLimits(out float min_x, out float min_y, out float min_z,
                              out float max_x, out float max_y, out float max_z)
        {
            max_x = limit_max_x;
            max_y = limit_max_y;
            max_z = limit_max_z;

            min_x = limit_min_x;
            min_y = limit_min_y;
            min_z = limit_min_z;
        }

        public void PaintPath(float x, float y, float z, float x2, float y2, float z2)
        {
            int v0 = AddVertex(x, y, z + 0.1f);
            int v1 = AddVertex(x, y, z + 0.5f);
            int v2 = AddVertex(x2, y2, z2 + 0.1f);

            AddTriangle(v0, v1, v2);
            AddTriangle(v2, v1, v0);
        }

        public void AddBigMarker(float x, float y, float z)
        {
            int v0 = AddVertex(x, y, z);
            int v1 = AddVertex(x + 1.3f, y, z + 4);
            int v2 = AddVertex(x - 1.3f, y, z + 4);
            int v3 = AddVertex(x, y + 1.3f, z + 4);
            int v4 = AddVertex(x, y - 1.3f, z + 4);
            AddTriangle(v0, v1, v2);
            AddTriangle(v2, v1, v0);
            AddTriangle(v0, v3, v4);
            AddTriangle(v4, v3, v0);
        }

        public void GetBBox(out float min_x, out float min_y, out float min_z,
                              out float max_x, out float max_y, out float max_z)
        {
            max_x = this.max_x;
            max_y = this.max_y;
            max_z = this.max_z;

            min_x = this.min_x;
            min_y = this.min_y;
            min_z = this.min_z;
        }

        public int AddVertex(float x, float y, float z)
        {
            vertices.Set(VertexCount, x, y, z);
            return VertexCount++;
        }

        // big list if triangles (3 vertice IDs per triangle)
        public int AddTriangle(int v0, int v1, int v2, int flags, int sequence)
        {
            // check limits
            if (!CheckVertexLimits(v0) &&
                !CheckVertexLimits(v1) &&
                !CheckVertexLimits(v2))
                return -1;

            // Create new
            SetMinMax(v0);
            SetMinMax(v1);
            SetMinMax(v2);

            triangles.Set(TriangleCount, v0, v1, v2, flags, sequence);
            changed = true;
            return TriangleCount++;
        }

        // big list if triangles (3 vertice IDs per triangle)
        public int AddTriangle(int v0, int v1, int v2)
        {
            return AddTriangle(v0, v1, v2, 0, 0);
        }

        private void SetMinMax(int v)
        {
            GetVertex(v, out float x, out float y, out float z);
            if (x < min_x)
                min_x = x;
            if (y < min_y)
                min_y = y;
            if (z < min_z)
                min_z = z;

            if (x > max_x)
                max_x = x;
            if (y > max_y)
                max_y = y;
            if (z > max_z)
                max_z = z;
        }

        private bool CheckVertexLimits(int v)
        {
            GetVertex(v, out float x, out float y, out float z);

            if (x < limit_min_x || x > limit_max_x)
                return false;
            if (y < limit_min_y || y > limit_max_y)
                return false;
            if (z < limit_min_z || z > limit_max_z)
                return false;

            return true;
        }

        public void GetBoundMax(out float x, out float y, out float z)
        {
            x = max_x;
            y = max_y;
            z = max_z;
        }

        public void GetBoundMin(out float x, out float y, out float z)
        {
            x = min_x;
            y = min_y;
            z = min_z;
        }

        public void GetVertex(int i, out float x, out float y, out float z)
        {
            vertices.Get(i, out x, out y, out z);
        }

        public void GetTriangle(int i, out int v0, out int v1, out int v2)
        {
            triangles.Get(i, out v0, out v1, out v2, out _, out _);
        }

        public void GetTriangle(int i, out int v0, out int v1, out int v2, out int flags, out int sequence)
        {
            triangles.Get(i, out v0, out v1, out v2, out flags, out sequence);
        }

        public void GetTriangleVertices(int i,
                                        out float x0, out float y0, out float z0,
                                        out float x1, out float y1, out float z1,
                                        out float x2, out float y2, out float z2, out int flags, out int sequence)
        {
            triangles.Get(i, out int v0, out int v1, out int v2, out flags, out sequence);
            vertices.Get(v0, out x0, out y0, out z0);
            vertices.Get(v1, out x1, out y1, out z1);
            vertices.Get(v2, out x2, out y2, out z2);
        }

        public void GetTriangleVertices(int i,
                                        out float x0, out float y0, out float z0,
                                        out float x1, out float y1, out float z1,
                                        out float x2, out float y2, out float z2)
        {
            triangles.Get(i, out int v0, out int v1, out int v2, out _, out _);
            vertices.Get(v0, out x0, out y0, out z0);
            vertices.Get(v1, out x1, out y1, out z1);
            vertices.Get(v2, out x2, out y2, out z2);
        }

        public float[] GetFlatVertices()
        {
            float[] flat = new float[VertexCount * 3];
            for (int i = 0; i < VertexCount; i++)
            {
                int off = i * 3;
                vertices.Get(i, out flat[off], out flat[off + 1], out flat[off + 2]);
            }
            return flat;
        }

        public void AddAllTrianglesFrom(TriangleCollection set)
        {
            for (int i = 0; i < set.TriangleCount; i++)
            {
                float v0x, v0y, v0z;
                float v1x, v1y, v1z;
                float v2x, v2y, v2z;

                set.GetTriangleVertices(i,
                    out v0x, out v0y, out v0z,
                    out v1x, out v1y, out v1z,
                    out v2x, out v2y, out v2z);

                int v0 = AddVertex(v0x, v0y, v0z);
                int v1 = AddVertex(v1x, v1y, v1z);
                int v2 = AddVertex(v2x, v2y, v2z);
                AddTriangle(v0, v1, v2);
            }
        }
    }
}