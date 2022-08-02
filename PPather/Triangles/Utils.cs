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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static System.MathF;
using static System.Numerics.Vector3;

namespace WowTriangles
{
    public class Matrix4
    {
        private float[,] m = new float[4, 4];

        public void makeQuaternionRotate(Quaternion q)
        {
            m[0, 0] = 1.0f - 2.0f * q.Y * q.Y - 2.0f * q.Z * q.Z;
            m[0, 1] = 2.0f * q.X * q.Y + 2.0f * q.W * q.Z;
            m[0, 2] = 2.0f * q.X * q.Z - 2.0f * q.W * q.Y;
            m[1, 0] = 2.0f * q.X * q.Y - 2.0f * q.W * q.Z;
            m[1, 1] = 1.0f - 2.0f * q.X * q.X - 2.0f * q.Z * q.Z;
            m[1, 2] = 2.0f * q.Y * q.Z + 2.0f * q.W * q.X;
            m[2, 0] = 2.0f * q.X * q.Z + 2.0f * q.W * q.Y;
            m[2, 1] = 2.0f * q.Y * q.Z - 2.0f * q.W * q.X;
            m[2, 2] = 1.0f - 2.0f * q.X * q.X - 2.0f * q.Y * q.Y;
            m[0, 3] = m[1, 3] = m[2, 3] = m[3, 0] = m[3, 1] = m[3, 2] = 0;
            m[3, 3] = 1.0f;
        }

        public Vector3 mutiply(Vector3 v)
        {
            Vector3 o;
            o.X = m[0, 0] * v.X + m[0, 1] * v.Y + m[0, 2] * v.Z + m[0, 3];
            o.Y = m[1, 0] * v.X + m[1, 1] * v.Y + m[1, 2] * v.Z + m[1, 3];
            o.Z = m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z + m[2, 3];
            return o;
        }
    }

    public unsafe class Utils
    {
        public static bool SegmentTriangleIntersect(Vector3 p0, Vector3 p1,
                                                    Vector3 t0, Vector3 t1, Vector3 t2,
                                                    out Vector3 I)
        {
            I = new();

            Vector3 u = Subtract(t1, t0); // triangle vector 1
            Vector3 v = Subtract(t2, t0); // triangle vector 2
            Vector3 n = Cross(u, v); // triangle normal

            Vector3 dir = Subtract(p1, p0); // ray direction vector
            Vector3 w0 = Subtract(p0, t0);
            float a = -Dot(n, w0);
            float b = Dot(n, dir);
            if (Abs(b) < float.Epsilon) return false; // parallel

            // get intersect point of ray with triangle plane
            float r = a / b;
            if (r < 0.0f) return false; // "before" p0
            if (r > 1.0f) return false; // "after" p1

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

        public static float PointDistanceToSegment(Vector3 p0,
                                           Vector3 x1, Vector3 x2)
        {
            Vector3 L = Subtract(x2, x1); // the segment vector
            float l2 = Dot(L, L);   // square length of the segment

            Vector3 D = Subtract(p0, x1);   // vector from point to segment start
            float d = Dot(D, L);     // projection factor [x2-x1].[p0-x1]

            if (d < 0.0f) // closest to x1
                return D.Length();

            Vector3 E = Multiply(L, d / l2); // intersect

            if (Dot(E, L) > l2) // closest to x2
            {
                Vector3 L2 = Subtract(D, L);
                return L2.Length();
            }

            Vector3 L3 = Subtract(D, E);
            return L3.Length();
        }

        public static void GetTriangleNormal(Vector3 t0, Vector3 t1, Vector3 t2, out Vector3 normal)
        {
            Vector3 u = Subtract(t1, t0); // triangle vector 1
            Vector3 v = Subtract(t2, t0); // triangle vector 2
            normal = Cross(u, v); // triangle normal
            float l = normal.Length();
            normal = Divide(normal, l);
        }

        public static float PointDistanceToTriangle(Vector3 p0,
                                                    Vector3 t0, Vector3 t1, Vector3 t2)
        {
            Vector3 u = Subtract(t1, t0); // triangle vector 1
            Vector3 v = Subtract(t2, t0); // triangle vector 2
            Vector3 n = Cross(u, v); // triangle normal
            n.X *= -1E6f;
            n.Y *= -1E6f;
            n.Z *= -1E6f;

            if (SegmentTriangleIntersect(p0, n, t0, t1, t2, out Vector3 intersect))
            {
                Vector3 L = Subtract(intersect, p0);
                return L.Length();
            }

            float d0 = PointDistanceToSegment(p0, t0, t1);
            float d1 = PointDistanceToSegment(p0, t0, t1);
            float d2 = PointDistanceToSegment(p0, t0, t1);

            return Min(Min(d0, d1), d2);
        }

        public static bool TestBoxBoxIntersect(Vector3 box0_min, Vector3 box0_max,
                                               Vector3 box1_min, Vector3 box1_max)
        {
            if (box0_min.X > box1_max.X) return false;
            if (box0_min.Y > box1_max.Y) return false;
            if (box0_min.Z > box1_max.Z) return false;

            if (box1_min.X > box0_max.X) return false;
            if (box1_min.Y > box0_max.Y) return false;
            if (box1_min.Z > box0_max.Z) return false;

            return true;
        }

        // From the book "Real-Time Collision Detection" by Christer Ericson, page 169
        // See also the published Errata at http://realtimecollisiondetection.net/books/rtcd/errata/
        public static bool TestTriangleBoxIntersect(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 boxCenter, in Vector3 boxExtents)
        {
            // Translate triangle as conceptually moving AABB to origin
            Vector3 v0 = a - boxCenter;
            Vector3 v1 = b - boxCenter;
            Vector3 v2 = c - boxCenter;

            // Compute edge vectors for triangle
            Vector3 f0 = v1 - v0;
            Vector3 f1 = v2 - v1;
            Vector3 f2 = v0 - v2;

            #region Test axes a00..a22 (category 3)

            // Test axis a00
            Vector3 a00 = new(0, -f0.Z, f0.Y);
            float p0 = Dot(v0, a00);
            float p1 = Dot(v1, a00);
            float p2 = Dot(v2, a00);
            float r = boxExtents.Y * Abs(f0.Z) + boxExtents.Z * Abs(f0.Y);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a01
            Vector3 a01 = new(0, -f1.Z, f1.Y);
            p0 = Dot(v0, a01);
            p1 = Dot(v1, a01);
            p2 = Dot(v2, a01);
            r = boxExtents.Y * Abs(f1.Z) + boxExtents.Z * Abs(f1.Y);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a02
            Vector3 a02 = new(0, -f2.Z, f2.Y);
            p0 = Dot(v0, a02);
            p1 = Dot(v1, a02);
            p2 = Dot(v2, a02);
            r = boxExtents.Y * Abs(f2.Z) + boxExtents.Z * Abs(f2.Y);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a10
            Vector3 a10 = new(f0.Z, 0, -f0.X);
            p0 = Dot(v0, a10);
            p1 = Dot(v1, a10);
            p2 = Dot(v2, a10);
            r = boxExtents.X * Abs(f0.Z) + boxExtents.Z * Abs(f0.X);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a11
            Vector3 a11 = new(f1.Z, 0, -f1.X);
            p0 = Dot(v0, a11);
            p1 = Dot(v1, a11);
            p2 = Dot(v2, a11);
            r = boxExtents.X * Abs(f1.Z) + boxExtents.Z * Abs(f1.X);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a12
            Vector3 a12 = new(f2.Z, 0, -f2.X);
            p0 = Dot(v0, a12);
            p1 = Dot(v1, a12);
            p2 = Dot(v2, a12);
            r = boxExtents.X * Abs(f2.Z) + boxExtents.Z * Abs(f2.X);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a20
            Vector3 a20 = new(-f0.Y, f0.X, 0);
            p0 = Dot(v0, a20);
            p1 = Dot(v1, a20);
            p2 = Dot(v2, a20);
            r = boxExtents.X * Abs(f0.Y) + boxExtents.Y * Abs(f0.X);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a21
            Vector3 a21 = new(-f1.Y, f1.X, 0);
            p0 = Dot(v0, a21);
            p1 = Dot(v1, a21);
            p2 = Dot(v2, a21);
            r = boxExtents.X * Abs(f1.Y) + boxExtents.Y * Abs(f1.X);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a22
            Vector3 a22 = new(-f2.Y, f2.X, 0);
            p0 = Dot(v0, a22);
            p1 = Dot(v1, a22);
            p2 = Dot(v2, a22);
            r = boxExtents.X * Abs(f2.Y) + boxExtents.Y * Abs(f2.X);
            if (Max(-Max3(p0, p1, p2), Min3(p0, p1, p2)) > r)
            {
                return false;
            }

            #endregion

            #region Test the three axes corresponding to the face normals of AABB b (category 1)

            // Exit if...
            // ... [-extents.X, extents.X] and [min(v0.X,v1.X,v2.X), max(v0.X,v1.X,v2.X)] do not overlap
            if (Max3(v0.X, v1.X, v2.X) < -boxExtents.X || Min3(v0.X, v1.X, v2.X) > boxExtents.X)
            {
                return false;
            }

            // ... [-extents.Y, extents.Y] and [min(v0.Y,v1.Y,v2.Y), max(v0.Y,v1.Y,v2.Y)] do not overlap
            if (Max3(v0.Y, v1.Y, v2.Y) < -boxExtents.Y || Min3(v0.Y, v1.Y, v2.Y) > boxExtents.Y)
            {
                return false;
            }

            // ... [-extents.Z, extents.Z] and [min(v0.Z,v1.Z,v2.Z), max(v0.Z,v1.Z,v2.Z)] do not overlap
            if (Max3(v0.Z, v1.Z, v2.Z) < -boxExtents.Z || Min3(v0.Z, v1.Z, v2.Z) > boxExtents.Z)
            {
                return false;
            }

            #endregion

            #region Test separating axis corresponding to triangle face normal (category 2)

            Vector3 planeNormal = Cross(f0, f1);
            float planeDistance = Dot(planeNormal, v0);

            // Compute the projection interval radius of b onto L(t) = b.c + t * p.n
            r = boxExtents.X * Abs(planeNormal.X)
                + boxExtents.Y * Abs(planeNormal.Y)
                + boxExtents.Z * Abs(planeNormal.Z);

            // Intersection occurs when plane distance falls within [-r,+r] interval
            if (planeDistance > r)
            {
                return false;
            }

            #endregion

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Min3(float a, float b, float c)
        {
            return Min(a, Min(b, c));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Max3(float a, float b, float c)
        {
            return Max(a, Max(b, c));
        }
    }

    public class SparseFloatMatrix3D<T> : SparseMatrix3D<T>
    {
        private const float offset = 100000f;
        private readonly float gridSize;

        public SparseFloatMatrix3D(float gridSize)
        {
            this.gridSize = gridSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LocalToGrid(float f)
        {
            return (int)((f + offset) / gridSize);
        }

        public T Get(float x, float y, float z)
        {
            return base.Get(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z));
        }

        public bool IsSet(float x, float y, float z)
        {
            return base.IsSet(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z));
        }

        public void Set(float x, float y, float z, T val)
        {
            base.Set(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z), val);
        }
    }

    public class SparseFloatMatrix2D<T> : SparseMatrix2D<T>
    {
        private const float offset = 100000f;
        private readonly float gridSize;

        public SparseFloatMatrix2D(float gridSize)
            : base(0)
        {
            this.gridSize = gridSize;
        }

        public SparseFloatMatrix2D(float gridSize, int initialCapazity)
            : base(initialCapazity)
        {
            this.gridSize = gridSize;
        }

        public float GridToLocal(int grid)
        {
            return (grid * gridSize) - offset;
        }

        public int LocalToGrid(float f)
        {
            return (int)((f + offset) / gridSize);
        }

        public (T[], int) GetAllInSquare(float min_x, float min_y,
                                      float max_x, float max_y)
        {
            int sx = LocalToGrid(min_x);
            int ex = LocalToGrid(max_x);

            int sy = LocalToGrid(min_y);
            int ey = LocalToGrid(max_y);

            T[] l = new T[(int)Math.Ceiling(((ex - sx + 1) * gridSize) + ((ey - sy + 1) * gridSize))];
            int i = 0;
            for (int x = sx; x <= ex; x++)
            {
                for (int y = sy; y <= ey; y++)
                {
                    if (base.IsSet(x, y))
                    {
                        l[i++] = base.Get(x, y);
                    }
                }
            }

            return (l, i);
        }

        public T Get(float x, float y)
        {
            return base.Get(LocalToGrid(x), LocalToGrid(y));
        }

        public void Set(float x, float y, T val)
        {
            base.Set(LocalToGrid(x), LocalToGrid(y), val);
        }
    }

    public class SparseMatrix2D<T>
    {
        private readonly Dictionary<(int x, int y), T> dict;

        public SparseMatrix2D(int initialCapacity)
        {
            dict = new(initialCapacity);
        }

        public bool HasValue(int x, int y)
        {
            return dict.ContainsKey((x, y));
        }

        public T Get(int x, int y)
        {
            T r = default;
            dict.TryGetValue((x, y), out r);
            return r;
        }

        public void Set(int x, int y, T val)
        {
            dict[(x, y)] = val;
        }

        public bool IsSet(int x, int y)
        {
            return HasValue(x, y);
        }

        public void Clear(int x, int y)
        {
            dict.Remove((x, y));
        }

        public ICollection<T> GetAllElements()
        {
            return dict.Values;
        }
    }

    public class SparseMatrix3D<T>
    {
        private readonly Dictionary<(int x, int y, int z), T> dict = new();

        public T Get(int x, int y, int z)
        {
            dict.TryGetValue((x, y, z), out T r);
            return r;
        }

        public bool IsSet(int x, int y, int z)
        {
            return dict.ContainsKey((x, y, z));
        }

        public void Set(int x, int y, int z, T val)
        {
            dict[(x, y, z)] = val;
        }

        public void Clear(int x, int y, int z)
        {
            dict.Remove((x, y, z));
        }
    }

    public class TrioArray<T>
    {
        private const int SIZE = 1024; // Max size if SIZE*SIZE = 16M

        // Jagged array
        // pointer chasing FTL

        // SIZE*(SIZE*3)
        private T[][] arrays;

        private static void getIndices(int index, out int i0, out int i1)
        {
            i1 = index % SIZE; index /= SIZE;
            i0 = index % SIZE;
        }

        private void allocateAt(int i0, int i1)
        {
            if (arrays == null)
                arrays = new T[SIZE][];

            T[] a1 = arrays[i0];
            if (a1 == null)
            {
                a1 = new T[SIZE * 3];
                arrays[i0] = a1;
            }
        }

        public void SetSize(int new_size)
        {
            if (arrays == null) return;
            getIndices(new_size, out int i0, out _);
            for (int i = i0 + 1; i < SIZE; i++)
                arrays[i] = null;
        }

        public void Set(int index, T x, T y, T z)
        {
            getIndices(index, out int i0, out int i1);
            allocateAt(i0, i1);
            T[] innermost = arrays[i0];
            i1 *= 3;
            innermost[i1 + 0] = x;
            innermost[i1 + 1] = y;
            innermost[i1 + 2] = z;
        }

        public void Get(int index, out T x, out T y, out T z)
        {
            getIndices(index, out int i0, out int i1);

            x = default;
            y = default;
            z = default;

            T[] a1 = arrays[i0];
            if (a1 == null) return;

            T[] innermost = arrays[i0];
            i1 *= 3;
            x = innermost[i1 + 0];
            y = innermost[i1 + 1];
            z = innermost[i1 + 2];
        }
    }

    public class QuadArray<T>
    {
        private const int SIZE = 512 * 5; // Max size if SIZE*SIZE = 16M

        // Jagged array
        // pointer chasing FTL

        // SIZE*(SIZE*4)
        private T[][] arrays;

        private static void getIndices(int index, out int i0, out int i1)
        {
            i1 = index % SIZE; index /= SIZE;
            i0 = index % SIZE;
        }

        private void allocateAt(int i0, int i1)
        {
            if (arrays == null) arrays = new T[SIZE][];

            T[] a1 = arrays[i0];
            if (a1 == null) { a1 = new T[SIZE * 5]; arrays[i0] = a1; }
        }

        public void SetSize(int new_size)
        {
            if (arrays == null) return;
            getIndices(new_size, out int i0, out int i1);
            for (int i = i0 + 1; i < SIZE; i++)
                arrays[i] = null;
        }

        public void Set(int index, T x, T y, T z, T w, T sequence)
        {
            getIndices(index, out int i0, out int i1);
            allocateAt(i0, i1);
            T[] innermost = arrays[i0];
            i1 *= 5;
            innermost[i1 + 0] = x;
            innermost[i1 + 1] = y;
            innermost[i1 + 2] = z;
            innermost[i1 + 3] = w;
            innermost[i1 + 4] = sequence;
        }

        public void Get(int index, out T x, out T y, out T z, out T w, out T sequence)
        {
            getIndices(index, out int i0, out int i1);

            x = default;
            y = default;
            z = default;
            w = default;
            sequence = default;

            T[] a1 = arrays[i0];
            if (a1 == null) return;

            T[] innermost = arrays[i0];
            i1 *= 5;
            x = innermost[i1 + 0];
            y = innermost[i1 + 1];
            z = innermost[i1 + 2];
            w = innermost[i1 + 3];
            sequence = innermost[i1 + 4];
        }
    }
}