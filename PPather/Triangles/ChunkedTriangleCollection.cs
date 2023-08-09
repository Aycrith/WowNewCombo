/*
 *  Part of PPather
 *  Copyright Pontus Borg 2008
 *
 */

using System;
using System.Numerics;
using Wmo;
using Microsoft.Extensions.Logging;
using PPather.Graph;
using static System.MathF;
using static WowTriangles.Utils;
using PPather.Triangles.Data;
using PPather;
using System.Runtime.CompilerServices;

namespace WowTriangles;

/// <summary>
/// A chunked collection of triangles
/// </summary>
public sealed class ChunkedTriangleCollection
{
    private readonly ILogger logger;
    private readonly MPQTriangleSupplier supplier;
    private readonly SparseMatrix2D<TriangleCollection> chunks;

    private const int maxCache = 128;
    public Action<ChunkEventArgs> NotifyChunkAdded;

    public ChunkedTriangleCollection(ILogger logger, int initCapacity, MPQTriangleSupplier supplier)
    {
        this.logger = logger;
        this.supplier = supplier;
        chunks = new SparseMatrix2D<TriangleCollection>(initCapacity);
    }

    public void Close()
    {
        NotifyChunkAdded = null;
        supplier.Clear();
        EvictAll();
    }

    public void EvictAll()
    {
        foreach (TriangleCollection chunk in chunks.GetAllElements())
        {
            chunk.Clear();
        }

        chunks.Clear();
    }

    public static void GetGridStartAt(float x, float y, out int grid_x, out int grid_y)
    {
        x = ChunkReader.ZEROPOINT - x;
        grid_x = (int)(x / ChunkReader.TILESIZE);
        y = ChunkReader.ZEROPOINT - y;
        grid_y = (int)(y / ChunkReader.TILESIZE);
    }

    private static void GetGridLimits(int grid_x, int grid_y,
                                out float min_x, out float min_y,
                                out float max_x, out float max_y)
    {
        max_x = ChunkReader.ZEROPOINT - (grid_x * ChunkReader.TILESIZE);
        min_x = max_x - ChunkReader.TILESIZE;
        max_y = ChunkReader.ZEROPOINT - (grid_y * ChunkReader.TILESIZE);
        min_y = max_y - ChunkReader.TILESIZE;
    }

    private void LoadChunkAt(float x, float y)
    {
        GetGridStartAt(x, y, out int grid_x, out int grid_y);

        if (chunks.ContainsKey(grid_x, grid_y))
            return;

        GetGridLimits(grid_x, grid_y, out float min_x, out float min_y, out float max_x, out float max_y);

        TriangleCollection tc = new(logger);
        tc.SetLimits(min_x - 1, min_y - 1, -1E30f, max_x + 1, max_y + 1, 1E30f);

        supplier.GetTriangles(tc, min_x, min_y, max_x, max_y);

        chunks.Add(grid_x, grid_y, tc);

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace($"Grid [{grid_x},{grid_y}] Bounds: [{min_x:F4}, {min_y:F4}] [{max_x:F4}, {max_y:F4}] [{x}, {y}] - Count: {chunks.Count}");
        }

        NotifyChunkAdded?.Invoke(new ChunkEventArgs(grid_x, grid_y));
    }

    public TriangleCollection GetChunkAt(float x, float y)
    {
        LoadChunkAt(x, y);
        GetGridStartAt(x, y, out int grid_x, out int grid_y);

        return GetChunkAt(grid_x, grid_y);
    }

    public TriangleCollection GetChunkAt(int grid_x, int grid_y)
    {
        return chunks.TryGetValue(grid_x, grid_y, out TriangleCollection tc)
            ? tc
            : default;
    }

    [SkipLocalsInit]
    public bool IsSpotBlocked(float x, float y, float z,
                              float toonHeight, float toonSize)
    {
        TriangleCollection tc = GetChunkAt(x, y);
        TriangleMatrix tm = tc.GetTriangleMatrix();
        ReadOnlySpan<int> ts = tm.GetAllCloseTo(x, y, toonSize);

        Vector3 toon = new(x, y, z + toonHeight - toonSize);

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        for (int i = 0; i < ts.Length; i++)
        {
            int t = ts[i];

            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z, out _);

            float d = PointDistanceToTriangle(toon, v0, v1, v2);
            if (d < toonSize / 2)
                return true;
        }

        return false;
    }

    [SkipLocalsInit]
    public bool IsStepBlocked(float x0, float y0, float z0,
                              float x1, float y1, float z1,
                              float toonHeight, float toonSize)
    {
        TriangleCollection tc = GetChunkAt(x0, y0);

        float dx = x0 - x1;
        float dy = y0 - y1;
        float dz = z0 - z1;
        float stepLength = Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        // 1: check steepness

        // 2: check is there is a big step

        float mid_x = (x0 + x1) / 2.0f;
        float mid_y = (y0 + y1) / 2.0f;
        float mid_z = (z0 + z1) / 2.0f;
        float mid_z_hit;
        float mid_dz = Abs(stepLength);
        //if (mid_dz < 1.0f) mid_dz = 1.0f;
        if (FindStandableAt(mid_x, mid_y, mid_z - mid_dz, mid_z + mid_dz, out mid_z_hit, out _, toonHeight, toonSize))
        {
            float dz0 = Abs(z0 - mid_z_hit);
            float dz1 = Abs(z1 - mid_z_hit);

            // Console.WriteLine("z0 " + z0 + " z1 " + z1 + " dz0 " + dz0+ " dz1 " + dz1 );
            if (dz0 > stepLength / 2.0f && dz0 > 1.0f)
                return true; // too steep

            if (dz1 > stepLength / 2.0f && dz1 > 1.0f)
                return true; // too steep
        }
        else
        {
            // bad!
            return true;
        }

        // 3: check collision with objects

        Vector3 from, from_up, from_low;
        Vector3 to, to_up, to_low;

        from.X = x0;
        from.Y = y0;
        from.Z = z0 + toonSize; //+0.5

        to.X = x1;
        to.Y = y1;
        to.Z = z1 + toonSize;

        from_up = new Vector3(from.X, from.Y, from.Z);
        from_up.Z = z0 + toonHeight - toonSize;

        to_up = new Vector3(to.X, from.Y, from.Z);
        to_up.Z = z1 + toonHeight - toonSize;

        TriangleMatrix tm = tc.GetTriangleMatrix();
        ReadOnlySpan<int> ts = tm.GetAllInSquare(Min(x0, x1), Min(y0, y1), Max(x0, x1), Max(y0, y1));

        //diagonal
        if (CheckForCollision(tc, ts, from, to_up))
        {
            return true;
        }

        //diagonal
        if (CheckForCollision(tc, ts, from_up, to))
        {
            return true;
        }

        //head height
        // if (CheckForCollision(tc, ts, ref from_up, ref to_up)) { return true; }

        //close to the ground
        const float stepDistance = 0.4f;

        from_low = new Vector3(from.X, from.Y, from.Z);
        from_low.Z = z0 + stepDistance;

        to_low = new Vector3(to.X, to.Y, to.Z);
        to_low.Z = z1 + stepDistance;

        if (CheckForCollision(tc, ts, from_low, to_low))
        {
            return true;
        }

        GetNormal(x0, y0, x1, y1, out float ddx, out float ddy, 0.2f);

        from_low.X += ddy;
        from_low.Y += ddx;
        to_low.X += ddy;
        to_low.Y += ddx;

        if (CheckForCollision(tc, ts, from_low, to_low))
        {
            return true;
        }

        from_low.X -= 2 * ddy;
        from_low.Y -= 2 * ddx;
        to_low.X -= 2 * ddy;
        to_low.Y -= 2 * ddx;

        return CheckForCollision(tc, ts, from_low, to_low);
    }

    public static void GetNormal(float x1, float y1, float x2, float y2, out float dx, out float dy, float factor)
    {
        dx = x2 - x1;
        dy = y2 - y1;

        if (Abs(dx) > Abs(dy))
        {
            dy /= dx;
            dx = 1;
        }
        else
        {
            dx /= dy;
            dy = 1;
        }

        dx *= factor;
        dy *= factor;
    }

    [SkipLocalsInit]
    private static bool CheckForCollision(TriangleCollection tc, ReadOnlySpan<int> ts, in Vector3 from, in Vector3 to)
    {
        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        for (int i = 0; i < ts.Length; i++)
        {
            int t = ts[i];

            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z);

            if (SegmentTriangleIntersect(from, to, v0, v1, v2, out _))
            {
                return true;
            }
        }
        return false;
    }

    public bool FindStandableAt(float x, float y, float min_z, float max_z,
                               out float z0, out TriangleType flags, float toonHeight, float toonSize)
    {
        return FindStandableAt1(x, y, min_z, max_z,
            out z0, out flags, toonHeight, toonSize, false,
            TriangleType.Terrain | TriangleType.Water | TriangleType.Model | TriangleType.Object);
    }

    public bool IsInWater(float x, float y, float min_z, float max_z)
    {
        TriangleCollection tc = GetChunkAt(x, y);
        TriangleMatrix tm = tc.GetTriangleMatrix();
        ReadOnlySpan<int> ts = tm.GetAllCloseTo(x, y, 1.0f);

        Vector3 s0 = new(x, y, min_z);
        Vector3 s1 = new(x, y, max_z);

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        for (int i = 0; i < ts.Length; i++)
        {
            int t = ts[i];

            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z,
                    out TriangleType t_flags);

            GetTriangleNormal(v0, v1, v2, out _);

            if (SegmentTriangleIntersect(s0, s1, v0, v1, v2, out _))
            {
                if ((t_flags & TriangleType.Water) != 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [SkipLocalsInit]
    public int GradiantScore(float x, float y, float z, float range)
    {
        TriangleCollection tc = GetChunkAt(x, y);
        TriangleMatrix tm = tc.GetTriangleMatrix();

        float maxZ = float.MinValue;
        float minZ = float.MaxValue;

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        ReadOnlySpan<int> array = tm.GetAllCloseTo(x, y, range);
        for (int i = 0; i < array.Length; i++)
        {
            int t = array[i];

            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z, out TriangleType flags);

            if (flags == TriangleType.Terrain)
            {
                if (v0.Z > maxZ) { maxZ = v0.Z; }
                if (v2.Z > maxZ) { maxZ = v1.Z; }
                if (v1.Z > maxZ) { maxZ = v2.Z; }

                if (v0.Z < minZ) { minZ = v0.Z; }
                if (v2.Z < minZ) { minZ = v1.Z; }
                if (v1.Z < minZ) { minZ = v2.Z; }
            }
        }
        int g = (int)(maxZ - minZ);
        if (g > 10)
        {
            g = 10;
        }
        return g;
    }

    [SkipLocalsInit]
    public bool IsCloseToModel(float x, float y, float z, float range)
    {
        TriangleCollection tc = GetChunkAt(x, y);
        TriangleMatrix tm = tc.GetTriangleMatrix();

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        ReadOnlySpan<int> array = tm.GetAllCloseTo(x, y, range);
        for (int i = 0; i < array.Length; i++)
        {
            int t = array[i];

            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z,
                    out TriangleType flags);

            //check triangle is part of a model
            if ((flags & TriangleType.Object) != 0 || (flags & TriangleType.Model) != 0)
            {
                const float minHeight = 0.75f;
                const float height = 2;

                //and the vertex is close to the char
                if ((v0.Z > z + minHeight && v0.Z < z + height) ||
                    (v1.Z > z + minHeight && v1.Z < z + height) ||
                    (v2.Z > z + minHeight && v2.Z < z + height))
                {
                    return true;
                }
            }
        }
        return false;
    }

    [SkipLocalsInit]
    public bool LineOfSightExists(Spot a, Spot b)
    {
        TriangleCollection tc = GetChunkAt(a.Loc.X, a.Loc.Y);
        TriangleMatrix tm = tc.GetTriangleMatrix();
        ReadOnlySpan<int> ts = tm.GetAllCloseTo(a.Loc.X, a.Loc.Y, a.GetDistanceTo(b) + 1);

        Vector3 s0 = a.Loc;
        Vector3 s1 = b.Loc;

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        for (int i = 0; i < ts.Length; i++)
        {
            int t = ts[i];
            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z, out _);

            if (SegmentTriangleIntersect(s0, s1, v0, v1, v2, out _))
            {
                return false;
            }
        }
        return true;
    }

    [SkipLocalsInit]
    public bool FindStandableAt1(float x, float y, float min_z, float max_z,
                               out float z0, out TriangleType flags,
                               float toonHeight, float toonSize,
                               bool IgnoreGradient, TriangleType allowedFlags)
    {
        TriangleCollection tc = GetChunkAt(x, y);
        TriangleMatrix tm = tc.GetTriangleMatrix();
        ReadOnlySpan<int> ts = tm.GetAllCloseTo(x, y, 1.0f);

        float hint_z = (max_z + min_z) / 2f;

        Vector3 s0 = new(x, y, min_z);
        Vector3 s1 = new(x, y, max_z);

        float best_z = float.MinValue;
        TriangleType best_flags = TriangleType.None;

        // 45f -> 40 degree || 60f -> 50 degree || 30f -> 28.6 degree
        float angle_z = Sin(30f / 360.0f * Tau);

        Vector3 v0;
        Vector3 v1;
        Vector3 v2;

        for (int i = 0; i < ts.Length; i++)
        {
            int t = ts[i];
            tc.GetTriangleVertices(t,
                    out v0.X, out v0.Y, out v0.Z,
                    out v1.X, out v1.Y, out v1.Z,
                    out v2.X, out v2.Y, out v2.Z,
                    out TriangleType t_flags);

            if (!allowedFlags.Has(t_flags))
                continue;

            GetTriangleNormal(v0, v1, v2, out Vector3 normal);
            if (Abs(normal.Z) <= angle_z)
                continue;

            if (!SegmentTriangleIntersect(s0, s1, v0, v1, v2,
                out Vector3 intersect))
                continue;

            float b = Abs(intersect.Z - best_z);
            float h = Abs(intersect.Z - hint_z);

            if (h < b && !IsSpotBlocked(
                    intersect.X, intersect.Y, intersect.Z,
                    toonHeight, toonSize))
            {
                if (best_z == float.MinValue)
                {
                    best_z = intersect.Z;
                    best_flags = t_flags;
                }
                else if (b < Abs(best_z - hint_z))
                {
                    best_z = intersect.Z;
                    best_flags = t_flags;
                }
            }
        }

        z0 = best_z;
        flags = best_flags;

        const bool nearCliffCheck = false;

        if (nearCliffCheck && best_flags != TriangleType.None)
        {
            Vector3 up, dn;
            up.Z = best_z + 2;
            dn.Z = best_z - 5;

            const float minCliffD = 0.5f;

            const int size = 4;
            Span<bool> nearCliff = stackalloc bool[size] { true, true, true, true };
            Span<float> dx = stackalloc float[size] { minCliffD, -minCliffD, 0, 0 };
            Span<float> dy = stackalloc float[size] { 0, 0, minCliffD, -minCliffD };

            bool allGood;
            for (int j = 0; j < ts.Length; j++)
            {
                int t = ts[j];

                tc.GetTriangleVertices(t,
                        out v0.X, out v0.Y, out v0.Z,
                        out v1.X, out v1.Y, out v1.Z,
                        out v2.X, out v2.Y, out v2.Z);

                allGood = true;
                for (int i = 0; i < size; i++)
                {
                    if (nearCliff[i])
                    {
                        up.X = dn.X = x + dx[i];
                        up.Y = dn.Y = y + dy[i];
                        if (SegmentTriangleIntersect(up, dn, v0, v1, v2, out _))
                            nearCliff[i] = false;
                    }
                    allGood &= !nearCliff[i];
                }
                if (allGood)
                    break;
            }

            allGood = true;
            for (int i = 0; i < size; i++)
                allGood &= !nearCliff[i];
            if (!allGood)
                return false; // too close to cliff
        }

        return best_flags != TriangleType.None;
    }
}