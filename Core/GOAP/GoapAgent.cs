using Core.Goals;
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

        public bool Active { get; set; }

        public GoapAgentState State { get; }

        public IEnumerable<GoapGoal> AvailableGoals { get; }
        public GoapGoal? CurrentGoal { get; private set; }

        public GoapAgent(ILogger logger, GoapAgentState goapAgentState, ConfigurableInput input, AddonReader addonReader, HashSet<GoapGoal> availableGoals, IBlacklist blacklist)
        {
            this.logger = logger;
            this.State = goapAgentState;
            this.input = input;

            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;

            this.addonReader.CreatureHistory.KillCredit -= OnKillCredit;
            this.addonReader.CreatureHistory.KillCredit += OnKillCredit;

            this.stopMoving = new StopMoving(input, playerReader);
            this.blacklist = blacklist;

            this.AvailableGoals = availableGoals.OrderBy(a => a.CostOfPerformingAction);
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

            var plan = GoapPlanner.Plan(AvailableGoals, GetWorldState(), GoapPlanner.EmptyGoalState);
            CurrentGoal = plan.Count > 0 ? plan.Peek() : null;
            return CurrentGoal;
        }

        private HashSet<KeyValuePair<GoapKey, bool>> GetWorldState()
        {
            return new()
            {
                new(GoapKey.hastarget, !blacklist.IsTargetBlacklisted() && playerReader.HasTarget),
                new(GoapKey.dangercombat, addonReader.PlayerReader.Bits.PlayerInCombat && addonReader.CombatCreatureCount > 0),
                new(GoapKey.pethastarget, playerReader.PetHasTarget),
                new(GoapKey.targetisalive, playerReader.HasTarget && !playerReader.Bits.TargetIsDead),
                new(GoapKey.incombat, playerReader.Bits.PlayerInCombat),
                new(GoapKey.withinpullrange, playerReader.WithInPullRange),
                new(GoapKey.incombatrange, playerReader.WithInCombatRange),
                new(GoapKey.pulled, false),
                new(GoapKey.isdead, playerReader.Bits.DeadStatus),
                new(GoapKey.isswimming, playerReader.Bits.IsSwimming),
                new(GoapKey.itemsbroken, playerReader.Bits.ItemsAreBroken),
                new(GoapKey.producedcorpse, State.LastCombatKillCount > 0),

                // these hold their state
                new(GoapKey.consumecorpse, State.ShouldConsumeCorpse),
                new(GoapKey.shouldloot, State.NeedLoot),
                new(GoapKey.shouldskin, State.NeedSkin)
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
            }
        }

        private void OnKillCredit(object? obj, EventArgs e)
        {
            if (Active)
            {
                State.LastCombatKillCount++;

                if (CurrentGoal == null)
                {
                    AvailableGoals.ToList().ForEach(x => x.OnActionEvent(this, new ActionEventArgs(GoapKey.producedcorpse, true)));
                }
                else
                {
                    CurrentGoal.OnActionEvent(this, new ActionEventArgs(GoapKey.producedcorpse, true));
                }

                LogActiveKillDetected(logger, State.LastCombatKillCount, addonReader.CombatCreatureCount);
            }
            else
            {
                LogInactiveKillDetected(logger);
            }
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