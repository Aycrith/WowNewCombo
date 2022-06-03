using Core.GOAP;
using System;
using System.Collections.Generic;
using Core.Session;
using Game;

namespace Core
{
    public interface IBotController
    {
        bool IsBotActive { get; }
        AddonReader AddonReader { get; }
        WowScreen WowScreen { get; }
        string SelectedClassFilename { get; }
        string? SelectedPathFilename { get; }
        ClassConfiguration? ClassConfig { get; }
        GoapAgent? GoapAgent { get; }
        RouteInfo? RouteInfo { get; }
        IGrindSessionDAO GrindSessionDAO { get; }
        double AvgScreenLatency { get; }
        double AvgNPCLatency { get; }

        event Action? ProfileLoaded;
        event Action? StatusChanged;

        void ToggleBotStatus();

        void MinimapNodeFound();

        void Shutdown();

        IEnumerable<string> ClassFiles();

        IEnumerable<string> PathFiles();

        void LoadClassProfile(string classFilename);

        void LoadPathProfile(string pathFilename);

        void OverrideClassConfig(ClassConfiguration classConfig);
    }
}