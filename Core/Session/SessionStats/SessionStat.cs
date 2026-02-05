using System;

using static System.Diagnostics.Stopwatch;


namespace Core;

public sealed class SessionStat
{
    private const int RecentlyBandagedMs = 60_000;
    private const int RecentlyBadAttackPositionMs = 2_000;

    public int Deaths { get; set; }
    public int Kills { get; set; }

    public long StartTime { get; set; }

    /// <summary>
    /// Set to true when vendor/repair (AdhocNPCGoal) completes successfully.
    /// Cleared when MailGoal completes successfully.
    /// Used to ensure Mail only runs after Vendor/Repair.
    /// </summary>
    public bool VendoredOrRepairedRecently { get; set; }

    private long lastBandageTime;
    private long lastBadAttackPositionTime;

    public int _Deaths() => Deaths;

    public int _Kills() => Kills;

    public int Seconds => (int)GetElapsedTime(StartTime).TotalSeconds;

    public int _Seconds() => Seconds;

    public int Minutes => (int)GetElapsedTime(StartTime).TotalMinutes;

    public int _Minutes() => Minutes;

    public int Hours => (int)GetElapsedTime(StartTime).TotalHours;

    public int _Hours() => Hours;

    public bool _VendoredOrRepairedRecently() => VendoredOrRepairedRecently;

    public bool _RecentlyBandaged()
    {
        if (lastBandageTime == 0)
        {
            return false;
        }

        return GetElapsedTime(lastBandageTime).TotalMilliseconds < RecentlyBandagedMs;
    }

    public void MarkBandaged()
    {
        lastBandageTime = GetTimestamp();
    }

    public bool _RecentlyBadAttackPosition()
    {
        if (lastBadAttackPositionTime == 0)
        {
            return false;
        }

        return GetElapsedTime(lastBadAttackPositionTime).TotalMilliseconds < RecentlyBadAttackPositionMs;
    }

    public void MarkBadAttackPosition()
    {
        lastBadAttackPositionTime = GetTimestamp();
    }

    public void Reset()
    {
        Deaths = 0;
        Kills = 0;
        VendoredOrRepairedRecently = false;
        lastBandageTime = 0;
        lastBadAttackPositionTime = 0;
    }

    public void Start()
    {
        StartTime = GetTimestamp();
    }
}
