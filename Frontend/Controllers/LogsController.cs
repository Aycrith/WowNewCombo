using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Frontend.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log",
        ".txt",
        ".json"
    };

    private readonly ILogger<LogsController> logger;
    private readonly IHostEnvironment env;

    public LogsController(ILogger<LogsController> logger, IHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    [HttpGet("files")]
    public IActionResult ListFiles()
    {
        string root = env.ContentRootPath;
        string logsDir = Path.Combine(root, "logs");

        List<object> results = [];

        AddMatches(results, root, "out*.log");
        AddMatches(results, logsDir, "*.*");

        return Ok(new
        {
            ContentRoot = root,
            LogsDir = logsDir,
            Files = results
        });
    }

    [HttpGet("tail")]
    public IActionResult Tail([FromQuery] string file, [FromQuery] int lines = 200)
    {
        if (!TryResolveFile(file, out string fullPath, out string error))
        {
            return BadRequest(new { Error = error });
        }

        lines = Math.Clamp(lines, 1, 2000);

        try
        {
            string[] lastLines;
            using (FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new(stream))
            {
                Queue<string> tail = new(lines);
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line == null)
                    {
                        continue;
                    }

                    if (tail.Count == lines)
                    {
                        tail.Dequeue();
                    }

                    tail.Enqueue(line);
                }

                lastLines = tail.ToArray();
            }

            FileInfo info = new(fullPath);
            return Ok(new
            {
                File = file,
                FullPath = fullPath,
                Lines = lastLines.Length,
                SizeBytes = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                Content = string.Join('\n', lastLines)
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tail failed for {File}", file);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("download")]
    public IActionResult Download([FromQuery] string file)
    {
        if (!TryResolveFile(file, out string fullPath, out string error))
        {
            return BadRequest(new { Error = error });
        }

        string contentType = "text/plain";
        return PhysicalFile(fullPath, contentType, Path.GetFileName(fullPath));
    }

    private static void AddMatches(List<object> results, string dir, string pattern)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string fullPath in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
        {
            string ext = Path.GetExtension(fullPath);
            if (!AllowedExtensions.Contains(ext))
            {
                continue;
            }

            FileInfo info = new(fullPath);
            results.Add(new
            {
                Name = info.Name,
                Relative = Path.GetFileName(dir) == "logs" ? $"logs/{info.Name}" : info.Name,
                SizeBytes = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc
            });
        }
    }

    private bool TryResolveFile(string file, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(file))
        {
            error = "file is required";
            return false;
        }

        string root = env.ContentRootPath;

        string normalized = file.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            error = "invalid file path";
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            error = "invalid file path";
            return false;
        }

        string ext = Path.GetExtension(candidate);
        if (!AllowedExtensions.Contains(ext))
        {
            error = $"extension not allowed: {ext}";
            return false;
        }

        if (!System.IO.File.Exists(candidate))
        {
            error = "file not found";
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
