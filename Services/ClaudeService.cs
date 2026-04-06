using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BugTriageApi.Models;

namespace BugTriageApi.Services;

public class ClaudeService
{
    private readonly HttpClient _httpClient;

    public ClaudeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TriageResult> TriageBugAsync(BugReportRequest bug, List<string> fileTree)
    {
        var fileTreeText = string.Join("\n", fileTree.Select(f => $"  {f}"));

        var systemPrompt = $$"""
            You are a senior full-stack developer triaging bugs for a .NET Core 8 + Vue 3 construction/maintenance company website.

            Here is the repository file tree:
            <file_tree>
            {{fileTreeText}}
            </file_tree>

            Complexity scale:
            1 = Typo, wrong CSS class, missing null check — one file, obvious fix
            2 = Simple logic error, missing validation — 1-2 files, clear fix
            3 = Business logic bug, state management — multiple files, needs investigation
            4 = Cross-cutting concern, race condition — architectural understanding needed
            5 = Intermittent, environment-specific — needs manual debugging

            Rules:
            - Be specific about which files are affected and why
            - Rate complexity honestly based on the scale above
            - Only suggest a fix for complexity 1-2
            - Respond with JSON only — no markdown, no explanation
            """;

        var userPrompt = $$"""
            Triage this bug report:

            Title: {{bug.Title}}
            Area: {{bug.Area}}
            Page/Feature: {{bug.PageOrFeature}}
            Steps to Reproduce: {{bug.StepsToReproduce}}
            Expected Behavior: {{bug.Expected}}
            Actual Behavior: {{bug.Actual}}
            Severity: {{bug.Severity}}
            Reporter: {{bug.ReporterName}}
            Screenshot: {{bug.Screenshot ?? "None"}}

            Respond ONLY with a JSON object in this exact structure (no markdown fences, no extra text):
            {
              "complexityScore": <number 1-5>,
              "complexityReason": "<why this complexity>",
              "rootCauseHypothesis": "<what you think is wrong>",
              "affectedFiles": [
                { "path": "<file path>", "reason": "<why this file>" }
              ],
              "suggestedFix": "<fix description or null if complexity > 2>",
              "action": "<auto-fix|review-needed|manual>",
              "summary": "<one-line summary>"
            }
            """;

        var responseText = await SendClaudeRequestAsync(systemPrompt, userPrompt);
        return JsonSerializer.Deserialize<TriageResult>(responseText)
            ?? throw new InvalidOperationException("Failed to deserialize triage result from Claude response");
    }

    public async Task<List<FixResult>> GenerateFixAsync(
        BugReportRequest bug,
        TriageResult triage,
        Dictionary<string, (string Content, string Sha)> fileContents)
    {
        var filesSection = string.Join("\n\n", fileContents.Select(f =>
            $"--- FILE: {f.Key} ---\n{f.Value.Content}\n--- END FILE ---"));

        var systemPrompt = $$"""
            You are a senior developer fixing a bug in a .NET Core 8 + Vue 3 construction company website.

            Bug Report:
            Title: {{bug.Title}}
            Area: {{bug.Area}}
            Root Cause: {{triage.RootCauseHypothesis}}
            Suggested Fix: {{triage.SuggestedFix ?? "None"}}

            Here are the affected files:
            {{filesSection}}

            Rules:
            - Return the COMPLETE fixed file content for each file — not a diff, not a snippet
            - Only fix what is broken — do NOT refactor, rename, or reorganize anything else
            - Rate your confidence 1-10 for each fix (10 = certain, 1 = guessing)
            - If you are not confident about a file, still include it but rate it low
            - Respond with JSON only — no markdown, no explanation
            """;

        var userPrompt = """
            Generate the fixes. Respond ONLY with a JSON object (no markdown fences):
            {
              "fixes": [
                {
                  "filePath": "<exact file path>",
                  "fixedContent": "<complete fixed file content>",
                  "confidence": <1-10>,
                  "changeDescription": "<what you changed and why>"
                }
              ]
            }
            """;

        var responseText = await SendClaudeRequestAsync(systemPrompt, userPrompt);
        var fixResponse = JsonSerializer.Deserialize<ClaudeFixResponse>(responseText)
            ?? throw new InvalidOperationException("Failed to deserialize fix response");

        return fixResponse.Fixes.Select(f => new FixResult
        {
            FilePath = f.FilePath,
            OriginalContent = fileContents.TryGetValue(f.FilePath, out var meta) ? meta.Content : "",
            FixedContent = f.FixedContent,
            Confidence = f.Confidence,
            ChangeDescription = f.ChangeDescription
        }).ToList();
    }

    public async Task<ClaudeReviewResponse> ReviewFixAsync(string filePath, string originalContent, string fixedContent, string bugContext)
    {
        var systemPrompt = $$"""
            You are a strict senior code reviewer. Review this bug fix for:
            1. Correctness — does it actually fix the reported bug?
            2. Side effects — does it break anything else?
            3. Security — does it introduce any vulnerabilities?
            4. Completeness — is the fix sufficient or partial?

            Bug context: {{bugContext}}

            Be strict. If anything is wrong, do NOT approve.
            Respond with JSON only — no markdown, no explanation.
            """;

        var userPrompt = $$"""
            File: {{filePath}}

            ORIGINAL:
            {{originalContent}}

            FIXED:
            {{fixedContent}}

            Respond ONLY with a JSON object (no markdown fences):
            {
              "approved": <true or false>,
              "issues": ["<issue 1>", "<issue 2>"],
              "notes": "<overall review notes>"
            }
            """;

        var responseText = await SendClaudeRequestAsync(systemPrompt, userPrompt);
        return JsonSerializer.Deserialize<ClaudeReviewResponse>(responseText)
            ?? throw new InvalidOperationException("Failed to deserialize review response");
    }

    private async Task<string> SendClaudeRequestAsync(string systemPrompt, string userPrompt)
    {
        var request = new ClaudeRequest
        {
            System = systemPrompt,
            Messages = [new ClaudeMessage { Role = "user", Content = userPrompt }]
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("v1/messages", httpContent);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Claude API error ({response.StatusCode}): {responseBody}");

        var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseBody)
            ?? throw new InvalidOperationException("Failed to deserialize Claude response");

        var text = claudeResponse.Content.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in Claude response");

        text = Regex.Replace(text, @"^```(?:json)?\s*\n?", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\n?```\s*$", "", RegexOptions.Multiline);
        return text.Trim();
    }
}
