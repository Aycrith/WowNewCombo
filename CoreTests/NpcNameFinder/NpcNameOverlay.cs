using System;
using System.Collections.Generic;

using Core.Goals;

using GameOverlay.Drawing;
using GameOverlay.Windows;

using SharedLib.Extensions;
using SharedLib.NpcFinder;

namespace CoreTests
{
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

            if (npcNameFinder.nameType.HasFlag(NpcNames.Enemy))
                brush = graphics.CreateSolidBrush(
                    NpcNameFinder.sE_R,NpcNameFinder.sE_G, NpcNameFinder.sE_B);
            else if (npcNameFinder.nameType.HasFlag(NpcNames.Friendly))
                brush = graphics.CreateSolidBrush(
                    NpcNameFinder.sF_R, NpcNameFinder.sF_G, NpcNameFinder.sF_B);
            else if (npcNameFinder.nameType.HasFlag(NpcNames.Neutral))
                brush = graphics.CreateSolidBrush(
                    NpcNameFinder.sN_R, NpcNameFinder.sN_G, NpcNameFinder.sN_B);
            else if (npcNameFinder.nameType.HasFlag(NpcNames.Corpse))
                brush = graphics.CreateSolidBrush(
                    NpcNameFinder.fC_RGB, NpcNameFinder.fC_RGB, NpcNameFinder.fC_RGB);
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

            int j = 0;
            foreach (NpcPosition npc in npcNameFinder.Npcs)
            {
                g.DrawRectangle(brush, npc.Rect.Left, npc.Rect.Top, npc.Rect.Right, npc.Rect.Bottom, 1);
                g.DrawText(font, 10, brush, npc.Rect.Left - 20f, npc.Rect.Top, j.ToString());
                j++;

                if (debugTargeting)
                {
                    foreach (var l in npcNameTargeting.locTargeting)
                    {
                        g.DrawCircle(brush, l.X + npc.ClickPoint.X, l.Y + npc.ClickPoint.Y, 5, 1);
                    }
                }

                if (debugSkinning)
                {
                    int c = npcNameTargeting.locFindBy.Length;
                    int ex = 3;
                    System.Drawing.Point[] attemptPoints = new System.Drawing.Point[c + (c * ex)];
                    for (int i = 0; i < c; i += ex)
                    {
                        System.Drawing.Point p = npcNameTargeting.locFindBy[i];
                        attemptPoints[i] = p;
                        attemptPoints[i + c] = new System.Drawing.Point(npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                        attemptPoints[i + c + 1] = new System.Drawing.Point(-npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    }

                    foreach (var l in attemptPoints)
                    {
                        g.DrawCircle(brush, l.X + npc.ClickPoint.X, l.Y + npc.ClickPoint.Y, 5, 1);
                    }
                }
            }
        }
    }
}
