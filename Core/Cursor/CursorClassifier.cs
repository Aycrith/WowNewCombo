using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using static WinAPI.NativeMethods;

#pragma warning disable CS0162

namespace Core
{
    public sealed class CursorClassifier
    {
        private const bool saveImage = false;

        // index matches CursorType order
        private static readonly ulong[][] imageHashes =
        {
            new ulong[] { 4645529528554094592, 4665762466636896256, 6376251547633783040, 6376251547633783552 },
            new ulong[] { 9286546093378506253, 16208728271425048093 },
            new ulong[] { 16205332705670085656, 16495805933079509016 },
            new ulong[] { 13901748381153107456, 16207591392111181312 },
            new ulong[] { 4669700909741929478, 4669700909674820614 },
            new ulong[] { 4683320813727784960, 4669700909741929478, 4683461550142398464 },
            new ulong[] { 17940331276560775168, 17940331276594329600, 17940331276594460672 },
            new ulong[] { 16207573517913036808, 4669140166357294088 },
            new ulong[] { 4667452417086599168, 4676529985085517824 },
            new ulong[] { 4682718988357606424, 4682718988358655000 }
        };

        private readonly Bitmap bitmap;
        private readonly Graphics graphics;

        private readonly Bitmap workBitmap;
        private readonly Graphics workGraphics;

        public CursorClassifier()
        {
            Size size = GetCursorSize();
            bitmap = new(size.Width, size.Height);
            graphics = Graphics.FromImage(bitmap);

            workBitmap = new(8, 8, PixelFormat.Format32bppArgb);
            workGraphics = Graphics.FromImage(workBitmap);
            workGraphics.CompositingQuality = CompositingQuality.HighQuality;
            workGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            workGraphics.SmoothingMode = SmoothingMode.HighQuality;
        }

        public void Dispose()
        {
            graphics.Dispose();
            bitmap.Dispose();

            workGraphics.Dispose();
            workBitmap.Dispose();
        }


        public void Classify(out CursorType classification)
        {
            CURSORINFO cursorInfo = new();
            cursorInfo.cbSize = Marshal.SizeOf(cursorInfo);
            if (GetCursorInfo(ref cursorInfo) &&
                cursorInfo.flags == CURSOR_SHOWING)
            {
                graphics.Clear(Color.Transparent);
                DrawIcon(graphics.GetHdc(), 0, 0, cursorInfo.hCursor);
                graphics.ReleaseHdc();
            }

            ulong cursorHash = ImageHashing.AverageHash(bitmap, workBitmap, workGraphics);
            if (saveImage)
            {
                string path = Path.Join("..", "Cursors", $"{cursorHash}.bmp");
                if (!File.Exists(path))
                {
                    bitmap.Save(path);
                }
            }

            int index = 0;
            double similarity = 0;
            for (int i = 0; i < imageHashes.Length; i++)
            {
                for (int j = 0; j < imageHashes[i].Length; j++)
                {
                    double sim = ImageHashing.Similarity(cursorHash, imageHashes[i][j]);
                    if (sim > 80 && sim > similarity)
                    {
                        index = i;
                        similarity = sim;
                    }
                }
            }

            classification = (CursorType)index;
            Debug.WriteLine($"[CursorClassifier.Classify] {classification.ToStringF()} - {similarity}");
        }
    }
}