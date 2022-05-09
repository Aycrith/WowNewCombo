using static System.IO.Path;
using static System.IO.File;
using Newtonsoft.Json;

public static class DataConfigMeta
{
    public const int Version = 9;
    public const string DefaultFileName = "data_config.json";
}

public class DataConfig
{
    public int Version = DataConfigMeta.Version;
    public string Root { get; set; } = "../json/";

    [JsonIgnore]
    public string Class => Join(Root, "class/");
    [JsonIgnore]
    public string Path => Join(Root, "path/");
    [JsonIgnore]
    public string Dbc => Join(Root, "dbc/");
    [JsonIgnore]
    public string WorldToMap => Join(Root, "WorldToMap/");
    [JsonIgnore]
    public string PathInfo => Join(Root, "PathInfo/");
    [JsonIgnore]
    public string MPQ => Join(Root, "MPQ/");
    [JsonIgnore]
    public string Area => Join(Root, "area/");
    [JsonIgnore]
    public string PPather => Join(Root, "PPather/");
    [JsonIgnore]
    public string History => Join(Root, "History/");
    [JsonIgnore]
    public string Experience => Join(Root, "experience/");
    [JsonIgnore]
    public string AuctionHouse => Join(Root, "ah/");

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

    private DataConfig Save()
    {
        WriteAllText(DataConfigMeta.DefaultFileName, JsonConvert.SerializeObject(this));

        return this;
    }
}