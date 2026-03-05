using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Core.FeatureFlags;

namespace Core.AI.LLM;

/// <summary>
/// Factory for creating LLM clients based on configuration.
/// </summary>
public sealed class LLMClientFactory : ILLMClientFactory
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<LLMClientFactory> logger;
    private readonly AIProfileGeneratorOptions options;

    private readonly ConcurrentDictionary<string, Lazy<ILLMClient>> _clientCache =
        new(StringComparer.OrdinalIgnoreCase);

    public LLMClientFactory(
        IServiceProvider serviceProvider,
        ILogger<LLMClientFactory> logger,
        IOptions<AIProfileGeneratorOptions> options)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public ILLMClient CreateClient(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        Lazy<ILLMClient> lazy = _clientCache.GetOrAdd(
            providerName,
            static (key, self) => new Lazy<ILLMClient>(
                () => self.CreateClientCore(key),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this);

        return lazy.Value;
    }

    private ILLMClient CreateClientCore(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAIClient(),
            "local" or "llama" or "local_llama" => CreateLocalLlamaClient(),
            _ => throw new ArgumentException(
                $"Unknown LLM provider: '{providerName}'. Supported: {string.Join(", ", options.AllowedProviders)}.",
                nameof(providerName))
        };
    }

    /// <inheritdoc />
    public ILLMClient GetDefaultClient()
    {
        // Use configured provider or fallback to first available
        var provider = !string.IsNullOrEmpty(options.APIProvider) &&
                       options.APIProvider != "none"
            ? options.APIProvider
            : GetFirstAvailableProvider();

        if (string.IsNullOrEmpty(provider))
        {
            throw new InvalidOperationException(
                "No LLM provider configured. Set AIProfileGenerator:APIProvider to 'openai' or 'local' " +
                "and configure the appropriate API key.");
        }

        return CreateClient(provider);
    }

    /// <summary>
    /// Creates an OpenAI client.
    /// </summary>
    private OpenAIClient CreateOpenAIClient()
    {
        logger.LogInformation("[LLMClientFactry] Creating OpenAI client");

        HttpClient httpClient = new();
        ILogger<OpenAIClient> openaiLogger = serviceProvider.GetService<ILogger<OpenAIClient>>()
            ?? throw new InvalidOperationException("ILogger<OpenAIClient> not registered");
        IOptions<AIProfileGeneratorOptions> options = Microsoft.Extensions.Options.Options.Create(this.options);

        return new OpenAIClient(httpClient, openaiLogger, options);
    }

    /// <summary>
    /// Creates a local Llama client.
    /// </summary>
    private LocalLlamaClient CreateLocalLlamaClient()
    {
        logger.LogInformation("[LLMClientFactry] Creating LocalLlama client");

        HttpClient httpClient = new();
        ILogger<LocalLlamaClient> llamaLogger = serviceProvider.GetService<ILogger<LocalLlamaClient>>()
            ?? throw new InvalidOperationException("ILogger<LocalLlamaClient> not registered");
        IOptions<AIProfileGeneratorOptions> options = Microsoft.Extensions.Options.Options.Create(this.options);

        return new LocalLlamaClient(httpClient, llamaLogger, options);
    }

    /// <summary>
    /// Determines the first available provider based on environment.
    /// </summary>
    private static string GetFirstAvailableProvider()
    {
        // Check for OpenAI key
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return "openai";
        }

        // Check for local LLM
        // This would need async check, so for now just return empty
        return "";
    }
}
