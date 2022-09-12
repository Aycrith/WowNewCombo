using Game;

namespace Core
{
    public sealed class StartupClientVersion
    {
        public ClientVersion Version { get; }

        public string Path { get; }

        public StartupClientVersion(WowProcess process)
        {
            switch (process.FileVersion.Major)
            {
                case 1:
                    Version = ClientVersion.SoM;
                    Path = "som";
                    break;
                case 2:
                    Version = ClientVersion.TBC;
                    Path = "tbc";
                    break;
                case 3:
                    Version = ClientVersion.Wrath;
                    Path = "wrath";
                    break;
                default:
                    Version = ClientVersion.Retail;
                    Path = "retail";
                    break;
            }
        }

    }
}
