using System.Drawing.Imaging;

namespace Core;

internal static class AddonDataProviderConfig
{
    //                                       B  G  R  A
    public static readonly byte[] fColor = { 0, 0, 0, 255 };
    public static readonly byte[] lColor = { 129, 132, 30, 255 };

    public const PixelFormat PIXEL_FORMAT = PixelFormat.Format32bppPArgb;
    public const int BYTES_PER_PIXEL = 4;
}
