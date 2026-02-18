using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Core.FeatureFlags;

namespace Core.AI.LLM;

/// <summary>
/// Local LLM client implementation using llama.cpp HTTP server.
/// Provides offline profile generation without API costs.
/// </summary>
/// <remarks>
/// Requires llama.cpp server running locally on port 8080 (default).
/// Download from: https://github.com/ggerganov/llama.cpp
/// Run: ./server -m models/llama-2-7b-chat.Q4_K_M.gguf --port 8080
/// </remarks>
public sealed class LocalLlamaClient : HttpLLMClientBase, IDisposable
{
    private readonly string _baseUrl;
    private readonly string _modelPath;
    private readonly float _temperature;
    private readonly int _maxTokens;
    private bool _disposed;

    public LocalLlamaClient(
        HttpClient httpClient,
        ILogger<LocalLlamaClient> logger,
        IOptions<AIProfileGeneratorOptions> options) : base(httpClient, logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Default to localhost:8080 for llama.cpp server
        _baseUrl = Environment.GetEnvironmentVariable("LLAMA_SERVER_URL")
            ?? "http://localhost:8080";

        _modelPath = Environment.GetEnvironmentVariable("LLAMA_MODEL_PATH")
            ?? "";

        _temperature = 0.3f;
        _maxTokens = Math.Min(options.Value.MaxTokensPerRequest, 2048); // Local models often have lower limits

        HttpClient.Timeout = TimeSpan.FromSeconds(60); // Local inference can be slow
        Logger.LogInformation("[LocalLlamaClient ] Configured for {Url}", _baseUrl);
    }

    public override string ProviderName => "LocalLlama";

    public override async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (_disposed)
            throw new ObjectDisposedException(nameof(LocalLlamaClient));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = new LlamaRequest
            {
                Prompt = BuildPromptWithSystem(prompt),
                Temperature = _temperature,
                MaxTokens = _maxTokens,
                Stop = ["</s>", "```"], // Common stop tokens for chat models
                Stream = false
            };

            Logger.LogDebug("[LocalLlamaClient ] Sending request to {Url}", _baseUrl);

            using var response = await HttpClient.PostAsJsonAsync(
                $"{_baseUrl}/completion",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LlamaResponse>(
                cancellationToken: cancellationToken);

            if (result?.Content == null)
            {
                throw new InvalidOperationException("Local LLM returned empty response");
            }

            Logger.LogInformation("[LocalLlamaClient ] Request completed in {Latency}ms, generated {Tokens} tokens",
                stopwatch.ElapsedMilliseconds,
                result.TokensPredicted);

            LastLatencyMs = stopwatch.ElapsedMilliseconds;
            return result.Content;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "[LocalLlamaClient ] Cannot connect to llama.cpp server at {Url}. " +
                "Ensure the server is running with: ./server -m <model.gguf> --port 8080", _baseUrl);
            throw new InvalidOperationException(
                $"Local LLM server not available at {_baseUrl}. " +
                "Please start llama.cpp server or use cloud provider.", ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[LocalLlamaClient ] Request failed: {Message}", ex.Message);
            throw;
        }
    }

    protected override async Task<bool> IsAvailableInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                $"{_baseUrl}/health",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds prompt with system instructions for local models.
    /// </summary>
    private static string BuildPromptWithSystem(string userPrompt)
    {
        // Llama-2 chat format
        return $"[INST] <<SYS>>You are a World of Warcraft Classic bot configuration expert. Generate valid JSON configuration files for the WowClassicGrindBot. Return ONLY valid JSON, no explanations.<</SYS>>\n\n{userPrompt} [/INST]";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    // llama.cpp API Models
    private sealed class LlamaRequest
    {
        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("n_predict")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("stop")]
        public List<string>? Stop { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class LlamaResponse
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tokens_predicted")]
        public int TokensPredicted { get; set; }

        [JsonPropertyName("tokens_evaluated")]
        public int TokensEvaluated { get; set; }

        [JsonPropertyName("generation_settings")]
        public GenerationSettings? GenerationSettings { get; set; }
    }

    private sealed class GenerationSettings
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}
