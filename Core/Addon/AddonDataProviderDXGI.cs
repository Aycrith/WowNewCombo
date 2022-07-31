using Game;
using SharpGen.Runtime;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static WinAPI.NativeMethods;

namespace Core
{
    public sealed class AddonDataProviderDXGI : IAddonDataProvider, IDisposable
    {
        private readonly CancellationTokenSource cts;

        private readonly WowScreen wowScreen;

        private bool disposing;

        //                                 B  G  R
        private readonly byte[] fColor = { 0, 0, 0 };
        private readonly byte[] lColor = { 129, 132, 30 };

        private static readonly FeatureLevel[] s_featureLevels = new[]
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
        private readonly DataFrame[] frames;

        public AddonDataProviderDXGI(CancellationTokenSource cts, WowScreen wowScreen, DataFrame[] frames)
        {
            this.cts = cts;
            this.wowScreen = wowScreen;

            this.frames = frames;

            data = new int[this.frames.Length];

            for (int i = 0; i < this.frames.Length; i++)
            {
                if (frames[i].X > rect.Width)
                    rect.Width = frames[i].X;

                if (frames[i].Y > rect.Height)
                    rect.Height = frames[i].Y;
            }
            rect.Width++;
            rect.Height++;

            bitmap = new(rect.Right, rect.Bottom, PixelFormat.Format32bppRgb);

            IntPtr hMonitor = MonitorFromWindow(wowScreen.ProcessHwnd, MONITOR_DEFAULTTONULL);

            IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            adapter = factory.GetAdapter(0);

            int srcIdx = 0;
            output = adapter.GetOutput(srcIdx);
            do
            {
                if (output.Description.Monitor == hMonitor)
                    break;

                srcIdx++;
                output = adapter.GetOutput(srcIdx);
            } while (output != null);

            output1 = output!.QueryInterface<IDXGIOutput1>();
            D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.None, s_featureLevels, out ID3D11Device? d);

            if (d == null)
                throw new Exception("device is null");

            device = d;

            bounds = output1.Description.DesktopCoordinates;

            duplication = output1.DuplicateOutput(device);

            Texture2DDescription textureDesc = new()
            {
                CPUAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = rect.Right,
                Height = rect.Bottom,
                //Width = bounds.Right - bounds.Left,
                //Height = bounds.Bottom - bounds.Top,
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
            if (disposing)
                return;

            disposing = true;

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
            cts.Token.WaitHandle.WaitOne(1);

            if (cts.IsCancellationRequested || disposing) return;

            Point p = new();
            wowScreen.GetPosition(ref p);

            duplication.ReleaseFrame();
            Result result = duplication.AcquireNextFrame(50, out OutduplFrameInfo frameInfo, out IDXGIResource? desktopResource);
            if (!result.Success || disposing)
                return;

            const int bytesPerPixel = 4;

            ID3D11Texture2D texture = desktopResource.QueryInterface<ID3D11Texture2D>();
            device.ImmediateContext.CopySubresourceRegion(addonTexture, 0, 0, 0, 0, texture, 0,
                new Vortice.Mathematics.Box(p.X, p.Y, 0, p.X + rect.Right, p.Y + rect.Bottom, 1));
            MappedSubresource dataBox = device.ImmediateContext.Map(addonTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

            int sizeInBytesToCopy = rect.Right * bytesPerPixel;

            BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            for (int y = 0; y < rect.Bottom; y++)
            {
                MemoryHelpers.CopyMemory(bd.Scan0 + y * bd.Stride, (dataBox.DataPointer + y * dataBox.RowPitch), sizeInBytesToCopy);
            }
            device.ImmediateContext.Unmap(addonTexture, 0);
            texture.Dispose();

            //bitmap.Save($"bitmap.bmp", ImageFormat.Bmp);
            //Thread.Sleep(1000);

            unsafe
            {
                byte* fLine = (byte*)bd.Scan0 + (frames[0].Y * bd.Stride);
                int fx = frames[0].X * bytesPerPixel;

                byte* lLine = (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride);
                int lx = frames[^1].X * bytesPerPixel;

                for (int i = 0; i < 3; i++)
                {
                    if (fLine[fx + i] != fColor[i] || lLine[lx + i] != lColor[i])
                        goto Unlock;
                }

                for (int i = 0; i < frames.Length; i++)
                {
                    fLine = (byte*)bd.Scan0 + (frames[i].Y * bd.Stride);
                    fx = frames[i].X * bytesPerPixel;

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
            return GetInt(index) / 100000f;
        }

        public string GetString(int index)
        {
            int color = GetInt(index);
            if (color != 0)
            {
                string colorString = color.ToString();
                if (colorString.Length > 6) { return string.Empty; }
                string colorText = "000000"[..(6 - colorString.Length)] + colorString;
                return ToChar(colorText, 0) + ToChar(colorText, 2) + ToChar(colorText, 4);
            }
            else
            {
                return string.Empty;
            }
        }

        private static string ToChar(string colorText, int start)
        {
            return ((char)int.Parse(colorText.Substring(start, 2))).ToString();
        }
    }
}

