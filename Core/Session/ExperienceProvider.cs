using System.IO;
using Newtonsoft.Json;

namespace Core.Session
{
    public static class ExperienceProvider
    {
        private const string Version = "exp_tbc.json";

        public static int[] GetExperienceList()
        {
            DataConfig dataConfig = new();
            var json = File.ReadAllText(Path.Join(dataConfig.Experience, Version));
            return JsonConvert.DeserializeObject<int[]>(json);
        }
    }
}
