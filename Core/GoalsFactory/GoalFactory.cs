using Core.Goals;
using Core.GOAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using static System.IO.Path;
using static System.IO.File;
using System.Numerics;
using System.Threading;
using static Newtonsoft.Json.JsonConvert;
using Serilog;
using Core.Session;
using SharedLib;

namespace Core;

public static class GoalFactory
{
    public static IServiceScope CreateGoals(
        IServiceProvider sp, ClassConfiguration classConfig)
    {
        ServiceCollection services = new();

        services.AddSingleton(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger>());

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        services.AddSingleton<ClassConfiguration>(classConfig);

        services.AddStartupIoC(sp);

        // session scoped services

        services.AddScoped<GoapAgentState>();

        services.AddScoped<CancellationTokenSource<GoapAgent>>();
        services.AddScoped<IGrindSessionHandler, GrindSessionHandler>();

        if (classConfig.LogBagChanges)
            services.AddScoped<IBagChangeTracker, BagChangeTracker>();
        else
            services.AddScoped<IBagChangeTracker, NoBagChangeTracker>();

        // TODO: Should be scoped as it comes from ClassConfig
        // 432 issue
        services.AddSingleton<Vector3[]>(
            GetPath(classConfig, sp.GetRequiredService<DataConfig>()));

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

        services.AddScoped<NpcNameTargeting>();

        // Goals components
        services.AddScoped<PlayerDirection>();
        services.AddScoped<StopMoving>();
        services.AddScoped<ReactCastError>();
        services.AddScoped<CastingHandlerInterruptWatchdog>();
        services.AddScoped<CastingHandler>();
        services.AddScoped<StuckDetector>();
        services.AddScoped<CombatUtil>();

        var playerReader = sp.GetRequiredService<PlayerReader>();

        if (playerReader.Class is UnitClass.Druid)
        {
            services.AddScoped<MountHandler>();
            services.AddScoped<IMountHandler, DruidMountHandler>();
        }
        else
        {
            services.AddScoped<IMountHandler, MountHandler>();
        }

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

            ResolvePetClass(services, playerReader.Class);

            if (classConfig.Parallel.Sequence.Length > 0)
            {
                services.AddScoped<GoapGoal, ParallelGoal>();
            }

            ResolveAdhocGoals(services, classConfig);

            ResolveAdhocNPCGoal(services, classConfig,
                sp.GetRequiredService<DataConfig>());

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

            ResolvePetClass(services, playerReader.Class);

            if (classConfig.Parallel.Sequence.Length > 0)
            {
                services.AddScoped<GoapGoal, ParallelGoal>();
            }

            ResolveAdhocGoals(services, classConfig);

            ResolveAdhocNPCGoal(services, classConfig,
                sp.GetRequiredService<DataConfig>());

            ResolveWaitGoal(services, classConfig);
        }

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        IServiceScope scope = provider.CreateScope();
        return scope;
    }

    private static void ResolveLootAndSkin(ServiceCollection services,
        ClassConfiguration classConfig)
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

    private static void ResolveAdhocGoals(ServiceCollection services,
        ClassConfiguration classConfig)
    {
        for (int i = 0; i < classConfig.Adhoc.Sequence.Length; i++)
        {
            KeyAction keyAction = classConfig.Adhoc.Sequence[i];
            services.AddScoped<GoapGoal, AdhocGoal>(x => new(keyAction,
                x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<ConfigurableInput>(),
                x.GetRequiredService<Wait>(),
                x.GetRequiredService<PlayerReader>(),
                x.GetRequiredService<StopMoving>(),
                x.GetRequiredService<CastingHandler>(),
                x.GetRequiredService<IMountHandler>(),
                x.GetRequiredService<AddonBits>(),
                x.GetRequiredService<CombatLog>()));
        }
    }

    private static void ResolveAdhocNPCGoal(ServiceCollection services,
        ClassConfiguration classConfig, DataConfig dataConfig)
    {
        for (int i = 0; i < classConfig.NPC.Sequence.Length; i++)
        {
            KeyAction keyAction = classConfig.NPC.Sequence[i];
            keyAction.Path = GetPath(keyAction, dataConfig);

            services.AddScoped<GoapGoal, AdhocNPCGoal>(x => new(keyAction,
                x.GetRequiredService<ILogger<AdhocNPCGoal>>(),
                x.GetRequiredService<ConfigurableInput>(),
                x.GetRequiredService<Wait>(),
                x.GetRequiredService<PlayerReader>(),
                x.GetRequiredService<GossipReader>(),
                x.GetRequiredService<AddonBits>(),
                x.GetRequiredService<Navigation>(),
                x.GetRequiredService<StopMoving>(),
                x.GetRequiredService<NpcNameTargeting>(),
                x.GetRequiredService<ClassConfiguration>(),
                x.GetRequiredService<IMountHandler>(),
                x.GetRequiredService<ExecGameCommand>(),
                x.GetRequiredService<CancellationTokenSource>()));
        }
    }

    private static void ResolveWaitGoal(ServiceCollection services,
        ClassConfiguration classConfig)
    {
        for (int i = 0; i < classConfig.Wait.Sequence.Length; i++)
        {
            KeyAction keyAction = classConfig.Wait.Sequence[i];

            services.AddScoped<GoapGoal, ConditionalWaitGoal>(x => new(
                keyAction,
                x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<Wait>()));
        }
    }

    private static void ResolvePetClass(ServiceCollection services,
        UnitClass @class)
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
        return !path.Contains(dataConfig.Path)
            ? Join(dataConfig.Path, path)
            : path;
    }

    private static Vector3[] GetPath(ClassConfiguration classConfig,
        DataConfig dataConfig)
    {
        classConfig.PathFilename =
            RelativeFilePath(dataConfig, classConfig.PathFilename);

        Vector3[] rawPath = DeserializeObject<Vector3[]>(
            ReadAllText(classConfig.PathFilename))!;

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

            // TODO: there could be saved user routes where
            //       the Z component not 0
            if (path[i].Z != 0)
                path[i].Z = 0;
        }
        return path;
    }

    public static Vector3[] GetPath(KeyAction keyAction, DataConfig dataConfig)
    {
        return string.IsNullOrEmpty(keyAction.PathFilename)
            ? Array.Empty<Vector3>()
            : DeserializeObject<Vector3[]>(
            ReadAllText(RelativeFilePath(dataConfig, keyAction.PathFilename)))!;
    }
}