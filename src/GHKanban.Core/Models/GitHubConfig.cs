namespace GHKanban.Core.Models;

public sealed record GitHubAuth(string PatEnv);
public sealed record GitHubWebhook(string? PublicUrl, string? SecretEnv);

public sealed record GitHubConfig(
    GitHubAuth Auth,
    GitHubWebhook Webhook,
    TimeSpan PollInterval,
    TimeSpan ReconcileInterval);
