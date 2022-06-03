namespace Core
{
    public readonly struct DataFrame
    {
        public int Index { get; }
        public int X { get; }
        public int Y { get; }

        public DataFrame(int index, int x, int y)
        {
            Index = index;
            X = x;
            Y = y;
        }
    }
}