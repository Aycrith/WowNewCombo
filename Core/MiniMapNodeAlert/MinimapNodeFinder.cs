using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Game;
using SharedLib.Extensions;

namespace Core
{
    public class MinimapNodeFinder : INodeFinder, IImageProvider
    {
        private readonly WowScreen wowScreen;
        private readonly IPixelClassifier pixelClassifier;
        public event EventHandler<NodeEventArgs>? NodeEvent;

        private const int MinScore = 2;

        public MinimapNodeFinder(WowScreen wowScreen, IPixelClassifier pixelClassifier)
        {
            this.wowScreen = wowScreen;
            this.pixelClassifier = pixelClassifier;
        }

        public void TryFind()
        {
            wowScreen.UpdateMinimapBitmap();

            var list = FindYellowPoints();
            ScorePoints(list, out Score best);
            NodeEvent?.Invoke(this, new NodeEventArgs(best.point, list.Count(x => x.count > MinScore)));
        }

        private List<Score> FindYellowPoints()
        {
            List<Score> points = new(100);
            Bitmap bitmap = wowScreen.MiniMapBitmap;

            // TODO: adjust these values based on resolution
            // The reference resolution is 1920x1080
            int minX = 6;
            int maxX = 170;
            int minY = 36;
            int maxY = bitmap.Height - 6;

            Rectangle rect = new(minX, minY, maxX - minX, maxY - minY);
            Point center = rect.Centre();
            float radius = (maxX - minX) / 2f;

            unsafe
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(bitmap.PixelFormat) / 8;

                //for (int y = minY; y < maxY; y++)
                Parallel.For(minY, maxY, y =>
                {
                    byte* currentLine = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = minX; x < maxX; x++)
                    {
                        if (!IsValidSquareLocation(x, y, center, radius))
                            continue;

                        int xi = x * bytesPerPixel;
                        if (pixelClassifier.IsMatch(currentLine[xi + 2], currentLine[xi + 1], currentLine[xi]))
                        {
                            if (points.Capacity == points.Count)
                                return;

                            points.Add(new Score { point = new Point(x, y), count = 0 });
                            currentLine[xi + 2] = 255;
                            currentLine[xi + 1] = 0;
                            currentLine[xi + 0] = 0;
                        }
                    }
                });

                bitmap.UnlockBits(data);
            }

            if (points.Count == points.Capacity)
            {
                Debug.WriteLine("Error: Too much yellow in this image, adjust the configuration !");
                points.Clear();
            }

            return points;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidSquareLocation(int x, int y, Point center, float width)
        {
            return Math.Sqrt(((x - center.X) * (x - center.X)) + ((y - center.Y) * (y - center.Y))) < width;
        }

        private static bool ScorePoints(List<Score> points, out Score best)
        {
            best = new Score();
            const int size = 5;

            for (int i = 0; i < points.Count; i++)
            {
                Score p = points[i];
                p.count = points.Where(s => Math.Abs(s.point.X - p.point.X) < size) // + or - n pixels horizontally
                    .Where(s => Math.Abs(s.point.Y - p.point.Y) < size) // + or - n pixels vertically
                    .Count();
                points[i] = p;
            }

            points.Sort((a, b) => a.count.CompareTo(b.count));

            if (points.Count > 0 && points[^1].count > MinScore)
            {
                best = points[^1];
                return true;
            }

            return false;
        }

        private struct Score
        {
            public Point point;
            public int count;
        }
    }
}