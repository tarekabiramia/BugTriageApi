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
}

public class AffectedFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
