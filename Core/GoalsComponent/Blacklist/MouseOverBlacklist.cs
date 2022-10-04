using Microsoft.Extensions.Logging;

using System;

using SharedLib.Extensions;

namespace Core
{
    public sealed partial class MouseOverBlacklist : IBlacklist
    {
        private readonly string[] blacklist;

        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly ILogger logger;
        private readonly int above;
        private readonly int below;
        private readonly bool checkMouseOverGivesExp;
        private readonly UnitClassification mask;

        private readonly bool allowPvP;

        private int lastGuid;

        public MouseOverBlacklist(ILogger logger, AddonReader addonReader, ClassConfiguration classConfig)
        {
            this.addonReader = addonReader;
            playerReader = addonReader.PlayerReader;
            this.logger = logger;
            this.above = classConfig.NPCMaxLevels_Above;
            this.below = classConfig.NPCMaxLevels_Below;

            this.checkMouseOverGivesExp = classConfig.CheckTargetGivesExp;
            this.mask = classConfig.TargetMask;

            this.blacklist = classConfig.Blacklist;

            this.allowPvP = classConfig.AllowPvP;

            logger.LogInformation($"[{nameof(MouseOverBlacklist)}] {nameof(classConfig.TargetMask)}: {string.Join(", ", mask.GetIndividualFlags())}");

            if (blacklist.Length > 0)
                logger.LogInformation($"[{nameof(MouseOverBlacklist)}] Name: {string.Join(", ", blacklist)}");
        }

        public bool Is()
        {
            if (!playerReader.Bits.HasMouseOver())
            {
                lastGuid = 0;
                return false;
            }
            else if (addonReader.CombatLog.DamageTaken.Contains(playerReader.MouseOverGuid))
            {
                return false;
            }

            if (playerReader.PetHasTarget() && playerReader.MouseOverGuid == playerReader.PetGuid)
            {
                return true;
            }

            // it is trying to kill me
            if (playerReader.Bits.MouseOverTargetIsPlayerOrPet())
            {
                return false;
            }

            if (!mask.HasFlag(playerReader.MouseOverClassification))
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogClassification(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName, playerReader.MouseOverClassification.ToStringF());
                    lastGuid = playerReader.MouseOverGuid;
                }

                return true; // ignore non white listed unit classification
            }

            if (!allowPvP && (playerReader.Bits.MouseOverIsPlayer() || playerReader.Bits.MouseOverPlayerControlled()))
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogPlayerOrPet(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                    lastGuid = playerReader.MouseOverGuid;
                }

                return true; // ignore players and pets
            }

            if (!playerReader.Bits.MouseOverIsDead() && playerReader.Bits.MouseOverIsTagged())
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogTagged(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                    lastGuid = playerReader.MouseOverGuid;
                }

                return true; // ignore tagged mobs
            }


            if (playerReader.Bits.MouseOverCanBeHostile() && playerReader.MouseOverLevel > playerReader.Level.Value + above)
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogLevelHigh(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                    lastGuid = playerReader.MouseOverGuid;
                }

                return true; // ignore if current level + 2
            }

            if (checkMouseOverGivesExp)
            {
                if (playerReader.Bits.MouseOverIsTrivial())
                {
                    if (lastGuid != playerReader.MouseOverGuid)
                    {
                        LogNoExperienceGain(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                        lastGuid = playerReader.MouseOverGuid;
                    }
                    return true;
                }
            }
            else if (playerReader.Bits.MouseOverCanBeHostile() && playerReader.MouseOverLevel < playerReader.Level.Value - below)
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogLevelLow(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                    lastGuid = playerReader.MouseOverGuid;
                }
                return true; // ignore if current level - 7
            }

            if (blacklist.Length > 0 && Contains())
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogNameMatch(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                    lastGuid = playerReader.MouseOverGuid;
                }
                return true;
            }

            return false;
        }

        private bool Contains()
        {
            for (int i = 0; i < blacklist.Length; i++)
            {
                if (addonReader.MouseOverName.Contains(blacklist[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #region logging

        [LoggerMessage(
            EventId = 60,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name}) is player!")]
        static partial void LogPlayerOrPet(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 61,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name}) is tagged!")]
        static partial void LogTagged(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 62,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name}) too high level!")]
        static partial void LogLevelHigh(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 63,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name}) too low level!")]
        static partial void LogLevelLow(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 64,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name}) not yield experience!")]
        static partial void LogNoExperienceGain(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 65,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name}) name match!")]
        static partial void LogNameMatch(ILogger logger, int id, int guid, string name);

        [LoggerMessage(
            EventId = 66,
            Level = LogLevel.Warning,
            Message = "MouseOverBlacklist ({id},{guid},{name},{classification}) not defined in the TargetMask!")]
        static partial void LogClassification(ILogger logger, int id, int guid, string name, string classification);

        #endregion
    }
}