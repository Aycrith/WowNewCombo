using Game;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace Core;

public sealed class AddonDataProviderGDI : IAddonDataProvider, IDisposable
{
    private readonly int[] data;
    private readonly DataFrame[] frames;

    private readonly Rectangle rect;
    private readonly Bitmap bitmap;
    private readonly Graphics graphics;

    private readonly WowScreen wowScreen;

    private readonly StringBuilder sb = new(3);

    public AddonDataProviderGDI(WowScreen wowScreen, DataFrame[] frames)
    {
        this.wowScreen = wowScreen;
        this.frames = frames;

        data = new int[frames.Length];

        for (int i = 0; i < frames.Length; i++)
        {
            rect.Width = Math.Max(rect.Width, frames[i].X);
            rect.Height = Math.Max(rect.Height, frames[i].Y);
        }
        rect.Width++;
        rect.Height++;

        bitmap = new(rect.Width, rect.Height, AddonDataProviderConfig.PIXEL_FORMAT);
        graphics = Graphics.FromImage(bitmap);
    }

    public void Dispose()
    {
        graphics.Dispose();
        bitmap.Dispose();
    }

    public void Update()
    {
        Point p = new();
        wowScreen.GetPosition(ref p);
        graphics.CopyFromScreen(p, Point.Empty, rect.Size);

        unsafe
        {
            BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, AddonDataProviderConfig.PIXEL_FORMAT);

            ReadOnlySpan<DataFrame> frames = this.frames;

            ReadOnlySpan<byte> first = new(
                (byte*)bd.Scan0 + (frames[0].Y * bd.Stride) +
                (frames[0].X * AddonDataProviderConfig.BYTES_PER_PIXEL),
                AddonDataProviderConfig.BYTES_PER_PIXEL);

            ReadOnlySpan<byte> last = new(
                (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride) +
                (frames[^1].X * AddonDataProviderConfig.BYTES_PER_PIXEL),
                AddonDataProviderConfig.BYTES_PER_PIXEL);

            if (!first.SequenceEqual(AddonDataProviderConfig.fColor) ||
                !last.SequenceEqual(AddonDataProviderConfig.lColor))
            {
                goto Unlock;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                byte* y = (byte*)bd.Scan0 + (frames[i].Y * bd.Stride);
                int x = frames[i].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

                data[frames[i].Index] = y[x] | (y[x + 1] << 8) | (y[x + 2] << 16);
            }

        Unlock:
            bitmap.UnlockBits(bd);
        }
    }

    public void InitFrames(DataFrame[] frames) { }

    public int GetInt(int index)
    {
        return data[index];
    }

    public float GetFixed(int index)
    {
        return data[index] / 100000f;
    }

    public string GetString(int index)
    {
        int color = GetInt(index);
        if (color == 0 || color > 999999)
            return string.Empty;

        sb.Clear();

        int n = color / 10000;
        if (n > 0) sb.Append((char)n);

        n = color / 100 % 100;
        if (n > 0) sb.Append((char)n);

        n = color % 100;
        if (n > 0) sb.Append((char)n);

        return sb.ToString().Trim();
    }
}

