using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Core.LLM;

/// <summary>
/// Null Object pattern implementation of ILLMClient.
/// Returns safe fallback decisions without making external API calls.
/// Used when LLM features are disabled or no provider is configured.
/// </summary>
public sealed class NullLLMClient : ILLMClient
{
    private readonly ILogger<NullLLMClient> logger;

    public NullLLMClient(ILogger<NullLLMClient> logger)
    {
        this.logger = logger;
        logger.LogInformation("[NullLLMClient   ] Initialized (LLM features disabled)");
    }

    public bool IsAvailable => true; // Always "available" but does nothing

    public Task<LLMDecision> QueryAsync(string context, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("[NullLLMClient   ] Query skipped (LLM disabled)");

        // Return a safe fallback decision
        LLMDecision fallback = new(
            SuggestedAction: "NoAction",
            Reasoning: "LLM features are disabled",
            Confidence: 0.0f,
            Metadata: new Dictionary<string, object>
            {
                ["Provider"] = "NullLLMClient",
                ["Timestamp"] = DateTimeOffset.UtcNow
            }
        );

        return Task.FromResult(fallback);
    }
}
