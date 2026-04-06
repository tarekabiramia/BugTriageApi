using BugTriageApi.Models;

namespace BugTriageApi.Services;

public class TriageService
{
    private readonly GitHubService _gitHubService;
    private readonly ClaudeService _claudeService;
    private readonly AutoFixService _autoFixService;
    private readonly ILogger<TriageService> _logger;

    public TriageService(GitHubService gitHubService, ClaudeService claudeService, AutoFixService autoFixService, ILogger<TriageService> logger)
    {
        _gitHubService = gitHubService;
        _claudeService = claudeService;
        _autoFixService = autoFixService;
        _logger = logger;
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

        // Attempt auto-fix for low complexity bugs
        if (result.Action == "auto-fix")
        {
            try
            {
                _logger.LogInformation("Triggering auto-fix for bug: {Title}", bug.Title);
                var autoFixResult = await _autoFixService.CreateAutoFixAsync(bug, result);
                result.AutoFixResult = autoFixResult;
                result.PrUrl = autoFixResult.PrUrl;

                if (!autoFixResult.Success)
                {
                    _logger.LogWarning("Auto-fix did not produce a PR: {Message}", autoFixResult.Message);
                    result.Action = "review-needed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-fix failed, falling back to review-needed");
                result.Action = "review-needed";
                result.AutoFixResult = new AutoFixResult
                {
                    Success = false,
                    Message = $"Auto-fix failed: {ex.Message}"
                };
            }
        }

        // Build flat fields for Power Automate
        result.ComplexityLabel = $"{result.ComplexityScore}/5 — {result.ComplexityReason}";

        result.AffectedFilesText = string.Join("\n",
            result.AffectedFiles.Select(f => $"- {f.Path}: {f.Reason}"));

        var prLine = result.PrUrl != null ? $"\nPR: {result.PrUrl}" : "";

        result.PlannerDescription = $"""
            --- BUG REPORT ---
            Title: {bug.Title}
            Area: {bug.Area}
            Page/Feature: {bug.PageOrFeature}
            Severity: {bug.Severity}
            Reporter: {bug.ReporterName}

            Steps to Reproduce:
            {bug.StepsToReproduce}

            Expected: {bug.Expected}
            Actual: {bug.Actual}

            --- AI TRIAGE RESULTS ---
            Complexity: {result.ComplexityLabel}
            Action: {result.Action}
            Root Cause: {result.RootCauseHypothesis}
            Summary: {result.Summary}
            Suggested Fix: {result.SuggestedFix ?? "N/A (complexity too high)"}

            Affected Files:
            {result.AffectedFilesText}{prLine}
            """;

        result.TeamsMessage = $"""
            Bug Triaged!

            Title: {bug.Title}
            Complexity: {result.ComplexityLabel}
            Action: {result.Action}
            Root Cause: {result.RootCauseHypothesis}
            Summary: {result.Summary}
            Suggested Fix: {result.SuggestedFix ?? "N/A"}{prLine}
            """;

        return result;
    }
}
