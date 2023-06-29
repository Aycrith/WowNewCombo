using System;
using System.Threading;

namespace SharedLib;

public sealed class NpcResetEvent : ManualResetEventSlim, INpcResetEvent
{
    public NpcResetEvent() : base() { }
}

public interface INpcResetEvent : IDisposable
{
    void Reset();
    void Wait();
    void Set();
}
