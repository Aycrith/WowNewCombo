using Core.GOAP;

using System;
using System.Threading;

namespace Core.Goals;

public sealed class SafeSpotCollector : IDisposable
{
    private readonly PlayerReader playerReader;
    private readonly GoapAgentState state;
    private readonly AddonBits bits;

    private readonly Timer timer;

    public SafeSpotCollector(
        PlayerReader playerReader,
        GoapAgentState state,
        AddonBits bits)
    {
        this.playerReader = playerReader;
        this.state = state;
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

        if (state.SafeLocations.TryPeek(out var lastPos) &&
            lastPos == playerReader.MapPosNoZ)
            return;

        state.SafeLocations.Push(playerReader.MapPosNoZ);
    }
}
