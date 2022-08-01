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
        private enum Stage
        {
            Reset,
            DetectRunningGame,
            CheckGameWindowLocation,
            EnterConfigMode,
            ValidateMetaSize,
            CreateDataFrames,
            ReturnNormalMode,
            UpdateReader,
            ValidateData,
            Done
        }

        private Stage stage = Stage.Reset;

        private const int MAX_HEIGHT = 25; // this one just arbitrary number for sanity check
        private const int INTERVAL = 500;

        private readonly ILogger logger;
        private readonly WowProcess wowProcess;
        private readonly WowScreen wowScreen;
        private readonly WowProcessInput wowProcessInput;
        private readonly ExecGameCommand execGameCommand;
        private readonly AddonConfigurator addonConfigurator;
        private readonly Wait wait;
        private readonly IAddonDataProvider reader;

        private Thread? screenshotThread;
        private CancellationTokenSource cts = new();

        public DataFrameMeta DataFrameMeta { get; private set; } = DataFrameMeta.Empty;

        public DataFrame[] DataFrames { get; private set; } = Array.Empty<DataFrame>();

        public bool Saved { get; private set; }
        public bool AddonNotVisible { get; private set; }

        public string ImageBase64 { private set; get; } = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";

        private Rectangle screenRect = Rectangle.Empty;
        private Size size = Size.Empty;

        public event Action? OnUpdate;

        public FrameConfigurator(ILogger logger, Wait wait,
            WowProcess wowProcess, IAddonDataProvider reader,
            WowScreen wowScreen, WowProcessInput wowProcessInput,
            ExecGameCommand execGameCommand, AddonConfigurator addonConfigurator)
        {
            this.logger = logger;
            this.wait = wait;
            this.wowProcess = wowProcess;
            this.reader = reader;
            this.wowScreen = wowScreen;
            this.wowProcessInput = wowProcessInput;
            this.execGameCommand = execGameCommand;
            this.addonConfigurator = addonConfigurator;
        }

        public void Dispose()
        {
            cts.Cancel();
        }

        private void ManualConfigThread()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                DoConfig(false);

                OnUpdate?.Invoke();
                cts.Token.WaitHandle.WaitOne(INTERVAL);
                wait.Update();
            }
            screenshotThread = null;
        }

        private bool DoConfig(bool auto)
        {
            switch (stage)
            {
                case Stage.Reset:
                    screenRect = Rectangle.Empty;
                    size = Size.Empty;
                    ResetConfigState();

                    stage++;
                    break;
                case Stage.DetectRunningGame:
                    if (wowProcess.IsRunning)
                    {
                        if (auto)
                        {
                            logger.LogInformation($"Found {nameof(WowProcess)}");
                        }
                        stage++;
                    }
                    else
                    {
                        if (auto)
                        {
                            logger.LogWarning($"{nameof(WowProcess)} no longer running!");
                            return false;
                        }
                        stage--;
                    }
                    break;
                case Stage.CheckGameWindowLocation:
                    wowScreen.GetRectangle(out screenRect);
                    if (screenRect.Location.X < 0 || screenRect.Location.Y < 0)
                    {
                        logger.LogWarning($"Client window outside of the visible area of the screen {screenRect.Location}");
                        stage = Stage.Reset;

                        if (auto)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        AddonNotVisible = false;
                        stage++;

                        if (auto)
                        {
                            logger.LogInformation($"Client window: {screenRect}");
                        }
                    }
                    break;
                case Stage.EnterConfigMode:
                    if (auto)
                    {
                        Version? version = addonConfigurator.GetInstallVersion();
                        if (version == null)
                        {
                            stage = Stage.Reset;
                            logger.LogError($"Addon is not installed!");
                            return false;
                        }
                        logger.LogInformation($"Addon installed! Version: {version}");

                        logger.LogInformation("Enter configuration mode.");
                        wowProcessInput.SetForegroundWindow();
                        wait.Fixed(INTERVAL);
                        ToggleInGameConfiguration(execGameCommand);
                        wait.Update();
                    }

                    DataFrameMeta temp = GetDataFrameMeta();
                    if (DataFrameMeta == DataFrameMeta.Empty && temp != DataFrameMeta.Empty)
                    {
                        DataFrameMeta = temp;
                        stage++;

                        if (auto)
                        {
                            logger.LogInformation($"DataFrameMeta: {DataFrameMeta}");
                        }
                    }
                    break;
                case Stage.ValidateMetaSize:
                    size = DataFrameMeta.EstimatedSize(screenRect);
                    if (!size.IsEmpty &&
                        size.Width <= screenRect.Size.Width &&
                        size.Height <= screenRect.Size.Height &&
                        size.Height <= MAX_HEIGHT)
                    {
                        stage++;
                    }
                    else
                    {
                        logger.LogWarning($"Addon Rect({size}) size issue. Either too small or too big!");
                        stage = Stage.Reset;

                        if (auto)
                            return false;
                    }
                    break;
                case Stage.CreateDataFrames:
                    Bitmap bitmap = wowScreen.GetBitmap(size.Width, size.Height);
                    if (!auto)
                    {
                        using MemoryStream ms = new();
                        bitmap.Save(ms, ImageFormat.Png);
                        this.ImageBase64 = Convert.ToBase64String(ms.ToArray());
                    }

                    DataFrames = FrameConfig.TryCreateFrames(DataFrameMeta, bitmap);
                    if (DataFrames.Length == DataFrameMeta.frames)
                    {
                        stage++;
                    }
                    else
                    {
                        logger.LogWarning($"DataFrameMeta and FrameConfig dosen't match Frames: ({DataFrames.Length}) != Meta: ({DataFrameMeta.frames})");
                        stage = Stage.Reset;

                        if (auto)
                            return false;
                    }

                    bitmap.Dispose();
                    break;
                case Stage.ReturnNormalMode:
                    if (auto)
                    {
                        logger.LogInformation($"Exit configuration mode.");
                        wowProcessInput.SetForegroundWindow();
                        ToggleInGameConfiguration(execGameCommand);
                        wait.Fixed(INTERVAL);
                    }

                    if (GetDataFrameMeta() == DataFrameMeta.Empty)
                        stage++;
                    break;
                case Stage.UpdateReader:
                    reader.InitFrames(DataFrames);
                    wait.Update();
                    wait.Update();
                    stage++;
                    break;
                case Stage.ValidateData:
                    if (TryResolveRaceAndClass(out UnitRace race, out UnitClass @class) &&
                        race != UnitRace.None && @class != UnitClass.None)
                    {
                        if (auto)
                        {
                            logger.LogInformation($"Found {race.ToStringF()} {@class.ToStringF()}!");
                        }

                        stage++;
                    }
                    else
                    {
                        logger.LogError($"Unable to identify {nameof(UnitRace)} and {nameof(UnitClass)}!");
                        stage = Stage.Reset;

                        if (auto)
                            return false;
                    }
                    break;
                case Stage.Done:
                    return false;
                default:
                    break;
            }

            return true;
        }


        private void ResetConfigState()
        {
            screenRect = Rectangle.Empty;
            size = Size.Empty;

            AddonNotVisible = true;
            stage = Stage.Reset;
            Saved = false;

            DataFrameMeta = DataFrameMeta.Empty;
            DataFrames = Array.Empty<DataFrame>();

            reader.InitFrames(DataFrames);
            wait.Update();
        }

        private DataFrameMeta GetDataFrameMeta()
        {
            using Bitmap bitmap = wowScreen.GetBitmap(5, 5);
            return FrameConfig.GetMeta(bitmap.GetPixel(0, 0));
        }

        public void ToggleManualConfig()
        {
            if (screenshotThread == null)
            {
                ResetConfigState();

                cts.Dispose();
                cts = new();
                screenshotThread = new Thread(ManualConfigThread);
                screenshotThread.Start();
            }
            else
            {
                cts.Cancel();
            }
        }

        public bool FinishConfig()
        {
            Version? version = addonConfigurator.GetInstallVersion();
            if (version == null ||
                DataFrames.Length == 0 ||
                DataFrameMeta.frames == 0 ||
                DataFrames.Length != DataFrameMeta.frames)
            {
                logger.LogInformation($"Frame configuration was incomplete! Please try again, after resolving the previusly mentioned issue...");
                ResetConfigState();
                return false;
            }

            wowScreen.GetRectangle(out Rectangle rect);
            FrameConfig.Save(rect, version, DataFrameMeta, DataFrames);
            logger.LogInformation($"Frame configuration was successful! Configuration saved!");
            Saved = true;

            return true;
        }

        public bool StartAutoConfig()
        {
            while (DoConfig(true))
            {
                wait.Update();
            }

            return FinishConfig();
        }

        public static void DeleteConfig()
        {
            FrameConfig.Delete();
        }

        private void ToggleInGameConfiguration(ExecGameCommand exec)
        {
            exec.Run($"/{addonConfigurator.Config.Command}");
        }

        public bool TryResolveRaceAndClass(out UnitRace race, out UnitClass @class)
        {
            int raceClassCombo = reader.GetInt(46);

            race = (UnitRace)(raceClassCombo / 100f);
            @class = (UnitClass)(raceClassCombo - ((int)race * 100f));

            return Enum.IsDefined(race) && Enum.IsDefined(@class);
        }
    }
}
