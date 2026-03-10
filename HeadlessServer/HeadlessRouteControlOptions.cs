namespace HeadlessServer;

public sealed class HeadlessRouteControlOptions
{
    public const string SectionName = "HeadlessControl";

    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5001;
}
