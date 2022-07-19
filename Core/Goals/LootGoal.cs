using Core.Database;
using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using WowheadDB;
using System.Collections.Generic;
using System.Numerics;
using SharedLib.Extensions;
using System;

namespace Core.Goals
{
    public class LootGoal : GoapGoal, IGoapEventListener
    {
        public override float Cost => 4.4f;

        private const bool debug = true;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly AreaDB areaDb;
        private readonly StopMoving stopMoving;
        private readonly BagReader bagReader;
        private readonly ClassConfiguration classConfig;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly CombatUtil combatUtil;
        private readonly PlayerDirection playerDirection;

        private readonly List<Vector3> corpseLocations = new();

        private int lastLoot;
        private bool gatherCorpse;

        public LootGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, ClassConfiguration classConfig, NpcNameTargeting npcNameTargeting, CombatUtil combatUtil, PlayerDirection playerDirection)
            : base(nameof(LootGoal))
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = addonReader.PlayerReader;
            this.areaDb = addonReader.AreaDb;
            this.stopMoving = stopMoving;
            this.bagReader = addonReader.BagReader;

            this.classConfig = classConfig;
            this.npcNameTargeting = npcNameTargeting;
            this.combatUtil = combatUtil;
            this.playerDirection = playerDirection;

            if (classConfig.Mode == Mode.AssistFocus)
            {
                AddPrecondition(GoapKey.incombat, false);
            }
            else
            {
                AddPrecondition(GoapKey.dangercombat, false);
            }

            AddPrecondition(GoapKey.shouldloot, true);

            AddEffect(GoapKey.shouldloot, false);
        }

        public override void OnEnter()
        {
            if (bagReader.BagsFull)
            {
                LogWarning("Inventory is full!");
                SendGoapEvent(new GoapStateEvent(GoapKey.shouldloot, false));
            }

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
                Vector3 location = playerReader.PlayerLocation;
                Vector3 closestCorpse = GetClosestCorpse();
                float heading = DirectionCalculator.CalculateHeading(location, closestCorpse);
                playerDirection.SetDirection(heading, closestCorpse);
                logger.LogInformation("Look at possible closest corpse and try again");

                wait.Fixed(playerReader.NetworkLatency.Value / 2);

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
                if (playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead())
                {
                    CheckForGather();

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

            (bool lootTimeOut, double elapsedMs) = wait.Until(1000, LootChanged);
            Log($"Loot {(!lootTimeOut ? "Successfull" : "Failed")} after {elapsedMs}ms");
            wait.Fixed(playerReader.NetworkLatency.Value);

            SendGoapEvent(new GoapStateEvent(GoapKey.shouldloot, false));

            AddEffect(GoapKey.shouldgather, !lootTimeOut && gatherCorpse);
            SendGoapEvent(new GoapStateEvent(GoapKey.shouldgather, !lootTimeOut && gatherCorpse));

            if (playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead())
            {
                input.ClearTarget();
                wait.Update();
            }
        }

        public override void OnExit()
        {
            if (!classConfig.GatherCorpse)
            {
                npcNameTargeting.ChangeNpcType(NpcNames.None);
            }
        }

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is CorpseEvent corpseEvent)
            {
                corpseLocations.Add(corpseEvent.Location);
            }
        }

        private bool FoundByCursor()
        {
            wait.Fixed(playerReader.NetworkLatency.Value / 2);
            npcNameTargeting.WaitForUpdate();
            if (!npcNameTargeting.FindBy(CursorType.Loot))
            {
                return false;
            }

            Log("Found corpse - clicked");
            (bool searchTimeOut, double elapsedMs) = wait.Until(200, playerReader.Bits.HasTarget);
            Log($"Found target ? {!searchTimeOut} after {elapsedMs}ms");

            CheckForGather();

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

            int index = -1;
            float minDistance = float.MaxValue;
            Vector3 playerLoc = playerReader.PlayerLocation;
            for (int i = 0; i < corpseLocations.Count; i++)
            {
                float d = playerLoc.DistanceXYTo(corpseLocations[i]);
                if (d < minDistance)
                {
                    minDistance = d;
                    index = i;
                }
            }

            return corpseLocations[index];
        }

        private void CheckForGather()
        {
            if (!classConfig.GatherCorpse ||
                areaDb.CurrentArea == null)
                return;

            gatherCorpse = false;
            int targetId = playerReader.TargetId;
            Area area = areaDb.CurrentArea;

            if ((classConfig.Skin && Array.BinarySearch(area.skinnable, targetId) >= 0) ||
               (classConfig.Herb && Array.BinarySearch(area.gatherable, targetId) >= 0) ||
               (classConfig.Mine && Array.BinarySearch(area.minable, targetId) >= 0) ||
               (classConfig.Salvage && Array.BinarySearch(area.salvegable, targetId) >= 0))
            {
                gatherCorpse = true;
            }

            Log($"Should gather {targetId} ? {gatherCorpse}");
        }

        private bool LootChanged()
        {
            return lastLoot != playerReader.LastLootTime;
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