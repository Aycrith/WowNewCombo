using Game;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace Core
{
    public sealed class FrameConfigurator : IDisposable
    {
        private const int MAX_HEIGHT = 25; // this one just arbitrary number for sanity check

        private readonly ILogger logger;
        private readonly WowProcess wowProcess;
        private readonly WowScreen wowScreen;
        private readonly WowProcessInput wowProcessInput;
        private readonly ExecGameCommand execGameCommand;
        private readonly AddonConfigurator addonConfigurator;

        private AddonDataProvider? addonDataProvider;

        private Thread? screenshotThread;
        private CancellationTokenSource cts = new();
        private const int interval = 500;

        public DataFrameMeta DataFrameMeta { get; private set; } = DataFrameMeta.Empty;

        public DataFrame[] DataFrames { get; private set; } = Array.Empty<DataFrame>();

        public bool Saved { get; private set; }
        public bool AddonNotVisible { get; private set; }

        public string ImageBase64 { private set; get; } = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";

        public event Action? OnUpdate;

        public FrameConfigurator(ILogger logger, WowProcess wowProcess, WowScreen wowScreen, WowProcessInput wowProcessInput, ExecGameCommand execGameCommand, AddonConfigurator addonConfigurator)
        {
            this.logger = logger;
            this.wowProcess = wowProcess;
            this.wowScreen = wowScreen;
            this.wowProcessInput = wowProcessInput;
            this.execGameCommand = execGameCommand;
            this.addonConfigurator = addonConfigurator;
        }

        public void Dispose()
        {
            cts.Cancel();
        }

        private void ScreenshotRefreshThread()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (wowProcess.IsRunning)
                    {
                        if (DataFrameMeta == DataFrameMeta.Empty)
                        {
                            AddonNotVisible = false;
                            DataFrameMeta = GetDataFrameMeta();

                            OnUpdate?.Invoke();
                        }
                        else
                        {
                            DataFrameMeta temp = GetDataFrameMeta();
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

                            Size size = DataFrameMeta.EstimatedSize(screenRect);

                            if (!size.IsEmpty &&
                                size.Width <= screenRect.Size.Width &&
                                size.Height <= screenRect.Size.Height &&
                                size.Height <= MAX_HEIGHT)
                            {
                                Bitmap bitmap = wowScreen.GetBitmap(size.Width, size.Height);
                                UpdatePreview(bitmap);

                                if (DataFrameMeta == DataFrameMeta.Empty)
                                {
                                    DataFrameMeta = FrameConfig.GetMeta(bitmap.GetPixel(0, 0));
                                }

                                if (DataFrames.Length != DataFrameMeta.frames)
                                {
                                    DataFrames = FrameConfig.TryCreateFrames(DataFrameMeta, bitmap);
                                }
                                bitmap.Dispose();

                                if (DataFrames.Length == DataFrameMeta.frames && addonDataProvider == null)
                                {
                                    addonDataProvider = new AddonDataProvider(wowScreen, DataFrames);
                                }
                                OnUpdate?.Invoke();
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
                        AddonNotVisible = true;
                        DataFrameMeta = DataFrameMeta.Empty;

                        OnUpdate?.Invoke();
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
            wowScreen.GetPosition(ref location);
            if (location.X < 0)
            {
                logger.LogWarning($"Client window outside of the visible area of the screen by {location}");
                return DataFrameMeta.Empty;
            }

            Bitmap bitmap = wowScreen.GetBitmap(5, 5);
            return FrameConfig.GetMeta(bitmap.GetPixel(0, 0));
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
            Version? version = addonConfigurator.GetInstallVersion();
            if (version == null)
                return false;

            if (DataFrames.Length != DataFrameMeta.frames)
                return false;

            wowScreen.GetRectangle(out Rectangle rect);

            FrameConfig.Save(rect, version, DataFrameMeta, DataFrames);
            Saved = true;

            OnUpdate?.Invoke();

            return true;
        }

        public bool StartAutoConfig()
        {
            if (!wowProcess.IsRunning)
            {
                logger.LogInformation("Wow Process no longer running!");
                return false;
            }

            logger.LogInformation("Found WowProcess");
            OnUpdate?.Invoke();

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

            Version? version = addonConfigurator.GetInstallVersion();
            if (version == null)
            {
                OnUpdate?.Invoke();
                return false;
            }
            logger.LogInformation($"Addon installed. Version {version}");

            wowProcessInput.SetForegroundWindow();
            cts.Token.WaitHandle.WaitOne(100);

            DataFrameMeta meta = GetDataFrameMeta();
            if (meta == DataFrameMeta.Empty || meta.hash == 0)
            {
                logger.LogInformation("Enter configuration mode.");

                ToggleInGameConfiguration(execGameCommand);
                cts.Token.WaitHandle.WaitOne(interval);
                meta = GetDataFrameMeta();
            }

            if (meta == DataFrameMeta.Empty)
            {
                logger.LogWarning("Unable to enter configuration mode! You might running the game with admin privileges! Please restart the game without it!");
                OnUpdate?.Invoke();
                return false;
            }

            logger.LogInformation($"DataFrameMeta: hash: {meta.hash} | spacing: {meta.spacing} | size: {meta.size} | rows: {meta.rows} | frames: {meta.frames}");

            Size size = meta.EstimatedSize(rect);
            if (size.Height > MAX_HEIGHT || size.IsEmpty)
            {
                logger.LogWarning($"Something is worng. esimated size: {size}.");
                OnUpdate?.Invoke();
                return false;
            }

            Bitmap bitmap = wowScreen.GetBitmap(size.Width, size.Height);

            logger.LogInformation($"Found cells - {rect} - estimated size {size}");

            UpdatePreview(bitmap);

            OnUpdate?.Invoke();
            cts.Token.WaitHandle.WaitOne(interval);

            DataFrame[] dataFrames = FrameConfig.TryCreateFrames(meta, bitmap);
            if (dataFrames.Length != meta.frames)
            {
                return false;
            }

            logger.LogInformation($"Exit configuration mode.");
            ToggleInGameConfiguration(execGameCommand);
            cts.Token.WaitHandle.WaitOne(interval);

            addonDataProvider?.Dispose();
            addonDataProvider = new AddonDataProvider(wowScreen, dataFrames);

            if (!TryResolveRaceAndClass(out RaceEnum race, out PlayerClassEnum @class))
            {
                logger.LogError($"Unable to identify {nameof(RaceEnum)} and {nameof(PlayerClassEnum)}!");
                return false;
            }

            logger.LogInformation($"Found {race.ToStringF()} {@class.ToStringF()}!");

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


        public bool TryResolveRaceAndClass(out RaceEnum raceEnum, out PlayerClassEnum playerClassEnum)
        {
            if (addonDataProvider == null)
            {
                raceEnum = RaceEnum.None;
                playerClassEnum = PlayerClassEnum.None;

                return false;
            }

            addonDataProvider.Update();
            int raceClassCombo = addonDataProvider.GetInt(46);

            raceEnum = (RaceEnum)(raceClassCombo / 100f);
            playerClassEnum = (PlayerClassEnum)(raceClassCombo - ((int)raceEnum * 100f));

            return Enum.IsDefined(raceEnum) && Enum.IsDefined(playerClassEnum);
        }
    }
}
