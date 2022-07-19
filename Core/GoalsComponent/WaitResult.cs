namespace Core
{
    public readonly struct WaitResult
    {
        public readonly bool timeout;
        public readonly double elapsedMs;

        public WaitResult(bool timeout, double elapsedMs)
        {
            this.timeout = timeout;
            this.elapsedMs = elapsedMs;
        }

        public void Deconstruct(out bool timeout, out double elapsedMs)
        {
            timeout = this.timeout;
            elapsedMs = this.elapsedMs;
        }
    }
}
