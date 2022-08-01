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
    public class LootGoal : GoapGoal, IGoapEventListener, IDisposable
    {
        public override float Cost => 4.6f;

        private const bool debug = true;

        private const int MAX_TIME_TO_REACH_MELEE = 10000;
        private const int MAX_TIME_TO_DETECT_LOOT = 2 * CastingHandler.GCD;
        private const int MAX_TIME_TO_WAIT_NPC_NAME = 1000;

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
        private readonly GoapAgentState state;

        private readonly List<CorpseEvent> corpseLocations = new();

        private bool gatherCorpse;
        private bool listenLootWindow;
        private bool successfulInBackground;

        public LootGoal(ILogger logger, ConfigurableInput input, Wait wait,
            AddonReader addonReader, StopMoving stopMoving, ClassConfiguration classConfig,
            NpcNameTargeting npcNameTargeting, CombatUtil combatUtil, PlayerDirection playerDirection,
            GoapAgentState state)
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
            this.state = state;

            playerReader.LootEvent.Changed += ListenLootEvent;

            AddPrecondition(GoapKey.shouldloot, true);
            AddEffect(GoapKey.shouldloot, false);
        }

        public void Dispose()
        {
            playerReader.LootEvent.Changed -= ListenLootEvent;
        }

        public override void OnEnter()
        {
            combatUtil.Update();

            wait.While(LootReset);

            successfulInBackground = false;
            listenLootWindow = true;

            if (bagReader.BagsFull())
            {
                logger.LogWarning("Inventory is full");
                SendGoapEvent(new GoapStateEvent(GoapKey.shouldloot, false));
            }

            bool success = false;
            if (classConfig.KeyboardOnly)
            {
                success = LootKeyboard();
            }
            else
            {
                if (state.LastCombatKillCount == 1)
                {
                    success = LootKeyboard();
                }

                if (!success)
                {
                    success = LootMouse();
                }
            }

            if (success)
            {
                (bool lootTimeOut, double elapsedMs) = wait.Until(MAX_TIME_TO_DETECT_LOOT, LootReadyOrClosed, input.ApproachOnCooldown);
                Log($"Loot {(successfulInBackground || !lootTimeOut ? "Successful" : "Failed")} after {elapsedMs}ms");

                gatherCorpse &= successfulInBackground || !lootTimeOut;

                AddEffect(GoapKey.shouldgather, gatherCorpse);
                SendGoapEvent(new GoapStateEvent(GoapKey.shouldgather, gatherCorpse));
            }
            else
            {
                Log($"Loot Failed!");
            }

            SendGoapEvent(new GoapStateEvent(GoapKey.shouldloot, false));
            state.LootableCorpseCount = Math.Max(0, state.LootableCorpseCount - 1);

            if (!gatherCorpse && playerReader.Bits.HasTarget())
            {
                input.ClearTarget();
                wait.Update();
            }

            if (corpseLocations.Count > 0)
                corpseLocations.Remove(GetClosestCorpse()!);
        }

        public override void OnExit()
        {
            listenLootWindow = false;
        }

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is CorpseEvent corpseEvent)
            {
                corpseLocations.Add(corpseEvent);
            }
        }

        private bool FoundByCursor()
        {
            npcNameTargeting.ChangeNpcType(NpcNames.Corpse);

            wait.Fixed(playerReader.NetworkLatency.Value);
            npcNameTargeting.WaitForUpdate();

            if (!npcNameTargeting.FindBy(CursorType.Loot))
            {
                return false;
            }
            npcNameTargeting.ChangeNpcType(NpcNames.None);

            Log("Corpse clicked...");
            (bool searchTimeOut, double elapsedMs) = wait.Until(playerReader.NetworkLatency.Value, playerReader.Bits.HasTarget);
            Log($"Found Npc Name ? {!searchTimeOut} | Count: {npcNameTargeting.NpcCount} {elapsedMs}ms");

            CheckForGather();

            if (!MinRangeZero())
            {
                (bool timeout, elapsedMs) = wait.Until(MAX_TIME_TO_REACH_MELEE, MinRangeZero, input.ApproachOnCooldown);
                Log($"Reached clicked corpse ? {!timeout} {elapsedMs}ms");
            }

            return true;
        }

        private CorpseEvent? GetClosestCorpse()
        {
            if (corpseLocations.Count == 0)
                return null;

            int index = -1;
            float minDistance = float.MaxValue;
            Vector3 playerLoc = playerReader.PlayerLocation;
            for (int i = 0; i < corpseLocations.Count; i++)
            {
                float d = playerLoc.DistanceXYTo(corpseLocations[i].Location);
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
                state.GatherableCorpseCount++;

                CorpseEvent? e = GetClosestCorpse();
                if (e != null)
                    SendGoapEvent(new SkinCorpseEvent(e.Location, e.Radius, targetId));
            }

            Log($"Should gather {targetId} ? {gatherCorpse}");
        }

        private bool LootReadyOrClosed()
        {
            return successfulInBackground ||
                (LootStatus)playerReader.LootEvent.Value is
                LootStatus.READY or
                LootStatus.CLOSED;
        }

        private bool LootMouse()
        {
            stopMoving.Stop();
            wait.Update();

            if (FoundByCursor())
            {
                return true;
            }
            else if (corpseLocations.Count > 0)
            {
                Vector3 location = playerReader.PlayerLocation;
                CorpseEvent e = GetClosestCorpse()!;
                float heading = DirectionCalculator.CalculateHeading(location, e.Location);
                playerDirection.SetDirection(heading, e.Location);

                logger.LogInformation("Look at possible closest corpse and try once again...");

                wait.Fixed(playerReader.NetworkLatency.Value);

                if (FoundByCursor())
                {
                    return true;
                }
            }

            return LootKeyboard();
        }

        private bool LootKeyboard()
        {
            if (!playerReader.Bits.HasTarget())
            {
                input.FastLastTarget();
                wait.Update();
            }

            if (playerReader.Bits.HasTarget())
            {
                if (playerReader.Bits.TargetIsDead())
                {
                    CheckForGather();

                    Log("Target Last Target Found!");
                    input.FastInteract();

                    if (!MinRangeZero())
                    {
                        (bool timeout, double elapsedMs) = wait.Until(MAX_TIME_TO_REACH_MELEE, MinRangeZero, input.ApproachOnCooldown);
                        Log($"Reached Last Target ? {!timeout} {elapsedMs}ms");
                    }
                }
                else
                {
                    LogWarning("Don't attack alive target!");
                    input.ClearTarget();
                    wait.Update();

                    return false;
                }
            }

            return playerReader.Bits.HasTarget();
        }

        private bool LootReset()
        {
            return (LootStatus)playerReader.LootEvent.Value != LootStatus.CORPSE;
        }

        private void ListenLootEvent()
        {
            if (listenLootWindow &&
                (LootStatus)playerReader.LootEvent.Value is LootStatus.READY or LootStatus.CLOSED)
            {
                successfulInBackground = true;
            }
        }

        private bool MinRangeZero()
        {
            return playerReader.MinRange() == 0;
        }

        #region Logging

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

        #endregion
    }
}