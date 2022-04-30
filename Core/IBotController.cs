using Core.GOAP;
using System;
using System.Collections.Generic;
using System.Threading;
using Core.Session;
using Game;

namespace Core
{
    public interface IBotController
    {
        DataConfig DataConfig { get; }
        AddonReader AddonReader { get; }
        Thread? screenshotThread { get; }
        Thread addonThread { get; }
        Thread? botThread { get; set; }
        GoapAgent? GoapAgent { get; set; }
        RouteInfo? RouteInfo { get; set; }
        WowScreen WowScreen { get; }
        WowProcessInput WowProcessInput { get; }
        ConfigurableInput? ConfigurableInput { get; set; }
        ClassConfiguration? ClassConfig { get; set; }
        IImageProvider? MinimapImageFinder { get; }

        ExecGameCommand ExecGameCommand { get; }

        ActionBarPopulator? ActionBarPopulator { get; set; }
        public IGrindSession GrindSession { get; }
        public IGrindSessionHandler GrindSessionHandler { get; }

        string SelectedClassFilename { get; set; }
        string? SelectedPathFilename { get; set; }

        event EmptyEvent? ProfileLoaded;
        event EmptyEvent? StatusChanged;

        double AvgScreenLatency { get; }
        double AvgNPCLatency { get; }

        void ToggleBotStatus();
        void StopBot();

        void MinimapNodeFound();

        void Shutdown();

        bool IsBotActive { get; }

        List<string> ClassFileList();

        void LoadClassProfile(string classFilename);

        List<string> PathFileList();

        void LoadPathProfile(string pathFilename);

        void OverrideClassConfig(ClassConfiguration classConfiguration);
    }
}