using System;

namespace Core.Hazard;

public static class HazardAnalytics
{
    public static float CalculateTemporalWeight(DateTime eventTimeUtc, int halfLifeDays)
    {
        if (halfLifeDays <= 0)
        {
            return 1.0f;
        }

        double ageInDays = (DateTime.UtcNow - eventTimeUtc).TotalDays;
        if (ageInDays <= 0)
        {
            return 1.0f;
        }

        double decayConstant = Math.Log(2) / halfLifeDays; // λ = ln(2) / t½
        return (float)Math.Exp(-decayConstant * ageInDays);
    }

    public static float GetEventTypeWeight(HazardEventType type)
    {
        return type switch
        {
            HazardEventType.Death => 10.0f,
            HazardEventType.Stuck => 5.0f,
            HazardEventType.UnexpectedAggro => 4.0f,
            HazardEventType.PathfindingFailure => 3.0f,
            HazardEventType.TargetEvade => 2.0f,
            HazardEventType.ManualMarker => 100.0f,
            _ => 1.0f
        };
    }

    public static float CalculateSeverityScore(HazardCluster cluster, int halfLifeDays)
    {
        float totalWeight = 0f;

        for (int i = 0; i < cluster.Events.Count; i++)
        {
            HazardEvent evt = cluster.Events[i];
            float typeWeight = GetEventTypeWeight(evt.Type);
            float recencyWeight = CalculateTemporalWeight(evt.Timestamp, halfLifeDays);
            totalWeight += typeWeight * recencyWeight;
        }

        float frequencyMultiplier = MathF.Log2(cluster.EventCount + 1);
        return totalWeight * MathF.Max(1.0f, frequencyMultiplier);
    }
}

