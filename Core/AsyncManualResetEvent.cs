using System.Threading;
using System.Threading.Tasks;
namespace Core;
public sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public AsyncManualResetEvent(bool set) { if (set) tcs.SetResult(true); }
    public Task WaitAsync() => tcs.Task;
    public void Set() => tcs.TrySetResult(true);
    public void Reset() { while (true) { var t = tcs; if (!t.Task.IsCompleted || Interlocked.CompareExchange(ref tcs, new(TaskCreationOptions.RunContinuationsAsynchronously), t) == t) return; } }
}
