using System;
using System.Drawing;
using Newtonsoft.Json;

namespace Core
{
    public readonly struct DataFrameMeta : IEquatable<DataFrameMeta>
    {
        [JsonIgnore]
        public static DataFrameMeta Empty { get; } = new(-1, 0, 0, 0, 0);

        [JsonConstructor]
        public DataFrameMeta(int hash, int spacing, int size, int rows, int frames)
        {
            this.hash = hash;
            this.spacing = spacing;
            this.size = size;
            this.rows = rows;
            this.frames = frames;
        }

        public int hash { get; }

        public int spacing { get; }

        public int size { get; }

        public int rows { get; }

        public int frames { get; }

        public Size EstimatedSize(Rectangle screenRect)
        {
            const int error = 2;

            int squareSize = size + error + (spacing != 0 ? spacing + error : 0);
            if (squareSize <= 0)
                return Size.Empty;

            SizeF estimatedSize = new((float)Math.Ceiling(frames / (float)rows) * squareSize, rows * squareSize);

            if (estimatedSize.Width > screenRect.Width ||
                estimatedSize.Height > screenRect.Height)
            {
                return Size.Empty;
            }

            return estimatedSize.ToSize();
        }

        public override int GetHashCode()
        {
            return hash;
        }

        public static bool operator ==(DataFrameMeta left, DataFrameMeta right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DataFrameMeta left, DataFrameMeta right)
        {
            return !(left == right);
        }

        public bool Equals(DataFrameMeta other)
        {
            return other.hash == hash &&
                other.spacing == spacing &&
                other.size == size &&
                other.rows == rows &&
                other.frames == frames;
        }

        public override bool Equals(object? obj)
        {
            return obj is DataFrameMeta && Equals((DataFrameMeta)obj);
        }

        public override string ToString()
        {
            return $"hash: {hash} | spacing: {spacing} | size: {size} | rows: {rows} | frames: {frames}";
        }
    }
}
