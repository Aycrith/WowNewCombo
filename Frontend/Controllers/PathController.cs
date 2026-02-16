using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using PPather;

using SharedLib.Converters;

using System.Numerics;
using System.Text.Json;

namespace Frontend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PathController : ControllerBase
{
    private readonly DataConfig dataConfig;
    private readonly PPatherService service;

    private readonly JsonSerializerOptions options = new()
    {
        Converters =
        {
            new Vector3Converter(true)
        }
    };

    public PathController(DataConfig dataConfig, PPatherService service)
    {
        this.dataConfig = dataConfig;
        this.service = service;
    }

    [HttpGet()]
    public IActionResult Get(string filter)
    {
        string[] pathFiles = Directory.GetFiles(dataConfig.Path, $"*{filter}*.json", SearchOption.AllDirectories);

        for (int i = 0; i < pathFiles.Length; i++)
        {
            string path = pathFiles[i];
            pathFiles[i] = path.Replace(dataConfig.Root, "");
        }

        return Ok(pathFiles);
    }

    [HttpPost("SavePath")]
    public IActionResult SavePath([FromQuery] string fileName, [FromBody] Vector3[] mapPoints)
    {
        // Validate filename - prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new { Error = "Filename cannot be empty" });
        }

        // Check for path traversal sequences
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(new { Error = "Invalid filename - path traversal detected" });
        }

        // Ensure file has .json extension
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".json";
        }

        // Resolve the full path and verify it's within the allowed directory
        string filePath = Path.GetFullPath(Path.Combine(dataConfig.Path, fileName));
        string fullDataPath = Path.GetFullPath(dataConfig.Path);

        // Security check: verify the resolved path is within the allowed directory
        if (!filePath.StartsWith(fullDataPath, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Error = "Invalid path - file must be within data directory" });
        }

        System.IO.File.WriteAllText(filePath, JsonSerializer.Serialize(mapPoints, options));

        return Ok();
    }

    [HttpGet("GetAreaIdAndZ")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK, "application/json")]
    public IActionResult GetAreaIdAndZ(int mapid, float x, float y)
    {
        service.Initialise(mapid);

        (int areaId, float z) = service.GetAreaIdAndZ(new Vector3(x, y, 0));
        return new JsonResult(new { areaId, z }, options);
    }
}