using static System.IO.Path;
using static System.IO.File;
using static Newtonsoft.Json.JsonConvert;

namespace Core.Session;

public static class ExperienceProvider
{
    public static int MaxLevel = 60;

    public static int[] Get(DataConfig dataConfig)
    {
        string json = ReadAllText(Join(dataConfig.ExpExperience, "exp.json"));

        int[] array = DeserializeObject<int[]>(json) ?? [];

        MaxLevel = array.Length + 1;

        return array;
    }
}
