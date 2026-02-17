using Game;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Core;

/// <summary>
/// Pre-flight validator for WoW addon installation.
/// Checks addon files, AddOns.txt entries, and detects common issues
/// like broken symlinks before the bot attempts to start.
/// </summary>
public sealed partial class AddonValidator
{
    private readonly ILogger<AddonValidator> logger;
    private readonly WowProcess process;
    private readonly AddonConfigurator addonConfigurator;

    private string WowPath => process.Path;
    private string AddonsBasePath => Path.Join(WowPath, "Interface", "AddOns");
    private string WtfPath => Path.Join(WowPath, "WTF", "Account");

    // Required addons for the bot to function
    // Note: BindPad was previously required but is now integrated into DataToColor via SecureButtons.xml
    private static readonly string[] RequiredAddons = Array.Empty<string>();  // Empty - all functionality now in DataToColor

    public AddonValidator(
        ILogger<AddonValidator> logger,
        WowProcess process,
        AddonConfigurator addonConfigurator)
    {
        this.logger = logger;
        this.process = process;
        this.addonConfigurator = addonConfigurator;
    }

    /// <summary>
    /// Performs all validation checks and returns a comprehensive result.
    /// </summary>
    public AddonValidationResult Validate()
    {
        var result = new AddonValidationResult();

        try
        {
            // Check WoW path exists
            if (!Directory.Exists(WowPath))
            {
                result.AddError("WoW path not found", $"Path does not exist: {WowPath}");
                return result;
            }

            // Check AddOns folder exists
            if (!Directory.Exists(AddonsBasePath))
            {
                result.AddError("AddOns folder missing", $"Interface/AddOns folder not found at: {AddonsBasePath}");
                return result;
            }

            // Validate main DataToColor addon
            ValidateDataToColorAddon(result);

            // Validate required dependency addons
            ValidateRequiredAddons(result);

            // Check for broken symlinks in AddOns folder
            CheckForBrokenSymlinks(result);

            // Validate AddOns.txt entries
            ValidateAddOnsTxt(result);
        }
        catch (Exception ex)
        {
            result.AddError("Validation exception", ex.Message);
            logger.LogError(ex, "Exception during addon validation");
        }

        LogValidationResult(result);
        return result;
    }

    private void ValidateDataToColorAddon(AddonValidationResult result)
    {
        string addonTitle = addonConfigurator.Config.Title;
        string addonPath = addonConfigurator.FinalAddonPath;

        if (string.IsNullOrEmpty(addonTitle))
        {
            result.AddError("DataToColor not configured",
                "Addon configuration is missing. Please configure the addon first.");
            return;
        }

        if (!Directory.Exists(addonPath))
        {
            result.AddError($"Addon folder missing: {addonTitle}",
                $"Expected at: {addonPath}. Please install the addon.");
            return;
        }

        // Check for main .toc file
        string tocPath = Path.Join(addonPath, $"{addonTitle}.toc");
        if (!File.Exists(tocPath))
        {
            result.AddError($"Missing TOC file: {addonTitle}.toc",
                $"The addon folder exists but is missing the main .toc file.");
            return;
        }

        // Check for main .lua file
        string luaPath = Path.Join(addonPath, $"{addonTitle}.lua");
        if (!File.Exists(luaPath))
        {
            result.AddError($"Missing LUA file: {addonTitle}.lua",
                $"The addon folder exists but is missing the main .lua file.");
            return;
        }

        // Check for SecureButtons.xml (creates BindPadMacro button)
        string secureButtonsPath = Path.Join(addonPath, "SecureButtons.xml");
        if (!File.Exists(secureButtonsPath))
        {
            result.AddWarning($"Missing SecureButtons.xml",
                $"The addon may be an older version. SecureButtons.xml provides the BindPadMacro button. " +
                $"Update to v1.9.3+ or ensure BindPad addon is installed as fallback.");
        }
        else
        {
            result.AddSuccess("SecureButtons.xml found (provides BindPadMacro)");
        }

        // Check version
        Version? version = addonConfigurator.GetInstallVersion();
        if (version == null)
        {
            result.AddWarning($"Cannot read version from {addonTitle}",
                "The TOC file may be malformed.");
        }
        else
        {
            result.AddSuccess($"DataToColor addon: {addonTitle} v{version}");
        }
    }

    private void ValidateRequiredAddons(AddonValidationResult result)
    {
        foreach (string addonName in RequiredAddons)
        {
            string addonPath = Path.Join(AddonsBasePath, addonName);

            if (!Directory.Exists(addonPath))
            {
                result.AddError($"Required addon missing: {addonName}",
                    $"The {addonName} addon is required for the bot to function. " +
                    $"Expected at: {addonPath}");
                continue;
            }

            // Check if it's a broken symlink (exists as directory entry but not accessible)
            if (IsDirectoryBrokenSymlink(addonPath))
            {
                result.AddError($"Broken symlink: {addonName}",
                    $"The {addonName} folder is a broken symbolic link pointing to a non-existent location. " +
                    "Delete it and reinstall the addon.");
                continue;
            }

            // Check for TOC file
            string tocPath = Path.Join(addonPath, $"{addonName}.toc");
            if (!File.Exists(tocPath))
            {
                result.AddError($"Missing TOC: {addonName}.toc",
                    $"The {addonName} folder exists but is missing the .toc file. " +
                    "The addon may be corrupted or incomplete.");
                continue;
            }

            // Check for XML file (BindPad specific)
            if (addonName == "BindPad")
            {
                string xmlPath = Path.Join(addonPath, $"{addonName}.xml");
                if (!File.Exists(xmlPath))
                {
                    result.AddError($"Missing XML: {addonName}.xml",
                        $"The {addonName} folder exists but is missing the .xml file. " +
                        "This file creates the required BindPadMacro button.");
                    continue;
                }
            }

            result.AddSuccess($"Required addon found: {addonName}");
        }
    }

    private void CheckForBrokenSymlinks(AddonValidationResult result)
    {
        if (!Directory.Exists(AddonsBasePath))
            return;

        try
        {
            // Get all directories including potential symlinks
            foreach (string dir in Directory.GetDirectories(AddonsBasePath))
            {
                string dirName = Path.GetFileName(dir);

                // Skip disabled addons (common pattern)
                if (dirName.EndsWith("_DISABLED", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsDirectoryBrokenSymlink(dir))
                    {
                        result.AddWarning($"Broken disabled symlink: {dirName}",
                            $"The disabled addon folder '{dirName}' is a broken symbolic link. " +
                            "Consider removing it.");
                    }
                    continue;
                }

                if (IsDirectoryBrokenSymlink(dir))
                {
                    result.AddError($"Broken symlink detected: {dirName}",
                        $"The folder '{dirName}' is a broken symbolic link. " +
                        "This will cause WoW to fail loading addons. Please remove it.");
                }
            }
        }
        catch (Exception ex)
        {
            result.AddWarning("Could not scan for broken symlinks", ex.Message);
        }
    }

    private void ValidateAddOnsTxt(AddonValidationResult result)
    {
        if (!Directory.Exists(WtfPath))
        {
            result.AddWarning("WTF folder not found",
                "Cannot validate AddOns.txt - WTF folder doesn't exist. " +
                "This may be normal if WoW hasn't been run yet.");
            return;
        }

        string addonTitle = addonConfigurator.Config.Title;
        var addOnsTxtFiles = FindAddOnsTxtFiles();

        if (addOnsTxtFiles.Count == 0)
        {
            result.AddWarning("No AddOns.txt files found",
                "Cannot validate addon enablement. Run WoW at least once to create these files.");
            return;
        }

        foreach (string file in addOnsTxtFiles)
        {
            ValidateSingleAddOnsTxt(file, addonTitle, result);
        }
    }

    private void ValidateSingleAddOnsTxt(string filePath, string addonTitle, AddonValidationResult result)
    {
        try
        {
            string relativePath = Path.GetRelativePath(WtfPath, filePath);
            var lines = File.ReadAllLines(filePath);
            var addonStates = ParseAddOnsTxt(lines);

            // Check DataToColor addon
            if (!string.IsNullOrEmpty(addonTitle))
            {
                if (!addonStates.TryGetValue(addonTitle, out bool enabled))
                {
                    result.AddWarning($"{relativePath}: {addonTitle} not listed",
                        "The main addon isn't in AddOns.txt. Enable it in WoW's addon menu.");
                }
                else if (!enabled)
                {
                    result.AddWarning($"{relativePath}: {addonTitle} disabled",
                        "The main addon is disabled. Enable it in WoW's addon menu.");
                }
            }

            // Check required addons
            foreach (string required in RequiredAddons)
            {
                if (!addonStates.TryGetValue(required, out bool enabled))
                {
                    result.AddWarning($"{relativePath}: {required} not listed",
                        $"Required addon {required} isn't in AddOns.txt. Enable it in WoW's addon menu.");
                }
                else if (!enabled)
                {
                    result.AddError($"{relativePath}: {required} disabled",
                        $"Required addon {required} is disabled! Enable it in WoW's addon menu, then /reload.");
                }
            }

            // NOTE:
            // We intentionally do NOT warn about arbitrary enabled add-ons that are missing their folders.
            // AddOns.txt often contains stale entries across alts/realms, and these warnings were flooding
            // the launch wizard and making "Add-ons" appear errored even though the bot add-on is fine.
            // We only validate DataToColor + explicitly required add-ons above.
        }
        catch (Exception ex)
        {
            result.AddWarning($"Could not parse {Path.GetFileName(filePath)}", ex.Message);
        }
    }

    private static Dictionary<string, bool> ParseAddOnsTxt(string[] lines)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Format: "AddonName: enabled" or "AddonName: disabled"
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                string addonName = trimmed[..colonIndex].Trim();
                string state = trimmed[(colonIndex + 1)..].Trim().ToLowerInvariant();
                result[addonName] = state == "enabled";
            }
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
            // Search for AddOns.txt in all character folders
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

    /// <summary>
    /// Detects if a directory path is a broken symbolic link.
    /// </summary>
    private static bool IsDirectoryBrokenSymlink(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);

            // Check if it's a reparse point (symlink/junction)
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                // Try to access the directory - if it fails, it's broken
                try
                {
                    _ = Directory.GetFiles(path);
                    return false; // Accessible, not broken
                }
                catch (DirectoryNotFoundException)
                {
                    return true; // Broken symlink
                }
                catch (IOException)
                {
                    return true; // Broken symlink
                }
            }

            return false; // Not a symlink
        }
        catch
        {
            // If we can't even get attributes, consider it problematic
            return true;
        }
    }

    private void LogValidationResult(AddonValidationResult result)
    {
        if (result.IsValid)
        {
            logger.LogInformation("Addon validation passed with {SuccessCount} checks",
                result.Successes.Count);
        }
        else
        {
            logger.LogWarning("Addon validation found {ErrorCount} errors and {WarningCount} warnings",
                result.Errors.Count, result.Warnings.Count);
        }

        foreach (var error in result.Errors)
        {
            logger.LogError("Addon Error: {Title} - {Description}", error.Title, error.Description);
        }

        foreach (var warning in result.Warnings)
        {
            logger.LogWarning("Addon Warning: {Title} - {Description}", warning.Title, warning.Description);
        }
    }

    [GeneratedRegex(@"^## Interface: (\d+)$", RegexOptions.Multiline)]
    private static partial Regex RegexInterfaceVersion();
}

/// <summary>
/// Result of addon validation containing errors, warnings, and success messages.
/// </summary>
public sealed class AddonValidationResult
{
    public List<ValidationMessage> Errors { get; } = new();
    public List<ValidationMessage> Warnings { get; } = new();
    public List<string> Successes { get; } = new();

    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;

    public void AddError(string title, string description)
    {
        Errors.Add(new ValidationMessage(title, description));
    }

    public void AddWarning(string title, string description)
    {
        Warnings.Add(new ValidationMessage(title, description));
    }

    public void AddSuccess(string message)
    {
        Successes.Add(message);
    }

    /// <summary>
    /// Gets a summary suitable for display in UI.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid && !HasWarnings)
            return $"All checks passed ({Successes.Count} items verified)";

        var parts = new List<string>();
        if (Errors.Count > 0)
            parts.Add($"{Errors.Count} error(s)");
        if (Warnings.Count > 0)
            parts.Add($"{Warnings.Count} warning(s)");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// A single validation message with title and description.
/// </summary>
public sealed record ValidationMessage(string Title, string Description);
