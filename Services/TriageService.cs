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

        // Build flat fields for Power Automate
        result.ComplexityLabel = $"{result.ComplexityScore}/5 — {result.ComplexityReason}";

        result.AffectedFilesText = string.Join("\n",
            result.AffectedFiles.Select(f => $"- {f.Path}: {f.Reason}"));

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
            {result.AffectedFilesText}
            """;

        result.TeamsMessage = $"""
            Bug Triaged!

            Title: {bug.Title}
            Complexity: {result.ComplexityLabel}
            Action: {result.Action}
            Root Cause: {result.RootCauseHypothesis}
            Summary: {result.Summary}
            Suggested Fix: {result.SuggestedFix ?? "N/A"}
            """;

        return result;
    }
}
