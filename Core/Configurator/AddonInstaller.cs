using Game;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core;

/// <summary>
/// Handles automatic addon installation, updates, and AddOns.txt management.
/// Ensures addons are properly deployed to WoW and enabled for all characters.
/// </summary>
public sealed class AddonInstaller
{
    private readonly ILogger<AddonInstaller> logger;
    private readonly WowProcess process;
    private readonly AddonConfigurator addonConfigurator;

    private const string AddonsSourcePath = @".\Addons\";

    private string WowPath => process.Path;
    private string AddonsBasePath => Path.Join(WowPath, "Interface", "AddOns");
    private string WtfPath => Path.Join(WowPath, "WTF", "Account");

    public AddonInstaller(
        ILogger<AddonInstaller> logger,
        WowProcess process,
        AddonConfigurator addonConfigurator)
    {
        this.logger = logger;
        this.process = process;
        this.addonConfigurator = addonConfigurator;
    }

    /// <summary>
    /// Ensures the addon is installed and enabled for all characters.
    /// </summary>
    /// <returns>True if successful, false if there were errors.</returns>
    public bool EnsureAddonInstalled()
    {
        try
        {
            // 1. Check if addon needs to be installed/updated
            bool needsInstall = !addonConfigurator.Installed();
            bool needsUpdate = addonConfigurator.UpdateAvailable();

            if (needsInstall)
            {
                logger.LogInformation("Addon not installed, installing...");
                if (!InstallAddon())
                    return false;
            }
            else if (needsUpdate)
            {
                logger.LogInformation("Addon update available, updating...");
                if (!InstallAddon())
                    return false;
            }

            // 2. Enable addon for all characters
            string addonName = addonConfigurator.Config.Title;
            if (!string.IsNullOrEmpty(addonName))
            {
                EnableAddonForAllCharacters(addonName);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during addon installation");
            return false;
        }
    }

    /// <summary>
    /// Installs or updates the addon using the AddonConfigurator.
    /// </summary>
    private bool InstallAddon()
    {
        try
        {
            if (!addonConfigurator.Validate())
            {
                logger.LogError("Addon configuration validation failed");
                return false;
            }

            addonConfigurator.Install();
            addonConfigurator.Save();
            
            logger.LogInformation("Addon installed successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install addon");
            return false;
        }
    }

    /// <summary>
    /// Enables the specified addon for all characters in AddOns.txt files.
    /// </summary>
    public void EnableAddonForAllCharacters(string addonName)
    {
        if (!Directory.Exists(WtfPath))
        {
            logger.LogWarning("WTF folder not found, cannot update AddOns.txt files");
            return;
        }

        var addOnsTxtFiles = FindAddOnsTxtFiles();
        if (addOnsTxtFiles.Count == 0)
        {
            logger.LogInformation("No AddOns.txt files found (no characters created yet)");
            return;
        }

        int updatedCount = 0;
        foreach (string file in addOnsTxtFiles)
        {
            if (EnableAddonInFile(file, addonName))
            {
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            logger.LogInformation("Enabled {AddonName} for {Count} character(s)", addonName, updatedCount);
        }
    }

    /// <summary>
    /// Enables an addon in a specific AddOns.txt file.
    /// </summary>
    /// <returns>True if the file was modified.</returns>
    private bool EnableAddonInFile(string filePath, string addonName)
    {
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            bool modified = false;
            bool found = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                string lineAddonName = line[..colonIndex].Trim();
                if (lineAddonName.Equals(addonName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    string state = line[(colonIndex + 1)..].Trim().ToLowerInvariant();
                    if (state != "enabled")
                    {
                        lines[i] = $"{addonName}: enabled";
                        modified = true;
                    }
                    break;
                }
            }

            if (!found)
            {
                // Add the addon entry
                lines.Add($"{addonName}: enabled");
                modified = true;
            }

            if (modified)
            {
                File.WriteAllLines(filePath, lines);
                string relativePath = Path.GetRelativePath(WtfPath, filePath);
                logger.LogDebug("Updated {FilePath} - enabled {AddonName}", relativePath, addonName);
            }

            return modified;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Disables an addon for all characters (e.g., legacy BindPad addon).
    /// </summary>
    public void DisableAddonForAllCharacters(string addonName)
    {
        if (!Directory.Exists(WtfPath))
            return;

        var addOnsTxtFiles = FindAddOnsTxtFiles();
        foreach (string file in addOnsTxtFiles)
        {
            DisableAddonInFile(file, addonName);
        }
    }

    private void DisableAddonInFile(string filePath, string addonName)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            bool modified = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                string lineAddonName = line[..colonIndex].Trim();
                if (lineAddonName.Equals(addonName, StringComparison.OrdinalIgnoreCase))
                {
                    string state = line[(colonIndex + 1)..].Trim().ToLowerInvariant();
                    if (state == "enabled")
                    {
                        lines[i] = $"{addonName}: disabled";
                        modified = true;
                    }
                    break;
                }
            }

            if (modified)
            {
                File.WriteAllLines(filePath, lines);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Removes broken symlinks from the AddOns folder.
    /// </summary>
    /// <returns>Number of broken symlinks removed.</returns>
    public int CleanupBrokenSymlinks()
    {
        if (!Directory.Exists(AddonsBasePath))
            return 0;

        int removed = 0;
        try
        {
            foreach (string dir in Directory.GetDirectories(AddonsBasePath))
            {
                if (IsDirectoryBrokenSymlink(dir))
                {
                    string dirName = Path.GetFileName(dir);
                    logger.LogInformation("Removing broken symlink: {DirName}", dirName);
                    
                    try
                    {
                        // For symlinks/junctions, we need to delete the directory entry itself
                        Directory.Delete(dir, false);
                        removed++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not remove broken symlink: {DirName}", dirName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error scanning for broken symlinks");
        }

        return removed;
    }

    /// <summary>
    /// Removes the legacy BindPad addon folder if it exists.
    /// Since SecureButtons.xml is now part of DataToColor, the separate addon is no longer needed.
    /// </summary>
    /// <returns>True if the addon was removed.</returns>
    public bool RemoveLegacyBindPadAddon()
    {
        string bindPadPath = Path.Join(AddonsBasePath, "BindPad");
        
        if (!Directory.Exists(bindPadPath))
            return false;

        // Check if this is the minimal version (safe to remove)
        string tocPath = Path.Join(bindPadPath, "BindPad.toc");
        if (File.Exists(tocPath))
        {
            try
            {
                string tocContent = File.ReadAllText(tocPath);
                
                // Only remove if it's our minimal version
                if (tocContent.Contains("Minimal BindPad") || 
                    tocContent.Contains("WowGrindBot") ||
                    tocContent.Contains("DataToColor"))
                {
                    logger.LogInformation("Removing legacy BindPad addon (now integrated into DataToColor)");
                    Directory.Delete(bindPadPath, true);
                    
                    // Also disable it in AddOns.txt
                    DisableAddonForAllCharacters("BindPad");
                    
                    return true;
                }
                else
                {
                    logger.LogInformation("Found third-party BindPad addon - keeping it");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not check BindPad addon");
            }
        }

        return false;
    }

    /// <summary>
    /// Performs full addon maintenance: install, cleanup, enable.
    /// </summary>
    public AddonMaintenanceResult PerformMaintenance()
    {
        var result = new AddonMaintenanceResult();

        try
        {
            // 1. Cleanup broken symlinks
            result.BrokenSymlinksRemoved = CleanupBrokenSymlinks();

            // 2. Install/update addon if needed
            result.AddonInstalled = EnsureAddonInstalled();

            // 3. Remove legacy BindPad if present
            result.LegacyBindPadRemoved = RemoveLegacyBindPadAddon();

            result.Success = result.AddonInstalled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Addon maintenance failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private List<string> FindAddOnsTxtFiles()
    {
        var files = new List<string>();

        if (!Directory.Exists(WtfPath))
            return files;

        try
        {
            // Pattern: WTF/Account/{AccountName}/{ServerName}/{CharacterName}/AddOns.txt
            foreach (string accountDir in Directory.GetDirectories(WtfPath))
            {
                foreach (string serverDir in Directory.GetDirectories(accountDir))
                {
                    // Skip SavedVariables folder
                    if (Path.GetFileName(serverDir).Equals("SavedVariables", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (string charDir in Directory.GetDirectories(serverDir))
                    {
                        string addOnsTxt = Path.Join(charDir, "AddOns.txt");
                        if (File.Exists(addOnsTxt))
                        {
                            files.Add(addOnsTxt);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for AddOns.txt files");
        }

        return files;
    }

    private static bool IsDirectoryBrokenSymlink(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);

            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                try
                {
                    _ = Directory.GetFiles(path);
                    return false;
                }
                catch (DirectoryNotFoundException)
                {
                    return true;
                }
                catch (IOException)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>
/// Result of addon maintenance operations.
/// </summary>
public sealed class AddonMaintenanceResult
{
    public bool Success { get; set; }
    public bool AddonInstalled { get; set; }
    public bool LegacyBindPadRemoved { get; set; }
    public int BrokenSymlinksRemoved { get; set; }
    public string? ErrorMessage { get; set; }

    public string GetSummary()
    {
        if (!Success)
            return $"Maintenance failed: {ErrorMessage ?? "Unknown error"}";

        var parts = new List<string>();
        
        if (AddonInstalled)
            parts.Add("Addon ready");
        if (LegacyBindPadRemoved)
            parts.Add("Legacy BindPad removed");
        if (BrokenSymlinksRemoved > 0)
            parts.Add($"{BrokenSymlinksRemoved} broken symlink(s) cleaned");

        return parts.Count > 0 
            ? string.Join(", ", parts) 
            : "No maintenance needed";
    }
}
