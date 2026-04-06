using System.Text.Json.Serialization;

namespace BugTriageApi.Models;

public class TriageResult
{
    [JsonPropertyName("complexityScore")]
    public int ComplexityScore { get; set; }

    [JsonPropertyName("complexityReason")]
    public string ComplexityReason { get; set; } = string.Empty;

    [JsonPropertyName("rootCauseHypothesis")]
    public string RootCauseHypothesis { get; set; } = string.Empty;

    [JsonPropertyName("affectedFiles")]
    public List<AffectedFile> AffectedFiles { get; set; } = [];

    [JsonPropertyName("suggestedFix")]
    public string? SuggestedFix { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    // Pre-formatted fields for Power Automate (flat strings, no parsing needed)
    [JsonPropertyName("plannerDescription")]
    public string PlannerDescription { get; set; } = string.Empty;

    [JsonPropertyName("teamsMessage")]
    public string TeamsMessage { get; set; } = string.Empty;

    [JsonPropertyName("affectedFilesText")]
    public string AffectedFilesText { get; set; } = string.Empty;

    [JsonPropertyName("complexityLabel")]
    public string ComplexityLabel { get; set; } = string.Empty;

    [JsonPropertyName("autoFixResult")]
    public AutoFixResult? AutoFixResult { get; set; }

    [JsonPropertyName("prUrl")]
    public string? PrUrl { get; set; }
}

public class AffectedFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
