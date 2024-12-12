using Core.GOAP;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Core.Goals;

public sealed class SafeSpotCollector : IDisposable
{
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;

    private readonly Timer timer;

    public Stack<Vector3> Locations { get; } = new();

    public SafeSpotCollector(
        PlayerReader playerReader,
        AddonBits bits)
    {
        this.playerReader = playerReader;
        this.bits = bits;

        timer = new(Update, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        timer.Dispose();
    }

    public void Update(object? obj)
    {
        if (bits.Combat())
            return;

        if (Locations.TryPeek(out var lastPos) &&
            lastPos == playerReader.MapPosNoZ)
            return;

        // TODO: might be a good idea to check distance to last location
        Locations.Push(playerReader.MapPosNoZ);
    }
}
