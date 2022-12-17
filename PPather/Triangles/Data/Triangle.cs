namespace PPather.Triangles
{
    public readonly struct Triangle<T>
    {
        private readonly T v0;
        private readonly T v1;
        private readonly T v2;
        private readonly TriangleType Flags;

        public Triangle(T v0, T v1, T v2, TriangleType flags)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            Flags = flags;
        }

        public void Deconstruct(out T v0, out T v1, out T v2, out TriangleType flags)
        {
            v0 = this.v0;
            v1 = this.v1;
            v2 = this.v2;
            flags = Flags;
        }
    }
}
