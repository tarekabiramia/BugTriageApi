using System.Text;
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

    public async Task<List<string>> GetFileTreeAsync(string branch = "main")
    {
        var url = $"repos/{_owner}/{_repo}/git/trees/{branch}?recursive=1";
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

    public async Task<(string Content, string Sha)> GetFileMetadataAsync(string path, string branch = "main")
    {
        var url = $"repos/{_owner}/{_repo}/contents/{path}?ref={branch}";
        var root = await GetJsonAsync(url, $"GetFileMetadata({path})");

        var content = root.GetProperty("content").GetString() ?? "";
        var cleaned = content.Replace("\n", "");
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
        var sha = root.GetProperty("sha").GetString() ?? "";

        return (decoded, sha);
    }

    public async Task<string> GetFileContentAsync(string path, string branch = "main")
    {
        var (content, _) = await GetFileMetadataAsync(path, branch);
        return content;
    }

    public async Task<string> GetFileShaAsync(string path, string branch = "main")
    {
        var (_, sha) = await GetFileMetadataAsync(path, branch);
        return sha;
    }

    public async Task<string> GetBranchShaAsync(string branch = "main")
    {
        var url = $"repos/{_owner}/{_repo}/git/ref/heads/{branch}";
        var root = await GetJsonAsync(url, $"GetBranchSha({branch})");
        return root.GetProperty("object").GetProperty("sha").GetString() ?? "";
    }

    public async Task CreateBranchAsync(string branchName, string fromBranch = "main")
    {
        var sha = await GetBranchShaAsync(fromBranch);

        var payload = JsonSerializer.Serialize(new { @ref = $"refs/heads/{branchName}", sha });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var url = $"repos/{_owner}/{_repo}/git/refs";
        var response = await _httpClient.PostAsync(url, content);
        await EnsureSuccessAsync(response, "CreateBranch", branchName);
    }

    public async Task UpdateFileAsync(string path, string fileContent, string commitMessage, string branch, string fileSha)
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

        var url = $"repos/{_owner}/{_repo}/contents/{path}";
        var response = await _httpClient.PutAsync(url, content);
        await EnsureSuccessAsync(response, "UpdateFile", $"{path} on {branch}");
    }

    public async Task<string> CreatePullRequestAsync(string title, string body, string head, string baseBranch = "main")
    {
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            head,
            @base = baseBranch
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var url = $"repos/{_owner}/{_repo}/pulls";
        var response = await _httpClient.PostAsync(url, content);
        await EnsureSuccessAsync(response, "CreatePullRequest", $"{head} → {baseBranch}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("html_url").GetString() ?? "";
    }
}
