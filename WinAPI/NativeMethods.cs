using System;

using SixLabors.ImageSharp;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]

namespace WinAPI;

public static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public Int32 cbSize;
        public Int32 flags;
        public IntPtr hCursor;
        public Point ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorInfo(ref CURSORINFO pci);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DrawIcon(IntPtr hDC, int x, int y, IntPtr hIcon);

    public const int CURSOR_SHOWING = 0x0001;
    public const int DI_NORMAL = 0x0003;

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageA")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out Point p);

    public const UInt32 WM_KEYDOWN = 0x0100;
    public const UInt32 WM_KEYUP = 0x0101;
    public const UInt32 WM_LBUTTONDOWN = 0x201;
    public const UInt32 WM_LBUTTONUP = 0x202;
    public const UInt32 WM_RBUTTONDOWN = 0x204;
    public const UInt32 WM_RBUTTONUP = 0x205;

    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;

    public static int MakeLParam(int x, int y) => (y << 16) | (x & 0xFFFF);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndexn);

    private const int SM_CXCURSOR = 13;
    private const int SM_CYCURSOR = 14;

    [LibraryImport("gdi32.dll")]
    private static partial int GetDeviceCaps(IntPtr hDC, int nIndex);

    private const int LOGPIXELSX = 88;

    public static bool IsWindowedMode(Point point)
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
        rect = Rectangle.FromLTRB(nRect.left, nRect.top, nRect.right, nRect.bottom);

        Point topLeft = new();
        ClientToScreen(hWnd, ref topLeft);
        if (IsWindowedMode(topLeft))
        {
            rect.X = topLeft.X;
            rect.Y = topLeft.Y;
        }
    }

    public static int GetDpi()
    {
        using System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return GetDeviceCaps(g.GetHdc(), LOGPIXELSX);
    }

    public static Size GetCursorSize()
    {
        int dpi = GetDpi();
        SizeF size = new(GetSystemMetrics(SM_CXCURSOR), GetSystemMetrics(SM_CYCURSOR));
        size *= DPI2PPI(dpi);
        return (Size)size;
    }

    public static float DPI2PPI(int dpi)
    {
        return dpi / 96f;
    }

    public const int MONITOR_DEFAULT_TO_NULL = 0;
    public const int MONITOR_DEFAULT_TO_PRIMARY = 1;
    public const int MONITOR_DEFAULT_TO_NEAREST = 2;

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetWindowDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, TernaryRasterOperations dwRop);
}