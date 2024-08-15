using System;

namespace Core;

public sealed class SessionStat
{
    public int Deaths { get; set; }
    public int Kills { get; set; }

    public DateTime StartTime { get; set; }

    public int _Deaths() => Deaths;

    public int _Kills() => Kills;

    public int Seconds => (int)(DateTime.UtcNow - StartTime).TotalSeconds;

    public int _Seconds() => Seconds;

    public int Minutes => (int)(DateTime.UtcNow - StartTime).TotalMinutes;

    public int _Minutes() => Minutes;

    public int Hours => (int)(DateTime.UtcNow - StartTime).TotalHours;

    public int _Hours() => Hours;

    public void Reset()
    {
        Deaths = 0;
        Kills = 0;
    }

    public void Start()
    {
        StartTime = DateTime.UtcNow;
    }
}
