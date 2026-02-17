using System;

namespace SharedLib;

public sealed class StartupClientVersion
{
    public ClientVersion Version { get; }

    public string Path { get; }

    public StartupClientVersion(Version v, string installPath = null)
    {
        // Priority 1: Anniversary Edition path detection - force to SoM regardless of version number
        string detectedPath = installPath ?? DetectAnniversaryPath();

        if (!string.IsNullOrEmpty(detectedPath) && detectedPath.Contains("_anniversary_"))
        {
            Version = ClientVersion.SoM;
            Path = "som";
            return;
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

            // --- Anniversary Edition (Classic Vanilla with Major=205) ---
            { Major: 205 } => (ClientVersion.SoM, "som"),

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