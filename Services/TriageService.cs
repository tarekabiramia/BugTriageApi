using BugTriageApi.Models;

namespace BugTriageApi.Services;

public class TriageService
{
    private readonly GitHubService _gitHubService;
    private readonly ClaudeService _claudeService;

    public TriageService(GitHubService gitHubService, ClaudeService claudeService)
    {
        _gitHubService = gitHubService;
        _claudeService = claudeService;
    }

    public async Task<TriageResult> TriageAsync(BugReportRequest bug)
    {
        var fileTree = await _gitHubService.GetFileTreeAsync();
        var result = await _claudeService.TriageBugAsync(bug, fileTree);

        // Override action based on complexity thresholds
        result.Action = result.ComplexityScore switch
        {
            <= 2 => "auto-fix",
            <= 3 => "review-needed",
            _ => "manual"
        };

        return result;
    }
}
