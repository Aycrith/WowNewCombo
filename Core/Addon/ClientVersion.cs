namespace Core
{
    public enum ClientVersion
    {
        None,
        Retail,
        SoM,
        TBC,
        Wrath
    }

    public static class ClientVersion_Extension
    {
        public static string ToStringF(this ClientVersion value) => value switch
        {
            ClientVersion.None => nameof(ClientVersion.None),
            ClientVersion.Retail => nameof(ClientVersion.Retail),
            ClientVersion.SoM => nameof(ClientVersion.SoM),
            ClientVersion.TBC => nameof(ClientVersion.TBC),
            ClientVersion.Wrath => nameof(ClientVersion.Wrath),
            _ => nameof(ClientVersion.None)
        };
    }
}
