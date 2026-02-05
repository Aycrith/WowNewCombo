using BenchmarkDotNet.Attributes;

using WinAPI;

namespace Benchmarks.ClassProfile;

public sealed class LoadAllProfiles
{
    [Benchmark]
    [ArgumentsSource(nameof(GetProfileNames))]
    public void LoadProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        string? repoRoot = TryFindRepoRoot();
        if (repoRoot != null)
        {
            Directory.SetCurrentDirectory(repoRoot);
        }

        // TODO: fix loading error frame_config.json not exists
        HeadlessServer.Program.Main([$"{profileName}", "-m Local", "--loadonly"]);
    }

    public static IEnumerable<string> GetProfileNames()
    {
        string? repoRoot = TryFindRepoRoot();
        if (repoRoot == null)
        {
            yield return string.Empty;
            yield break;
        }

        string classRoot = Path.Join(repoRoot, "Json", "class");
        if (!Directory.Exists(classRoot))
        {
            yield return string.Empty;
            yield break;
        }

        IEnumerable<string> files = Directory.EnumerateFiles(classRoot, "*.json*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(classRoot, path))
            .OrderBy(x => x, new NaturalStringComparer());

        string? first = files.FirstOrDefault();
        yield return first ?? string.Empty;

        //foreach (var fileName in files)
        //{
        //    yield return fileName;
        //}
    }

    private static string? TryFindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (File.Exists(Path.Join(dir, "MasterOfPuppets.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}
