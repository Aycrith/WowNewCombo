using Game;
using SharedLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Core.Addon
{
    public sealed class AddonDataProvider : IDisposable
    {
        private readonly WowScreen wowScreen;

        private readonly DataFrame[] frames;
        private readonly int[] data;

        private readonly Color firstColor = Color.FromArgb(255, 0, 0, 0);
        private readonly Color lastColor = Color.FromArgb(255, 30, 132, 129);

        private readonly PixelFormat pixelFormat = PixelFormat.Format32bppPArgb;
        private readonly int bytesPerPixel;

        private readonly Rectangle bitmapRect;
        private readonly Bitmap bitmap = null!;

        private Rectangle rect;

        public AddonDataProvider(WowScreen wowScreen, List<DataFrame> frames)
        {
            this.wowScreen = wowScreen;

            this.frames = frames.ToArray();
            data = new int[this.frames.Length];

            rect.Width = frames.Last().point.X + 1;
            rect.Height = frames.Max(f => f.point.Y) + 1;

            bytesPerPixel = Image.GetPixelFormatSize(pixelFormat) / 8;
            bitmap = new(rect.Width, rect.Height, pixelFormat);
            bitmapRect = new(0, 0, bitmap.Width, bitmap.Height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            Point p = new();
            wowScreen.GetPosition(ref p);
            rect.X = p.X;
            rect.Y = p.Y;

            var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, bitmap.Size);
            graphics.Dispose();

            unsafe
            {
                BitmapData data = bitmap.LockBits(bitmapRect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                byte* fLine = (byte*)data.Scan0 + frames[0].point.Y * data.Stride;
                int fx = frames[0].point.X * bytesPerPixel;

                byte* lLine = (byte*)data.Scan0 + frames[^1].point.Y * data.Stride;
                int lx = frames[^1].point.X * bytesPerPixel;

                if (fLine[fx + 2] == firstColor.R && fLine[fx + 1] == firstColor.G && fLine[fx] == firstColor.B &&
                    lLine[lx + 2] == lastColor.R && lLine[lx + 1] == lastColor.G && lLine[lx] == lastColor.B)
                {
                    for (int i = 0; i < frames.Length; i++)
                    {
                        byte* line = (byte*)data.Scan0 + frames[i].point.Y * data.Stride;
                        int x = frames[i].point.X * bytesPerPixel;

                        this.data[frames[i].index] = line[x + 2] * 65536 + line[x + 1] * 256 + line[x];
                    }
                }
                bitmap.UnlockBits(data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt(int index)
        {
            return data[index];
        }

        public void Dispose()
        {
            bitmap.Dispose();
        }
    }
}

