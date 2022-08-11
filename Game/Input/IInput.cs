using System.Drawing;
using System.Threading;

namespace Game
{
    public interface IInput
    {
        void KeyDown(int key);

        void KeyUp(int key);

        int KeyPress(int key, int milliseconds);

        void KeyPressSleep(int key, int milliseconds, CancellationToken ct);

        void SetCursorPosition(Point p);

        void RightClickMouse(Point p);

        void LeftClickMouse(Point p);

        void SendText(string text);

        void SetClipboard(string text);

        void PasteFromClipboard();
    }
}
