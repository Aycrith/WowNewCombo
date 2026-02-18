using System.Numerics;

namespace Core;

public interface IMountHandler
{
    void MountUp();
    void Dismount();

    bool CanMount();
    bool ShouldMount(Vector3 targetW);
    bool IsMounted();

    /// <summary>
    /// Optimizes travel speed by mounting if possible, or unstealthing for travel if mounting is not available.
    /// </summary>
    void OptimizeTravelSpeed(float totalDistance);
}
