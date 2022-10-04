using Microsoft.Extensions.Logging;

using System;

using SharedLib.Extensions;

namespace Core
{
    public sealed partial class TargetBlacklist : IBlacklist
    {
        private readonly string[] blacklist;

        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly ILogger logger;
        private readonly int above;
        private readonly int below;
        private readonly bool checkTargetGivesExp;
        private readonly UnitClassification targetMask;

        private readonly bool allowPvP;

        private int lastGuid;

        public TargetBlacklist(ILogger logger, AddonReader addonReader, ClassConfiguration classConfig)
        {
            this.addonReader = addonReader;
            playerReader = addonReader.PlayerReader;
            this.logger = logger;
            this.above = classConfig.NPCMaxLevels_Above;
            this.below = classConfig.NPCMaxLevels_Below;

            this.checkTargetGivesExp = classConfig.CheckTargetGivesExp;
            this.targetMask = classConfig.TargetMask;

            this.blacklist = classConfig.Blacklist;

            this.allowPvP = classConfig.AllowPvP;

            logger.LogInformation($"[{nameof(TargetBlacklist)}] {nameof(classConfig.TargetMask)}: {string.Join(", ", targetMask.GetIndividualFlags())}");

            if (blacklist.Length > 0)
                logger.LogInformation($"[{nameof(TargetBlacklist)}] Name: {string.Join(", ", blacklist)}");
        }

        public bool Is()
        {
            if (!playerReader.Bits.HasTarget())
            {
                lastGuid = 0;
                return false;
            }
            else if (addonReader.CombatLog.DamageTaken.Contains(playerReader.TargetGuid))
            {
                return false;
            }

            if (playerReader.PetHasTarget() && playerReader.TargetGuid == playerReader.PetGuid)
            {
                return true;
            }

            // it is trying to kill me
            if (playerReader.Bits.TargetOfTargetIsPlayerOrPet())
            {
                return false;
            }

            if (!targetMask.HasFlag(playerReader.TargetClassification))
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogClassification(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName, playerReader.TargetClassification.ToStringF());
                    lastGuid = playerReader.TargetGuid;
                }

                return true; // ignore non white listed unit classification
            }

            if (!allowPvP && (playerReader.Bits.TargetIsPlayer() || playerReader.Bits.TargetIsPlayerControlled()))
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogPlayerOrPet(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }

                return true; // ignore players and pets
            }

            if (!playerReader.Bits.TargetIsDead() && playerReader.Bits.TargetIsTagged())
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogTagged(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }

                return true; // ignore tagged mobs
            }


            if (playerReader.Bits.TargetCanBeHostile() && playerReader.TargetLevel > playerReader.Level.Value + above)
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
                if (playerReader.Bits.TargetIsTrivial())
                {
                    if (lastGuid != playerReader.TargetGuid)
                    {
                        LogNoExperienceGain(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                        lastGuid = playerReader.TargetGuid;
                    }
                    return true;
                }
            }
            else if (playerReader.Bits.TargetCanBeHostile() && playerReader.TargetLevel < playerReader.Level.Value - below)
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogLevelLow(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }
                return true; // ignore if current level - 7
            }

            if (blacklist.Length > 0 && Contains())
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogNameMatch(logger, playerReader.TargetId, playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }
                return true;
            }

            return false;
        }

        private bool Contains()
        {
            for (int i = 0; i < blacklist.Length; i++)
            {
                if (addonReader.TargetName.Contains(blacklist[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #region logging

        [LoggerMessage(
            EventId = 60,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name}) is player or pet!")]
        static partial void LogPlayerOrPet(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 61,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name}) is tagged!")]
        static partial void LogTagged(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 62,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name}) too high level!")]
        static partial void LogLevelHigh(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 63,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name}) too low level!")]
        static partial void LogLevelLow(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 64,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name}) not yield experience!")]
        static partial void LogNoExperienceGain(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 65,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name}) name match!")]
        static partial void LogNameMatch(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 66,
            Level = LogLevel.Warning,
            Message = "Blacklist ({id},{guid},{name},{classification}) not defined in the TargetMask!")]
        static partial void LogClassification(ILogger logger, int id, int guid, string name, string classification);

        #endregion
    }
}