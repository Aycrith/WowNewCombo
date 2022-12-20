/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using System.Collections.Generic;
using System.Numerics;

using Microsoft.Extensions.Logging;

using PPather;
using PPather.Triangles;

namespace WowTriangles
{
    /// <summary>
    ///
    /// </summary>
    public sealed class TriangleCollection
    {
        private readonly ILogger logger;
        public List<Vector3> Vertecies { get; }
        public List<Triangle<int>> Triangles { get; }

        private TriangleMatrix matrix;

        public int LRU;
        public int grid_x;
        public int grid_y;

        private Vector3 max = new(-1E30f, -1E30f, -1E30f);
        public Vector3 Max => max;

        private Vector3 min = new(1E30f, 1E30f, 1E30f);
        public Vector3 Min => min;

        private Vector3 limit_max = new(1E30f, 1E30f, 1E30f);
        private Vector3 limit_min = new(-1E30f, -1E30f, -1E30f);

        private int triangleCount;
        public int TriangleCount => Triangles.Count;

        public int VertexCount { get; private set; }

        public TriangleCollection(ILogger logger)
        {
            this.logger = logger;
            Vertecies = new(65536); // terrain mesh
            Triangles = new(128);
        }

        public void Clear()
        {
            triangleCount = 0;
            VertexCount = 0;

            Triangles.Clear();
            Vertecies.Clear();
            matrix.Clear();
        }

        public bool HasTriangleMatrix => matrix != null;

        public TriangleMatrix GetTriangleMatrix()
        {
            if (matrix == null)
                matrix = new TriangleMatrix(this, logger);

            return matrix;
        }

        public void SetLimits(float min_x, float min_y, float min_z,
                              float max_x, float max_y, float max_z)
        {
            limit_max = new(max_x, max_y, max_z);
            limit_min = new(min_x, min_y, min_z);
        }

        public int AddVertex(float x, float y, float z)
        {
            VerticesSet(VertexCount, x, y, z);
            return VertexCount++;
        }

        // big list if triangles (3 vertice IDs per triangle)
        public void AddTriangle(int v0, int v1, int v2, TriangleType flags)
        {
            // check limits
            if (!CheckVertexLimits(v0) &&
                !CheckVertexLimits(v1) &&
                !CheckVertexLimits(v2))
                return;

            // Create new
            SetMinMax(v0);
            SetMinMax(v1);
            SetMinMax(v2);

            TrianglesSet(triangleCount, v0, v1, v2, flags);
            triangleCount++;
        }

        private void SetMinMax(int v)
        {
            GetVertex(v, out float x, out float y, out float z);
            if (x < min.X)
                min.X = x;
            if (y < min.Y)
                min.Y = y;
            if (z < min.Z)
                min.Z = z;

            if (x > max.X)
                max.X = x;
            if (y > max.Y)
                max.Y = y;
            if (z > max.Z)
                max.Z = z;
        }

        private bool CheckVertexLimits(int v)
        {
            GetVertex(v, out float x, out float y, out float z);

            if (x < limit_min.X || x > limit_max.X ||
               y < limit_min.Y || y > limit_max.Y ||
               z < limit_min.Z || z > limit_max.Z)
                return false;

            return true;
        }


        public void GetVertex(int i, out float x, out float y, out float z)
        {
            VerticesGet(i, out x, out y, out z);
        }

        public void GetTriangle(int i,
            out int v0, out int v1, out int v2, out TriangleType flags)
        {
            TrianglesGet(i, out v0, out v1, out v2, out flags);
        }

        public void GetTriangleVertices(int i,
                                        out float x0, out float y0, out float z0,
                                        out float x1, out float y1, out float z1,
                                        out float x2, out float y2, out float z2, out TriangleType flags)
        {
            TrianglesGet(i, out int v0, out int v1, out int v2, out flags);

            VerticesGet(v0, out x0, out y0, out z0);
            VerticesGet(v1, out x1, out y1, out z1);
            VerticesGet(v2, out x2, out y2, out z2);
        }

        [System.Runtime.CompilerServices.SkipLocalsInit]
        public void GetTriangleVertices(int i,
                                        out float x0, out float y0, out float z0,
                                        out float x1, out float y1, out float z1,
                                        out float x2, out float y2, out float z2)
        {
            TrianglesGet(i, out int v0, out int v1, out int v2, out _);

            VerticesGet(v0, out x0, out y0, out z0);
            VerticesGet(v1, out x1, out y1, out z1);
            VerticesGet(v2, out x2, out y2, out z2);
        }

        [System.Runtime.CompilerServices.SkipLocalsInit]
        private void VerticesGet(int index, out float x, out float y, out float z)
        {
            var local = Vertecies;
            Vector3 v = local[index];
            x = v.X;
            y = v.Y;
            z = v.Z;
        }

        private void VerticesSet(int index, float x, float y, float z)
        {
            Vertecies.Insert(index, new(x, y, z));
        }


        [System.Runtime.CompilerServices.SkipLocalsInit]
        private void TrianglesGet(int index, out int v0, out int v1, out int v2, out TriangleType flags)
        {
            var local = Triangles;
            Triangle<int> t = local[index];
            (v0, v1, v2, flags) = t;
        }

        private void TrianglesSet(int index, int v0, int v1, int v2, TriangleType flags)
        {
            Triangles.Insert(index, new(v0, v1, v2, flags));
        }
    }
}