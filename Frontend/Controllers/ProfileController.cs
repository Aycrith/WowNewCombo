using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Frontend.Controllers;

/// <summary>
/// API controller for managing class profiles.
/// Provides read and write access to JSON profile files.
/// </summary>
[ApiController]
[Route("api/profiles")]
public sealed class ProfileController : ControllerBase
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };
    
    private readonly ILogger<ProfileController> logger;
    private readonly string profilesDirectory;

    private static bool IsValidProfileName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               !name.Contains("..") &&
               !name.Contains('/') &&
               !name.Contains('\\');
    }

    private bool TryGetProfilePath(string name, out string profilePath, out IActionResult? errorResult)
    {
        errorResult = null;

        if (!IsValidProfileName(name))
        {
            errorResult = BadRequest(new { Error = "Invalid profile name" });
            profilePath = string.Empty;
            return false;
        }

        profilePath = Path.Combine(profilesDirectory, $"{name}.json");
        string fullPath = Path.GetFullPath(profilePath);
        string fullProfilesDir = Path.GetFullPath(profilesDirectory);

        if (!fullPath.StartsWith(fullProfilesDir, StringComparison.OrdinalIgnoreCase))
        {
            errorResult = BadRequest(new { Error = "Invalid profile name" });
            return false;
        }

        return true;
    }

    public ProfileController(ILogger<ProfileController> logger)
    {
        this.logger = logger;

        // Default profile directory relative to current directory
        profilesDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Json",
            "class");

        // Fallback to relative path if not found (e.g., running from different working directory)
        if (!Directory.Exists(profilesDirectory))
        {
            string fallbackPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "Json",
                "class");

            if (Directory.Exists(fallbackPath))
            {
                profilesDirectory = Path.GetFullPath(fallbackPath);
            }
        }
    }

    /// <summary>
    /// GET /api/profiles — Returns list of available class profiles.
    /// </summary>
    [HttpGet]
    public IActionResult List()
    {
        if (!Directory.Exists(profilesDirectory))
        {
            return NotFound(new
            {
                Error = "Profiles directory not found",
                Path = profilesDirectory
            });
        }

        string[] profiles = Directory.GetFiles(profilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name)
            .ToArray()!;

        return Ok(new
        {
            Count = profiles.Length,
            Directory = profilesDirectory,
            Profiles = profiles
        });
    }

    /// <summary>
    /// GET /api/profiles/{name} — Returns a specific class profile.
    /// </summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        if (!Directory.Exists(profilesDirectory))
        {
            return NotFound(new { Error = "Profiles directory not found" });
        }

        if (!TryGetProfilePath(name, out string profilePath, out IActionResult? error))
        {
            return error!;
        }

        if (!System.IO.File.Exists(profilePath))
        {
            return NotFound(new { Error = $"Profile '{name}' not found" });
        }

        try
        {
            string json = await System.IO.File.ReadAllTextAsync(profilePath);
            using JsonDocument doc = JsonDocument.Parse(json);

            return Ok(new
            {
                Name = name,
                Path = profilePath,
                Profile = doc.RootElement.Clone()
            });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[ProfileCtrl    ] Failed to parse profile '{Name}'", name);
            return StatusCode(500, new { Error = "Invalid JSON in profile file" });
        }
    }

    /// <summary>
    /// PUT /api/profiles/{name} — Updates a class profile.
    /// Body: Full profile JSON
    /// </summary>
    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] JsonElement profile)
    {
        if (!Directory.Exists(profilesDirectory))
        {
            return NotFound(new { Error = "Profiles directory not found" });
        }

        if (!TryGetProfilePath(name, out string profilePath, out IActionResult? error))
        {
            return error!;
        }

        if (!System.IO.File.Exists(profilePath))
        {
            return NotFound(new { Error = $"Profile '{name}' not found" });
        }

        try
        {
            // Validate JSON structure
            if (!profile.TryGetProperty("ClassName", out _))
            {
                return BadRequest(new { Error = "Profile must have 'ClassName' property" });
            }

            if (!profile.TryGetProperty("Mode", out _))
            {
                return BadRequest(new { Error = "Profile must have 'Mode' property" });
            }

            // Create backup before overwriting
            string backupPath = profilePath + $".backup_{DateTime.UtcNow:yyyyMMddHHmmss}";
            System.IO.File.Copy(profilePath, backupPath, overwrite: true);

            // Write updated profile with formatting
            string formattedJson = JsonSerializer.Serialize(profile, IndentedJsonOptions);

            await System.IO.File.WriteAllTextAsync(profilePath, formattedJson);

            logger.LogInformation(
                "[ProfileCtrl    ] Profile '{Name}' updated via API (backup: {Backup})",
                name, Path.GetFileName(backupPath));

            return Ok(new
            {
                Name = name,
                Path = profilePath,
                BackupPath = backupPath,
                Updated = true
            });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[ProfileCtrl    ] Failed to update profile '{Name}'", name);
            return BadRequest(new { Error = "Invalid JSON in request body" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ProfileCtrl    ] Failed to write profile '{Name}'", name);
            return StatusCode(500, new { Error = "Failed to write profile file" });
        }
    }

    /// <summary>
    /// GET /api/profiles/{name}/backup — Lists backups for a profile.
    /// </summary>
    [HttpGet("{name}/backups")]
    public IActionResult ListBackups(string name)
    {
        if (!Directory.Exists(profilesDirectory))
        {
            return NotFound(new { Error = "Profiles directory not found" });
        }

        if (!IsValidProfileName(name))
        {
            return BadRequest(new { Error = "Invalid profile name" });
        }

        string[] backups = Directory.GetFiles(profilesDirectory, $"{name}.json.backup_*")
            .Select(Path.GetFileName)
            .OrderByDescending(f => f)
            .ToArray()!;

        return Ok(new
        {
            Profile = name,
            Count = backups.Length,
            Backups = backups
        });
    }
}
