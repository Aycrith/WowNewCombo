using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

using Game;

using WinAPI;

using static WinAPI.NativeMethods;

namespace Core;

public sealed class AddonDataProviderBitBlt : IAddonDataProvider, IDisposable
{
    private readonly int[] data;
    private readonly DataFrame[] frames;

    private readonly Rectangle rect;

    private readonly Bitmap bitmap;
    private readonly Graphics graphics;

    private readonly WowScreen wowScreen;

    private readonly StringBuilder sb = new(3);

    private IntPtr hWnd = IntPtr.Zero;
    private IntPtr windowDC = IntPtr.Zero;

    private readonly bool windowedMode;
    private Point p;

    public AddonDataProviderBitBlt(WowScreen wowScreen, DataFrame[] frames)
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
