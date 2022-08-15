using System;
using System.Numerics;

namespace Core
{
    public interface IRouteProvider
    {
        Vector3[] PathingRoute();

        DateTime LastActive { get; }

        bool HasNext();

        Vector3 NextMapPoint();
    }
}
