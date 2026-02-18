using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.Marketplace;

/// <summary>
/// Represents a profile available in the marketplace.
/// </summary>
public sealed class ProfileListing
{
    /// <summary>
    /// Unique identifier for the profile.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the profile.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// WoW class this profile is for.
    /// </summary>
    [JsonPropertyName("className")]
    public required string ClassName { get; init; }

    /// <summary>
    /// Description of the profile.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Profile author name.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Minimum level this profile supports.
    /// </summary>
    [JsonPropertyName("minLevel")]
    public int MinLevel { get; init; } = 1;

    /// <summary>
    /// Maximum level this profile supports.
    /// </summary>
    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; init; } = 60;

    /// <summary>
    /// WoW expansion (classic, tbc, wotlk, etc.).
    /// </summary>
    [JsonPropertyName("expansion")]
    public string Expansion { get; init; } = "classic";

    /// <summary>
    /// Number of times this profile has been downloaded.
    /// </summary>
    [JsonPropertyName("downloads")]
    public int Downloads { get; init; }

    /// <summary>
    /// User rating (0-5).
    /// </summary>
    [JsonPropertyName("rating")]
    public float Rating { get; init; }

    /// <summary>
    /// Profile version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// URL to download the profile JSON.
    /// </summary>
    [JsonPropertyName("downloadUrl")]
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Search criteria for marketplace queries.
/// </summary>
public sealed class ProfileSearchCriteria
{
    /// <summary>
    /// Filter by class name (e.g., "Mage", "Warrior").
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Minimum character level.
    /// </summary>
    public int? MinLevel { get; set; }

    /// <summary>
    /// Maximum character level.
    /// </summary>
    public int? MaxLevel { get; set; }

    /// <summary>
    /// Expansion filter (classic, tbc, wotlk, etc.).
    /// </summary>
    public string? Expansion { get; set; }

    /// <summary>
    /// Text search in name, description, or tags.
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; set; } = 20;
}

/// <summary>
/// Result of a profile download operation.
/// </summary>
/// <param name="Success">Whether the download succeeded.</param>
/// <param name="LocalPath">Path to downloaded file if successful.</param>
/// <param name="ProfileName">Name of the downloaded profile.</param>
/// <param name="ErrorMessage">Error message if failed.</param>
public sealed record DownloadResult(
    bool Success,
    string? LocalPath = null,
    string? ProfileName = null,
    string? ErrorMessage = null);

/// <summary>
/// Root object for the profile index JSON.
/// </summary>
internal sealed class ProfileIndex
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("profiles")]
    public required List<ProfileListing> Profiles { get; init; }
}
