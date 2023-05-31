using System;

using Core.Goals;

using GameOverlay.Drawing;
using GameOverlay.Windows;

using SharedLib.Extensions;
using SharedLib.NpcFinder;

using DPoint = System.Drawing.Point;

namespace CoreTests;

public class NpcNameOverlay : IDisposable
{
    private readonly GraphicsWindow window;
    private readonly Graphics graphics;

    private Font font;
    private SolidBrush brush;
    private SolidBrush brushBackground;

    private readonly NpcNameFinder npcNameFinder;
    private readonly NpcNameTargeting npcNameTargeting;

    private readonly bool debugTargeting;
    private readonly bool debugSkinning;

    private const int padding = 6;
    private const float NumberLeftPadding = 20f;
    private const int FontSize = 10;

    public NpcNameOverlay(IntPtr handle, NpcNameFinder npcNameFinder,
        NpcNameTargeting npcNameTargeting, bool debugTargeting, bool debugSkinning)
    {
        this.npcNameFinder = npcNameFinder;
        this.npcNameTargeting = npcNameTargeting;
        this.debugTargeting = debugTargeting;
        this.debugSkinning = debugSkinning;

        graphics = new Graphics()
        {
            MeasureFPS = true,
            PerPrimitiveAntiAliasing = true,
            TextAntiAliasing = true,
            UseMultiThreadedFactories = false,
            VSync = false,
            WindowHandle = IntPtr.Zero
        };

        window = new StickyWindow(handle, graphics)
        {
            FPS = 60,
            AttachToClientArea = true,
            BypassTopmost = true,
        };
        window.SetupGraphics += SetupGraphics;
        window.DestroyGraphics += DestroyGraphics;
        window.DrawGraphics += DrawGraphics;

        window.Create();
    }

    ~NpcNameOverlay()
    {
        Dispose(false);
    }

    #region IDisposable Support

    private bool disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

        window.Dispose();
        disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    private void SetupGraphics(object sender, SetupGraphicsEventArgs e)
    {
        Graphics graphics = e.Graphics;

        font = graphics.CreateFont("Arial", 16);
        brushBackground = graphics.CreateSolidBrush(0, 0x27, 0x31, 0);

        brush = graphics.CreateSolidBrush(255, 255, 255, 255);
    }

    private void DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
    {
        brush.Dispose();
        brushBackground.Dispose();
        font.Dispose();
    }

    private void DrawGraphics(object sender, DrawGraphicsEventArgs e)
    {
        Graphics g = e.Graphics;

        g.ClearScene(brushBackground);

        if (npcNameFinder.NpcCount <= 0)
            return;

        g.DrawRectangle(brush, npcNameFinder.Area.Left, npcNameFinder.Area.Top, npcNameFinder.Area.Right, npcNameFinder.Area.Bottom, 1);

        for (int i = 0; i < npcNameFinder.Npcs.Count; i++)
        {
            NpcPosition npc = npcNameFinder.Npcs[i];

            g.DrawRectangle(brush, npc.Rect.Left - padding, npc.Rect.Top - padding, npc.Rect.Right + padding, npc.Rect.Bottom + padding, 1);
            g.DrawText(font, FontSize, brush, npc.Rect.Left - NumberLeftPadding - padding, npc.Rect.Top - padding, i.ToString());

            if (debugTargeting)
            {
                for (int k = 0; k < npcNameTargeting.locTargeting.Length; k++)
                {
                    DPoint l = npcNameTargeting.locTargeting[k];
                    g.DrawCircle(brush, l.X + npc.ClickPoint.X, l.Y + npc.ClickPoint.Y, 5, 1);
                }
            }

            if (debugSkinning)
            {
                int c = npcNameTargeting.locFindBy.Length;
                const int ex = 3;

                DPoint[] attemptPoints = new DPoint[c + (c * ex)];
                for (int j = 0; j < c; j += ex)
                {
                    DPoint p = npcNameTargeting.locFindBy[j];
                    attemptPoints[j] = p;
                    attemptPoints[j + c] = new DPoint(npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    attemptPoints[j + c + 1] = new DPoint(-npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                }

                for (int k = 0; k < attemptPoints.Length; k++)
                {
                    DPoint l = attemptPoints[k];
                    g.DrawCircle(brush, l.X + npc.ClickPoint.X, l.Y + npc.ClickPoint.Y, 5, 1);
                }
            }
        }
    }
}
