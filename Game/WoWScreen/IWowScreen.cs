using System;
using System.Drawing;

using SharedLib;

namespace Game;

public interface IWowScreen : IColorReader, IRectProvider, IBitmapProvider, IMinimapBitmapProvider, IDisposable
{
    bool Enabled { get; set; }

    IntPtr ProcessHwnd { get; }

    bool EnablePostProcess { get; set; }
    void PostProcess();
    void AddDrawAction(Action<Graphics> g);

    event Action OnScreenChanged;

    void Update();

    void UpdateMinimapBitmap();

    void DrawBitmapTo(Graphics g);
}
