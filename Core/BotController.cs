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
using System.Threading.Tasks;
using Core.Session;
using SharedLib;
using Game;
using WinAPI;
using Microsoft.Extensions.Configuration;
using SharedLib.NpcFinder;
using Cyotek.Collections.Generic;

namespace Core
{
    public sealed class BotController : IBotController, IDisposable
    {
        private readonly WowProcess wowProcess;
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

        public Thread? screenshotThread { get; }

        public Thread addonThread { get; }

        public Thread? botThread { get; set; }

        public GoapAgent? GoapAgent { get; set; }

        public RouteInfo? RouteInfo { get; set; }

        public WowScreen WowScreen { get; }
        public WowProcessInput WowProcessInput { get; }

        public ConfigurableInput? ConfigurableInput { get; set; }

        public ClassConfiguration? ClassConfig { get; set; }

        public IImageProvider? MinimapImageFinder { get; }

        public ExecGameCommand ExecGameCommand { get; }

        public ActionBarPopulator? ActionBarPopulator { get; set; }

        public IGrindSession GrindSession { get; }
        public IGrindSessionHandler GrindSessionHandler { get; }
        public string SelectedClassFilename { get; set; } = string.Empty;
        public string? SelectedPathFilename { get; set; }

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

        private const int screenshotTickMs = 200;

        private GoalThread? actionThread;

        private readonly Thread? remotePathing;
        private const int remotePathingTickMs = 500;

        private readonly Thread frontendThread;
        private const int frontendTickMs = 250;

        public BotController(ILogger logger, IPPather pather, DataConfig dataConfig)
        {
            this.logger = logger;
            this.pather = pather;
            this.DataConfig = dataConfig;

            cts = new CancellationTokenSource();

            wowProcess = new WowProcess();
            WowScreen = new WowScreen(logger, wowProcess);
            WowProcessInput = new WowProcessInput(logger, wowProcess);

            ExecGameCommand = new ExecGameCommand(logger, WowProcessInput);

            GrindSessionHandler = new LocalGrindSessionHandler(dataConfig.History);
            GrindSession = new GrindSession(this, GrindSessionHandler, cts);

            var frames = DataFrameConfiguration.LoadFrames();

            addonDataProvider = new AddonDataProvider(WowScreen, frames);
            AddonReader = new AddonReader(logger, DataConfig, addonDataProvider, globalTimeAutoResetEvent);

            wait = new Wait(globalTimeAutoResetEvent);

            minimapNodeFinder = new MinimapNodeFinder(WowScreen, new PixelClassifier());
            MinimapImageFinder = minimapNodeFinder as IImageProvider;

            ScreenLatencys = new CircularBuffer<double>(8);
            NPCLatencys = new CircularBuffer<double>(8);

            addonThread = new Thread(AddonThread);
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

            npcNameFinder = new NpcNameFinder(logger, WowScreen, npcNameFinderAutoResetEvent);
            npcNameTargeting = new NpcNameTargeting(logger, cts, npcNameFinder, WowProcessInput);
            WowScreen.AddDrawAction(npcNameFinder.ShowNames);
            WowScreen.AddDrawAction(npcNameTargeting.ShowClickPositions);

            screenshotThread = new Thread(ScreenshotThread);
            screenshotThread.Start();

            if (pather is RemotePathingAPI)
            {
                remotePathing = new(RemotePathingThread);
                remotePathing.Start();
            }
        }

        public void AddonThread()
        {
            while (!cts.IsCancellationRequested)
            {
                AddonReader.Update();
                cts.Token.WaitHandle.WaitOne(1);
            }
            logger.LogInformation("Addon thread stoppped!");
        }

        public void ScreenshotThread()
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
        }

        public bool IsBotActive => actionThread != null && actionThread.Active;

        public void ToggleBotStatus()
        {
            if (actionThread == null)
                return;

            if (!actionThread.Active)
            {
                if (ClassConfig?.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    GrindSession.StartBotSession();
                }

                actionThread.Active = true;
                botThread = new Thread(BotThread);
                botThread.Start();
            }
            else
            {
                actionThread.Active = false;
                if (ClassConfig?.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    GrindSession.StopBotSession("stopped", false);
                }

                WowScreen.Enabled = false;

                AddonReader.SoftReset();
                ConfigurableInput?.Reset();
            }

            StatusChanged?.Invoke();
        }

        public void BotThread()
        {
            if (actionThread != null)
            {
                actionThread.ResumeIfNeeded();

                while (!cts.IsCancellationRequested && actionThread.Active)
                {
                    actionThread.GoapPerformGoal();
                    cts.Token.WaitHandle.WaitOne(1);
                }
            }

            if (configurableInput != null)
                new StopMoving(configurableInput, AddonReader.PlayerReader).Stop();

            logger.LogInformation("Bot thread stopped!");
        }

        public bool InitialiseFromFile(string classFile, string? pathFile)
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

            stopwatch.Stop();
            logger.LogInformation($"[{nameof(BotController)}] Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

            return true;
        }

        private void Initialize(ClassConfiguration config)
        {
            AddonReader.SoftReset();

            ConfigurableInput = new ConfigurableInput(logger, wowProcess, config);

            ActionBarPopulator = new ActionBarPopulator(logger, config, AddonReader, ExecGameCommand);

            IBlacklist blacklist = config.Mode != Mode.Grind ? new NoBlacklist() : new Blacklist(logger, AddonReader, config.NPCMaxLevels_Above, config.NPCMaxLevels_Below, config.CheckTargetGivesExp, config.Blacklist);

            var actionFactory = new GoalFactory(logger, AddonReader, ConfigurableInput, DataConfig, npcNameFinder, npcNameTargeting, pather, ExecGameCommand);

            var goapAgentState = new GoapAgentState();
            var (routeInfo, availableActions) = actionFactory.CreateGoals(config, blacklist, goapAgentState, cts, wait);

            this.GoapAgent?.Dispose();
            this.GoapAgent = new GoapAgent(logger, WowScreen, goapAgentState, ConfigurableInput, AddonReader, availableActions, blacklist);

            RouteInfo = routeInfo;
            this.actionThread = new GoalThread(logger, GoapAgent, AddonReader, RouteInfo);

            // hookup events between actions
            availableActions.ToList().ForEach(a =>
            {
                a.ActionEvent += this.actionThread.OnActionEvent;
                a.ActionEvent += GoapAgent.OnActionEvent;

                // tell other action about my actions
                availableActions.ToList().ForEach(b =>
                {
                    if (b != a) { a.ActionEvent += b.OnActionEvent; }
                });
            });
        }

        private ClassConfiguration ReadClassConfiguration(string classFilename, string? pathFilename)
        {
            if(!classFilename.ToLower().Contains(AddonReader.PlayerReader.Class.ToString().ToLower()))
            {
                throw new Exception($"[{nameof(BotController)}] Not allowed to load other class profile!");
            }

            var classFilePath = Path.Join(DataConfig.Class, classFilename);
            if (File.Exists(classFilePath))
            {
                ClassConfiguration classConfig = JsonConvert.DeserializeObject<ClassConfiguration>(File.ReadAllText(classFilePath));
                var requirementFactory = new RequirementFactory(logger, AddonReader, npcNameFinder, classConfig.ImmunityBlacklist);
                classConfig.Initialise(DataConfig, AddonReader, requirementFactory, logger, pathFilename);

                logger.LogInformation($"[{nameof(BotController)}] Profile Loaded `{classFilename}` with `{classConfig.PathFilename}`.");

                return classConfig;
            }

            throw new ArgumentOutOfRangeException($"Class config file not found {classFilename}");
        }

        public void Dispose()
        {
            cts.Cancel();

            if (GrindSession is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // cleanup eventlisteners between actions
            GoapAgent?.AvailableGoals.ToList().ForEach(a =>
            {
                if (this.actionThread != null)
                {
                    a.ActionEvent -= this.actionThread.OnActionEvent;
                }

                a.ActionEvent -= GoapAgent.OnActionEvent;

                // tell other action about my actions
                GoapAgent?.AvailableGoals.ToList().ForEach(b =>
                {
                    if (b != a) { a.ActionEvent -= b.OnActionEvent; }
                });
            });
            GoapAgent?.Dispose();

            npcNameFinderAutoResetEvent.Dispose();
            globalTimeAutoResetEvent.Dispose();
            WowScreen.Dispose();
            addonDataProvider?.Dispose();
            AddonReader.Dispose();
        }

        public void StopBot()
        {
            if (actionThread != null)
            {
                actionThread.Active = false;
                StatusChanged?.Invoke();
            }
        }

        public void MinimapNodeFound()
        {
            GoapAgent?.NodeFound();
        }

        public void Shutdown()
        {
            cts.Cancel();
        }

        public void LoadClassProfile(string classFilename)
        {
            StopBot();
            if (InitialiseFromFile(classFilename, SelectedPathFilename))
            {
                SelectedClassFilename = classFilename;
            }

            ProfileLoaded?.Invoke();
        }

        public List<string> ClassFileList()
        {
            var root = DataConfig.Class;
            var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
                .Select(path => path.Replace(root, string.Empty)).ToList();

            files.Sort(new NaturalStringComparer());
            files.Insert(0, "Press Init State first!");
            return files;
        }

        public List<string> PathFileList()
        {
            var root = DataConfig.Path;
            var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
                .Select(path => path.Replace(root, string.Empty)).ToList();

            files.Sort(new NaturalStringComparer());
            files.Insert(0, "Use Class Profile Default");
            return files;
        }

        public void LoadPathProfile(string pathFilename)
        {
            StopBot();
            if (InitialiseFromFile(SelectedClassFilename, pathFilename))
            {
                SelectedPathFilename = pathFilename;
            }

            ProfileLoaded?.Invoke();
        }

        public void OverrideClassConfig(ClassConfiguration classConfiguration)
        {
            this.ClassConfig = classConfiguration;
            Initialize(this.ClassConfig);
        }
    }
}