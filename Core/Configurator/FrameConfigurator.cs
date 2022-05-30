using Core.Addon;
using Game;
using Microsoft.Extensions.Logging;
using SharedLib;
using System;
using System.Collections.Generic;
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

        private WowProcess? wowProcess;
        private WowScreen? wowScreen;

        private Thread? screenshotThread;
        private CancellationTokenSource cts = new();
        private const int interval = 500;

        private readonly AddonConfigurator addonConfigurator;

        public DataFrameMeta dataFrameMeta { get; private set; } = DataFrameMeta.Empty;

        public List<DataFrame> dataFrames { get; private set; } = new List<DataFrame>();

        private AddonDataProvider? addonDataProvider;
        public AddonReader? AddonReader { get; private set; }

        public bool Saved { get; private set; }
        public bool AddonNotVisible { get; private set; }

        public string ImageBase64 { private set; get; } = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";

        public event Action? OnUpdate;

        public FrameConfigurator(ILogger logger, AddonConfigurator addonConfigurator)
        {
            this.logger = logger;
            this.dataConfig = DataConfig.Load();
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
                        if (dataFrameMeta == DataFrameMeta.Empty)
                        {
                            AddonNotVisible = false;
                            dataFrameMeta = GetDataFrameMeta();

                            OnUpdate?.Invoke();
                        }
                        else
                        {
                            var temp = GetDataFrameMeta();
                            if (temp != DataFrameMeta.Empty && temp.rows != dataFrameMeta.rows)
                            {
                                AddonNotVisible = true;
                                dataFrameMeta = DataFrameMeta.Empty;

                                OnUpdate?.Invoke();
                            }
                        }

                        if (dataFrameMeta != DataFrameMeta.Empty)
                        {
                            wowScreen.GetRectangle(out Rectangle screenRect);

                            if (screenRect.Location.X < 0 || screenRect.Location.Y < 0)
                            {
                                logger.LogWarning($"Client window outside of the visible area of the screen {screenRect.Location}");
                                return;
                            }

                            var addonRect = dataFrameMeta.EstimatedSize(screenRect);

                            if (!addonRect.IsEmpty &&
                                addonRect.Width <= screenRect.Size.Width &&
                                addonRect.Height <= screenRect.Size.Height &&
                                addonRect.Height < 50) // this one just arbitrary number for sanity check
                            {
                                var screenshot = wowScreen.GetBitmap(addonRect.Width, addonRect.Height);
                                if (screenshot != null)
                                {
                                    UpdatePreview(screenshot);

                                    if (dataFrameMeta == DataFrameMeta.Empty)
                                    {
                                        dataFrameMeta = DataFrameConfiguration.GetMeta(screenshot);
                                    }

                                    if (dataFrames.Count != dataFrameMeta.frames)
                                    {
                                        dataFrames = DataFrameConfiguration.CreateFrames(dataFrameMeta, screenshot);
                                    }
                                    screenshot.Dispose();

                                    if (dataFrames.Count == dataFrameMeta.frames)
                                    {
                                        if (AddonReader != null)
                                        {
                                            AddonReader.Dispose();
                                            addonDataProvider = null;
                                        }

                                        if (addonDataProvider == null)
                                        {
                                            addonDataProvider = new AddonDataProvider(wowScreen, dataFrames);
                                        }

                                        AddonReader = new AddonReader(logger, dataConfig, addonDataProvider, new(false));
                                    }

                                    OnUpdate?.Invoke();
                                }
                            }
                            else
                            {
                                AddonNotVisible = true;
                                dataFrameMeta = DataFrameMeta.Empty;

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
                    dataFrameMeta = DataFrameMeta.Empty;

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
            return DataFrameConfiguration.GetMeta(screenshot);
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
            var version = addonConfigurator.GetInstalledVersion();
            if (version == null) return false;

            if (dataFrames.Count != dataFrameMeta.frames)
            {
                return false;
            }

            if (wowScreen == null) return false;
            wowScreen.GetRectangle(out Rectangle rect);

            DataFrameConfiguration.SaveConfiguration(rect, version, dataFrameMeta, dataFrames);
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

            var version = addonConfigurator.GetInstalledVersion();
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

            var dataFrames = DataFrameConfiguration.CreateFrames(meta, screenshot);
            if (dataFrames.Count != meta.frames)
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

            DataFrameConfiguration.SaveConfiguration(rect, version, meta, dataFrames);
            Saved = true;

            logger.LogInformation($"Frame configuration was successful! Configuration saved!");

            OnUpdate?.Invoke();
            cts.Token.WaitHandle.WaitOne(interval);

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
