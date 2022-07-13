using System.Drawing;

namespace Game
{
    public interface IMouseInput
    {
        void SetCursorPosition(Point p);

        void RightClickMouse(Point p);

        void LeftClickMouse(Point p);
    }
}
