namespace BugTriageApi.Services;

public class RepoContextService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<RepoContextService> _logger;

    public RepoContextService(IWebHostEnvironment env, ILogger<RepoContextService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string?> GetContextAsync(string repoKey)
    {
        var path = Path.Combine(_env.ContentRootPath, "RepoContext", $"{repoKey}.md");

        if (!File.Exists(path))
        {
            _logger.LogWarning("No context file for repo '{RepoKey}' at {Path}", repoKey, path);
            return null;
        }

        var content = await File.ReadAllTextAsync(path);
        _logger.LogInformation("Loaded context for '{RepoKey}' ({Length} chars)", repoKey, content.Length);
        return content;
    }

    public async Task UpdateContextAsync(string repoKey, string content)
    {
        var path = Path.Combine(_env.ContentRootPath, "RepoContext", $"{repoKey}.md");
        await File.WriteAllTextAsync(path, content);
        _logger.LogInformation("Updated context file for '{RepoKey}' ({Length} chars)", repoKey, content.Length);
    }
}
