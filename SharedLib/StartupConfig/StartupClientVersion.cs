using System;

namespace SharedLib;

public sealed class StartupClientVersion
{
    public ClientVersion Version { get; }

    public string Path { get; }

    public StartupClientVersion(Version v, string installPath = null)
    {
        // Priority 1: Anniversary Edition path detection
        // Only force SoM when the WoW version is vanilla (Major <= 1) or unknown.
        // When Major >= 2 (TBC phase or later), fall through to version-based detection.
        string detectedPath = installPath ?? DetectAnniversaryPath();

        if (!string.IsNullOrEmpty(detectedPath) && detectedPath.Contains("_anniversary_"))
        {
            if (v == null || v.Major <= 1)
            {
                Version = ClientVersion.SoM;
                Path = "som";
                return;
            }
            // Anniversary server in TBC+ phase: fall through to version switch below
        }

        if (v == null)
        {
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

            // --- Anniversary Edition ---
            // Major=205 is Blizzard's internal versioning for Anniversary Edition.
            // As the anniversary server progresses through game phases:
            //   Phase 1-4 (Vanilla): uses "som" (Season of Mastery) database
            //   Phase 5+ (TBC content): uses "tbc" database which includes all vanilla zones
            // Since TBC is a superset of vanilla zones, "tbc" is safe for all anniversary phases.
            { Major: 205 } => (ClientVersion.TBC, "tbc"),

            // --- Retail fallback ---
            { Major: >= 9 } => (ClientVersion.Retail, "retail"),

            _ => (ClientVersion.None, "unknown")
        };
    }

    private static string DetectAnniversaryPath()
    {
        try
        {
            // Check Windows Registry for Anniversary Edition
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft");

            if (key != null)
            {
                var installPath = key.GetValue("InstallPath") as string;
                key.Close();

                if (!string.IsNullOrEmpty(installPath) && installPath.Contains("_anniversary_"))
                {
                    return installPath;
                }
            }
        }
        catch
        {
            // Ignore registry access errors
        }

        // Check common paths for Anniversary Edition
        string[] anniversaryPaths = {
            @"C:\Program Files (x86)\World of Warcraft\_anniversary_",
            @"C:\Program Files\World of Warcraft\_anniversary_",
            @"D:\World of Warcraft\_anniversary_",
            @"D:\Games\World of Warcraft\_anniversary_",
            @"E:\World of Warcraft\_anniversary_",
            @"E:\Games\World of Warcraft\_anniversary_"
        };

        foreach (var path in anniversaryPaths)
        {
            if (System.IO.Directory.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }
}