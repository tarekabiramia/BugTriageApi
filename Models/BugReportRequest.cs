namespace BugTriageApi.Models;

public class BugReportRequest
{
    public string Title { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string PageOrFeature { get; set; } = string.Empty;
    public string StepsToReproduce { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string? Screenshot { get; set; }
    public string? PlannerTaskId { get; set; }
}
