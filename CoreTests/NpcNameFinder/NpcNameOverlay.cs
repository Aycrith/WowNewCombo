using System;

using Core.Goals;

using GameOverlay.Drawing;
using GameOverlay.Windows;

using SharedLib.Extensions;
using SharedLib.NpcFinder;

using DPoint = System.Drawing.Point;
using DRectangle = System.Drawing.Rectangle;

namespace CoreTests;

internal sealed class NpcNameOverlay : IDisposable
{
    private readonly GraphicsWindow window;
    private readonly Graphics graphics;

    private Font font;
    private SolidBrush brushWhite;
    private SolidBrush brushGrey;

    private readonly NpcNameFinder npcNameFinder;
    private readonly NpcNameTargeting npcNameTargeting;

    private readonly bool debugTargeting;
    private readonly bool debugSkinning;
    private readonly bool debugTargetVsAdd;

    private const int padding = 6;
    private const float NumberLeftPadding = 20f;
    private const int FontSize = 10;

    public NpcNameOverlay(IntPtr handle, NpcNameFinder npcNameFinder,
        NpcNameTargeting npcNameTargeting, bool debugTargeting, bool debugSkinning, bool debugTargetVsAdd)
    {
        this.npcNameFinder = npcNameFinder;
        this.npcNameTargeting = npcNameTargeting;
        this.debugTargeting = debugTargeting;
        this.debugSkinning = debugSkinning;
        this.debugTargetVsAdd = debugTargetVsAdd;

        graphics = new Graphics()
        {
            MeasureFPS = false,
            PerPrimitiveAntiAliasing = false,
            TextAntiAliasing = false,
            UseMultiThreadedFactories = false,
            VSync = true,
            WindowHandle = IntPtr.Zero
        };

        window = new StickyWindow(handle, graphics)
        {
            FPS = 10,
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

    public void Dispose(bool disposing)
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

        brushWhite = graphics.CreateSolidBrush(255, 255, 255, 255);
        brushGrey = graphics.CreateSolidBrush(128, 128, 128, 255);
    }

    private void DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
    {
        brushWhite.Dispose();
        brushGrey.Dispose();
        font.Dispose();
    }

    private void DrawGraphics(object sender, DrawGraphicsEventArgs e)
    {
        Graphics g = e.Graphics;

        g.ClearScene();

        if (npcNameFinder.NpcCount <= 0)
            return;

        int c = npcNameTargeting.locFindBy.Length;
        const int ex = 3;
        Span<DPoint> attemptPoints = stackalloc DPoint[c + (c * ex)];

        DRectangle area = npcNameFinder.Area;

        g.DrawRectangle(brushWhite, area.Left, area.Top, area.Right, area.Bottom, 1);

        if (debugTargetVsAdd)
        {
            int sm = npcNameFinder.screenMid;
            int stb = npcNameFinder.screenTargetBuffer;
            int sab = npcNameFinder.screenAddBuffer;

            // target area
            g.DrawLine(brushWhite, new Point(sm - stb, area.Top), new Point(sm - stb, area.Bottom), 1);
            g.DrawLine(brushWhite, new Point(sm + stb, area.Top), new Point(sm + stb, area.Bottom), 1);

            // adds area
            g.DrawLine(brushGrey, new Point(sm - sab, area.Top), new Point(sm - sab, area.Bottom), 1);
            g.DrawLine(brushGrey, new Point(sm + sab, area.Top), new Point(sm + sab, area.Bottom), 1);
        }

        for (int i = 0; i < npcNameFinder.Npcs.Count; i++)
        {
            NpcPosition npc = npcNameFinder.Npcs[i];
            DRectangle rect = npc.Rect;

            g.DrawRectangle(!debugTargetVsAdd ? brushWhite : npcNameFinder.IsAdd(npc) ? brushGrey : brushWhite,
                rect.Left - padding, rect.Top - padding, rect.Right + padding, rect.Bottom + padding, 1);

            g.DrawText(font, FontSize, brushWhite, rect.Left - NumberLeftPadding - padding, rect.Top - padding, i.ToString());

            if (debugTargeting)
            {
                for (int k = 0; k < npcNameTargeting.locTargeting.Length; k++)
                {
                    DPoint p = npcNameTargeting.locTargeting[k];
                    g.DrawCircle(brushWhite, p.X + npc.ClickPoint.X, p.Y + npc.ClickPoint.Y, 5, 1);
                }
            }

            if (debugSkinning)
            {
                for (int j = 0; j < c; j += ex)
                {
                    DPoint p = npcNameTargeting.locFindBy[j];
                    attemptPoints[j] = p;
                    attemptPoints[j + c] = new DPoint(rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    attemptPoints[j + c + 1] = new DPoint(-rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                }

                for (int k = 0; k < attemptPoints.Length; k++)
                {
                    DPoint p = attemptPoints[k];
                    g.DrawCircle(brushWhite, p.X + npc.ClickPoint.X, p.Y + npc.ClickPoint.Y, 5, 1);
                }
            }
        }
    }
}
