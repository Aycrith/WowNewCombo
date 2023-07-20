using System.Drawing.Imaging;
using System;

using static Core.AddonDataProviderConfig;
using System.Text;

namespace Core;

public interface IAddonDataProvider
{
    void Update();
    void InitFrames(DataFrame[] frames) { }

    int[] Data { get; }
    StringBuilder TextBuilder { get; }

    void Dispose();

    static unsafe void InternalUpdate(BitmapData bd,
        ReadOnlySpan<DataFrame> frames, Span<int> output)
    {
        ReadOnlySpan<byte> first = new(
            (byte*)bd.Scan0 + (frames[0].Y * bd.Stride) +
            (frames[0].X * BYTES_PER_PIXEL),
            BYTES_PER_PIXEL);

        ReadOnlySpan<byte> last = new(
            (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride) +
            (frames[^1].X * BYTES_PER_PIXEL),
            BYTES_PER_PIXEL);

        if (!first.SequenceEqual(fColor) ||
            !last.SequenceEqual(lColor))
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            DataFrame frame = frames[i];

            byte* y = (byte*)bd.Scan0 + (frame.Y * bd.Stride);
            int x = frame.X * BYTES_PER_PIXEL;

            output[frame.Index] = y[x] | (y[x + 1] << 8) | (y[x + 2] << 16);
        }
    }

    int GetInt(int index)
    {
        return Data[index];
    }

    float GetFixed(int index)
    {
        return Data[index] / 100000f;
    }

    string GetString(int index)
    {
        int color = GetInt(index);
        if (color == 0 || color > 999999)
            return string.Empty;

        TextBuilder.Clear();

        int n = color / 10000;
        if (n > 0) TextBuilder.Append((char)n);

        n = color / 100 % 100;
        if (n > 0) TextBuilder.Append((char)n);

        n = color % 100;
        if (n > 0) TextBuilder.Append((char)n);

        return TextBuilder.ToString().Trim();
    }
}
