using Microsoft.AspNetCore.Components;
using System.Threading;
using System;

#nullable enable

namespace PathingAPI;

public abstract class ComponentWithCancellationToken : ComponentBase, IDisposable
{
    private CancellationTokenSource? _cts;

    protected CancellationToken Token => (_cts ??= new()).Token;

    public virtual void Dispose()
    {
        if (_cts == null)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;

        GC.SuppressFinalize(this);
    }
}