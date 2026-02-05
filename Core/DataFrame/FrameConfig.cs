using Newtonsoft.Json;

using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using System;
using System.IO;

namespace Core;

public static class FrameConfigMeta
{
    public const int Version = 4;
    public const string DefaultFilename = "frame_config.json";
}

public static class FrameConfig
{
    private static string GetPath()
    {
        return Path.Combine(AppContext.BaseDirectory, FrameConfigMeta.DefaultFilename);
    }

    public static bool Exists()
    {
        return File.Exists(GetPath());
    }

    public static bool IsValid(Rectangle rect, Version addonVersion)
    {
        try
        {
            var config = Load();

            bool sameVersion = config.Version == FrameConfigMeta.Version;
            bool sameAddonVersion = config.AddonVersion == addonVersion;
            bool sameRect = config.Rect.Width == rect.Width && config.Rect.Height == rect.Height;
            return sameAddonVersion && sameVersion && sameRect && config.Frames.Length > 1;
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
        File.WriteAllText(GetPath(), json);
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
