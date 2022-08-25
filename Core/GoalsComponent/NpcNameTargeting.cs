using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using SharedLib.Extensions;
using SharedLib.NpcFinder;
using Game;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Core.Goals
{
    public class NpcNameTargeting : IDisposable
    {
        private const int FAST_DELAY = 5;
        private const int INTERACT_DELAY = 25;

        private readonly ILogger logger;
        private readonly CancellationToken ct;
        private readonly IWowScreen wowScreen;
        private readonly NpcNameFinder npcNameFinder;
        private readonly IMouseInput input;
        private readonly IMouseOverReader mouseOverReader;
        private readonly Wait wait;

        private readonly CursorClassifier classifier;

        private IBlacklist mouseOverBlacklist;

        private readonly Pen whitePen;

        private readonly HashSet<int> blacklistIndexes;
        private int npcCount = -1;

        public int NpcCount => npcNameFinder.NpcCount;

        public Point[] locTargeting { get; }
        public Point[] locFindBy { get; }

        public NpcNameTargeting(ILogger logger, CancellationTokenSource cts,
            IWowScreen wowScreen, NpcNameFinder npcNameFinder, IMouseInput input,
            IMouseOverReader mouseOverReader, IBlacklist blacklist, Wait wait)
        {
            this.logger = logger;
            ct = cts.Token;
            this.wowScreen = wowScreen;
            this.npcNameFinder = npcNameFinder;
            this.input = input;
            this.mouseOverReader = mouseOverReader;
            this.mouseOverBlacklist = blacklist;
            this.wait = wait;

            classifier = new();

            whitePen = new Pen(Color.White, 3);

            blacklistIndexes = new();

            locTargeting = new Point[]
            {
                new Point(0, 0),
                new Point(-5, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(5, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };

            locFindBy = new Point[]
            {
                new Point(0, 0),
                new Point(0, 15).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 50).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 50).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 50).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 100).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 100).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 100).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0, 150).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 150).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(15, 150).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),

                new Point(0,   200).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 200).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
                new Point(-15, 200).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            };
        }

        public void Dispose()
        {
            classifier.Dispose();
        }

        public void UpdateBlacklist(IBlacklist blacklist)
        {
            this.mouseOverBlacklist = blacklist;

            logger.LogInformation($"{nameof(NpcNameTargeting)}: set blacklist to {blacklist.GetType().Name}");
        }

        public void ChangeNpcType(NpcNames npcNames)
        {
            bool changed = npcNameFinder.ChangeNpcType(npcNames);
            wowScreen.Enabled = npcNames != NpcNames.None;
            if (changed)
                ct.WaitHandle.WaitOne(5); // BotController ScreenshotThread 4ms delay when idle
        }

        public void Reset()
        {
            npcCount = -1;
            blacklistIndexes.Clear();
        }

        public void WaitForUpdate()
        {
            npcNameFinder.WaitForUpdate();
        }

        public bool FoundNpcName()
        {
            return npcNameFinder.NpcCount > 0;
        }

        public bool AquireNonBlacklisted(CancellationToken ct)
        {
            if (npcCount != NpcCount)
            {
                npcCount = NpcCount;
                blacklistIndexes.Clear();
            }

            int index = blacklistIndexes.Count;
            if (index >= npcCount)
                return false;
            NpcPosition npc = npcNameFinder.Npcs.ElementAt(index);

            for (int i = 0; i < locTargeting.Length; i++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                Point p = locTargeting[i];

                var clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + p.X, npc.ClickPoint.Y + p.Y);
                input.SetCursorPosition(clickPostion);
                ct.WaitHandle.WaitOne(FAST_DELAY);

                classifier.Classify(out CursorType cls);
                if (cls is CursorType.Kill or CursorType.Vendor)
                {
                    wait.Update();
                    bool blacklisted = false;
                    if (mouseOverReader.MouseOverId == 0 || (blacklisted = mouseOverBlacklist.Is()))
                    {
                        if (blacklisted)
                        {
                            //logger.LogInformation($"> NPCs {index} added to blacklist {mouseOverReader.MouseOverId} - {npc.Rect}");
                            blacklistIndexes.Add(index);
                        }

                        return false;
                    }

                    logger.LogInformation($"> mouseover NPC found: {mouseOverReader.MouseOverId} - {npc.Rect}");
                    input.InteractMouseOver();
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
                    attemptPoints[i + c] = new Point(npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    attemptPoints[i + c + 1] = new Point(-npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                }

                foreach (Point p in attemptPoints)
                {
                    Point clickPostion = npcNameFinder.ToScreenCoordinates(npc.ClickPoint.X + p.X, npc.ClickPoint.Y + p.Y);
                    input.SetCursorPosition(clickPostion);
                    ct.WaitHandle.WaitOne(INTERACT_DELAY);

                    classifier.Classify(out CursorType cls);
                    if (cursor.Contains(cls))
                    {
                        input.InteractMouseOver();
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
