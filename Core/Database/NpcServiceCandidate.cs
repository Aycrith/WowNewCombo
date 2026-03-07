using SharedLib.Data;

using System;
using System.Numerics;

namespace Core.Database;

public enum NpcServiceKind
{
    None,
    Vendor,
    Repair,
    Innkeeper,
    Trainer,
    FlightMaster
}

public static class NpcServiceKindExtensions
{
    public static string ToStringF(this NpcServiceKind value) => value switch
    {
        NpcServiceKind.None => nameof(NpcServiceKind.None),
        NpcServiceKind.Vendor => nameof(NpcServiceKind.Vendor),
        NpcServiceKind.Repair => nameof(NpcServiceKind.Repair),
        NpcServiceKind.Innkeeper => nameof(NpcServiceKind.Innkeeper),
        NpcServiceKind.Trainer => nameof(NpcServiceKind.Trainer),
        NpcServiceKind.FlightMaster => nameof(NpcServiceKind.FlightMaster),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}

public enum NpcServiceCandidateSource
{
    None,
    AreaCurated,
    MapWideSearch,
    HardCodedFallback
}

public static class NpcServiceCandidateSourceExtensions
{
    public static string ToStringF(this NpcServiceCandidateSource value) => value switch
    {
        NpcServiceCandidateSource.None => nameof(NpcServiceCandidateSource.None),
        NpcServiceCandidateSource.AreaCurated => nameof(NpcServiceCandidateSource.AreaCurated),
        NpcServiceCandidateSource.MapWideSearch => nameof(NpcServiceCandidateSource.MapWideSearch),
        NpcServiceCandidateSource.HardCodedFallback => nameof(NpcServiceCandidateSource.HardCodedFallback),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}

public readonly record struct NpcServiceCandidate(
    NpcServiceKind ServiceKind,
    NpcServiceCandidateSource Source,
    int Entry,
    string Name,
    Vector3 WorldPosition,
    Vector3 MapPosition,
    NpcFlags Flags,
    string Description)
{
    public string IdentityKey => $"{(int)ServiceKind}:{Entry}:{Name}";
}
