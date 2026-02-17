using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Core.FeatureFlags;

namespace Core.Marketplace;

/// <summary>
/// Service for discovering and downloading community profiles from GitHub.
/// </summary>
public sealed class ProfileMarketplaceService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<ProfileMarketplaceService> logger;
    private readonly DataConfig dataConfig;
    private readonly ProfileMarketplaceOptions options;

    // Cache
    private ProfileIndex? cachedIndex;
    private DateTime lastIndexFetch = DateTime.MinValue;
    private readonly SemaphoreSlim cacheLock = new(1, 1);

    // Download tracking
    private int downloadsThisSession = 0;

    public ProfileMarketplaceService(
        HttpClient httpClient,
        ILogger<ProfileMarketplaceService> logger,
        DataConfig dataConfig,
        IOptions<ProfileMarketplaceOptions> options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dataConfig = dataConfig ?? throw new ArgumentNullException(nameof(dataConfig));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Set GitHub API headers
        this.httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        this.httpClient.DefaultRequestHeaders.Add("User-Agent", "WowClassicGrindBot/1.0");

        // Add auth token if available (for higher rate limits)
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            this.httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// Searches for profiles matching the criteria.
    /// </summary>
    public async Task<IReadOnlyList<ProfileListing>> SearchProfilesAsync(
        ProfileSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var index = await GetIndexAsync(cancellationToken);

            var results = index.Profiles.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(criteria.ClassName))
            {
                results = results.Where(p =>
                    p.ClassName.Equals(criteria.ClassName, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.MinLevel.HasValue)
            {
                results = results.Where(p => p.MaxLevel >= criteria.MinLevel.Value);
            }

            if (criteria.MaxLevel.HasValue)
            {
                results = results.Where(p => p.MinLevel <= criteria.MaxLevel.Value);
            }

            if (!string.IsNullOrEmpty(criteria.Expansion))
            {
                results = results.Where(p =>
                    p.Expansion.Equals(criteria.Expansion, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                var searchLower = criteria.SearchText.ToLowerInvariant();
                results = results.Where(p =>
                    p.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    p.Tags.Any(t => t.Contains(searchLower, StringComparison.OrdinalIgnoreCase)));
            }

            // Sort by rating then downloads
            results = results
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.Downloads)
                .Take(criteria.MaxResults);

            return results.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Marketplace   ] Failed to search profiles: {Message}", ex.Message);
            return Array.Empty<ProfileListing>();
        }
    }

    /// <summary>
    /// Downloads a profile by ID.
    /// </summary>
    public async Task<DownloadResult> DownloadProfileAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        // Check download limit
        if (downloadsThisSession >= options.MaxDownloadsPerSession)
        {
            return new DownloadResult(
                Success: false,
                ErrorMessage: $"Download limit reached ({options.MaxDownloadsPerSession} per session)");
        }

        try
        {
            var index = await GetIndexAsync(cancellationToken);
            var listing = index.Profiles.FirstOrDefault(p => p.Id == profileId);

            if (listing == null)
            {
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Profile '{profileId}' not found");
            }

            logger.LogInformation("[Marketplace   ] Downloading profile {Id} from {Url}",
                profileId, listing.DownloadUrl);

            // Download content
            string content = await httpClient.GetStringAsync(listing.DownloadUrl, cancellationToken);

            // Validate JSON
            try
            {
                using var doc = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid JSON in profile: {ex.Message}");
            }

            // Sanitize filename
            string safeName = SanitizeFileName($"{listing.ClassName}_{listing.Name}_{listing.Id}.json");
            string targetPath = Path.Combine(dataConfig.Class, safeName);

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            // Write file
            await File.WriteAllTextAsync(targetPath, content, cancellationToken);

            downloadsThisSession++;
            logger.LogInformation("[Marketplace   ] Downloaded profile to {Path}", targetPath);

            return new DownloadResult(
                Success: true,
                LocalPath: targetPath,
                ProfileName: listing.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Marketplace   ] Download failed: {Message}", ex.Message);
            return new DownloadResult(
                Success: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Gets featured/popular profiles.
    /// </summary>
    public async Task<IReadOnlyList<ProfileListing>> GetFeaturedProfilesAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(cancellationToken);

        return index.Profiles
            .OrderByDescending(p => p.Downloads)
            .ThenByDescending(p => p.Rating)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets profiles by class.
    /// </summary>
    public async Task<IReadOnlyList<ProfileListing>> GetProfilesByClassAsync(
        string className,
        CancellationToken cancellationToken = default)
    {
        return await SearchProfilesAsync(
            new ProfileSearchCriteria { ClassName = className, MaxResults = 100 },
            cancellationToken);
    }

    /// <summary>
    /// Clears the cache to force refresh on next access.
    /// </summary>
    public void ClearCache()
    {
        cachedIndex = null;
        lastIndexFetch = DateTime.MinValue;
        logger.LogDebug("[Marketplace   ] Cache cleared");
    }

    /// <summary>
    /// Gets the profile index from cache or remote.
    /// </summary>
    private async Task<ProfileIndex> GetIndexAsync(CancellationToken cancellationToken)
    {
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Check if cache is still valid
            if (cachedIndex != null &&
                DateTime.UtcNow - lastIndexFetch < TimeSpan.FromMinutes(options.CacheDurationMinutes))
            {
                logger.LogDebug("[Marketplace   ] Using cached index");
                return cachedIndex;
            }

            // Fetch from remote
            logger.LogInformation("[Marketplace   ] Fetching profile index from {Repo}", options.RepositoryUrl);

            // Parse repository URL
            var repoInfo = ParseRepositoryUrl(options.RepositoryUrl);

            // GitHub API URL for contents
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Owner}/{repoInfo.Repo}/contents/index.json";

            var response = await httpClient.GetFromJsonAsync<GitHubContent>(apiUrl, cancellationToken);

            if (response?.Content == null)
            {
                throw new InvalidOperationException("GitHub API returned empty content");
            }

            // Decode base64 content
            string jsonContent = Encoding.UTF8.GetString(
                Convert.FromBase64String(response.Content));

            cachedIndex = JsonSerializer.Deserialize<ProfileIndex>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (cachedIndex == null)
            {
                throw new InvalidOperationException("Failed to deserialize profile index");
            }

            lastIndexFetch = DateTime.UtcNow;
            logger.LogInformation("[Marketplace   ] Loaded {Count} profiles from index",
                cachedIndex.Profiles.Count);

            return cachedIndex;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    /// <summary>
    /// Parses GitHub repository URL.
    /// </summary>
    private static (string Owner, string Repo) ParseRepositoryUrl(string url)
    {
        // Handle various GitHub URL formats
        // https://github.com/owner/repo
        // https://api.github.com/repos/owner/repo

        url = url.TrimEnd('/');

        if (url.Contains("/repos/"))
        {
            var parts = url.Split("/repos/")[1].Split('/');
            return (parts[0], parts[1]);
        }

        if (url.Contains("github.com/"))
        {
            var parts = url.Split("github.com/")[1].Split('/');
            return (parts[0], parts[1]);
        }

        throw new ArgumentException($"Invalid GitHub repository URL: {url}");
    }

    /// <summary>
    /// Sanitizes a filename for safe filesystem use.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(fileName);
        foreach (var c in invalid)
        {
            sanitized.Replace(c, '_');
        }
        return sanitized.ToString();
    }
}

// GitHub API Models
internal sealed class GitHubContent
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }
}
