using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core
{
    public class Wait
    {
        private readonly AutoResetEvent globalTime;
        private readonly CancellationTokenSource cts;

        public Wait(AutoResetEvent globalTime, CancellationTokenSource cts)
        {
            this.globalTime = globalTime;
            this.cts = cts;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            globalTime.WaitOne();
        }

        public void Fixed(int durationMs)
        {
            cts.Token.WaitHandle.WaitOne(durationMs);
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
        public WaitResult Until(int timeoutMs, Func<bool> interrupt)
        {
            DateTime start = DateTime.UtcNow;
            double elapsedMs;
            while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
            {
                if (interrupt())
                    return new(false, elapsedMs);

                Update();
            }

            return new(true, elapsedMs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WaitResult Until(int timeoutMs, Func<bool> interrupt, Action repeat)
        {
            DateTime start = DateTime.UtcNow;
            double elapsedMs;
            while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
            {
                repeat.Invoke();
                if (interrupt())
                    return new(false, elapsedMs);

                Update();
            }

            return new(true, elapsedMs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WaitResult UntilNot(int timeoutMs, Func<bool> interrupt)
        {
            DateTime start = DateTime.UtcNow;
            double elapsedMs;
            while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
            {
                if (!interrupt())
                    return new(false, elapsedMs);

                Update();
            }

            return new(true, elapsedMs);
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
