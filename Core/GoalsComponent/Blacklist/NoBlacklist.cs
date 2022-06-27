namespace Core
{
    public class NoBlacklist : IBlacklist
    {
        public bool IsTargetBlacklisted() => false;
    }
}