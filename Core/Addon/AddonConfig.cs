using Newtonsoft.Json;

using System;
using System.IO;

using static Newtonsoft.Json.JsonConvert;

namespace Core;

public static class AddonConfigMeta
{
    public const int Version = 1;
    public const string DefaultFileName = "addon_config.json";
}

public sealed class AddonConfig
{
    public int Version { get; init; } = AddonConfigMeta.Version;

    public string Author { get; set; } = "FreeHongKongMMO";
    public string CellSize { get; set; } = "4";
    public string Title { get; set; } = "DataToColor";
    public string Command { get; set; } = "dc";
    public bool ExcludeBag1FromAutoSell { get; set; }

    [JsonIgnore]
    public string CommandFlush => Command + "flush";

    [JsonIgnore]
    public string CommandBindings => Command + "bindings";

    [JsonIgnore]
    public string CommandNumberKeys => Command + "numberkeys";

    [JsonIgnore]
    public string CommandActions => Command + "actions";

    public bool IsDefault()
    {
        return
            string.IsNullOrEmpty(Author) ||
            string.IsNullOrEmpty(Title) ||
            string.IsNullOrEmpty(Command);
    }

    public static AddonConfig Load()
    {
        if (Exists())
        {
            try
            {
                AddonConfig? loaded = DeserializeObject<AddonConfig>(File.ReadAllText(GetPath()));
                if (loaded != null && loaded.Version == AddonConfigMeta.Version)
                {
                    return loaded;
                }
            }
            catch
            {
                // Corrupted/malformed JSON should not crash the server; fall back to defaults.
            }
        }

        return new AddonConfig();
    }

    public static bool Exists()
    {
        return File.Exists(GetPath());
    }

    public static void Delete()
    {
        if (Exists())
        {
            File.Delete(GetPath());
        }
    }

    public void Save()
    {
        File.WriteAllText(GetPath(), SerializeObject(this));
    }

    private static string GetPath()
    {
        return Path.Combine(AppContext.BaseDirectory, AddonConfigMeta.DefaultFileName);
    }
}
