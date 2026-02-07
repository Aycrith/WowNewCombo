using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Game;

namespace Core.Launch;

public enum LaunchAutoFixStatus
{
    Skipped = 0,
    Applied = 1,
    Failed = 2
}

public sealed record LaunchAutoFixStep(
    string Name,
    LaunchAutoFixStatus Status,
    string Message);

public sealed record LaunchAutoFixResult(
    bool Success,
    bool RequiresRestart,
    IReadOnlyList<LaunchAutoFixStep> Steps);

public sealed class LaunchAutoFixService
{
    private const int RecommendedCellSize = 4;
    private const string FallbackCommand = "dc";

    private readonly ILogger<LaunchAutoFixService> logger;
    private readonly IServiceProvider services;
    private readonly WowProcess wowProcess;
    private readonly AddonConfigurator addonConfigurator;
    private readonly AddonValidator addonValidator;
    private readonly FrameConfigurator? frameConfigurator;
    private readonly ILaunchReadinessCacheInvalidator? cacheInvalidator;
    private readonly IAddonReader? addonReader;

    public LaunchAutoFixService(
        ILogger<LaunchAutoFixService> logger,
        IServiceProvider services,
        WowProcess wowProcess,
        AddonConfigurator addonConfigurator,
        AddonValidator addonValidator,
        FrameConfigurator? frameConfigurator = null,
        ILaunchReadinessCacheInvalidator? cacheInvalidator = null,
        IAddonReader? addonReader = null)
    {
        this.logger = logger;
        this.services = services;
        this.wowProcess = wowProcess;
        this.addonConfigurator = addonConfigurator;
        this.addonValidator = addonValidator;
        this.frameConfigurator = frameConfigurator;
        this.cacheInvalidator = cacheInvalidator;
        this.addonReader = addonReader;
    }

    public async Task<LaunchAutoFixResult> ApplyRecommendedFixesAsync(CancellationToken cancellationToken)
    {
        List<LaunchAutoFixStep> steps = [];
        bool requiresRestart = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!AddonConfig.Exists())
            {
                // Ensure we have a config file so other systems can reason about prefix/cell size.
                AddonConfig config = addonConfigurator.Config;
                config.Author = "FreeHongKongMMO";
                config.Title = "DataToColor";
                config.Command = FallbackCommand;
                config.CellSize = RecommendedCellSize.ToString();
                config.Save();
                steps.Add(new LaunchAutoFixStep("addon_config.json", LaunchAutoFixStatus.Applied, "Created default addon_config.json"));
            }
            else
            {
                steps.Add(new LaunchAutoFixStep("addon_config.json", LaunchAutoFixStatus.Skipped, "addon_config.json present"));
            }

            bool needsAddonReinstall = EnsureAddonConfigRecommended(steps);
            needsAddonReinstall |= DetectInstalledAddonMismatch(steps);

            if (needsAddonReinstall)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!addonConfigurator.Validate())
                {
                    steps.Add(new LaunchAutoFixStep("AddonConfig.Validate", LaunchAutoFixStatus.Failed, "Addon configuration invalid"));
                    return new LaunchAutoFixResult(false, false, steps);
                }

                if (!addonConfigurator.TryInstall(out string message))
                {
                    steps.Add(new LaunchAutoFixStep("DataToColor install", LaunchAutoFixStatus.Failed, $"Install failed: {message}"));
                    return new LaunchAutoFixResult(false, false, steps);
                }
                addonConfigurator.Save();
                steps.Add(new LaunchAutoFixStep("DataToColor install", LaunchAutoFixStatus.Applied, "Reinstalled addon with recommended config"));
                cacheInvalidator?.InvalidateAddonValidation();

                ExecGameCommand? exec = services.GetService<ExecGameCommand>();
                if (exec != null)
                {
                    exec.Run("/reload");
                    steps.Add(new LaunchAutoFixStep("WoW /reload", LaunchAutoFixStatus.Applied, "Triggered /reload"));
                }
                else
                {
                    steps.Add(new LaunchAutoFixStep("WoW /reload", LaunchAutoFixStatus.Skipped, "WoW command executor unavailable"));
                }
            }
            else
            {
                steps.Add(new LaunchAutoFixStep("DataToColor install", LaunchAutoFixStatus.Skipped, "Addon appears consistent"));
            }

            cacheInvalidator?.InvalidateAddonValidation();
            AddonValidationResult validation = addonValidator.Validate();
            if (!validation.IsValid)
            {
                steps.Add(new LaunchAutoFixStep("Addon validation", LaunchAutoFixStatus.Failed, validation.GetSummary()));
                return new LaunchAutoFixResult(false, requiresRestart, steps);
            }

            cacheInvalidator?.PrimeAddonValidation(validation, "AutoFix");
            steps.Add(new LaunchAutoFixStep("Addon validation", LaunchAutoFixStatus.Applied, "Validated"));

            string addonTitle = addonConfigurator.Config.Title;
            if (!string.IsNullOrWhiteSpace(addonTitle))
            {
                global::Core.AddonInstaller? installer = services.GetService<global::Core.AddonInstaller>();
                if (installer != null)
                {
                    installer.EnableAddonForAllCharacters(addonTitle);
                    int disabled = installer.DisableEnabledMissingAddOns("Blizzard_");
                    steps.Add(new LaunchAutoFixStep(
                        "AddOns.txt cleanup",
                        LaunchAutoFixStatus.Applied,
                        disabled > 0
                            ? $"Disabled {disabled} stale Blizzard_ entries"
                            : "Ensured addon enabled; no stale Blizzard_ entries found"));
                }
                else
                {
                    steps.Add(new LaunchAutoFixStep("AddOns.txt cleanup", LaunchAutoFixStatus.Skipped, "AddonInstaller unavailable"));
                }
            }
            else
            {
                steps.Add(new LaunchAutoFixStep("AddOns.txt cleanup", LaunchAutoFixStatus.Skipped, "Addon title not configured"));
            }

            if (frameConfigurator != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!FrameConfig.Exists())
                {
                    bool ok = await frameConfigurator.StartAutoConfigWithRetriesAsync(
                        maxRetries: 3,
                        retryDelaySeconds: 5,
                        cancellationToken: cancellationToken);

                    if (!ok)
                    {
                        steps.Add(new LaunchAutoFixStep("Frame auto-config", LaunchAutoFixStatus.Failed, frameConfigurator.StatusMessage));
                        return new LaunchAutoFixResult(false, requiresRestart, steps);
                    }

                    requiresRestart = true;
                    steps.Add(new LaunchAutoFixStep("Frame auto-config", LaunchAutoFixStatus.Applied, "Frame config created (restart server required)"));
                }
                else
                {
                    steps.Add(new LaunchAutoFixStep("Frame auto-config", LaunchAutoFixStatus.Skipped, "frame_config.json present"));
                }
            }
            else
            {
                steps.Add(new LaunchAutoFixStep("Frame auto-config", LaunchAutoFixStatus.Skipped, "FrameConfigurator unavailable"));
            }

            // --- Key Bindings auto-fix ---
            cancellationToken.ThrowIfCancellationRequested();
            TryFixKeyBindings(steps);

            return new LaunchAutoFixResult(true, requiresRestart, steps);
        }
        catch (OperationCanceledException)
        {
            steps.Add(new LaunchAutoFixStep("Auto-fix", LaunchAutoFixStatus.Failed, "Cancelled"));
            return new LaunchAutoFixResult(false, requiresRestart, steps);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[LaunchAutoFixService] Auto-fix failed");
            steps.Add(new LaunchAutoFixStep("Auto-fix", LaunchAutoFixStatus.Failed, ex.Message));
            return new LaunchAutoFixResult(false, requiresRestart, steps);
        }
    }

    private bool EnsureAddonConfigRecommended(List<LaunchAutoFixStep> steps)
    {
        AddonConfig config = addonConfigurator.Config;
        bool changed = false;

        if (string.IsNullOrWhiteSpace(config.Author))
        {
            config.Author = "FreeHongKongMMO";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(config.Title))
        {
            config.Title = "DataToColor";
            changed = true;
        }

        if (!int.TryParse(config.CellSize, out int cellSize) || cellSize < RecommendedCellSize)
        {
            config.CellSize = RecommendedCellSize.ToString();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(config.Command))
        {
            config.Command = FallbackCommand;
            changed = true;
        }
        else if (config.Command.StartsWith('/'))
        {
            config.Command = config.Command.TrimStart('/');
            changed = true;
        }

        if (changed)
        {
            steps.Add(new LaunchAutoFixStep("Addon config", LaunchAutoFixStatus.Applied, "Normalized CellSize/Command"));
        }
        else
        {
            steps.Add(new LaunchAutoFixStep("Addon config", LaunchAutoFixStatus.Skipped, "Already normalized"));
        }

        return changed;
    }

    private bool DetectInstalledAddonMismatch(List<LaunchAutoFixStep> steps)
    {
        if (!wowProcess.IsRunning || string.IsNullOrWhiteSpace(wowProcess.Path))
        {
            steps.Add(new LaunchAutoFixStep("Installed addon check", LaunchAutoFixStatus.Skipped, "WoW not running (cannot verify on-disk addon state)"));
            return false;
        }

        try
        {
            string addonPath = addonConfigurator.FinalAddonPath;
            string luaPath = Path.Combine(addonPath, $"{addonConfigurator.Config.Title}.lua");
            if (!File.Exists(luaPath))
            {
                steps.Add(new LaunchAutoFixStep("Installed addon check", LaunchAutoFixStatus.Applied, "Addon LUA missing (reinstall required)"));
                return true;
            }

            int? installedCellSize = TryReadInstalledCellSize(luaPath);
            if (installedCellSize == null)
            {
                steps.Add(new LaunchAutoFixStep("Installed addon check", LaunchAutoFixStatus.Skipped, "Could not read installed CELL_SIZE"));
                return false;
            }

            if (!int.TryParse(addonConfigurator.Config.CellSize, out int desiredCellSize))
            {
                desiredCellSize = RecommendedCellSize;
            }

            if (installedCellSize.Value != desiredCellSize)
            {
                steps.Add(new LaunchAutoFixStep("Installed addon check", LaunchAutoFixStatus.Applied,
                    $"Installed CELL_SIZE={installedCellSize} differs from config={desiredCellSize} (reinstall required)"));
                return true;
            }

            steps.Add(new LaunchAutoFixStep("Installed addon check", LaunchAutoFixStatus.Skipped, "Installed addon matches configured CELL_SIZE"));
            return false;
        }
        catch (Exception ex)
        {
            steps.Add(new LaunchAutoFixStep("Installed addon check", LaunchAutoFixStatus.Skipped, $"Check failed: {ex.Message}"));
            return false;
        }
    }

    private static int? TryReadInstalledCellSize(string luaPath)
    {
        string[] lines = File.ReadAllLines(luaPath);
        Regex rx = new(@"^\s*local\s+CELL_SIZE\s*=\s*(?<n>\d+)\s*", RegexOptions.Compiled);

        foreach (string line in lines)
        {
            Match m = rx.Match(line);
            if (!m.Success)
            {
                continue;
            }

            if (int.TryParse(m.Groups["n"].Value, out int n))
            {
                return n;
            }
        }

        return null;
    }

    private void TryFixKeyBindings(List<LaunchAutoFixStep> steps)
    {
        KeyBindingsReader? keyBindings = services.GetService<KeyBindingsReader>();
        if (keyBindings == null)
        {
            steps.Add(new LaunchAutoFixStep("Key bindings", LaunchAutoFixStatus.Skipped, "KeyBindingsReader unavailable"));
            return;
        }

        if (keyBindings.IsInitialized)
        {
            steps.Add(new LaunchAutoFixStep("Key bindings", LaunchAutoFixStatus.Skipped, $"Already initialized ({keyBindings.Count} bindings)"));
            return;
        }

        // Check that addon handshake is alive before sending commands
        if (addonReader is not AddonReader fullReader || fullReader.GlobalTime.Value <= 3)
        {
            steps.Add(new LaunchAutoFixStep("Key bindings", LaunchAutoFixStatus.Skipped,
                "Addon handshake not yet established — cannot send /dcbindings"));
            return;
        }

        ExecGameCommand? exec = services.GetService<ExecGameCommand>();
        if (exec == null)
        {
            steps.Add(new LaunchAutoFixStep("Key bindings", LaunchAutoFixStatus.Skipped, "ExecGameCommand unavailable"));
            return;
        }

        string prefix = addonConfigurator.Config.Command ?? "dc";
        string command = $"/{prefix}bindings";
        exec.Run(command);
        logger.LogInformation("[LaunchAutoFix    ] Sent {Command} to populate key bindings", command);
        steps.Add(new LaunchAutoFixStep("Key bindings", LaunchAutoFixStatus.Applied, $"Sent {command} to WoW"));
    }
}
