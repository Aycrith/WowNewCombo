namespace Core;

public readonly struct WaitResult
{
    private readonly bool timeout;
    private readonly float elapsedMs;

    // Timeout = true
    // elapsed = negative

    // Timeout = false
    // elapsed = positive

    public WaitResult(bool timeout, float elapsedMs)
    {
        this.timeout = timeout;
        this.elapsedMs = elapsedMs;
    }

    public void Deconstruct(out bool timeout, out float elapsedMs)
    {
        timeout = this.timeout;
        elapsedMs = this.elapsedMs;
    }
}
