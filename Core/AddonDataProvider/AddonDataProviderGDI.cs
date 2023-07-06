using Game;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

using WinAPI;

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

    private readonly bool windowedMode;
    private Point p;

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

        wowScreen.GetRectangle(out Rectangle pRect);
        p = pRect.Location;
        windowedMode = NativeMethods.IsWindowedMode(p);
    }

    public void Dispose()
    {
        graphics.Dispose();
        bitmap.Dispose();
    }

    public void Update()
    {
        if (windowedMode)
        {
            wowScreen.GetRectangle(out Rectangle pRect);
            p = pRect.Location;
        }

        graphics.CopyFromScreen(p, Point.Empty, rect.Size);

        BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, AddonDataProviderConfig.PIXEL_FORMAT);

        IAddonDataProvider.InternalUpdate(bd, frames, data);

        bitmap.UnlockBits(bd);
    }

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

