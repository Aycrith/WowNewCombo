using Microsoft.Extensions.Logging;
using SharedLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using WinAPI;

namespace Game
{
    public sealed class WowScreen : IWowScreen, IBitmapProvider, IDisposable
    {
        private readonly ILogger logger;
        private readonly WowProcess wowProcess;

        public event Action OnScreenChanged;

        private readonly List<Action<Graphics>> drawActions = new();

        // TODO: make it work for higher resolution ex. 4k
        public const int MinimapSize = 200;

        public bool Enabled { get; set; }

        public bool EnablePostProcess { get; set; }
        public Bitmap Bitmap { get; private set; }

        public Bitmap MiniMapBitmap { get; private set; }

        private Rectangle rect;
        public Rectangle Rect => rect;

        private readonly Graphics graphics;
        private readonly Graphics graphicsMinimap;

        public WowScreen(ILogger logger, WowProcess wowProcess)
        {
            this.logger = logger;
            this.wowProcess = wowProcess;

            Point p = new();
            GetPosition(ref p);
            GetRectangle(out rect);
            rect.Location = p;

            Bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppPArgb);
            graphics = Graphics.FromImage(Bitmap);

            MiniMapBitmap = new Bitmap(MinimapSize, MinimapSize, PixelFormat.Format32bppPArgb);
            graphicsMinimap = Graphics.FromImage(MiniMapBitmap);
        }

        public void Update()
        {
            Point p = new();
            GetPosition(ref p);
            rect.Location = p;

            graphics.CopyFromScreen(rect.Location, Point.Empty, Bitmap.Size);
        }

        public void AddDrawAction(Action<Graphics> a)
        {
            drawActions.Add(a);
        }

        public void PostProcess()
        {
            using (var gr = Graphics.FromImage(Bitmap))
            {
                using (var blackPen = new SolidBrush(Color.Black))
                {
                    gr.FillRectangle(blackPen, new Rectangle(new Point(Bitmap.Width / 15, Bitmap.Height / 40), new Size(Bitmap.Width / 15, Bitmap.Height / 40)));
                }

                drawActions.ForEach(x => x(gr));
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
            Update();

            Bitmap bitmap = new(width, height);
            Rectangle sourceRect = new(0, 0, width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.DrawImage(Bitmap, 0, 0, sourceRect, GraphicsUnit.Pixel);
            }
            return bitmap;
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
        }

        private static Bitmap CropImage(Bitmap img, bool highlight)
        {
            int x = img.Width / 2;
            int y = img.Height / 2;
            int r = Math.Min(x, y);

            var tmp = new Bitmap(2 * r, 2 * r);
            using (Graphics g = Graphics.FromImage(tmp))
            {
                if (highlight)
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 0, 0)))
                    {
                        g.FillRectangle(brush, 0, 0, img.Width, img.Height);
                    }
                }

                g.SmoothingMode = SmoothingMode.None;
                g.TranslateTransform(tmp.Width / 2, tmp.Height / 2);
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(0 - r, 0 - r, 2 * r, 2 * r);
                    using (var region = new Region(gp))
                    {
                        g.SetClip(region, CombineMode.Replace);
                        using (var bmp = new Bitmap(img))
                        {

                            g.DrawImage(bmp, new Rectangle(-r, -r, 2 * r, 2 * r), new Rectangle(x - r, y - r, 2 * r, 2 * r), GraphicsUnit.Pixel);
                        }
                    }
                }
            }
            return tmp;
        }

        public static string ToBase64(Bitmap bitmap, int size)
        {
            int width, height;
            if (bitmap.Width > bitmap.Height)
            {
                width = size;
                height = Convert.ToInt32(bitmap.Height * size / (float)bitmap.Width);
            }
            else
            {
                width = Convert.ToInt32(bitmap.Width * size / (float)bitmap.Height);
                height = size;
            }
            var resized = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImage(bitmap, 0, 0, width, height);
            }

            using (MemoryStream ms = new MemoryStream())
            {
                resized.Save(ms, ImageFormat.Png);
                resized.Dispose();
                byte[] byteImage = ms.ToArray();
                return Convert.ToBase64String(byteImage); // Get Base64
            }
        }

    }
}