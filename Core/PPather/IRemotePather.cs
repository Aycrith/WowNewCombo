using System;

namespace Core;

/// <summary>
/// Extends <see cref="IPPather"/> with connection awareness for remote
/// pathfinding services. Implemented by <see cref="RemotePathingAPIV3"/>.
/// </summary>
public interface IRemotePather : IPPather, IDisposable
{
    bool IsConnected { get; }
}
