using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace SharedLib
{
    public sealed class BitmapCapturer : IBitmapProvider, IColorReader, IDisposable
    {
        public Rectangle Rect { get; set; }

        public Bitmap Bitmap { get; private set; }

        public BitmapCapturer(Rectangle rect)
        {
            this.Rect = rect;
            Bitmap = new(rect.Width, rect.Height, PixelFormat.Format32bppPArgb);
        }

        public void Capture()
        {
            var graphics = Graphics.FromImage(Bitmap);
            graphics.CopyFromScreen(Rect.Left, Rect.Top, 0, 0, Bitmap.Size);
            graphics.Dispose();
        }
        public void Capture(Rectangle rect)
        {
            Rect = rect;
            Capture();
        }

        public Color GetColorAt(Point point)
        {
            return Bitmap.GetPixel(point.X, point.Y);
        }

        public Bitmap GetBitmap(int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);
            Rectangle sourceRect = new Rectangle(0, 0, width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.DrawImage(Bitmap, 0, 0, sourceRect, GraphicsUnit.Pixel);
            }
            return bitmap;
        }

        public void Dispose()
        {
            Bitmap?.Dispose();
        }
    }
}
