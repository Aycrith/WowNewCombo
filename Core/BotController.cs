using Core.Goals;
using Core.GOAP;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Core.Session;
using SharedLib;
using Game;
using WinAPI;
using SharedLib.NpcFinder;
using Cyotek.Collections.Generic;

namespace Core
{
    public sealed class BotController : IBotController, IDisposable
    {
        private readonly WowProcess wowProcess;
        private readonly WowProcessInput wowProcessInput;
        private readonly ILogger logger;
        private readonly IPPather pather;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly AddonDataProvider addonDataProvider;
        private readonly INodeFinder minimapNodeFinder;
        private readonly CancellationTokenSource cts;
        private readonly Wait wait;
        private readonly AutoResetEvent globalTimeAutoResetEvent = new(false);
        private readonly AutoResetEvent npcNameFinderAutoResetEvent = new(false);

        public DataConfig DataConfig { get; }

        public AddonReader AddonReader { get; }

        public WowScreen WowScreen { get; }

        public IImageProvider? MinimapImageFinder { get; }

        public ExecGameCommand ExecGameCommand { get; }

        public IGrindSession GrindSession { get; }
        public IGrindSessionHandler GrindSessionHandler { get; }

        public string SelectedClassFilename { get; set; } = string.Empty;
        public string? SelectedPathFilename { get; set; }
        public ClassConfiguration? ClassConfig { get; set; }
        public ActionBarPopulator? ActionBarPopulator { get; set; }
        public GoapAgent? GoapAgent { get; set; }
        public RouteInfo? RouteInfo { get; set; }

        private readonly Thread addonThread;

        private readonly Thread? screenshotThread;
        private const int screenshotTickMs = 200;

        private GoalThread? goalThread;
        private Thread? botThread;

        public event Action? ProfileLoaded;
        public event Action? StatusChanged;

        public double AvgScreenLatency
        {
            get
            {
                double avg = 0;
                for (int i = 0; i < ScreenLatencys.Size; i++)
                {
                    avg += ScreenLatencys.PeekAt(i);
                }
                return avg /= ScreenLatencys.Size;
            }
        }
        private readonly CircularBuffer<double> ScreenLatencys;

        public double AvgNPCLatency
        {
            get
            {
                double avg = 0;
                for (int i = 0; i < NPCLatencys.Size; i++)
                {
                    avg += NPCLatencys.PeekAt(i);
                }
                return avg /= NPCLatencys.Size;
            }
        }
        private readonly CircularBuffer<double> NPCLatencys;

        private readonly Thread? remotePathing;
        private const int remotePathingTickMs = 500;

        private readonly Thread frontendThread;
        private const int frontendTickMs = 250;

        public bool IsBotActive => goalThread != null && goalThread.Active;

        public BotController(ILogger logger, IPPather pather, DataConfig dataConfig)
        {
            this.logger = logger;
            this.pather = pather;
            this.DataConfig = dataConfig;

            cts = new();

            wowProcess = new();
            WowScreen = new(logger, wowProcess);
            wowProcessInput = new(logger, wowProcess);

            ExecGameCommand = new(logger, wowProcessInput);

            GrindSessionHandler = new LocalGrindSessionHandler(dataConfig.History);
            GrindSession = new GrindSession(this, GrindSessionHandler, cts);

            List<DataFrame> frames = DataFrameConfiguration.LoadFrames();

            addonDataProvider = new(WowScreen, frames);
            AddonReader = new(logger, DataConfig, addonDataProvider, globalTimeAutoResetEvent);

            wait = new(globalTimeAutoResetEvent);

            minimapNodeFinder = new MinimapNodeFinder(WowScreen, new PixelClassifier());
            MinimapImageFinder = minimapNodeFinder as IImageProvider;

            ScreenLatencys = new(8);
            NPCLatencys = new(8);

            addonThread = new(AddonThread);
            addonThread.Start();

            frontendThread = new(FrontendThread);
            frontendThread.Start();

            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                wait.Update();

                if (sw.ElapsedMilliseconds > 5000)
                {
                    logger.LogWarning("There is a problem with the addon, I have been unable to read the player class. Is it running ?");
                    sw.Restart();
                }
            } while (!Enum.GetValues(typeof(PlayerClassEnum)).Cast<PlayerClassEnum>().Contains(AddonReader.PlayerReader.Class));

            logger.LogDebug($"Woohoo, I have read the player class. You are a {AddonReader.PlayerReader.Race} {AddonReader.PlayerReader.Class}.");

            npcNameFinder = new(logger, WowScreen, npcNameFinderAutoResetEvent);
            npcNameTargeting = new(logger, cts, npcNameFinder, wowProcessInput);
            WowScreen.AddDrawAction(npcNameFinder.ShowNames);
            WowScreen.AddDrawAction(npcNameTargeting.ShowClickPositions);

            screenshotThread = new(ScreenshotThread);
            screenshotThread.Start();

            if (pather is RemotePathingAPI)
            {
                remotePathing = new(RemotePathingThread);
                remotePathing.Start();
            }
        }

        private void AddonThread()
        {
            while (!cts.IsCancellationRequested)
            {
                AddonReader.Update();
                cts.Token.WaitHandle.WaitOne(1);
            }
            logger.LogInformation("Addon thread stoppped!");
        }

        private void ScreenshotThread()
        {
            Stopwatch stopWatch = new();
            while (!cts.IsCancellationRequested)
            {
                if (WowScreen.Enabled)
                {
                    stopWatch.Restart();
                    WowScreen.Update();
                    ScreenLatencys.Put(stopWatch.ElapsedMilliseconds);

                    stopWatch.Restart();
                    npcNameFinder.Update();
                    NPCLatencys.Put(stopWatch.ElapsedMilliseconds);

                    if (WowScreen.EnablePostProcess)
                        WowScreen.PostProcess();
                }
                else
                {
                    npcNameFinder.FakeUpdate();
                }

                if (ClassConfig?.Mode == Mode.AttendedGather)
                {
                    stopWatch.Restart();
                    minimapNodeFinder.TryFind();
                    ScreenLatencys.Put(stopWatch.ElapsedMilliseconds);
                }

                cts.Token.WaitHandle.WaitOne(WowScreen.Enabled ||
                    ClassConfig?.Mode == Mode.AttendedGather ? screenshotTickMs : 4);
            }
            logger.LogInformation("Screenshot thread stoppped!");
        }

        private void RemotePathingThread()
        {
            while (!cts.IsCancellationRequested)
            {
                pather.DrawSphere(new PPather.SphereArgs
                {
                    Colour = AddonReader.PlayerReader.Bits.PlayerInCombat ? 1 : AddonReader.PlayerReader.HasTarget ? 6 : 2,
                    Name = "Player",
                    MapId = this.AddonReader.UIMapId.Value,
                    Spot = this.AddonReader.PlayerReader.PlayerLocation
                });

                cts.Token.WaitHandle.WaitOne(remotePathingTickMs);
            }
            logger.LogInformation("RemotePathing thread stoppped!");
        }

        private void FrontendThread()
        {
            while (!cts.IsCancellationRequested)
            {
                AddonReader.UpdateUI();
                cts.Token.WaitHandle.WaitOne(frontendTickMs);
            }
            logger.LogInformation("Frontend Thread stopped!");
        }

        private void BotThread()
        {
            if (goalThread != null)
            {
                goalThread.ResumeIfNeeded();

                while (!cts.IsCancellationRequested && goalThread.Active)
                {
                    goalThread.Update();
                    cts.Token.WaitHandle.WaitOne(1);
                }
            }

            logger.LogInformation("Bot thread stopped!");
        }


        public void ToggleBotStatus()
        {
            if (goalThread == null)
                return;

            if (!goalThread.Active)
            {
                if (ClassConfig?.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    GrindSession.StartBotSession();
                }

                goalThread.Active = true;
                botThread = new(BotThread);
                botThread.Start();
            }
            else
            {
                goalThread.Active = false;
                if (ClassConfig?.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    GrindSession.StopBotSession("stopped", false);
                }

                WowScreen.Enabled = false;
            }

            StatusChanged?.Invoke();
        }


        private bool InitialiseFromFile(string classFile, string? pathFile)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ClassConfig?.Dispose();
                ClassConfig = ReadClassConfiguration(classFile, pathFile);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return false;
            }

            Initialize(ClassConfig);

            logger.LogInformation($"[{nameof(BotController)}] Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

            return true;
        }

        private void Initialize(ClassConfiguration config)
        {
            AddonReader.SoftReset();

            ConfigurableInput configurableInput = new(logger, wowProcess, config);

            ActionBarPopulator = new(logger, config, AddonReader, ExecGameCommand);

            GoalFactory goalFactory = new(logger, AddonReader, configurableInput, DataConfig, npcNameFinder, npcNameTargeting, pather, ExecGameCommand);

            GoapAgentState goapAgentState = new();
            (RouteInfo routeInfo, HashSet<GoapGoal> availableActions) = goalFactory.CreateGoals(config, goapAgentState, cts, wait);

            GoapAgent?.Dispose();
            GoapAgent = new(logger, WowScreen, goapAgentState, AddonReader, availableActions);

            RouteInfo = routeInfo;
            goalThread = new(logger, GoapAgent, AddonReader, configurableInput, RouteInfo!);

            foreach (GoapGoal a in availableActions)
            {
                a.ActionEvent += goalThread.OnActionEvent;
                a.ActionEvent += GoapAgent.OnActionEvent;

                foreach (GoapGoal b in availableActions)
                {
                    if (b != a) { a.ActionEvent += b.OnActionEvent; }
                }
            }
        }

        private ClassConfiguration ReadClassConfiguration(string classFilename, string? pathFilename)
        {
            if (!classFilename.ToLower().Contains(AddonReader.PlayerReader.Class.ToString().ToLower()))
            {
                throw new Exception($"[{nameof(BotController)}] Not allowed to load other class profile!");
            }

            var classFilePath = Path.Join(DataConfig.Class, classFilename);
            if (File.Exists(classFilePath))
            {
                ClassConfiguration classConfig = JsonConvert.DeserializeObject<ClassConfiguration>(File.ReadAllText(classFilePath));
                RequirementFactory requirementFactory = new(logger, AddonReader, npcNameFinder, classConfig.ImmunityBlacklist);
                classConfig.Initialise(DataConfig, AddonReader, requirementFactory, logger, pathFilename);

                logger.LogInformation($"[{nameof(BotController)}] Profile Loaded `{classFilename}` with `{classConfig.PathFilename}`.");

                return classConfig;
            }

            throw new ArgumentOutOfRangeException($"Class config file not found {classFilename}");
        }

        public void Dispose()
        {
            cts.Cancel();

            if (GoapAgent != null)
            {
                foreach (GoapGoal a in GoapAgent.AvailableGoals)
                {
                    if (goalThread != null)
                    {
                        a.ActionEvent -= goalThread.OnActionEvent;
                    }
                    a.ActionEvent -= GoapAgent.OnActionEvent;

                    foreach (GoapGoal b in GoapAgent.AvailableGoals)
                    {
                        if (b != a) { a.ActionEvent -= b.OnActionEvent; }
                    }
                }

                GoapAgent.Dispose();
            }

            npcNameFinderAutoResetEvent.Dispose();
            globalTimeAutoResetEvent.Dispose();
            WowScreen.Dispose();
            addonDataProvider?.Dispose();
            AddonReader.Dispose();
        }

        public void MinimapNodeFound()
        {
            GoapAgent?.NodeFound();
        }

        public void Shutdown()
        {
            cts.Cancel();
        }

        public IEnumerable<string> ClassFiles()
        {
            var root = DataConfig.Class;
            var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
                .Select(path => path.Replace(root, string.Empty))
                .OrderBy(x => x, new NaturalStringComparer())
                .ToList();

            files.Insert(0, "Press Init State first!");
            return files;
        }

        public IEnumerable<string> PathFiles()
        {
            var root = DataConfig.Path;
            var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
                .Select(path => path.Replace(root, string.Empty))
                .OrderBy(x => x, new NaturalStringComparer())
                .ToList();

            files.Insert(0, "Use Class Profile Default");
            return files;
        }

        public void LoadClassProfile(string classFilename)
        {
            if (InitialiseFromFile(classFilename, SelectedPathFilename))
            {
                SelectedClassFilename = classFilename;
            }

            ProfileLoaded?.Invoke();
        }

        public void LoadPathProfile(string pathFilename)
        {
            if (InitialiseFromFile(SelectedClassFilename, pathFilename))
            {
                SelectedPathFilename = pathFilename;
            }

            ProfileLoaded?.Invoke();
        }

        public void OverrideClassConfig(ClassConfiguration classConfig)
        {
            this.ClassConfig = classConfig;
            Initialize(this.ClassConfig);
        }
    }
}