using SharedLib;
using Core.Goals;
using Core.GOAP;
using Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLib.NpcFinder;
using System;
using static System.IO.Path;
using static System.IO.File;
using System.Numerics;
using System.Threading;
using static Newtonsoft.Json.JsonConvert;

namespace Core
{
    public static class GoalFactory
    {
        public static IServiceScope CreateGoals(ILogger logger, AddonReader addonReader,
            ConfigurableInput input, DataConfig dataConfig, NpcNameFinder npcNameFinder,
            NpcNameTargeting npcNameTargeting, IPPather pather, ExecGameCommand execGameCommand,
            ClassConfiguration classConfig, GoapAgentState goapAgentState, CancellationTokenSource cts, Wait wait)
        {
            ServiceCollection services = new();

            services.AddSingleton<ILogger>(logger);
            services.AddSingleton<AddonReader>(addonReader);
            services.AddSingleton<PlayerReader>(addonReader.PlayerReader);
            services.AddSingleton<ConfigurableInput>(input);
            services.AddSingleton<WowProcessInput>(input.Proc);
            services.AddSingleton<NpcNameFinder>(npcNameFinder);
            services.AddSingleton<NpcNameTargeting>(npcNameTargeting);
            services.AddSingleton<IPPather>(pather);
            services.AddSingleton<ExecGameCommand>(execGameCommand);
            services.AddSingleton<WorldMapAreaDB>(addonReader.WorldMapAreaDb);
            services.AddSingleton<ClassConfiguration>(classConfig);
            services.AddSingleton<GoapAgentState>(goapAgentState);
            services.AddSingleton<CancellationTokenSource>(cts);
            services.AddSingleton<Wait>(wait);

            // TODO: Should be scoped as it comes from ClassConfig
            // 432 issue
            Vector3[] mapRoute = GetPath(classConfig, dataConfig);
            services.AddSingleton<Vector3[]>(mapRoute);

            if (classConfig.Mode != Mode.Grind)
            {
                services.AddScoped<IBlacklist, NoBlacklist>();
            }
            else
            {
                services.AddScoped<MouseOverBlacklist, MouseOverBlacklist>();
                services.AddScoped<IBlacklist, TargetBlacklist>();
                services.AddScoped<GoapGoal, BlacklistTargetGoal>();
            }

            // Goals components
            services.AddScoped<PlayerDirection>();
            services.AddScoped<StopMoving>();
            services.AddScoped<ReactCastError>();
            services.AddScoped<CastingHandlerInterruptWatchdog>();
            services.AddScoped<CastingHandler>();
            services.AddScoped<StuckDetector>();
            services.AddScoped<CombatUtil>();
            services.AddScoped<MountHandler>();
            services.AddScoped<TargetFinder>();

            // each GoapGoal gets an individual instance
            services.AddTransient<Navigation>();

            if (classConfig.Mode == Mode.CorpseRun)
            {
                services.AddScoped<GoapGoal, WalkToCorpseGoal>();
            }
            else if (classConfig.Mode == Mode.AttendedGather)
            {
                services.AddScoped<GoapGoal, WalkToCorpseGoal>();
                services.AddScoped<GoapGoal, CombatGoal>();
                services.AddScoped<GoapGoal, ApproachTargetGoal>();
                services.AddScoped<GoapGoal, WaitForGatheringGoal>();
                services.AddScoped<GoapGoal, FollowRouteGoal>();

                ResolveLootAndSkin(services, classConfig);

                ResolvePetClass(services, addonReader.PlayerReader.Class);

                if (classConfig.Parallel.Sequence.Length > 0)
                {
                    services.AddScoped<GoapGoal, ParallelGoal>();
                }

                ResolveAdhocGoals(services, classConfig);
                ResolveAdhocNPCGoal(services, classConfig, dataConfig);
                ResolveWaitGoal(services, classConfig);
            }
            else if (classConfig.Mode == Mode.AssistFocus)
            {
                services.AddScoped<GoapGoal, PullTargetGoal>();
                services.AddScoped<GoapGoal, ApproachTargetGoal>();
                services.AddScoped<GoapGoal, CombatGoal>();

                ResolveLootAndSkin(services, classConfig);

                services.AddScoped<GoapGoal, TargetFocusTargetGoal>();
                services.AddScoped<GoapGoal, FollowFocusGoal>();

                if (classConfig.Parallel.Sequence.Length > 0)
                {
                    services.AddScoped<GoapGoal, ParallelGoal>();
                }

                ResolveAdhocGoals(services, classConfig);
            }
            else if (classConfig.Mode is Mode.Grind or Mode.AttendedGrind)
            {
                if (classConfig.Mode == Mode.AttendedGrind)
                {
                    services.AddScoped<GoapGoal, WaitGoal>();
                }
                else
                {
                    services.AddScoped<GoapGoal, FollowRouteGoal>();
                }

                services.AddScoped<GoapGoal, WalkToCorpseGoal>();
                services.AddScoped<GoapGoal, PullTargetGoal>();
                services.AddScoped<GoapGoal, ApproachTargetGoal>();
                services.AddScoped<GoapGoal, CombatGoal>();

                if (classConfig.WrongZone.ZoneId > 0)
                {
                    services.AddScoped<GoapGoal, WrongZoneGoal>();
                }

                ResolveLootAndSkin(services, classConfig);

                ResolvePetClass(services, addonReader.PlayerReader.Class);

                if (classConfig.Parallel.Sequence.Length > 0)
                {
                    services.AddScoped<GoapGoal, ParallelGoal>();
                }

                ResolveAdhocGoals(services, classConfig);
                ResolveAdhocNPCGoal(services, classConfig, dataConfig);
                ResolveWaitGoal(services, classConfig);
            }

            ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            IServiceScope scope = provider.CreateScope();
            return scope;
        }

        private static void ResolveLootAndSkin(ServiceCollection services, ClassConfiguration classConfig)
        {
            services.AddScoped<GoapGoal, ConsumeCorpseGoal>();
            services.AddScoped<GoapGoal, CorpseConsumedGoal>();

            if (classConfig.Loot)
            {
                services.AddScoped<GoapGoal, LootGoal>();

                if (classConfig.GatherCorpse)
                {
                    services.AddScoped<GoapGoal, SkinningGoal>();
                }
            }
        }

        private static void ResolveAdhocGoals(ServiceCollection services, ClassConfiguration classConfig)
        {
            for (int i = 0; i < classConfig.Adhoc.Sequence.Length; i++)
            {
                KeyAction keyAction = classConfig.Adhoc.Sequence[i];
                services.AddScoped<GoapGoal, AdhocGoal>(x => new(keyAction,
                    x.GetRequiredService<ILogger>(),
                    x.GetRequiredService<ConfigurableInput>(), x.GetRequiredService<Wait>(),
                    x.GetRequiredService<AddonReader>(), x.GetRequiredService<StopMoving>(),
                    x.GetRequiredService<CastingHandler>(), x.GetRequiredService<MountHandler>()));
            }
        }

        private static void ResolveAdhocNPCGoal(ServiceCollection services, ClassConfiguration classConfig, DataConfig dataConfig)
        {
            for (int i = 0; i < classConfig.NPC.Sequence.Length; i++)
            {
                KeyAction keyAction = classConfig.NPC.Sequence[i];
                keyAction.Path = GetPath(keyAction, dataConfig);

                services.AddScoped<GoapGoal, AdhocNPCGoal>(x => new(keyAction,
                    x.GetRequiredService<ILogger>(), x.GetRequiredService<ConfigurableInput>(),
                    x.GetRequiredService<Wait>(), x.GetRequiredService<AddonReader>(),
                    x.GetRequiredService<Navigation>(), x.GetRequiredService<StopMoving>(),
                    x.GetRequiredService<NpcNameTargeting>(), x.GetRequiredService<ClassConfiguration>(),
                    x.GetRequiredService<MountHandler>(), x.GetRequiredService<ExecGameCommand>(),
                    x.GetRequiredService<CancellationTokenSource>()));
            }
        }

        private static void ResolveWaitGoal(ServiceCollection services, ClassConfiguration classConfig)
        {
            for (int i = 0; i < classConfig.Wait.Sequence.Length; i++)
            {
                KeyAction keyAction = classConfig.Wait.Sequence[i];

                services.AddScoped<GoapGoal, ConditionalWaitGoal>(x => new(keyAction,
                    x.GetRequiredService<ILogger>(), x.GetRequiredService<Wait>()));
            }
        }

        private static void ResolvePetClass(ServiceCollection services, UnitClass @class)
        {
            if (@class is
                UnitClass.Hunter or
                UnitClass.Warlock or
                UnitClass.Mage or
                UnitClass.DeathKnight)
            {
                services.AddScoped<GoapGoal, TargetPetTargetGoal>();
            }
        }


        private static string RelativeFilePath(DataConfig dataConfig, string path)
        {
            return !path.Contains(dataConfig.Path) ? Join(dataConfig.Path, path) : path;
        }

        private static Vector3[] GetPath(ClassConfiguration classConfig, DataConfig dataConfig)
        {
            classConfig.PathFilename = RelativeFilePath(dataConfig, classConfig.PathFilename);

            Vector3[] rawPath = DeserializeObject<Vector3[]>(ReadAllText(classConfig.PathFilename))!;
            if (!classConfig.PathReduceSteps)
                return rawPath;

            int step = 2;
            int reducedLength = rawPath.Length % step == 0 ?
                rawPath.Length / step :
                (rawPath.Length / step) + 1;

            Vector3[] path = new Vector3[reducedLength];
            for (int i = 0; i < path.Length; i++)
            {
                path[i] = rawPath[i * step];
            }
            return path;
        }

        public static Vector3[] GetPath(KeyAction keyAction, DataConfig dataConfig)
        {
            if (string.IsNullOrEmpty(keyAction.PathFilename))
                return Array.Empty<Vector3>();

            return DeserializeObject<Vector3[]>(ReadAllText(RelativeFilePath(dataConfig, keyAction.PathFilename)))!;
        }
    }
}