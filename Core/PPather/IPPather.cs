using PPather.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;

namespace Core
{
    public interface IPPather
    {
        Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo);
        Vector3[] FindWorldRoute(int uiMap, Vector3 worldFrom, Vector3 worldTo);
        ValueTask DrawLines(List<LineArgs> lineArgs);
        ValueTask DrawSphere(SphereArgs args);
    }
}