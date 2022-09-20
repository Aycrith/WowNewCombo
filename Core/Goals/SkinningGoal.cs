using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Core.Goals
{
    public sealed class SkinningGoal : GoapGoal, IGoapEventListener, IDisposable
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

        private bool canRun;
        private int bagHash;

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

            //AddPrecondition(GoapKey.dangercombat, false);

            AddPrecondition(GoapKey.shouldgather, true);
            AddEffect(GoapKey.shouldgather, false);
        }

        public void Dispose()
        {
            bagReader.DataChanged -= BagReader_DataChanged;
            equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;
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

            (bool t, double e) = wait.Until(CastingHandler.GCD, LootReset);
            if (t)
            {
                LogWarning($"Loot window still open! {e}ms");
                ExitInterruptOrFailed(false);
                return;
            }

            bagHash = bagReader.Hash;

            wait.Fixed(playerReader.NetworkLatency.Value);

            if (bagReader.BagsFull())
            {
                LogWarning("Inventory is full!");
            }

            int attempts = 0;
            while (attempts < MAX_ATTEMPTS)
            {
                bool foundTarget = playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead();

                if (!foundTarget && state.LastCombatKillCount == 1)
                {
                    input.FastLastTarget();
                    wait.Update();

                    if (playerReader.Bits.HasTarget())
                    {
                        if (playerReader.Bits.TargetIsDead())
                        {
                            foundTarget = true;
                            Log("Last Target found!");
                        }
                        else
                        {
                            Log("Last Target is alive!");
                            input.ClearTarget();
                            wait.Update();
                        }
                    }
                }

                bool interact = false;
                if (!foundTarget && !input.ClassConfig.KeyboardOnly)
                {
                    stopMoving.Stop();
                    combatUtil.Update();

                    npcNameTargeting.ChangeNpcType(NpcNames.Corpse);
                    (t, e) = wait.Until(MAX_TIME_TO_WAIT_NPC_NAME, npcNameTargeting.FoundNpcName);
                    Log($"Found Npc Name ? {!t} | Count: {npcNameTargeting.NpcCount} {e}ms");

                    foundTarget = npcNameTargeting.FindBy(CursorType.Skin, CursorType.Mine, CursorType.Herb); // todo salvage icon
                    interact = true;
                }

                if (!foundTarget)
                {
                    LogWarning($"Unable to gather Target({playerReader.TargetId})!");
                    ExitInterruptOrFailed(false);
                    return;
                }

                if (!MinRangeZero())
                {
                    (t, e) = wait.Until(MAX_TIME_TO_REACH_MELEE, MinRangeZero, input.ApproachOnCooldown);
                    Log($"Reached Target ? {!t} {e}ms");
                    interact = true;
                }

                playerReader.LastUIError = 0;
                playerReader.CastEvent.ForceUpdate(0);

                (t, e) = wait.Until(MAX_TIME_TO_DETECT_CAST, CastStartedOrFailed, interact ? Empty : WhileNotCastingInteract);

                Log($"Started casting or interrupted ? {!t} - casting: {playerReader.IsCasting()} {e}ms");
                if (playerReader.LastUIError == UI_ERROR.ERR_REQUIRES_S)
                {
                    LogWarning("Missing Spell/Item/Skill Requirement!");
                    ExitInterruptOrFailed(false);
                    return;
                }
                else if ((t || playerReader.LastUIError == UI_ERROR.ERR_LOOT_LOCKED) && !playerReader.IsCasting())
                {
                    int delay = playerReader.LastUIError == UI_ERROR.ERR_LOOT_LOCKED
                        ? Loot.LOOTFRAME_AUTOLOOT_DELAY
                        : playerReader.NetworkLatency.Value;

                    Log($"Wait {delay}ms and try again...");
                    wait.Fixed(delay);

                    Log($"Try again: {playerReader.CastState.ToStringF()} | {playerReader.LastUIError.ToStringF()} | {playerReader.IsCasting()}");
                    attempts++;

                    ClearTargetIfExists();
                    continue;
                }

                bool herbalism = Array.BinarySearch(GatherSpells.Herbalism, playerReader.SpellBeingCast) > -1;

                int remainMs = playerReader.RemainCastMs;
                playerReader.LastUIError = 0;

                int waitTime = remainMs + CastingHandler.SpellQueueHalfMs + playerReader.NetworkLatency.Value;
                Log($"Waiting for {(herbalism ? "Herb Gathering" : "Skinning")} castbar to end! {waitTime}ms");

                (t, e) = wait.Until(waitTime, herbalism ? HerbalismCastEnded : SkinningCastEnded);

                if (herbalism
                    ? t || playerReader.LastUIError != UI_ERROR.SPELL_FAILED_TRY_AGAIN
                    : playerReader.CastState == UI_ERROR.CAST_SUCCESS)
                {
                    Log($"Gathering Successful!");
                    ExitSuccess();
                    return;
                }
                else
                {
                    if (combatUtil.EnteredCombat())
                    {
                        Log("Interrupted due combat!");
                        ExitInterruptOrFailed(true);
                        return;
                    }

                    wait.Fixed(Loot.LOOTFRAME_AUTOLOOT_DELAY);
                    LogWarning($"Gathering Failed! {playerReader.CastState.ToStringF()} attempts: {attempts}");
                    attempts++;

                    ClearTargetIfExists();
                }
            }

            LogWarning($"Ran out of {attempts} maximum attempts...");
            ExitInterruptOrFailed(false);
        }

        public override void OnExit()
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
        }

        private void ExitSuccess()
        {
            (bool t, double e) = wait.Until(MAX_TIME_TO_DETECT_LOOT, LootWindowClosedOrBagChanged);
            Log($"Loot {((!t && !bagReader.BagsFull()) ? "Successful" : "Failed")} after {e}ms");

            SendGoapEvent(new RemoveClosestPoi(SkinCorpseEvent.NAME));
            state.GatherableCorpseCount = Math.Max(0, state.GatherableCorpseCount - 1);

            ClearTargetIfExists();
        }

        private void ExitInterruptOrFailed(bool interrupted)
        {
            if (!interrupted)
                state.GatherableCorpseCount = Math.Max(0, state.GatherableCorpseCount - 1);

            ClearTargetIfExists();
        }

        private void ClearTargetIfExists()
        {
            if (playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead())
            {
                input.ClearTarget();
                wait.Update();
            }
        }

        private void WhileNotCastingInteract()
        {
            if (!playerReader.IsCasting())
                input.ApproachOnCooldown();
        }

        private static void Empty() { }

        private bool LootReset()
        {
            return (LootStatus)playerReader.LootEvent.Value == LootStatus.CORPSE;
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

        private bool LootWindowClosedOrBagChanged()
        {
            return bagHash != bagReader.Hash ||
                (LootStatus)playerReader.LootEvent.Value is
                LootStatus.CLOSED;
        }

        private bool SkinningCastEnded()
        {
            return
                playerReader.CastState is
                UI_ERROR.CAST_SUCCESS or
                UI_ERROR.SPELL_FAILED_TRY_AGAIN;
        }

        private bool HerbalismCastEnded()
        {
            return
                playerReader.LastUIError is
                UI_ERROR.SPELL_FAILED_TRY_AGAIN;
        }

        private bool CastStartedOrFailed()
        {
            return playerReader.IsCasting() ||
                playerReader.LastUIError is
                UI_ERROR.ERR_LOOT_LOCKED or
                UI_ERROR.ERR_REQUIRES_S;
        }

        private bool MinRangeZero()
        {
            return playerReader.MinRange() == 0;
        }

        private void Log(string text)
        {
            logger.LogInformation(text);
        }

        private void LogWarning(string text)
        {
            logger.LogWarning(text);
        }
    }
}
