using Core.Goals;
using Core.GOAP;
using Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PPather.Data;
using SharedLib.NpcFinder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using static Newtonsoft.Json.JsonConvert;

namespace Core
{
    public class GoalFactory
    {
        public static (RouteInfo, IEnumerable<GoapGoal>) CreateGoals(ILogger logger, AddonReader addonReader,
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

            Vector3[] route = GetPath(classConfig, dataConfig);
            services.AddSingleton<Vector3[]>(route);

            services.AddSingleton<ClassConfiguration>(classConfig);
            services.AddSingleton<GoapAgentState>(goapAgentState);
            services.AddSingleton<CancellationTokenSource>(cts);
            services.AddSingleton<Wait>(wait);

            if (classConfig.Mode != Mode.Grind)
            {
                services.AddSingleton<IBlacklist, NoBlacklist>();
            }
            else
            {
                services.AddSingleton<IBlacklist, Blacklist>();
                services.AddSingleton<GoapGoal, BlacklistTargetGoal>();
            }

            // Goals components
            services.AddSingleton<PlayerDirection>();
            services.AddSingleton<StopMoving>();
            services.AddSingleton<ReactCastError>();
            services.AddSingleton<CastingHandler>();
            services.AddSingleton<StuckDetector>();
            services.AddSingleton<CombatUtil>();
            services.AddSingleton<MountHandler>();
            services.AddSingleton<TargetFinder>();

            // each GoapGoal gets an individual instance
            services.AddTransient<Navigation>();

            if (classConfig.Mode == Mode.CorpseRun)
            {
                services.AddSingleton<GoapGoal, WalkToCorpseGoal>();
            }
            else if (classConfig.Mode == Mode.AttendedGather)
            {
                services.AddSingleton<GoapGoal, WalkToCorpseGoal>();
                services.AddSingleton<GoapGoal, CombatGoal>();
                services.AddSingleton<GoapGoal, ApproachTargetGoal>();
                services.AddSingleton<GoapGoal, WaitForGatheringGoal>();
                services.AddSingleton<GoapGoal, FollowRouteGoal>();

                ResolveLootAndSkin(services, classConfig);

                ResolvePetClass(services, addonReader.PlayerReader.Class);

                if (classConfig.Parallel.Sequence.Length > 0)
                {
                    services.AddSingleton<GoapGoal, ParallelGoal>();
                }

                ResolveAdhocGoals(services, classConfig);
                ResolveAdhocNPCGoal(services, classConfig, dataConfig);
            }
            else if (classConfig.Mode == Mode.AssistFocus)
            {
                services.AddSingleton<GoapGoal, PullTargetGoal>();
                services.AddSingleton<GoapGoal, ApproachTargetGoal>();
                services.AddSingleton<GoapGoal, CombatGoal>();

                ResolveLootAndSkin(services, classConfig);

                services.AddSingleton<GoapGoal, TargetFocusTargetGoal>();
                services.AddSingleton<GoapGoal, FollowFocusGoal>();

                if (classConfig.Parallel.Sequence.Length > 0)
                {
                    services.AddSingleton<GoapGoal, ParallelGoal>();
                }

                ResolveAdhocGoals(services, classConfig);
            }
            else if (classConfig.Mode is Mode.Grind or Mode.AttendedGrind)
            {
                if (classConfig.Mode == Mode.AttendedGrind)
                {
                    services.AddSingleton<GoapGoal, WaitGoal>();
                }
                else
                {
                    services.AddSingleton<GoapGoal, FollowRouteGoal>();
                }

                services.AddSingleton<GoapGoal, WalkToCorpseGoal>();
                services.AddSingleton<GoapGoal, PullTargetGoal>();
                services.AddSingleton<GoapGoal, ApproachTargetGoal>();
                services.AddSingleton<GoapGoal, CombatGoal>();

                if (classConfig.WrongZone.ZoneId > 0)
                {
                    services.AddSingleton<GoapGoal, WrongZoneGoal>();
                }

                ResolveLootAndSkin(services, classConfig);

                ResolvePetClass(services, addonReader.PlayerReader.Class);

                if (classConfig.Parallel.Sequence.Length > 0)
                {
                    services.AddSingleton<GoapGoal, ParallelGoal>();
                }

                ResolveAdhocGoals(services, classConfig);
                ResolveAdhocNPCGoal(services, classConfig, dataConfig);
            }

            ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            IEnumerable<GoapGoal> goals = provider.GetServices<GoapGoal>();
            IEnumerable<IRouteProvider> pathProviders = goals.OfType<IRouteProvider>();

            RouteInfo routeInfo = new(route, pathProviders, addonReader);

            pather.DrawLines(new()
            {
                new LineArgs("grindpath", route, 2, addonReader.UIMapId.Value)
            });

            return (routeInfo, goals);
        }

        private static void ResolveLootAndSkin(ServiceCollection services, ClassConfiguration classConfig)
        {
            if (classConfig.Loot)
            {
                services.AddSingleton<GoapGoal, ConsumeCorpseGoal>();
                services.AddSingleton<GoapGoal, CorpseConsumedGoal>();

                services.AddSingleton<GoapGoal, LootGoal>();

                if (classConfig.GatherCorpse)
                {
                    services.AddSingleton<GoapGoal, SkinningGoal>();
                }
            }
        }

        private static void ResolveAdhocGoals(ServiceCollection services, ClassConfiguration classConfig)
        {
            for (int i = 0; i < classConfig.Adhoc.Sequence.Length; i++)
            {
                KeyAction keyAction = classConfig.Adhoc.Sequence[i];
                services.AddSingleton<GoapGoal, AdhocGoal>(x => new(keyAction,
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

                services.AddSingleton<GoapGoal, AdhocNPCGoal>(x => new(keyAction,
                    x.GetRequiredService<ILogger>(), x.GetRequiredService<ConfigurableInput>(),
                    x.GetRequiredService<Wait>(), x.GetRequiredService<AddonReader>(),
                    x.GetRequiredService<Navigation>(), x.GetRequiredService<StopMoving>(),
                    x.GetRequiredService<NpcNameTargeting>(), x.GetRequiredService<ClassConfiguration>(),
                    x.GetRequiredService<MountHandler>(), x.GetRequiredService<ExecGameCommand>()));
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
                services.AddSingleton<GoapGoal, TargetPetTargetGoal>();
            }
        }


        private static string RelativeFilePath(DataConfig dataConfig, string path)
        {
            return !path.Contains(dataConfig.Path) ? Path.Join(dataConfig.Path, path) : path;
        }

        private static Vector3[] GetPath(ClassConfiguration classConfig, DataConfig dataConfig)
        {
            classConfig.PathFilename = RelativeFilePath(dataConfig, classConfig.PathFilename);

            Vector3[] rawPath = DeserializeObject<Vector3[]>(File.ReadAllText(classConfig.PathFilename));
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

            return DeserializeObject<Vector3[]>(File.ReadAllText(RelativeFilePath(dataConfig, keyAction.PathFilename)));
        }
    }
}