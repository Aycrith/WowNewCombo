using Core.Goals;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Numerics;
using Game;

namespace Core;

public sealed partial class KeyAction
{
    public float Cost { get; set; } = 18;
    public string Name { get; set; } = string.Empty;
    public bool HasCastBar { get; set; }
    public ConsoleKey ConsoleKey { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Slot { get; set; }
    public int SlotIndex { get; private set; }
    public int SpellId { get; set; }

    public int PressDuration { get; set; } = InputDuration.DefaultPress;

    public Form? Form { get; init; }
    public Form FormValue => HasForm ? Form!.Value : Core.Form.None;
    public bool HasForm => Form.HasValue;

    public int Cooldown { get; set; } = CastingHandler.SPELL_QUEUE;

    private int _charge;
    public int Charge { get; set; } = 1;
    public SchoolMask School { get; set; } = SchoolMask.None;
    public int MinComboPoints { get; set; }

    public string Requirement { get; set; } = string.Empty;
    public List<string> Requirements { get; } = new();
    public Requirement[] RequirementsRuntime { get; set; } = Array.Empty<Requirement>();

    public string Interrupt { get; set; } = string.Empty;
    public List<string> Interrupts { get; } = new();
    public Requirement[] InterruptsRuntime { get; set; } = Array.Empty<Requirement>();

    public bool WhenUsable { get; set; }

    public bool ResetOnNewTarget { get; set; }

    public bool Log { get; set; } = true;

    public bool BaseAction { get; set; }

    public bool Item { get; set; }

    public int BeforeCastDelay { get; set; }
    public int BeforeCastMaxDelay { get; set; }

    public bool BeforeCastStop { get; set; }
    public bool BeforeCastDismount { get; set; } = true;

    public int AfterCastDelay { get; set; }
    public int AfterCastMaxDelay { get; set; }
    public bool AfterCastWaitMeleeRange { get; set; }
    public bool AfterCastWaitBuff { get; set; }
    public bool AfterCastWaitBag { get; set; }
    public bool AfterCastWaitSwing { get; set; }
    public bool AfterCastWaitCastbar { get; set; }
    public bool AfterCastWaitCombat { get; set; }
    public bool AfterCastWaitGCD { get; set; }
    public bool AfterCastAuraExpected { get; set; }
    public int AfterCastStepBack { get; set; }

    public string InCombat { get; set; } = "false";

    public bool? UseWhenTargetIsCasting { get; set; }

    public string PathFilename { get; set; } = string.Empty;
    public Vector3[] Path { get; set; } = Array.Empty<Vector3>();

    public int ConsoleKeyFormHash { private set; get; }

    private DateTime LastClicked = DateTime.UtcNow.AddDays(-1);

    private static int LastKey;
    private static DateTime LastKeyTime;

    public static int LastKeyClicked()
    {
        return (DateTime.UtcNow - LastKeyTime).TotalSeconds > 2
            ? (int)ConsoleKey.NoName
            : LastKey;
    }

    private RecordInt globalTime = null!;
    private int canRunTime;
    private bool canRun;

    private int canBeInterruptedTime;
    private bool canBeInterrupted;

    public void InitSlot(ILogger logger)
    {
        if (!KeyReader.ReadKey(logger, this) && !BaseAction)
        {
            LogInputNoValidKey(logger, Name, Key, ConsoleKey);
        }
        else if (Slot == 0)
        {
            LogInputNonActionbar(logger, Name, Key, ConsoleKey);
        }
    }

    public void Init(
        ILogger logger, bool globalLog,
        PlayerReader playerReader,
        RecordInt globalTime)
    {
        this.globalTime = globalTime;
        this.canRunTime = globalTime.Value;
        this.canBeInterruptedTime = globalTime.Value;

        if (!globalLog)
            Log = false;

        ResetCharges();

        InitSlot(logger);
        if (Slot > 0)
        {
            this.SlotIndex = Stance.ToSlot(this, playerReader) - 1;
            LogInputActionbar(logger, Name, Key, Slot, SlotIndex);
        }

        if (HasForm)
        {
            LogFormRequired(logger, Name, FormValue.ToStringF());
        }

        ConsoleKeyFormHash = ((int)FormValue * 1000) + (int)ConsoleKey;

        if (!string.IsNullOrEmpty(Requirement))
        {
            Requirements.Add(Requirement);
        }

        if (!string.IsNullOrEmpty(Interrupt))
        {
            Interrupts.Add(Interrupt);
        }

        if (!string.IsNullOrEmpty(PathFilename))
        {
            LogPath(logger, Name, PathFilename);
        }

        if (BeforeCastMaxDelay < BeforeCastDelay)
            BeforeCastMaxDelay = BeforeCastDelay;

        if (AfterCastMaxDelay < AfterCastDelay)
            AfterCastMaxDelay = AfterCastDelay;
    }

    public int GetRemainingCooldown()
    {
        return Math.Max(Cooldown - SinceLastClickMs, 0);
    }

    public void SetClicked(double offset = 0)
    {
        LastKey = ConsoleKeyFormHash;
        LastKeyTime = LastClicked = DateTime.UtcNow.AddMilliseconds(offset);
    }

    public int SinceLastClickMs =>
        (int)(DateTime.UtcNow - LastClicked).TotalMilliseconds;

    public void ResetCooldown()
    {
        LastClicked = DateTime.UtcNow.AddDays(-1);
    }

    public int GetChargeRemaining()
    {
        return _charge;
    }

    public void ConsumeCharge()
    {
        if (Charge <= 1)
            return;

        _charge--;
        if (_charge > 0)
        {
            ResetCooldown();
        }
        else
        {
            ResetCharges();
            SetClicked();
        }
    }

    public void ResetCharges()
    {
        _charge = Charge;
    }

    public bool CanRun()
    {
        if (canRunTime == globalTime.Value)
            return canRun;

        canRunTime = globalTime.Value;

        ReadOnlySpan<Requirement> span = RequirementsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return canRun = false;
        }

        return canRun = true;
    }

    public bool CanBeInterrupted()
    {
        if (canBeInterruptedTime == globalTime.Value)
            return canBeInterrupted;

        canBeInterruptedTime = globalTime.Value;

        ReadOnlySpan<Requirement> span = InterruptsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return canBeInterrupted = false;
        }

        return canBeInterrupted = true;
    }

    #region Logging

    [LoggerMessage(
        EventId = 0001,
        Level = LogLevel.Information,
        Message = "[{name,-15}] Path: {path}")]
    static partial void LogPath(ILogger logger, string name, string path);

    [LoggerMessage(
        EventId = 0002,
        Level = LogLevel.Information,
        Message = "[{name,-15}] Required Form: {form}")]
    static partial void LogFormRequired(ILogger logger, string name, string form);

    [LoggerMessage(
        EventId = 0003,
        Level = LogLevel.Information,
        Message = "[{name,-15}] Actionbar Key:{key} -> Actionbar:{slot} -> Index:{slotIndex}")]
    static partial void LogInputActionbar(ILogger logger, string name, string key, int slot, int slotIndex);

    [LoggerMessage(
        EventId = 0004,
        Level = LogLevel.Information,
        Message = "[{name,-15}] Non Actionbar {key} -> {consoleKey}")]
    static partial void LogInputNonActionbar(ILogger logger, string name, string key, ConsoleKey consoleKey);

    [LoggerMessage(
        EventId = 0005,
        Level = LogLevel.Warning,
        Message = "[{name,-15}] has no valid Key={key} or ConsoleKey={consoleKey}")]
    static partial void LogInputNoValidKey(ILogger logger, string name, string key, ConsoleKey consoleKey);

    [LoggerMessage(
        EventId = 0006,
        Level = LogLevel.Information,
        Message = "[{name,-15}] Update {type} cost to {newCost} from {oldCost}")]
    static partial void LogPowerCostChange(ILogger logger, string name, string type, int newCost, int oldCost);

    #endregion
}