using System.IO;
using Newtonsoft.Json;

public static class AddonConfigMeta
{
    public const int Version = 1;
    public const string DefaultFileName = "addon_config.json";
}

public class AddonConfig
{
    public int Version = AddonConfigMeta.Version;

    public string InstallPath { get; set; }
    public string Author { get; set; }
    public string Title { get; set; }
    public string Command { get; set; }

    [JsonIgnore]
    public string CommandFlush => Command + "flush";

    public bool IsDefault()
    {
        return string.IsNullOrEmpty(InstallPath) ||
            string.IsNullOrEmpty(Author) ||
            string.IsNullOrEmpty(Title) ||
            string.IsNullOrEmpty(Command);
    }

    public static AddonConfig Load()
    {
        if (Exists())
        {
            var loaded = JsonConvert.DeserializeObject<AddonConfig>(File.ReadAllText(AddonConfigMeta.DefaultFileName));
            if (loaded.Version == AddonConfigMeta.Version)
                return loaded;
        }

        return new AddonConfig();
    }

    public static bool Exists()
    {
        return File.Exists(AddonConfigMeta.DefaultFileName);
    }

    public static void Delete()
    {
        if (Exists())
        {
            File.Delete(AddonConfigMeta.DefaultFileName);
        }
    }

    public void Save()
    {
        File.WriteAllText(AddonConfigMeta.DefaultFileName, JsonConvert.SerializeObject(this));
    }
}