using System;
using System.Diagnostics;

using Core;

using Game;

using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;

#pragma warning disable 0162
#pragma warning disable 8618

#nullable enable

namespace CoreTests;

internal sealed class Test_MinimapNodeFinder : IDisposable
{
    private const bool saveImage = false;
    private const bool LogEachUpdate = false;

    private readonly ILogger logger;

    private readonly WowProcess wowProcess;
    private readonly IWowScreen wowScreen;

    private readonly MinimapNodeFinder minimapNodeFinder;

    private readonly Stopwatch stopwatch = new();

    public Test_MinimapNodeFinder(ILogger logger, WowProcess wowProcess, IWowScreen wowScreen, ILoggerFactory loggerFactory)
    {
        this.logger = logger;

        this.wowProcess = wowProcess;
        this.wowScreen = wowScreen;

        minimapNodeFinder = new(logger, wowScreen);
    }

    public void Dispose()
    {
        wowScreen.Dispose();
        wowProcess.Dispose();
    }

    public void Execute()
    {
        if (LogEachUpdate)
            stopwatch.Restart();

        wowScreen.Update();

        if (LogEachUpdate)
            logger.LogInformation($"Capture: {stopwatch.ElapsedMilliseconds}ms");

        if (LogEachUpdate)
            stopwatch.Restart();

        minimapNodeFinder.Update();

        if (LogEachUpdate)
            logger.LogInformation($"Update: {stopwatch.ElapsedMilliseconds}ms");

        if (saveImage)
        {
            SaveImage();
        }
    }

    private void SaveImage()
    {
        wowScreen.MiniMapImage.SaveAsPng("minimap.png");
    }
}
