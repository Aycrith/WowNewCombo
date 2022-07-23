using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using Core.Extensions;
using SharedLib.NpcFinder;
using Game;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class NpcNameTargeting
    {
        private const int MOUSE_DELAY = 50;

        private readonly ILogger logger;
        private readonly CancellationTokenSource cts;
        private readonly IWowScreen wowScreen;
        private readonly NpcNameFinder npcNameFinder;
        private readonly IMouseInput input;

        private readonly Pen whitePen;

        public int NpcCount => npcNameFinder.NpcCount;

        public Point[] locTargetingAndClickNpc { get; }
        public Point[] locFindByCursorType { get; }

        public NpcNameTargeting(ILogger logger, CancellationTokenSource cts, IWowScreen wowScreen, NpcNameFinder npcNameFinder, IMouseInput input)
        {
            this.logger = logger;
            this.cts = cts;
            this.wowScreen = wowScreen;
            this.npcNameFinder = npcNameFinder;
            this.input = input;

            whitePen = new Pen(Color.White, 3);

            locTargetingAndClickNpc = new Point[]
            {
                new Point(0, 0),
                new Point(-10, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(10, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };

            locFindByCursorType = new Point[]
            {
                new Point(0, 0),
                new Point(0, 25).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(0, 75).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };
        }

        public void ChangeNpcType(NpcNames npcNames)
        {
            bool changed = npcNameFinder.ChangeNpcType(npcNames);
            wowScreen.Enabled = npcNames != NpcNames.None;
            if (changed)
                cts.Token.WaitHandle.WaitOne(5); // BotController ScreenshotThread 4ms delay when idle
        }

        public void WaitForUpdate()
        {
            npcNameFinder.WaitForUpdate();
        }

        public bool FoundNpcName()
        {
            return npcNameFinder.NpcCount > 0;
        }

        public void TargetingAndClickNpc(bool leftClick, CancellationTokenSource cts)
        {
            NpcPosition npc = npcNameFinder.Npcs.First();
            logger.LogInformation($"> NPCs found: {npc.Rect}");

            foreach (Point p in locTargetingAndClickNpc)
            {
                if (cts.IsCancellationRequested)
                    return;

                var clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + p.X, npc.ClickPoint.Y + p.Y);
                input.SetCursorPosition(clickPostion);
                cts.Token.WaitHandle.WaitOne(MOUSE_DELAY);

                if (cts.IsCancellationRequested)
                    return;

                CursorClassifier.Classify(out CursorType cls);
                if (cls is CursorType.Kill or CursorType.Vendor)
                {
                    AquireTargetAtCursor(clickPostion, npc, leftClick);
                    return;
                }
            }
        }

        public bool FindBy(params CursorType[] cursor)
        {
            int c = locFindByCursorType.Length;
            foreach (NpcPosition npc in npcNameFinder.Npcs)
            {
                Point[] attemptPoints = new Point[c + (c * 2)];
                for (int i = 0; i < c; i++)
                {
                    Point p = locFindByCursorType[i];
                    attemptPoints[i] = p;
                    attemptPoints[i + c] = new Point(npc.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    attemptPoints[i + c] = new Point(-npc.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                }

                foreach (Point p in attemptPoints)
                {
                    Point clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + p.X, npc.ClickPoint.Y + p.Y);
                    input.SetCursorPosition(clickPostion);

                    cts.Token.WaitHandle.WaitOne(MOUSE_DELAY);

                    CursorClassifier.Classify(out CursorType cls);
                    if (cursor.Contains(cls))
                    {
                        AquireTargetAtCursor(clickPostion, npc);
                        return true;
                    }
                    cts.Token.WaitHandle.WaitOne(5);
                }
            }
            return false;
        }

        private void AquireTargetAtCursor(Point clickPostion, NpcPosition npc, bool leftClick = false)
        {
            if (leftClick)
                input.LeftClickMouse(clickPostion);
            else
                input.RightClickMouse(clickPostion);

            logger.LogInformation($"{nameof(NpcNameTargeting)}: NPC found! Height={npc.Height}, width={npc.Width}, pos={clickPostion}");
        }

        public void ShowClickPositions(Graphics gr)
        {
            foreach (NpcPosition npc in npcNameFinder.Npcs)
            {
                foreach (Point p in locFindByCursorType)
                {
                    gr.DrawEllipse(whitePen, p.X + npc.ClickPoint.X, p.Y + npc.ClickPoint.Y, 5, 5);
                }
            }
        }
    }
}
