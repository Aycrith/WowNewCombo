namespace PPather.Triangles.Data
{
    public sealed class SparseFloatMatrix3D<T> : SparseMatrix3D<T>
    {
        private const float offset = 100000f;
        private readonly float gridSize;

        public SparseFloatMatrix3D(float gridSize)
        {
            this.gridSize = gridSize;
        }

        private int LocalToGrid(float f)
        {
            return (int)((f + offset) / gridSize);
        }

        public T Get(float x, float y, float z)
        {
            return base.Get(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z));
        }

        public bool ContainsKey(float x, float y, float z)
        {
            return base.ContainsKey(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z));
        }

        public void Add(float x, float y, float z, T val)
        {
            base.Add(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z), val);
        }
    }

}
