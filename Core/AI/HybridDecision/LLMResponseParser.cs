namespace Core.AI.HybridDecision;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

using Core;

using Microsoft.Extensions.Logging;

/// <summary>
/// Parses LLM responses into structured decision results.
/// </summary>
public sealed class LLMResponseParser
{
    private readonly ILogger<LLMResponseParser>? logger;

    /// <summary>
    /// Creates a new parser.
    /// </summary>
    public LLMResponseParser(ILogger<LLMResponseParser>? logger = null)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Parses an LLM decision response.
    /// </summary>
    /// <param name="response">Raw LLM response.</param>
    /// <param name="availableActions">Available actions to choose from.</param>
    /// <returns>Parsed decision result, or null if parsing fails.</returns>
    public LLMDecisionResult? ParseDecision(string response, IReadOnlyList<KeyAction> availableActions)
    {
        try
        {
            // Try to extract JSON from the response
            string? json = ExtractJson(response);
            if (string.IsNullOrEmpty(json))
            {
                logger?.LogWarning("[LLMParser] No JSON found in response");
                return null;
            }

            // Parse the JSON
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // Extract action index
            int actionIndex = 0;
            if (root.TryGetProperty("actionIndex", out JsonElement indexElement))
            {
                actionIndex = indexElement.GetInt32();
            }
            else if (root.TryGetProperty("action", out JsonElement actionElement))
            {
                // Alternative format: action name instead of index
                string? actionName = actionElement.GetString();
                actionIndex = FindActionIndex(actionName, availableActions);
            }

            // Extract reasoning
            string reasoning = "No reasoning provided";
            if (root.TryGetProperty("reasoning", out JsonElement reasoningElement))
            {
                reasoning = reasoningElement.GetString() ?? reasoning;
            }

            // Validate action index
            KeyAction? selectedAction = null;
            if (actionIndex > 0 && actionIndex <= availableActions.Count)
            {
                selectedAction = availableActions[actionIndex - 1]; // Convert to 0-based index
            }

            float confidence = selectedAction != null ? 0.8f : 0.0f;

            return new LLMDecisionResult(selectedAction, reasoning, confidence);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "[LLMParser] JSON parsing failed");
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[LLMParser] Unexpected error parsing response");
            return null;
        }
    }

    /// <summary>
    /// Extracts JSON from a response that may contain markdown or other text.
    /// </summary>
    private static string? ExtractJson(string response)
    {
        // Try to find JSON block in markdown code fences
        Match codeBlockMatch = Regex.Match(response, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // Try to find JSON object directly
        Match jsonMatch = Regex.Match(response, @"\{[\s\S]*?\}");
        if (jsonMatch.Success)
        {
            return jsonMatch.Value;
        }

        // Response might be the JSON directly
        if (response.TrimStart().StartsWith("{"))
        {
            return response.Trim();
        }

        return null;
    }

    /// <summary>
    /// Finds action index by name.
    /// </summary>
    private static int FindActionIndex(string? actionName, IReadOnlyList<KeyAction> availableActions)
    {
        if (string.IsNullOrEmpty(actionName))
        {
            return 0;
        }

        for (int i = 0; i < availableActions.Count; i++)
        {
            if (availableActions[i].Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1; // Return 1-based index
            }
        }

        return 0;
    }
}
