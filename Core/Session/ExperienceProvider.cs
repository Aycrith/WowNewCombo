using System.IO;
using Newtonsoft.Json;

namespace Core.Session
{
    public static class ExperienceProvider
    {
        private const string Version = "exp_tbc.json";

        public static int[] GetExperienceList()
        {
            var dataConfig = new DataConfig();
            var json = File.ReadAllText($"{dataConfig.Experience}{Version}");
            var expList = JsonConvert.DeserializeObject<int[]>(json);
            return expList;
        }
    }
}
