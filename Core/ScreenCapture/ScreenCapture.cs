using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

using Game;

using Microsoft.Extensions.Logging;

namespace Core;

public sealed partial class ScreenCapture : ScreenCaptureCleaner, IDisposable
{
    private readonly ILogger logger;
    private readonly DataConfig dataConfig;
    private readonly WowScreen wowScreen;
    private readonly CancellationToken token;

    private readonly ManualResetEventSlim manualReset;
    private readonly Thread thread;

    private readonly Bitmap bitmap;
    private readonly Graphics graphics;

    public ScreenCapture(ILogger logger, DataConfig dataConfig,
        CancellationTokenSource cts, WowScreen wowScreen)
        : base(logger, dataConfig)
    {
        this.logger = logger;
        this.dataConfig = dataConfig;
        this.token = cts.Token;
        this.wowScreen = wowScreen;

        bitmap = new(wowScreen.Rect.Width, wowScreen.Rect.Height);
        graphics = Graphics.FromImage(bitmap);

        manualReset = new(false);
        thread = new(Thread);
        thread.Start();
    }

    public void Dispose()
    {
        manualReset.Set();

        graphics.Dispose();
        bitmap.Dispose();
    }

    private void Thread()
    {
        manualReset.Wait();

        while (!token.IsCancellationRequested)
        {
            manualReset.Reset();
            try
            {
                string fileName = $"{DateTimeOffset.Now:MM_dd_HH_mm_ss_fff}.jpg";
                LogScreenCapture(logger, fileName);

                wowScreen.DrawBitmapTo(graphics);
                bitmap.Save(Path.Join(dataConfig.Screenshot, fileName), ImageFormat.Jpeg);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
            manualReset.Wait();
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"{nameof(ScreenCapture)} thread stopped!");
    }

    public override void Request()
    {
        manualReset.Set();
    }


    #region Logging 

    [LoggerMessage(
        EventId = 111,
        Level = LogLevel.Information,
        Message = "[ScreenCapture] {fileName}")]
    static partial void LogScreenCapture(ILogger logger, string fileName);

    #endregion
}
