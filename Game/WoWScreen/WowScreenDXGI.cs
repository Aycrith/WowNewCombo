using Microsoft.Extensions.Logging;

using SharedLib;

using SharpGen.Runtime;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

using WinAPI;

using static WinAPI.NativeMethods;

namespace Game;

public sealed class WowScreenDXGI : IWowScreen, IBitmapProvider, IDisposable
{
    private readonly ILogger<WowScreenDXGI> logger;
    private readonly WowProcess wowProcess;

    public event Action OnScreenChanged;

    private readonly List<Action<Graphics>> drawActions = new();

    // TODO: make it work for higher resolution ex. 4k
    public const int MinimapSize = 200;

    public bool Enabled { get; set; }

    public bool EnablePostProcess { get; set; }
    public Bitmap Bitmap { get; private set; }

    public Bitmap MiniMapBitmap { get; private set; }
    public Rectangle MiniMapRect { get; private set; }

    public IntPtr ProcessHwnd => wowProcess.Process.MainWindowHandle;

    private static readonly FeatureLevel[] s_featureLevels =
    {
        FeatureLevel.Level_12_1,
        FeatureLevel.Level_12_0,
        FeatureLevel.Level_11_0,
    };

    private readonly IDXGIAdapter adapter;
    private readonly IDXGIOutput output;
    private readonly IDXGIOutput1 output1;

    private readonly ID3D11Texture2D screenTexture;
    private readonly ID3D11Device device;
    private readonly IDXGIOutputDuplication duplication;

    private Point p;

    private Rectangle rect;
    public Rectangle Rect => rect;

    private readonly Graphics graphics;
    private readonly Graphics graphicsMinimap;

    private readonly SolidBrush blackPen;

    private readonly bool windowedMode;

    public WowScreenDXGI(ILogger<WowScreenDXGI> logger, WowProcess wowProcess)
    {
        this.logger = logger;
        this.wowProcess = wowProcess;

        GetRectangle(out rect);
        p = rect.Location;
        windowedMode = IsWindowedMode(rect.Location);

        Bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb);
        graphics = Graphics.FromImage(Bitmap);

        MiniMapBitmap = new Bitmap(MinimapSize, MinimapSize, PixelFormat.Format32bppPArgb);
        graphicsMinimap = Graphics.FromImage(MiniMapBitmap);

        blackPen = new SolidBrush(System.Drawing.Color.Black);

        IntPtr hMonitor = MonitorFromWindow(wowProcess.Process.MainWindowHandle, MONITOR_DEFAULT_TO_NULL);

        Result result;

        IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        result = factory.EnumAdapters(0, out adapter);
        if (result == Result.Fail)
            throw new Exception($"Unable to enumerate adapter! {result.Description}");

        int srcIdx = 0;
        do
        {
            result = adapter.EnumOutputs(srcIdx, out output);
            if (result == Result.Ok &&
                output.Description.Monitor == hMonitor)
            {
                break;
            }
        } while (result != Result.Fail);

        output1 = output.QueryInterface<IDXGIOutput1>();
        result = D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.Singlethreaded, s_featureLevels, out device!);

        if (result == Result.Fail)
            throw new Exception($"device is null {result.Description}");

        duplication = output1.DuplicateOutput(device);

        Texture2DDescription textureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = rect.Right,
            Height = rect.Bottom,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };

        screenTexture = device.CreateTexture2D(textureDesc);

        this.logger.LogInformation($"{rect} - " +
            $"Windowed Mode: {windowedMode} - " +
            $"Scale: {DPI2PPI(GetDpi()):F2}");
    }

    public void Update()
    {
        if (windowedMode)
            GetRectangle(out rect);

        duplication.ReleaseFrame();

        Result result = duplication.AcquireNextFrame(999,
            out OutduplFrameInfo frameInfo,
            out IDXGIResource desktopResource);

        if (!result.Success ||
            //frameInfo.AccumulatedFrames != 1 ||
            frameInfo.LastPresentTime == 0)
        {
            return;
        }

        ID3D11Texture2D texture = desktopResource.QueryInterface<ID3D11Texture2D>();

        Box box = new(p.X, p.Y, 0, p.X + rect.Right, p.Y + rect.Bottom, 1);
        device.ImmediateContext.CopySubresourceRegion(screenTexture, 0, 0, 0, 0, texture, 0, box);

        MappedSubresource dataBox = device.ImmediateContext.Map(screenTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        int sizeInBytesToCopy = (rect.Right - rect.Left) * 4;

        BitmapData bd = Bitmap.LockBits(rect, ImageLockMode.WriteOnly, Bitmap.PixelFormat);
        if (windowedMode)
        {
            for (int y = 0; y < rect.Bottom; y++)
            {
                MemoryHelpers.CopyMemory(bd.Scan0 + y * bd.Stride, dataBox.DataPointer + y * dataBox.RowPitch, sizeInBytesToCopy);
            }
        }
        else
        {
            MemoryHelpers.CopyMemory(bd.Scan0, dataBox.DataPointer, sizeInBytesToCopy * rect.Bottom);
        }

        device.ImmediateContext.Unmap(screenTexture, 0);

        texture.Dispose();

        Bitmap.UnlockBits(bd);
    }

    public void AddDrawAction(Action<Graphics> a)
    {
        drawActions.Add(a);
    }

    public void PostProcess()
    {
        using (Graphics gr = Graphics.FromImage(Bitmap))
        {
            gr.FillRectangle(blackPen,
                new Rectangle(new Point(Bitmap.Width / 15, Bitmap.Height / 40),
                new Size(Bitmap.Width / 15, Bitmap.Height / 40)));

            for (int i = 0; i < drawActions.Count; i++)
            {
                drawActions[i](gr);
            }
        }

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
        Bitmap bitmap = new(width, height);
        Rectangle sourceRect = new(0, 0, width, height);

        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(Bitmap, 0, 0, sourceRect, GraphicsUnit.Pixel);

        return bitmap;
    }

    public void DrawBitmapTo(Graphics graphics)
    {
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

    public System.Drawing.Color GetColorAt(Point point)
    {
        return Bitmap.GetPixel(point.X, point.Y);
    }

    public void UpdateMinimapBitmap()
    {
        // TODO: move this to update
        //GetRectangle(out var rect);
        //graphicsMinimap.CopyFromScreen(rect.Right - MinimapSize, rect.Top, 0, 0, MiniMapBitmap.Size);
    }

    public void Dispose()
    {
        duplication.ReleaseFrame();
        duplication.Dispose();

        screenTexture.Dispose();
        device.Dispose();
        adapter.Dispose();
        output1.Dispose();
        output.Dispose();

        Bitmap.Dispose();
        graphics.Dispose();
        graphicsMinimap.Dispose();

        blackPen.Dispose();
    }
}