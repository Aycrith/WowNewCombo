using System;
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

        public void Update()
        {
            globalTime.WaitOne();
        }

        public void Fixed(int durationMs)
        {
            cts.Token.WaitHandle.WaitOne(durationMs);
        }

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

        public void While(Func<bool> condition)
        {
            while (condition())
            {
                Update();
            }
        }
    }
}
