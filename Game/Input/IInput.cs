using SixLabors.ImageSharp;

using System;
using System.Threading;

namespace Game;

public interface IInput : IDisposable
{
    void KeyDown(int key);

    void KeyUp(int key);

    int PressRandom(int key, int milliseconds);

    int PressRandom(int key, int milliseconds, CancellationToken token);

    void PressFixed(int key, int milliseconds, CancellationToken token);

    void SetCursorPos(Point p);

    void RightClick(Point p);

    void LeftClick(Point p);

    void SendText(string text);
}
