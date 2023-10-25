using Game;

using Microsoft.Extensions.Logging;

using SharpGen.Runtime;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

using WinAPI;

using static WinAPI.NativeMethods;

namespace Core;

public sealed class WowScreenDXGI : IWowScreen, IAddonDataProvider
{
    private readonly ILogger<WowScreenDXGI> logger;
    private readonly WowProcess wowProcess;

    public event Action? OnScreenChanged;

    public bool Enabled { get; set; }

    public bool EnablePostProcess { get; set; }
    public Bitmap Bitmap { get; private set; }
    public object Lock { get; init; }

    private const double screenshotTickMs = 180;
    private DateTime lastScreenUpdate;

    // TODO: make it work for higher resolution ex. 4k
    public const int MiniMapSize = 200;
    public Bitmap MiniMapBitmap { get; private set; }
    public Rectangle MiniMapRect { get; private set; }
    public object MiniMapLock { get; init; }

    private readonly int minimapBytesToCopy;

    private const double miniMapTickMs = 180;
    private DateTime lastMinimapUpdate;

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

    private readonly ID3D11Texture2D minimapTexture;
    private readonly ID3D11Texture2D screenTexture;
    private readonly ID3D11Texture2D addonTexture;

    private readonly ID3D11Device device;
    private readonly IDXGIOutputDuplication duplication;

    private Point p;

    private Rectangle screenRect;
    public Rectangle Rect => screenRect;

    private readonly bool windowedMode;
    private readonly int screenBytesToCopy;

    // IAddonDataProvider

    private readonly Rectangle addonRect;
    private readonly Bitmap addonBitmap;
    private readonly DataFrame[] frames;

    public int[] Data { get; private init; }
    public StringBuilder TextBuilder { get; } = new(3);

    public WowScreenDXGI(ILogger<WowScreenDXGI> logger,
        WowProcess wowProcess, DataFrame[] frames)
    {
        this.logger = logger;
        this.wowProcess = wowProcess;
        this.frames = frames;

        this.frames = frames;

        Data = new int[frames.Length];

        for (int i = 0; i < frames.Length; i++)
        {
            addonRect.Width = Math.Max(addonRect.Width, frames[i].X);
            addonRect.Height = Math.Max(addonRect.Height, frames[i].Y);
        }
        addonRect.Width++;
        addonRect.Height++;

        addonBitmap =
            new(addonRect.Right, addonRect.Bottom, PixelFormat.Format32bppRgb);

        GetRectangle(out screenRect);
        p = screenRect.Location;
        windowedMode = IsWindowedMode(screenRect.Location);

        screenBytesToCopy = windowedMode
            ? (screenRect.Right - screenRect.Left) * 4
            : (screenRect.Right - screenRect.Left) * 4 * screenRect.Bottom;

        Bitmap =
            new(screenRect.Width, screenRect.Height, PixelFormat.Format32bppPArgb);
        Lock = new();

        MiniMapRect = new(0, 0, MiniMapSize, MiniMapSize);
        MiniMapBitmap = new(MiniMapSize, MiniMapSize, PixelFormat.Format32bppPArgb);
        minimapBytesToCopy = (MiniMapRect.Right - MiniMapRect.Left) * 4;
        MiniMapLock = new();

        IntPtr hMonitor =
            MonitorFromWindow(wowProcess.Process.MainWindowHandle, MONITOR_DEFAULT_TO_NULL);

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
        result = D3D11.D3D11CreateDevice(adapter, DriverType.Unknown,
            DeviceCreationFlags.Singlethreaded, s_featureLevels, out device!);

        if (result == Result.Fail)
            throw new Exception($"device is null {result.Description}");

        duplication = output1.DuplicateOutput(device);

        Texture2DDescription screenTextureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = screenRect.Right,
            Height = screenRect.Bottom,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };
        screenTexture = device.CreateTexture2D(screenTextureDesc);

        Texture2DDescription addonTextureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = addonRect.Right,
            Height = addonRect.Bottom,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };
        addonTexture = device.CreateTexture2D(addonTextureDesc);

        Texture2DDescription miniMapTextureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = MiniMapRect.Right,
            Height = MiniMapRect.Bottom,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };
        minimapTexture = device.CreateTexture2D(miniMapTextureDesc);

        this.logger.LogInformation($"{screenRect} - " +
            $"Windowed Mode: {windowedMode} - " +
            $"Scale: {DPI2PPI(GetDpi()):F2}");
    }

    public void Dispose()
    {
        duplication.ReleaseFrame();
        duplication.Dispose();

        minimapTexture.Dispose();
        addonTexture.Dispose();
        screenTexture.Dispose();

        device.Dispose();
        adapter.Dispose();
        output1.Dispose();
        output.Dispose();

        addonBitmap.Dispose();
        Bitmap.Dispose();
    }

    public void Update()
    {
        if (windowedMode)
            GetRectangle(out screenRect);

        duplication.ReleaseFrame();

        Result result = duplication.AcquireNextFrame(0,
            out OutduplFrameInfo frame,
        out IDXGIResource resource);

        // If only the pointer was updated(that is, the desktop image was not updated),
        // the AccumulatedFrames, TotalMetadataBufferSize, LastPresentTime members are set to zero.
        if (!result.Success ||
            frame.AccumulatedFrames == 0 ||
            frame.TotalMetadataBufferSize == 0 ||
            frame.LastPresentTime == 0)
        {
            return;
        }

        ID3D11Texture2D texture = resource.QueryInterface<ID3D11Texture2D>();

        // Addon

        Box box = new(p.X, p.Y, 0, p.X + addonRect.Right, p.Y + addonRect.Bottom, 1);
        device.ImmediateContext.CopySubresourceRegion(addonTexture, 0, 0, 0, 0, texture, 0, box);

        MappedSubresource dataBoxAddon = device.ImmediateContext.Map(addonTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        int sizeInBytesToCopy = (addonRect.Right - addonRect.Left) * AddonDataProviderConfig.BYTES_PER_PIXEL;

        BitmapData bdAddon = addonBitmap.LockBits(addonRect, ImageLockMode.WriteOnly, addonBitmap.PixelFormat);
        for (int y = 0; y < addonRect.Bottom; y++)
        {
            MemoryHelpers.CopyMemory(bdAddon.Scan0 + y * bdAddon.Stride, dataBoxAddon.DataPointer + y * dataBoxAddon.RowPitch, sizeInBytesToCopy);
        }
        device.ImmediateContext.Unmap(addonTexture, 0);

        //bitmap.Save($"bitmap.bmp", ImageFormat.Bmp);
        //System.Threading.Thread.Sleep(1000);

        IAddonDataProvider.InternalUpdate(bdAddon, frames, Data);

        addonBitmap.UnlockBits(bdAddon);

        // Screen

        if (DateTime.UtcNow > lastScreenUpdate.AddMilliseconds(screenshotTickMs))
        {
            box = new(p.X, p.Y, 0, p.X + screenRect.Right, p.Y + screenRect.Bottom, 1);
            device.ImmediateContext.CopySubresourceRegion(screenTexture, 0, 0, 0, 0, texture, 0, box);

            MappedSubresource dataBoxScreen = device.ImmediateContext.Map(screenTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

            lock (Lock)
            {
                BitmapData bdScreen = Bitmap.LockBits(screenRect, ImageLockMode.WriteOnly, Bitmap.PixelFormat);
                if (windowedMode)
                {
                    for (int y = 0; y < screenRect.Bottom; y++)
                    {
                        MemoryHelpers.CopyMemory(bdScreen.Scan0 + y * bdScreen.Stride, dataBoxScreen.DataPointer + y * dataBoxScreen.RowPitch, screenBytesToCopy);
                    }
                }
                else
                {
                    MemoryHelpers.CopyMemory(bdScreen.Scan0, dataBoxScreen.DataPointer, screenBytesToCopy);
                }

                device.ImmediateContext.Unmap(screenTexture, 0);

                Bitmap.UnlockBits(bdScreen);
            }

            lastScreenUpdate = DateTime.UtcNow;
        }

        // Minimap

        if (DateTime.UtcNow > lastMinimapUpdate.AddMilliseconds(miniMapTickMs))
        {
            box = new(p.X + screenRect.Right - MiniMapSize, p.Y, 0, p.X + MiniMapRect.Right, p.Y + MiniMapRect.Bottom, 1);
            device.ImmediateContext.CopySubresourceRegion(minimapTexture, 0, 0, 0, 0, texture, 0, box);

            MappedSubresource dataBoxMap = device.ImmediateContext.Map(minimapTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

            lock (MiniMapLock)
            {
                BitmapData bdMap = MiniMapBitmap.LockBits(MiniMapRect, ImageLockMode.WriteOnly, MiniMapBitmap.PixelFormat);
                for (int y = 0; y < MiniMapRect.Bottom; y++)
                {
                    MemoryHelpers.CopyMemory(bdMap.Scan0 + y * bdMap.Stride, dataBoxMap.DataPointer + y * dataBoxMap.RowPitch, minimapBytesToCopy);
                }

                device.ImmediateContext.Unmap(screenTexture, 0);

                MiniMapBitmap.UnlockBits(bdMap);
            }

            lastMinimapUpdate = DateTime.UtcNow;
        }
        texture.Dispose();
    }

    public void UpdateData()
    {

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
        Bitmap bitmap = new(width, height);
        Rectangle sourceRect = new(0, 0, width, height);

        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(Bitmap, 0, 0, sourceRect, GraphicsUnit.Pixel);

        return bitmap;
    }

    public void DrawBitmapTo(Graphics graphics)
    {
        lock (Lock)
        {
            graphics.DrawImage(Bitmap, 0, 0, screenRect, GraphicsUnit.Pixel);
        }

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
        // not used
    }
}