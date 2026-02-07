using Game;

using Microsoft.Extensions.Logging;

using SharedLib;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

using System;
using System.IO;
using System.Linq;
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

    private const int MAX_ROWS = 100; // Maximum number of frame rows (sanity check)
    private const int MAX_CELL_SIZE = 20; // Maximum pixel size per frame cell (sanity check)
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
    
    /// <summary>
    /// The detected X offset where the addon's metadata pixel was found.
    /// Exposed for diagnostics. Returns 0 if not yet detected.
    /// </summary>
    public int MetaPixelXOffset => metaPixelXOffset;

    private const string DefaultImagePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";

    public string ImageMimeType { get; private set; } = "image/png";

    public string ImageBase64 { get; private set; } = DefaultImagePngBase64;

    public string ImageDataUri => $"data:{ImageMimeType};base64,{ImageBase64}";

    private Rectangle screenRect = Rectangle.Empty;
    private Size size = Size.Empty;
    private int waitRetryCount;
    private int configRetryCount;
    
    /// <summary>
    /// The X offset where the addon's metadata pixel was found.
    /// The addon may have a small border/offset before its pixel grid starts.
    /// </summary>
    private int metaPixelXOffset;
    
    /// <summary>
    /// Maximum X pixels to scan when looking for the metadata pixel.
    /// </summary>
    private const int MAX_META_SCAN_PIXELS = 20;

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
        logger.LogDebug("DoConfig: Stage={Stage}, Auto={Auto}", stage, auto);
        
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
                                logger.LogError("Ensure character is logged in and chat is available.");
                                logger.LogError("The bot will type /{Command} in chat to toggle config mode.", GetAddonCommand());
                                StatusMessage = "Config mode timeout - ensure character is in-world";
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
                
                // Sanity checks:
                // 1. Size must not be empty
                // 2. Size must fit within screen bounds
                // 3. Row count must be reasonable (not more than MAX_ROWS)
                // 4. Frame cell size must be reasonable (not more than MAX_CELL_SIZE pixels)
                bool sizeValid = !size.IsEmpty &&
                    size.Width <= screenRect.Size.Width &&
                    size.Height <= screenRect.Size.Height &&
                    DataFrameMeta.Rows <= MAX_ROWS &&
                    DataFrameMeta.Sizes <= MAX_CELL_SIZE;
                
                if (sizeValid)
                {
                    logger.LogDebug($"Frame size validated: {size.Width}x{size.Height}, Rows={DataFrameMeta.Rows}, CellSize={DataFrameMeta.Sizes}");
                    stage++;
                }
                else
                {
                    logger.LogWarning($"Addon Rect({size}) size issue. Rows={DataFrameMeta.Rows} (max={MAX_ROWS}), CellSize={DataFrameMeta.Sizes} (max={MAX_CELL_SIZE})");
                    StatusMessage = "Invalid frame size";
                    stage = Stage.Reset;

                    if (auto)
                        return false;
                }
                break;
            case Stage.CreateDataFrames:
                StatusMessage = "Creating data frames...";

                // Multiple screen update attempts with verification
                int updateAttempts = 0;
                const int MAX_UPDATE_ATTEMPTS = 10;
                const int UPDATE_DELAY_MS = 200;
                bool foundConfigFrame = false;
                
                logger.LogDebug("Attempting to capture config mode screen (max {MaxAttempts} attempts)...", MAX_UPDATE_ATTEMPTS);
                
                while (updateAttempts < MAX_UPDATE_ATTEMPTS && !foundConfigFrame)
                {
                    // Use WaitForUpdate for better frame acquisition
                    bool frameAcquired = screen.WaitForUpdate(3, 50);
                    updateAttempts++;
                    
                    if (!frameAcquired)
                    {
                        logger.LogDebug("Screen update attempt {Attempt}: no new frame acquired", updateAttempts);
                        wait.Fixed(UPDATE_DELAY_MS);
                        continue;
                    }
                    
                    // Verify we have a valid config frame by checking for frame 1 marker
                    var testImage = screen.ScreenImage;
                    if (testImage == null || testImage.Width == 0 || testImage.Height == 0)
                    {
                        logger.LogWarning("Screen update attempt {Attempt}: screen image is null or empty", updateAttempts);
                        wait.Fixed(UPDATE_DELAY_MS);
                        continue;
                    }
                    
                    int expectedFrame1Y = DataFrameMeta.Sizes; // CELL_SIZE (typically 4)
                    if (testImage.Height > expectedFrame1Y && testImage.Width > metaPixelXOffset)
                    {
                        // Check for frame 1 marker at (xOffset, CELL_SIZE)
                        var pixel = testImage[metaPixelXOffset, expectedFrame1Y];
                        logger.LogDebug("Screen update attempt {Attempt}: pixel at ({X},{Y}) = R:{R} G:{G} B:{B} (expect frame 1: R:0 G:0 B:1)", 
                            updateAttempts, metaPixelXOffset, expectedFrame1Y, pixel.R, pixel.G, pixel.B);
                        
                        if (pixel.B == 1 && pixel.R == 0 && pixel.G == 0)
                        {
                            foundConfigFrame = true;
                            logger.LogInformation("Found valid config frame at attempt {Attempt}", updateAttempts);
                        }
                    }
                    
                    if (!foundConfigFrame && updateAttempts < MAX_UPDATE_ATTEMPTS)
                    {
                        wait.Fixed(UPDATE_DELAY_MS);
                    }
                }
                
                if (!foundConfigFrame)
                {
                    logger.LogWarning("Could not verify config frame marker after {Attempts} attempts, proceeding with frame detection anyway", updateAttempts);
                }

                Size addonSize = size;
                var cropped = screen.ScreenImage.Clone(cropSize);
                void cropSize(IImageProcessingContext x)
                {
                    x.Crop(addonSize.Width, addonSize.Height);
                }

                if (!auto)
                {
                    ImageMimeType = "image/jpeg";
                    ImageBase64 = cropped.ToBase64String(JpegFormat.Instance);
                }

                // Pass logger to CreateFrames for detailed diagnostics
                DataFrames = FrameConfig.CreateFrames(DataFrameMeta, cropped, metaPixelXOffset, logger);
                
                // Diagnostic: Check if frame detection succeeded
                int detectedFrames = DataFrames.Count(f => f.X != 0 || f.Y != 0) + (DataFrames.Length > 0 ? 1 : 0);
                
                if (detectedFrames < DataFrameMeta.Count)
                {
                    // Save diagnostic image
                    string debugPath = Path.Combine(AppContext.BaseDirectory, "frame_config_debug.png");
                    try
                    {
                        cropped.SaveAsPng(debugPath);
                        logger.LogError("Frame detection incomplete: found {Found}/{Expected} frames. Debug image saved to: {Path}", 
                            detectedFrames, DataFrameMeta.Count, debugPath);
                        
                        // Log detailed info about first few frames
                        logger.LogError("Frame detection details:");
                        logger.LogError("  Metadata: Hash={Hash}, Spacing={Spacing}, Size={Size}, Rows={Rows}, Count={Count}", 
                            DataFrameMeta.Hash, DataFrameMeta.Spacing, DataFrameMeta.Sizes, DataFrameMeta.Rows, DataFrameMeta.Count);
                        logger.LogError("  Cropped image size: {W}x{H}, X offset: {Offset}", 
                            cropped.Width, cropped.Height, metaPixelXOffset);
                        
                        // Log expected positions and actual pixel values for first few frames
                        for (int i = 0; i < Math.Min(10, DataFrameMeta.Count); i++)
                        {
                            int gridX = i / DataFrameMeta.Rows;
                            int gridY = i % DataFrameMeta.Rows;
                            int expectedPixelX = gridX * DataFrameMeta.Sizes + metaPixelXOffset;
                            int expectedPixelY = gridY * DataFrameMeta.Sizes;
                            
                            string pixelInfo = "out of bounds";
                            if (expectedPixelX < cropped.Width && expectedPixelY < cropped.Height)
                            {
                                var p = cropped[expectedPixelX, expectedPixelY];
                                pixelInfo = $"RGB=({p.R},{p.G},{p.B})";
                            }
                            
                            string detectedInfo = i < DataFrames.Length ? $"detected at ({DataFrames[i].X},{DataFrames[i].Y})" : "not detected";
                            
                            logger.LogError("  Frame {Index}: grid({GridX},{GridY}) -> expected pixel({ExpX},{ExpY}) {PixelInfo}, {DetectedInfo}", 
                                i, gridX, gridY, expectedPixelX, expectedPixelY, pixelInfo, detectedInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to save debug image");
                    }
                }
                
                if (DataFrames.Length == DataFrameMeta.Count)
                {
                    if (metaPixelXOffset > 0)
                    {
                        logger.LogInformation("Created {Count} frames with X offset {Offset}", 
                            DataFrames.Length, metaPixelXOffset);
                    }
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
                    wait.Fixed(INTERVAL);  // Wait for window focus before sending keypress
                    ToggleInGameConfiguration();
                    wait.Fixed(INTERVAL);  // Wait for WoW to process keypress and render new frame
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
                        
                        // Every 3 retries, try sending the command again
                        if (waitRetryCount > 0 && waitRetryCount % 3 == 0 && waitRetryCount < MAX_WAIT_RETRIES)
                        {
                            logger.LogWarning("Config mode still detected, retrying /{Command} (attempt {Attempt})...",
                                GetAddonCommand(), waitRetryCount / 3 + 1);
                            input.SetForegroundWindow();
                            wait.Fixed(INTERVAL);
                            ToggleInGameConfiguration();
                            wait.Fixed(INTERVAL);
                        }
                        
                        if (waitRetryCount >= MAX_WAIT_RETRIES)
                        {
                            logger.LogError("Unable to return normal mode after {Retries} attempts!", MAX_WAIT_RETRIES);
                            logger.LogError("Ensure character is logged in and chat is not open.");
                            StatusMessage = "Could not exit config mode - ensure character is in-world";
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
        metaPixelXOffset = 0;

        DataFrameMeta = DataFrameMeta.Empty;
        DataFrames = Array.Empty<DataFrame>();

        reader.InitFrames(DataFrames);
        wait.Update();

        logger.LogDebug("ResetConfigState");
    }

    private DataFrameMeta GetDataFrameMeta()
    {
        // Refresh the screen capture before reading pixels
        screen.Update();
        
        // Scan the first row to find the metadata pixel
        // The addon may have a small offset/border before its pixel grid starts
        var image = screen.ScreenImage;
        
        if (image == null || image.Width == 0 || image.Height == 0)
        {
            logger.LogWarning("GetDataFrameMeta: Screen image is null or empty");
            return DataFrameMeta.Empty;
        }
        
        int scanLimit = Math.Min(MAX_META_SCAN_PIXELS, image.Width);
        logger.LogDebug("GetDataFrameMeta: Scanning first {ScanLimit} pixels of row 0 (image size: {W}x{H})", 
            scanLimit, image.Width, image.Height);
        
        for (int x = 0; x < scanLimit; x++)
        {
            var pixel = image[x, 0];
            var meta = FrameConfig.GetMeta(pixel);
            
            if (x < 5 || meta != DataFrameMeta.Empty)
            {
                // Log first few pixels and any valid meta found
                logger.LogDebug("GetDataFrameMeta: Pixel[{X},0] = R:{R} G:{G} B:{B} -> Meta: {Meta}", 
                    x, pixel.R, pixel.G, pixel.B, meta);
            }
            
            if (meta != DataFrameMeta.Empty)
            {
                if (x > 0)
                {
                    logger.LogInformation("Found metadata pixel at X offset {Offset} (not at 0,0)", x);
                }
                metaPixelXOffset = x;
                return meta;
            }
        }
        
        // Log that we didn't find any valid metadata
        logger.LogDebug("GetDataFrameMeta: No valid metadata pixel found in first {ScanLimit} pixels", scanLimit);
        
        // Reset offset if not found
        metaPixelXOffset = 0;
        return DataFrameMeta.Empty;
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
        string command = GetAddonCommand();

        // Ensure WoW is in foreground
        input.SetForegroundWindow();
        wait.Fixed(200); // Brief delay to let window focus settle
        
        // IMPORTANT: Press Escape first to close any open UI (chat, menus, targeting, etc.)
        // This ensures a clean state before we open chat and type the command.
        // Without this, if chat is already open from a previous command, we'd type into it wrong.
        logger.LogDebug("ToggleInGameConfiguration: Pressing Escape to ensure clean UI state...");
        input.PressRandom(ConsoleKey.Escape, 50);
        wait.Fixed(300); // Wait for any UI to close
        
        // Press Escape again to be extra sure (handles nested menus, targeting, etc.)
        input.PressRandom(ConsoleKey.Escape, 50);
        wait.Fixed(300);
        
        // ROBUST APPROACH: Type command directly in chat
        // This bypasses the keybinding requirement entirely - no need for SHIFT-PAGEUP
        // to be configured. Works immediately after addon install.
        logger.LogDebug("ToggleInGameConfiguration: Typing /{Command} command in chat...", command);
        
        // Press Enter to open chat
        input.PressRandom(ConsoleKey.Enter, 50);
        wait.Fixed(200);
        
        // Type the command
        input.SendText("/" + command);
        wait.Fixed(150);
        
        // Press Enter to execute command
        input.PressRandom(ConsoleKey.Enter, 50);
        wait.Fixed(300);
        
        logger.LogDebug("ToggleInGameConfiguration: /{Command} command sent via chat", command);
    }

    private string GetAddonCommand()
    {
        string cmd = addonConfigurator.Config.Command?.Trim() ?? string.Empty;
        if (cmd.StartsWith('/'))
        {
            cmd = cmd.TrimStart('/');
        }

        if (string.IsNullOrWhiteSpace(cmd))
        {
            return "dc";
        }

        return cmd;
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
