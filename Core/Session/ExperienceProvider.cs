using System.IO;
using Newtonsoft.Json;

namespace Core.Session
{
    public static class ExperienceProvider
    {
        public static int[] GetExperienceList(DataConfig dataConfig)
        {
            var json = File.ReadAllText(Path.Join(dataConfig.ExpExperience, "exp.json"));
            return JsonConvert.DeserializeObject<int[]>(json);
        }
    }
}
