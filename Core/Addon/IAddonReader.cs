using System;

namespace Core;

public interface IAddonReader
{
    double AvgUpdateLatency { get; }

    string TargetName { get; }

    event Action? AddonDataChanged;

    void FetchData();
    void FullReset();

    void Update();
    void UpdateUI();
    void SessionReset();

    int GetInt(int index);
}