using Core.GOAP;
using System;
using System.Collections.Generic;
using Core.Session;
using Game;

namespace Core
{
    public class ConfigBotController : IBotController
    {
        public AddonReader AddonReader => throw new NotImplementedException();
        public GoapAgent? GoapAgent => throw new NotImplementedException();
        public RouteInfo? RouteInfo => throw new NotImplementedException();
        public WowScreen WowScreen => throw new NotImplementedException();
        public IGrindSessionDAO GrindSessionDAO => throw new NotImplementedException();
        public string SelectedClassFilename => throw new NotImplementedException();
        public string? SelectedPathFilename => throw new NotImplementedException();

        public ClassConfiguration? ClassConfig => null;

        public bool IsBotActive => false;

        public double AvgScreenLatency => throw new NotImplementedException();
        public double AvgNPCLatency => throw new NotImplementedException();

        public event Action? ProfileLoaded;
        public event Action? StatusChanged;

        public void Shutdown()
        {

        }

        public void MinimapNodeFound()
        {
            throw new NotImplementedException();
        }

        public void ToggleBotStatus()
        {
            StatusChanged?.Invoke();
            throw new NotImplementedException();
        }

        public IEnumerable<string> ClassFiles()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> PathFiles()
        {
            throw new NotImplementedException();
        }

        public void LoadClassProfile(string classFilename)
        {
            ProfileLoaded?.Invoke();
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