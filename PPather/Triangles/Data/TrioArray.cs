
namespace PPather.Triangles.Data
{
    public readonly struct TrioArray
    {
        private const int VERTEX = 3;
        private const int SIZE = 1024; // Max size if SIZE*SIZE = 16M

        // Jagged array
        // pointer chasing FTL

        // SIZE*(SIZE*VERTEX)
        private readonly float[][] arrays;

        public TrioArray()
        {
            arrays = new float[SIZE][];
        }

        private static void getIndices(int index, out int i0, out int i1)
        {
            i1 = index % SIZE;
            index /= SIZE;
            i0 = index % SIZE;
        }

        private void allocateAt(int i0)
        {
            float[] a1 = arrays[i0];
            if (a1 == null)
            {
                arrays[i0] = new float[SIZE * VERTEX];
            }
        }

        public void SetSize(int new_size)
        {
            getIndices(new_size, out int i0, out _);
            for (int i = i0 + 1; i < SIZE; i++)
                arrays[i] = null;
        }

        public void Set(int index, float x, float y, float z)
        {
            getIndices(index, out int i0, out int i1);
            allocateAt(i0);
            float[] innermost = arrays[i0];
            i1 *= VERTEX;
            innermost[i1 + 0] = x;
            innermost[i1 + 1] = y;
            innermost[i1 + 2] = z;
        }

        public void Get(int index, out float x, out float y, out float z)
        {
            getIndices(index, out int i0, out int i1);

            float[] a1 = arrays[i0];
            if (a1 == null)
            {
                x = default;
                y = default;
                z = default;
                return;
            }

            float[] innermost = arrays[i0];
            i1 *= VERTEX;
            x = innermost[i1 + 0];
            y = innermost[i1 + 1];
            z = innermost[i1 + 2];
        }
    }

}
