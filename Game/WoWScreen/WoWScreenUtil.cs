using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using SharedLib;

namespace Game;

public static class WoWScreenUtil
{
    public static string ToBase64(IBitmapProvider provider, Bitmap resized, Graphics graphics)
    {
        lock (provider.Lock)
        {
            graphics.DrawImage(provider.Bitmap, 0, 0, resized.Width, resized.Height);
        }

        using MemoryStream ms = new();
        resized.Save(ms, ImageFormat.Png);

        byte[] byteImage = ms.ToArray();
        return Convert.ToBase64String(byteImage);
    }

    public static string ToBase64(IMinimapBitmapProvider provider, Bitmap resized, Graphics graphics)
    {
        lock (provider.MiniMapLock)
        {
            graphics.DrawImage(provider.MiniMapBitmap, 0, 0, resized.Width, resized.Height);
        }
        using MemoryStream ms = new();
        resized.Save(ms, ImageFormat.Png);

        byte[] byteImage = ms.ToArray();
        return Convert.ToBase64String(byteImage);
    }

    public static string ToBase64(Bitmap bitmap, Bitmap resized, Graphics graphics)
    {
        graphics.DrawImage(bitmap, 0, 0, resized.Width, resized.Height);

        using MemoryStream ms = new();
        resized.Save(ms, ImageFormat.Png);

        byte[] byteImage = ms.ToArray();
        return Convert.ToBase64String(byteImage);
    }
}
