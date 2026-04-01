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

        var request = new ClaudeRequest
        {
            System = systemPrompt,
            Messages =
            [
                new ClaudeMessage { Role = "user", Content = userPrompt }
            ]
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

        // Strip accidental markdown fences
        text = Regex.Replace(text, @"^```(?:json)?\s*\n?", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\n?```\s*$", "", RegexOptions.Multiline);
        text = text.Trim();

        return JsonSerializer.Deserialize<TriageResult>(text)
            ?? throw new InvalidOperationException("Failed to deserialize triage result from Claude response");
    }
}
