using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

using WinAPI;

using static WinAPI.NativeMethods;

namespace Game;

public sealed class WowScreenGDI : IWowScreen
{
    private readonly ILogger<WowScreenGDI> logger;
    private readonly WowProcess wowProcess;

    public event Action OnScreenChanged;

    // TODO: make it work for higher resolution ex. 4k
    public const int MinimapSize = 200;

    public bool Enabled { get; set; }

    public bool EnablePostProcess { get; set; }
    public Bitmap Bitmap { get; private set; }
    public object Lock { get; init; }

    public Bitmap MiniMapBitmap { get; private set; }
    public Rectangle MiniMapRect { get; private set; }
    public object MiniMapLock { get; init; }

    public IntPtr ProcessHwnd => wowProcess.Process.MainWindowHandle;

    private Rectangle rect;
    public Rectangle Rect => rect;

    private readonly Graphics graphics;
    private readonly Graphics graphicsMinimap;

    private readonly SolidBrush blackPen;

    private readonly bool windowedMode;

    public WowScreenGDI(ILogger<WowScreenGDI> logger, WowProcess wowProcess)
    {
        this.logger = logger;
        this.wowProcess = wowProcess;

        GetRectangle(out rect);
        windowedMode = IsWindowedMode(rect.Location);

        Bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb);
        graphics = Graphics.FromImage(Bitmap);
        Lock = new();

        MiniMapBitmap = new Bitmap(MinimapSize, MinimapSize, PixelFormat.Format32bppPArgb);
        graphicsMinimap = Graphics.FromImage(MiniMapBitmap);
        MiniMapLock = new();

        blackPen = new SolidBrush(Color.Black);

        logger.LogInformation($"{rect} - " +
            $"Windowed Mode: {windowedMode} - " +
            $"Scale: {DPI2PPI(GetDpi()):F2}");
    }

    public void Update()
    {
        if (windowedMode)
            GetRectangle(out rect);

        graphics.CopyFromScreen(rect.Location, Point.Empty, Bitmap.Size);
    }

    public void PostProcess()
    {
        OnScreenChanged?.Invoke();
    }

    public void GetPosition(ref Point point)
    {
        NativeMethods.GetPosition(wowProcess.Process.MainWindowHandle, ref point);
    }

    public void GetRectangle(out Rectangle rect)
    {
        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out rect);
    }


    public Bitmap GetBitmap(int width, int height)
    {
        Update();

        Bitmap bitmap = new(width, height);
        Rectangle sourceRect = new(0, 0, width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(Bitmap, 0, 0, sourceRect, GraphicsUnit.Pixel);
        }
        return bitmap;
    }

    public void DrawBitmapTo(Graphics graphics)
    {
        Update();
        graphics.DrawImage(Bitmap, 0, 0, rect, GraphicsUnit.Pixel);

        GetCursorPos(out Point cursorPoint);
        GetRectangle(out Rectangle windowRect);

        if (!windowRect.Contains(cursorPoint))
            return;

        CURSORINFO cursorInfo = new();
        cursorInfo.cbSize = Marshal.SizeOf(cursorInfo);
        if (GetCursorInfo(ref cursorInfo) &&
            cursorInfo.flags == CURSOR_SHOWING)
        {
            DrawIcon(graphics.GetHdc(),
                cursorPoint.X, cursorPoint.Y, cursorInfo.hCursor);

            graphics.ReleaseHdc();
        }
    }

    public Color GetColorAt(Point point)
    {
        return Bitmap.GetPixel(point.X, point.Y);
    }

    public void UpdateMinimapBitmap()
    {
        GetRectangle(out var rect);
        graphicsMinimap.CopyFromScreen(rect.Right - MinimapSize, rect.Top, 0, 0, MiniMapBitmap.Size);
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        graphics.Dispose();
        graphicsMinimap.Dispose();

        blackPen.Dispose();
    }
}