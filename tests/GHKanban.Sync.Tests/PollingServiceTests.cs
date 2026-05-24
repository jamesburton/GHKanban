using GHKanban.Config;
using GHKanban.Core.Models;
using GHKanban.GitHub;
using GHKanban.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Sync.Tests;

public class PollingServiceTests
{
    [Fact]
    public async Task PopulatesStoreFromReader()
    {
        var reader = Substitute.For<IGitHubReader>();
        reader.ListIssuesAsync("owner/repo", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new IssuePage(new[] { MakeIssue(1), MakeIssue(2) }, "cursor"));

        var store = new IssueModelStore();
        var cfg = new ConfigStore(new ConfigSnapshot(
            new GitHubConfig(new GitHubAuth("X"), new GitHubWebhook(null, null), TimeSpan.FromMilliseconds(50), TimeSpan.FromHours(1)),
            new[] { new BoardConfig("b", "B", new BoardScope(new[] { "owner/repo" }, [], new Dictionary<string, string>()), []) },
            [], []));
        var processor = new WebhookEventProcessor(store, NullLogger<WebhookEventProcessor>.Instance);
        var svc = new PollingService(reader, store, cfg, processor, NullLogger<PollingService>.Instance);

        await svc.PollOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, store.All().Count());
    }

    private static IssueView MakeIssue(int n) =>
        new("owner/repo", n, "t", IssueState.Open, [], [], null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, $"https://github.com/owner/repo/issues/{n}");
}
