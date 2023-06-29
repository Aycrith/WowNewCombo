using System;
using System.Drawing;

using SharedLib;

namespace Game;

public interface IWowScreen : IColorReader, IRectProvider, IBitmapProvider
{
    bool Enabled { get; set; }

    void AddDrawAction(Action<Graphics> g);
}
