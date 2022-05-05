/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using Microsoft.Extensions.Logging;
using PatherPath;
using System.Collections.Generic;
using System.Numerics;

namespace WowTriangles
{
    public class TriangleMatrix
    {
        private float resolution = 2.0f;
        private SparseFloatMatrix2D<List<int>> matrix;
        private int maxAtOne;

        private void AddTriangleAt(float x, float y, int triangle)
        {
            List<int> l = matrix.Get(x, y);
            if (l == null)
            {
                l = new List<int>(8); // hmm
                l.Add(triangle);

                matrix.Set(x, y, l);
            }
            else
            {
                l.Add(triangle);
            }

            if (l.Count > maxAtOne)
                maxAtOne = l.Count;
        }

        private readonly ILogger logger;

        public TriangleMatrix(TriangleCollection tc, ILogger logger)
        {
            this.logger = logger;

            System.DateTime pre = System.DateTime.UtcNow;
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Build hash  " + tc.GetNumberOfTriangles());
            matrix = new SparseFloatMatrix2D<List<int>>(resolution, tc.GetNumberOfTriangles());

            Vector3 vertex0;
            Vector3 vertex1;
            Vector3 vertex2;

            for (int i = 0; i < tc.GetNumberOfTriangles(); i++)
            {
                tc.GetTriangleVertices(i,
                        out vertex0.X, out vertex0.Y, out vertex0.Z,
                        out vertex1.X, out vertex1.Y, out vertex1.Z,
                        out vertex2.X, out vertex2.Y, out vertex2.Z);

                float minx = Utils.min(vertex0.X, vertex1.X, vertex2.X);
                float maxx = Utils.max(vertex0.X, vertex1.X, vertex2.X);
                float miny = Utils.min(vertex0.Y, vertex1.Y, vertex2.Y);
                float maxy = Utils.max(vertex0.Y, vertex1.Y, vertex2.Y);

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
                    for (int y = starty; y <= endy; y++)
                    {
                        float grid_x = matrix.GridToLocal(x);
                        float grid_y = matrix.GridToLocal(y);
                        box_center.X = grid_x + resolution / 2;
                        box_center.Y = grid_y + resolution / 2;
                        box_center.Z = 0;
                        if (Utils.TestTriangleBoxIntersect(vertex0, vertex1, vertex2, box_center, box_halfsize))
                            AddTriangleAt(grid_x, grid_y, i);
                    }
            }
            System.DateTime post = System.DateTime.UtcNow;
            System.TimeSpan ts = post.Subtract(pre);
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("done " + maxAtOne + " time " + ts);
        }

        public Set<int> GetAllCloseTo(float x, float y, float distance)
        {
            List<List<int>> close = matrix.GetAllInSquare(x - distance, y - distance, x + distance, y + distance);
            Set<int> all = new Set<int>();

            foreach (List<int> l in close)
            {
                all.AddRange(l);
            }
            return all;
        }

        public ICollection<int> GetAllInSquare(float x0, float y0, float x1, float y1)
        {
            Set<int> all = new Set<int>();
            List<List<int>> close = matrix.GetAllInSquare(x0, y0, x1, y1);

            foreach (List<int> l in close)
            {
                all.AddRange(l);
            }
            return all;
        }
    }
}