using System;

namespace Core;

/// <summary>
/// Safe no-op addon data provider for configuration mode or degraded environments.
/// </summary>
public sealed class NullAddonDataProvider : IAddonDataProvider
{
    private int[] data = Array.Empty<int>();

    public int[] Data => data;

    public void InitFrames(DataFrame[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            data = Array.Empty<int>();
            return;
        }

        data = new int[frames.Length];
    }

    public void UpdateData()
    {
        // No-op.
    }

    public void Dispose()
    {
        // No-op.
    }
}

