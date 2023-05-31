using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using SharedLib.Extensions;
using SharedLib.NpcFinder;
using Game;
using Microsoft.Extensions.Logging;

namespace Core.Goals;

public sealed class NpcNameTargeting : IDisposable
{
    private const int INTERACT_DELAY = 5;

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

    private int index;
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

        locTargeting = new Point[]
        {
            new Point(0, 0),
            new Point(-10, 5).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
            new Point(10, 5).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight),
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
        npcNameFinder.ChangeNpcType(npcNames);
        wowScreen.Enabled = npcNames != NpcNames.None;
    }

    public void Reset()
    {
        npcCount = -1;
        index = 0;
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
            index = 0;
        }

        if (index > NpcCount - 1)
            return false;
        NpcPosition npc = npcNameFinder.Npcs[index];

        for (int i = 0; i < locTargeting.Length; i++)
        {
            if (ct.IsCancellationRequested)
                return false;

            Point p = locTargeting[i];
            p.Offset(npc.ClickPoint);
            p.Offset(npcNameFinder.ToScreenCoordinates());

            input.SetCursorPos(p);
            classifier.Classify(out CursorType cls);

            if (cls is CursorType.Kill)
            {
                if (mouseOverReader.MouseOverId == 0)
                    wait.Update();

                if (mouseOverReader.MouseOverId == 0)
                    continue;

                if (mouseOverBlacklist.Is())
                {
                    logger.LogInformation($"> NPCs {index} added to blacklist {mouseOverReader.MouseOverId} - {npc.Rect}");
                    index++;
                    return false;
                }

                logger.LogInformation($"> mouseover NPC found: {mouseOverReader.MouseOverId} - {npc.Rect}");
                input.InteractMouseOver(ct);
                return true;
            }
            ct.WaitHandle.WaitOne(1);
        }
        return false;
    }

    public bool FindBy(params CursorType[] cursor)
    {
        int c = locFindBy.Length;
        const int e = 3;
        Span<Point> attemptPoints = stackalloc Point[c + (c * e)];

        for (int ni = 0; ni < npcNameFinder.Npcs.Count; ni++)
        {
            NpcPosition npc = npcNameFinder.Npcs[ni];
            for (int i = 0; i < c; i += e)
            {
                Point p = locFindBy[i];
                attemptPoints[i] = p;
                attemptPoints[i + c] = new Point(npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                attemptPoints[i + c + 1] = new Point(-npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
            }

            for (int pi = 0; pi < attemptPoints.Length; pi++)
            {
                Point p = attemptPoints[pi];
                p.Offset(npc.ClickPoint);
                p.Offset(npcNameFinder.ToScreenCoordinates());
                input.SetCursorPos(p);

                ct.WaitHandle.WaitOne(Random.Shared.Next(2, INTERACT_DELAY));

                classifier.Classify(out CursorType cls);
                if (cursor.Contains(cls))
                {
                    input.InteractMouseOver(ct);
                    logger.LogInformation($"> NPCs found: {npc.Rect}");
                    return true;
                }
            }
        }
        return false;
    }

    public void ShowClickPositions(Graphics gr)
    {
        for (int i = 0; i < npcNameFinder.Npcs.Count; i++)
        {
            NpcPosition npc = npcNameFinder.Npcs[i];
            for (int j = 0; j < locFindBy.Length; j++)
            {
                Point p = locFindBy[j];
                p.Offset(npc.ClickPoint);
                gr.DrawEllipse(whitePen, p.X, p.Y, 5, 5);
            }
        }
    }
}
