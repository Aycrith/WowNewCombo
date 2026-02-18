using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SharedLib.Humanization;
using SharedLib.InputSecurity;

namespace Core.Humanization;

/// <summary>
/// Analyzes current behavior patterns and calculates a detection risk score.
/// Higher scores indicate more bot-like behavior patterns.
/// </summary>
public sealed class DetectionRiskAnalyzer
{
    private readonly ILogger<DetectionRiskAnalyzer>? logger;
    private readonly HumanizationMetrics metrics;

    // Risk thresholds
    private const double LowRiskThreshold = 25.0;
    private const double MediumRiskThreshold = 50.0;
    private const double HighRiskThreshold = 75.0;

    public DetectionRiskAnalyzer(HumanizationMetrics metrics, ILogger<DetectionRiskAnalyzer>? logger = null)
    {
        this.metrics = metrics;
        this.logger = logger;
    }

    /// <summary>
    /// Analyzes current metrics and calculates an overall detection risk score (0-100).
    /// </summary>
    public RiskAssessment AnalyzeRisk(IHumanizationProvider? humanizationProvider)
    {
        var snapshot = metrics.GetSnapshot();
        var factors = new List<RiskFactor>();

        // Factor 1: Timing Regularity
        double timingRisk = AnalyzeTimingRegularity(snapshot);
        factors.Add(new RiskFactor("Timing Regularity", timingRisk, "Measures consistency of action timings"));

        // Factor 2: Input Security Coverage
        double securityRisk = AnalyzeInputSecurityCoverage(humanizationProvider);
        factors.Add(new RiskFactor("Input Security", securityRisk, "Coverage of detection vector mitigations"));

        // Factor 3: Session Duration
        double sessionRisk = AnalyzeSessionDuration(snapshot);
        factors.Add(new RiskFactor("Session Duration", sessionRisk, "Risk increases with session length"));

        // Factor 4: Action Density
        double densityRisk = AnalyzeActionDensity(snapshot);
        factors.Add(new RiskFactor("Action Density", densityRisk, "Actions per minute rate"));

        // Factor 5: Humanization Enabled
        double enabledRisk = humanizationProvider?.Enabled == true ? 0 : 30;
        factors.Add(new RiskFactor("Humanization Disabled", enabledRisk, "Penalty for disabled humanization"));

        // Calculate overall score
        double overallScore = factors.Average(f => f.Score);
        RiskLevel level = overallScore switch
        {
            < LowRiskThreshold => RiskLevel.Low,
            < MediumRiskThreshold => RiskLevel.Medium,
            < HighRiskThreshold => RiskLevel.High,
            _ => RiskLevel.Critical
        };

        var assessment = new RiskAssessment
        {
            OverallScore = overallScore,
            Level = level,
            Factors = factors.ToArray(),
            Timestamp = DateTime.UtcNow,
            Recommendations = GenerateRecommendations(factors, humanizationProvider)
        };

        logger?.LogDebug("[DetectionRiskAnalyzer] Risk score: {Score:F1} ({Level})", overallScore, level);

        return assessment;
    }

    #region Risk Factor Analysis

    private static double AnalyzeTimingRegularity(MetricsSnapshot snapshot)
    {
        if (snapshot.TotalKeyPresses < 10)
            return 50; // Not enough data

        // Check if recent key hold times are too consistent
        var recentTimes = snapshot.RecentKeyHoldTimes.Select(s => (double)s.DurationMs).ToArray();

        if (recentTimes.Length < 5)
            return 50; // Not enough data

        double stdDev = CalculateStandardDeviation(recentTimes);
        double mean = recentTimes.Average();
        double coefficientOfVariation = mean > 0 ? stdDev / mean : 0;

        // CV < 0.1 is very regular (high risk)
        // CV > 0.3 is natural (low risk)
        if (coefficientOfVariation < 0.1)
            return 85;
        if (coefficientOfVariation < 0.2)
            return 60;
        if (coefficientOfVariation < 0.3)
            return 30;

        return 10;
    }

    private static double AnalyzeInputSecurityCoverage(IHumanizationProvider? provider)
    {
        if (provider == null)
            return 100;

        // This would ideally check actual InputSecurityOptions
        // For now, assume moderate coverage if humanization is enabled
        return provider.Enabled ? 20 : 100;
    }

    private static double AnalyzeSessionDuration(MetricsSnapshot snapshot)
    {
        double hours = snapshot.SessionDuration.TotalHours;

        // Risk increases with session duration
        // < 1 hour: low risk
        // 1-2 hours: moderate risk
        // 2-4 hours: high risk
        // > 4 hours: critical risk
        return hours switch
        {
            < 1 => 10,
            < 2 => 30,
            < 4 => 60,
            < 6 => 80,
            _ => 95
        };
    }

    private static double AnalyzeActionDensity(MetricsSnapshot snapshot)
    {
        if (snapshot.SessionDuration.TotalMinutes < 1)
            return 50;

        double actionsPerMinute = snapshot.SessionKeyPresses / snapshot.SessionDuration.TotalMinutes;

        // Very high action rates are suspicious
        // < 20/min: normal
        // 20-40/min: elevated
        // > 40/min: high risk
        return actionsPerMinute switch
        {
            < 20 => 15,
            < 40 => 40,
            < 60 => 70,
            _ => 90
        };
    }

    #endregion

    #region Helper Methods

    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length < 2)
            return 0;

        double avg = values.Average();
        double sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumOfSquares / (values.Length - 1));
    }

    private static string[] GenerateRecommendations(List<RiskFactor> factors, IHumanizationProvider? provider)
    {
        var recommendations = new List<string>();

        var highestRisk = factors.OrderByDescending(f => f.Score).First();

        if (highestRisk.Name == "Timing Regularity" && highestRisk.Score > 50)
        {
            recommendations.Add("Increase timing variance in humanization settings");
            recommendations.Add("Consider enabling micro-pauses");
        }

        if (highestRisk.Name == "Session Duration" && highestRisk.Score > 50)
        {
            recommendations.Add("Take a break or restart session");
            recommendations.Add("Consider enabling scheduled breaks");
        }

        if (highestRisk.Name == "Action Density" && highestRisk.Score > 50)
        {
            recommendations.Add("Reduce action frequency");
            recommendations.Add("Increase reaction delays");
        }

        if (provider?.Enabled != true)
        {
            recommendations.Add("Enable humanization in settings");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Continue monitoring - no immediate action required");
        }

        return recommendations.ToArray();
    }

    #endregion
}

/// <summary>
/// A complete risk assessment result.
/// </summary>
public sealed class RiskAssessment
{
    public double OverallScore { get; init; }
    public RiskLevel Level { get; init; }
    public RiskFactor[] Factors { get; init; } = Array.Empty<RiskFactor>();
    public DateTime Timestamp { get; init; }
    public string[] Recommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// An individual risk factor contributing to the overall score.
/// </summary>
public sealed record RiskFactor(string Name, double Score, string Description);

/// <summary>
/// Risk level classifications.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}
