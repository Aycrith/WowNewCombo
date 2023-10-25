using System;
using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;

#pragma warning disable 162

namespace Core;

public sealed class MinimapNodeFinder
{
    private readonly struct PixelPoint
    {
        public readonly int X;
        public readonly int Y;

        public PixelPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    private const bool DEBUG_MASK = false;

    private readonly ILogger logger;
    private readonly IMinimapBitmapProvider provider;
    public event EventHandler<MinimapNodeEventArgs>? NodeEvent;

    private const int minScore = 2;
    private const byte maxBlue = 34;
    private const byte minRedGreen = 176;

    private readonly int maxY;

    public MinimapNodeFinder(ILogger logger, IMinimapBitmapProvider provider)
    {
        this.logger = logger;
        this.provider = provider;

        maxY = provider.MiniMapBitmap.Height - 6;
    }

    public void Update()
    {
        var span = FindYellowPoints();
        ScorePoints(span, out PixelPoint best, out int amountAboveMin);
        NodeEvent?.Invoke(this, new MinimapNodeEventArgs(best.X, best.Y, amountAboveMin));
    }

    private ReadOnlySpan<PixelPoint> FindYellowPoints()
    {
        const int SIZE = 100;
        var pooler = ArrayPool<PixelPoint>.Shared;
        PixelPoint[] points = pooler.Rent(SIZE);

        // TODO: adjust these values based on resolution
        // The reference resolution is 1920x1080
        const int minX = 6;
        const int maxX = 170;
        const int minY = 36;

        Rectangle rect = new(minX, minY, maxX - minX, maxY - minY);
        Point center = rect.Centre();
        float radius = (maxX - minX) / 2f;

        int count = 0;

        lock (provider.MiniMapLock)
        {
            Bitmap bitmap = provider.MiniMapBitmap;

            unsafe
            {
                BitmapData bd = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size),
                    DEBUG_MASK ? ImageLockMode.ReadWrite : ImageLockMode.ReadOnly, bitmap.PixelFormat);
                const int bytesPerPixel = 4; //Bitmap.GetPixelFormatSize(bitmap.PixelFormat) / 8;

                Parallel.For(minY, maxY, y =>
                {
                    ReadOnlySpan<byte> bitmapSpan = new(bd.Scan0.ToPointer(), bd.Height * bd.Stride);
                    ReadOnlySpan<byte> currentLine = bitmapSpan.Slice(y * bd.Stride, bd.Stride);

                    for (int x = minX; x < maxX; x++)
                    {
                        if (!IsValidSquareLocation(x, y, center, radius))
                        {
                            /*
                            if (DEBUG_MASK)
                            {
                                int xii = x * bytesPerPixel;
                                currentLine[xii + 2] = 0;
                                currentLine[xii + 1] = 0;
                                currentLine[xii + 0] = 0;
                            }
                            */
                            continue;
                        }

                        int xi = x * bytesPerPixel;
                        if (IsMatch(currentLine[xi + 2], currentLine[xi + 1], currentLine[xi]))
                        {
                            if (count >= SIZE)
                                return;

                            points[count++] = new PixelPoint(x, y);

                            /*
                            if (DEBUG_MASK)
                            {
                                currentLine[xi + 2] = 255;
                                currentLine[xi + 1] = 0;
                                currentLine[xi + 0] = 0;
                            }
                            */
                        }
                    }
                });

                bitmap.UnlockBits(bd);
            }
        }

        if (count >= SIZE)
        {
            //logger.LogWarning("Too much yellow in this image!");
        }

        return points.AsSpan(0, count);

        static bool IsValidSquareLocation(int x, int y, Point center, float width)
        {
            return MathF.Sqrt(((x - center.X) * (x - center.X)) + ((y - center.Y) * (y - center.Y))) < width;
        }

        static bool IsMatch(byte red, byte green, byte blue)
        {
            return blue < maxBlue && red > minRedGreen && green > minRedGreen;
        }
    }

    private static void ScorePoints(ReadOnlySpan<PixelPoint> points, out PixelPoint best, out int amountAboveMin)
    {
        const int size = 5;

        best = new PixelPoint();
        amountAboveMin = 0;

        int maxIndex = -1;
        int maxScore = 0;

        for (int i = 0; i < points.Length; i++)
        {
            PixelPoint pi = points[i];

            int score = 0;
            for (int j = 0; j < points.Length; j++)
            {
                PixelPoint pj = points[j];

                if (i != j &&
                    (Math.Abs(pi.X - pj.X) < size ||
                    Math.Abs(pi.Y - pj.Y) < size))
                {
                    score++;
                }
            }

            if (score > minScore)
                amountAboveMin++;

            if (maxScore < score)
            {
                maxIndex = i;
                maxScore = score;
            }
        }

        if (maxIndex >= 0 && maxScore > minScore)
        {
            best = points[maxIndex];
        }
    }
}