using Core.Goals;
using SharedLib.NpcFinder;
using PPather.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.GOAP;
using System.Numerics;

namespace Core
{
    public class GoalFactory
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly DataConfig dataConfig;

        private readonly AddonReader addonReader;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly IPPather pather;

        private readonly ExecGameCommand exec;

        public GoalFactory(ILogger logger, AddonReader addonReader, ConfigurableInput input, DataConfig dataConfig, NpcNameFinder npcNameFinder, NpcNameTargeting npcNameTargeting, IPPather pather, ExecGameCommand execGameCommand)
        {
            this.logger = logger;
            this.addonReader = addonReader;
            this.input = input;
            this.dataConfig = dataConfig;
            this.npcNameFinder = npcNameFinder;
            this.npcNameTargeting = npcNameTargeting;
            this.pather = pather;
            this.exec = execGameCommand;
        }

        public (RouteInfo, HashSet<GoapGoal>) CreateGoals(ClassConfiguration classConfig, GoapAgentState goapAgentState, CancellationTokenSource cts, Wait wait)
        {
            HashSet<GoapGoal> availableActions = new();

            GetPath(out List<Vector3> pathPoints, classConfig);

            IBlacklist blacklist = classConfig.Mode != Mode.Grind ?
                new NoBlacklist() :
                new Blacklist(logger, addonReader, classConfig.NPCMaxLevels_Above, classConfig.NPCMaxLevels_Below, classConfig.CheckTargetGivesExp, classConfig.Blacklist);

            PlayerDirection playerDirection = new(logger, input, addonReader.PlayerReader);
            StopMoving stopMoving = new(input, addonReader.PlayerReader);

            CastingHandler castingHandler = new(logger, cts, input, wait, addonReader, playerDirection, stopMoving);

            StuckDetector stuckDetector = new(logger, input, addonReader.PlayerReader, playerDirection, stopMoving);
            CombatUtil combatUtil = new(logger, input, wait, addonReader);
            MountHandler mountHandler = new(logger, input, classConfig, wait, addonReader, castingHandler, stopMoving);

            TargetFinder targetFinder = new(input, classConfig, addonReader.PlayerReader, npcNameTargeting);

            Navigation followNav = new(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler, classConfig.Mode);
            FollowRouteGoal followRouteAction = new(logger, input, wait, addonReader, classConfig, pathPoints, followNav, mountHandler, npcNameFinder, targetFinder);

            Navigation corpseNav = new(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler, classConfig.Mode);
            WalkToCorpseGoal walkToCorpseAction = new(logger, input, wait, addonReader, corpseNav, stopMoving);

            CombatGoal genericCombat = new(logger, input, wait, addonReader, stopMoving, classConfig, castingHandler, mountHandler);
            ApproachTargetGoal approachTarget = new(logger, input, wait, addonReader.PlayerReader, stopMoving, combatUtil);

            availableActions.Clear();

            if (blacklist is Blacklist)
            {
                availableActions.Add(new BlacklistTargetGoal(addonReader.PlayerReader, input, blacklist));
            }

            if (classConfig.Mode == Mode.CorpseRun)
            {
                availableActions.Add(walkToCorpseAction);
            }
            else if (classConfig.Mode == Mode.AttendedGather)
            {
                followNav.SimplifyRouteToWaypoint = false;

                availableActions.Add(walkToCorpseAction);
                availableActions.Add(genericCombat);
                availableActions.Add(approachTarget);
                availableActions.Add(new WaitForGathering(logger, wait, addonReader.PlayerReader, stopMoving));
                availableActions.Add(followRouteAction);

                if (classConfig.Loot)
                {
                    availableActions.Add(new ConsumeCorpse(logger, classConfig));
                    availableActions.Add(new CorpseConsumed(logger, goapAgentState, wait));

                    if (classConfig.KeyboardOnly)
                    {
                        var lootAction = new LastTargetLoot(logger, input, wait, addonReader, stopMoving, combatUtil);
                        lootAction.AddPreconditions();
                        availableActions.Add(lootAction);
                    }
                    else
                    {
                        var lootAction = new LootGoal(logger, input, wait, addonReader, stopMoving, classConfig, npcNameTargeting, combatUtil, playerDirection);
                        lootAction.AddPreconditions();
                        availableActions.Add(lootAction);
                    }

                    if (classConfig.Skin)
                    {
                        availableActions.Add(new SkinningGoal(logger, input, addonReader, wait, stopMoving, npcNameTargeting, combatUtil));
                    }
                }

                if (addonReader.PlayerReader.Class is
                    PlayerClassEnum.Hunter or
                    PlayerClassEnum.Warlock or
                    PlayerClassEnum.Mage)
                {
                    availableActions.Add(new TargetPetTarget(input, addonReader.PlayerReader));
                }

                if (classConfig.Parallel.Sequence.Count > 0)
                {
                    availableActions.Add(new ParallelGoal(logger, input, wait, addonReader.PlayerReader, stopMoving, classConfig.Parallel.Sequence, castingHandler, mountHandler));
                }

                foreach (var item in classConfig.Adhoc.Sequence)
                {
                    availableActions.Add(new AdhocGoal(logger, input, wait, item, addonReader, stopMoving, castingHandler, mountHandler));
                }

                foreach (var item in classConfig.NPC.Sequence)
                {
                    var nav = new Navigation(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler, classConfig.Mode);
                    availableActions.Add(new AdhocNPCGoal(logger, input, item, wait, addonReader, nav, stopMoving, npcNameTargeting, classConfig, mountHandler, exec));
                    item.Path.Clear();
                    item.Path.AddRange(ReadPath(item.Name, item.PathFilename));
                }
            }
            else
            {
                if (classConfig.Mode == Mode.AttendedGrind)
                {
                    availableActions.Add(new WaitGoal(logger, wait));
                }
                else
                {
                    availableActions.Add(followRouteAction);
                    availableActions.Add(walkToCorpseAction);
                }

                availableActions.Add(approachTarget);

                if (classConfig.WrongZone.ZoneId > 0)
                {
                    availableActions.Add(new WrongZoneGoal(addonReader, input, playerDirection, logger, stuckDetector, classConfig));
                }

                if (classConfig.Loot)
                {
                    availableActions.Add(new ConsumeCorpse(logger, classConfig));
                    availableActions.Add(new CorpseConsumed(logger, goapAgentState, wait));

                    if (classConfig.KeyboardOnly)
                    {
                        var lootAction = new LastTargetLoot(logger, input, wait, addonReader, stopMoving, combatUtil);
                        lootAction.AddPreconditions();
                        availableActions.Add(lootAction);
                    }
                    else
                    {
                        var lootAction = new LootGoal(logger, input, wait, addonReader, stopMoving, classConfig, npcNameTargeting, combatUtil, playerDirection);
                        lootAction.AddPreconditions();
                        availableActions.Add(lootAction);
                    }

                    if (classConfig.Skin)
                    {
                        availableActions.Add(new SkinningGoal(logger, input, addonReader, wait, stopMoving, npcNameTargeting, combatUtil));
                    }
                }

                availableActions.Add(genericCombat);

                if (addonReader.PlayerReader.Class is
                    PlayerClassEnum.Hunter or
                    PlayerClassEnum.Warlock or
                    PlayerClassEnum.Mage)
                {
                    availableActions.Add(new TargetPetTarget(input, addonReader.PlayerReader));
                }

                availableActions.Add(new PullTargetGoal(logger, input, wait, addonReader, blacklist, stopMoving, castingHandler, mountHandler, stuckDetector, combatUtil));

                if (classConfig.Parallel.Sequence.Count > 0)
                {
                    availableActions.Add(new ParallelGoal(logger, input, wait, addonReader.PlayerReader, stopMoving, classConfig.Parallel.Sequence, castingHandler, mountHandler));
                }

                foreach (var item in classConfig.Adhoc.Sequence)
                {
                    availableActions.Add(new AdhocGoal(logger, input, wait, item, addonReader, stopMoving, castingHandler, mountHandler));
                }

                foreach (var item in classConfig.NPC.Sequence)
                {
                    var nav = new Navigation(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler, classConfig.Mode);
                    availableActions.Add(new AdhocNPCGoal(logger, input, item, wait, addonReader, nav, stopMoving, npcNameTargeting, classConfig, mountHandler, exec));
                    item.Path.Clear();
                    item.Path.AddRange(ReadPath(item.Name, item.PathFilename));
                }
            }

            var pathProviders = availableActions.Where(a => a is IRouteProvider)
                .Cast<IRouteProvider>()
                .ToList();

            RouteInfo routeInfo = new(pathPoints, pathProviders, addonReader);

            pather.DrawLines(new()
            {
                new LineArgs("grindpath", pathPoints, 2, addonReader.UIMapId.Value)
            });

            return (routeInfo, availableActions);
        }

        private string FixPathFilename(string path)
        {
            return !path.Contains(dataConfig.Path) ? Path.Join(dataConfig.Path, path) : path;
        }

        private void GetPath(out List<Vector3> pathPoints, ClassConfiguration classConfig)
        {
            classConfig.PathFilename = FixPathFilename(classConfig.PathFilename);

            pathPoints = CreatePathPoints(classConfig);
        }

        private IEnumerable<Vector3> ReadPath(string name, string pathFilename)
        {
            try
            {
                if (string.IsNullOrEmpty(pathFilename))
                {
                    return new List<Vector3>();
                }
                else
                {
                    return JsonConvert.DeserializeObject<Vector3[]>(File.ReadAllText(FixPathFilename(pathFilename)));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Reading path: {name}");
                throw;
            }
        }

        private static List<Vector3> CreatePathPoints(ClassConfiguration classConfig)
        {
            List<Vector3> output = new();

            string text = File.ReadAllText(classConfig.PathFilename);
            var points = JsonConvert.DeserializeObject<Vector3[]>(text);

            int step = classConfig.PathReduceSteps ? 2 : 1;
            for (int i = 0; i < points.Length; i += step)
            {
                output.Add(points[i]);
            }

            // last point of the path is added
            if (points.Length > 0 && points.Length % step != 0)
            {
                output.Add(points[^1]);
            }

            return output;
        }
    }
}