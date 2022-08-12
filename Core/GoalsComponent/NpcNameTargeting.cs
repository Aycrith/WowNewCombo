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
        private const int FAST_DELAY = 5;

        private readonly ILogger logger;
        private readonly CancellationToken ct;
        private readonly IWowScreen wowScreen;
        private readonly NpcNameFinder npcNameFinder;
        private readonly IMouseInput input;

        private readonly Pen whitePen;

        public int NpcCount => npcNameFinder.NpcCount;

        public Point[] locTargeting { get; }
        public Point[] locFindBy { get; }

        public NpcNameTargeting(ILogger logger, CancellationTokenSource cts,
            IWowScreen wowScreen, NpcNameFinder npcNameFinder, IMouseInput input)
        {
            this.logger = logger;
            ct = cts.Token;
            this.wowScreen = wowScreen;
            this.npcNameFinder = npcNameFinder;
            this.input = input;

            whitePen = new Pen(Color.White, 3);

            locTargeting = new Point[]
            {
                new Point(0, 0),
                new Point(0, -10),
                new Point(-15, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };

            locFindBy = new Point[]
            {
                new Point(0, 0),
                new Point(0, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 25).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 25).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 25).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 50).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 50).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 50).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 75).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 75).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 75).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 125).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };
        }

        public void ChangeNpcType(NpcNames npcNames)
        {
            bool changed = npcNameFinder.ChangeNpcType(npcNames);
            wowScreen.Enabled = npcNames != NpcNames.None;
            if (changed)
                ct.WaitHandle.WaitOne(5); // BotController ScreenshotThread 4ms delay when idle
        }

        public void WaitForUpdate()
        {
            npcNameFinder.WaitForUpdate();
        }

        public bool FoundNpcName()
        {
            return npcNameFinder.NpcCount > 0;
        }

        public bool InteractFirst(CancellationToken ct)
        {
            NpcPosition npc = npcNameFinder.Npcs.First();

            for (int i = 0; i < locTargeting.Length; i++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                Point p = locTargeting[i];

                var clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + p.X, npc.ClickPoint.Y + p.Y);
                input.SetCursorPosition(clickPostion);
                ct.WaitHandle.WaitOne(FAST_DELAY);

                CursorClassifier.Classify(out CursorType cls);
                if (cls is CursorType.Kill or CursorType.Vendor)
                {
                    input.InteractMouseOver();
                    logger.LogInformation($"> NPCs found: {npc.Rect}");
                    return true;
                }
            }
            return false;
        }

        public bool FindBy(params CursorType[] cursor)
        {
            int c = locFindBy.Length;
            int e = 3;
            foreach (NpcPosition npc in npcNameFinder.Npcs)
            {
                Point[] attemptPoints = new Point[c + (c * e)];
                for (int i = 0; i < c; i += e)
                {
                    Point p = locFindBy[i];
                    attemptPoints[i] = p;
                    attemptPoints[i + c] = new Point(npc.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    attemptPoints[i + c + 1] = new Point(-npc.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                }

                foreach (Point p in attemptPoints)
                {
                    Point clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + p.X, npc.ClickPoint.Y + p.Y);
                    input.SetCursorPosition(clickPostion);
                    ct.WaitHandle.WaitOne(FAST_DELAY);

                    CursorClassifier.Classify(out CursorType cls);
                    if (cursor.Contains(cls))
                    {
                        input.InteractMouseOver();
                        ct.WaitHandle.WaitOne(FAST_DELAY);
                        logger.LogInformation($"> NPCs found: {npc.Rect}");
                        return true;
                    }
                }
            }
            return false;
        }

        public void ShowClickPositions(Graphics gr)
        {
            foreach (NpcPosition npc in npcNameFinder.Npcs)
            {
                foreach (Point p in locFindBy)
                {
                    gr.DrawEllipse(whitePen, p.X + npc.ClickPoint.X, p.Y + npc.ClickPoint.Y, 5, 5);
                }
            }
        }
    }
}
