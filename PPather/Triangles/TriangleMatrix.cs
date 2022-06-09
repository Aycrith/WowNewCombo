/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using static System.MathF;

namespace WowTriangles
{
    public class TriangleMatrix
    {
        private const float resolution = 2.0f;
        private readonly SparseFloatMatrix2D<List<int>> matrix;
        private readonly int maxAtOne;

        public TriangleMatrix(TriangleCollection tc, ILogger logger)
        {
            DateTime pre = DateTime.UtcNow;

            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"Build hash for {tc.TriangleCount} triangles");

            matrix = new SparseFloatMatrix2D<List<int>>(resolution, tc.TriangleCount);

            Vector3 vertex0;
            Vector3 vertex1;
            Vector3 vertex2;

            for (int i = 0; i < tc.TriangleCount; i++)
            {
                tc.GetTriangleVertices(i,
                        out vertex0.X, out vertex0.Y, out vertex0.Z,
                        out vertex1.X, out vertex1.Y, out vertex1.Z,
                        out vertex2.X, out vertex2.Y, out vertex2.Z);

                float minx = Min(Min(vertex0.X, vertex1.X), vertex2.X);
                float maxx = Max(Max(vertex0.X, vertex1.X), vertex2.X);
                float miny = Min(Min(vertex0.Y, vertex1.Y), vertex2.Y);
                float maxy = Max(Max(vertex0.Y, vertex1.Y), vertex2.Y);

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

                        if (Utils.TestTriangleBoxIntersect(vertex0, vertex1, vertex2, box_center, box_halfsize))
                        {
                            List<int> list = matrix.Get(grid_x, grid_y);
                            if (list == null)
                            {
                                list = new();
                                matrix.Set(grid_x, grid_y, list);
                            }

                            list.Add(i);

                            if (list.Count > maxAtOne)
                                maxAtOne = list.Count;
                        }
                    }
                }
            }

            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"Build hash done {maxAtOne} - time {DateTime.UtcNow - pre}");
        }

        public ICollection<int> GetAllCloseTo(float x, float y, float distance)
        {
            HashSet<int> all = new();

            (List<int>[] close, int count) = matrix.GetAllInSquare(x - distance, y - distance, x + distance, y + distance);
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < close[i].Count; j++)
                {
                    all.Add(close[i][j]);
                }
            }

            return all;
        }

        public ICollection<int> GetAllInSquare(float x0, float y0, float x1, float y1)
        {
            HashSet<int> all = new();
            (List<int>[] close, int count) = matrix.GetAllInSquare(x0, y0, x1, y1);

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < close[i].Count; j++)
                {
                    all.Add(close[i][j]);
                }
            }
            return all;
        }
    }
}