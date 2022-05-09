using Core.GOAP;
using System;
using System.Collections.Generic;
using System.Threading;
using Core.Session;
using Game;

namespace Core
{
    public class ConfigBotController : IBotController
    {
        public DataConfig DataConfig => throw new NotImplementedException();
        public AddonReader AddonReader => throw new NotImplementedException();
        public Thread? screenshotThread => throw new NotImplementedException();
        public Thread addonThread => throw new NotImplementedException();
        public Thread? botThread { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public GoapAgent? GoapAgent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RouteInfo? RouteInfo { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public WowScreen WowScreen => throw new NotImplementedException();
        public WowProcessInput WowProcessInput => throw new NotImplementedException();
        public ConfigurableInput? ConfigurableInput { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IGrindSession GrindSession => throw new NotImplementedException();
        public IGrindSessionHandler GrindSessionHandler => throw new NotImplementedException();
        public string SelectedClassFilename { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string? SelectedPathFilename { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsBotActive => false;

        public IImageProvider? MinimapImageFinder => throw new NotImplementedException();
        public ClassConfiguration? ClassConfig { get => null; set => throw new NotImplementedException(); }

        public ActionBarPopulator? ActionBarPopulator { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ExecGameCommand ExecGameCommand => throw new NotImplementedException();

        public event Action? ProfileLoaded;
        public event Action? StatusChanged;

        public double AvgScreenLatency => throw new NotImplementedException();
        public double AvgNPCLatency => throw new NotImplementedException();

        public void Shutdown()
        {

        }

        public void StopBot()
        {
            StatusChanged?.Invoke();
            throw new NotImplementedException();
        }

        public void MinimapNodeFound()
        {
            throw new NotImplementedException();
        }

        public void ToggleBotStatus()
        {
            throw new NotImplementedException();
        }

        public void LoadClassProfile(string classFilename)
        {
            ProfileLoaded?.Invoke();
            throw new NotImplementedException();
        }

        public List<string> ClassFileList()
        {
            throw new NotImplementedException();
        }

        public List<string> PathFileList()
        {
            throw new NotImplementedException();
        }

        public void LoadPathProfile(string pathFilename)
        {
            ProfileLoaded?.Invoke();
            throw new NotImplementedException();
        }

        public void OverrideClassConfig(ClassConfiguration classConfiguration)
        {
            throw new NotImplementedException();
        }
    }
}