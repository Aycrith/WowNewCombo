using Microsoft.Extensions.Logging;

using System;

using SharedLib.Extensions;
using System.Collections.Generic;

namespace Core;

public sealed partial class TargetBlacklist : IBlacklist, IDisposable
{
    private readonly string[] blacklist;

    private readonly ILogger<TargetBlacklist> logger;

    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly CombatLog combatLog;

    private readonly int above;
    private readonly int below;
    private readonly bool checkTargetGivesExp;
    private readonly UnitClassification targetMask;

    private readonly bool allowPvP;

    private int lastGuid;
    private readonly HashSet<int> evadeMobs;

    public TargetBlacklist(ILogger<TargetBlacklist> logger,
        AddonReader addonReader, PlayerReader playerReader,
        CombatLog combatLog,
        AddonBits bits, ClassConfiguration classConfig)
    {
        this.addonReader = addonReader;
        this.playerReader = playerReader;
        this.combatLog = combatLog;
        this.bits = bits;

        this.logger = logger;
        this.above = classConfig.NPCMaxLevels_Above;
        this.below = classConfig.NPCMaxLevels_Below;

        this.checkTargetGivesExp = classConfig.CheckTargetGivesExp;
        this.targetMask = classConfig.TargetMask;

        this.blacklist = classConfig.Blacklist;

        this.allowPvP = classConfig.AllowPvP;

        evadeMobs = new HashSet<int>();

        combatLog.TargetEvade += CombatLog_TargetEvade;

        logger.LogInformation($"{nameof(classConfig.TargetMask)}: {string.Join(", ", targetMask.GetIndividualFlags())}");

        if (blacklist.Length > 0)
            logger.LogInformation($"Name: {string.Join(", ", blacklist)}");
    }

    public void Dispose()
    {
        combatLog.TargetEvade -= CombatLog_TargetEvade;
    }

    public bool Is()
    {
        if (!bits.Target())
        {
            lastGuid = 0;
            return false;
        }
        else if (combatLog.DamageTaken.Contains(playerReader.TargetGuid))
        {
            return false;
        }

        if (playerReader.PetTarget() && playerReader.TargetGuid == playerReader.PetGuid)
        {
            return true;
        }

        if (evadeMobs.Contains(playerReader.TargetGuid))
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogEvade(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName,
                    playerReader.TargetClassification.ToStringF());

                lastGuid = playerReader.TargetGuid;
            }
            return true;
        }

        // it is trying to kill me
        if (bits.TargetTarget_PlayerOrPet())
        {
            return false;
        }

        if (!targetMask.HasFlagF(playerReader.TargetClassification))
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogClassification(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName,
                    playerReader.TargetClassification.ToStringF());
                lastGuid = playerReader.TargetGuid;
            }

            return true; // ignore non white listed unit classification
        }

        if (!allowPvP && (bits.Target_Player() || bits.Target_PlayerControlled()))
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogPlayerOrPet(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName);

                lastGuid = playerReader.TargetGuid;
            }

            return true; // ignore players and pets
        }

        if (!bits.Target_Dead() && bits.Target_Tagged())
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogTagged(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName);
                lastGuid = playerReader.TargetGuid;
            }

            return true; // ignore tagged mobs
        }


        if (bits.Target_Hostile() && playerReader.TargetLevel > playerReader.Level.Value + above)
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogLevelHigh(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName);
                lastGuid = playerReader.TargetGuid;
            }

            return true; // ignore if current level + 2
        }

        if (checkTargetGivesExp)
        {
            if (bits.Target_Trivial())
            {
                if (lastGuid != playerReader.TargetGuid)
                {
                    LogNoExperienceGain(logger, playerReader.TargetId,
                        playerReader.TargetGuid, addonReader.TargetName);
                    lastGuid = playerReader.TargetGuid;
                }
                return true;
            }
        }
        else if (bits.Target_Hostile() && playerReader.TargetLevel < playerReader.Level.Value - below)
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogLevelLow(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName);
                lastGuid = playerReader.TargetGuid;
            }
            return true; // ignore if current level - 7
        }

        if (blacklist.Length > 0 && Contains())
        {
            if (lastGuid != playerReader.TargetGuid)
            {
                LogNameMatch(logger, playerReader.TargetId,
                    playerReader.TargetGuid, addonReader.TargetName);
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

    private void CombatLog_TargetEvade()
    {
        if (playerReader.TargetGuid != 0)
            evadeMobs.Add(playerReader.TargetGuid);
    }

    #region logging

    [LoggerMessage(
        EventId = 0060,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) is player or pet!")]
    static partial void LogPlayerOrPet(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0061,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) is tagged!")]
    static partial void LogTagged(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0062,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) too high level!")]
    static partial void LogLevelHigh(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0063,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) too low level!")]
    static partial void LogLevelLow(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0064,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) not yield experience!")]
    static partial void LogNoExperienceGain(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0065,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) name match!")]
    static partial void LogNameMatch(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0066,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name},{classification}) not defined in the TargetMask!")]
    static partial void LogClassification(ILogger logger, int id, int guid, string name, string classification);

    [LoggerMessage(
        EventId = 0067,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name},{classification}) evade on attack!")]
    static partial void LogEvade(ILogger logger, int id, int guid, string name, string classification);

    #endregion
}