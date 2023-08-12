using System;
using System.Drawing;
using System.Threading;
using SharedLib.Extensions;
using SharedLib.NpcFinder;
using Game;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Core.Goals;

public sealed partial class NpcNameTargeting : IDisposable
{
    private const int INTERACT_DELAY = 5;

    private readonly ILogger<NpcNameTargeting> logger;
    private readonly CancellationToken ct;
    private readonly IWowScreen wowScreen;
    private readonly NpcNameFinder npcNameFinder;
    private readonly IMouseInput input;
    private readonly IMouseOverReader mouseOverReader;
    private readonly Wait wait;

    private readonly CursorClassifier classifier;

    private readonly IBlacklist mouseOverBlacklist;

    private readonly Pen whitePen;

    private int index;
    private int npcCount = -1;

    public int NpcCount => npcNameFinder.NpcCount;

    public Point[] locTargeting { get; }
    public Point[] locFindBy { get; }

    public NpcNameTargeting(ILogger<NpcNameTargeting> logger,
        CancellationTokenSource cts, IWowScreen wowScreen,
        NpcNameFinder npcNameFinder, IMouseInput input,
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

        float refWidth = npcNameFinder.ScaleToRefWidth;
        float refHeight = npcNameFinder.ScaleToRefHeight;

        locTargeting = new Point[]
        {
            new Point(0, -2),
            new Point(-13, 8).Scale(refWidth, refHeight),
            new Point(13, 8).Scale(refWidth, refHeight),
        };

        locFindBy = new Point[]
        {
            new Point(0, 0),
            new Point(0, 15).Scale(refWidth, refHeight),

            new Point(0, 50).Scale(refWidth, refHeight),
            new Point(-15, 50).Scale(refWidth, refHeight),
            new Point(15, 50).Scale(refWidth, refHeight),

            new Point(0, 100).Scale(refWidth, refHeight),
            new Point(-15, 100).Scale(refWidth, refHeight),
            new Point(15, 100).Scale(refWidth, refHeight),

            new Point(0, 150).Scale(refWidth, refHeight),
            new Point(-15, 150).Scale(refWidth, refHeight),
            new Point(15, 150).Scale(refWidth, refHeight),

            new Point(0,   200).Scale(refWidth, refHeight),
            new Point(-15, 200).Scale(refWidth, refHeight),
            new Point(-15, 200).Scale(refWidth, refHeight),
        };

        // also only visible while using BlazorServer
        //wowScreen.AddDrawAction(npcNameFinder.ShowNames);
        //wowScreen.AddDrawAction(ShowClickPositions);
    }

    public void Dispose()
    {
        classifier.Dispose();
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

    public bool FoundAny()
    {
        return npcNameFinder.NpcCount > 0;
    }

    public bool AcquireNonBlacklisted(CancellationToken ct)
    {
        if (npcCount != NpcCount)
        {
            npcCount = NpcCount;
            index = 0;
        }

        if (index > NpcCount - 1)
            return false;

        ReadOnlySpan<NpcPosition> span = npcNameFinder.Npcs;
        ref readonly NpcPosition npc = ref span[index];

        Point p = locTargeting[Random.Shared.Next(locTargeting.Length)];
        p.Offset(npc.ClickPoint);
        p.Offset(npcNameFinder.ToScreenCoordinates());

        input.SetCursorPos(p);
        classifier.Classify(out CursorType cls, out _);

        if (cls is CursorType.Kill && mouseOverReader.MouseOverId != 0)
        {
            if (mouseOverBlacklist.Is())
            {
                LogBlacklistAdded(logger, index,
                    mouseOverReader.MouseOverId, npc.Rect);
                index++;
                return false;
            }

            LogFoundTarget(logger, cls.ToStringF(), mouseOverReader.MouseOverId,
                npc.Rect);

            input.InteractMouseOver(ct);
            return true;
        }

        return false;
    }

    public bool FindBy(ReadOnlySpan<CursorType> cursors)
    {
        int c = locFindBy.Length;
        const int e = 3;
        Span<Point> attempts = stackalloc Point[c + (c * e)];

        float refWidth = npcNameFinder.ScaleToRefWidth;
        float refHeight = npcNameFinder.ScaleToRefHeight;

        ReadOnlySpan<NpcPosition> span = npcNameFinder.Npcs;
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly NpcPosition npc = ref span[i];
            for (int j = 0; j < c; j += e)
            {
                Point p = locFindBy[j];
                attempts[j] = p;
                attempts[j + c] = new Point(npc.Rect.Width / 2, p.Y).Scale(refWidth, refHeight);
                attempts[j + c + 1] = new Point(-npc.Rect.Width / 2, p.Y).Scale(refWidth, refHeight);
            }

            for (int k = 0; k < attempts.Length; k++)
            {
                Point p = attempts[k];
                p.Offset(npc.ClickPoint);
                p.Offset(npcNameFinder.ToScreenCoordinates());

                input.SetCursorPos(p);

                classifier.Classify(out CursorType cls, out _);
                if (cursors.BinarySearch(cls, Comparer<CursorType>.Default) != -1)
                {
                    input.InteractMouseOver(ct);
                    LogFoundTarget(logger, cls.ToStringF(), mouseOverReader.MouseOverId, npc.Rect);
                    return true;
                }

                wait.Update();
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

    #region Logging

    [LoggerMessage(
        EventId = 0160,
        Level = LogLevel.Warning,
        Message = "NPC {index} added to blacklist {mouseOverId} | {rect}")]
    static partial void LogBlacklistAdded(ILogger logger, int index, int mouseOverId, Rectangle rect);

    [LoggerMessage(
        EventId = 0161,
        Level = LogLevel.Information,
        Message = "NPC found: {cursorType} | {mouseOverId} | {rect}")]
    static partial void LogFoundTarget(ILogger logger, string cursorType, int mouseOverId, Rectangle rect);

    #endregion
}
