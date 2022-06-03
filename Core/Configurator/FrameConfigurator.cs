using Game;
using Microsoft.Extensions.Logging;
using SharedLib;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;

namespace Core
{
    public sealed class FrameConfigurator : IDisposable
    {
        private readonly ILogger logger;
        private readonly DataConfig dataConfig;
        private readonly AddonConfigurator addonConfigurator;

        private AddonDataProvider? addonDataProvider;
        public AddonReader? AddonReader { get; private set; }

        private WowProcess? wowProcess;
        private WowScreen? wowScreen;

        private Thread? screenshotThread;
        private CancellationTokenSource cts = new();
        private const int interval = 500;


        public DataFrameMeta DataFrameMeta { get; private set; } = DataFrameMeta.Empty;

        public DataFrame[] DataFrames { get; private set; } = Array.Empty<DataFrame>();

        public bool Saved { get; private set; }
        public bool AddonNotVisible { get; private set; }

        public string ImageBase64 { private set; get; } = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";

        public event Action? OnUpdate;

        public FrameConfigurator(ILogger logger, AddonConfigurator addonConfigurator, DataConfig dataConfig)
        {
            this.logger = logger;
            this.dataConfig = dataConfig;
            this.addonConfigurator = addonConfigurator;
        }

        public void Dispose()
        {
            cts.Cancel();
            wowScreen?.Dispose();
        }

        private void ScreenshotRefreshThread()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (wowProcess != null && wowScreen != null)
                    {
                        if (DataFrameMeta == DataFrameMeta.Empty)
                        {
                            AddonNotVisible = false;
                            DataFrameMeta = GetDataFrameMeta();

                            OnUpdate?.Invoke();
                        }
                        else
                        {
                            var temp = GetDataFrameMeta();
                            if (temp != DataFrameMeta.Empty && temp.rows != DataFrameMeta.rows)
                            {
                                AddonNotVisible = true;
                                DataFrameMeta = DataFrameMeta.Empty;

                                OnUpdate?.Invoke();
                            }
                        }

                        if (DataFrameMeta != DataFrameMeta.Empty)
                        {
                            wowScreen.GetRectangle(out Rectangle screenRect);

                            if (screenRect.Location.X < 0 || screenRect.Location.Y < 0)
                            {
                                logger.LogWarning($"Client window outside of the visible area of the screen {screenRect.Location}");
                                return;
                            }

                            var addonRect = DataFrameMeta.EstimatedSize(screenRect);

                            if (!addonRect.IsEmpty &&
                                addonRect.Width <= screenRect.Size.Width &&
                                addonRect.Height <= screenRect.Size.Height &&
                                addonRect.Height < 50) // this one just arbitrary number for sanity check
                            {
                                var screenshot = wowScreen.GetBitmap(addonRect.Width, addonRect.Height);
                                if (screenshot != null)
                                {
                                    UpdatePreview(screenshot);

                                    if (DataFrameMeta == DataFrameMeta.Empty)
                                    {
                                        DataFrameMeta = FrameConfig.GetMeta(screenshot.GetPixel(0, 0));
                                    }

                                    if (DataFrames.Length != DataFrameMeta.frames)
                                    {
                                        DataFrames = FrameConfig.TryCreateFrames(DataFrameMeta, screenshot);
                                    }
                                    screenshot.Dispose();

                                    if (DataFrames.Length == DataFrameMeta.frames)
                                    {
                                        if (AddonReader != null)
                                        {
                                            AddonReader.Dispose();
                                            addonDataProvider = null;
                                        }

                                        if (addonDataProvider == null)
                                        {
                                            addonDataProvider = new AddonDataProvider(wowScreen, DataFrames);
                                        }

                                        AddonReader = new AddonReader(logger, dataConfig, addonDataProvider, new(false));
                                    }

                                    OnUpdate?.Invoke();
                                }
                            }
                            else
                            {
                                AddonNotVisible = true;
                                DataFrameMeta = DataFrameMeta.Empty;

                                OnUpdate?.Invoke();
                            }
                        }
                    }
                    else
                    {
                        wowProcess = new WowProcess();
                        wowScreen = new WowScreen(logger, wowProcess);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, e.StackTrace);
                    AddonNotVisible = true;
                    DataFrameMeta = DataFrameMeta.Empty;

                    OnUpdate?.Invoke();
                }

                cts.Token.WaitHandle.WaitOne(interval);
            }
            screenshotThread = null;
        }

        private DataFrameMeta GetDataFrameMeta()
        {
            Point location = new();
            wowScreen?.GetPosition(ref location);
            if (location.X < 0)
            {
                logger.LogWarning($"Client window outside of the visible area of the screen by {location}");
                return DataFrameMeta.Empty;
            }

            var screenshot = wowScreen?.GetBitmap(5, 5);
            if (screenshot == null) return DataFrameMeta.Empty;
            return FrameConfig.GetMeta(screenshot.GetPixel(0, 0));
        }

        public void ToggleManualConfig()
        {
            if (screenshotThread == null)
            {
                cts.Dispose();
                cts = new CancellationTokenSource();
                screenshotThread = new Thread(ScreenshotRefreshThread);
                screenshotThread.Start();
            }
            else
            {
                cts.Cancel();
            }
        }

        public bool FinishManualConfig()
        {
            var version = addonConfigurator.GetInstallVersion();
            if (version == null) return false;

            if (DataFrames.Length != DataFrameMeta.frames)
            {
                return false;
            }

            if (wowScreen == null) return false;
            wowScreen.GetRectangle(out Rectangle rect);

            FrameConfig.Save(rect, version, DataFrameMeta, DataFrames);
            Saved = true;

            OnUpdate?.Invoke();

            return true;
        }

        public bool StartAutoConfig()
        {
            if (wowProcess == null)
                wowProcess = new WowProcess();

            if (wowScreen == null && wowProcess != null)
                wowScreen = new WowScreen(logger, wowProcess);

            if (wowProcess == null) return false;
            logger.LogInformation("Found WowProcess");
            OnUpdate?.Invoke();

            if (wowScreen == null) return false;
            Point location = new();
            wowScreen.GetPosition(ref location);

            if (location.X < 0)
            {
                logger.LogWarning($"Please make sure the client window does not outside of the visible area! Currently outside by {location}");
                OnUpdate?.Invoke();
                return false;
            }

            wowScreen.GetRectangle(out Rectangle rect);
            logger.LogInformation($"Found WowScreen Location: {location} - Size: {rect}");

            WowProcessInput wowProcessInput = new(logger, wowProcess);
            ExecGameCommand execGameCommand = new(logger, wowProcessInput);

            var version = addonConfigurator.GetInstallVersion();
            if (version == null)
            {
                OnUpdate?.Invoke();
                return false;
            }
            logger.LogInformation($"Addon installed. Version {version}");

            wowProcessInput.SetForegroundWindow();
            cts.Token.WaitHandle.WaitOne(100);

            var meta = GetDataFrameMeta();
            if (meta == DataFrameMeta.Empty || meta.hash == 0)
            {
                logger.LogInformation("Enter configuration mode.");

                ToggleInGameConfiguration(execGameCommand);
                cts.Token.WaitHandle.WaitOne(interval);
                meta = GetDataFrameMeta();
            }

            if (meta == DataFrameMeta.Empty)
            {
                logger.LogWarning("Unable to enter configuration mode! You most likely running the game with admin privileges! Please restart the game without it!");
                OnUpdate?.Invoke();
                return false;
            }

            logger.LogInformation($"DataFrameMeta: hash: {meta.hash} | spacing: {meta.spacing} | size: {meta.size} | rows: {meta.rows} | frames: {meta.frames}");

            var size = meta.EstimatedSize(rect);
            if (size.Height > 50 || size.IsEmpty)
            {
                logger.LogWarning($"Something is worng. esimated size: {size}.");
                OnUpdate?.Invoke();
                return false;
            }

            var screenshot = wowScreen.GetBitmap(size.Width, size.Height);
            if (screenshot == null)
            {
                OnUpdate?.Invoke();
                return false;
            }

            logger.LogInformation($"Found cells - {rect} - estimated size {size}");

            UpdatePreview(screenshot);

            OnUpdate?.Invoke();
            cts.Token.WaitHandle.WaitOne(interval);

            var dataFrames = FrameConfig.TryCreateFrames(meta, screenshot);
            if (dataFrames.Length != meta.frames)
            {
                return false;
            }

            logger.LogInformation($"Exit configuration mode.");
            ToggleInGameConfiguration(execGameCommand);
            cts.Token.WaitHandle.WaitOne(interval);

            addonDataProvider?.Dispose();
            AddonReader?.Dispose();

            addonDataProvider = new AddonDataProvider(wowScreen, dataFrames);
            AddonReader = new AddonReader(logger, dataConfig, addonDataProvider, new(false));

            if (!ResolveClass())
            {
                logger.LogError("Unable to find class.");
                return false;
            }

            logger.LogInformation("Found Class!");

            OnUpdate?.Invoke();
            cts.Token.WaitHandle.WaitOne(interval);

            FrameConfig.Save(rect, version, meta, dataFrames);
            Saved = true;

            logger.LogInformation($"Frame configuration was successful! Configuration saved!");

            return true;
        }

        private void ToggleInGameConfiguration(ExecGameCommand execGameCommand)
        {
            execGameCommand.Run($"/{addonConfigurator.Config.Command}");
        }

        private void UpdatePreview(Bitmap screenshot)
        {
            using MemoryStream ms = new();
            screenshot.Save(ms, ImageFormat.Png);
            this.ImageBase64 = Convert.ToBase64String(ms.ToArray());
        }


        public bool ResolveClass()
        {
            if (AddonReader != null)
            {
                AddonReader.FetchData();
                return Enum.GetValues(typeof(PlayerClassEnum)).Cast<PlayerClassEnum>().Contains(AddonReader.PlayerReader.Class);
            }
            return false;
        }
    }
}
