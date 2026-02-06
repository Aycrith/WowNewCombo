using Newtonsoft.Json;

using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core;

public static class FrameConfigMeta
{
    public const int Version = 4;
    public const string DefaultFilename = "frame_config.json";
    public const string ResolutionPrefix = "frame_config_";
    public const string ResolutionSuffix = ".json";
}

public static class FrameConfig
{
    private const int WidthTolerancePixels = 20;
    private const int HeightTolerancePixels = 100;

    private static string GetPath()
    {
        return Path.Combine(AppContext.BaseDirectory, FrameConfigMeta.DefaultFilename);
    }

    /// <summary>
    /// Gets the path for a resolution-specific config file.
    /// Example: frame_config_1920x1080.json
    /// </summary>
    public static string GetResolutionPath(int width, int height)
    {
        string filename = $"{FrameConfigMeta.ResolutionPrefix}{width}x{height}{FrameConfigMeta.ResolutionSuffix}";
        return Path.Combine(AppContext.BaseDirectory, filename);
    }

    /// <summary>
    /// Gets the path for a resolution-specific config in the source directory (BlazorServer/).
    /// </summary>
    private static string GetSourceResolutionPath(int width, int height)
    {
        string filename = $"{FrameConfigMeta.ResolutionPrefix}{width}x{height}{FrameConfigMeta.ResolutionSuffix}";
        // Walk up from bin/Debug/net10.0/ to find the project root
        string baseDir = AppContext.BaseDirectory;
        string? projectDir = FindProjectDirectory(baseDir);
        if (projectDir != null)
            return Path.Combine(projectDir, filename);

        return Path.Combine(baseDir, filename);
    }

    private static string? FindProjectDirectory(string startDir)
    {
        string? dir = startDir;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0)
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public static bool Exists()
    {
        return File.Exists(GetPath());
    }

    /// <summary>
    /// Checks if a resolution-specific config exists.
    /// </summary>
    public static bool ExistsForResolution(int width, int height)
    {
        return File.Exists(GetResolutionPath(width, height));
    }

    /// <summary>
    /// Lists all available resolution-specific configs.
    /// Returns tuples of (width, height, filePath).
    /// </summary>
    public static IReadOnlyList<(int Width, int Height, string Path)> ListResolutionConfigs()
    {
        string baseDir = AppContext.BaseDirectory;
        string pattern = $"{FrameConfigMeta.ResolutionPrefix}*{FrameConfigMeta.ResolutionSuffix}";
        List<(int, int, string)> results = new();

        foreach (string file in Directory.GetFiles(baseDir, pattern))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(file);
            string resPart = name.Replace(FrameConfigMeta.ResolutionPrefix, "");
            string[] parts = resPart.Split('x');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int w) &&
                int.TryParse(parts[1], out int h))
            {
                results.Add((w, h, file));
            }
        }

        return results;
    }

    public static bool IsValid(Rectangle rect, Version addonVersion)
    {
        try
        {
            var config = Load();

            bool sameVersion = config.Version == FrameConfigMeta.Version;
            bool sameAddonVersion = config.AddonVersion == addonVersion;
            bool similarWidth = Math.Abs(config.Rect.Width - rect.Width) <= WidthTolerancePixels;
            bool similarHeight = Math.Abs(config.Rect.Height - rect.Height) <= HeightTolerancePixels;
            bool sameRect = similarWidth && similarHeight;
            return sameAddonVersion && sameVersion && sameRect && config.Frames.Length > 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to activate a resolution-specific config for the given window rect.
    /// If a matching resolution config exists, copies it to the active frame_config.json.
    /// Returns true if a matching config was found and activated.
    /// </summary>
    public static bool TryActivateForResolution(Rectangle rect, Version addonVersion)
    {
        string resPath = GetResolutionPath(rect.Width, rect.Height);
        if (!File.Exists(resPath))
            return false;

        try
        {
            string json = File.ReadAllText(resPath);
            DataFrameConfig config = JsonConvert.DeserializeObject<DataFrameConfig>(json);

            if (config.Version != FrameConfigMeta.Version)
                return false;

            if (config.AddonVersion != addonVersion)
                return false;

            // Copy the resolution-specific config to the active path
            File.Copy(resPath, GetPath(), overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static DataFrameConfig Load()
    {
        return JsonConvert.DeserializeObject<DataFrameConfig>(File.ReadAllText(GetPath()));
    }

    public static DataFrame[] LoadFrames()
    {
        if (Exists())
        {
            var config = Load();
            if (config.Version == FrameConfigMeta.Version)
                return config.Frames;
        }

        return Array.Empty<DataFrame>();
    }

    public static DataFrameMeta LoadMeta()
    {
        var config = Load();
        if (config.Version == FrameConfigMeta.Version)
            return config.Meta;

        return DataFrameMeta.Empty;
    }

    public static void Save(Rectangle rect, Version addonVersion, DataFrameMeta meta, DataFrame[] dataFrames)
    {
        DataFrameConfig config = new(FrameConfigMeta.Version, addonVersion, rect, meta, dataFrames);

        string json = JsonConvert.SerializeObject(config);

        // Save to the active config path
        File.WriteAllText(GetPath(), json);

        // Also save a resolution-specific copy
        string resPath = GetResolutionPath(rect.Width, rect.Height);
        File.WriteAllText(resPath, json);

        // Try to also save the resolution-specific copy to the source project directory
        // so it persists across rebuilds
        try
        {
            string sourceResPath = GetSourceResolutionPath(rect.Width, rect.Height);
            if (sourceResPath != resPath)
            {
                string? sourceDir = Path.GetDirectoryName(sourceResPath);
                if (sourceDir != null && Directory.Exists(sourceDir))
                {
                    File.WriteAllText(sourceResPath, json);

                    // Also update the source frame_config.json
                    string? projectDir = FindProjectDirectory(AppContext.BaseDirectory);
                    if (projectDir != null)
                    {
                        string sourceDefaultPath = Path.Combine(projectDir, FrameConfigMeta.DefaultFilename);
                        File.WriteAllText(sourceDefaultPath, json);
                    }
                }
            }
        }
        catch
        {
            // Non-critical: source directory write failed
        }
    }

    public static void Delete()
    {
        if (Exists())
        {
            File.Delete(GetPath());
        }
    }

    public static DataFrameMeta GetMeta(Bgra32 color)
    {
        int hash = color.R * 65536 + color.G * 256 + color.B;
        if (hash == 0)
            return DataFrameMeta.Empty;

        // CELL_SPACING * 10000000 + CELL_SIZE * 100000 + 1000 * FRAME_ROWS + NUMBER_OF_FRAMES
        int spacing = hash / 10000000;
        int size = hash / 100000 % 100;
        int rows = hash / 1000 % 100;
        int count = hash % 1000;

        // Validate that the decoded values are within reasonable bounds.
        // Without this validation, random game pixels could be misinterpreted as valid metadata.
        // Valid ranges (based on addon design):
        // - Spacing: 0-5 (typically 0-2 pixels between cells)
        // - Size: 1-20 (pixel size per cell, typically 4-8)
        // - Rows: 10-100 (number of frame rows, typically 50)
        // - Count: 100-999 (number of frames, typically 300-400)
        // 
        // The addon uses format: CELL_SPACING * 10000000 + CELL_SIZE * 100000 + 1000 * FRAME_ROWS + NUMBER_OF_FRAMES
        // For example: 0 * 10000000 + 4 * 100000 + 50 * 1000 + 324 = 450324
        
        bool isValid = spacing >= 0 && spacing <= 5 &&    // Reasonable cell spacing
                       size >= 2 && size <= 20 &&          // Reasonable cell size (at least 2 pixels)
                       rows >= 10 && rows <= 100 &&        // Reasonable row count
                       count >= 50 && count <= 999;        // Reasonable frame count
        
        if (!isValid)
            return DataFrameMeta.Empty;

        return new DataFrameMeta(hash, spacing, size, rows, count);
    }

    public static DataFrame[] CreateFrames(DataFrameMeta meta, Image<Bgra32> bmp)
    {
        return CreateFrames(meta, bmp, 0, null);
    }
    
    public static DataFrame[] CreateFrames(DataFrameMeta meta, Image<Bgra32> bmp, int xOffset)
    {
        return CreateFrames(meta, bmp, xOffset, null);
    }
    
    public static DataFrame[] CreateFrames(DataFrameMeta meta, Image<Bgra32> bmp, int xOffset, ILogger? logger)
    {
        DataFrame[] frames = new DataFrame[meta.Count];
        frames[0] = new(0, xOffset, 0);  // First frame starts at the detected X offset

        int foundCount = 1;  // We already have frame 0
        for (int i = 1; i < meta.Count; i++)
        {
            if (TryGetNextPoint(bmp, i, frames[i - 1].X, out int x, out int y))
            {
                frames[i] = new(i, x, y);
                foundCount++;
            }
            else
            {
                // Frame not found - remaining frames will have default coordinates
                logger?.LogWarning("Frame {Index} not found. Previous frame at ({PrevX},{PrevY}). Image size: {W}x{H}", 
                    i, frames[i - 1].X, frames[i - 1].Y, bmp.Width, bmp.Height);
                
                // Log expected position for this frame
                int gridX = i / meta.Rows;
                int gridY = i % meta.Rows;
                int expectedX = gridX * meta.Sizes + xOffset;
                int expectedY = gridY * meta.Sizes;
                
                if (expectedX < bmp.Width && expectedY < bmp.Height)
                {
                    var p = bmp[expectedX, expectedY];
                    logger?.LogWarning("Frame {Index}: expected at grid({GridX},{GridY}) -> pixel({ExpX},{ExpY}), found RGB=({R},{G},{B}), expected B={Index}", 
                        i, gridX, gridY, expectedX, expectedY, p.R, p.G, p.B, i);
                }
                
                break;
            }
        }

        // Log summary
        if (foundCount < meta.Count)
        {
            logger?.LogError("Only found {Found}/{Total} frames (image size: {W}x{H}, X offset: {Offset})", 
                foundCount, meta.Count, bmp.Width, bmp.Height, xOffset);
        }
        else
        {
            logger?.LogInformation("Successfully found all {Count} frames", meta.Count);
        }

        return frames;
    }

    private static bool TryGetNextPoint(Image<Bgra32> bmp, int i, int startX, out int x, out int y)
    {
        // The addon encodes frame index using all 3 RGB channels in config mode
        // via the int() function: R=(i>>16)&255, G=(i>>8)&255, B=i&255
        // So frame 256 = RGB(0,1,0), frame 257 = RGB(0,1,1), etc.
        byte expectedR = (byte)((i >> 16) & 255);
        byte expectedG = (byte)((i >> 8) & 255);
        byte expectedB = (byte)(i & 255);
        
        for (int xi = startX; xi < bmp.Width; xi++)
        {
            for (int yi = 0; yi < bmp.Height; yi++)
            {
                Bgra32 pixel = bmp[xi, yi];
                if (pixel.R == expectedR && pixel.G == expectedG && pixel.B == expectedB)
                {
                    x = xi;
                    y = yi;
                    return true;
                }
            }
        }

        x = y = -1;
        return false;
    }
}
