using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

using Game;

using WinAPI;

using static WinAPI.NativeMethods;

namespace Core;

public sealed class AddonDataProviderBitBlt : IAddonDataProvider
{
    public int[] Data { get; private init; }
    public StringBuilder TextBuilder { get; } = new(3);

    private readonly DataFrame[] frames;

    private readonly Rectangle rect;

    private readonly Bitmap bitmap;
    private readonly Graphics graphics;

    private readonly IWowScreen wowScreen;

    private IntPtr hWnd = IntPtr.Zero;
    private IntPtr windowDC = IntPtr.Zero;

    private readonly bool windowedMode;
    private Point p;

    public AddonDataProviderBitBlt(IWowScreen wowScreen, DataFrame[] frames)
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
        windowedMode = IsWindowedMode(p);
    }

    public void Dispose()
    {
        ReleaseDC(hWnd, windowDC);

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

        if (hWnd != wowScreen.ProcessHwnd)
        {
            ReleaseDC(hWnd, windowDC);

            hWnd = wowScreen.ProcessHwnd;
            windowDC = GetWindowDC(hWnd);
        }

        IntPtr memoryDC = graphics.GetHdc();

        BitBlt(memoryDC,
            0, 0,
            rect.Width, rect.Height,
            windowDC, 0, 0,
            TernaryRasterOperations.SRCCOPY);

        graphics.ReleaseHdc(memoryDC);

        BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, AddonDataProviderConfig.PIXEL_FORMAT);

        // TODO: problem the  
        // int nXSrc, int nYSrc
        // 0 0 coordinates dosent point to the top left corner in the window
        // instead there are (68,28) area offset ?!
        //bitmap.Save("helpme.bmp");

        IAddonDataProvider.InternalUpdate(bd, frames, Data);

        bitmap.UnlockBits(bd);
    }
}
