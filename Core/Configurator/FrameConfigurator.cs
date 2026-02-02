using Game;

using Microsoft.Extensions.Logging;

using SharedLib;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core;

public sealed class FrameConfigurator : IDisposable
{
    private enum Stage
    {
        Reset,
        PreFlightCheck,
        DetectRunningGame,
        CheckGameWindowLocation,
        EnterConfigMode,
        WaitEnterConfigMode,
        RetryEnterConfigMode,
        ValidateMetaSize,
        CreateDataFrames,
        ReturnNormalMode,
        WaitReturnNormalMode,
        UpdateReader,
        ValidateData,
        Done
    }

    private Stage stage = Stage.Reset;

    private const int MAX_HEIGHT = 25; // this one just arbitrary number for sanity check
    private const int INTERVAL = 500;
    private const int MAX_WAIT_RETRIES = 10;
    private const int MAX_CONFIG_RETRIES = 3;  // Retry toggle config this many times before failing

    private readonly ILogger<FrameConfigurator> logger;
    private readonly WowProcess process;
    private readonly IWowScreen screen;
    private readonly WowProcessInput input;
    private readonly AddonConfigurator addonConfigurator;
    private readonly AddonValidator? addonValidator;
    private readonly Wait wait;
    private readonly IAddonDataProvider reader;

    private Thread? screenshotThread;
    private CancellationTokenSource cts = new();

    public DataFrameMeta DataFrameMeta { get; private set; } = DataFrameMeta.Empty;

    public DataFrame[] DataFrames { get; private set; } = Array.Empty<DataFrame>();

    public bool Saved { get; private set; }
    public bool AddonNotVisible { get; private set; }
    
    /// <summary>
    /// Current status message for UI display.
    /// </summary>
    public string StatusMessage { get; private set; } = "Ready";
    
    /// <summary>
    /// Indicates if there was a pre-flight validation failure.
    /// </summary>
    public bool PreFlightFailed { get; private set; }
    
    /// <summary>
    /// Pre-flight validation result, if available.
    /// </summary>
    public AddonValidationResult? ValidationResult { get; private set; }

    public string ImageBase64 { private set; get; } = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";

    private Rectangle screenRect = Rectangle.Empty;
    private Size size = Size.Empty;
    private int waitRetryCount = 0;
    private int configRetryCount = 0;

    public event Action? OnUpdate;

    public FrameConfigurator(ILogger<FrameConfigurator> logger, Wait wait,
        WowProcess process, IAddonDataProvider reader,
        IWowScreen screen, WowProcessInput input,
        AddonConfigurator addonConfigurator,
        AddonValidator? addonValidator = null)
    {
        this.logger = logger;
        this.wait = wait;
        this.process = process;
        this.reader = reader;
        this.screen = screen;
        this.input = input;
        this.addonConfigurator = addonConfigurator;
        this.addonValidator = addonValidator;
    }

    public void Dispose()
    {
        cts.Cancel();
    }

    private void ManualConfigThread()
    {
        screen.Enabled = true;

        while (!cts.Token.IsCancellationRequested)
        {
            DoConfig(false);

            OnUpdate?.Invoke();
            cts.Token.WaitHandle.WaitOne(INTERVAL);
            wait.Update();
        }
        screenshotThread = null;

        screen.Enabled = false;
    }

    private bool DoConfig(bool auto)
    {
        switch (stage)
        {
            case Stage.Reset:
                screenRect = Rectangle.Empty;
                size = Size.Empty;
                ResetConfigState();
                StatusMessage = "Initializing...";

                stage++;
                break;
                
            case Stage.PreFlightCheck:
                if (auto && addonValidator != null)
                {
                    StatusMessage = "Running pre-flight checks...";
                    ValidationResult = addonValidator.Validate();
                    
                    if (!ValidationResult.IsValid)
                    {
                        PreFlightFailed = true;
                        logger.LogError("Pre-flight checks failed: {Summary}", ValidationResult.GetSummary());
                        foreach (var error in ValidationResult.Errors)
                        {
                            logger.LogError("  - {Title}: {Description}", error.Title, error.Description);
                        }
                        StatusMessage = $"Pre-flight failed: {ValidationResult.GetSummary()}";
                        stage = Stage.Reset;
                        return false;
                    }
                    
                    if (ValidationResult.HasWarnings)
                    {
                        logger.LogWarning("Pre-flight warnings: {Summary}", ValidationResult.GetSummary());
                    }
                    
                    logger.LogInformation("Pre-flight checks passed");
                }
                PreFlightFailed = false;
                stage++;
                break;
                
            case Stage.DetectRunningGame:
                StatusMessage = "Detecting game...";
                if (process.IsRunning)
                {
                    if (auto)
                    {
                        logger.LogInformation(
                            $"Found {nameof(WowProcess)} with pid={process.Id} " +
                            $"{process.ProcessName}");
                    }
                    stage++;
                }
                else
                {
                    if (auto)
                    {
                        logger.LogWarning($"{nameof(WowProcess)} no longer running!");
                        StatusMessage = "Game not running";
                        return false;
                    }
                    stage--;
                }
                break;
            case Stage.CheckGameWindowLocation:
                StatusMessage = "Checking window position...";
                screen.GetRectangle(out screenRect);
                if (screenRect.Location.X < 0 || screenRect.Location.Y < 0)
                {
                    logger.LogWarning($"Client window outside of the visible area of the screen {screenRect.Location}");
                    StatusMessage = "Window outside visible area";
                    stage = Stage.Reset;

                    if (auto)
                    {
                        return false;
                    }
                }
                else
                {
                    AddonNotVisible = false;
                    stage++;

                    if (auto)
                    {
                        logger.LogInformation($"Client window: {screenRect}");
                    }
                }
                break;
            case Stage.EnterConfigMode:
                if (auto)
                {
                    Version? version = addonConfigurator.GetInstallVersion();
                    if (version == null)
                    {
                        stage = Stage.Reset;
                        logger.LogError("Addon is not installed!");
                        StatusMessage = "Addon not installed";
                        return false;
                    }
                    logger.LogInformation($"Addon installed! Version: {version}");

                    StatusMessage = "Entering config mode...";
                    logger.LogInformation("Enter configuration mode (attempt {Attempt}/{Max})", 
                        configRetryCount + 1, MAX_CONFIG_RETRIES);
                    input.SetForegroundWindow();
                    wait.Fixed(INTERVAL);
                    ToggleInGameConfiguration();
                    waitRetryCount = 0;
                    stage = Stage.WaitEnterConfigMode;
                }
                else
                {
                    // Manual mode: just check if already in config mode
                    DataFrameMeta temp = GetDataFrameMeta();
                    if (DataFrameMeta == DataFrameMeta.Empty && temp != DataFrameMeta.Empty)
                    {
                        DataFrameMeta = temp;
                        stage = Stage.ValidateMetaSize;
                        logger.LogInformation($"{DataFrameMeta}");
                    }
                }
                break;
            case Stage.WaitEnterConfigMode:
                {
                    StatusMessage = $"Waiting for config mode ({waitRetryCount + 1}/{MAX_WAIT_RETRIES})...";
                    wait.Update();
                    DataFrameMeta temp = GetDataFrameMeta();
                    if (DataFrameMeta == DataFrameMeta.Empty && temp != DataFrameMeta.Empty)
                    {
                        DataFrameMeta = temp;
                        stage = Stage.ValidateMetaSize;
                        logger.LogInformation($"{DataFrameMeta}");
                        configRetryCount = 0;  // Reset retry count on success
                    }
                    else
                    {
                        waitRetryCount++;
                        if (waitRetryCount >= MAX_WAIT_RETRIES)
                        {
                            // Check if we should retry
                            if (configRetryCount < MAX_CONFIG_RETRIES - 1)
                            {
                                stage = Stage.RetryEnterConfigMode;
                            }
                            else
                            {
                                logger.LogError("Timeout waiting for config mode after {Retries} attempts!", 
                                    configRetryCount + 1);
                                logger.LogError("The SHIFT-PAGEUP binding may not be configured.");
                                logger.LogError("In WoW, type: /dcactions to setup bindings");
                                StatusMessage = "Config mode timeout - run /dcactions in WoW";
                                stage = Stage.Reset;
                                if (auto) return false;
                            }
                        }
                    }
                }
                break;
                
            case Stage.RetryEnterConfigMode:
                configRetryCount++;
                logger.LogWarning("Config mode not detected, retrying (attempt {Attempt}/{Max})...", 
                    configRetryCount + 1, MAX_CONFIG_RETRIES);
                
                // Wait a bit longer before retry
                wait.Fixed(INTERVAL * 2);
                
                // Try toggling again
                stage = Stage.EnterConfigMode;
                break;
                
            case Stage.ValidateMetaSize:
                StatusMessage = "Validating frame size...";
                size = DataFrameMeta.EstimatedSize(screenRect);
                if (!size.IsEmpty &&
                    size.Width <= screenRect.Size.Width &&
                    size.Height <= screenRect.Size.Height &&
                    size.Height <= MAX_HEIGHT)
                {
                    stage++;
                }
                else
                {
                    logger.LogWarning($"Addon Rect({size}) size issue. Either too small or too big!");
                    StatusMessage = "Invalid frame size";
                    stage = Stage.Reset;

                    if (auto)
                        return false;
                }
                break;
            case Stage.CreateDataFrames:
                StatusMessage = "Creating data frames...";

                Size addonSize = size;
                var cropped = screen.ScreenImage.Clone(cropSize);
                void cropSize(IImageProcessingContext x)
                {
                    x.Crop(addonSize.Width, addonSize.Height);
                }

                if (!auto)
                {
                    ImageBase64 = cropped.ToBase64String(JpegFormat.Instance);
                }

                DataFrames = FrameConfig.CreateFrames(DataFrameMeta, cropped);
                if (DataFrames.Length == DataFrameMeta.Count)
                {
                    stage++;
                }
                else
                {
                    logger.LogWarning($"DataFrameMeta and FrameConfig doesn't match Frames: ({DataFrames.Length}) != Meta: ({DataFrameMeta.Count})");
                    StatusMessage = "Frame count mismatch";
                    stage = Stage.Reset;

                    if (auto)
                        return false;
                }

                break;
            case Stage.ReturnNormalMode:
                if (auto)
                {
                    StatusMessage = "Exiting config mode...";
                    logger.LogInformation("Exit configuration mode.");
                    input.SetForegroundWindow();
                    ToggleInGameConfiguration();
                    waitRetryCount = 0;
                    stage = Stage.WaitReturnNormalMode;
                }
                else
                {
                    // Manual mode: just check if already in normal mode
                    DataFrameMeta temp = GetDataFrameMeta();
                    if (temp == DataFrameMeta.Empty)
                    {
                        logger.LogDebug(temp.ToString());
                        stage = Stage.UpdateReader;
                    }
                }
                break;
            case Stage.WaitReturnNormalMode:
                {
                    StatusMessage = $"Waiting for normal mode ({waitRetryCount + 1}/{MAX_WAIT_RETRIES})...";
                    wait.Update();
                    DataFrameMeta temp = GetDataFrameMeta();
                    if (temp == DataFrameMeta.Empty)
                    {
                        logger.LogDebug(temp.ToString());
                        stage = Stage.UpdateReader;
                    }
                    else
                    {
                        waitRetryCount++;
                        if (waitRetryCount >= MAX_WAIT_RETRIES)
                        {
                            logger.LogError("Unable to return normal mode!");
                            StatusMessage = "Could not exit config mode";
                            ResetConfigState();
                            return false;
                        }
                    }
                }
                break;
            case Stage.UpdateReader:
                StatusMessage = "Updating data reader...";
                reader.InitFrames(DataFrames);
                wait.Update();
                wait.Update();
                reader.UpdateData();
                stage++;
                break;
            case Stage.ValidateData:
                StatusMessage = "Validating character data...";
                if (TryResolveRaceAndClass(out UnitRace race, out UnitClass @class, out ClientVersion clientVersion))
                {
                    if (auto)
                    {
                        logger.LogInformation($"Found {clientVersion.ToStringF()} {race.ToStringF()} {@class.ToStringF()}!");
                    }
                    StatusMessage = $"Detected: {race.ToStringF()} {@class.ToStringF()}";
                    stage++;
                }
                else
                {
                    logger.LogError($"Unable to identify {nameof(ClientVersion)} {nameof(UnitRace)} and {nameof(UnitClass)}!");
                    StatusMessage = "Could not detect character";
                    stage = Stage.Reset;

                    if (auto)
                        return false;
                }
                break;
            case Stage.Done:
                StatusMessage = "Configuration complete";
                return false;
            default:
                break;
        }

        return true;
    }


    private void ResetConfigState()
    {
        screenRect = Rectangle.Empty;
        size = Size.Empty;

        AddonNotVisible = true;
        stage = Stage.Reset;
        Saved = false;
        configRetryCount = 0;

        DataFrameMeta = DataFrameMeta.Empty;
        DataFrames = Array.Empty<DataFrame>();

        reader.InitFrames(DataFrames);
        wait.Update();

        logger.LogDebug("ResetConfigState");
    }

    private DataFrameMeta GetDataFrameMeta()
    {
        return FrameConfig.GetMeta(screen.ScreenImage[0, 0]);
    }

    public void ToggleManualConfig()
    {
        if (screenshotThread != null)
        {
            cts.Cancel();
            return;
        }

        ResetConfigState();

        cts.Dispose();
        cts = new();
        screenshotThread = new Thread(ManualConfigThread);
        screenshotThread.Start();
    }

    public bool FinishConfig()
    {
        Version? version = addonConfigurator.GetInstallVersion();
        if (version == null ||
            DataFrames.Length == 0 ||
            DataFrameMeta.Count == 0 ||
            DataFrames.Length != DataFrameMeta.Count ||
            !TryResolveRaceAndClass(out _, out _, out _))
        {
            logger.LogInformation("Frame configuration was incomplete! Please try again, after resolving the previously mentioned issues...");
            StatusMessage = "Configuration incomplete";
            ResetConfigState();
            return false;
        }

        screen.GetRectangle(out Rectangle rect);
        FrameConfig.Save(rect, version, DataFrameMeta, DataFrames);
        logger.LogInformation("Frame configuration was successful! Configuration saved!");
        StatusMessage = "Configuration saved!";
        Saved = true;

        return true;
    }

    public bool StartAutoConfig()
    {
        screen.Enabled = true;
        PreFlightFailed = false;

        while (DoConfig(true))
        {
            wait.Update();
        }

        screen.Enabled = false;

        return FinishConfig();
    }

    /// <summary>
    /// Async version of StartAutoConfig that supports cancellation and doesn't block.
    /// Use this from the startup orchestrator for non-blocking frame configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if configuration succeeded, false otherwise</returns>
    public async Task<bool> StartAutoConfigAsync(CancellationToken cancellationToken = default)
    {
        screen.Enabled = true;
        PreFlightFailed = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested && DoConfig(true))
            {
                // Use async delay instead of blocking wait
                await Task.Delay(INTERVAL, cancellationToken);
                wait.Update();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                StatusMessage = "Configuration cancelled";
                return false;
            }

            return FinishConfig();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Configuration cancelled";
            return false;
        }
        finally
        {
            screen.Enabled = false;
        }
    }

    /// <summary>
    /// Attempts auto-configuration with retries.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="retryDelaySeconds">Delay between retries in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if configuration succeeded, false otherwise</returns>
    public async Task<bool> StartAutoConfigWithRetriesAsync(
        int maxRetries = 3,
        int retryDelaySeconds = 5,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            logger.LogInformation("Frame auto-configuration attempt {Attempt}/{MaxRetries}", attempt, maxRetries);

            ResetConfigState();
            var success = await StartAutoConfigAsync(cancellationToken);

            if (success)
            {
                logger.LogInformation("Frame configuration succeeded on attempt {Attempt}", attempt);
                return true;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (attempt < maxRetries)
            {
                logger.LogWarning("Frame configuration failed, retrying in {Delay} seconds...", retryDelaySeconds);
                StatusMessage = $"Retrying in {retryDelaySeconds}s (attempt {attempt}/{maxRetries})...";
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
            }
        }

        logger.LogError("Frame configuration failed after {MaxRetries} attempts", maxRetries);
        StatusMessage = $"Configuration failed after {maxRetries} attempts";
        return false;
    }

    public static void DeleteConfig()
    {
        FrameConfig.Delete();
    }

    private void ToggleInGameConfiguration()
    {
        // Press SHIFT-PAGEUP to trigger CUSTOM_CONFIG (/dc)
        input.PressRandomWithModifier(ConsoleKey.PageUp, ModifierKey.Shift, 50);
    }

    public bool TryResolveRaceAndClass(out UnitRace race, out UnitClass @class, out ClientVersion version)
    {
        if (reader.Data.Length < 46)
        {
            race = 0;
            @class = 0;
            version = 0;
            return false;
        }

        int value = reader.GetInt(46);

        // RACE_ID * 10000 + CLASS_ID * 100 + ClientVersion
        race = (UnitRace)(value / 10000);
        @class = (UnitClass)(value / 100 % 100);
        version = (ClientVersion)(value % 100);

        return Enum.IsDefined(race) && Enum.IsDefined(@class) && Enum.IsDefined(version) &&
            race != UnitRace.None && @class != UnitClass.None && version != ClientVersion.None;
    }
}
