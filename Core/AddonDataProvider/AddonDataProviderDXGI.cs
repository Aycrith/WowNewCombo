using Game;

using SharpGen.Runtime;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

using static WinAPI.NativeMethods;

namespace Core;

public sealed class AddonDataProviderDXGI : IAddonDataProvider, IDisposable
{
    private readonly WowScreen wowScreen;
    private readonly DataFrame[] frames;

    private static readonly FeatureLevel[] s_featureLevels =
    {
        FeatureLevel.Level_12_1,
        FeatureLevel.Level_12_0,
        FeatureLevel.Level_11_0,
    };

    private readonly Vortice.RawRect bounds;

    private readonly IDXGIAdapter adapter;
    private readonly IDXGIOutput output;
    private readonly IDXGIOutput1 output1;

    private readonly IDXGIOutputDuplication duplication;
    private readonly ID3D11Texture2D addonTexture;
    private readonly ID3D11Device device;

    private readonly Bitmap bitmap;
    private readonly Rectangle rect;
    private readonly int[] data;

    private readonly StringBuilder sb = new(3);

    public AddonDataProviderDXGI(WowScreen wowScreen, DataFrame[] frames)
    {
        this.wowScreen = wowScreen;

        this.frames = frames;

        data = new int[this.frames.Length];

        for (int i = 0; i < this.frames.Length; i++)
        {
            rect.Width = Math.Max(rect.Width, frames[i].X);
            rect.Height = Math.Max(rect.Height, frames[i].Y);
        }
        rect.Width++;
        rect.Height++;

        bitmap = new(rect.Right, rect.Bottom, PixelFormat.Format32bppRgb);

        IntPtr hMonitor = MonitorFromWindow(wowScreen.ProcessHwnd, MONITOR_DEFAULTTONULL);

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
        result = D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.None, s_featureLevels, out device!);

        if (result == Result.Fail)
            throw new Exception($"device is null {result.Description}");

        bounds = output1.Description.DesktopCoordinates;

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
    }

    public void Dispose()
    {
        duplication.ReleaseFrame();
        duplication.Dispose();

        addonTexture.Dispose();
        device.Dispose();
        adapter.Dispose();
        output1.Dispose();
        output.Dispose();

        bitmap.Dispose();
    }

    public void Update()
    {
        Point p = new();
        wowScreen.GetPosition(ref p);

        duplication.ReleaseFrame();

        Result result = duplication.AcquireNextFrame(50, out OutduplFrameInfo frameInfo, out IDXGIResource? desktopResource);
        if (!result.Success)
            return;

        ID3D11Texture2D texture = desktopResource.QueryInterface<ID3D11Texture2D>();
        device.ImmediateContext.CopySubresourceRegion(addonTexture, 0, 0, 0, 0, texture, 0,
            new Vortice.Mathematics.Box(p.X, p.Y, 0, p.X + rect.Right, p.Y + rect.Bottom, 1));
        MappedSubresource dataBox = device.ImmediateContext.Map(addonTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

        int sizeInBytesToCopy = rect.Right * AddonDataProviderConfig.BYTES_PER_PIXEL;

        BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
        for (int y = 0; y < rect.Bottom; y++)
        {
            MemoryHelpers.CopyMemory(bd.Scan0 + y * bd.Stride, dataBox.DataPointer + y * dataBox.RowPitch, sizeInBytesToCopy);
        }
        device.ImmediateContext.Unmap(addonTexture, 0);
        texture.Dispose();

        //bitmap.Save($"bitmap.bmp", ImageFormat.Bmp);
        //Thread.Sleep(1000);

        unsafe
        {
            byte* fLine = (byte*)bd.Scan0 + (frames[0].Y * bd.Stride);
            int fx = frames[0].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

            byte* lLine = (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride);
            int lx = frames[^1].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

            for (int i = 0; i < 3; i++)
            {
                if (fLine[fx + i] != AddonDataProviderConfig.fColor[i] ||
                    lLine[lx + i] != AddonDataProviderConfig.lColor[i])
                    goto Unlock;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                fLine = (byte*)bd.Scan0 + (frames[i].Y * bd.Stride);
                fx = frames[i].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

                data[frames[i].Index] = (fLine[fx + 2] * 65536) + (fLine[fx + 1] * 256) + fLine[fx];
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

