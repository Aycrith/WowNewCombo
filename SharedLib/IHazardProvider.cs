using System.Numerics;

namespace SharedLib;

public interface IHazardProvider
{
    float GetHazardCost(Vector3 position, float mapId);
}

