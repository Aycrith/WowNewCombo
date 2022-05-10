using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core
{
    public class Wait
    {
        private readonly AutoResetEvent globalTimeChange;

        public Wait(AutoResetEvent globalTimeChange)
        {
            this.globalTimeChange = globalTimeChange;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            globalTimeChange.WaitOne();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Till(int timeoutMs, Func<bool> interrupt)
        {
            DateTime start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                if (interrupt())
                    return false;

                Update();
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (bool timeout, double elapsedMs) Until(int timeoutMs, Func<bool> interrupt, Action? repeat = null)
        {
            DateTime start = DateTime.UtcNow;
            double elapsedMs;
            while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
            {
                repeat?.Invoke();
                if (interrupt())
                    return (false, elapsedMs);

                Update();
            }

            return (true, elapsedMs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void While(Func<bool> condition)
        {
            while (condition())
            {
                Update();
            }
        }
    }
}
