using CommandLine;
using Core;

namespace HeadlessServer
{
    public class RunOptions
    {
        [Value(0,
            MetaName = "ClassConfig file",
            Required = true,
            HelpText = "ClassConfiguration file example: Warrior_1.json")]
        public string? ClassConfig { get; set; }

        [Option('m',
            "mode",
            Required = false,
            Default = nameof(StartupConfigPathing.Types.Local),
            HelpText = $@"PPather service: 
                {nameof(StartupConfigPathing.Types.Local)} | 
                {nameof(StartupConfigPathing.Types.RemoteV1)} | 
                {nameof(StartupConfigPathing.Types.RemoteV3)}")]
        public string? Mode { get; set; }

        [Option('p',
            "pid",
            Required = false,
            Default = -1,
            HelpText = $"World of Warcraft Process Id")]
        public int Pid { get; set; }

        [Option("hostv1",
            Required = false,
            Default = "localhost",
            HelpText = $"PPather Remote V1 host")]
        public string? Hostv1 { get; set; }

        [Option("portv1",
            Required = false,
            Default = 5001,
            HelpText = $"PPather Remote V1 port")]
        public int Portv1 { get; set; }

        [Option("hostv3",
            Required = false,
            Default = "127.0.0.1",
            HelpText = $"PPather Remote V3 host")]
        public string? Hostv3 { get; set; }

        [Option("portv3",
            Required = false,
            Default = 47111,
            HelpText = $"PPather Remote V3 port")]
        public int Portv3 { get; set; }
    }
}
