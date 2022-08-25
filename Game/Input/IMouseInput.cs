using System.Drawing;
using System.Threading;

namespace Game
{
    public interface IMouseInput
    {
        void SetCursorPosition(Point p);

        void RightClickMouse(Point p);

        void LeftClickMouse(Point p);

        void InteractMouseOver(CancellationToken ct);
    }
}
