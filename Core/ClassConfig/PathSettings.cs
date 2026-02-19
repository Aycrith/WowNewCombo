using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Core;

public sealed class PathSettings
{
    public int Id { get; set; }
    public string PathFilename { get; set; } = string.Empty;
    public string? OverridePathFilename { get; set; } = string.Empty;
    public bool PathThereAndBack { get; set; } = true;
    public bool PathReduceSteps { get; set; }
    public int ExpectedUIMapId { get; set; }

    public Vector3[] Path = Array.Empty<Vector3>();

    public string FileName =>
        !string.IsNullOrEmpty(OverridePathFilename)
        ? OverridePathFilename
        : PathFilename;

    public List<string> Requirements = [];
    public Requirement[] RequirementsRuntime = [];

    private RecordInt globalTime = null!;
    private PlayerReader playerReader = null!;

    private int canRunTime;
    private bool canRun;
    private int canRunRequirementsOnlyTime;
    private bool canRunRequirementsOnly;

    public List<string> SideActivityRequirements = [];
    public Requirement[] SideActivityRequirementsRuntime = [];

    private int canSideActivityTime;
    private bool canSideActivity;

    private int resolvedExpectedUiMapId;
    public string ExpectedAreaName { get; private set; } = string.Empty;
    public int EffectiveExpectedUIMapId => resolvedExpectedUiMapId;

    public bool PathFinished() => Finished();
    public Func<bool> Finished = () => true;

    public void Init(RecordInt globalTime, PlayerReader playerReader, int id)
    {
        this.globalTime = globalTime;
        this.playerReader = playerReader;
        Id = Id == default ? id : Id;
    }

    public void ResolveExpectedUiMapId(WorldMapAreaDB worldMapAreaDB, ILogger logger)
    {
        if (ExpectedUIMapId > 0)
        {
            resolvedExpectedUiMapId = ExpectedUIMapId;
            if (worldMapAreaDB.TryGet(resolvedExpectedUiMapId, out WorldMapArea area))
            {
                ExpectedAreaName = area.AreaName;
            }
            return;
        }

        (int uiMapId, string areaName) inferred = InferExpectedUiMapId(worldMapAreaDB, FileName);
        if (inferred.uiMapId > 0)
        {
            resolvedExpectedUiMapId = inferred.uiMapId;
            ExpectedAreaName = inferred.areaName;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "[PathSettings      ] Inferred route zone {AreaName} (UIMapId={UIMapId}) from {FileName}",
                    ExpectedAreaName,
                    resolvedExpectedUiMapId,
                    FileName);
            }
        }
    }

    public bool CanRun()
    {
        if (canRunTime == globalTime.Value)
            return canRun;

        canRunTime = globalTime.Value;

        if (!CanRunRequirementsOnly())
        {
            return canRun = false;
        }

        if (resolvedExpectedUiMapId > 0)
        {
            int currentUiMapId = playerReader.UIMapId.Value;
            if (currentUiMapId > 0 &&
                currentUiMapId != resolvedExpectedUiMapId)
            {
                return canRun = false;
            }
        }

        return canRun = true;
    }

    public bool CanRunRequirementsOnly()
    {
        if (canRunRequirementsOnlyTime == globalTime.Value)
            return canRunRequirementsOnly;

        canRunRequirementsOnlyTime = globalTime.Value;

        ReadOnlySpan<Requirement> span = RequirementsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return canRunRequirementsOnly = false;
        }

        return canRunRequirementsOnly = true;
    }

    public bool IsZoneMismatch()
    {
        int currentUiMapId = playerReader.UIMapId.Value;
        return resolvedExpectedUiMapId > 0 &&
               currentUiMapId > 0 &&
               currentUiMapId != resolvedExpectedUiMapId;
    }

    public bool CanRunSideActivity()
    {
        if (canSideActivityTime == globalTime.Value)
            return canSideActivity;

        canSideActivityTime = globalTime.Value;

        ReadOnlySpan<Requirement> span = SideActivityRequirementsRuntime;
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].HasRequirement())
                return canSideActivity = false;
        }

        return canSideActivity = true;
    }

    public int GetDistanceXYFromPath()
    {
        if (Path.Length == 0)
            return int.MaxValue;

        ReadOnlySpan<Vector3> path = Path;
        Vector2 playerPosition = playerReader.WorldPos.AsVector2();

        if (Path.Length == 1)
        {
            Vector3 a = WorldMapAreaDB.ToWorld_FlipXY(path[0], playerReader.WorldMapArea);
            return (int)Vector2.Distance(a.AsVector2(), playerPosition);
        }

        float distance = float.MaxValue;

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 a = WorldMapAreaDB.ToWorld_FlipXY(path[i - 1], playerReader.WorldMapArea);
            Vector3 b = WorldMapAreaDB.ToWorld_FlipXY(path[i], playerReader.WorldMapArea);

            Vector2 closestPoint = VectorExt.GetClosestPointOnLineSegment(a.AsVector2(), b.AsVector2(), playerPosition);
            float d = Vector2.Distance(closestPoint, playerPosition);
            if (d < distance)
                distance = d;
        }

        return (int)distance;
    }

    private static (int uiMapId, string areaName) InferExpectedUiMapId(
        WorldMapAreaDB worldMapAreaDB,
        string pathFilename)
    {
        if (string.IsNullOrWhiteSpace(pathFilename))
        {
            return (0, string.Empty);
        }

        string normalizedPath = NormalizeText(pathFilename);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return (0, string.Empty);
        }

        List<(int uiMapId, string areaName, string normalizedName)> matches = [];
        foreach (WorldMapArea area in worldMapAreaDB.Values)
        {
            if (area.UIMapId <= 0 || string.IsNullOrWhiteSpace(area.AreaName))
            {
                continue;
            }

            string normalizedAreaName = NormalizeAreaName(area.AreaName);
            if (string.IsNullOrWhiteSpace(normalizedAreaName))
            {
                continue;
            }

            if (ContainsWordPhrase(normalizedPath, normalizedAreaName))
            {
                matches.Add((area.UIMapId, area.AreaName, normalizedAreaName));
            }
        }

        if (matches.Count == 0)
        {
            return (0, string.Empty);
        }

        int longestNameLength = matches.Max(m => m.normalizedName.Length);
        List<(int uiMapId, string areaName, string normalizedName)> best = matches
            .Where(m => m.normalizedName.Length == longestNameLength)
            .ToList();

        if (best.Count != 1)
        {
            return (0, string.Empty);
        }

        return (best[0].uiMapId, best[0].areaName);
    }

    private static bool ContainsWordPhrase(string text, string phrase)
    {
        string paddedText = $" {text} ";
        string paddedPhrase = $" {phrase} ";
        return paddedText.Contains(paddedPhrase, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string NormalizeAreaName(string input)
    {
        string normalized = NormalizeText(input);
        if (normalized.StartsWith("c ", StringComparison.InvariantCultureIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    private static string NormalizeText(string input)
    {
        StringBuilder sb = new(input.Length);
        bool previousSpace = true;

        ReadOnlySpan<char> chars = input.AsSpan();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = char.ToLowerInvariant(chars[i]);
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                previousSpace = false;
                continue;
            }

            if (!previousSpace)
            {
                sb.Append(' ');
                previousSpace = true;
            }
        }

        return sb.ToString().Trim();
    }
}
