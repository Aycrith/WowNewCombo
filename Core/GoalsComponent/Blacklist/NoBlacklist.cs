namespace Core
{
    public class NoBlacklist : IBlacklist
    {
        public bool Is() => false;
    }
}