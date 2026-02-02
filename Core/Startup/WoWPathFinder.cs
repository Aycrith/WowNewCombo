using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace Core.Startup;

/// <summary>
/// Discovers WoW installations on the system without requiring a running WoW process.
/// Searches registry, Battle.net config, and common installation paths.
/// </summary>
public sealed class WoWPathFinder
{
    private readonly ILogger<WoWPathFinder> _logger;
    private readonly StartupOptions _options;

    // Known WoW executable names
    private static readonly string[] WoWExecutableNames =
    [
        "WowClassic.exe",
        "WowClassicT.exe",
        "WowClassicB.exe",
        "Wow.exe",
        "Wow-64.exe"
    ];

    // Common WoW installation paths to check
    private static readonly string[] CommonPaths =
    [
        @"C:\Program Files (x86)\World of Warcraft\_anniversary_",
        @"C:\Program Files (x86)\World of Warcraft\_classic_",
        @"C:\Program Files (x86)\World of Warcraft\_classic_era_",
        @"C:\Program Files\World of Warcraft\_anniversary_",
        @"C:\Program Files\World of Warcraft\_classic_",
        @"D:\World of Warcraft\_anniversary_",
        @"D:\World of Warcraft\_classic_",
        @"D:\Games\World of Warcraft\_anniversary_",
        @"D:\Games\World of Warcraft\_classic_",
        @"E:\World of Warcraft\_anniversary_",
        @"E:\World of Warcraft\_classic_",
        @"E:\Games\World of Warcraft\_anniversary_",
        @"E:\Games\World of Warcraft\_classic_"
    ];

    public WoWPathFinder(ILogger<WoWPathFinder> logger, IOptions<StartupOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Find a WoW installation using all available discovery methods.
    /// Returns the first valid installation found.
    /// </summary>
    public WoWInstallation? FindInstallation()
    {
        _logger.LogInformation("[WoWPathFinder] Searching for WoW installation...");

        // Priority 1: Explicit configuration
        if (!string.IsNullOrEmpty(_options.WoWPath))
        {
            _logger.LogDebug("[WoWPathFinder] Checking configured path: {Path}", _options.WoWPath);
            var install = ValidateAndCreateInstallation(_options.WoWPath);
            if (install != null)
            {
                _logger.LogInformation("[WoWPathFinder] Found WoW at configured path: {Path}", install.Path);
                return install;
            }
            _logger.LogWarning("[WoWPathFinder] Configured path is invalid: {Path}", _options.WoWPath);
        }

        // Priority 2: Registry
        var registryPath = FindFromRegistry();
        if (registryPath != null)
        {
            var install = ValidateAndCreateInstallation(registryPath);
            if (install != null)
            {
                _logger.LogInformation("[WoWPathFinder] Found WoW via registry: {Path}", install.Path);
                return install;
            }
        }

        // Priority 3: Battle.net config
        var battleNetPaths = FindFromBattleNetConfig();
        foreach (var path in battleNetPaths)
        {
            var install = ValidateAndCreateInstallation(path);
            if (install != null)
            {
                _logger.LogInformation("[WoWPathFinder] Found WoW via Battle.net config: {Path}", install.Path);
                return install;
            }
        }

        // Priority 4: Common paths
        foreach (var path in CommonPaths)
        {
            var install = ValidateAndCreateInstallation(path);
            if (install != null)
            {
                _logger.LogInformation("[WoWPathFinder] Found WoW at common path: {Path}", install.Path);
                return install;
            }
        }

        _logger.LogWarning("[WoWPathFinder] No WoW installation found");
        return null;
    }

    /// <summary>
    /// Find all WoW installations on the system.
    /// </summary>
    public List<WoWInstallation> FindAllInstallations()
    {
        var installations = new List<WoWInstallation>();
        var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check configured path
        if (!string.IsNullOrEmpty(_options.WoWPath) && checkedPaths.Add(_options.WoWPath))
        {
            var install = ValidateAndCreateInstallation(_options.WoWPath);
            if (install != null) installations.Add(install);
        }

        // Check registry
        var registryPath = FindFromRegistry();
        if (registryPath != null && checkedPaths.Add(registryPath))
        {
            var install = ValidateAndCreateInstallation(registryPath);
            if (install != null) installations.Add(install);
        }

        // Check Battle.net config
        foreach (var path in FindFromBattleNetConfig())
        {
            if (checkedPaths.Add(path))
            {
                var install = ValidateAndCreateInstallation(path);
                if (install != null) installations.Add(install);
            }
        }

        // Check common paths
        foreach (var path in CommonPaths)
        {
            if (checkedPaths.Add(path))
            {
                var install = ValidateAndCreateInstallation(path);
                if (install != null) installations.Add(install);
            }
        }

        return installations;
    }

    /// <summary>
    /// Validate a path and create a WoWInstallation if valid.
    /// </summary>
    public WoWInstallation? ValidateAndCreateInstallation(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return null;

        // Find WoW executable
        string? exePath = null;
        string? exeName = null;

        foreach (var name in WoWExecutableNames)
        {
            var fullPath = Path.Combine(path, name);
            if (File.Exists(fullPath))
            {
                exePath = fullPath;
                exeName = name;
                break;
            }
        }

        if (exePath == null || exeName == null)
            return null;

        // Get version info
        string version = "Unknown";
        try
        {
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            if (versionInfo.FileVersion != null)
                version = versionInfo.FileVersion;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[WoWPathFinder] Could not read version info: {Error}", ex.Message);
        }

        // Check for DataToColor addon
        var addonsPath = Path.Combine(path, "Interface", "AddOns");
        var dataToColorPath = Path.Combine(addonsPath, "DataToColor");
        bool hasDataToColor = Directory.Exists(dataToColorPath) &&
            (File.Exists(Path.Combine(dataToColorPath, "DataToColor.toc")) ||
             File.Exists(Path.Combine(dataToColorPath, "DataToColor_TBC.toc")));

        // Check for SecureButtons.xml (our fix)
        bool hasSecureButtons = File.Exists(Path.Combine(dataToColorPath, "SecureButtons.xml"));

        return new WoWInstallation(
            Path: path,
            ExecutablePath: exePath,
            ExecutableName: exeName,
            Version: version,
            HasDataToColorAddon: hasDataToColor,
            HasSecureButtonsXml: hasSecureButtons
        );
    }

    /// <summary>
    /// Search Windows Registry for WoW installation path.
    /// </summary>
    private string? FindFromRegistry()
    {
        try
        {
            // Blizzard's registry key
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft");

            if (key != null)
            {
                var installPath = key.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(installPath))
                {
                    _logger.LogDebug("[WoWPathFinder] Found registry path: {Path}", installPath);
                    return installPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[WoWPathFinder] Registry search failed: {Error}", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Parse Battle.net config file to find WoW installation paths.
    /// </summary>
    private List<string> FindFromBattleNetConfig()
    {
        var paths = new List<string>();

        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = Path.Combine(appDataPath, "Battle.net", "Battle.net.config");

            if (!File.Exists(configPath))
            {
                _logger.LogDebug("[WoWPathFinder] Battle.net config not found at: {Path}", configPath);
                return paths;
            }

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);

            // Look for WoW installation paths in the config
            if (doc.RootElement.TryGetProperty("Games", out var games))
            {
                // Check for WoW Classic variants
                var gameKeys = new[] { "wow_classic", "wow_classic_era", "wow_classic_ptr" };

                foreach (var gameKey in gameKeys)
                {
                    if (games.TryGetProperty(gameKey, out var game))
                    {
                        if (game.TryGetProperty("AdditionalLaunchArguments", out var args))
                        {
                            // The path is sometimes in launch args
                        }

                        if (game.TryGetProperty("DefaultInstallPath", out var installPath))
                        {
                            var pathStr = installPath.GetString();
                            if (!string.IsNullOrEmpty(pathStr))
                            {
                                _logger.LogDebug("[WoWPathFinder] Found Battle.net path for {Game}: {Path}", gameKey, pathStr);
                                paths.Add(pathStr);
                            }
                        }
                    }
                }
            }

            // Also check Client.Install paths
            if (doc.RootElement.TryGetProperty("Client", out var client))
            {
                if (client.TryGetProperty("Install", out var install))
                {
                    if (install.TryGetProperty("DefaultInstallPath", out var defaultPath))
                    {
                        var pathStr = defaultPath.GetString();
                        if (!string.IsNullOrEmpty(pathStr))
                        {
                            // This is the base Blizzard install path, WoW would be in a subfolder
                            var wowPath = Path.Combine(pathStr, "World of Warcraft", "_anniversary_");
                            if (Directory.Exists(wowPath))
                                paths.Add(wowPath);

                            wowPath = Path.Combine(pathStr, "World of Warcraft", "_classic_");
                            if (Directory.Exists(wowPath))
                                paths.Add(wowPath);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("[WoWPathFinder] Battle.net config parse failed: {Error}", ex.Message);
        }

        return paths;
    }
}
