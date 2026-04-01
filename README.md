# BugTriageApi

Automated bug triage system powered by Claude AI. Receives bug reports from Microsoft Teams via Power Automate, analyzes them against a GitHub repository's codebase, and returns structured triage results.

## Architecture

```
Teams Adaptive Card
        |
        v
  Power Automate
        |
        v
  Planner Card (created)
        |
        v
  POST /api/triage  ──> BugTriageApi
        |                    |
        |          ┌─────────┴─────────┐
        |          v                   v
        |    GitHubService       ClaudeService
        |    (repo file tree)    (AI analysis)
        |          └─────────┬─────────┘
        |                    v
        |             TriageService
        |             (orchestrator)
        |                    |
        v                    v
  Structured JSON result returned to Power Automate
```

## Local Setup

### Prerequisites
- .NET 8 SDK
- GitHub Personal Access Token (with repo read access)
- Claude API Key (from console.anthropic.com)

### Environment Variables (PowerShell)

```powershell
$env:CLAUDE_API_KEY = "sk-ant-..."
$env:GITHUB_TOKEN = "github_pat_..."
$env:GITHUB_OWNER = "tarekabiramia"
$env:GITHUB_REPO = "deka-construction"
```

### Run

```bash
cd BugTriageApi
dotnet run
```

The API starts on `http://localhost:8080`.

### Test Endpoints

**Health check:**
```bash
curl http://localhost:8080/api/health
```

**Triage a bug:**
```bash
curl -X POST http://localhost:8080/api/triage \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Call Us button dials wrong number",
    "area": "Frontend",
    "pageOrFeature": "Homepage header/navbar",
    "stepsToReproduce": "1. Go to site. 2. Click Call Us. 3. Wrong number dials.",
    "expected": "Should dial (714) 369-8714",
    "actual": "Dials +1234567890 placeholder",
    "severity": "Medium",
    "reporterName": "Sarah"
  }'
```

## Railway Deployment

1. Push this repo to GitHub
2. Go to [railway.app](https://railway.app), create new project
3. Connect your GitHub repository
4. Railway auto-detects the Dockerfile
5. Set environment variables in Railway dashboard:
   - `CLAUDE_API_KEY`
   - `GITHUB_TOKEN`
   - `GITHUB_OWNER`
   - `GITHUB_REPO`
6. Deploy and generate a public domain under Settings > Networking

## Power Automate Flow Setup

1. **Trigger**: "When a new response is submitted" (Teams Adaptive Card)
2. **Action 1**: Create a Planner task with the bug title and details
3. **Action 2**: HTTP POST to `https://<your-railway-domain>/api/triage`
   - Method: POST
   - Headers: `Content-Type: application/json`
   - Body: Map Adaptive Card fields to the request JSON
4. **Action 3**: Parse the JSON response
5. **Action 4**: Update the Planner task with triage results (add as comment or update description)
6. **Optional**: Send a Teams message back to the reporter with the triage summary

## API Reference

### GET /api/health

Returns service health status.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2026-04-01T12:00:00.0000000Z"
}
```

### POST /api/triage

Analyzes a bug report and returns structured triage results.

**Request:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| title | string | Yes | Bug title |
| area | string | Yes | Frontend, Backend, or Both |
| pageOrFeature | string | Yes | Where the bug occurs |
| stepsToReproduce | string | Yes | How to reproduce |
| expected | string | Yes | Expected behavior |
| actual | string | Yes | Actual behavior |
| severity | string | Yes | Blocker, High, Medium, Low |
| reporterName | string | Yes | Who reported it |
| screenshot | string | No | Screenshot URL |
| plannerTaskId | string | No | Planner task ID |

**Response:**
| Field | Type | Description |
|-------|------|-------------|
| complexityScore | int | 1-5 complexity rating |
| complexityReason | string | Why this complexity level |
| rootCauseHypothesis | string | Likely root cause |
| affectedFiles | array | Files involved with reasons |
| suggestedFix | string? | Fix suggestion (complexity 1-2 only) |
| action | string | auto-fix, review-needed, or manual |
| summary | string | One-line summary |

## Phase 2 Roadmap

- **Auto-fix PRs**: For complexity 1-2 bugs, automatically create a GitHub PR with the fix
- **Screenshot analysis**: Use Claude's vision to analyze uploaded screenshots
- **Feedback loop**: Allow staff to rate triage accuracy, improving prompts over time
- **Slack integration**: Post triage results to a dedicated Slack channel
- **Metrics dashboard**: Track bug trends, resolution times, and AI accuracy
