using GHKanban.Agents;
using GHKanban.Config;
using GHKanban.ContainerRuntime;
using GHKanban.GitHub;
using GHKanban.Sync;
using GHKanban.Web;
using GHKanban.Web.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console());

// Config: first-run wizard creates ~/.ghkanban/ if missing
var configRoot = Environment.GetEnvironmentVariable("GHKANBAN_CONFIG_ROOT")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ghkanban");
FirstRunWizard.EnsureInitialised(configRoot);

var initialSnapshot = ConfigWatcher.LoadOnce(configRoot);
var configStore = new ConfigStore(initialSnapshot);
builder.Services.AddSingleton(configStore);
builder.Services.AddSingleton(_ => new ConfigWatcher(configRoot, configStore));

// SQLite state
var dbPath = Path.Combine(configRoot, "state.db");
var connString = $"Data Source={dbPath}";
var conn = new SqliteConnection(connString);
conn.Open();
SqliteSchema.Apply(conn);
builder.Services.AddSingleton(conn);
builder.Services.AddSingleton<SyncCursorStore>();
builder.Services.AddSingleton<AgentRunStore>();

// Container runtime
builder.Services.AddSingleton<IContainerRuntime, DockerContainerRuntime>();
builder.Services.AddSingleton(new ContainerAgentDirs(
    ConfigRoot: configRoot,
    RunsRoot: Path.Combine(Path.GetTempPath(), "ghkanban", "runs")));
builder.Services.AddHostedService<ContainerJanitor>();

// Ensure runs dir exists.
Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ghkanban", "runs"));

// Materialise secrets to disk (for mounting into containers).
var secretsDir = Path.Combine(configRoot, "secrets");
SecretFilePlumbing.WriteAll(secretsDir, initialSnapshot);

// GitHub
var pat = Environment.GetEnvironmentVariable(initialSnapshot.GitHub.Auth.PatEnv) ?? "";
builder.Services.AddSingleton<IGitHubReader>(_ => new GitHubReader(pat));
builder.Services.AddSingleton<IGitHubWriter>(_ => new GitHubWriter(pat));

// Sync engine
builder.Services.AddSingleton<IssueModelStore>();
builder.Services.AddHostedService<PollingService>();
builder.Services.AddSingleton<WebhookEventProcessor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookEventProcessor>());
builder.Services.AddHostedService<ReconcilerService>();

// Agents
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<AgentDispatcher>(sp =>
{
    var registry = sp.GetRequiredService<AgentRegistry>();
    var snap = sp.GetRequiredService<ConfigStore>().Current;
    var agents = snap.Agents.ToDictionary(
        a => a.Id,
        a => registry.Resolve(a),
        StringComparer.OrdinalIgnoreCase);
    var reader = sp.GetRequiredService<IGitHubReader>();
    string currentUser;
    try { currentUser = reader.GetCurrentUserLoginAsync().GetAwaiter().GetResult(); }
    catch { currentUser = ""; }  // bootstraps with empty user when PAT is invalid (e.g. first-run)
    return new AgentDispatcher(agents, sp.GetRequiredService<AgentRunStore>(), currentUser, sp.GetRequiredService<ILogger<AgentDispatcher>>());
});

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

// Start config watcher
_ = app.Services.GetRequiredService<ConfigWatcher>();

// Wire the channel consumer to the agent dispatcher. Both the webhook endpoint and the
// polling delta detector write to the same channel; routing dispatch through OnEvent makes
// both paths trigger agents uniformly.
var processor = app.Services.GetRequiredService<WebhookEventProcessor>();
var dispatcher = app.Services.GetRequiredService<AgentDispatcher>();
var configStoreInstance = app.Services.GetRequiredService<ConfigStore>();
processor.OnEvent = (ev, ct) => dispatcher.DispatchAsync(ev, configStoreInstance.Current.Agents, ct);

app.MapWebhook();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Urls.Add($"http://localhost:{Environment.GetEnvironmentVariable("GHKANBAN_PORT") ?? "5454"}");
app.Run();
