using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;

namespace Core.Goals
{
    public class SkinningGoal : GoapGoal, IDisposable
    {
        public override float CostOfPerformingAction => 4.6f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly StopMoving stopMoving;
        private readonly BagReader bagReader;
        private readonly EquipmentReader equipmentReader;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly CombatUtil combatUtil;

        private int lastLoot;
        private bool canRun;

        public SkinningGoal(ILogger logger, ConfigurableInput input, AddonReader addonReader, Wait wait, StopMoving stopMoving, NpcNameTargeting npcNameTargeting, CombatUtil combatUtil)
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

            canRun = HaveItemRequirement();
            bagReader.DataChanged -= BagReader_DataChanged;
            bagReader.DataChanged += BagReader_DataChanged;
            equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;
            equipmentReader.OnEquipmentChanged += EquipmentReader_OnEquipmentChanged;

            AddPrecondition(GoapKey.dangercombat, false);
            AddPrecondition(GoapKey.shouldskin, true);

            AddEffect(GoapKey.shouldskin, false);
        }

        public void Dispose()
        {
            bagReader.DataChanged -= BagReader_DataChanged;
            equipmentReader.OnEquipmentChanged -= EquipmentReader_OnEquipmentChanged;
        }

        public override bool CheckIfActionCanRun()
        {
            return canRun;
        }

        public override void OnEnter()
        {
            if (bagReader.BagsFull)
            {
                LogWarning("Inventory is full!");
                SendActionEvent(new ActionEventArgs(GoapKey.shouldskin, false));
            }

            Log($"Search for {NpcNames.Corpse}");
            npcNameTargeting.ChangeNpcType(NpcNames.Corpse);
            npcNameTargeting.WaitForUpdate();

            lastLoot = playerReader.LastLootTime;

            stopMoving.Stop();
            combatUtil.Update();

            int attempts = 1;
            while (attempts < 5)
            {
                if (combatUtil.EnteredCombat())
                {
                    if (combatUtil.AquiredTarget())
                    {
                        Log("Interrupted!");
                        return;
                    }
                }

                bool foundCursor = npcNameTargeting.FindBy(CursorType.Skin);
                if (foundCursor)
                {
                    Log("Found corpse - interacted with right click");
                    wait.Update();

                    (bool foundTarget, bool moved) = combatUtil.FoundTargetWhileMoved();
                    if (foundTarget)
                    {
                        Log("Interrupted!");
                        return;
                    }

                    if (moved)
                    {
                        Log("Had to move so interact again");
                        input.Interact();
                        wait.Update();
                    }

                    // wait until start casting
                    wait.Till(500, StartsCasting);
                    Log("Started casting...");

                    playerReader.LastUIError = UI_ERROR.NONE;

                    wait.Till(3000, CastFinishedOrInterrupted);

                    if (playerReader.LastUIError != UI_ERROR.ERR_SPELL_FAILED_S)
                    {
                        playerReader.LastUIError = UI_ERROR.NONE;
                        Log($"Skinning Successful! {playerReader.LastUIError.ToStringF()}");

                        GoalExit();
                        return;
                    }
                    else
                    {
                        Log($"Skinning Failed! Retry... Attempts: {attempts}");
                        attempts++;
                    }
                }
                else
                {
                    Log($"Target({playerReader.TargetId}) is not skinnable - NPC Count: {npcNameTargeting.NpcCount}");

                    GoalExit();
                    return;
                }
            }
        }

        public override void OnExit()
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
        }

        public override void PerformAction()
        {
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
            }

            lastLoot = playerReader.LastLootTime;

            SendActionEvent(new ActionEventArgs(GoapKey.shouldskin, false));

            if (playerReader.HasTarget && playerReader.Bits.TargetIsDead)
            {
                input.ClearTarget();
                wait.Update();
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
            return
                bagReader.HasItem(7005) ||
                bagReader.HasItem(12709) ||
                bagReader.HasItem(19901) ||
                bagReader.HasItem(40772) ||
                bagReader.HasItem(40893) ||

                equipmentReader.HasItem(7005) ||
                equipmentReader.HasItem(12709) ||
                equipmentReader.HasItem(19901);
        }

        private bool LootChanged()
        {
            return lastLoot != playerReader.LastLootTime;
        }

        private bool StartsCasting()
        {
            return playerReader.IsCasting;
        }

        private bool CastFinishedOrInterrupted()
        {
            return !playerReader.IsCasting || playerReader.LastUIError != UI_ERROR.NONE;
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
