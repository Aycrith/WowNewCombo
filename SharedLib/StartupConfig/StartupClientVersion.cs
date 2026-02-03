using System;

namespace SharedLib;

public sealed class StartupClientVersion
{
    public ClientVersion Version { get; }

    public string Path { get; }

    public StartupClientVersion(Version v)
    {
        // Debug logging to understand version detection
        Console.WriteLine($"[StartupClientVersion] Input version: {v} (Major={v?.Major}, Minor={v?.Minor}, Build={v?.Build}, Revision={v?.Revision})");
        
        if (v == null)
        {
            Console.WriteLine("[StartupClientVersion] WARNING: Version is null, defaulting to unknown");
            Version = ClientVersion.None;
            Path = "unknown";
            return;
        }
        
        (Version, Path) = v switch
        {
            // --- Classic branch ---
            { Major: 1, Minor: >= 13 } => (ClientVersion.SoM, "som"), // Vanilla / SoM
            { Major: 2, Minor: >= 5 } => (ClientVersion.TBC, "tbc"),  // TBC Classic
            { Major: 3, Minor: >= 4 } => (ClientVersion.Wrath, "wrath"),
            { Major: 4, Minor: >= 4 } => (ClientVersion.Cata, "cata"),

            // --- Legacy branch ---
            { Major: 1, Minor: <= 12 } => (ClientVersion.Legacy_Vanilla, "legacy_vanilla"),
            { Major: 2, Minor: <= 4 } => (ClientVersion.Legacy_TBC, "legacy_tbc"),
            { Major: 3, Minor: <= 3 } => (ClientVersion.Legacy_Wrath, "legacy_wrath"),
            { Major: 4, Minor: <= 3 } => (ClientVersion.Legacy_Cata, "legacy_cata"),
            { Major: 5, Minor: <= 4 } => (ClientVersion.Legacy_Mop, "legacy_mop"),

            // --- Retail fallback ---
            { Major: >= 9 } => (ClientVersion.Retail, "retail"),

            _ => (ClientVersion.None, "unknown")
        };
        
        Console.WriteLine($"[StartupClientVersion] Resolved to: {Version} -> path '{Path}'");
    }

}
