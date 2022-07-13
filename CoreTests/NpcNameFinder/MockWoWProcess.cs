using System.Drawing;
using Game;

namespace CoreTests
{
    public class MockWoWProcess : IMouseInput
    {
        public void RightClickMouse(Point p)
        {
            throw new System.NotImplementedException();
        }

        public void LeftClickMouse(Point p)
        {
            throw new System.NotImplementedException();
        }

        public void SetCursorPosition(Point p)
        {
            throw new System.NotImplementedException();
        }
    }
}
