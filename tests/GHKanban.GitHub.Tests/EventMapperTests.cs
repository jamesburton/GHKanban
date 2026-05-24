using GHKanban.Core.Events;
using GHKanban.GitHub;
using Xunit;

namespace GHKanban.GitHub.Tests;

public class EventMapperTests
{
    [Fact]
    public void MapsIssueOpened()
    {
        var json = """
            {
              "action": "opened",
              "issue": {
                "number": 7, "title": "X", "state": "open",
                "labels": [], "assignees": [],
                "created_at": "2026-05-01T00:00:00Z",
                "updated_at": "2026-05-01T00:00:00Z",
                "html_url": "https://github.com/owner/repo/issues/7"
              },
              "repository": { "full_name": "owner/repo" }
            }
            """;
        var ev = EventMapper.MapIssueEvent("issues", json);
        Assert.NotNull(ev);
        Assert.Equal(EventType.IssueOpened, ev!.Type);
        Assert.Equal(7, ev.Issue.Number);
        Assert.Equal("owner/repo", ev.Issue.Repo);
    }

    [Fact]
    public void MapsIssueLabeled()
    {
        var json = """
            {
              "action": "labeled",
              "issue": {
                "number": 7, "title": "X", "state": "open",
                "labels": [{"name":"bug"}], "assignees": [],
                "created_at": "2026-05-01T00:00:00Z",
                "updated_at": "2026-05-23T00:00:00Z",
                "html_url": "https://github.com/owner/repo/issues/7"
              },
              "label": {"name": "bug"},
              "repository": {"full_name":"owner/repo"}
            }
            """;
        var ev = EventMapper.MapIssueEvent("issues", json);
        Assert.NotNull(ev);
        Assert.Equal(EventType.IssueLabeled, ev!.Type);
        Assert.Equal("bug", ev.ChangedLabel);
    }
}
