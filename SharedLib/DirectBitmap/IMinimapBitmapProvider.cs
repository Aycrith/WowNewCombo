using System.Drawing;

namespace SharedLib;

public interface IMinimapBitmapProvider
{
    Bitmap MiniMapBitmap { get; }

    Rectangle MiniMapRect { get; }

    object MiniMapLock { get; }
}
