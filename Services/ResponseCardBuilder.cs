using BugTriageApi.Models;

namespace BugTriageApi.Services;

public static class ResponseCardBuilder
{
    public static object Build(BugReportRequest bug, TriageResult triage, string systemName)
    {
        var (statusText, statusDescription, bannerStyle) = triage.Action switch
        {
            "auto-fix" => (
                "Fix Submitted for Review",
                "Our AI agents have analyzed the issue, generated a fix, and submitted it for review. The R&D team will verify and deploy shortly.",
                "good"
            ),
            "review-needed" => (
                "Under Review",
                "Our AI agents have analyzed the issue and prepared a detailed report. A developer will pick this up shortly.",
                "warning"
            ),
            _ => (
                "Assigned to R&D",
                "This issue requires hands-on investigation. It has been assigned to the R&D team.",
                "emphasis"
            )
        };

        var body = new List<object>
        {
            // Header row
            new
            {
                type = "ColumnSet",
                columns = new object[]
                {
                    new
                    {
                        type = "Column",
                        width = "stretch",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "Bug Report — Status Update",
                                weight = "Bolder",
                                size = "Medium",
                                color = "Accent"
                            }
                        }
                    },
                    new
                    {
                        type = "Column",
                        width = "auto",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = bug.Severity,
                                weight = "Bolder",
                                color = bug.Severity is "Blocker" or "High" ? "Attention" : bug.Severity == "Medium" ? "Warning" : "Good"
                            }
                        }
                    }
                }
            },

            // Bug title
            new
            {
                type = "TextBlock",
                text = bug.Title,
                weight = "Bolder",
                size = "Large",
                wrap = true,
                spacing = "Small"
            },

            // System + reporter
            new
            {
                type = "FactSet",
                facts = new object[]
                {
                    new { title = "System", value = systemName },
                    new { title = "Reported by", value = bug.ReporterName }
                },
                spacing = "Small"
            },

            // Status banner
            new
            {
                type = "Container",
                style = bannerStyle,
                items = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = statusText,
                        weight = "Bolder",
                        wrap = true
                    },
                    new
                    {
                        type = "TextBlock",
                        text = statusDescription,
                        wrap = true,
                        size = "Small"
                    }
                },
                bleed = true,
                spacing = "Medium"
            },

            // What happens next
            new
            {
                type = "TextBlock",
                text = "A task has been created in Planner and your team has been notified. We'll keep you posted on progress.",
                wrap = true,
                size = "Small",
                isSubtle = true,
                spacing = "Medium"
            },

            // Footer
            new
            {
                type = "ColumnSet",
                spacing = "Medium",
                columns = new object[]
                {
                    new
                    {
                        type = "Column",
                        width = "stretch",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "Powered by AI Triage",
                                size = "Small",
                                isSubtle = true
                            }
                        }
                    },
                    new
                    {
                        type = "Column",
                        width = "auto",
                        items = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = DateTime.UtcNow.ToString("MMM d, yyyy h:mm tt") + " UTC",
                                size = "Small",
                                isSubtle = true
                            }
                        }
                    }
                }
            }
        };

        // PR link button only when auto-fix created one
        var actions = new List<object>();
        if (!string.IsNullOrEmpty(triage.PrUrl))
        {
            actions.Add(new
            {
                type = "Action.OpenUrl",
                title = "View Fix on GitHub",
                url = triage.PrUrl,
                style = "positive"
            });
        }

        return new Dictionary<string, object>
        {
            ["type"] = "AdaptiveCard",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["version"] = "1.4",
            ["body"] = body,
            ["actions"] = actions
        };
    }
}
