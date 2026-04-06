using System.Text.Json.Serialization;

namespace BugTriageApi.Models;

public class FixResult
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("originalContent")]
    public string OriginalContent { get; set; } = string.Empty;

    [JsonPropertyName("fixedContent")]
    public string FixedContent { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }

    [JsonPropertyName("changeDescription")]
    public string ChangeDescription { get; set; } = string.Empty;

    [JsonPropertyName("reviewApproved")]
    public bool ReviewApproved { get; set; }

    [JsonPropertyName("reviewNotes")]
    public string ReviewNotes { get; set; } = string.Empty;
}

public class AutoFixResult
{
    [JsonPropertyName("prUrl")]
    public string? PrUrl { get; set; }

    [JsonPropertyName("branchName")]
    public string BranchName { get; set; } = string.Empty;

    [JsonPropertyName("fixesApplied")]
    public List<FixSummary> FixesApplied { get; set; } = [];

    [JsonPropertyName("fixesSkipped")]
    public List<FixSummary> FixesSkipped { get; set; } = [];

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class FixSummary
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class ClaudeFixResponse
{
    [JsonPropertyName("fixes")]
    public List<ClaudeFixEntry> Fixes { get; set; } = [];
}

public class ClaudeFixEntry
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("fixedContent")]
    public string FixedContent { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }

    [JsonPropertyName("changeDescription")]
    public string ChangeDescription { get; set; } = string.Empty;
}

public class ClaudeReviewResponse
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
