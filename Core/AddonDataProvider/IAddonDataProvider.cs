using System.Drawing.Imaging;
using System;

using static Core.AddonDataProviderConfig;

namespace Core;

public interface IAddonDataProvider
{
    void Update();
    void InitFrames(DataFrame[] frames) { }

    int GetInt(int index);
    float GetFixed(int index);
    string GetString(int index);

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
}
