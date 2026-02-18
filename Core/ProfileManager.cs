using System.Collections.Generic;
using System.IO;
using System.Linq;

using WinAPI;

using static Newtonsoft.Json.JsonConvert;

namespace Core;

/// <summary>
/// Manages loading and accessing class and path profile files.
/// </summary>
public sealed class ProfileManager
{
    private readonly DataConfig dataConfig;

    public ProfileManager(DataConfig dataConfig)
    {
        this.dataConfig = dataConfig;
    }

    /// <summary>
    /// Gets all available class configuration files.
    /// </summary>
    public IEnumerable<string> GetClassFiles()
    {
        string root = Path.Join(dataConfig.Class, Path.DirectorySeparatorChar.ToString());
        List<string> files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
            .Select(path => path.Replace(root, string.Empty))
            .OrderBy(x => x, new NaturalStringComparer())
            .ToList();

        files.Insert(0, "Press Init State first!");
        return files;
    }

    /// <summary>
    /// Gets all available path files.
    /// </summary>
    public IEnumerable<string> GetPathFiles()
    {
        string root = Path.Join(dataConfig.Path, Path.DirectorySeparatorChar.ToString());
        List<string> files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
            .Select(path => path.Replace(root, string.Empty))
            .OrderBy(x => x, new NaturalStringComparer())
            .ToList();

        files.Insert(0, "Use Class Profile Default");
        return files;
    }

    /// <summary>
    /// Reads a class configuration from file.
    /// </summary>
    /// <param name="classFile">The class filename.</param>
    /// <returns>The loaded ClassConfiguration.</returns>
    public ClassConfiguration ReadClassConfiguration(string classFile)
    {
        string filePath = Path.Join(dataConfig.Class, classFile);
        return DeserializeObject<ClassConfiguration>(File.ReadAllText(filePath))!;
    }
}
