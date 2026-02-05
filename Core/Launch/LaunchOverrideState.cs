using System;

namespace Core.Launch;

/// <summary>
/// In-memory overrides for advanced users (not persisted).
/// These overrides affect launch readiness gating and bot start permissions.
/// </summary>
public sealed class LaunchOverrideState
{
    private readonly object gate = new();

    public event Action? Changed;

    public bool AllowStartWithWarnings { get; private set; }

    public bool SkipNavigationChecks { get; private set; }

    public bool SkipKeybindingChecks { get; private set; }

    public bool SkipActionBarChecks { get; private set; }

    public void Reset()
    {
        lock (gate)
        {
            AllowStartWithWarnings = false;
            SkipNavigationChecks = false;
            SkipKeybindingChecks = false;
            SkipActionBarChecks = false;
        }

        Changed?.Invoke();
    }

    public void SetAllowStartWithWarnings(bool value)
    {
        bool changed;
        lock (gate)
        {
            changed = AllowStartWithWarnings != value;
            AllowStartWithWarnings = value;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void SetSkipNavigationChecks(bool value)
    {
        bool changed;
        lock (gate)
        {
            changed = SkipNavigationChecks != value;
            SkipNavigationChecks = value;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void SetSkipKeybindingChecks(bool value)
    {
        bool changed;
        lock (gate)
        {
            changed = SkipKeybindingChecks != value;
            SkipKeybindingChecks = value;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void SetSkipActionBarChecks(bool value)
    {
        bool changed;
        lock (gate)
        {
            changed = SkipActionBarChecks != value;
            SkipActionBarChecks = value;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }
}

