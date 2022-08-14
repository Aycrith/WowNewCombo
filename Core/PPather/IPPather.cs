using PPather.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;

namespace Core
{
    public interface IPPather
    {
        ValueTask<Vector3[]> FindRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo);
        ValueTask DrawLines(List<LineArgs> lineArgs);
        ValueTask DrawSphere(SphereArgs args);
    }
}