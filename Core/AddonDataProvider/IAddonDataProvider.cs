using System;

using System.Text;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp;

namespace Core;

public interface IAddonDataProvider : IDisposable
{
    private static readonly Bgra32 firstColor = new(0, 0, 0, 255);
    private static readonly Bgra32 lastColor = new(30, 132, 129, 255);

    void UpdateData();
    void InitFrames(DataFrame[] frames);

    int[] Data { get; }
    StringBuilder TextBuilder { get; }

    [SkipLocalsInit]
    static void InternalUpdate(Image<Bgra32> bd,
        ReadOnlySpan<DataFrame> frames, Span<int> output)
    {
        ref readonly Bgra32 first = ref bd.DangerousGetPixelRowMemory(frames[0].Y)
            .Span[frames[0].X];

        ref readonly Bgra32 last = ref bd.DangerousGetPixelRowMemory(frames[^1].Y)
            .Span[frames[^1].X];

        if (!first.Equals(firstColor) ||
            !last.Equals(lastColor))
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            DataFrame frame = frames[i];

            ReadOnlySpan<Bgra32> row = bd.DangerousGetPixelRowMemory(frame.Y).Span;
            ref readonly Bgra32 pixel = ref row[frame.X];

            output[frame.Index] = pixel.B | (pixel.G << 8) | (pixel.R << 16);
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
        if (color is 0 or > 999999)
            return string.Empty;

        TextBuilder.Clear();

        int n1 = color / 10000;
        int n2 = (color / 100) % 100;
        int n3 = color % 100;

        if (n1 > 0)
            TextBuilder.Append((char)n1);

        if (n2 > 0)
            TextBuilder.Append((char)n2);

        if (n3 > 0)
            TextBuilder.Append((char)n3);

        return TextBuilder.ToString();
    }
}
