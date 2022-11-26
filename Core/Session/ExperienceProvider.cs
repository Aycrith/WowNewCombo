using static System.IO.Path;
using static System.IO.File;
using static Newtonsoft.Json.JsonConvert;

namespace Core.Session
{
    public static class ExperienceProvider
    {
        public static int[] GetExperienceList(DataConfig dataConfig)
        {
            string json = ReadAllText(Join(dataConfig.ExpExperience, "exp.json"));
            return DeserializeObject<int[]>(json)!;
        }
    }
}
