using System;
using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;

namespace Core
{
    public sealed class MinimapNodeFinder
    {
        private readonly struct Point
        {
            public readonly int X;
            public readonly int Y;

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private readonly ILogger logger;
        private readonly WowScreen wowScreen;
        public event EventHandler<MinimapNodeEventArgs>? NodeEvent;

        private const int MinScore = 2;
        private const int MaxBlue = 34;
        private const int MinRedGreen = 176;

        public MinimapNodeFinder(ILogger logger, WowScreen wowScreen)
        {
            this.logger = logger;
            this.wowScreen = wowScreen;
        }

        public void TryFind()
        {
            wowScreen.UpdateMinimapBitmap();

            var span = FindYellowPoints();
            ScorePoints(span, out Point best, out int amountAboveMin);
            NodeEvent?.Invoke(this, new MinimapNodeEventArgs(best.X, best.Y, amountAboveMin));
        }

        private Span<Point> FindYellowPoints()
        {
            const int SIZE = 100;
            var pooler = ArrayPool<Point>.Shared;
            Point[] points = pooler.Rent(SIZE);

            Bitmap bitmap = wowScreen.MiniMapBitmap;

            // TODO: adjust these values based on resolution
            // The reference resolution is 1920x1080
            const int minX = 6;
            const int maxX = 170;
            const int minY = 36;
            int maxY = bitmap.Height - 6;

            Rectangle rect = new(minX, minY, maxX - minX, maxY - minY);
            System.Drawing.Point center = rect.Centre();
            float radius = (maxX - minX) / 2f;

            int count = 0;

            unsafe
            {
                BitmapData data = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size),
                    ImageLockMode.ReadWrite, bitmap.PixelFormat);
                const int bytesPerPixel = 4; //Bitmap.GetPixelFormatSize(bitmap.PixelFormat) / 8;

                Parallel.For(minY, maxY, y =>
                {
                    byte* currentLine = (byte*)data.Scan0 + (y * data.Stride);
                    for (int x = minX; x < maxX; x++)
                    {
                        if (!IsValidSquareLocation(x, y, center, radius))
                            continue;

                        int xi = x * bytesPerPixel;
                        if (IsMatch(currentLine[xi + 2], currentLine[xi + 1], currentLine[xi]))
                        {
                            if (count >= SIZE)
                                return;

                            points[count++] = new Point(x, y);

                            currentLine[xi + 2] = 255;
                            currentLine[xi + 1] = 0;
                            currentLine[xi + 0] = 0;
                        }
                    }
                });

                bitmap.UnlockBits(data);
            }

            if (count >= SIZE)
            {
                logger.LogWarning("Too much yellow in this image!");
            }

            return points.AsSpan(0, count);

            static bool IsValidSquareLocation(int x, int y, System.Drawing.Point center, float width)
            {
                return Math.Sqrt(((x - center.X) * (x - center.X)) + ((y - center.Y) * (y - center.Y))) < width;
            }

            static bool IsMatch(byte red, byte green, byte blue)
            {
                return blue < MaxBlue && red > MinRedGreen && green > MinRedGreen;
            }
        }

        private static void ScorePoints(Span<Point> points, out Point best, out int amountAboveMin)
        {
            const int size = 5;

            best = new Point();
            amountAboveMin = 0;

            int maxIndex = -1;
            int maxScore = 0;

            for (int i = 0; i < points.Length; i++)
            {
                Point pi = points[i];

                int score = 0;
                for (int j = 0; j < points.Length; j++)
                {
                    Point pj = points[j];

                    if (Math.Abs(pi.X - pj.X) < size ||
                        Math.Abs(pi.Y - pj.Y) < size)
                    {
                        score++;
                    }
                }

                if (score > MinScore)
                    amountAboveMin++;

                if (maxScore < score)
                {
                    maxIndex = i;
                    maxScore = score;
                }
            }

            if (maxIndex >= 0 && maxScore > MinScore)
            {
                best = points[maxIndex];
            }
        }
    }
}