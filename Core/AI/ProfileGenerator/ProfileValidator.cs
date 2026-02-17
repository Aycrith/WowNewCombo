using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Core.AI.ProfileGenerator;

/// <summary>
/// Validates generated profiles against schema and game rules.
/// </summary>
public sealed class ProfileValidator
{
    private readonly ILogger<ProfileValidator> logger;
    private readonly HashSet<string> validConsoleKeys;
    private readonly HashSet<string> validClasses;
    private readonly HashSet<string> validModes;

    public ProfileValidator(ILogger<ProfileValidator> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Valid ConsoleKey values
        validConsoleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i <= 12; i++)
        {
            validConsoleKeys.Add($"D{i}");
            validConsoleKeys.Add($"F{i}");
        }
        validConsoleKeys.Add("Q");
        validConsoleKeys.Add("E");
        validConsoleKeys.Add("R");
        validConsoleKeys.Add("T");
        validConsoleKeys.Add("F");
        validConsoleKeys.Add("G");
        validConsoleKeys.Add("Z");
        validConsoleKeys.Add("X");
        validConsoleKeys.Add("C");
        validConsoleKeys.Add("V");
        validConsoleKeys.Add("B");
        validConsoleKeys.Add("N");
        validConsoleKeys.Add("M");
        validConsoleKeys.Add("Tab");
        validConsoleKeys.Add("Space");
        validConsoleKeys.Add("Enter");

        // Valid WoW classes
        validClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Mage", "Warlock", "Priest", "Rogue", "Warrior", "Hunter", "Paladin", "Druid", "Shaman"
        };

        // Valid modes
        validModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Grinding", "Gather", "CorpseRun", "Healer", "AH", "Mail"
        };
    }

    /// <summary>
    /// Validates a profile JSON string.
    /// </summary>
    /// <param name="json">The JSON to validate.</param>
    /// <returns>Validation result with errors if any.</returns>
    public ValidationResult Validate(string json)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return new ValidationResult(false, new[] { "Empty JSON" });
        }

        // Try to parse as JSON
        JsonDocument? doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new ValidationResult(false, new[] { $"Invalid JSON: {ex.Message}" });
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Check required top-level fields
            ValidateRequiredField(root, "ClassName", errors);
            ValidateRequiredField(root, "Mode", errors);

            // Validate ClassName
            if (TryGetString(root, "ClassName", out var className))
            {
                if (!validClasses.Contains(className))
                {
                    errors.Add($"Invalid ClassName: '{className}'. Valid: {string.Join(", ", validClasses)}");
                }
            }

            // Validate Mode
            if (TryGetString(root, "Mode", out var mode))
            {
                if (!validModes.Contains(mode))
                {
                    errors.Add($"Invalid Mode: '{mode}'. Valid: {string.Join(", ", validModes)}");
                }
            }

            // Validate Combat sequence
            if (root.TryGetProperty("Combat", out var combat))
            {
                ValidateCombatSequence(combat, errors);
            }

            // Validate Adhoc sequence
            if (root.TryGetProperty("Adhoc", out var adhoc))
            {
                ValidateKeyActionSequence(adhoc, "Adhoc", errors);
            }

            // Validate Pull sequence
            if (root.TryGetProperty("Pull", out var pull))
            {
                ValidateKeyActionSequence(pull, "Pull", errors);
            }
        }

        return new ValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors);
    }

    /// <summary>
    /// Validates the Combat sequence specifically.
    /// </summary>
    private void ValidateCombatSequence(JsonElement combat, List<string> errors)
    {
        if (!combat.TryGetProperty("Sequence", out var sequence))
        {
            errors.Add("Combat.Sequence is required");
            return;
        }

        if (sequence.GetArrayLength() == 0)
        {
            errors.Add("Combat.Sequence cannot be empty");
            return;
        }

        if (sequence.GetArrayLength() > 20)
        {
            errors.Add("Combat.Sequence has too many actions (max 20)");
        }

        int index = 0;
        foreach (var action in sequence.EnumerateArray())
        {
            ValidateKeyAction(action, $"Combat[{index}]", errors);
            index++;
        }
    }

    /// <summary>
    /// Validates a generic KeyAction sequence.
    /// </summary>
    private void ValidateKeyActionSequence(JsonElement container, string context, List<string> errors)
    {
        if (!container.TryGetProperty("Sequence", out var sequence))
        {
            // Optional, so just return
            return;
        }

        int index = 0;
        foreach (var action in sequence.EnumerateArray())
        {
            ValidateKeyAction(action, $"{context}[{index}]", errors);
            index++;
        }
    }

    /// <summary>
    /// Validates a single KeyAction.
    /// </summary>
    private void ValidateKeyAction(JsonElement action, string context, List<string> errors)
    {
        // Name is required
        if (!action.TryGetProperty("Name", out _))
        {
            errors.Add($"{context}.Name is required");
        }

        // Key is required and must be valid
        if (action.TryGetProperty("Key", out var keyElement))
        {
            var key = keyElement.GetString();
            if (!string.IsNullOrEmpty(key) && !validConsoleKeys.Contains(key))
            {
                errors.Add($"{context}.Key '{key}' is not a valid ConsoleKey");
            }
        }
        else
        {
            errors.Add($"{context}.Key is required");
        }

        // Validate HasCastBar if present
        if (action.TryGetProperty("HasCastBar", out var hasCastBar))
        {
            if (hasCastBar.ValueKind != JsonValueKind.True && hasCastBar.ValueKind != JsonValueKind.False)
            {
                errors.Add($"{context}.HasCastBar must be a boolean");
            }
        }

        // Validate Cooldown if present
        if (action.TryGetProperty("Cooldown", out var cooldown))
        {
            if (cooldown.ValueKind != JsonValueKind.Number || cooldown.GetInt32() < 0)
            {
                errors.Add($"{context}.Cooldown must be a non-negative integer");
            }
        }

        // Validate Requirements if present
        if (action.TryGetProperty("Requirements", out var requirements))
        {
            if (requirements.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"{context}.Requirements must be an array");
            }
            else
            {
                int reqIndex = 0;
                foreach (var req in requirements.EnumerateArray())
                {
                    var reqStr = req.GetString();
                    if (!string.IsNullOrEmpty(reqStr))
                    {
                        ValidateRequirement(reqStr, $"{context}.Requirements[{reqIndex}]", errors);
                    }
                    reqIndex++;
                }
            }
        }
    }

    /// <summary>
    /// Validates a requirement expression.
    /// </summary>
    private void ValidateRequirement(string requirement, string context, List<string> errors)
    {
        // Common requirement patterns
        var validPatterns = new[]
        {
            @"^Health[%<>=]+\d+$",
            @"^Mana[%<>=]+\d+$",
            @"^Energy[%<>=]+\d+$",
            @"^Rage[%<>=]+\d+$",
            @"^Combo Points[=<>]+\d+$",
            @"^TargetHealth[%<>=]+\d+$",
            @"^!?Buff\s*:\s*\w+$",
            @"^!?Debuff\s*:\s*\w+$",
            @"^MobCount[=<>]+\d+$",
            @"^InMeleeRange$",
            @"^HasTarget$",
            @"^!?HasTarget$",
            @"^IsCasting$",
            @"^!?IsCasting$",
            @"^IsChanneling$",
            @"^!IsChanneling$"
        };

        bool isValid = validPatterns.Any(pattern => Regex.IsMatch(requirement, pattern, RegexOptions.IgnoreCase));

        if (!isValid)
        {
            // Don't fail for unknown requirements, just warn
            logger.LogDebug("[ProfileValidator] Unknown requirement pattern: {Requirement}", requirement);
        }
    }

    /// <summary>
    /// Validates a required field exists.
    /// </summary>
    private static void ValidateRequiredField(JsonElement element, string fieldName, List<string> errors)
    {
        if (!element.TryGetProperty(fieldName, out _))
        {
            errors.Add($"Missing required field: {fieldName}");
        }
    }

    /// <summary>
    /// Tries to get a string property value.
    /// </summary>
    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }
}

/// <summary>
/// Result of profile validation.
/// </summary>
/// <param name="IsValid">Whether the profile is valid.</param>
/// <param name="Errors">List of validation errors if not valid.</param>
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
