using System.Text.Json;
using GHKanban.AgentImage.Config;
using GHKanban.AgentImage.Providers;
using GHKanban.AgentImage.Tools;
using Microsoft.Extensions.AI;

const string SkillPath = "/skill/agent.yaml";
const string SkillFilesDir = "/skill/files/";
const string EventPath = "/event.json";
const string GithubPatPath = "/secrets/github-pat";
const string LlmKeyPath = "/secrets/llm-api-key";

try
{
    // 1. Read skill + event.
    var skill = SkillConfigLoader.Load(File.ReadAllText(SkillPath));
    var eventJson = File.ReadAllText(EventPath);
    using var eventDoc = JsonDocument.Parse(eventJson);
    var root = eventDoc.RootElement;

    var repo = Environment.GetEnvironmentVariable("GHKANBAN_GH_REPO")
        ?? root.GetProperty("Issue").GetProperty("Repo").GetString()!;
    var issueNumber = int.Parse(Environment.GetEnvironmentVariable("GHKANBAN_GH_ISSUE")
        ?? root.GetProperty("Issue").GetProperty("Number").GetInt32().ToString());
    var runId = Environment.GetEnvironmentVariable("GHKANBAN_RUN_ID") ?? Guid.NewGuid().ToString("N");

    // 2. Build interpolation tokens.
    var tokens = BuildTokens(root, runId);

    // 3. Render user prompt.
    var userPrompt = PromptTemplate.Render(skill.Prompt.User, tokens);

    // 4. Load PAT for GitHub tool.
    var pat = File.ReadAllText(GithubPatPath).Trim();
    var tools = new ToolRegistry(new IAgentTool[]
    {
        new GitHubPostCommentTool(pat, repo, issueNumber),
    });
    var allowed = tools.GetAllowed(skill.Tools).ToList();
    var postComment = allowed.FirstOrDefault(t => t.Name == "github.post-comment")
        ?? throw new InvalidOperationException("github.post-comment tool is required in v1");

    // 5. Run the agent (or short-circuit on provider: none).
    string commentBody;
    if (skill.Llm.Provider == "none")
    {
        commentBody = userPrompt;
    }
    else
    {
        var apiKey = File.Exists(LlmKeyPath) ? File.ReadAllText(LlmKeyPath).Trim() : "";
        using var chatClient = ChatClientFactory.Create(skill.Llm, apiKey)!;

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(skill.Prompt.SystemFile))
        {
            var systemPath = Path.Combine(SkillFilesDir, skill.Prompt.SystemFile.TrimStart('.', '/'));
            if (File.Exists(systemPath))
                messages.Add(new ChatMessage(ChatRole.System, File.ReadAllText(systemPath)));
        }
        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        // Pass model via ChatOptions — Anthropic.SDK doesn't bind it at construction.
        var options = new ChatOptions { ModelId = skill.Llm.Model };
        var response = await chatClient.GetResponseAsync(messages, options);
        commentBody = response.Messages.LastOrDefault()?.Text ?? "(agent produced no text)";
    }

    // 6. Post comment.
    var commentUrl = await postComment.InvokeAsync(commentBody);

    // 7. Emit terminal structured log line.
    var completeLine = JsonSerializer.Serialize(new
    {
        @event = "complete",
        run_id = runId,
        comment_url = commentUrl,
    });
    Console.WriteLine(completeLine);
    return 0;
}
catch (Exception ex)
{
    var errLine = JsonSerializer.Serialize(new
    {
        @event = "error",
        message = ex.Message,
        type = ex.GetType().Name,
    });
    Console.Error.WriteLine(errLine);
    return 1;
}

static Dictionary<string, string> BuildTokens(JsonElement eventRoot, string runId)
{
    var issue = eventRoot.GetProperty("Issue");
    var tokens = new Dictionary<string, string>
    {
        ["issue.title"] = issue.GetProperty("Title").GetString() ?? "",
        ["issue.body"] = issue.TryGetProperty("Body", out var b) ? b.GetString() ?? "" : "",
        ["issue.labels"] = string.Join(", ", issue.GetProperty("Labels").EnumerateArray().Select(l => l.GetString() ?? "")),
        ["issue.assignees"] = string.Join(", ", issue.GetProperty("Assignees").EnumerateArray().Select(a => a.GetString() ?? "")),
        ["issue.repo"] = issue.GetProperty("Repo").GetString() ?? "",
        ["issue.number"] = issue.GetProperty("Number").GetInt32().ToString(),
        ["issue.html_url"] = issue.GetProperty("HtmlUrl").GetString() ?? "",
        ["trigger.event"] = eventRoot.GetProperty("TriggerEvent").GetString() ?? "",
        ["trigger.rule"] = eventRoot.GetProperty("MatchingRule").GetString() ?? "",
        ["trigger.agent_name"] = eventRoot.GetProperty("AgentName").GetString() ?? "",
        ["run.id"] = runId,
    };
    return tokens;
}
