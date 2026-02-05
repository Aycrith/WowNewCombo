using System;
using System.IO;

using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigFilesController : ControllerBase
{
    private readonly DataConfig dataConfig;

    public ConfigFilesController(DataConfig dataConfig)
    {
        this.dataConfig = dataConfig;
    }

    [HttpGet("export/class")]
    public IActionResult ExportClass([FromQuery] string file)
    {
        return ExportFromBaseDir(dataConfig.Class, file);
    }

    [HttpGet("export/path")]
    public IActionResult ExportPath([FromQuery] string file)
    {
        return ExportFromBaseDir(dataConfig.Path, file);
    }

    private IActionResult ExportFromBaseDir(string baseDir, string file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(new { Error = "Missing 'file' query parameter." });
        }

        // Normalize relative path and prevent traversal/rooted paths.
        file = file.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(file))
        {
            return BadRequest(new { Error = "Rooted paths are not allowed." });
        }

        string baseFull = Path.GetFullPath(baseDir);
        string fullPath = Path.GetFullPath(Path.Combine(baseFull, file));

        if (!fullPath.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Error = "Path traversal detected." });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { Error = "File not found." });
        }

        string downloadName = Path.GetFileName(fullPath);
        return PhysicalFile(fullPath, "application/json", downloadName);
    }
}

