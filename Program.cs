using BugTriageApi.Models;
using BugTriageApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Register HttpClient-backed services
builder.Services.AddHttpClient<GitHubService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    client.DefaultRequestHeaders.Add("User-Agent", "BugTriageApi");

    var token = config["GitHub:Token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (!string.IsNullOrEmpty(token))
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
});

builder.Services.AddHttpClient<ClaudeService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("x-api-key",
        config["Claude:ApiKey"] ?? Environment.GetEnvironmentVariable("CLAUDE_API_KEY") ?? "");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

builder.Services.AddScoped<AutoFixService>();
builder.Services.AddScoped<TriageService>();

var app = builder.Build();

// Health check
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow.ToString("o")
}));

// Bug triage endpoint
app.MapPost("/api/triage", async (BugReportRequest bug, TriageService triageService, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(bug.Title))
        return Results.BadRequest(new { error = "Title is required" });

    try
    {
        var result = await triageService.TriageAsync(bug);
        return Results.Ok(result);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "External API call failed during triage");
        return Results.Json(new { error = "Failed to contact external service", detail = ex.Message }, statusCode: 502);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Triage failed for bug: {Title}", bug.Title);
        return Results.Json(new { error = "Triage failed", detail = ex.Message }, statusCode: 500);
    }
});

// Bind to Railway's PORT env var
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();
