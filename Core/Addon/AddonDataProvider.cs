using Game;
using SharedLib;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace Core
{
    public sealed class AddonDataProvider : IDisposable
    {
        private readonly WowScreen wowScreen;

        private readonly DataFrame[] frames;
        private readonly int[] data;

        //                                 B  G  R
        private readonly byte[] fColor = { 0, 0, 0 };
        private readonly byte[] lColor = { 129, 132, 30 };

        private const PixelFormat pixelFormat = PixelFormat.Format32bppPArgb;
        private readonly int bytesPerPixel;

        private readonly Rectangle bitmapRect;
        private readonly Bitmap bitmap = null!;
        private readonly Graphics graphics;

        private Rectangle rect;

        public AddonDataProvider(WowScreen wowScreen, DataFrame[] frames)
        {
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

            bytesPerPixel = Image.GetPixelFormatSize(pixelFormat) / 8;
            bitmap = new(rect.Width, rect.Height, pixelFormat);
            bitmapRect = new(0, 0, bitmap.Width, bitmap.Height);

            graphics = Graphics.FromImage(bitmap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            Point p = new();
            wowScreen.GetPosition(ref p);
            rect.Location = p;

            graphics.CopyFromScreen(rect.Location, Point.Empty, bitmap.Size);

            unsafe
            {
                BitmapData bd = bitmap.LockBits(bitmapRect, ImageLockMode.ReadOnly, pixelFormat);

                byte* fLine = (byte*)bd.Scan0 + (frames[0].Y * bd.Stride);
                int fx = frames[0].X * bytesPerPixel;

                byte* lLine = (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride);
                int lx = frames[^1].X * bytesPerPixel;

                for (int i = 0; i < 3; i++)
                {
                    if (fLine[fx + i] != fColor[i] || lLine[lx + i] != lColor[i])
                        goto Exit;
                }

                for (int i = 0; i < frames.Length; i++)
                {
                    byte* line = (byte*)bd.Scan0 + (frames[i].Y * bd.Stride);
                    int x = frames[i].X * bytesPerPixel;

                    data[frames[i].Index] = (line[x + 2] * 65536) + (line[x + 1] * 256) + line[x];
                }

            Exit:
                bitmap.UnlockBits(bd);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt(int index)
        {
            return data[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetFixed(int index)
        {
            return GetInt(index) / 100000f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public void Dispose()
        {
            bitmap.Dispose();
            graphics.Dispose();
        }
    }
}

