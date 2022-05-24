using Core.Goals;
using Game;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.GOAP
{
    public sealed partial class GoapAgent : IDisposable
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly IBlacklist blacklist;
        private readonly WowScreen wowScreen;

        public bool Active { get; set; }

        public GoapAgentState State { get; }

        public IEnumerable<GoapGoal> AvailableGoals { get; }

        public Stack<GoapGoal> Plan { get; private set; }
        public GoapGoal? CurrentGoal { get; private set; }

        public GoapAgent(ILogger logger, WowScreen wowScreen, GoapAgentState goapAgentState, ConfigurableInput input, AddonReader addonReader, HashSet<GoapGoal> availableGoals, IBlacklist blacklist)
        {
            this.logger = logger;
            this.wowScreen = wowScreen;
            this.State = goapAgentState;
            this.input = input;

            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;

            this.addonReader.CreatureHistory.KillCredit -= OnKillCredit;
            this.addonReader.CreatureHistory.KillCredit += OnKillCredit;

            this.stopMoving = new StopMoving(input, playerReader);
            this.blacklist = blacklist;

            this.AvailableGoals = availableGoals.OrderBy(a => a.CostOfPerformingAction);
            this.Plan = new();
        }

        public void Dispose()
        {
            foreach (var goal in AvailableGoals.Where(x => x is IDisposable).AsEnumerable().OfType<IDisposable>())
            {
                goal.Dispose();
            }

            addonReader.CreatureHistory.KillCredit -= OnKillCredit;
        }

        public GoapGoal? GetAction()
        {
            if (blacklist.IsTargetBlacklisted())
            {
                input.StopAttack();
                input.ClearTarget();
            }

            if (Plan.Count == 0)
            {
                Plan = GoapPlanner.Plan(AvailableGoals, GetWorldState(), GoapPlanner.EmptyGoalState);
            }
            CurrentGoal = Plan.Count > 0 ? Plan.Pop() : null;

            return CurrentGoal;
        }

        private Dictionary<GoapKey, bool> GetWorldState()
        {
            return new()
            {
                { GoapKey.hastarget, !blacklist.IsTargetBlacklisted() && playerReader.HasTarget },
                { GoapKey.dangercombat, playerReader.Bits.PlayerInCombat && addonReader.CombatCreatureCount > 0 },
                { GoapKey.pethastarget, playerReader.PetHasTarget },
                { GoapKey.targetisalive, playerReader.HasTarget && !playerReader.Bits.TargetIsDead },
                { GoapKey.targettargetsus, (playerReader.HasTarget && playerReader.TargetHealthPercentage < 30) || playerReader.TargetTarget is // hacky way to keep attacking fleeing humanoids
                    TargetTargetEnum.Me or
                    TargetTargetEnum.Pet or
                    TargetTargetEnum.PartyOrPet },
                { GoapKey.incombat, playerReader.Bits.PlayerInCombat },
                { GoapKey.withinpullrange, playerReader.WithInPullRange },
                { GoapKey.incombatrange, playerReader.WithInCombatRange },
                { GoapKey.pulled, false },
                { GoapKey.isdead, playerReader.Bits.DeadStatus },
                { GoapKey.isswimming, playerReader.Bits.IsSwimming },
                { GoapKey.itemsbroken, playerReader.Bits.ItemsAreBroken },
                { GoapKey.producedcorpse, State.LastCombatKillCount > 0 },

                // these hold their state
                { GoapKey.consumecorpse, State.ShouldConsumeCorpse },
                { GoapKey.shouldloot, State.NeedLoot },
                { GoapKey.shouldskin, State.NeedSkin },
                { GoapKey.gathering, State.Gathering }
            };
        }

        public void OnActionEvent(object sender, ActionEventArgs e)
        {
            switch (e.Key)
            {
                case GoapKey.consumecorpse:
                    State.ShouldConsumeCorpse = (bool)e.Value;
                    break;
                case GoapKey.shouldloot:
                    State.NeedLoot = (bool)e.Value;
                    break;
                case GoapKey.shouldskin:
                    State.NeedSkin = (bool)e.Value;
                    break;
                case GoapKey.gathering:
                    State.Gathering = (bool)e.Value;
                    break;
                case GoapKey.wowscreen:
                    wowScreen.Enabled = (bool)e.Value;
                    break;
            }
        }

        private void OnKillCredit()
        {
            if (Active)
            {
                State.LastCombatKillCount++;
                Broadcast(GoapKey.producedcorpse, true);

                LogActiveKillDetected(logger, State.LastCombatKillCount, addonReader.CombatCreatureCount);
            }
            else
            {
                LogInactiveKillDetected(logger);
            }
        }

        private void Broadcast(GoapKey goapKey, bool value)
        {
            if (CurrentGoal == null)
            {
                AvailableGoals.ToList().ForEach(x => x.OnActionEvent(this, new ActionEventArgs(goapKey, value)));
            }
            else
            {
                CurrentGoal.OnActionEvent(this, new ActionEventArgs(goapKey, value));
            }
        }


        public void NodeFound()
        {
            State.Gathering = true;
            Broadcast(GoapKey.gathering, true);
        }

        #region Logging

        [LoggerMessage(
            EventId = 50,
            Level = LogLevel.Information,
            Message = "Kill credit detected! Known kills: {count} | Remains: {remain}")]
        static partial void LogActiveKillDetected(ILogger logger, int count, int remain);

        [LoggerMessage(
            EventId = 51,
            EventName = $"{nameof(GoapAgent)}",
            Level = LogLevel.Information,
            Message = "Inactive, kill credit detected!")]
        static partial void LogInactiveKillDetected(ILogger logger);

        #endregion
    }
}