using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Core.AI.LLM;

/// <summary>
/// Interface for LLM client implementations. Provides abstraction over
/// different LLM providers (OpenAI, Anthropic, Local models).
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Gets the display name of this LLM provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends a completion request to the LLM.
    /// </summary>
    /// <param name="prompt">The prompt text to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The LLM's response text.</returns>
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the LLM service is available.
    /// </summary>
    /// <returns>True if the service is reachable and ready.</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Gets the last latency measurement in milliseconds.
    /// </summary>
    long LastLatencyMs { get; }
}

/// <summary>
/// Base class for HTTP-based LLM clients with common functionality.
/// </summary>
public abstract class HttpLLMClientBase : ILLMClient
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected string? ApiKey;
    protected string? ModelName;

    protected HttpLLMClientBase(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string ProviderName { get; }
    public long LastLatencyMs { get; protected set; }

    public abstract Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);

    public virtual async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await IsAvailableInternalAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("[{Provider} ] Health check failed: {Message}", ProviderName, ex.Message);
            return false;
        }
    }

    protected abstract Task<bool> IsAvailableInternalAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Extracts JSON content from markdown code blocks or raw response.
    /// </summary>
    protected static string ExtractJson(string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        // Try to extract from markdown code block
        int start = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            int end = response.LastIndexOf("```", StringComparison.Ordinal);
            if (end > start + 7)
            {
                return response[(start + 7)..end].Trim();
            }
        }

        // Try generic code block
        start = response.IndexOf("```", StringComparison.Ordinal);
        if (start >= 0)
        {
            int end = response.LastIndexOf("```", StringComparison.Ordinal);
            if (end > start + 3)
            {
                return response[(start + 3)..end].Trim();
            }
        }

        // Return raw response, attempting to find JSON object boundaries
        int jsonStart = response.IndexOf('{');
        int jsonEnd = response.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return response[jsonStart..(jsonEnd + 1)].Trim();
        }

        return response.Trim();
    }
}

/// <summary>
/// Result of an LLM completion request.
/// </summary>
/// <param name="Success">Whether the request succeeded.</param>
/// <param name="Content">The response content if successful.</param>
/// <param name="ErrorMessage">Error message if failed.</param>
/// <param name="LatencyMs">Request latency in milliseconds.</param>
public sealed record CompletionResult(
    bool Success,
    string? Content = null,
    string? ErrorMessage = null,
    long LatencyMs = 0);

/// <summary>
/// Factory for creating LLM clients based on configuration.
/// </summary>
public interface ILLMClientFactory
{
    /// <summary>
    /// Creates an LLM client for the specified provider.
    /// </summary>
    /// <param name="providerName">Provider name (openai, anthropic, local).</param>
    /// <returns>Configured LLM client.</returns>
    /// <exception cref="ArgumentException">If provider is not supported.</exception>
    ILLMClient CreateClient(string providerName);

    /// <summary>
    /// Gets the default client based on configuration.
    /// </summary>
    ILLMClient GetDefaultClient();
}
