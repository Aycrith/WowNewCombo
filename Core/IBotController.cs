using Core.GOAP;
using System;
using System.Collections.Generic;
using Core.Session;
using Game;

namespace Core
{
    public interface IBotController
    {
        DataConfig DataConfig { get; }
        AddonReader AddonReader { get; }
        WowScreen WowScreen { get; }

        string SelectedClassFilename { get; set; }
        string? SelectedPathFilename { get; set; }
        ClassConfiguration? ClassConfig { get; set; }
        GoapAgent? GoapAgent { get; set; }
        RouteInfo? RouteInfo { get; set; }
        ActionBarPopulator? ActionBarPopulator { get; set; }

        ExecGameCommand ExecGameCommand { get; }
        IGrindSessionDAO GrindSessionDAO { get; }

        IImageProvider? MinimapImageFinder { get; }

        event Action? ProfileLoaded;
        event Action? StatusChanged;

        double AvgScreenLatency { get; }
        double AvgNPCLatency { get; }

        void ToggleBotStatus();

        void MinimapNodeFound();

        void Shutdown();

        bool IsBotActive { get; }

        IEnumerable<string> ClassFiles();

        IEnumerable<string> PathFiles();

        void LoadClassProfile(string classFilename);

        void LoadPathProfile(string pathFilename);

        void OverrideClassConfig(ClassConfiguration classConfig);
    }
}