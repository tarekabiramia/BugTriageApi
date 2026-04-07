using System.Text;
using System.Text.Json;
using BugTriageApi.Models;

namespace BugTriageApi.Services;

public class GitHubService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, RepoConfig> _repos;

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
        _repos = config.GetSection("Repositories")
            .GetChildren()
            .ToDictionary(
                section => section.Key,
                section => new RepoConfig
                {
                    Owner = section["Owner"] ?? "",
                    Repo = section["Repo"] ?? "",
                    DefaultBranch = section["DefaultBranch"] ?? "main",
                    DisplayName = section["DisplayName"] ?? section.Key
                });

    }

    public RepoConfig ResolveRepo(string repoKey)
    {
        if (_repos.TryGetValue(repoKey, out var config))
            return config;

        throw new ArgumentException($"Unknown repository: '{repoKey}'. Available: {string.Join(", ", _repos.Keys)}");
    }

    private string RepoBase(RepoConfig repo) => $"repos/{repo.Owner}/{repo.Repo}";

    private async Task<JsonElement> GetJsonAsync(string url, string operation)
    {
        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response, operation, url);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, string context = "")
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"GitHub {operation} failed ({response.StatusCode}): {body}. Context: {context}");
    }

    public async Task<List<string>> GetFileTreeAsync(RepoConfig repo)
    {
        var url = $"{RepoBase(repo)}/git/trees/{repo.DefaultBranch}?recursive=1";
        var root = await GetJsonAsync(url, "GetFileTree");

        var tree = root.GetProperty("tree");
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

    public async Task<(string Content, string Sha)> GetFileMetadataAsync(RepoConfig repo, string path)
    {
        var url = $"{RepoBase(repo)}/contents/{path}?ref={repo.DefaultBranch}";
        var root = await GetJsonAsync(url, $"GetFileMetadata({path})");

        var content = root.GetProperty("content").GetString() ?? "";
        var cleaned = content.Replace("\n", "");
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
        var sha = root.GetProperty("sha").GetString() ?? "";

        return (decoded, sha);
    }

    public async Task<(string Content, string Sha)> GetFileMetadataAsync(RepoConfig repo, string path, string branch)
    {
        var url = $"{RepoBase(repo)}/contents/{path}?ref={branch}";
        var root = await GetJsonAsync(url, $"GetFileMetadata({path})");

        var content = root.GetProperty("content").GetString() ?? "";
        var cleaned = content.Replace("\n", "");
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
        var sha = root.GetProperty("sha").GetString() ?? "";

        return (decoded, sha);
    }

    public async Task<string> GetBranchShaAsync(RepoConfig repo, string branch)
    {
        var url = $"{RepoBase(repo)}/git/ref/heads/{branch}";
        var root = await GetJsonAsync(url, $"GetBranchSha({branch})");
        return root.GetProperty("object").GetProperty("sha").GetString() ?? "";
    }

    public async Task CreateBranchAsync(RepoConfig repo, string branchName)
    {
        var sha = await GetBranchShaAsync(repo, repo.DefaultBranch);

        var payload = JsonSerializer.Serialize(new { @ref = $"refs/heads/{branchName}", sha });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var url = $"{RepoBase(repo)}/git/refs";
        var response = await _httpClient.PostAsync(url, content);
        await EnsureSuccessAsync(response, "CreateBranch", branchName);
    }

    public async Task UpdateFileAsync(RepoConfig repo, string path, string fileContent, string commitMessage, string branch, string fileSha)
    {
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileContent));

        var payload = JsonSerializer.Serialize(new
        {
            message = commitMessage,
            content = base64Content,
            sha = fileSha,
            branch
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var url = $"{RepoBase(repo)}/contents/{path}";
        var response = await _httpClient.PutAsync(url, content);
        await EnsureSuccessAsync(response, "UpdateFile", $"{path} on {branch}");
    }

    public async Task<string> CreatePullRequestAsync(RepoConfig repo, string title, string body, string head)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            head,
            @base = repo.DefaultBranch
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var url = $"{RepoBase(repo)}/pulls";
        var response = await _httpClient.PostAsync(url, content);
        await EnsureSuccessAsync(response, "CreatePullRequest", $"{head} → {repo.DefaultBranch}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("html_url").GetString() ?? "";
    }
}
