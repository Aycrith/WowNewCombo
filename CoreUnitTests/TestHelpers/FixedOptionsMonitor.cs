using Microsoft.Extensions.Options;

using System;

namespace CoreUnitTests.TestHelpers;

public sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    private readonly T value = value;

    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
