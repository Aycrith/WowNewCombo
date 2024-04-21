using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core;

public sealed class PathSettings
{
    public string PathFilename { get; set; } = string.Empty;
    public string? OverridePathFilename { get; set; } = string.Empty;
    public bool PathThereAndBack { get; set; } = true;
    public bool PathReduceSteps { get; set; }

    public Vector3[] Path = Array.Empty<Vector3>();

    public string FileName =>
        !string.IsNullOrEmpty(OverridePathFilename)
        ? OverridePathFilename
        : PathFilename;

    public List<string> Requirements = [];
    public Requirement[] RequirementsRuntime = [];

    private RecordInt globalTime = null!;
    private int canRunTime;
    private bool canRun;

    public void Init(RecordInt globalTime)
    {
        this.globalTime = globalTime;
    }

    public bool CanRun()
    {
        if (canRunTime == globalTime.Value)
            return canRun;

        canRunTime = globalTime.Value;

        ReadOnlySpan<Requirement> span = RequirementsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return canRun = false;
        }

        return canRun = true;
    }
}
