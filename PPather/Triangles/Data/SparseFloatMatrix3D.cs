namespace PPather.Triangles.Data
{
    public class SparseFloatMatrix3D<T> : SparseMatrix3D<T>
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

        public bool IsSet(float x, float y, float z)
        {
            return base.IsSet(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z));
        }

        public void Set(float x, float y, float z, T val)
        {
            base.Set(LocalToGrid(x), LocalToGrid(y), LocalToGrid(z), val);
        }
    }

}
