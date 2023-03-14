namespace Core;

public sealed class SessionStat
{
    public int Deaths { get; set; }
    public int Kills { get; set; }

    public int _Deaths() => Deaths;

    public int _Kills() => Kills;

    public void Reset()
    {
        Deaths = 0;
        Kills = 0;
    }

}
