using Game;

using SharpGen.Runtime;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

using static WinAPI.NativeMethods;

namespace Core;

public sealed class AddonDataProviderDXGI : IAddonDataProvider
{
    public int[] Data { get; private init; }
    public StringBuilder TextBuilder { get; } = new(3);

    private readonly IWowScreen wowScreen;
    private readonly DataFrame[] frames;

    private static readonly FeatureLevel[] s_featureLevels =
    {
        FeatureLevel.Level_12_1,
        FeatureLevel.Level_12_0,
        FeatureLevel.Level_11_0,
    };

    private readonly IDXGIAdapter adapter;
    private readonly IDXGIOutput output;
    private readonly IDXGIOutput1 output1;

    private readonly IDXGIOutputDuplication duplication;
    private readonly ID3D11Texture2D addonTexture;
    private readonly ID3D11Device device;

    private readonly Bitmap bitmap;
    private readonly Rectangle rect;

    private readonly bool windowedMode;
    private Point p;

    public AddonDataProviderDXGI(IWowScreen wowScreen, DataFrame[] frames)
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

        bitmap = new(rect.Right, rect.Bottom, PixelFormat.Format32bppRgb);

        IntPtr hMonitor = MonitorFromWindow(wowScreen.ProcessHwnd, MONITOR_DEFAULT_TO_NULL);
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

        addonTexture = device.CreateTexture2D(textureDesc);

        wowScreen.GetRectangle(out Rectangle pRect);
        p = pRect.Location;
        windowedMode = IsWindowedMode(p);
    }

    public void Dispose()
    {
        duplication.ReleaseFrame();
        duplication.Dispose();
        device.Dispose();

        adapter.Dispose();
        output1.Dispose();
        output.Dispose();

        addonTexture.Dispose();
        bitmap.Dispose();
    }

    public void Update()
    {
        if (windowedMode)
        {
            wowScreen.GetRectangle(out Rectangle pRect);
            p = pRect.Location;
        }

        duplication.ReleaseFrame();

        Result result = duplication.AcquireNextFrame(999,
            out OutduplFrameInfo frameInfo,
            out IDXGIResource? desktopResource);

        if (!result.Success ||
            frameInfo.AccumulatedFrames != 1 ||
            frameInfo.LastPresentTime == 0)
        {
            return;
        }

        ID3D11Texture2D texture = desktopResource.QueryInterface<ID3D11Texture2D>();

        Box box = new(p.X, p.Y, 0, p.X + rect.Right, p.Y + rect.Bottom, 1);

        device.ImmediateContext.CopySubresourceRegion(addonTexture, 0, 0, 0, 0, texture, 0, box);

        MappedSubresource dataBox = device.ImmediateContext.Map(addonTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        int sizeInBytesToCopy = (rect.Right - rect.Left) * AddonDataProviderConfig.BYTES_PER_PIXEL;

        BitmapData bd = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
        for (int y = 0; y < rect.Bottom; y++)
        {
            MemoryHelpers.CopyMemory(bd.Scan0 + y * bd.Stride, dataBox.DataPointer + y * dataBox.RowPitch, sizeInBytesToCopy);
        }
        device.ImmediateContext.Unmap(addonTexture, 0);
        texture.Dispose();

        //bitmap.Save($"bitmap.bmp", ImageFormat.Bmp);
        //System.Threading.Thread.Sleep(1000);

        IAddonDataProvider.InternalUpdate(bd, frames, Data);

        bitmap.UnlockBits(bd);
    }
}

