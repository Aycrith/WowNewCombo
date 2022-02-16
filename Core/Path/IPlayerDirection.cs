using System;
using System.Numerics;
using System.Threading;

namespace Core
{
    public interface IPlayerDirection
    {
        void SetDirection(float desiredDirection, Vector3 point, string source);

        void SetDirection(float desiredDirection, Vector3 point, string source, int ignoreDistance, CancellationTokenSource cts);

        DateTime LastSetDirection { get; }
    }
}