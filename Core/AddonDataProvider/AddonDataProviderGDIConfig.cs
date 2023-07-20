using Game;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;

namespace Core;

public sealed class AddonDataProviderGDIConfig : IAddonDataProvider, IDisposable
{
    public int[] Data { get; private set; } = Array.Empty<int>();
    public StringBuilder TextBuilder { get; } = new(3);

    private readonly CancellationToken ct;
    private readonly ManualResetEventSlim manualReset = new(true);
    private readonly WowScreen wowScreen;

    private DataFrame[] frames = Array.Empty<DataFrame>();

    private Rectangle rect;
    private Bitmap? bitmap;
    private Graphics? graphics;

    private bool disposing;

    public AddonDataProviderGDIConfig(CancellationTokenSource cts, WowScreen wowScreen, DataFrame[] frames)
    {
        ct = cts.Token;
        this.wowScreen = wowScreen;
        InitFrames(frames);
    }

    public void Dispose()
    {
        if (disposing)
            return;

        disposing = true;

        graphics?.Dispose();
        bitmap?.Dispose();
    }

    public void Update()
    {
        manualReset.Wait();
        ct.WaitHandle.WaitOne(25);

        if (ct.IsCancellationRequested ||
            disposing ||
            Data.Length == 0 ||
            frames.Length == 0 ||
            bitmap == null ||
            graphics == null)
            return;

        Point p = new();
        wowScreen.GetRectangle(out Rectangle pRect);
        p = pRect.Location;
        graphics.CopyFromScreen(p, Point.Empty, rect.Size);

        BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, AddonDataProviderConfig.PIXEL_FORMAT);

        IAddonDataProvider.InternalUpdate(bd, frames, Data);

        bitmap.UnlockBits(bd);
    }

    public void InitFrames(DataFrame[] frames)
    {
        manualReset.Reset();

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

        manualReset.Set();
    }

    public int GetInt(int index)
    {
        return index > Data.Length ? 0 : Data[index];
    }
}

