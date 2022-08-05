using System.Numerics;

namespace PPather.Triangles.Data
{
    public struct Matrix4
    {
        private readonly float[,] m = new float[4, 4];

        public Matrix4() { }

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
}
