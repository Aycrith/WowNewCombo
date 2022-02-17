using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public partial class Blacklist : IBlacklist
    {
        private readonly HashSet<string> blacklist = new();

        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly ILogger logger;
        private readonly int above;
        private readonly int below;
        private readonly bool checkTargetGivesExp;

        private int lastGuid;

        public Blacklist(ILogger logger, AddonReader addonReader, int above, int below, bool checkTargetGivesExp, List<string> blacklisted)
        {
            this.addonReader = addonReader;
            playerReader = addonReader.PlayerReader;
            this.logger = logger;
            this.above = above;
            this.below = below;

            this.checkTargetGivesExp = checkTargetGivesExp;

            blacklisted.ForEach(name => blacklist.Add(name.ToUpper()));
        }

        public void Add(string name)
        {
            blacklist.Add(name);
        }

        public bool IsTargetBlacklisted()
        {
            if (!playerReader.HasTarget)
            {
                lastGuid = 0;
                return false;
            }
            else if (addonReader.CreatureHistory.DamageTaken.Exists(x => x.HealthPercent > 0 && x.Guid == playerReader.TargetGuid))
            {
                return false;
            }

            if (playerReader.PetHasTarget && playerReader.TargetGuid == playerReader.PetGuid)
            {
                return true;
            }

            // it is trying to kill me
            if (playerReader.Bits.TargetOfTargetIsPlayer)
            {
                return false;
            }

            if (!playerReader.Bits.TargetIsNormal)
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogNotNormal(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }

                return true; // ignore elites
            }

            if (playerReader.Bits.IsTagged)
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogTagged(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }

                return true; // ignore tagged mobs
            }


            if (playerReader.Bits.TargetCanBeHostile && playerReader.TargetLevel > playerReader.Level.Value + above)
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogLevelHigh(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }

                return true; // ignore if current level + 2
            }

            if (checkTargetGivesExp)
            {
                if (!playerReader.TargetYieldXP)
                {
                    if (lastGuid != playerReader.TargetGuid)
                    {
                        LogNoExperienceGain(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                        lastGuid = playerReader.TargetGuid;
                    }
                    return true;
                }
            }
            else if (playerReader.Bits.TargetCanBeHostile && playerReader.TargetLevel < playerReader.Level.Value - below)
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogLevelLow(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }
                return true; // ignore if current level - 7
            }

            string? match = blacklist.FirstOrDefault(s => addonReader.TargetName.ToUpper().StartsWith(s));
            if (!string.IsNullOrEmpty(match))
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogNameMatch(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName, match);
                    lastGuid = playerReader.TargetGuid;
                }
                return true;
            }

            return false;
        }

        #region logging

        [LoggerMessage(
            EventId = 60,
            Level = LogLevel.Warning,
            Message = "Blacklisted ({id},{guid},{name}) not a normal mob!")]
        static partial void LogNotNormal(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 61,
            Level = LogLevel.Warning,
            Message = "Blacklisted ({id},{guid},{name}) is tagged!")]
        static partial void LogTagged(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 62,
            Level = LogLevel.Warning,
            Message = "Blacklisted ({id},{guid},{name}) too high level!")]
        static partial void LogLevelHigh(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 63,
            Level = LogLevel.Warning,
            Message = "Blacklisted ({id},{guid},{name}) too low level!")]
        static partial void LogLevelLow(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 64,
            Level = LogLevel.Warning,
            Message = "Blacklisted ({id},{guid},{name}) not yield experience!")]
        static partial void LogNoExperienceGain(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 65,
            Level = LogLevel.Warning,
            Message = "Blacklisted ({id},{guid},{name}) name match {match}!")]
        static partial void LogNameMatch(ILogger logger, int id, int guid, string name, string match);

        #endregion
    }
}