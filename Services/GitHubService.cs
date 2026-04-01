using System.Text.Json;

namespace BugTriageApi.Services;

public class GitHubService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;

    private static readonly HashSet<string> AllowedExtensions =
    [
        ".cs", ".vue", ".ts", ".js", ".tsx", ".jsx",
        ".json", ".csproj", ".sln", ".css", ".scss"
    ];

    private static readonly string[] ExcludedPrefixes =
    [
        "bin/", "obj/", "node_modules/", "dist/",
        ".git/", ".vs/", ".idea/"
    ];

    public GitHubService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _owner = config["GitHub:Owner"] ?? Environment.GetEnvironmentVariable("GITHUB_OWNER") ?? "tarekabiramia";
        _repo = config["GitHub:Repo"] ?? Environment.GetEnvironmentVariable("GITHUB_REPO") ?? "deka-construction";
    }

    public async Task<List<string>> GetFileTreeAsync(string branch = "main")
    {
        var url = $"repos/{_owner}/{_repo}/git/trees/{branch}?recursive=1";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var tree = doc.RootElement.GetProperty("tree");
        var files = new List<string>();

        foreach (var node in tree.EnumerateArray())
        {
            if (node.GetProperty("type").GetString() != "blob")
                continue;

            var path = node.GetProperty("path").GetString() ?? "";

            if (ExcludedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;

            var ext = Path.GetExtension(path);
            if (AllowedExtensions.Contains(ext))
                files.Add(path);
        }

        return files;
    }

    public async Task<string> GetFileContentAsync(string path, string branch = "main")
    {
        var url = $"repos/{_owner}/{_repo}/contents/{path}?ref={branch}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var content = doc.RootElement.GetProperty("content").GetString() ?? "";
        var cleaned = content.Replace("\n", "");
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
    }
}
