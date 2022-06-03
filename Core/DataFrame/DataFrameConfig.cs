using System;
using System.Drawing;

namespace Core
{
    public readonly struct DataFrameConfig
    {
        public int Version { get; }
        public Version addonVersion { get; }
        public Rectangle rect { get; }
        public DataFrameMeta meta { get; }
        public DataFrame[] frames { get; }

        public DataFrameConfig(int version, Version addonVersion, Rectangle rect, DataFrameMeta meta, DataFrame[] frames)
        {
            Version = version;
            this.addonVersion = addonVersion;
            this.rect = rect;
            this.meta = meta;
            this.frames = frames;
        }
    }
}
