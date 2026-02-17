using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Core.AI.LLM;
using Core.Database;
using Core.FeatureFlags;

namespace Core.AI.ProfileGenerator;

/// <summary>
/// Generates class profiles from natural language descriptions using LLM.
/// </summary>
public sealed class AIProfileGeneratorService
{
    private readonly ILogger<AIProfileGeneratorService> logger;
    private readonly ILLMClientFactory llmClientFactory;
    private readonly ProfileValidator profileValidator;
    private readonly SpellDB spellDb;
    private readonly AIProfileGeneratorOptions options;
    private readonly TimeProvider timeProvider;

    // Rate limiting
    private readonly SemaphoreSlim rateLimitSemaphore = new(1, 1);
    private readonly Queue<DateTime> requestHistory = new();
    private readonly TimeSpan rateLimitWindow = TimeSpan.FromHours(1);

    public AIProfileGeneratorService(
        ILogger<AIProfileGeneratorService> logger,
        ILLMClientFactory llmClientFactory,
        ProfileValidator profileValidator,
        SpellDB spellDb,
        IOptions<AIProfileGeneratorOptions> options,
        TimeProvider? timeProvider = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
        this.profileValidator = profileValidator ?? throw new ArgumentNullException(nameof(profileValidator));
        this.spellDb = spellDb ?? throw new ArgumentNullException(nameof(spellDb));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Generates a profile from a natural language description.
    /// </summary>
    /// <param name="description">User description like "Level 30 Frost Mage in Hillsbrad".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generation result with profile or errors.</returns>
    public async Task<ProfileGenerationResult> GenerateProfileAsync(
        string description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!await CheckRateLimitAsync(cancellationToken))
        {
            logger.LogWarning("[AIProfileGen  ] Rate limit exceeded. Max {RateLimit} requests per hour.",
                options.RateLimitPerHour);
            return new ProfileGenerationResult(
                Success: false,
                Profile: null,
                Errors: new[] { $"Rate limit exceeded. Maximum {options.RateLimitPerHour} requests per hour." });
        }

        logger.LogInformation("[AIProfileGen  ] Generating profile for: {Description}", description);

        try
        {
            var client = llmClientFactory.GetDefaultClient();

            if (!await client.IsAvailableAsync())
            {
                return new ProfileGenerationResult(
                    Success: false,
                    Profile: null,
                    Errors: new[] { $"LLM provider '{client.ProviderName}' is not available." });
            }

            string prompt = BuildPrompt(description);
            string response = await client.CompleteAsync(prompt, cancellationToken);

            logger.LogDebug("[AIProfileGen  ] LLM response received, length: {Length}", response.Length);

            // Extract JSON from response
            string json = ExtractJson(response);

            // Validate against schema
            var validation = profileValidator.Validate(json);
            if (!validation.IsValid)
            {
                logger.LogWarning("[AIProfileGen  ] Generated profile failed validation: {Errors}",
                    string.Join(", ", validation.Errors));

                return new ProfileGenerationResult(
                    Success: false,
                    Profile: null,
                    Errors: validation.Errors);
            }

            // Deserialize
            var profile = JsonSerializer.Deserialize<ClassConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (profile == null)
            {
                return new ProfileGenerationResult(
                    Success: false,
                    Profile: null,
                    Errors: new[] { "Failed to deserialize generated profile" });
            }

            logger.LogInformation("[AIProfileGen  ] Profile generated successfully");

            return new ProfileGenerationResult(
                Success: true,
                Profile: profile,
                Errors: Array.Empty<string>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AIProfileGen  ] Profile generation failed: {Message}", ex.Message);
            return new ProfileGenerationResult(
                Success: false,
                Profile: null,
                Errors: new[] { $"Generation failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Checks if the service is available (LLM configured and reachable).
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var client = llmClientFactory.GetDefaultClient();
            return await client.IsAvailableAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the LLM prompt with context about available spells and JSON schema.
    /// </summary>
    private static string BuildPrompt(string description)
    {
        // Get available spells for the class mentioned
        string classHint = ExtractClassHint(description);
        var spellList = GetRelevantSpells(classHint);

        return $$"""
Generate a WowClassicGrindBot JSON profile for: {{description}}

Available spells in database: {{spellList}}

Required JSON schema:
{
    "ClassName": "string (e.g., Mage)",
    "PathFilename": "string (zone_route.json)",
    "Mode": "Grinding",
    "Combat": {
        "Sequence": [
            {
                "Name": "SpellName",
                "Key": "ConsoleKey (e.g., 'D1', 'D2', etc.)",
                "Requirements": ["Requirement:Expression"],
                "HasCastBar": true/false,
                "Cooldown": 0 (in seconds)
            }
        ]
    },
    "Adhoc": {
        "Sequence": [/* buff/consumable abilities */]
    },
    "Pull": {
        "Sequence": [/* initial pull ability */]
    },
    "NPC": {
        "Sequence": [/* sell/repair abilities */]
    },
    "Parallel": {
        "Sequence": [/* always-on abilities */]
    }
}

Guidelines:
1. Use abilities appropriate for the level range
2. Include mana/health management thresholds (e.g., "Health%<30", "Mana%>20")
3. Add buff maintenance in Adhoc
4. Order combat rotation by priority (most important first)
5. Use only ConsoleKey values: D1-D12, F1-F12, etc.
6. Combat sequence should have 4-8 abilities
7. Include food/drink in Adhoc if needed

Return ONLY valid JSON, no explanations or markdown formatting.
""";
    }

    /// <summary>
    /// Extracts class name hint from description.
    /// </summary>
    private static string ExtractClassHint(string description)
    {
        string[] classes = ["Mage", "Warlock", "Priest", "Rogue", "Warrior", "Hunter", "Paladin", "Druid", "Shaman"];
        foreach (var className in classes)
        {
            if (description.Contains(className, StringComparison.OrdinalIgnoreCase))
            {
                return className;
            }
        }
        return "";
    }

    /// <summary>
    /// Gets relevant spells for the class.
    /// </summary>
    private static string GetRelevantSpells(string classHint)
    {
        if (string.IsNullOrEmpty(classHint))
        {
            // Return a sample of common spells
            return "Fireball, Frostbolt, Arcane Missiles, Shadow Bolt, Heal, Sinister Strike, Eviscerate, etc.";
        }

        // In a real implementation, this would query the SpellDB for the class
        // For now, return class-specific hints
        return classHint switch
        {
            "Mage" => "Frostbolt, Fireball, Arcane Missiles, Fire Blast, Frost Nova, Blink, Counterspell, Polymorph",
            "Warlock" => "Shadow Bolt, Immolate, Corruption, Curse of Agony, Drain Soul, Fear, Health Funnel",
            "Rogue" => "Sinister Strike, Eviscerate, Backstab, Slice and Dice, Gouge, Kick, Stealth",
            "Warrior" => "Heroic Strike, Rend, Overpower, Execute, Battle Shout, Charge",
            "Hunter" => "Auto Shot, Arcane Shot, Serpent Sting, Concussive Shot, Hunter's Mark, Aspect of the Hawk",
            "Priest" => "Smite, Mind Blast, Shadow Word: Pain, Power Word: Shield, Heal, Renew",
            "Paladin" => "Seal of Righteousness, Judgement, Crusader Strike, Divine Shield, Flash of Light",
            "Druid" => "Wrath, Moonfire, Starfire, Entangling Roots, Healing Touch, Rejuvenation",
            "Shaman" => "Lightning Bolt, Earth Shock, Flame Shock, Lightning Shield, Healing Wave",
            _ => "Various class abilities"
        };
    }

    /// <summary>
    /// Extracts JSON from LLM response.
    /// </summary>
    private static string ExtractJson(string response)
    {
        // Try markdown code blocks
        var match = Regex.Match(response, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Try to find JSON object boundaries
        int start = response.IndexOf('{');
        int end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return response[start..(end + 1)].Trim();
        }

        return response.Trim();
    }

    /// <summary>
    /// Checks rate limit before making request.
    /// </summary>
    private async Task<bool> CheckRateLimitAsync(CancellationToken cancellationToken)
    {
        await rateLimitSemaphore.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().DateTime;
            var cutoff = now - rateLimitWindow;

            // Remove old requests
            while (requestHistory.Count > 0 && requestHistory.Peek() < cutoff)
            {
                requestHistory.Dequeue();
            }

            // Check if under limit
            if (requestHistory.Count >= options.RateLimitPerHour)
            {
                return false;
            }

            // Record this request
            requestHistory.Enqueue(now);
            return true;
        }
        finally
        {
            rateLimitSemaphore.Release();
        }
    }
}

/// <summary>
/// Result of profile generation attempt.
/// </summary>
/// <param name="Success">Whether generation succeeded.</param>
/// <param name="Profile">The generated profile if successful.</param>
/// <param name="Errors">Error messages if failed.</param>
public sealed record ProfileGenerationResult(
    bool Success,
    ClassConfiguration? Profile,
    IReadOnlyList<string> Errors);
