using static System.IO.Path;
using static System.IO.File;
using Newtonsoft.Json;

public static class DataConfigMeta
{
    public const int Version = 12;
    public const string DefaultFileName = "data_config.json";
}

public sealed class DataConfig
{
    public int Version = DataConfigMeta.Version;
    public string Root { get; set; } = Join("..", "json");

    [JsonIgnore]
    public string Class => Join(Root, "class");
    [JsonIgnore]
    public string Path => Join(Root, "path");
    [JsonIgnore]
    public string ExpDbc => Join(Root, "dbc", Exp);
    [JsonIgnore]
    public string PathInfo => Join(Root, "PathInfo");
    [JsonIgnore]
    public string MPQ => Join(Root, "MPQ");
    [JsonIgnore]
    public string ExpArea => Join(Root, "area", Exp);
    [JsonIgnore]
    public string PPather => Join(Root, "PPather");
    [JsonIgnore]
    public string ExpHistory => Join(Root, "History", Exp);
    [JsonIgnore]
    public string ExpExperience => Join(Root, "experience", Exp);

    // at runtime - determined from the running exe file version
    [JsonIgnore]
    public string Exp { get; set; } = "tbc"; // hardcoded default

    public static DataConfig Load()
    {
        if (Exists(DataConfigMeta.DefaultFileName))
        {
            var loaded = JsonConvert.DeserializeObject<DataConfig>(ReadAllText(DataConfigMeta.DefaultFileName));
            if (loaded.Version == DataConfigMeta.Version)
                return loaded;
        }

        return new DataConfig().Save();
    }

    public static DataConfig Load(string client)
    {
        if (Exists(DataConfigMeta.DefaultFileName))
        {
            var loaded = JsonConvert.DeserializeObject<DataConfig>(ReadAllText(DataConfigMeta.DefaultFileName));
            if (loaded.Version == DataConfigMeta.Version)
            {
                loaded.Exp = client;
                return loaded;
            }
        }

        DataConfig newConfig = new DataConfig().Save();
        newConfig.Exp = client;
        return newConfig;
    }

    private DataConfig Save()
    {
        WriteAllText(DataConfigMeta.DefaultFileName, JsonConvert.SerializeObject(this));

        return this;
    }
}