using PInvoke;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WinAPI
{
    public static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public Point ptScreenPos;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(ref CURSORINFO pci);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        [DllImport("user32.dll")]
        public static extern bool DrawIcon(IntPtr hDC, int x, int y, IntPtr hIcon);

        public const Int32 CURSOR_SHOWING = 0x0001;
        public const Int32 DI_NORMAL = 0x0003;

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point p);

        public const UInt32 WM_KEYDOWN = 0x0100;
        public const UInt32 WM_KEYUP = 0x0101;
        public const UInt32 WM_LBUTTONDOWN = 0x201;
        public const UInt32 WM_LBUTTONUP = 0x202;
        public const UInt32 WM_RBUTTONDOWN = 0x204;
        public const UInt32 WM_RBUTTONUP = 0x205;

        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;

        public static int MakeLParam(int x, int y) => (y << 16) | (x & 0xFFFF);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndexn);

        private static int SM_CXCURSOR = 13;
        private static int SM_CYCURSOR = 14;

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hDC, int nIndex);

        private static int LOGPIXELSX = 88;

        private static bool IsWindowedMode(Point point)
        {
            return point.X != 0 || point.Y != 0;
        }

        public static void GetPosition(IntPtr hWnd, ref Point point)
        {
            ClientToScreen(hWnd, ref point);
        }

        public static void GetWindowRect(IntPtr hWnd, out Rectangle rect)
        {
            GetClientRect(hWnd, out RECT nRect);
            rect = new Rectangle
            {
                X = nRect.left,
                Y = nRect.top,
                Width = nRect.right - nRect.left,
                Height = nRect.bottom - nRect.top
            };

            Point topLeft = new();
            ClientToScreen(hWnd, ref topLeft);
            if (IsWindowedMode(topLeft))
            {
                rect.X = topLeft.X;
                rect.Y = topLeft.Y;
            }
        }

        private static int GetDpi()
        {
            using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
            return GetDeviceCaps(g.GetHdc(), LOGPIXELSX);
        }

        public static Size GetCursorSize()
        {
            int dpi = GetDpi();
            var size = new SizeF(GetSystemMetrics(SM_CXCURSOR), GetSystemMetrics(SM_CYCURSOR));
            size *= DPI2PPI(dpi);
            return size.ToSize();
        }

        private static float DPI2PPI(int dpi)
        {
            return dpi / 96f;
        }

        public const int MONITOR_DEFAULTTONULL = 0;
        public const int MONITOR_DEFAULTTOPRIMARY = 1;
        public const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    }
}