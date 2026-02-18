using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Core.PathOptimization;

/// <summary>
/// Applies path simplification algorithms to reduce waypoint count
/// while preserving shape within tolerance.
/// </summary>
/// <remarks>
/// <para>
/// Implements the Ramer-Douglas-Peucker algorithm for line simplification.
/// Reference: https://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm
/// </para>
/// <para>
/// Pattern derived from: noisiver/AmeisenBotX/Engines/Movement/PathSmoothing.cs
/// </para>
/// </remarks>
public static class PathSimplifier
{
    /// <summary>
    /// Default tolerance in world units for path simplification.
    /// </summary>
    public const float DefaultTolerance = 2.0f;

    /// <summary>
    /// Simplifies a path by removing redundant waypoints using the
    /// Ramer-Douglas-Peucker algorithm.
    /// </summary>
    /// <param name="path">Original path waypoints.</param>
    /// <param name="tolerance">Maximum allowed perpendicular distance in world units.</param>
    /// <returns>Simplified path with reduced waypoint count.</returns>
    /// <remarks>
    /// Time complexity: O(n log n) average, O(n²) worst case
    /// Space complexity: O(n)
    /// </remarks>
    public static List<Vector3> Simplify(IReadOnlyList<Vector3> path, float tolerance = DefaultTolerance)
    {
        if (path.Count < 3)
            return path.ToList();

        if (tolerance <= 0)
            return path.ToList();

        return RamerDouglasPeucker(path, 0, path.Count - 1, tolerance);
    }

    /// <summary>
    /// Simplifies a path using a Span for allocation-free input.
    /// </summary>
    /// <param name="path">Original path waypoints as a span.</param>
    /// <param name="tolerance">Maximum allowed perpendicular distance.</param>
    /// <returns>Simplified path.</returns>
    public static List<Vector3> Simplify(ReadOnlySpan<Vector3> path, float tolerance = DefaultTolerance)
    {
        if (path.Length < 3)
            return path.ToArray().ToList();

        return RamerDouglasPeucker(path.ToArray(), 0, path.Length - 1, tolerance);
    }

    /// <summary>
    /// Calculates the reduction percentage after simplification.
    /// </summary>
    /// <param name="originalCount">Original waypoint count.</param>
    /// <param name="simplifiedCount">Simplified waypoint count.</param>
    /// <returns>Percentage of points removed (0-100).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateReduction(int originalCount, int simplifiedCount)
    {
        if (originalCount == 0) return 0;
        return (1.0 - (double)simplifiedCount / originalCount) * 100.0;
    }

    /// <summary>
    /// Suggests an optimal tolerance based on path characteristics.
    /// </summary>
    /// <param name="path">The path to analyze.</param>
    /// <returns>Suggested tolerance value.</returns>
    public static float SuggestTolerance(IReadOnlyList<Vector3> path)
    {
        if (path.Count < 3)
            return DefaultTolerance;

        // Calculate average segment length
        float totalLength = 0;
        for (int i = 1; i < path.Count; i++)
        {
            totalLength += Vector3.Distance(path[i - 1], path[i]);
        }

        float avgSegmentLength = totalLength / (path.Count - 1);

        // Tolerance as percentage of average segment length
        // Adjust for path complexity
        return MathF.Max(0.5f, MathF.Min(avgSegmentLength * 0.1f, 5.0f));
    }

    /// <summary>
    /// Ramer-Douglas-Peucker recursive implementation.
    /// </summary>
    private static List<Vector3> RamerDouglasPeucker(
        IReadOnlyList<Vector3> points,
        int start,
        int end,
        float epsilon)
    {
        // Find the point with maximum distance from the line segment
        float dmax = 0f;
        int index = start;

        for (int i = start + 1; i < end; i++)
        {
            float d = PerpendicularDistance(points[i], points[start], points[end]);
            if (d > dmax)
            {
                index = i;
                dmax = d;
            }
        }

        // If max distance is greater than epsilon, recursively simplify
        if (dmax > epsilon)
        {
            // Recursive call for two halves
            List<Vector3> left = RamerDouglasPeucker(points, start, index, epsilon);
            List<Vector3> right = RamerDouglasPeucker(points, index, end, epsilon);

            // Combine results, avoiding duplicate at junction point
            List<Vector3> result = new(left.Count + right.Count - 1);
            result.AddRange(left);
            result.AddRange(right.Skip(1));
            return result;
        }

        // Base case: all intermediate points are within tolerance
        return new List<Vector3> { points[start], points[end] };
    }

    /// <summary>
    /// Calculates perpendicular distance from a point to a line segment.
    /// </summary>
    /// <param name="point">The point to measure from.</param>
    /// <param name="lineStart">Start of the line segment.</param>
    /// <param name="lineEnd">End of the line segment.</param>
    /// <returns>Perpendicular distance in world units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PerpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLengthSq = line.LengthSquared();

        // Handle degenerate case where start and end are the same point
        if (lineLengthSq == 0)
            return Vector3.Distance(point, lineStart);

        // Project point onto line, clamped to segment
        // t represents how far along the line the projection falls (0 to 1)
        float t = Math.Clamp(Vector3.Dot(point - lineStart, line) / lineLengthSq, 0f, 1f);

        // Calculate the closest point on the line segment
        Vector3 projection = lineStart + t * line;

        // Return distance from point to its projection on the line
        return Vector3.Distance(point, projection);
    }

    /// <summary>
    /// Calculates the total path length.
    /// </summary>
    /// <param name="path">The path to measure.</param>
    /// <returns>Total length in world units.</returns>
    public static float CalculatePathLength(IReadOnlyList<Vector3> path)
    {
        if (path.Count < 2)
            return 0;

        float total = 0;
        for (int i = 1; i < path.Count; i++)
        {
            total += Vector3.Distance(path[i - 1], path[i]);
        }

        return total;
    }

    /// <summary>
    /// Validates that simplification preserved path integrity.
    /// </summary>
    /// <param name="original">Original path.</param>
    /// <param name="simplified">Simplified path.</param>
    /// <param name="maxDeviation">Maximum allowed deviation from original.</param>
    /// <returns>True if simplified path is valid.</returns>
    public static bool ValidateSimplification(
        IReadOnlyList<Vector3> original,
        IReadOnlyList<Vector3> simplified,
        float maxDeviation = 10.0f)
    {
        if (simplified.Count == 0)
            return original.Count == 0;

        if (simplified.Count > original.Count)
            return false;

        // Check that endpoints match
        if (Vector3.Distance(original[0], simplified[0]) > 0.01f)
            return false;

        if (Vector3.Distance(original[^1], simplified[^1]) > 0.01f)
            return false;

        // Check that no point on original path is too far from simplified path
        foreach (Vector3 point in original)
        {
            float minDist = float.MaxValue;
            for (int i = 1; i < simplified.Count; i++)
            {
                float dist = PerpendicularDistance(point, simplified[i - 1], simplified[i]);
                minDist = MathF.Min(minDist, dist);
            }

            if (minDist > maxDeviation)
                return false;
        }

        return true;
    }
}

/// <summary>
/// Result of path simplification operation.
/// </summary>
public readonly record struct PathSimplificationResult(
    List<Vector3> SimplifiedPath,
    int OriginalCount,
    int SimplifiedCount,
    float OriginalLength,
    float SimplifiedLength,
    double ReductionPercent)
{
    /// <summary>
    /// Creates a result from original and simplified paths.
    /// </summary>
    public static PathSimplificationResult Create(
        IReadOnlyList<Vector3> original,
        List<Vector3> simplified)
    {
        float originalLength = PathSimplifier.CalculatePathLength(original);
        float simplifiedLength = PathSimplifier.CalculatePathLength(simplified);
        double reduction = PathSimplifier.CalculateReduction(original.Count, simplified.Count);

        return new PathSimplificationResult(
            simplified,
            original.Count,
            simplified.Count,
            originalLength,
            simplifiedLength,
            reduction);
    }
}
