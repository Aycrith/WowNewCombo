using System;

using SharedLib;

namespace Game;

public interface IWowScreen : IRectProvider, IScreenImageProvider, IMinimapImageProvider, IDisposable
{
    bool Enabled { get; set; }

    bool MinimapEnabled { get; set; }

    IntPtr ProcessHwnd { get; }

    bool EnablePostProcess { get; set; }
    void PostProcess();

    event Action OnScreenChanged;

    void Update();
}
