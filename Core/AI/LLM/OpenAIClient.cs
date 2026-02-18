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
/// OpenAI GPT client implementation for profile generation.
/// </summary>
public sealed class OpenAIClient : HttpLLMClientBase, IDisposable
{
    private const string ApiBaseUrl = "https://api.openai.com/v1/";
    private readonly string _model;
    private readonly float _temperature;
    private readonly int _maxTokens;
    private bool _disposed;

    public OpenAIClient(
        HttpClient httpClient,
        ILogger<OpenAIClient> logger,
        IOptions<AIProfileGeneratorOptions> options) : base(httpClient, logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY environment variable not set. " +
                "Set it to use the OpenAI profile generator.");

        _model = "gpt-4o-mini";
        _temperature = 0.3f;
        _maxTokens = options.Value.MaxTokensPerRequest;

        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "WowClassicGrindBot/1.0");
    }

    public override string ProviderName => "OpenAI";

    public override async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenAIClient));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = new OpenAIRequest
            {
                Model = _model,
                Messages =
                [
                    new Message { Role = "system", Content = GetSystemPrompt() },
                    new Message { Role = "user", Content = prompt }
                ],
                Temperature = _temperature,
                MaxTokens = _maxTokens
            };

            using var content = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            Logger.LogDebug("[OpenAIClient ] Sending request to {Model} with {TokenCount} max tokens",
                _model, _maxTokens);

            using var response = await HttpClient.PostAsync(
                $"{ApiBaseUrl}chat/completions",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(
                cancellationToken: cancellationToken);

            if (result?.Choices == null || result.Choices.Count == 0)
            {
                throw new InvalidOperationException("OpenAI returned empty response");
            }

            var content_text = result.Choices[0].Message?.Content
                ?? throw new InvalidOperationException("OpenAI returned null content");

            Logger.LogInformation("[OpenAIClient ] Request completed in {Latency}ms, usage: {Tokens} tokens",
                stopwatch.ElapsedMilliseconds,
                result.Usage?.TotalTokens ?? 0);

            LastLatencyMs = stopwatch.ElapsedMilliseconds;
            return content_text;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "[OpenAIClient ] HTTP request failed: {Status}", ex.StatusCode);
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Logger.LogError("[OpenAIClient ] Request timed out after {Latency}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[OpenAIClient ] Request failed: {Message}", ex.Message);
            throw;
        }
    }

    protected override async Task<bool> IsAvailableInternalAsync(CancellationToken cancellationToken)
    {
        // OpenAI doesn't have a dedicated health endpoint, so we do a minimal request
        // or check if the API key is configured
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    /// <summary>
    /// System prompt that establishes the AI's role as a WoW bot configuration expert.
    /// </summary>
    private static string GetSystemPrompt()
    {
        return """You are a World of Warcraft Classic bot configuration expert. Your task is to generate valid JSON configuration files for the WowClassicGrindBot based on natural language descriptions. You must ONLY return valid JSON, no explanations or markdown formatting outside the JSON structure. Ensure all spell names match WoW Classic abilities exactly. Include appropriate mana/health thresholds and cooldown management. Order combat abilities by priority (most important first).""";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    // OpenAI API Models
    private sealed class OpenAIRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required List<Message> Messages { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private sealed class OpenAIResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
