using System.Text.RegularExpressions;
using BugTriageApi.Models;

namespace BugTriageApi.Services;

public class AutoFixService
{
    private readonly GitHubService _gitHubService;
    private readonly ClaudeService _claudeService;
    private readonly ILogger<AutoFixService> _logger;

    private const int MinConfidence = 7;

    public AutoFixService(GitHubService gitHubService, ClaudeService claudeService, ILogger<AutoFixService> logger)
    {
        _gitHubService = gitHubService;
        _claudeService = claudeService;
        _logger = logger;
    }

    public async Task<AutoFixResult> CreateAutoFixAsync(BugReportRequest bug, TriageResult triage)
    {
        var branchName = GenerateBranchName(bug.Title);
        var result = new AutoFixResult { BranchName = branchName };

        try
        {
            // Step 1: Fetch content + SHA for each affected file (parallel)
            var filePaths = triage.AffectedFiles.Select(f => f.Path).ToList();
            var fileMetadataTasks = filePaths.Select(p => FetchFileMetadataSafe(p));
            var fileResults = await Task.WhenAll(fileMetadataTasks);

            var fileContents = new Dictionary<string, (string Content, string Sha)>();
            for (var i = 0; i < filePaths.Count; i++)
            {
                if (fileResults[i] != null)
                    fileContents[filePaths[i]] = fileResults[i]!.Value;
            }

            if (fileContents.Count == 0)
            {
                result.Message = "Could not fetch any affected files from GitHub";
                return result;
            }

            // Step 2: Generate fixes via Claude
            _logger.LogInformation("Generating fixes for {Count} files", fileContents.Count);
            var fixes = await _claudeService.GenerateFixAsync(bug, triage, fileContents);

            // Step 3: Review each fix via Claude
            var approvedFixes = new List<FixResult>();
            foreach (var fix in fixes)
            {
                if (fix.Confidence < MinConfidence)
                {
                    _logger.LogInformation("Skipping {Path} — confidence {Confidence}/10 below threshold", fix.FilePath, fix.Confidence);
                    result.FixesSkipped.Add(new FixSummary
                    {
                        FilePath = fix.FilePath,
                        Reason = $"Low confidence: {fix.Confidence}/10"
                    });
                    continue;
                }

                var bugContext = $"Bug: {bug.Title}. Root cause: {triage.RootCauseHypothesis}. Fix: {fix.ChangeDescription}";
                var review = await _claudeService.ReviewFixAsync(fix.FilePath, fix.OriginalContent, fix.FixedContent, bugContext);

                fix.ReviewApproved = review.Approved;
                fix.ReviewNotes = review.Notes;

                if (review.Approved)
                {
                    approvedFixes.Add(fix);
                    _logger.LogInformation("Fix approved for {Path} — confidence {Confidence}/10", fix.FilePath, fix.Confidence);
                }
                else
                {
                    _logger.LogInformation("Fix rejected for {Path}: {Notes}", fix.FilePath, review.Notes);
                    result.FixesSkipped.Add(new FixSummary
                    {
                        FilePath = fix.FilePath,
                        Reason = $"Review rejected: {string.Join("; ", review.Issues)}"
                    });
                }
            }

            if (approvedFixes.Count == 0)
            {
                result.Message = "No fixes passed review — manual intervention needed";
                return result;
            }

            // Step 4: Create branch
            _logger.LogInformation("Creating branch {Branch}", branchName);
            await _gitHubService.CreateBranchAsync(branchName);

            // Step 5: Commit each approved fix
            foreach (var fix in approvedFixes)
            {
                var sha = fileContents[fix.FilePath].Sha;
                var commitMsg = $"fix: {fix.ChangeDescription}";
                await _gitHubService.UpdateFileAsync(fix.FilePath, fix.FixedContent, commitMsg, branchName, sha);

                // Update SHA for subsequent commits to the same file
                var (_, newSha) = await _gitHubService.GetFileMetadataAsync(fix.FilePath, branchName);
                fileContents[fix.FilePath] = (fix.FixedContent, newSha);

                result.FixesApplied.Add(new FixSummary
                {
                    FilePath = fix.FilePath,
                    Reason = fix.ChangeDescription
                });
            }

            // Step 6: Open PR
            var prTitle = $"[Auto-Fix] {bug.Title}";
            var prBody = BuildPrDescription(bug, triage, approvedFixes);
            var prUrl = await _gitHubService.CreatePullRequestAsync(prTitle, prBody, branchName);

            result.PrUrl = prUrl;
            result.Success = true;
            result.Message = $"PR created with {approvedFixes.Count} fix(es)";

            _logger.LogInformation("Auto-fix PR created: {PrUrl}", prUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-fix pipeline failed for bug: {Title}", bug.Title);
            result.Message = $"Auto-fix failed: {ex.Message}";
        }

        return result;
    }

    private async Task<(string Content, string Sha)?> FetchFileMetadataSafe(string path)
    {
        try
        {
            return await _gitHubService.GetFileMetadataAsync(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch file: {Path}", path);
            return null;
        }
    }

    private static string GenerateBranchName(string bugTitle)
    {
        var slug = Regex.Replace(bugTitle.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return $"autofix/{slug}-{timestamp}";
    }

    private static string BuildPrDescription(BugReportRequest bug, TriageResult triage, List<FixResult> fixes)
    {
        var fixDetails = string.Join("\n", fixes.Select(f =>
            $"### `{f.FilePath}`\n- **Change:** {f.ChangeDescription}\n- **Confidence:** {f.Confidence}/10\n- **Review:** {f.ReviewNotes}"));

        return $"""
            ## Auto-Fix: {bug.Title}

            **Complexity:** {triage.ComplexityScore}/5 — {triage.ComplexityReason}
            **Root Cause:** {triage.RootCauseHypothesis}
            **Severity:** {bug.Severity}
            **Reporter:** {bug.ReporterName}

            ---

            ## Bug Details
            - **Area:** {bug.Area}
            - **Page/Feature:** {bug.PageOrFeature}
            - **Steps to Reproduce:** {bug.StepsToReproduce}
            - **Expected:** {bug.Expected}
            - **Actual:** {bug.Actual}

            ---

            ## Fixes Applied

            {fixDetails}

            ---

            > This PR was automatically generated by BugTriageApi.
            > A human review is required before merging.
            """;
    }
}
