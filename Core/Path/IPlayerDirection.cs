using System.Numerics;
using System.Threading;

namespace Core
{
    public interface IPlayerDirection
    {
        void SetDirection(float desiredDirection, Vector3 point);

        void SetDirection(float desiredDirection, Vector3 point, int ignoreDistance, CancellationTokenSource cts);
    }
}