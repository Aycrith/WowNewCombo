using System.Drawing;
using System.Threading;

using Game;

namespace CoreTests;

internal sealed class MockWoWProcess : IMouseInput
{
    public void RightClick(Point p)
    {
        throw new System.NotImplementedException();
    }

    public void LeftClick(Point p)
    {
        throw new System.NotImplementedException();
    }

    public void SetCursorPos(Point p)
    {
        throw new System.NotImplementedException();
    }

    public void InteractMouseOver(CancellationToken ct)
    {
        throw new System.NotImplementedException();
    }
}
