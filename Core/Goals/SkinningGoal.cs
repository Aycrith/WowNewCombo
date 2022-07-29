using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Core.Goals
{
    public class SkinningGoal : GoapGoal, IGoapEventListener, IDisposable
    {
        public override float Cost => 4.4f;

        private const int MAX_ATTEMPTS = 5;
        private const int MAX_TIME_TO_REACH_MELEE = 10000;
        private const int MAX_TIME_TO_DETECT_LOOT = 2 * CastingHandler.GCD;
        private const int MAX_TIME_TO_DETECT_CAST = 2 * CastingHandler.GCD;
        private const int MAX_TIME_TO_WAIT_NPC_NAME = 1000;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly StopMoving stopMoving;
        private readonly BagReader bagReader;
        private readonly EquipmentReader equipmentReader;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly CombatUtil combatUtil;
        private readonly GoapAgentState state;

        private int lastCastEvent;
        private bool canRun;

        private bool listenLootWindow;
        private bool successfulInBackground;

        private readonly List<SkinCorpseEvent> corpses = new();

        public SkinningGoal(ILogger logger, ConfigurableInput input,
            AddonReader addonReader, Wait wait, StopMoving stopMoving,
            NpcNameTargeting npcNameTargeting, CombatUtil combatUtil,
            GoapAgentState state)
            : base(nameof(SkinningGoal))
        {
            this.logger = logger;
            this.input = input;

            this.playerReader = addonReader.PlayerReader;
            this.wait = wait;
            this.stopMoving = stopMoving;
            this.bagReader = addonReader.BagReader;
            this.equipmentReader = addonReader.EquipmentReader;

            this.npcNameTargeting = npcNameTargeting;
            this.combatUtil = combatUtil;
            this.state = state;

            canRun = HaveItemRequirement();
            bagReader.DataChanged -= BagReader_DataChanged;
            bagReader.DataChanged += BagReader_DataChanged;
            equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;
            equipmentReader.OnEquipmentChanged += EquipmentReader_OnEquipmentChanged;

            playerReader.LootEvent.Changed += ListenLootEvent;

            //AddPrecondition(GoapKey.dangercombat, false);

            AddPrecondition(GoapKey.shouldgather, true);
            AddEffect(GoapKey.shouldgather, false);


        }

        public void Dispose()
        {
            bagReader.DataChanged -= BagReader_DataChanged;
            equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;

            playerReader.LootEvent.Changed -= ListenLootEvent;
        }

        public override bool CanRun() => canRun;

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is SkinCorpseEvent corpseEvent)
            {
                corpses.Add(corpseEvent);
            }
        }

        public override void OnEnter()
        {
            combatUtil.Update();

            wait.While(LootReset);

            successfulInBackground = false;
            listenLootWindow = true;

            wait.Fixed(playerReader.NetworkLatency.Value);

            if (bagReader.BagsFull)
            {
                LogWarning("Inventory is full!");
                SendGoapEvent(new GoapStateEvent(GoapKey.shouldgather, false));
            }

            int attempts = 1;
            while (attempts < MAX_ATTEMPTS)
            {
                bool foundTarget = false;

                if (playerReader.Bits.HasTarget())
                {
                    if (playerReader.Bits.TargetIsDead())
                    {
                        foundTarget = true;
                        input.FastInteract();
                    }
                    else
                    {
                        LogWarning("Last target is alive!");
                    }
                }
                else
                {
                    input.FastLastTarget();
                    wait.Update();

                    if (playerReader.Bits.HasTarget())
                    {
                        if (playerReader.Bits.TargetIsDead())
                        {
                            foundTarget = true;
                            input.FastInteract();
                        }
                        else
                        {
                            LogWarning("Last Target is alive! 2");
                        }
                    }
                    else
                    {
                        LogWarning("Last Target not found!");
                    }
                }

                if (!foundTarget && !input.ClassConfig.KeyboardOnly)
                {
                    stopMoving.Stop();
                    combatUtil.Update();

                    npcNameTargeting.ChangeNpcType(NpcNames.Corpse);
                    (bool timeOut, double elapsedMs) = wait.Until(MAX_TIME_TO_WAIT_NPC_NAME, npcNameTargeting.FoundNpcName);
                    Log($"Found Npc Name ? {!timeOut} | Count: {npcNameTargeting.NpcCount} {elapsedMs}ms");

                    foundTarget = npcNameTargeting.FindBy(CursorType.Skin, CursorType.Mine, CursorType.Herb); // todo salvage icon
                    wait.Update();
                }

                if (foundTarget)
                {
                    Log("Found corpse");

                    if (!MinRangeZero())
                    {
                        (bool timeout, double moveElapsedMs) = wait.Until(MAX_TIME_TO_REACH_MELEE, MinRangeZero, input.ApproachOnCooldown);
                        Log($"Reached Target ? {!timeout} {moveElapsedMs}ms");
                    }

                    bool castTimeout = false;
                    double elapsedMs = 0;

                    if (playerReader.IsCasting())
                    {
                        castTimeout = false;
                    }
                    else
                    {
                        (castTimeout, elapsedMs) = wait.Until(MAX_TIME_TO_DETECT_CAST, CastStartedOrFailed);
                    }

                    Log($"Started casting ? {!castTimeout} {elapsedMs}ms");
                    if (castTimeout)
                    {
                        Log($"Wait {playerReader.NetworkLatency.Value}ms and try again...");
                        wait.Fixed(playerReader.NetworkLatency.Value);

                        if (!playerReader.IsCasting())
                        {
                            Log($"Try again: {((UI_ERROR)playerReader.CastEvent.Value).ToStringF()} | {playerReader.LastUIError.ToStringF()} | {playerReader.IsCasting()}");
                            attempts++;
                            continue;
                        }
                    }

                    if (successfulInBackground)
                    {
                        GoalExit(true, false);
                        return;
                    }

                    lastCastEvent = playerReader.CastEvent.Value;
                    int remainMs = playerReader.RemainCastMs;

                    wait.Till(remainMs + playerReader.NetworkLatency.Value, CastStatusChanged);

                    if ((UI_ERROR)playerReader.CastEvent.Value is
                        UI_ERROR.CAST_SUCCESS or
                        UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS ||
                        successfulInBackground)
                    {
                        Log($"Gathering Successful!");
                        GoalExit(true, false);
                        return;
                    }
                    else
                    {
                        if (combatUtil.EnteredCombat())
                        {
                            Log("Interrupted due combat!");
                            GoalExit(false, true);
                            return;
                        }

                        LogWarning($"Gathering Failed! {((UI_ERROR)playerReader.CastEvent.Value).ToStringF()} attempts: {attempts}");
                        attempts++;
                    }
                }
                else
                {
                    LogWarning($"Unable to gather Target({playerReader.TargetId})!");

                    GoalExit(false, false);
                    return;
                }
            }

            GoalExit(false, false);
        }

        public override void OnExit()
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
            listenLootWindow = false;
        }

        private void GoalExit(bool castSuccess, bool interrupted)
        {
            (bool lootTimeOut, double elapsedMs) = wait.Until(MAX_TIME_TO_DETECT_LOOT, LootReadyOrClosed);
            Log($"Loot {(castSuccess && !lootTimeOut ? "Successful" : "Failed")} after {elapsedMs}ms");

            SendGoapEvent(new GoapStateEvent(GoapKey.shouldgather, interrupted));

            if (!interrupted)
                state.GatherableCorpseCount = Math.Max(0, state.GatherableCorpseCount - 1);

            if (playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead())
            {
                input.ClearTarget();
                wait.Update();
            }
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

        private void EquipmentReader_OnEquipmentChanged(object? sender, (int, int) e)
        {
            canRun = HaveItemRequirement();
        }

        private void BagReader_DataChanged()
        {
            canRun = HaveItemRequirement();
        }

        private bool HaveItemRequirement()
        {
            if (input.ClassConfig.Herb) return true;

            if (input.ClassConfig.Skin)
            {
                return
                bagReader.HasItem(7005) ||
                bagReader.HasItem(12709) ||
                bagReader.HasItem(19901) ||
                bagReader.HasItem(40772) || // army knife
                bagReader.HasItem(40893) ||

                equipmentReader.HasItem(7005) ||
                equipmentReader.HasItem(12709) ||
                equipmentReader.HasItem(19901);
            }

            if (input.ClassConfig.Mine || input.ClassConfig.Salvage)
                return
                bagReader.HasItem(40772) || // army knife
                                            // mining / todo salvage
                bagReader.HasItem(40893) ||
                bagReader.HasItem(20723) ||
                bagReader.HasItem(1959) ||
                bagReader.HasItem(9465) ||
                bagReader.HasItem(1819) ||
                bagReader.HasItem(40892) ||
                bagReader.HasItem(778) ||
                bagReader.HasItem(1893) ||
                bagReader.HasItem(2901) ||
                bagReader.HasItem(756);

            return false;
        }

        private bool LootReadyOrClosed()
        {
            return successfulInBackground ||
                (LootStatus)playerReader.LootEvent.Value is
                LootStatus.READY or
                LootStatus.CLOSED;
        }

        private bool CastStatusChanged()
        {
            return lastCastEvent != playerReader.CastEvent.Value;
        }

        private bool CastStartedOrFailed()
        {
            return successfulInBackground || playerReader.IsCasting();
        }

        private bool MinRangeZero()
        {
            return playerReader.MinRange() == 0;
        }

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(SkinningGoal)}: {text}");
        }

        private void LogWarning(string text)
        {
            logger.LogWarning($"{nameof(SkinningGoal)}: {text}");
        }
    }
}
