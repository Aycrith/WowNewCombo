using Game;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

using WinAPI;

namespace Core;

public sealed class AddonDataProviderGDI : IAddonDataProvider
{
    public int[] Data { get; private init; }
    public StringBuilder TextBuilder { get; } = new(3);

    private readonly DataFrame[] frames;

    private readonly Rectangle rect;
    private readonly Bitmap bitmap;
    private readonly Graphics graphics;

    private readonly IWowScreen wowScreen;

    private readonly bool windowedMode;
    private Point p;

    public AddonDataProviderGDI(IWowScreen wowScreen, DataFrame[] frames)
    {
        this.wowScreen = wowScreen;
        this.frames = frames;

        Data = new int[frames.Length];

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

    public void UpdateData()
    {
        if (windowedMode)
        {
            wowScreen.GetRectangle(out Rectangle pRect);
            p = pRect.Location;
        }

        graphics.CopyFromScreen(p, Point.Empty, rect.Size);

        BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, AddonDataProviderConfig.PIXEL_FORMAT);

        IAddonDataProvider.InternalUpdate(bd, frames, Data);

        bitmap.UnlockBits(bd);
    }
}

