namespace Core;

public static class BotRuntimeModeHelper
{
    public const string Live = "live";
    public const string Configuration = "configuration";
    public const string Unknown = "unknown";

    public static string GetRuntimeMode(IBotController botController)
    {
        return botController switch
        {
            BotController => Live,
            ConfigBotController => Configuration,
            _ when botController.GoapAgent != null || botController.IsBotActive => Live,
            _ => Unknown
        };
    }
}
