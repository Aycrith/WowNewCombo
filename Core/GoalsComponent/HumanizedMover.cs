using SharedLib.Humanization;

using System.Threading;

namespace Core.Goals;

/// <summary>
/// Adds humanization to movement by introducing micro-pauses and delays
/// to simulate natural human behavior.
/// </summary>
public sealed class HumanizedMover
{
    private readonly IHumanizationProvider? humanizationProvider;

    public HumanizedMover(IHumanizationProvider? humanizationProvider = null)
    {
        this.humanizationProvider = humanizationProvider;
    }

    /// <summary>
    /// Applies a micro-pause delay when reaching waypoints to simulate human behavior.
    /// Humans naturally pause briefly at decision points.
    /// </summary>
    /// <param name="token">Cancellation token</param>
    public void ApplyWaypointDelay(CancellationToken token)
    {
        if (humanizationProvider?.Enabled != true)
            return;

        // Small micro-pause between waypoints (100-300ms)
        // This simulates the natural hesitation humans have at decision points
        int delayMs = humanizationProvider.GetInterKeyDelayMs(100);
        delayMs = System.Math.Clamp(delayMs, 50, 500);

        if (delayMs > 50)
        {
            token.WaitHandle.WaitOne(delayMs);
        }
    }
}
