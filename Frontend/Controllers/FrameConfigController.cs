using Core;
using Game;

using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FrameConfigController : ControllerBase
{
    private readonly FrameConfigurator _frameConfigurator;
    private readonly IWowScreen _screen;

    public FrameConfigController(FrameConfigurator frameConfigurator, IWowScreen screen)
    {
        _frameConfigurator = frameConfigurator;
        _screen = screen;
    }

    /// <summary>
    /// Get the current frame configuration status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
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
                DetectedMeta = detectedXOffset >= 0 ? new { 
                    foundMeta.Hash, 
                    foundMeta.Spacing, 
                    Sizes = foundMeta.Sizes, 
                    foundMeta.Rows, 
                    foundMeta.Count,
                    IsEmpty = false
                } : new { 
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
    /// Get the current screenshot as base64 image
    /// </summary>
    [HttpGet("screenshot")]
    public IActionResult GetScreenshot()
    {
        var imageDataUri = _frameConfigurator.ImageDataUri;
        
        if (string.IsNullOrEmpty(imageDataUri))
        {
            return NotFound(new { Message = "No screenshot available" });
        }
        
        // Return as HTML img for easy viewing
        return Content($"<html><body><h1>WoW Screen Capture</h1><img src='{imageDataUri}' style='max-width: 100%;' /></body></html>", "text/html");
    }
}
