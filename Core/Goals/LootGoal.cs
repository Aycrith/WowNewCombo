using Core.Database;
using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using SharedLib.Extensions;

namespace Core.Goals
{
    public class LootGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 4.4f;

        private const bool debug = true;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly AreaDB areaDb;
        private readonly StopMoving stopMoving;
        private readonly BagReader bagReader;
        private readonly ClassConfiguration classConfiguration;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly CombatUtil combatUtil;
        private readonly PlayerDirection playerDirection;

        private readonly List<Vector3> corpseLocations = new();

        private int lastLoot;

        public LootGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, ClassConfiguration classConfiguration, NpcNameTargeting npcNameTargeting, CombatUtil combatUtil, PlayerDirection playerDirection)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = addonReader.PlayerReader;
            this.areaDb = addonReader.AreaDb;
            this.stopMoving = stopMoving;
            this.bagReader = addonReader.BagReader;

            this.classConfiguration = classConfiguration;
            this.npcNameTargeting = npcNameTargeting;
            this.combatUtil = combatUtil;
            this.playerDirection = playerDirection;
        }

        public virtual void AddPreconditions()
        {
            AddPrecondition(GoapKey.dangercombat, false);
            AddPrecondition(GoapKey.shouldloot, true);
            AddEffect(GoapKey.shouldloot, false);
        }

        public override void OnEnter()
        {
            if (bagReader.BagsFull)
            {
                LogWarning("Inventory is full!");
                SendActionEvent(new ActionEventArgs(GoapKey.shouldloot, false));
            }

            Log($"Search for {NpcNames.Corpse}");
            npcNameTargeting.ChangeNpcType(NpcNames.Corpse);

            lastLoot = playerReader.LastLootTime;

            stopMoving.Stop();
            combatUtil.Update();

            bool foundByCursor = false;

            if (FoundByCursor())
            {
                foundByCursor = true;
                corpseLocations.Remove(GetClosestCorpse());
            }
            else if (corpseLocations.Count > 0)
            {
                npcNameTargeting.ChangeNpcType(NpcNames.None);
                var location = playerReader.PlayerLocation;
                var closestCorpse = GetClosestCorpse();
                var heading = DirectionCalculator.CalculateHeading(location, closestCorpse);
                playerDirection.SetDirection(heading, closestCorpse);
                logger.LogInformation("Look at possible corpse and try again");
                npcNameTargeting.ChangeNpcType(NpcNames.Corpse);

                if (FoundByCursor())
                {
                    foundByCursor = true;
                    corpseLocations.Remove(closestCorpse);
                }
            }

            if (!foundByCursor)
            {
                corpseLocations.Remove(GetClosestCorpse());

                Log("No corpse name found - check last dead target exists");
                input.LastTarget();
                wait.Update();
                if (playerReader.HasTarget)
                {
                    if (playerReader.Bits.TargetIsDead)
                    {
                        CheckForSkinning();

                        Log("Found last dead target");
                        input.Interact();

                        (bool foundTarget, bool moved) = combatUtil.FoundTargetWhileMoved();
                        if (foundTarget)
                        {
                            Log("Goal interrupted!");
                            return;
                        }

                        if (moved)
                        {
                            Log("Last dead target double");
                            input.Interact();
                        }

                        if (!foundTarget && !moved)
                        {
                            Log("Just for safety Interact once more.");
                            input.Interact();
                        }
                    }
                    else
                    {
                        Log("Don't attack the target!");
                        input.ClearTarget();
                    }
                }
            }

            GoalExit();
        }

        public override void OnExit()
        {
            if (!classConfiguration.Skin)
            {
                npcNameTargeting.ChangeNpcType(NpcNames.None);
            }
        }

        public override void PerformAction()
        {
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.corpselocation && e.Value is CorpseLocation location)
            {
                corpseLocations.Add(location.Location);
            }
        }

        private bool FoundByCursor()
        {
            npcNameTargeting.WaitForUpdate();
            if (!npcNameTargeting.FindBy(CursorType.Loot))
            {
                return false;
            }

            Log("Found corpse - clicked");
            (bool searchTimeOut, double elapsedMs) = wait.Until(200, HasTarget);
            if (!searchTimeOut)
            {
                Log($"Found target after {elapsedMs}ms");
            }

            CheckForSkinning();

            (bool foundTarget, bool moved) = combatUtil.FoundTargetWhileMoved();
            if (foundTarget)
            {
                Log("Interrupted!");
                return false;
            }

            if (moved)
            {
                Log("Had to move so interact again");
                input.Interact();
                wait.Update();
            }

            return true;
        }

        private Vector3 GetClosestCorpse()
        {
            if (corpseLocations.Count == 0)
                return Vector3.Zero;

            var closest = corpseLocations.
                Select(loc => new { loc, d = playerReader.PlayerLocation.DistanceXYTo(loc) }).
                Aggregate((a, b) => a.d <= b.d ? a : b);

            return closest.loc;
        }

        private void CheckForSkinning()
        {
            if (!classConfiguration.Skin)
                return;

            bool targetSkinnable = false;
            if (areaDb.CurrentArea != null && areaDb.CurrentArea.skinnable != null)
            {
                targetSkinnable = areaDb.CurrentArea.skinnable.Contains(playerReader.TargetId);
            }

            Log($"Should skin {playerReader.TargetId} ? {targetSkinnable}");
            AddEffect(GoapKey.shouldskin, targetSkinnable);
            SendActionEvent(new ActionEventArgs(GoapKey.shouldskin, targetSkinnable));
        }

        private void GoalExit()
        {
            if (!wait.Till(1000, LootChanged))
            {
                Log("Loot Successfull");
            }
            else
            {
                Log("Loot Failed");

                SendActionEvent(new ActionEventArgs(GoapKey.shouldskin, false));
            }

            SendActionEvent(new ActionEventArgs(GoapKey.shouldloot, false));

            if (playerReader.HasTarget && playerReader.Bits.TargetIsDead)
            {
                input.ClearTarget();
                wait.Update();
            }
        }

        private bool LootChanged()
        {
            return lastLoot != playerReader.LastLootTime;
        }

        private bool HasTarget()
        {
            return playerReader.HasTarget;
        }

        private void Log(string text)
        {
            if (debug)
            {
                logger.LogInformation($"{nameof(LootGoal)}: {text}");
            }
        }

        private void LogWarning(string text)
        {
            logger.LogWarning($"{nameof(LootGoal)}: {text}");
        }
    }
}