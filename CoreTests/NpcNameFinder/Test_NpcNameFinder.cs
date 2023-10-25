using System.Drawing;
using Core.Goals;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System;
using SharedLib.Extensions;
using Core;
using Game;
using SharedLib;

using static System.Diagnostics.Stopwatch;

#pragma warning disable 0162
#nullable enable

namespace CoreTests;

internal sealed class Test_NpcNameFinder : IDisposable
{
    private const bool saveImage = false;
    private const bool showOverlay = true;

    private const bool LogEachUpdate = true;
    private const bool LogEachDetail = false;

    private const bool debugTargeting = false;
    private const bool debugSkinning = false;
    private const bool debugTargetVsAdd = false;

    private readonly ILogger logger;
    private readonly NpcNameFinder npcNameFinder;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly NpcNameTargetingLocations locations;

    private readonly WowProcess wowProcess;
    private readonly IWowScreen wowScreen;

    private readonly Stopwatch stopwatch = new();
    private readonly StringBuilder stringBuilder = new();

    private readonly Graphics paint;
    private readonly Bitmap paintBitmap;
    private readonly Font font = new("Arial", 10);
    private readonly SolidBrush brush = new(Color.White);
    private readonly Pen whitePen = new(Color.White, 1);

    private readonly NpcNameOverlay? npcNameOverlay;

    private DateTime lastNpcUpdate;
    private double updateDuration;

    public Test_NpcNameFinder(ILogger logger, ILoggerFactory loggerFactory, NpcNames types)
    {
        this.logger = logger;

        // its expected to have at least 2 DataFrame 
        DataFrame[] mockFrames = new DataFrame[2]
        {
            new DataFrame(0, 0, 0),
            new DataFrame(1, 0, 0),
        };

        wowProcess = new();
        wowScreen = new WowScreenDXGI(loggerFactory.CreateLogger<WowScreenDXGI>(), wowProcess, mockFrames);
        //wowScreen = new WowScreenGDI(loggerFactory.CreateLogger<WowScreenGDI>(), wowProcess);

        INpcResetEvent npcResetEvent = new NpcResetEvent();
        npcNameFinder = new(logger, wowScreen, npcResetEvent);

        locations = new(npcNameFinder);

        MockMouseOverReader mouseOverReader = new();
        MockGameMenuWindowShown gmws = new();
        MockWowProcessInput wpi = new();

        npcNameTargeting = new(loggerFactory.CreateLogger<NpcNameTargeting>(),
            new(), wowScreen, npcNameFinder, locations, wpi,
            mouseOverReader, new NoBlacklist(), null!, gmws);

        npcNameFinder.ChangeNpcType(types);

        paintBitmap = wowScreen.Bitmap;
        paint = Graphics.FromImage(paintBitmap);

        if (showOverlay)
            npcNameOverlay = new(wowProcess.Process.MainWindowHandle, npcNameFinder,
                locations, debugTargeting, debugSkinning, debugTargetVsAdd);
    }

    ~Test_NpcNameFinder()
    {
        Dispose();
    }

    public void Dispose()
    {
        npcNameOverlay?.Dispose();

        wowScreen.Dispose();
        wowProcess.Dispose();
    }

    public void UpdateScreen()
    {
        wowScreen.Update();
    }

    public (double capture, double update) Execute(int NpcUpdateIntervalMs)
    {
        long captureStart = GetTimestamp();
        UpdateScreen();
        double captureDuration = GetElapsedTime(captureStart).TotalMilliseconds;

        if (LogEachUpdate)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append("Capture: ");
            stringBuilder.Append($"{captureDuration:F5}");
            stringBuilder.Append("ms");
        }

        if (DateTime.UtcNow > lastNpcUpdate.AddMilliseconds(NpcUpdateIntervalMs))
        {
            long updateStart = GetTimestamp();
            npcNameFinder.Update();
            updateDuration = GetElapsedTime(updateStart).TotalMilliseconds;

            lastNpcUpdate = DateTime.UtcNow;
        }

        if (LogEachUpdate)
        {
            stringBuilder.Append(" | ");
            stringBuilder.Append($"Update: ");
            stringBuilder.Append($"{updateDuration:F5}");
            stringBuilder.Append("ms");

            logger.LogInformation(stringBuilder.ToString());
        }

        if (saveImage)
        {
            SaveImage();
        }

        if (LogEachUpdate && LogEachDetail)
        {
            stringBuilder.Length = 0;

            if (npcNameFinder.Npcs.Count > 0)
                stringBuilder.AppendLine();

            int i = 0;
            foreach (NpcPosition n in npcNameFinder.Npcs)
            {
                stringBuilder.Append($"{i,2} ");
                stringBuilder.AppendLine(n.ToString());
                i++;
            }

            logger.LogInformation(stringBuilder.ToString());
        }

        return (captureDuration, updateDuration);
    }

    public bool Execute_FindTargetBy(ReadOnlySpan<CursorType> cursorType)
    {
        return npcNameTargeting.FindBy(cursorType, CancellationToken.None);
    }

    private void SaveImage()
    {
        if (npcNameFinder.Npcs.Count <= 0)
            return;

        paint.DrawRectangle(whitePen, npcNameFinder.Area);

        for (int i = 0; i < npcNameFinder.Npcs.Count; i++)
        {
            NpcPosition npc = npcNameFinder.Npcs[i];

            if (debugTargeting)
            {
                foreach (var l in locations.Targeting)
                {
                    paint.DrawEllipse(whitePen, l.X + npc.ClickPoint.X, l.Y + npc.ClickPoint.Y, 5, 5);
                }
            }

            if (debugSkinning)
            {
                int c = locations.FindBy.Length;
                const int e = 3;
                Point[] attempts = new Point[c + (c * e)];
                for (int j = 0; j < c; j += e)
                {
                    Point p = locations.FindBy[j];
                    attempts[j] = p;
                    attempts[j + c] = new Point(npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                    attempts[j + c + 1] = new Point(-npc.Rect.Width / 2, p.Y).Scale(npcNameFinder.ScaleToRefWidth, npcNameFinder.ScaleToRefHeight);
                }

                foreach (var l in attempts)
                {
                    paint.DrawEllipse(whitePen, l.X + npc.ClickPoint.X, l.Y + npc.ClickPoint.Y, 5, 5);
                }
            }

            paint.DrawRectangle(whitePen, npc.Rect);
            paint.DrawString(i.ToString(), font, brush, new PointF(npc.Rect.Left - 20f, npc.Rect.Top));
        }

        paintBitmap.Save("target_names.png");
    }
}
