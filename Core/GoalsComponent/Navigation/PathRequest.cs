using System;
using System.Numerics;

namespace Core.Goals;

internal readonly struct PathRequest
{
    public readonly int MapId;
    public readonly Vector3 StartW;
    public readonly Vector3 EndW;
    public readonly float Distance;
    public readonly Action<PathResult> Callback;
    public readonly DateTime Time;

    public PathRequest(int mapId, Vector3 startW, Vector3 endW, float distance, Action<PathResult> callback)
    {
        MapId = mapId;
        StartW = startW;
        EndW = endW;
        Distance = distance;
        Callback = callback;
        Time = DateTime.UtcNow;
    }
}
