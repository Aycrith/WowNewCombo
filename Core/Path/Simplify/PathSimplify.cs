using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core
{
    public static class PathSimplify
    {
        // square distance from a WowPoint to a segment
        private static float GetSquareSegmentDistance(Vector3 p, Vector3 p1, Vector3 p2)
        {
            var x = p1.X;
            var y = p1.Y;
            var dx = p2.X - x;
            var dy = p2.Y - y;

            if (!dx.Equals(0.0) || !dy.Equals(0.0))
            {
                var t = ((p.X - x) * dx + (p.Y - y) * dy) / (dx * dx + dy * dy);

                if (t > 1)
                {
                    x = p2.X;
                    y = p2.Y;
                }
                else if (t > 0)
                {
                    x += dx * t;
                    y += dy * t;
                }
            }

            dx = p.X - x;
            dy = p.Y - y;

            return (dx * dx) + (dy * dy);
        }

        // basic distance-based simplification
        private static Span<Vector3> SimplifyRadialDistance(Vector3[] points, float sqTolerance)
        {
            Vector3[] reduced = new Vector3[points.Length];
            int j = 0;

            Vector3 prev = points[0];
            Vector3 curr = Vector3.Zero;

            for (int i = 1; i < points.Length; i++)
            {
                curr = points[i];
                if (Vector3.Distance(curr, prev) > sqTolerance)
                {
                    reduced[j++] = curr;
                    prev = curr;
                }
            }

            if (curr != Vector3.Zero && !prev.Equals(curr))
                reduced[j++] = curr;

            return reduced.AsSpan(0, j);
        }

        // simplification using optimized Douglas-Peucker algorithm with recursion elimination
        private static Vector3[] SimplifyDouglasPeucker(Vector3[] WowPoints, float sqTolerance)
        {
            var len = WowPoints.Length;
            var markers = new int?[len];
            int? first = 0;
            int? last = len - 1;
            int? index = 0;
            var stack = new List<int?>();
            var newWowPoints = new List<Vector3>();

            markers[first.Value] = markers[last.Value] = 1;

            while (last != null)
            {
                var maxSqDist = 0.0d;

                for (int? i = first + 1; first.HasValue && i < last; i++)
                {
                    var sqDist = GetSquareSegmentDistance(WowPoints[i.Value], WowPoints[first.Value], WowPoints[last.Value]);

                    if (sqDist > maxSqDist)
                    {
                        index = i;
                        maxSqDist = sqDist;
                    }
                }

                if (maxSqDist > sqTolerance)
                {
                    markers[index.Value] = 1;
                    stack.AddRange(new[] { first, index, index, last });
                }


                if (stack.Count > 0)
                {
                    last = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                }
                else
                    last = null;

                if (stack.Count > 0)
                {
                    first = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                }
                else
                    first = null;
            }

            for (var i = 0; i < len; i++)
            {
                if (markers[i] != null)
                    newWowPoints.Add(WowPoints[i]);
            }

            return newWowPoints.ToArray();
        }

        /// <summary>
        /// Simplifies a list of WowPoints to a shorter list of WowPoints.
        /// </summary>
        /// <param name="points">WowPoints original list of WowPoints</param>
        /// <param name="tolerance">Tolerance tolerance in the same measurement as the WowPoint coordinates</param>
        /// <param name="highestQuality">Enable highest quality for using Douglas-Peucker, set false for Radial-Distance algorithm</param>
        /// <returns>Simplified list of WowPoints</returns>
        public static Vector3[] Simplify(Vector3[] points, float tolerance = 0.3f, bool highestQuality = false)
        {
            if (points.Length == 0)
                return Array.Empty<Vector3>();

            float sqTolerance = tolerance * tolerance;

            if (highestQuality)
                return SimplifyDouglasPeucker(points, sqTolerance);

            Span<Vector3> reduced = SimplifyRadialDistance(points, sqTolerance);
            return SimplifyDouglasPeucker(reduced.ToArray(), sqTolerance);
        }

        /// <summary>
        /// Simplifies a list of WowPoints to a shorter list of WowPoints.
        /// </summary>
        /// <param name="WowPoints">WowPoints original list of WowPoints</param>
        /// <param name="tolerance">Tolerance tolerance in the same measurement as the WowPoint coordinates</param>
        /// <param name="highestQuality">Enable highest quality for using Douglas-Peucker, set false for Radial-Distance algorithm</param>
        /// <returns>Simplified list of WowPoints</returns>
        public static Vector3[] SimplifyArray(Vector3[] WowPoints, float tolerance = 0.3f, bool highestQuality = false)
        {
            return Simplify(WowPoints, tolerance, highestQuality);
        }
    }
}
