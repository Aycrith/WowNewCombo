using Core;
using Game;

using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FrameConfigController : ControllerBase
{
    private readonly IWowScreen _screen;
    private readonly FrameConfigurator? _frameConfigurator;

    public FrameConfigController(IWowScreen screen, FrameConfigurator? frameConfigurator = null)
    {
        _screen = screen;
        _frameConfigurator = frameConfigurator;
    }

    /// <summary>
    /// Get the current frame configuration status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        if (_frameConfigurator == null)
        {
            return Ok(new
            {
                FrameConfigExists = FrameConfig.Exists(),
                AddonConfigExists = AddonConfig.Exists(),
                StatusMessage = "FrameConfigurator unavailable (WoW not running)",
                PreFlightFailed = false,
                Saved = false,
                DataFrameCount = 0,
                ExpectedFrameCount = 0,
                AddonNotVisible = true
            });
        }

        return Ok(new
        {
            FrameConfigExists = FrameConfig.Exists(),
            AddonConfigExists = AddonConfig.Exists(),
            StatusMessage = _frameConfigurator.StatusMessage,
            PreFlightFailed = _frameConfigurator.PreFlightFailed,
            Saved = _frameConfigurator.Saved,
            DataFrameCount = _frameConfigurator.DataFrames.Length,
            ExpectedFrameCount = _frameConfigurator.DataFrameMeta.Count,
            AddonNotVisible = _frameConfigurator.AddonNotVisible
        });
    }

    /// <summary>
    /// Trigger auto-configuration of frames
    /// </summary>
    [HttpPost("auto-configure")]
    public async Task<IActionResult> AutoConfigure(CancellationToken cancellationToken)
    {
        if (_frameConfigurator == null)
        {
            return Ok(new
            {
                Success = false,
                Message = "WoW is not running (FrameConfigurator unavailable)",
                StatusMessage = "Start WoW and reload the page"
            });
        }

        if (FrameConfig.Exists())
        {
            return Ok(new
            {
                Success = true,
                Message = "Frame configuration already exists",
                AlreadyConfigured = true
            });
        }

        try
        {
            bool success = await _frameConfigurator.StartAutoConfigAsync(cancellationToken);

            return Ok(new
            {
                Success = success,
                Message = success ? "Frame configuration completed successfully" : "Frame configuration failed",
                StatusMessage = _frameConfigurator.StatusMessage,
                DataFrameCount = _frameConfigurator.DataFrames.Length,
                ExpectedFrameCount = _frameConfigurator.DataFrameMeta.Count,
                RequiresRestart = success
            });
        }
        catch (OperationCanceledException)
        {
            return Ok(new
            {
                Success = false,
                Message = "Configuration cancelled",
                StatusMessage = _frameConfigurator.StatusMessage
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Success = false,
                Message = $"Configuration failed: {ex.Message}",
                StatusMessage = _frameConfigurator.StatusMessage
            });
        }
    }

    /// <summary>
    /// Delete current frame configuration
    /// </summary>
    [HttpDelete("config")]
    public IActionResult DeleteConfig()
    {
        if (!FrameConfig.Exists())
        {
            return Ok(new
            {
                Success = true,
                Message = "No frame configuration to delete"
            });
        }

        FrameConfigurator.DeleteConfig();

        return Ok(new
        {
            Success = true,
            Message = "Frame configuration deleted. Restart required.",
            RequiresRestart = true
        });
    }

    /// <summary>
    /// Get diagnostic information about screen capture
    /// </summary>
    [HttpGet("diagnostics")]
    public IActionResult GetDiagnostics()
    {
        if (_frameConfigurator == null)
        {
            return Ok(new
            {
                Message = "FrameConfigurator unavailable (WoW not running)",
                ScreenEnabled = _screen.Enabled
            });
        }

        _screen.GetRectangle(out var screenRect);

        // Enable screen capture temporarily to get a frame
        bool wasEnabled = _screen.Enabled;
        _screen.Enabled = true;

        try
        {
            // Give it a moment to capture
            System.Threading.Thread.Sleep(100);
            _screen.Update();

            var screenImage = _screen.ScreenImage;

            // Read the first few pixels
            var topLeftPixels = new List<object>();
            for (int x = 0; x < Math.Min(20, screenImage.Width); x++)
            {
                var pixel = screenImage[x, 0];
                topLeftPixels.Add(new { x, r = pixel.R, g = pixel.G, b = pixel.B, a = pixel.A });
            }

            // Scan for the first non-black metadata pixel (like the fixed GetDataFrameMeta does)
            int detectedXOffset = -1;
            DataFrameMeta foundMeta = DataFrameMeta.Empty;

            for (int x = 0; x < Math.Min(20, screenImage.Width); x++)
            {
                var pixel = screenImage[x, 0];
                var meta = FrameConfig.GetMeta(pixel);
                if (meta != DataFrameMeta.Empty)
                {
                    detectedXOffset = x;
                    foundMeta = meta;
                    break;
                }
            }

            // Also check the original (0,0) pixel for comparison
            var originalMetaPixel = screenImage[0, 0];
            var originalMeta = FrameConfig.GetMeta(originalMetaPixel);

            return Ok(new
            {
                ScreenRect = new { screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height },
                ScreenEnabled = _screen.Enabled,
                ImageWidth = screenImage.Width,
                ImageHeight = screenImage.Height,

                // Original pixel at (0,0) - for debugging
                OriginalMetaPixel = new { X = 0, originalMetaPixel.R, originalMetaPixel.G, originalMetaPixel.B },
                OriginalMetaIsEmpty = originalMeta == DataFrameMeta.Empty,

                // Scanned result - what the fixed code will find
                DetectedXOffset = detectedXOffset,
                DetectedMeta = detectedXOffset >= 0 ? new
                {
                    foundMeta.Hash,
                    foundMeta.Spacing,
                    Sizes = foundMeta.Sizes,
                    foundMeta.Rows,
                    foundMeta.Count,
                    IsEmpty = false
                } : new
                {
                    Hash = -1,
                    Spacing = 0,
                    Sizes = 0,
                    Rows = 0,
                    Count = 0,
                    IsEmpty = true
                },

                // Current state from configurator
                ConfiguratorXOffset = _frameConfigurator.MetaPixelXOffset,

                TopLeftPixels = topLeftPixels,
                ImageBase64Length = _frameConfigurator.ImageBase64?.Length ?? 0
            });
        }
        finally
        {
            _screen.Enabled = wasEnabled;
        }
    }

    /// <summary>
    /// Read pixel at a specific screen position (for debugging frame positions)
    /// </summary>
    [HttpGet("pixel")]
    public IActionResult GetPixel([FromQuery] int x = 0, [FromQuery] int y = 0, [FromQuery] int count = 1)
    {
        bool wasEnabled = _screen.Enabled;
        _screen.Enabled = true;

        try
        {
            System.Threading.Thread.Sleep(100);
            _screen.Update();

            var screenImage = _screen.ScreenImage;
            int maxX = Math.Min(x + count, screenImage.Width);
            int maxY = screenImage.Height;

            var pixels = new List<object>();
            for (int px = x; px < maxX; px++)
            {
                if (px >= 0 && px < screenImage.Width && y >= 0 && y < screenImage.Height)
                {
                    var p = screenImage[px, y];
                    int value = p.B | (p.G << 8) | (p.R << 16);
                    pixels.Add(new { px, y, r = p.R, g = p.G, b = p.B, a = p.A, value });
                }
            }

            // Also read the sentinel positions from frame_config if it exists
            object? sentinelInfo = null;
            if (FrameConfig.Exists())
            {
                DataFrameConfig config = FrameConfig.Load();
                if (config.Frames.Length > 0)
                {
                    DataFrame first = config.Frames[0];
                    DataFrame last = config.Frames[^1];

                    var fp = screenImage[first.X, first.Y];
                    var lp = screenImage[last.X, last.Y];

                    sentinelInfo = new
                    {
                        FirstFrame = new { first.Index, first.X, first.Y, r = fp.R, g = fp.G, b = fp.B, value = fp.B | (fp.G << 8) | (fp.R << 16) },
                        LastFrame = new { last.Index, last.X, last.Y, r = lp.R, g = lp.G, b = lp.B, value = lp.B | (lp.G << 8) | (lp.R << 16) },
                        ExpectedFirstValue = 0,
                        ExpectedLastValue = 2000001
                    };
                }
            }

            return Ok(new { Pixels = pixels, Sentinel = sentinelInfo });
        }
        finally
        {
            _screen.Enabled = wasEnabled;
        }
    }

    /// <summary>
    /// List all available resolution-specific frame configs
    /// </summary>
    [HttpGet("resolutions")]
    public IActionResult GetResolutions()
    {
        var configs = FrameConfig.ListResolutionConfigs();
        _screen.GetRectangle(out var currentRect);

        DataFrameConfig? active = null;
        if (FrameConfig.Exists())
        {
            try { active = FrameConfig.Load(); }
            catch (Exception)
            {
                // ignore corrupt config - will auto-detect on next run
                active = null;
            }
        }

        return Ok(new
        {
            CurrentResolution = new { currentRect.Width, currentRect.Height },
            ActiveConfig = active.HasValue
                ? new { active.Value.Rect.Width, active.Value.Rect.Height }
                : null,
            AvailableConfigs = configs.Select(c => new
            {
                c.Width,
                c.Height,
                Label = $"{c.Width}x{c.Height}",
                IsActive = active.HasValue &&
                           active.Value.Rect.Width == c.Width &&
                           active.Value.Rect.Height == c.Height
            })
        });
    }

    /// <summary>
    /// Get the current screenshot as base64 image
    /// </summary>
    [HttpGet("screenshot")]
    public IActionResult GetScreenshot()
    {
        if (_frameConfigurator == null)
        {
            return NotFound(new { Message = "FrameConfigurator unavailable (WoW not running)" });
        }

        var imageDataUri = _frameConfigurator.ImageDataUri;

        if (string.IsNullOrEmpty(imageDataUri))
        {
            return NotFound(new { Message = "No screenshot available" });
        }

        // Return as HTML img for easy viewing
        return Content($"<html><body><h1>WoW Screen Capture</h1><img src='{imageDataUri}' style='max-width: 100%;' /></body></html>", "text/html");
    }
}
