/*
  This file is part of ppather.

    PPather is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PPather is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with ppather.  If not, see <http://www.gnu.org/licenses/>.

    Copyright Pontus Borg 2008

 */

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using static System.MathF;
using static System.Numerics.Vector3;

namespace WowTriangles;

public static class Utils
{
    [SkipLocalsInit]
    public static bool SegmentTriangleIntersect(
        in Vector3 p0, in Vector3 p1,
        in Vector3 t0, in Vector3 t1, in Vector3 t2,
        out Vector3 I)
    {
        Vector3 u = Subtract(t1, t0); // triangle vector 1
        Vector3 v = Subtract(t2, t0); // triangle vector 2
        Vector3 n = Cross(u, v); // triangle normal

        Vector3 dir = Subtract(p1, p0); // ray direction vector
        Vector3 w0 = Subtract(p0, t0);
        float a = -Dot(n, w0);
        float b = Dot(n, dir);
        if (Abs(b) < float.Epsilon)
        {
            I = default;
            return false; // parallel
        }

        // get intersect point of ray with triangle plane
        float r = a / b;
        if (r < 0.0f)
        {
            I = default;
            return false; // "before" p0
        }
        if (r > 1.0f)
        {
            I = default;
            return false; // "after" p1
        }

        Vector3 M = Multiply(dir, r);
        I = Add(p0, M);// intersect point of line and plane

        // is I inside T?
        float uu = Dot(u, u);
        float uv = Dot(u, v);
        float vv = Dot(v, v);
        Vector3 w = Subtract(I, t0);
        float wu = Dot(w, u);
        float wv = Dot(w, v);
        float D = uv * uv - uu * vv;

        // get and test parametric coords
        float s = (uv * wv - vv * wu) / D;
        if (s < 0.0f || s > 1.0f)        // I is outside T
            return false;

        float t = (uv * wu - uu * wv) / D;
        if (t < 0.0f || (s + t) > 1.0f)  // I is outside T
            return false;

        return true;
    }

    [SkipLocalsInit]
    public static float PointDistanceToSegment(
        in Vector3 p0,
        in Vector3 x1, in Vector3 x2)
    {
        Vector3 L = Subtract(x2, x1); // the segment vector
        float l2 = Dot(L, L);   // square length of the segment

        Vector3 D = Subtract(p0, x1);   // vector from point to segment start
        float d = Dot(D, L);     // projection factor [x2-x1].[p0-x1]

        if (d < 0.0f) // closest to x1
            return D.Length();

        float eDotL = Dot(D - (L * (d / l2)), L);

        return eDotL > l2
            ? (D - L).Length()
            : (D - (L * (d / l2))).Length();
    }

    [SkipLocalsInit]
    public static void GetTriangleNormal(
        in Vector3 t0, in Vector3 t1, in Vector3 t2, out Vector3 normal)
    {
        normal = Normalize(Cross(t1 - t0, t2 - t0));
    }

    [SkipLocalsInit]
    public static float PointDistanceToTriangle(
        in Vector3 p0,
        in Vector3 t0, in Vector3 t1, in Vector3 t2)
    {
        Vector3 u = Subtract(t1, t0); // triangle vector 1
        Vector3 v = Subtract(t2, t0); // triangle vector 2
        Vector3 n = Cross(u, v); // triangle normal
        n.X *= -1E6f;
        n.Y *= -1E6f;
        n.Z *= -1E6f;

        if (SegmentTriangleIntersect(p0, n, t0, t1, t2, out Vector3 intersect))
        {
            return Subtract(intersect, p0).Length();
        }

        float d0 = PointDistanceToSegment(p0, t0, t1);
        float d1 = PointDistanceToSegment(p0, t1, t2);
        float d2 = PointDistanceToSegment(p0, t2, t0);

        return Min(Min(d0, d1), d2);
    }

    // From the book "Real-Time Collision Detection" by Christer Ericson, page 169
    // See also the published Errata
    // http://realtimecollisiondetection.net/books/rtcd/errata/
    [SkipLocalsInit]
    public static bool TriangleBoxIntersect(
        in Vector3 a, in Vector3 b, in Vector3 c,
        in Vector3 boxCenter, in Vector3 boxExtents)
    {
        Vector3 v0 = a - boxCenter;
        Vector3 v1 = b - boxCenter;
        Vector3 v2 = c - boxCenter;

        Vector3 f0 = v1 - v0;
        Vector3 f1 = v2 - v1;
        Vector3 f2 = v0 - v2;

        return
            AxesIntersectTriangleBox(v0, v1, v2, boxExtents, f0, f1, f2) &&
            TriangleVerticesInsideBox(v0, v1, v2, boxExtents) &&
            TrianglePlaneIntersectBox(f0, f1, v0, boxExtents);
    }

    [SkipLocalsInit]
    private static bool AxesIntersectTriangleBox(
        in Vector3 v0, in Vector3 v1, in Vector3 v2,
        in Vector3 boxExtents,
        in Vector3 f0, in Vector3 f1, in Vector3 f2)
    {
        float r;

        ReadOnlySpan<Vector3> axes = stackalloc Vector3[]
        {
            new(0, -f0.Z, f0.Y),
            new(0, -f1.Z, f1.Y),
            new(0, -f2.Z, f2.Y),

            new(f0.Z, 0, -f0.X),
            new(f1.Z, 0, -f1.X),
            new(f2.Z, 0, -f2.X),

            new(-f0.Y, f0.X, 0),
            new(-f1.Y, f1.X, 0),
            new(-f2.Y, f2.X, 0)
        };

        for (int i = 0; i < axes.Length; i++)
        {
            Vector3 axis = axes[i];

            float p0 = Dot(v0, axis);
            float p1 = Dot(v1, axis);
            float p2 = Dot(v2, axis);

            r =
                (boxExtents.X * Abs(axis.X)) +
                (boxExtents.Y * Abs(axis.Y)) +
                (boxExtents.Z * Abs(axis.Z));

            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }
        }

        return true;
    }

    [SkipLocalsInit]
    private static bool TriangleVerticesInsideBox(
        in Vector3 v0, in Vector3 v1, in Vector3 v2,
        in Vector3 boxExtents)
    {
        return
            !(Max3(v0.X, v1.X, v2.X) < -boxExtents.X || Min3(v0.X, v1.X, v2.X) > boxExtents.X) &&
            !(Max3(v0.Y, v1.Y, v2.Y) < -boxExtents.Y || Min3(v0.Y, v1.Y, v2.Y) > boxExtents.Y) &&
            !(Max3(v0.Z, v1.Z, v2.Z) < -boxExtents.Z || Min3(v0.Z, v1.Z, v2.Z) > boxExtents.Z);
    }

    [SkipLocalsInit]
    private static bool TrianglePlaneIntersectBox(
        in Vector3 f0, in Vector3 f1,
        in Vector3 v0,
        in Vector3 boxExtents)
    {
        Vector3 planeNormal = Cross(f0, f1);
        float planeDistance = Dot(planeNormal, v0);

        float r =
            (boxExtents.X * Abs(planeNormal.X)) +
            (boxExtents.Y * Abs(planeNormal.Y)) +
            (boxExtents.Z * Abs(planeNormal.Z));

        return planeDistance <= r;
    }

    public static float Min3(float a, float b, float c)
    {
        return Min(a, Min(b, c));
    }

    public static float Max3(float a, float b, float c)
    {
        return Max(a, Max(b, c));
    }
}