namespace PPather.Triangles.Data
{
    public readonly struct QuadArray
    {
        private const int Q = 4;
        private const int SIZE = 256 * Q; // Max size if SIZE*SIZE = 16M

        // Jagged array
        // pointer chasing FTL

        // SIZE*(SIZE*4)
        private readonly int[][] arrays;

        public QuadArray()
        {
            arrays = new int[SIZE][];
        }

        private static void getIndices(int index, out int i0, out int i1)
        {
            i1 = index % SIZE;
            index /= SIZE;
            i0 = index % SIZE;
        }

        private void allocateAt(int i0)
        {
            int[] a1 = arrays[i0];
            if (a1 == null)
            {
                a1 = new int[SIZE * Q];
                arrays[i0] = a1;
            }
        }

        public void SetSize(int new_size)
        {
            getIndices(new_size, out int i0, out _);
            for (int i = i0 + 1; i < SIZE; i++)
                arrays[i] = null;
        }

        public void Set(int index, int x, int y, int z, int flags)
        {
            getIndices(index, out int i0, out int i1);
            allocateAt(i0);
            int[] innermost = arrays[i0];
            i1 *= Q;
            innermost[i1 + 0] = x;
            innermost[i1 + 1] = y;
            innermost[i1 + 2] = z;
            innermost[i1 + 3] = flags;
        }

        public void Get(int index, out int x, out int y, out int z, out int flags)
        {
            getIndices(index, out int i0, out int i1);

            int[] a1 = arrays[i0];
            if (a1 == null)
            {
                x = default;
                y = default;
                z = default;
                flags = default;
                return;
            }

            int[] innermost = arrays[i0];
            i1 *= Q;
            x = innermost[i1 + 0];
            y = innermost[i1 + 1];
            z = innermost[i1 + 2];
            flags = innermost[i1 + 3];
        }
    }
}
