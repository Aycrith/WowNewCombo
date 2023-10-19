using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;

namespace Game;

public static class WoWScreenUtil
{
    public static string ToBase64(Bitmap bitmap, Bitmap resized, Graphics graphics)
    {
        graphics.DrawImage(bitmap, 0, 0, resized.Width, resized.Height);

        using MemoryStream ms = new();
        resized.Save(ms, ImageFormat.Png);

        byte[] byteImage = ms.ToArray();
        return Convert.ToBase64String(byteImage);
    }
}
