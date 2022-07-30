using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;

namespace Core
{
    public static class FrameConfigMeta
    {
        public const int Version = 3;
        public const string DefaultFilename = "frame_config.json";
    }

    public static class FrameConfig
    {
        public static bool Exists()
        {
            return File.Exists(FrameConfigMeta.DefaultFilename);
        }

        public static bool IsValid(Rectangle rect, Version addonVersion)
        {
            try
            {
                var config = JsonConvert.DeserializeObject<DataFrameConfig>(File.ReadAllText(FrameConfigMeta.DefaultFilename));

                bool sameVersion = config.Version == FrameConfigMeta.Version;
                bool sameAddonVersion = config.addonVersion == addonVersion;
                bool sameRect = config.rect.Width == rect.Width && config.rect.Height == rect.Height;
                return sameAddonVersion && sameVersion && sameRect && config.frames.Length > 1;
            }
            catch
            {
                return false;
            }
        }

        public static DataFrame[] LoadFrames()
        {
            if (Exists())
            {
                var config = JsonConvert.DeserializeObject<DataFrameConfig>(File.ReadAllText(FrameConfigMeta.DefaultFilename));
                if (config.Version == FrameConfigMeta.Version)
                    return config.frames;
            }

            return Array.Empty<DataFrame>();
        }

        public static DataFrameMeta LoadMeta()
        {
            var config = JsonConvert.DeserializeObject<DataFrameConfig>(File.ReadAllText(FrameConfigMeta.DefaultFilename));
            if (config.Version == FrameConfigMeta.Version)
                return config.meta;

            return DataFrameMeta.Empty;
        }

        public static void Save(Rectangle rect, Version addonVersion, DataFrameMeta meta, DataFrame[] dataFrames)
        {
            DataFrameConfig config = new(FrameConfigMeta.Version, addonVersion, rect, meta, dataFrames);

            string json = JsonConvert.SerializeObject(config);
            File.WriteAllText(FrameConfigMeta.DefaultFilename, json);
        }

        public static void Delete()
        {
            if (Exists())
            {
                File.Delete(FrameConfigMeta.DefaultFilename);
            }
        }

        public static DataFrameMeta GetMeta(Color color)
        {
            int data, hash;
            data = hash = color.R * 65536 + color.G * 256 + color.B;

            if (hash == 0)
                return DataFrameMeta.Empty;

            // CELL_SPACING * 10000000 + CELL_SIZE * 100000 + 1000 * FRAME_ROWS + NUMBER_OF_FRAMES
            int spacing = (int)(data / 10000000f);
            data -= (10000000 * spacing);

            int size = (int)(data / 100000f);
            data -= (100000 * size);

            int rows = (int)(data / 1000f);
            data -= (1000 * rows);

            int count = data;

            return new DataFrameMeta(hash, spacing, size, rows, count);
        }

        public static DataFrame[] TryCreateFrames(DataFrameMeta meta, Bitmap bmp)
        {
            DataFrame[] frames = new DataFrame[meta.frames];
            frames[0] = new(0, 0, 0);

            for (int i = 1; i < meta.frames; i++)
            {
                if (TryGetNextPoint(bmp, i, frames[i].X, out int x, out int y))
                {
                    frames[i] = new(i, x, y);
                }
                else
                {
                    break;
                }
            }

            return frames;
        }

        private static bool TryGetNextPoint(Bitmap bmp, int i, int startX, out int x, out int y)
        {
            for (int xi = startX; xi < bmp.Width; xi++)
            {
                for (int yi = 0; yi < bmp.Height; yi++)
                {
                    Color pixel = bmp.GetPixel(xi, yi);
                    if (pixel.B == i && pixel.R == 0 && pixel.G == 0)
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
}