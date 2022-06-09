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

    internal unsafe class ccode
    {
        // int triBoxOverlap(float boxcenter[3],float boxhalfsize[3],float triverts[3][3]);
        [DllImport("MPQ\\ccode.dll")]
        public static extern int triBoxOverlap(
            float* boxcenter,
            float* boxhalfsize,
            float* trivert0,
            float* trivert1,
            float* trivert2
            );
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

        public static bool TestTriangleBoxIntersect(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2,
                                                    Vector3 boxcenter, Vector3 boxhalfsize)
        {
            int i = 0;
            float* pcenter = (float*)&boxcenter;
            float* phalf = (float*)&boxhalfsize;
            float* ptriangle0 = (float*)&vertex0;
            float* ptriangle1 = (float*)&vertex1;
            float* ptriangle2 = (float*)&vertex2;

            //int triBoxOverlap(float boxcenter[3],float boxhalfsize[3],float triverts[3][3]);
            try
            {
                i = ccode.triBoxOverlap(pcenter, phalf, ptriangle0, ptriangle1, ptriangle2);
            }
            catch (Exception e)
            {
                Console.WriteLine("WTF " + e);
            }
            if (i == 1) return true;
            return false;
            /*
            Vector min, max;
            min.x = ((vertex0.x < vertex1.x && vertex0.x < vertex2.x) ? vertex0.x : ((vertex1.x < vertex2.x) ? vertex1.x : vertex2.x));
            min.y = ((vertex0.y < vertex1.y && vertex0.y < vertex2.y) ? vertex0.y : ((vertex1.y < vertex2.y) ? vertex1.y : vertex2.y));
            min.z = ((vertex0.z < vertex1.z && vertex0.z < vertex2.z) ? vertex0.z : ((vertex1.z < vertex2.z) ? vertex1.z : vertex2.z));

            max.x = ((vertex0.x > vertex1.x && vertex0.x > vertex2.x) ? vertex0.x : ((vertex1.x > vertex2.x) ? vertex1.x : vertex2.x));
            max.y = ((vertex0.y > vertex1.y && vertex0.y > vertex2.y) ? vertex0.y : ((vertex1.y > vertex2.y) ? vertex1.y : vertex2.y));
            max.z = ((vertex0.z > vertex1.z && vertex0.z > vertex2.z) ? vertex0.z : ((vertex1.z > vertex2.z) ? vertex1.z : vertex2.z));

            bool outside = false;
            if (min.x > boxcenter.x + boxhalfsize.x) outside = true;
            if (max.x < boxcenter.x - boxhalfsize.x) outside = true;

            if (min.y > boxcenter.y + boxhalfsize.y) outside = true;
            if (max.y < boxcenter.y - boxhalfsize.y) outside = true;

            if (min.z > boxcenter.z + boxhalfsize.z) outside = true;
            if (max.z < boxcenter.z - boxhalfsize.z) outside = true;

            return !outside;*/
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