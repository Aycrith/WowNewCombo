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
using Game;
using WinAPI;
using SharedLib.NpcFinder;
using Cyotek.Collections.Generic;
using PPather.Data;
using Core.Environment;

namespace Core
{
    public sealed class BotController : IBotController, IDisposable
    {
        private readonly WowProcess wowProcess;
        private readonly WowProcessInput wowProcessInput;
        private readonly ILogger logger;
        private readonly IPPather pather;
        private readonly MinimapNodeFinder minimapNodeFinder;
        private readonly Wait wait;
        private readonly ExecGameCommand execGameCommand;
        private readonly DataConfig dataConfig;

        private readonly CancellationTokenSource cts;
        private readonly AutoResetEvent npcNameFinderEvent;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;

        private readonly Thread addonThread;

        private readonly Thread screenshotThread;
        private const int screenshotTickMs = 200;

        private readonly Thread? remotePathing;
        private const int remotePathingTickMs = 500;

        private readonly Thread? frontendThread;
        private const int frontendTickMs = 250;

        public bool IsBotActive => GoapAgent != null && GoapAgent.Active;
        public AddonReader AddonReader { get; }
        public WowScreen WowScreen { get; }
        public IGrindSessionDAO GrindSessionDAO { get; }

        public string SelectedClassFilename { get; private set; } = string.Empty;
        public string? SelectedPathFilename { get; private set; }
        public ClassConfiguration? ClassConfig { get; private set; }
        public GoapAgent? GoapAgent { get; private set; }
        public RouteInfo? RouteInfo { get; private set; }

        public event Action? ProfileLoaded;
        public event Action? StatusChanged;

        public double AvgScreenLatency
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < ScreenLatencys.Size; i++)
                {
                    sum += ScreenLatencys.PeekAt(i);
                }
                return sum /= ScreenLatencys.Size;
            }
        }
        private readonly CircularBuffer<double> ScreenLatencys;

        public double AvgNPCLatency
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < NPCLatencys.Size; i++)
                {
                    sum += NPCLatencys.PeekAt(i);
                }
                return sum /= NPCLatencys.Size;
            }
        }
        private readonly CircularBuffer<double> NPCLatencys;

        public BotController(ILogger logger, IEnvironment env, CancellationTokenSource cts, IPPather pather, IGrindSessionDAO grindSessionDAO, DataConfig dataConfig, WowProcess wowProcess, WowScreen wowScreen, WowProcessInput wowProcessInput, ExecGameCommand execGameCommand, Wait wait, IAddonReader addonReader, MinimapNodeFinder minimapNodeFinder)
        {
            this.logger = logger;
            this.pather = pather;
            this.dataConfig = dataConfig;
            GrindSessionDAO = grindSessionDAO;
            this.wowProcess = wowProcess;
            this.WowScreen = wowScreen;
            this.wowProcessInput = wowProcessInput;
            this.execGameCommand = execGameCommand;
            this.AddonReader = (addonReader as AddonReader)!;
            this.wait = wait;
            this.minimapNodeFinder = minimapNodeFinder;

            this.cts = cts;
            npcNameFinderEvent = new(false);

            ScreenLatencys = new(8);
            NPCLatencys = new(8);

            addonThread = new(AddonThread);
            addonThread.Start();

            if (env is BlazorFrontend)
            {
                frontendThread = new(FrontendThread);
                frontendThread.Start();
            }

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

            logger.LogDebug($"Woohoo, I have read the player class. You are a {AddonReader.PlayerReader.Race.ToStringF()} {AddonReader.PlayerReader.Class.ToStringF()}.");

            npcNameFinder = new(logger, WowScreen, npcNameFinderEvent);
            npcNameTargeting = new(logger, cts, WowScreen, npcNameFinder, wowProcessInput);
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
            }
            logger.LogWarning("Addon thread stoppped!");
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

                if (ClassConfig?.Mode == Mode.AttendedGather)
                {
                    stopWatch.Restart();
                    minimapNodeFinder.TryFind();
                    ScreenLatencys.Put(stopWatch.ElapsedMilliseconds);
                }

                cts.Token.WaitHandle.WaitOne(WowScreen.Enabled ||
                    ClassConfig?.Mode == Mode.AttendedGather ? screenshotTickMs : 4);
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Screenshot thread stopped!");
        }

        private void RemotePathingThread()
        {
            while (!cts.IsCancellationRequested)
            {
                _ = pather.DrawSphere(
                    new SphereArgs("Player",
                    AddonReader.PlayerReader.PlayerLocation,
                    AddonReader.PlayerReader.Bits.PlayerInCombat() ? 1 : AddonReader.PlayerReader.Bits.HasTarget() ? 6 : 2,
                    AddonReader.UIMapId.Value
                ));

                cts.Token.WaitHandle.WaitOne(remotePathingTickMs);
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("RemotePathing thread stopped!");
        }

        private void FrontendThread()
        {
            while (!cts.IsCancellationRequested)
            {
                AddonReader.UpdateUI();
                cts.Token.WaitHandle.WaitOne(frontendTickMs);
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Frontend thread stopped!");
        }

        public void ToggleBotStatus()
        {
            if (GoapAgent == null)
                return;

            GoapAgent.Active = !GoapAgent.Active;

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
            AddonReader.SessionReset();

            ConfigurableInput configInput = new(wowProcessInput, config);
            GoapAgentState goapAgentState = new();
            (RouteInfo routeInfo, IEnumerable<GoapGoal> availableActions) =
                GoalFactory.CreateGoals(logger, AddonReader, configInput, dataConfig, npcNameFinder, npcNameTargeting, pather, execGameCommand, config, goapAgentState, cts, wait);

            RouteInfo = routeInfo;

            GoapAgent?.Dispose();
            GoapAgent = new(logger, config, GrindSessionDAO, WowScreen, goapAgentState, AddonReader, availableActions, routeInfo, configInput);
        }

        private ClassConfiguration ReadClassConfiguration(string classFile, string? pathFile)
        {
            string filePath = Path.Join(dataConfig.Class, classFile);

            ClassConfiguration classConfig = JsonConvert.DeserializeObject<ClassConfiguration>(File.ReadAllText(filePath));
            RequirementFactory requirementFactory = new(logger, AddonReader, npcNameFinder, classConfig.ImmunityBlacklist);
            classConfig.Initialise(dataConfig, AddonReader, requirementFactory, logger, pathFile);

            logger.LogInformation($"[{nameof(BotController)}] Profile Loaded `{classFile}` with `{classConfig.PathFilename}`.");

            return classConfig;
        }

        public void Dispose()
        {
            cts.Cancel();
            ClassConfig?.Dispose();
            GoapAgent?.Dispose();

            npcNameFinderEvent.Dispose();
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
            var root = Path.Join(dataConfig.Class, Path.DirectorySeparatorChar.ToString());
            var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
                .Select(path => path.Replace(root, string.Empty))
                .OrderBy(x => x, new NaturalStringComparer())
                .ToList();

            files.Insert(0, "Press Init State first!");
            return files;
        }

        public IEnumerable<string> PathFiles()
        {
            var root = Path.Join(dataConfig.Path, Path.DirectorySeparatorChar.ToString());
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