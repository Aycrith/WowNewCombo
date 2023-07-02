using System.Drawing;
using System.Numerics;

using System.Collections.Generic;
using System.Linq;
using System.Drawing.Imaging;
using System;
using System.Drawing.Drawing2D;
using System.Net.Http;

namespace Core;

public static class PathDrawer
{
    private const int minSize = 40;

    private const float mapScalar = 100f;
    private const float radius = 3f;

    public static void Execute(
        List<Vector3> path, string imgUrl, string output)
    {
        List<PointF> points = new(path.Count);
        points.AddRange(path.ConvertAll(
            p => new PointF(p.X / mapScalar, p.Y / mapScalar)));

        Bitmap background = DownloadImageAsBitmap(imgUrl);

        using Graphics gr = Graphics.FromImage(background);
        gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
        gr.CompositingQuality = CompositingQuality.HighQuality;

        float width = background.Width;
        float height = background.Height;
        DrawPath(gr, points, width, height, radius);

        RectangleF sRect = RecalculateBounds(points);
        sRect.X *= width;
        sRect.Y *= height;
        sRect.Width *= width;
        sRect.Height *= height;

        sRect.Inflate(minSize, minSize);

        sRect.X = MathF.Max(sRect.X, 0);
        sRect.Y = MathF.Max(sRect.Y, 0);

        if (sRect.X + sRect.Width > width)
            sRect.Width = width - sRect.X;
        if (sRect.Y + sRect.Height > height)
            sRect.Height = height - sRect.Y;

        // Draw starting pos to the bottom
        Rectangle rect = Rectangle.Round(sRect);

        Font font = SystemFonts.DefaultFont;

        // Black background
        PointF startPoint = points.First();
        string startText =
            $"{startPoint.X * mapScalar:0.##} " +
            $"{startPoint.Y * mapScalar:0.##}";

        SizeF sizeStart = gr.MeasureString(startText, font);

        Rectangle infoRect = new(
            new Point(rect.X, rect.Bottom - (int)sizeStart.Height),
            new Size(rect.Width, (int)sizeStart.Height));

        gr.FillRectangle(Brushes.Black, infoRect);

        // xx.xx yy.yy
        Rectangle startRect = new(
            new Point(infoRect.X, infoRect.Y),
            new Size((int)(sizeStart.Width + 1), infoRect.Height));

        gr.DrawString(startText, font, Brushes.White, startRect);

        Bitmap outputImg =
            background.Clone(rect, PixelFormat.Format32bppArgb);

        if (outputImg.Width < 200 || outputImg.Height < 200)
        {
            const int upscale = 2;

            Bitmap scaled = new(outputImg,
                outputImg.Width * upscale,
                outputImg.Height * upscale);

            outputImg.Dispose();
            outputImg = scaled;
        }

        ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().
            First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        EncoderParameters encParams = new()
        {
            Param = new[] {
                new EncoderParameter(Encoder.Quality, 100L)
            }
        };

        outputImg.Save(output, encoder, encParams);
    }

    private static RectangleF RecalculateBounds(List<PointF> list)
    {
        float minX = list.Min(p => p.X);
        float minY = list.Min(p => p.Y);
        float maxX = list.Max(p => p.X);
        float maxY = list.Max(p => p.Y);

        return new(
            new PointF(minX, minY),
            new SizeF(maxX - minX, maxY - minY));
    }

    private static Bitmap DownloadImageAsBitmap(string url)
    {
        using HttpClient httpClient = new();
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        using HttpResponseMessage response = httpClient.Send(request);

        return new(response.Content.ReadAsStream());
    }

    private static void DrawPath(Graphics gr,
        List<PointF> list, float width, float height, float radius)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            float shade = (float)i / list.Count;
            int step = 255 - (int)(255 * shade);
            using SolidBrush b =
                new(Color.FromArgb(255, step, 0, 0));

            PointF p = list[i];
            gr.FillEllipse(
                p == list.First()
                ? Brushes.White
                : b,
                width * p.X, height * p.Y, radius, radius);
        }
    }
}
