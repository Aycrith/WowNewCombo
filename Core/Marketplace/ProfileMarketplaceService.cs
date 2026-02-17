using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    private readonly FeatureFlagService? featureFlagService;
    private readonly TimeProvider timeProvider;

    // Cache
    private ProfileIndex? cachedIndex;
    private DateTime lastIndexFetch = DateTime.MinValue;
    private readonly SemaphoreSlim cacheLock = new(1, 1);

    // Download tracking
    private int downloadsThisSession;

    /// <summary>
    /// Trusted domains for profile downloads. Downloads from untrusted hosts are rejected
    /// to prevent token leakage via attacker-controlled DownloadUrl values.
    /// </summary>
    private static readonly HashSet<string> TrustedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw.githubusercontent.com",
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com"
    };

    /// <summary>
    /// Maximum response size for downloads (10 MB).
    /// </summary>
    internal const int MaxResponseSizeBytes = 10 * 1024 * 1024;

    public ProfileMarketplaceService(
        HttpClient httpClient,
        ILogger<ProfileMarketplaceService> logger,
        DataConfig dataConfig,
        IOptions<ProfileMarketplaceOptions> options,
        FeatureFlagService? featureFlagService = null,
        TimeProvider? timeProvider = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dataConfig = dataConfig ?? throw new ArgumentNullException(nameof(dataConfig));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.featureFlagService = featureFlagService;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Creates an HttpRequestMessage with GitHub API headers and per-request auth token.
    /// Token is set per-request (not on DefaultRequestHeaders) to prevent leakage.
    /// Token is only added to requests to trusted GitHub hosts.
    /// </summary>
    internal static HttpRequestMessage CreateGitHubRequest(HttpMethod method, string url)
    {
        HttpRequestMessage request = new(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WowClassicGrindBot", "1.0"));

        // Only add auth token to trusted GitHub hosts to prevent token leakage
        if (IsTrustedDownloadUrl(url))
        {
            string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return request;
    }

    /// <summary>
    /// Checks if the marketplace feature is enabled via feature flags.
    /// </summary>
    public bool IsEnabled()
    {
        if (featureFlagService == null)
        {
            return true;
        }

        return featureFlagService.Current.ProfileMarketplace.Enabled;
    }

    /// <summary>
    /// Validates that a download URL points to a trusted GitHub host.
    /// </summary>
    internal static bool IsTrustedDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps && TrustedDownloadHosts.Contains(uri.Host);
    }

    /// <summary>
    /// Searches for profiles matching the criteria.
    /// </summary>
    public async Task<IReadOnlyList<ProfileListing>> SearchProfilesAsync(
        ProfileSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            logger.LogDebug("[Marketplace   ] Feature is disabled");
            return Array.Empty<ProfileListing>();
        }

        try
        {
            ProfileIndex index = await GetIndexAsync(cancellationToken);

            IEnumerable<ProfileListing> results = index.Profiles.AsEnumerable();

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
                string searchLower = criteria.SearchText.ToLowerInvariant();
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
        if (!IsEnabled())
        {
            return new DownloadResult(
                Success: false,
                ErrorMessage: "Profile marketplace is disabled");
        }

        // Check download limit
        if (downloadsThisSession >= options.MaxDownloadsPerSession)
        {
            return new DownloadResult(
                Success: false,
                ErrorMessage: $"Download limit reached ({options.MaxDownloadsPerSession} per session)");
        }

        try
        {
            ProfileIndex index = await GetIndexAsync(cancellationToken);
            ProfileListing? listing = index.Profiles.FirstOrDefault(p => p.Id == profileId);

            if (listing == null)
            {
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Profile '{profileId}' not found");
            }

            // Validate download URL against trusted hosts
            if (!IsTrustedDownloadUrl(listing.DownloadUrl))
            {
                logger.LogWarning("[Marketplace   ] Rejected untrusted download URL: {Url}", listing.DownloadUrl);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download URL is not from a trusted source");
            }

            logger.LogInformation("[Marketplace   ] Downloading profile {Id}", profileId);

            // Download content with size limit
            using HttpRequestMessage request = new(HttpMethod.Get, listing.DownloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WowClassicGrindBot", "1.0"));

            using HttpResponseMessage response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Check content length before reading body
            if (response.Content.Headers.ContentLength > MaxResponseSizeBytes)
            {
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Profile too large ({response.Content.Headers.ContentLength} bytes)");
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (content.Length > MaxResponseSizeBytes)
            {
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Profile content too large ({content.Length} bytes)");
            }

            // Validate JSON
            try
            {
                using JsonDocument doc = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid JSON in profile: {ex.Message}");
            }

            // Sanitize filename and validate path containment
            string safeName = SanitizeFileName($"{listing.ClassName}_{listing.Name}_{listing.Id}.json");
            string targetPath = Path.Combine(dataConfig.Class, safeName);

            // Path containment check: ensure the resolved path is within the expected directory
            string fullTargetPath = Path.GetFullPath(targetPath);
            string fullBaseDir = Path.GetFullPath(dataConfig.Class);
            if (!fullTargetPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("[Marketplace   ] Path traversal blocked: {Path}", targetPath);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Invalid profile path");
            }

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
        if (!IsEnabled())
        {
            return Array.Empty<ProfileListing>();
        }

        ProfileIndex index = await GetIndexAsync(cancellationToken);

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
            DateTime now = timeProvider.GetUtcNow().DateTime;

            // Check if cache is still valid
            if (cachedIndex != null &&
                now - lastIndexFetch < TimeSpan.FromMinutes(options.CacheDurationMinutes))
            {
                logger.LogDebug("[Marketplace   ] Using cached index");
                return cachedIndex;
            }

            // Fetch from remote
            logger.LogInformation("[Marketplace   ] Fetching profile index from {Repo}", options.RepositoryUrl);

            // Parse repository URL
            (string Owner, string Repo) repoInfo = ParseRepositoryUrl(options.RepositoryUrl);

            // GitHub API URL for contents
            string apiUrl = $"https://api.github.com/repos/{repoInfo.Owner}/{repoInfo.Repo}/contents/index.json";

            using HttpRequestMessage request = CreateGitHubRequest(HttpMethod.Get, apiUrl);
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            GitHubContent? response = await httpResponse.Content.ReadFromJsonAsync<GitHubContent>(
                cancellationToken: cancellationToken);

            if (response?.Content == null)
            {
                throw new InvalidOperationException("GitHub API returned empty content");
            }

            // Check base64 size before decoding
            if (response.Content.Length > MaxResponseSizeBytes)
            {
                throw new InvalidOperationException($"Index content too large ({response.Content.Length} bytes)");
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

            lastIndexFetch = now;
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
    internal static (string Owner, string Repo) ParseRepositoryUrl(string url)
    {
        // Handle various GitHub URL formats
        // https://github.com/owner/repo
        // https://api.github.com/repos/owner/repo

        url = url.TrimEnd('/');

        if (url.Contains("/repos/"))
        {
            string[] parts = url.Split("/repos/")[1].Split('/');
            return (parts[0], parts[1]);
        }

        if (url.Contains("github.com/"))
        {
            string[] parts = url.Split("github.com/")[1].Split('/');
            return (parts[0], parts[1]);
        }

        throw new ArgumentException($"Invalid GitHub repository URL: {url}");
    }

    /// <summary>
    /// Sanitizes a filename for safe filesystem use.
    /// Strips directory components and replaces invalid characters.
    /// </summary>
    internal static string SanitizeFileName(string fileName)
    {
        // Strip any directory components first (defense-in-depth against path traversal)
        fileName = Path.GetFileName(fileName);

        // Replace invalid filename characters
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sanitized = new(fileName);
        foreach (char c in invalid)
        {
            sanitized.Replace(c, '_');
        }

        string result = sanitized.ToString();

        // Verify the result is a valid filename (contains no directory separators after sanitization)
        // This prevents edge cases where exotic Unicode characters might reconstruct path traversal
        if (result.Contains(Path.DirectorySeparatorChar) || result.Contains(Path.AltDirectorySeparatorChar))
        {
            result = Path.GetFileName(result);
        }

        return result;
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
