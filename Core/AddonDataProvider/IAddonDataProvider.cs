using System.Drawing.Imaging;
using System;

using static Core.AddonDataProviderConfig;
using System.Text;
using System.Runtime.CompilerServices;

namespace Core;

public interface IAddonDataProvider : IDisposable
{
    void UpdateData();
    void InitFrames(DataFrame[] frames) { }

    int[] Data { get; }
    StringBuilder TextBuilder { get; }

    [SkipLocalsInit]
    static unsafe void InternalUpdate(BitmapData bd,
        ReadOnlySpan<DataFrame> frames, Span<int> output)
    {
        int stride = bd.Stride;

        ReadOnlySpan<byte> bitmapSpan =
            new(bd.Scan0.ToPointer(), bd.Height * stride);

        ReadOnlySpan<byte> first =
            bitmapSpan.Slice(frames[0].Y * stride + frames[0].X * BYTES_PER_PIXEL,
            BYTES_PER_PIXEL);

        ReadOnlySpan<byte> last =
            bitmapSpan.Slice(frames[^1].Y * stride + frames[^1].X * BYTES_PER_PIXEL,
            BYTES_PER_PIXEL);

        if (!first.SequenceEqual(fColor) ||
            !last.SequenceEqual(lColor))
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            DataFrame frame = frames[i];

            int yOffset = frame.Y * stride;
            int xOffset = frame.X * BYTES_PER_PIXEL;

            ReadOnlySpan<byte> pixel =
                bitmapSpan.Slice(yOffset + xOffset, BYTES_PER_PIXEL);

            output[frame.Index] = pixel[0] | (pixel[1] << 8) | (pixel[2] << 16);
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

        return TextBuilder.ToString();
    }
}
