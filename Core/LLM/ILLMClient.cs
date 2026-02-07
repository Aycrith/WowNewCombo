using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.LLM;

/// <summary>
/// Represents an LLM decision in response to a GOAP state.
/// </summary>
public sealed record LLMDecision(
    string SuggestedAction,
    string Reasoning,
    float Confidence,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// Abstract LLM client for querying external language models.
/// Implementations should handle API calls, rate limiting, and error handling.
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Queries the LLM for a decision based on current game state.
    /// </summary>
    /// <param name="context">Serialized game state context (JSON, plain text, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LLM decision with suggested action and confidence score</returns>
    Task<LLMDecision> QueryAsync(string context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the client is currently available (not rate-limited, API reachable, etc.).
    /// </summary>
    bool IsAvailable { get; }
}
