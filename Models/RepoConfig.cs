namespace BugTriageApi.Models;

public class RepoConfig
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public string DisplayName { get; set; } = string.Empty;
}
