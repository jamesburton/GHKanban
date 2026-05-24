# GHKanban v1 (Slice A + Stubbed Agent) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first runnable slice of GHKanban — a `dnx`-installable single-process .NET 10 app that shows a customisable Kanban view of GitHub Issues and runs a stubbed Microsoft Agent Framework (MAF) agent in response to rule-matched issue events.

**Architecture:** Single .NET 10 process, vanilla `Host.CreateApplicationBuilder` (no Aspire in runtime path). Seven src projects (`Core`, `Rules`, `Config`, `GitHub`, `Sync`, `Agents`, `Web`) + matching test projects. Blazor Server UI, SQLite for ephemeral state, YAML files for config (hot-reloaded), Octokit for REST writes, custom GraphQL client for reads, Microsoft Agent Framework for the agent abstraction.

**Tech Stack:** .NET 10 (C# 14) · Blazor Server · xUnit v3 + Microsoft Testing Platform (MTP) · Octokit.NET · Microsoft.Agents.AI · YamlDotNet · Microsoft.Data.Sqlite · Serilog (for structured logging).

**Source spec:** `docs/superpowers/specs/2026-05-23-ghkanban-slice-a-design.md` — every task in this plan implements one slice of that spec. Read the spec section referenced in each task before implementing.

---

## File structure (locked in here, referenced by every task)

```
ghkanban/
├── GHKanban.sln
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── README.md                                   (the survey — already exists; do NOT modify)
├── docs/                                       (existing planning artifacts)
├── src/
│   ├── GHKanban.Core/
│   │   ├── GHKanban.Core.csproj
│   │   ├── Models/
│   │   │   ├── IssueView.cs
│   │   │   ├── IssueContext.cs
│   │   │   ├── AgentRunResult.cs
│   │   │   ├── BoardConfig.cs
│   │   │   ├── ColumnConfig.cs
│   │   │   ├── AgentConfig.cs
│   │   │   ├── TriggerSpec.cs
│   │   │   └── GitHubConfig.cs
│   │   └── Events/
│   │       ├── IssueEvent.cs
│   │       └── EventType.cs
│   ├── GHKanban.Rules/
│   │   ├── GHKanban.Rules.csproj
│   │   ├── RuleAst.cs
│   │   ├── RuleParser.cs
│   │   ├── RuleEvaluator.cs
│   │   └── RuleException.cs
│   ├── GHKanban.Config/
│   │   ├── GHKanban.Config.csproj
│   │   ├── YamlConfigLoader.cs
│   │   ├── ConfigStore.cs
│   │   └── ConfigWatcher.cs
│   ├── GHKanban.GitHub/
│   │   ├── GHKanban.GitHub.csproj
│   │   ├── IGitHubReader.cs
│   │   ├── IGitHubWriter.cs
│   │   ├── GitHubReader.cs
│   │   ├── GitHubWriter.cs
│   │   ├── WebhookSignatureValidator.cs
│   │   └── EventMapper.cs
│   ├── GHKanban.Sync/
│   │   ├── GHKanban.Sync.csproj
│   │   ├── IssueModelStore.cs
│   │   ├── SyncCursorStore.cs
│   │   ├── SqliteSchema.cs
│   │   ├── PollingService.cs
│   │   ├── WebhookEventProcessor.cs
│   │   └── ReconcilerService.cs
│   ├── GHKanban.Agents/
│   │   ├── GHKanban.Agents.csproj
│   │   ├── IGHKanbanAgent.cs
│   │   ├── AgentRegistry.cs
│   │   ├── AgentDispatcher.cs
│   │   ├── TriggerEvaluator.cs
│   │   ├── AgentRunStore.cs
│   │   └── StubAcknowledgeAgent.cs
│   └── GHKanban.Web/
│       ├── GHKanban.Web.csproj           (dotnet tool entry — ToolCommandName=ghkanban)
│       ├── Program.cs
│       ├── FirstRunWizard.cs
│       ├── WebhookEndpoint.cs            (minimal-API endpoint registered in Program)
│       ├── Components/
│       │   ├── App.razor
│       │   ├── Routes.razor
│       │   ├── _Imports.razor
│       │   ├── Layout/
│       │   │   ├── MainLayout.razor
│       │   │   └── NavMenu.razor
│       │   └── Pages/
│       │       ├── BoardPicker.razor
│       │       ├── BoardView.razor
│       │       ├── ActivityFeed.razor
│       │       └── ConfigView.razor
│       └── wwwroot/
│           └── app.css
└── tests/
    ├── GHKanban.Core.Tests/GHKanban.Core.Tests.csproj
    ├── GHKanban.Rules.Tests/GHKanban.Rules.Tests.csproj
    ├── GHKanban.Config.Tests/GHKanban.Config.Tests.csproj
    ├── GHKanban.GitHub.Tests/GHKanban.GitHub.Tests.csproj
    ├── GHKanban.Sync.Tests/GHKanban.Sync.Tests.csproj
    ├── GHKanban.Agents.Tests/GHKanban.Agents.Tests.csproj
    └── GHKanban.Web.Tests/GHKanban.Web.Tests.csproj
```

Per-project responsibility:
- **Core** — POCOs only; no behaviour, no dependencies on other projects
- **Rules** — grammar parser + evaluator; depends on Core
- **Config** — YAML → strongly-typed configs; depends on Core + Rules
- **GitHub** — adapter layer (reads, writes, webhook validation); depends on Core
- **Sync** — orchestrates GitHub data into IssueModelStore; depends on Core + GitHub + Rules
- **Agents** — MAF integration, trigger evaluation, dispatch; depends on Core + Rules + GitHub
- **Web** — Blazor UI + dotnet-tool entry point; depends on all of the above

---

## Conventions used in this plan

**Commits:** one commit per task. Message format: `feat(scope): brief description` for new features, `chore(scope): …` for scaffolding. Body should mention the spec section being implemented.

**Tests:** xUnit v3 MTP. Test class per concern. Use `Assert.That(x, Is.EqualTo(y))` style is NOT xUnit — xUnit uses `Assert.Equal(expected, actual)`. Use `[Fact]` for single-case, `[Theory]` + `[InlineData]` for parameterised.

**Async:** all I/O is async. Constructors don't do I/O. Use `CancellationToken` parameters at boundaries; pass through.

**Logging:** Serilog via `ILogger<T>` (Microsoft.Extensions.Logging abstraction). Structured: `_log.LogInformation("Synced {Count} issues for {Repo}", count, repo)`.

**File paths:** Windows-friendly absolute paths in shell commands. Use `Path.Combine` in code.

**Branch:** all commits on `main`. No feature branches.

---

## Task 1: Solution + project scaffolding

**Files:**
- Create: `c:/Development/GHKanban/GHKanban.sln`
- Create: `c:/Development/GHKanban/global.json`
- Create: `c:/Development/GHKanban/Directory.Build.props`
- Create: `c:/Development/GHKanban/Directory.Packages.props`
- Create: 7 `src/GHKanban.<Name>/GHKanban.<Name>.csproj`
- Create: 7 `tests/GHKanban.<Name>.Tests/GHKanban.<Name>.Tests.csproj`

- [ ] **Step 1: Create `global.json` pinning the .NET 10 SDK**

Write `c:/Development/GHKanban/global.json`:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": true
  }
}
```

- [ ] **Step 2: Create `Directory.Build.props` for shared settings**

Write `c:/Development/GHKanban/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>NU1900;NU1901;NU1902;NU1903</WarningsNotAsErrors>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create `Directory.Packages.props` for central package versioning**

Write `c:/Development/GHKanban/Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- App dependencies -->
    <PackageVersion Include="Octokit" Version="14.0.0" />
    <PackageVersion Include="Microsoft.Agents.AI" Version="1.0.0" />
    <PackageVersion Include="YamlDotNet" Version="16.3.0" />
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="9.0.0" />
    <PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Components" Version="9.0.0" />
    <!-- Test dependencies -->
    <PackageVersion Include="xunit.v3" Version="2.0.0" />
    <PackageVersion Include="Microsoft.Testing.Platform" Version="1.5.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
</Project>
```

Note: If any package version above doesn't resolve on `dotnet restore`, use the closest available version. Subagents should verify with `dotnet list package --outdated` or `nuget search`.

- [ ] **Step 4: Create empty solution and seven src project files**

Run:
```pwsh
cd c:/Development/GHKanban
dotnet new sln -n GHKanban
```

For each of the 7 src projects, create the .csproj manually (do not use `dotnet new classlib` since templates can drift from our property conventions). Example for `src/GHKanban.Core/GHKanban.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
  </PropertyGroup>
</Project>
```

Create the same shape for `Rules`, `Config`, `GitHub`, `Sync`, `Agents`. For `Web`, use the Web SDK and add the dotnet-tool config:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>ghkanban</ToolCommandName>
    <PackageId>GHKanban</PackageId>
    <Version>0.1.0-alpha</Version>
    <Description>Customisable Kanban view over GitHub Issues with AI agent triggers.</Description>
  </PropertyGroup>
</Project>
```

Then add each project to the solution:

```pwsh
dotnet sln add src/GHKanban.Core/GHKanban.Core.csproj
dotnet sln add src/GHKanban.Rules/GHKanban.Rules.csproj
dotnet sln add src/GHKanban.Config/GHKanban.Config.csproj
dotnet sln add src/GHKanban.GitHub/GHKanban.GitHub.csproj
dotnet sln add src/GHKanban.Sync/GHKanban.Sync.csproj
dotnet sln add src/GHKanban.Agents/GHKanban.Agents.csproj
dotnet sln add src/GHKanban.Web/GHKanban.Web.csproj
```

- [ ] **Step 5: Add project-to-project references per dependency graph**

```pwsh
dotnet add src/GHKanban.Rules/ reference src/GHKanban.Core/
dotnet add src/GHKanban.Config/ reference src/GHKanban.Core/ src/GHKanban.Rules/
dotnet add src/GHKanban.GitHub/ reference src/GHKanban.Core/
dotnet add src/GHKanban.Sync/ reference src/GHKanban.Core/ src/GHKanban.GitHub/ src/GHKanban.Rules/
dotnet add src/GHKanban.Agents/ reference src/GHKanban.Core/ src/GHKanban.Rules/ src/GHKanban.GitHub/
dotnet add src/GHKanban.Web/ reference src/GHKanban.Core/ src/GHKanban.Rules/ src/GHKanban.Config/ src/GHKanban.GitHub/ src/GHKanban.Sync/ src/GHKanban.Agents/
```

- [ ] **Step 6: Create seven test project files**

Each test project's .csproj (e.g. `tests/GHKanban.Core.Tests/GHKanban.Core.Tests.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.Testing.Platform" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\GHKanban.Core\GHKanban.Core.csproj" />
  </ItemGroup>
</Project>
```

Adjust the `ProjectReference` per test project to point at the src project being tested. Add each to the solution:

```pwsh
dotnet sln add tests/GHKanban.Core.Tests/GHKanban.Core.Tests.csproj
# (repeat for each)
```

- [ ] **Step 7: Verify the solution builds**

```pwsh
dotnet restore
dotnet build --no-restore
```

Expected: zero errors. Zero warnings (because `TreatWarningsAsErrors=true`).

- [ ] **Step 8: Commit**

```pwsh
git add GHKanban.sln global.json Directory.Build.props Directory.Packages.props src/ tests/
git commit -m "chore(scaffold): create solution with 7 src + 7 test projects (spec §3)"
```

---

## Task 2: Core models

**Files:**
- Create: `src/GHKanban.Core/Models/IssueView.cs`
- Create: `src/GHKanban.Core/Models/IssueContext.cs`
- Create: `src/GHKanban.Core/Models/AgentRunResult.cs`
- Create: `src/GHKanban.Core/Models/BoardConfig.cs`
- Create: `src/GHKanban.Core/Models/ColumnConfig.cs`
- Create: `src/GHKanban.Core/Models/AgentConfig.cs`
- Create: `src/GHKanban.Core/Models/TriggerSpec.cs`
- Create: `src/GHKanban.Core/Models/GitHubConfig.cs`
- Create: `src/GHKanban.Core/Events/EventType.cs`
- Create: `src/GHKanban.Core/Events/IssueEvent.cs`
- Test: `tests/GHKanban.Core.Tests/IssueViewTests.cs`

Spec references: §4 (data model), §6 (event types).

- [ ] **Step 1: Write the failing test for IssueView equality + helpers**

Write `tests/GHKanban.Core.Tests/IssueViewTests.cs`:

```csharp
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Core.Tests;

public class IssueViewTests
{
    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var a = new IssueView(
            Repo: "owner/repo",
            Number: 42,
            Title: "Bug",
            State: IssueState.Open,
            Labels: ["bug"],
            Assignees: ["alice"],
            Milestone: null,
            CreatedAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero),
            HtmlUrl: "https://github.com/owner/repo/issues/42");

        var b = a with { };

        Assert.Equal(a, b);
    }

    [Fact]
    public void AgeDays_computes_from_CreatedAt_to_now()
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        var issue = new IssueView(
            Repo: "x/y", Number: 1, Title: "t", State: IssueState.Open,
            Labels: [], Assignees: [], Milestone: null,
            CreatedAt: now.AddDays(-7), UpdatedAt: now, HtmlUrl: "");

        Assert.Equal(7, issue.AgeDays(now));
    }

    [Fact]
    public void HasLabel_is_case_insensitive()
    {
        var issue = new IssueView(
            Repo: "x/y", Number: 1, Title: "t", State: IssueState.Open,
            Labels: ["Bug"], Assignees: [], Milestone: null,
            CreatedAt: DateTimeOffset.UtcNow, UpdatedAt: DateTimeOffset.UtcNow, HtmlUrl: "");

        Assert.True(issue.HasLabel("bug"));
        Assert.True(issue.HasLabel("BUG"));
        Assert.False(issue.HasLabel("feature"));
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

```pwsh
dotnet test tests/GHKanban.Core.Tests/ --no-restore
```

Expected: compilation errors (types not yet defined).

- [ ] **Step 3: Write the Core models**

Write `src/GHKanban.Core/Models/IssueView.cs`:

```csharp
namespace GHKanban.Core.Models;

public enum IssueState { Open, Closed }

public sealed record IssueView(
    string Repo,
    int Number,
    string Title,
    IssueState State,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Assignees,
    string? Milestone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string HtmlUrl)
{
    public int AgeDays(DateTimeOffset now) => (int)(now - CreatedAt).TotalDays;

    public bool HasLabel(string label) =>
        Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));

    public bool HasAssignee(string user) =>
        Assignees.Any(a => string.Equals(a, user, StringComparison.OrdinalIgnoreCase));
}
```

Write `src/GHKanban.Core/Models/IssueContext.cs`:

```csharp
namespace GHKanban.Core.Models;

/// <summary>
/// What an agent receives when triggered. Carries the issue plus the triggering event details.
/// </summary>
public sealed record IssueContext(
    IssueView Issue,
    string TriggerEvent,
    string MatchingRule,
    string AgentName);
```

Write `src/GHKanban.Core/Models/AgentRunResult.cs`:

```csharp
namespace GHKanban.Core.Models;

public enum AgentRunStatus { Success, Failed }

public sealed record AgentRunResult(
    AgentRunStatus Status,
    string? Output,
    string? Error);
```

Write `src/GHKanban.Core/Models/ColumnConfig.cs`:

```csharp
namespace GHKanban.Core.Models;

public sealed record ColumnConfig(string Name, string Rule);
```

Write `src/GHKanban.Core/Models/BoardConfig.cs`:

```csharp
namespace GHKanban.Core.Models;

public sealed record BoardScope(
    IReadOnlyList<string> Repos,
    IReadOnlyList<string> Orgs,
    IReadOnlyDictionary<string, string> Filters);

public sealed record BoardConfig(
    string Id,
    string Name,
    BoardScope Scope,
    IReadOnlyList<ColumnConfig> Columns);
```

Write `src/GHKanban.Core/Models/TriggerSpec.cs`:

```csharp
namespace GHKanban.Core.Models;

public sealed record TriggerSpec(string On, string When);
```

Write `src/GHKanban.Core/Models/AgentConfig.cs`:

```csharp
namespace GHKanban.Core.Models;

public sealed record AgentConfig(
    string Id,
    string Name,
    string Implementation,
    IReadOnlyList<TriggerSpec> Triggers);
```

Write `src/GHKanban.Core/Models/GitHubConfig.cs`:

```csharp
namespace GHKanban.Core.Models;

public sealed record GitHubAuth(string PatEnv);
public sealed record GitHubWebhook(string? PublicUrl, string? SecretEnv);

public sealed record GitHubConfig(
    GitHubAuth Auth,
    GitHubWebhook Webhook,
    TimeSpan PollInterval,
    TimeSpan ReconcileInterval);
```

Write `src/GHKanban.Core/Events/EventType.cs`:

```csharp
namespace GHKanban.Core.Events;

public enum EventType
{
    IssueOpened,
    IssueLabeled,
    IssueUnlabeled,
    IssueAssigned,
    IssueUnassigned,
    IssueClosed,
    IssueReopened,
    IssueCommentCreated
}
```

Write `src/GHKanban.Core/Events/IssueEvent.cs`:

```csharp
using GHKanban.Core.Models;

namespace GHKanban.Core.Events;

public sealed record IssueEvent(
    EventType Type,
    IssueView Issue,
    string? ChangedLabel,         // populated for IssueLabeled / IssueUnlabeled
    string? ChangedAssignee,      // populated for IssueAssigned / IssueUnassigned
    string? CommentBody,          // populated for IssueCommentCreated
    DateTimeOffset At);
```

- [ ] **Step 4: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Core.Tests/ --no-restore
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Core/ tests/GHKanban.Core.Tests/
git commit -m "feat(core): add domain models and event types (spec §4, §6)"
```

---

## Task 3: Rule grammar — parser

**Files:**
- Create: `src/GHKanban.Rules/RuleAst.cs`
- Create: `src/GHKanban.Rules/RuleException.cs`
- Create: `src/GHKanban.Rules/RuleParser.cs`
- Test: `tests/GHKanban.Rules.Tests/RuleParserTests.cs`

Spec reference: §4 "Rule grammar (v1)".

Grammar (recap from spec):
- Predicates: `has-label("X")`, `assignee == "name"`, `assignee-of-mine`, `state == "open"|"closed"`, `age-days > N`, `age-days < N`, `milestone == "X"`, `repo == "owner/name"`
- Boolean: `and`, `or`, `not`, parentheses
- Literals: double-quoted string, integer

- [ ] **Step 1: Write the failing parser tests**

Write `tests/GHKanban.Rules.Tests/RuleParserTests.cs`:

```csharp
using GHKanban.Rules;
using Xunit;

namespace GHKanban.Rules.Tests;

public class RuleParserTests
{
    [Theory]
    [InlineData("has-label(\"bug\")")]
    [InlineData("assignee == \"alice\"")]
    [InlineData("assignee-of-mine")]
    [InlineData("state == \"open\"")]
    [InlineData("state == \"closed\"")]
    [InlineData("age-days > 30")]
    [InlineData("age-days < 7")]
    [InlineData("milestone == \"v1\"")]
    [InlineData("repo == \"owner/repo\"")]
    public void Parses_each_predicate(string input)
    {
        var ast = RuleParser.Parse(input);
        Assert.NotNull(ast);
    }

    [Theory]
    [InlineData("has-label(\"a\") and has-label(\"b\")")]
    [InlineData("has-label(\"a\") or has-label(\"b\")")]
    [InlineData("not has-label(\"a\")")]
    [InlineData("(has-label(\"a\") or has-label(\"b\")) and not state == \"closed\"")]
    public void Parses_boolean_compositions(string input)
    {
        var ast = RuleParser.Parse(input);
        Assert.NotNull(ast);
    }

    [Theory]
    [InlineData("has-label", "expected '('")]
    [InlineData("has-label(\"unterminated", "unterminated string")]
    [InlineData("foo == \"x\"", "unknown identifier")]
    [InlineData("age-days >", "expected integer")]
    [InlineData("and has-label(\"a\")", "unexpected 'and'")]
    public void Rejects_invalid_input_with_diagnostic(string input, string expectedSubstring)
    {
        var ex = Assert.Throws<RuleException>(() => RuleParser.Parse(input));
        Assert.Contains(expectedSubstring, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

```pwsh
dotnet test tests/GHKanban.Rules.Tests/ --no-restore
```

Expected: compilation errors.

- [ ] **Step 3: Write the AST and exception types**

Write `src/GHKanban.Rules/RuleAst.cs`:

```csharp
namespace GHKanban.Rules;

public abstract record RuleNode;

public sealed record HasLabelNode(string Label) : RuleNode;
public sealed record AssigneeEqualsNode(string Username) : RuleNode;
public sealed record AssigneeOfMineNode : RuleNode;
public sealed record StateEqualsNode(string State) : RuleNode;
public sealed record AgeDaysGreaterNode(int Days) : RuleNode;
public sealed record AgeDaysLessNode(int Days) : RuleNode;
public sealed record MilestoneEqualsNode(string Milestone) : RuleNode;
public sealed record RepoEqualsNode(string Repo) : RuleNode;

public sealed record AndNode(RuleNode Left, RuleNode Right) : RuleNode;
public sealed record OrNode(RuleNode Left, RuleNode Right) : RuleNode;
public sealed record NotNode(RuleNode Inner) : RuleNode;
```

Write `src/GHKanban.Rules/RuleException.cs`:

```csharp
namespace GHKanban.Rules;

public sealed class RuleException : Exception
{
    public int Position { get; }
    public RuleException(string message, int position) : base($"{message} (at position {position})")
        => Position = position;
}
```

- [ ] **Step 4: Write the parser**

Write `src/GHKanban.Rules/RuleParser.cs`:

```csharp
namespace GHKanban.Rules;

/// <summary>
/// Recursive-descent parser for the rule grammar.
/// Grammar (precedence low → high): or, and, not, primary.
/// </summary>
public static class RuleParser
{
    public static RuleNode Parse(string input)
    {
        var tokens = Tokenize(input);
        var p = new Parser(tokens, input);
        var ast = p.ParseOr();
        p.Expect(TokenType.End);
        return ast;
    }

    private enum TokenType { Identifier, String, Integer, LParen, RParen, EqualsEquals, Greater, Less, And, Or, Not, End }
    private record Token(TokenType Type, string Text, int Position);

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            if (char.IsWhiteSpace(input[i])) { i++; continue; }
            int start = i;
            if (input[i] == '(') { tokens.Add(new(TokenType.LParen, "(", start)); i++; continue; }
            if (input[i] == ')') { tokens.Add(new(TokenType.RParen, ")", start)); i++; continue; }
            if (input[i] == '>') { tokens.Add(new(TokenType.Greater, ">", start)); i++; continue; }
            if (input[i] == '<') { tokens.Add(new(TokenType.Less, "<", start)); i++; continue; }
            if (i + 1 < input.Length && input[i] == '=' && input[i + 1] == '=')
            { tokens.Add(new(TokenType.EqualsEquals, "==", start)); i += 2; continue; }
            if (input[i] == '"')
            {
                int j = i + 1;
                while (j < input.Length && input[j] != '"') j++;
                if (j == input.Length) throw new RuleException("unterminated string literal", start);
                tokens.Add(new(TokenType.String, input.Substring(i + 1, j - i - 1), start));
                i = j + 1;
                continue;
            }
            if (char.IsDigit(input[i]))
            {
                int j = i;
                while (j < input.Length && char.IsDigit(input[j])) j++;
                tokens.Add(new(TokenType.Integer, input.Substring(i, j - i), start));
                i = j;
                continue;
            }
            if (char.IsLetter(input[i]))
            {
                int j = i;
                while (j < input.Length && (char.IsLetterOrDigit(input[j]) || input[j] == '-')) j++;
                var word = input.Substring(i, j - i);
                var type = word switch
                {
                    "and" => TokenType.And,
                    "or" => TokenType.Or,
                    "not" => TokenType.Not,
                    _ => TokenType.Identifier
                };
                tokens.Add(new(type, word, start));
                i = j;
                continue;
            }
            throw new RuleException($"unexpected character '{input[i]}'", i);
        }
        tokens.Add(new(TokenType.End, "", input.Length));
        return tokens;
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly string _input;
        private int _pos;

        public Parser(List<Token> tokens, string input) { _tokens = tokens; _input = input; }

        private Token Peek() => _tokens[_pos];
        private Token Consume() => _tokens[_pos++];

        public void Expect(TokenType t)
        {
            var tok = Peek();
            if (tok.Type != t) throw new RuleException($"expected {t}, got '{tok.Text}'", tok.Position);
            Consume();
        }

        public RuleNode ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Type == TokenType.Or) { Consume(); var right = ParseAnd(); left = new OrNode(left, right); }
            return left;
        }

        public RuleNode ParseAnd()
        {
            var left = ParseNot();
            while (Peek().Type == TokenType.And) { Consume(); var right = ParseNot(); left = new AndNode(left, right); }
            return left;
        }

        public RuleNode ParseNot()
        {
            if (Peek().Type == TokenType.Not) { Consume(); return new NotNode(ParseNot()); }
            return ParsePrimary();
        }

        public RuleNode ParsePrimary()
        {
            var tok = Peek();
            if (tok.Type == TokenType.LParen)
            {
                Consume();
                var inner = ParseOr();
                Expect(TokenType.RParen);
                return inner;
            }
            if (tok.Type == TokenType.Identifier)
            {
                return tok.Text switch
                {
                    "has-label" => ParseHasLabel(),
                    "assignee" => ParseAssigneeEquals(),
                    "assignee-of-mine" => ParseAssigneeOfMine(),
                    "state" => ParseStateEquals(),
                    "age-days" => ParseAgeDays(),
                    "milestone" => ParseMilestoneEquals(),
                    "repo" => ParseRepoEquals(),
                    _ => throw new RuleException($"unknown identifier '{tok.Text}'", tok.Position)
                };
            }
            throw new RuleException($"unexpected '{tok.Text}'", tok.Position);
        }

        private RuleNode ParseHasLabel()
        { Consume(); Expect(TokenType.LParen); var s = Peek(); if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position); Consume(); Expect(TokenType.RParen); return new HasLabelNode(s.Text); }

        private RuleNode ParseAssigneeEquals()
        { Consume(); Expect(TokenType.EqualsEquals); var s = Peek(); if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position); Consume(); return new AssigneeEqualsNode(s.Text); }

        private RuleNode ParseAssigneeOfMine()
        { Consume(); return new AssigneeOfMineNode(); }

        private RuleNode ParseStateEquals()
        { Consume(); Expect(TokenType.EqualsEquals); var s = Peek(); if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position); Consume(); return new StateEqualsNode(s.Text); }

        private RuleNode ParseAgeDays()
        {
            Consume();
            var op = Peek();
            if (op.Type == TokenType.Greater) { Consume(); var n = Peek(); if (n.Type != TokenType.Integer) throw new RuleException("expected integer", n.Position); Consume(); return new AgeDaysGreaterNode(int.Parse(n.Text)); }
            if (op.Type == TokenType.Less) { Consume(); var n = Peek(); if (n.Type != TokenType.Integer) throw new RuleException("expected integer", n.Position); Consume(); return new AgeDaysLessNode(int.Parse(n.Text)); }
            throw new RuleException("expected '>' or '<'", op.Position);
        }

        private RuleNode ParseMilestoneEquals()
        { Consume(); Expect(TokenType.EqualsEquals); var s = Peek(); if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position); Consume(); return new MilestoneEqualsNode(s.Text); }

        private RuleNode ParseRepoEquals()
        { Consume(); Expect(TokenType.EqualsEquals); var s = Peek(); if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position); Consume(); return new RepoEqualsNode(s.Text); }
    }
}
```

- [ ] **Step 5: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Rules.Tests/ --no-restore
```

Expected: all parser tests pass.

- [ ] **Step 6: Commit**

```pwsh
git add src/GHKanban.Rules/ tests/GHKanban.Rules.Tests/
git commit -m "feat(rules): rule grammar parser with diagnostics (spec §4)"
```

---

## Task 4: Rule grammar — evaluator

**Files:**
- Create: `src/GHKanban.Rules/RuleEvaluator.cs`
- Test: `tests/GHKanban.Rules.Tests/RuleEvaluatorTests.cs`

- [ ] **Step 1: Write the failing evaluator tests**

Write `tests/GHKanban.Rules.Tests/RuleEvaluatorTests.cs`:

```csharp
using GHKanban.Core.Models;
using GHKanban.Rules;
using Xunit;

namespace GHKanban.Rules.Tests;

public class RuleEvaluatorTests
{
    private static IssueView Issue(
        string repo = "owner/repo", int number = 1, IssueState state = IssueState.Open,
        string[]? labels = null, string[]? assignees = null, string? milestone = null,
        int ageDays = 0)
    {
        var now = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        return new IssueView(repo, number, "t", state,
            labels ?? [], assignees ?? [], milestone,
            CreatedAt: now.AddDays(-ageDays), UpdatedAt: now, HtmlUrl: "");
    }

    private static readonly DateTimeOffset _now = new(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
    private const string Me = "myself";

    [Fact]
    public void HasLabel_matches_when_present()
        => Assert.True(Eval("has-label(\"bug\")", Issue(labels: ["bug"])));

    [Fact]
    public void HasLabel_does_not_match_when_absent()
        => Assert.False(Eval("has-label(\"bug\")", Issue(labels: ["feature"])));

    [Fact]
    public void AssigneeEquals_matches_case_insensitive()
        => Assert.True(Eval("assignee == \"Alice\"", Issue(assignees: ["alice"])));

    [Fact]
    public void AssigneeOfMine_matches_current_user()
        => Assert.True(Eval("assignee-of-mine", Issue(assignees: [Me])));

    [Fact]
    public void StateEquals_matches()
    {
        Assert.True(Eval("state == \"open\"", Issue(state: IssueState.Open)));
        Assert.True(Eval("state == \"closed\"", Issue(state: IssueState.Closed)));
        Assert.False(Eval("state == \"open\"", Issue(state: IssueState.Closed)));
    }

    [Fact]
    public void AgeDays_comparisons_work()
    {
        Assert.True(Eval("age-days > 5", Issue(ageDays: 10)));
        Assert.False(Eval("age-days > 5", Issue(ageDays: 3)));
        Assert.True(Eval("age-days < 5", Issue(ageDays: 3)));
    }

    [Fact]
    public void And_Or_Not_compose()
    {
        var i = Issue(labels: ["bug", "urgent"]);
        Assert.True(Eval("has-label(\"bug\") and has-label(\"urgent\")", i));
        Assert.True(Eval("has-label(\"bug\") or has-label(\"missing\")", i));
        Assert.False(Eval("not has-label(\"bug\")", i));
    }

    private static bool Eval(string rule, IssueView issue)
        => new RuleEvaluator(_now, Me).Evaluate(RuleParser.Parse(rule), issue);
}
```

- [ ] **Step 2: Run tests, expect compile failure**

```pwsh
dotnet test tests/GHKanban.Rules.Tests/ --no-restore
```

- [ ] **Step 3: Write the evaluator**

Write `src/GHKanban.Rules/RuleEvaluator.cs`:

```csharp
using GHKanban.Core.Models;

namespace GHKanban.Rules;

public sealed class RuleEvaluator
{
    private readonly DateTimeOffset _now;
    private readonly string _currentUser;

    public RuleEvaluator(DateTimeOffset now, string currentUser)
    { _now = now; _currentUser = currentUser; }

    public bool Evaluate(RuleNode node, IssueView issue) => node switch
    {
        HasLabelNode n => issue.HasLabel(n.Label),
        AssigneeEqualsNode n => issue.HasAssignee(n.Username),
        AssigneeOfMineNode => issue.HasAssignee(_currentUser),
        StateEqualsNode n => string.Equals(n.State, issue.State.ToString(), StringComparison.OrdinalIgnoreCase),
        AgeDaysGreaterNode n => issue.AgeDays(_now) > n.Days,
        AgeDaysLessNode n => issue.AgeDays(_now) < n.Days,
        MilestoneEqualsNode n => string.Equals(issue.Milestone, n.Milestone, StringComparison.OrdinalIgnoreCase),
        RepoEqualsNode n => string.Equals(issue.Repo, n.Repo, StringComparison.OrdinalIgnoreCase),
        AndNode n => Evaluate(n.Left, issue) && Evaluate(n.Right, issue),
        OrNode n => Evaluate(n.Left, issue) || Evaluate(n.Right, issue),
        NotNode n => !Evaluate(n.Inner, issue),
        _ => throw new InvalidOperationException($"Unknown rule node: {node.GetType().Name}")
    };
}
```

- [ ] **Step 4: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Rules.Tests/ --no-restore
```

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Rules/RuleEvaluator.cs tests/GHKanban.Rules.Tests/RuleEvaluatorTests.cs
git commit -m "feat(rules): rule evaluator against IssueView (spec §4)"
```

---

## Task 5: YAML config loader

**Files:**
- Create: `src/GHKanban.Config/YamlConfigLoader.cs`
- Create: `src/GHKanban.Config/ConfigStore.cs`
- Test: `tests/GHKanban.Config.Tests/YamlConfigLoaderTests.cs`

Add YamlDotNet reference to `src/GHKanban.Config/GHKanban.Config.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="YamlDotNet" />
</ItemGroup>
```

Spec reference: §4 "Config (YAML, file-based)".

- [ ] **Step 1: Write the failing loader tests**

Write `tests/GHKanban.Config.Tests/YamlConfigLoaderTests.cs`:

```csharp
using GHKanban.Config;
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Config.Tests;

public class YamlConfigLoaderTests
{
    [Fact]
    public void LoadsGitHubConfig()
    {
        var yaml = """
            auth:
              pat-env: MY_PAT
            webhook:
              public-url: https://example.test/hook
              secret-env: MY_SECRET
            poll-interval: 5m
            reconcile-interval: 30m
            """;
        var cfg = YamlConfigLoader.LoadGitHubConfig(yaml);
        Assert.Equal("MY_PAT", cfg.Auth.PatEnv);
        Assert.Equal("https://example.test/hook", cfg.Webhook.PublicUrl);
        Assert.Equal("MY_SECRET", cfg.Webhook.SecretEnv);
        Assert.Equal(TimeSpan.FromMinutes(5), cfg.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), cfg.ReconcileInterval);
    }

    [Fact]
    public void LoadsBoardConfig()
    {
        var yaml = """
            name: My Board
            scope:
              repos: [owner/foo, owner/bar]
              orgs: []
              filters:
                state: open
            columns:
              - name: Inbox
                rule: not has-label("triage")
              - name: Triage
                rule: has-label("triage")
            """;
        var cfg = YamlConfigLoader.LoadBoardConfig("my-board", yaml);
        Assert.Equal("my-board", cfg.Id);
        Assert.Equal("My Board", cfg.Name);
        Assert.Equal(2, cfg.Scope.Repos.Count);
        Assert.Equal(2, cfg.Columns.Count);
        Assert.Equal("Inbox", cfg.Columns[0].Name);
    }

    [Fact]
    public void LoadsAgentConfig()
    {
        var yaml = """
            name: Stub Acknowledger
            implementation: GHKanban.Agents.StubAcknowledgeAgent
            triggers:
              - on: issue.labeled
                when: label == "ai-pls"
            """;
        var cfg = YamlConfigLoader.LoadAgentConfig("stub-ack", yaml);
        Assert.Equal("Stub Acknowledger", cfg.Name);
        Assert.Single(cfg.Triggers);
        Assert.Equal("issue.labeled", cfg.Triggers[0].On);
    }

    [Fact]
    public void GitHubConfigDefaultsPollIntervalsWhenAbsent()
    {
        var yaml = """
            auth:
              pat-env: P
            webhook: {}
            """;
        var cfg = YamlConfigLoader.LoadGitHubConfig(yaml);
        Assert.Equal(TimeSpan.FromMinutes(5), cfg.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), cfg.ReconcileInterval);
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

```pwsh
dotnet test tests/GHKanban.Config.Tests/ --no-restore
```

- [ ] **Step 3: Write the YAML loader**

Write `src/GHKanban.Config/YamlConfigLoader.cs`:

```csharp
using GHKanban.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GHKanban.Config;

public static class YamlConfigLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private sealed class RawGitHub
    {
        public RawAuth? Auth { get; set; }
        public RawWebhook? Webhook { get; set; }
        public string? PollInterval { get; set; }
        public string? ReconcileInterval { get; set; }
    }
    private sealed class RawAuth { public string? PatEnv { get; set; } }
    private sealed class RawWebhook { public string? PublicUrl { get; set; } public string? SecretEnv { get; set; } }

    public static GitHubConfig LoadGitHubConfig(string yaml)
    {
        var raw = _deserializer.Deserialize<RawGitHub>(yaml) ?? throw new InvalidOperationException("empty github.yaml");
        return new GitHubConfig(
            Auth: new GitHubAuth(raw.Auth?.PatEnv ?? throw new InvalidOperationException("auth.pat-env required")),
            Webhook: new GitHubWebhook(raw.Webhook?.PublicUrl, raw.Webhook?.SecretEnv),
            PollInterval: ParseDuration(raw.PollInterval) ?? TimeSpan.FromMinutes(5),
            ReconcileInterval: ParseDuration(raw.ReconcileInterval) ?? TimeSpan.FromMinutes(30));
    }

    private sealed class RawBoard
    {
        public string? Name { get; set; }
        public RawScope? Scope { get; set; }
        public List<RawColumn>? Columns { get; set; }
    }
    private sealed class RawScope
    {
        public List<string>? Repos { get; set; }
        public List<string>? Orgs { get; set; }
        public Dictionary<string, string>? Filters { get; set; }
    }
    private sealed class RawColumn { public string? Name { get; set; } public string? Rule { get; set; } }

    public static BoardConfig LoadBoardConfig(string id, string yaml)
    {
        var raw = _deserializer.Deserialize<RawBoard>(yaml) ?? throw new InvalidOperationException("empty board yaml");
        var scope = new BoardScope(
            Repos: raw.Scope?.Repos ?? new(),
            Orgs: raw.Scope?.Orgs ?? new(),
            Filters: raw.Scope?.Filters ?? new());
        var cols = (raw.Columns ?? new())
            .Select(c => new ColumnConfig(c.Name ?? throw new InvalidOperationException("column.name required"),
                                          c.Rule ?? throw new InvalidOperationException("column.rule required")))
            .ToList();
        return new BoardConfig(id, raw.Name ?? id, scope, cols);
    }

    private sealed class RawAgent
    {
        public string? Name { get; set; }
        public string? Implementation { get; set; }
        public List<RawTrigger>? Triggers { get; set; }
    }
    private sealed class RawTrigger { public string? On { get; set; } public string? When { get; set; } }

    public static AgentConfig LoadAgentConfig(string id, string yaml)
    {
        var raw = _deserializer.Deserialize<RawAgent>(yaml) ?? throw new InvalidOperationException("empty agent yaml");
        var triggers = (raw.Triggers ?? new())
            .Select(t => new TriggerSpec(t.On ?? throw new InvalidOperationException("trigger.on required"),
                                         t.When ?? "true"))
            .ToList();
        return new AgentConfig(id, raw.Name ?? id,
            raw.Implementation ?? throw new InvalidOperationException("implementation required"),
            triggers);
    }

    private static TimeSpan? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.EndsWith("ms")) return TimeSpan.FromMilliseconds(int.Parse(s[..^2]));
        if (s.EndsWith("s")) return TimeSpan.FromSeconds(int.Parse(s[..^1]));
        if (s.EndsWith("m")) return TimeSpan.FromMinutes(int.Parse(s[..^1]));
        if (s.EndsWith("h")) return TimeSpan.FromHours(int.Parse(s[..^1]));
        throw new FormatException($"unrecognised duration: {s}");
    }
}
```

Write `src/GHKanban.Config/ConfigStore.cs`:

```csharp
using GHKanban.Core.Models;

namespace GHKanban.Config;

/// <summary>
/// In-memory snapshot of all loaded config. Replaced atomically on each reload.
/// </summary>
public sealed record ConfigSnapshot(
    GitHubConfig GitHub,
    IReadOnlyList<BoardConfig> Boards,
    IReadOnlyList<AgentConfig> Agents,
    IReadOnlyList<string> Errors);

public sealed class ConfigStore
{
    private ConfigSnapshot _current;
    private readonly object _lock = new();

    public ConfigStore(ConfigSnapshot initial) { _current = initial; }

    public ConfigSnapshot Current { get { lock (_lock) return _current; } }

    public event Action<ConfigSnapshot>? OnChange;

    public void Set(ConfigSnapshot s)
    {
        lock (_lock) _current = s;
        OnChange?.Invoke(s);
    }
}
```

- [ ] **Step 4: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Config.Tests/ --no-restore
```

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Config/ tests/GHKanban.Config.Tests/
git commit -m "feat(config): YAML loader and in-memory ConfigStore (spec §4)"
```

---

## Task 6: Config hot-reload (ConfigWatcher)

**Files:**
- Create: `src/GHKanban.Config/ConfigWatcher.cs`
- Test: `tests/GHKanban.Config.Tests/ConfigWatcherTests.cs`

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.Config.Tests/ConfigWatcherTests.cs`:

```csharp
using GHKanban.Config;
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Config.Tests;

public class ConfigWatcherTests
{
    [Fact]
    public async Task LoadsInitialConfigFromDirectory()
    {
        var dir = MakeTempConfigDir();
        try
        {
            var snap = ConfigWatcher.LoadOnce(dir);
            Assert.Empty(snap.Errors);
            Assert.Equal("MY_PAT", snap.GitHub.Auth.PatEnv);
            Assert.Single(snap.Boards);
            Assert.Single(snap.Agents);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EmitsDiagnosticsForInvalidYaml()
    {
        var dir = MakeTempConfigDir();
        File.WriteAllText(Path.Combine(dir, "boards", "bad.yaml"), "name: Bad\nscope:\n  repos: [unterminated");
        try
        {
            var snap = ConfigWatcher.LoadOnce(dir);
            Assert.NotEmpty(snap.Errors);
            Assert.Contains("bad.yaml", snap.Errors[0]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string MakeTempConfigDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ghkanban-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "boards"));
        Directory.CreateDirectory(Path.Combine(dir, "agents"));
        File.WriteAllText(Path.Combine(dir, "github.yaml"), """
            auth:
              pat-env: MY_PAT
            webhook: {}
            """);
        File.WriteAllText(Path.Combine(dir, "boards", "example.yaml"), """
            name: Example
            scope:
              repos: [owner/repo]
            columns:
              - name: Inbox
                rule: state == "open"
            """);
        File.WriteAllText(Path.Combine(dir, "agents", "stub.yaml"), """
            name: Stub
            implementation: GHKanban.Agents.StubAcknowledgeAgent
            triggers:
              - on: issue.labeled
                when: has-label("ai-pls")
            """);
        return dir;
    }
}
```

- [ ] **Step 2: Run tests, expect compile failure**

```pwsh
dotnet test tests/GHKanban.Config.Tests/ --no-restore
```

- [ ] **Step 3: Write the watcher**

Write `src/GHKanban.Config/ConfigWatcher.cs`:

```csharp
using GHKanban.Core.Models;

namespace GHKanban.Config;

public sealed class ConfigWatcher : IDisposable
{
    private readonly string _root;
    private readonly ConfigStore _store;
    private readonly FileSystemWatcher _watcher;
    private DateTime _lastReload = DateTime.MinValue;
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(250);

    public ConfigWatcher(string root, ConfigStore store)
    {
        _root = root;
        _store = store;
        _watcher = new FileSystemWatcher(root, "*.yaml") { IncludeSubdirectories = true, EnableRaisingEvents = true };
        _watcher.Changed += (_, _) => DebouncedReload();
        _watcher.Created += (_, _) => DebouncedReload();
        _watcher.Deleted += (_, _) => DebouncedReload();
        _watcher.Renamed += (_, _) => DebouncedReload();
    }

    private void DebouncedReload()
    {
        var now = DateTime.UtcNow;
        if (now - _lastReload < _debounce) return;
        _lastReload = now;
        Task.Delay(_debounce).ContinueWith(_ => _store.Set(LoadOnce(_root)));
    }

    public static ConfigSnapshot LoadOnce(string root)
    {
        var errors = new List<string>();
        GitHubConfig? github = null;
        var boards = new List<BoardConfig>();
        var agents = new List<AgentConfig>();

        var ghPath = Path.Combine(root, "github.yaml");
        if (File.Exists(ghPath))
        {
            try { github = YamlConfigLoader.LoadGitHubConfig(File.ReadAllText(ghPath)); }
            catch (Exception ex) { errors.Add($"github.yaml: {ex.Message}"); }
        }
        else errors.Add("github.yaml not found");

        var boardsDir = Path.Combine(root, "boards");
        if (Directory.Exists(boardsDir))
        {
            foreach (var f in Directory.GetFiles(boardsDir, "*.yaml"))
            {
                try
                {
                    var id = Path.GetFileNameWithoutExtension(f);
                    boards.Add(YamlConfigLoader.LoadBoardConfig(id, File.ReadAllText(f)));
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(f)}: {ex.Message}"); }
            }
        }

        var agentsDir = Path.Combine(root, "agents");
        if (Directory.Exists(agentsDir))
        {
            foreach (var f in Directory.GetFiles(agentsDir, "*.yaml"))
            {
                try
                {
                    var id = Path.GetFileNameWithoutExtension(f);
                    agents.Add(YamlConfigLoader.LoadAgentConfig(id, File.ReadAllText(f)));
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(f)}: {ex.Message}"); }
            }
        }

        return new ConfigSnapshot(
            github ?? new GitHubConfig(new GitHubAuth("UNSET"), new GitHubWebhook(null, null), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)),
            boards, agents, errors);
    }

    public void Dispose() => _watcher.Dispose();
}
```

- [ ] **Step 4: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Config.Tests/ --no-restore
```

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Config/ConfigWatcher.cs tests/GHKanban.Config.Tests/ConfigWatcherTests.cs
git commit -m "feat(config): file watcher with debounced hot-reload (spec §4)"
```

---

## Task 7: SQLite schema + persistence stores

**Files:**
- Create: `src/GHKanban.Sync/SqliteSchema.cs`
- Create: `src/GHKanban.Sync/SyncCursorStore.cs`
- Create: `src/GHKanban.Agents/AgentRunStore.cs`
- Test: `tests/GHKanban.Sync.Tests/SyncCursorStoreTests.cs`
- Test: `tests/GHKanban.Agents.Tests/AgentRunStoreTests.cs`

Add `Microsoft.Data.Sqlite` reference to `src/GHKanban.Sync/GHKanban.Sync.csproj` AND `src/GHKanban.Agents/GHKanban.Agents.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite" />
</ItemGroup>
```

Spec reference: §4 "Persistence (SQLite, ephemeral state)".

- [ ] **Step 1: Write the failing cursor store test**

Write `tests/GHKanban.Sync.Tests/SyncCursorStoreTests.cs`:

```csharp
using GHKanban.Sync;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GHKanban.Sync.Tests;

public class SyncCursorStoreTests
{
    [Fact]
    public async Task RoundTripsCursor()
    {
        var conn = OpenInMemory();
        SqliteSchema.Apply(conn);
        var store = new SyncCursorStore(conn);

        await store.SetAsync("owner/repo", "cursor-abc");
        var got = await store.GetAsync("owner/repo");

        Assert.Equal("cursor-abc", got);
    }

    [Fact]
    public async Task ReturnsNullForUnknownRepo()
    {
        var conn = OpenInMemory();
        SqliteSchema.Apply(conn);
        var store = new SyncCursorStore(conn);
        Assert.Null(await store.GetAsync("unknown/repo"));
    }

    private static SqliteConnection OpenInMemory()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }
}
```

- [ ] **Step 2: Write the failing agent run store test**

Write `tests/GHKanban.Agents.Tests/AgentRunStoreTests.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.Core.Models;
using GHKanban.Sync;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GHKanban.Agents.Tests;

public class AgentRunStoreTests
{
    [Fact]
    public async Task RecordsAndListsRuns()
    {
        var conn = OpenInMemory();
        SqliteSchema.Apply(conn);
        var store = new AgentRunStore(conn);

        await store.RecordAsync(new AgentRunRecord(
            AgentName: "Stub", TriggerEvent: "issue.labeled",
            Repo: "owner/repo", IssueNumber: 1,
            StartedAt: DateTimeOffset.UtcNow, FinishedAt: DateTimeOffset.UtcNow,
            Status: AgentRunStatus.Success, Output: "ok", Error: null));

        var recent = (await store.GetRecentAsync(limit: 10)).ToList();
        Assert.Single(recent);
        Assert.Equal("Stub", recent[0].AgentName);
    }

    private static SqliteConnection OpenInMemory()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }
}
```

- [ ] **Step 3: Run tests, expect compile failure**

```pwsh
dotnet test --no-restore
```

- [ ] **Step 4: Write schema + stores**

Write `src/GHKanban.Sync/SqliteSchema.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace GHKanban.Sync;

public static class SqliteSchema
{
    public static void Apply(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sync_cursor (
              repo TEXT PRIMARY KEY,
              cursor TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS webhook_events (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              event_id TEXT NOT NULL,
              received_at TEXT NOT NULL,
              payload TEXT NOT NULL,
              UNIQUE(event_id)
            );
            CREATE TABLE IF NOT EXISTS agent_runs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              agent_name TEXT NOT NULL,
              trigger_event TEXT NOT NULL,
              repo TEXT NOT NULL,
              issue_number INTEGER NOT NULL,
              started_at TEXT NOT NULL,
              finished_at TEXT NOT NULL,
              status TEXT NOT NULL,
              output TEXT,
              error TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_agent_runs_started ON agent_runs(started_at DESC);
            """;
        cmd.ExecuteNonQuery();
    }
}
```

Write `src/GHKanban.Sync/SyncCursorStore.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace GHKanban.Sync;

public sealed class SyncCursorStore
{
    private readonly SqliteConnection _conn;
    public SyncCursorStore(SqliteConnection conn) { _conn = conn; }

    public async Task<string?> GetAsync(string repo, CancellationToken ct = default)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT cursor FROM sync_cursor WHERE repo = @r";
        cmd.Parameters.AddWithValue("@r", repo);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task SetAsync(string repo, string cursor, CancellationToken ct = default)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sync_cursor(repo, cursor, updated_at) VALUES (@r, @c, @u)
            ON CONFLICT(repo) DO UPDATE SET cursor = excluded.cursor, updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("@r", repo);
        cmd.Parameters.AddWithValue("@c", cursor);
        cmd.Parameters.AddWithValue("@u", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
```

Write `src/GHKanban.Agents/AgentRunStore.cs`:

```csharp
using GHKanban.Core.Models;
using Microsoft.Data.Sqlite;

namespace GHKanban.Agents;

public sealed record AgentRunRecord(
    string AgentName, string TriggerEvent, string Repo, int IssueNumber,
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    AgentRunStatus Status, string? Output, string? Error);

public sealed class AgentRunStore
{
    private readonly SqliteConnection _conn;
    public AgentRunStore(SqliteConnection conn) { _conn = conn; }

    public async Task RecordAsync(AgentRunRecord r, CancellationToken ct = default)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO agent_runs(agent_name, trigger_event, repo, issue_number, started_at, finished_at, status, output, error)
            VALUES (@n, @t, @r, @i, @s, @f, @st, @o, @e);
            """;
        cmd.Parameters.AddWithValue("@n", r.AgentName);
        cmd.Parameters.AddWithValue("@t", r.TriggerEvent);
        cmd.Parameters.AddWithValue("@r", r.Repo);
        cmd.Parameters.AddWithValue("@i", r.IssueNumber);
        cmd.Parameters.AddWithValue("@s", r.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@f", r.FinishedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@st", r.Status.ToString());
        cmd.Parameters.AddWithValue("@o", (object?)r.Output ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@e", (object?)r.Error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AgentRunRecord>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var list = new List<AgentRunRecord>();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT agent_name, trigger_event, repo, issue_number, started_at, finished_at, status, output, error FROM agent_runs ORDER BY started_at DESC LIMIT @l";
        cmd.Parameters.AddWithValue("@l", limit);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new AgentRunRecord(
                r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                DateTimeOffset.Parse(r.GetString(4)), DateTimeOffset.Parse(r.GetString(5)),
                Enum.Parse<AgentRunStatus>(r.GetString(6)),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8)));
        }
        return list;
    }
}
```

Add project reference from `Agents` to `Sync` so `SqliteSchema` is reachable from agent tests:

```pwsh
dotnet add src/GHKanban.Agents/ reference src/GHKanban.Sync/
```

- [ ] **Step 5: Run tests, expect green**

```pwsh
dotnet test --no-restore
```

- [ ] **Step 6: Commit**

```pwsh
git add src/GHKanban.Sync/ src/GHKanban.Agents/ tests/GHKanban.Sync.Tests/ tests/GHKanban.Agents.Tests/
git commit -m "feat(persistence): SQLite schema and stores for cursors + agent runs (spec §4)"
```

---

## Task 8: GitHub adapter — interfaces + webhook signature validation

**Files:**
- Create: `src/GHKanban.GitHub/IGitHubReader.cs`
- Create: `src/GHKanban.GitHub/IGitHubWriter.cs`
- Create: `src/GHKanban.GitHub/WebhookSignatureValidator.cs`
- Create: `src/GHKanban.GitHub/EventMapper.cs`
- Test: `tests/GHKanban.GitHub.Tests/WebhookSignatureValidatorTests.cs`
- Test: `tests/GHKanban.GitHub.Tests/EventMapperTests.cs`

Add `Octokit` reference to `src/GHKanban.GitHub/GHKanban.GitHub.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Octokit" />
</ItemGroup>
```

Spec reference: §5 (GitHub integration).

- [ ] **Step 1: Write the failing tests**

Write `tests/GHKanban.GitHub.Tests/WebhookSignatureValidatorTests.cs`:

```csharp
using GHKanban.GitHub;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace GHKanban.GitHub.Tests;

public class WebhookSignatureValidatorTests
{
    [Fact]
    public void ValidatesCorrectSignature()
    {
        const string secret = "super-secret";
        const string body = "{\"action\":\"opened\"}";
        var sig = ComputeSignature(secret, body);

        Assert.True(WebhookSignatureValidator.Validate(secret, body, sig));
    }

    [Fact]
    public void RejectsTamperedBody()
    {
        const string secret = "super-secret";
        var sig = ComputeSignature(secret, "{\"action\":\"opened\"}");
        Assert.False(WebhookSignatureValidator.Validate(secret, "{\"action\":\"closed\"}", sig));
    }

    [Fact]
    public void RejectsMalformedHeader()
    {
        Assert.False(WebhookSignatureValidator.Validate("s", "b", "notvalid"));
        Assert.False(WebhookSignatureValidator.Validate("s", "b", ""));
    }

    private static string ComputeSignature(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

Write `tests/GHKanban.GitHub.Tests/EventMapperTests.cs`:

```csharp
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
```

- [ ] **Step 2: Write interfaces, signature validator, event mapper**

Write `src/GHKanban.GitHub/IGitHubReader.cs`:

```csharp
using GHKanban.Core.Models;

namespace GHKanban.GitHub;

public sealed record IssuePage(IReadOnlyList<IssueView> Issues, string? NextCursor);

public interface IGitHubReader
{
    Task<IssuePage> ListIssuesAsync(string repo, string? afterCursor, CancellationToken ct = default);
    Task<string> GetCurrentUserLoginAsync(CancellationToken ct = default);
}
```

Write `src/GHKanban.GitHub/IGitHubWriter.cs`:

```csharp
namespace GHKanban.GitHub;

public interface IGitHubWriter
{
    Task PostCommentAsync(string repo, int issueNumber, string body, CancellationToken ct = default);
    Task AddLabelAsync(string repo, int issueNumber, string label, CancellationToken ct = default);
    Task AssignAsync(string repo, int issueNumber, string user, CancellationToken ct = default);
}
```

Write `src/GHKanban.GitHub/WebhookSignatureValidator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace GHKanban.GitHub;

public static class WebhookSignatureValidator
{
    public static bool Validate(string secret, string body, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.Ordinal))
            return false;
        var supplied = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied));
    }
}
```

Write `src/GHKanban.GitHub/EventMapper.cs`:

```csharp
using System.Text.Json;
using GHKanban.Core.Events;
using GHKanban.Core.Models;

namespace GHKanban.GitHub;

public static class EventMapper
{
    public static IssueEvent? MapIssueEvent(string eventName, string jsonBody)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        var action = root.GetProperty("action").GetString();

        var type = (eventName, action) switch
        {
            ("issues", "opened") => EventType.IssueOpened,
            ("issues", "labeled") => EventType.IssueLabeled,
            ("issues", "unlabeled") => EventType.IssueUnlabeled,
            ("issues", "assigned") => EventType.IssueAssigned,
            ("issues", "unassigned") => EventType.IssueUnassigned,
            ("issues", "closed") => EventType.IssueClosed,
            ("issues", "reopened") => EventType.IssueReopened,
            ("issue_comment", "created") => EventType.IssueCommentCreated,
            _ => (EventType?)null
        };
        if (type is null) return null;

        var issueEl = root.GetProperty("issue");
        var repo = root.GetProperty("repository").GetProperty("full_name").GetString()!;
        var labels = issueEl.GetProperty("labels").EnumerateArray()
            .Select(l => l.GetProperty("name").GetString()!)
            .ToList();
        var assignees = issueEl.GetProperty("assignees").EnumerateArray()
            .Select(a => a.GetProperty("login").GetString() ?? a.GetProperty("name").GetString()!)
            .ToList();

        var issue = new IssueView(
            repo,
            issueEl.GetProperty("number").GetInt32(),
            issueEl.GetProperty("title").GetString()!,
            issueEl.GetProperty("state").GetString() == "closed" ? IssueState.Closed : IssueState.Open,
            labels, assignees,
            issueEl.TryGetProperty("milestone", out var ms) && ms.ValueKind == JsonValueKind.Object
                ? ms.GetProperty("title").GetString() : null,
            issueEl.GetProperty("created_at").GetDateTimeOffset(),
            issueEl.GetProperty("updated_at").GetDateTimeOffset(),
            issueEl.GetProperty("html_url").GetString()!);

        string? changedLabel = type is EventType.IssueLabeled or EventType.IssueUnlabeled
            && root.TryGetProperty("label", out var lbl) ? lbl.GetProperty("name").GetString() : null;
        string? changedAssignee = type is EventType.IssueAssigned or EventType.IssueUnassigned
            && root.TryGetProperty("assignee", out var asg) ? asg.GetProperty("login").GetString() : null;
        string? commentBody = type == EventType.IssueCommentCreated
            && root.TryGetProperty("comment", out var c) ? c.GetProperty("body").GetString() : null;

        return new IssueEvent(type.Value, issue, changedLabel, changedAssignee, commentBody, DateTimeOffset.UtcNow);
    }
}
```

EventMapper test note: `assignees` array uses "login" in real GH payloads. The test JSON uses an empty array so no extraction needed for the first test, and the second labeled-test also has empty assignees. The code handles both `login` and `name` for robustness.

- [ ] **Step 3: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.GitHub.Tests/ --no-restore
```

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.GitHub/ tests/GHKanban.GitHub.Tests/
git commit -m "feat(github): interfaces, webhook signature validator, event mapper (spec §5, §6)"
```

---

## Task 9: GitHub reader + writer concrete implementations (Octokit)

**Files:**
- Create: `src/GHKanban.GitHub/GitHubReader.cs`
- Create: `src/GHKanban.GitHub/GitHubWriter.cs`
- Test: `tests/GHKanban.GitHub.Tests/GitHubWriterTests.cs` (interface contract verification only — no live API)

The concrete reader/writer wrap Octokit. Tests for these are minimal because the value is contract conformance, not Octokit's behaviour — we trust Octokit to call the right HTTP endpoints. Higher-level integration tests in Task 11 (sync engine) verify behaviour against a fake.

- [ ] **Step 1: Write the writer with constructor + connection wiring**

Write `src/GHKanban.GitHub/GitHubWriter.cs`:

```csharp
using Octokit;

namespace GHKanban.GitHub;

public sealed class GitHubWriter : IGitHubWriter
{
    private readonly IGitHubClient _client;

    public GitHubWriter(string personalAccessToken)
    {
        var conn = new Connection(new ProductHeaderValue("GHKanban", "0.1"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
        _client = new GitHubClient(conn);
    }

    // Test ctor for injecting a fake/mock IGitHubClient
    internal GitHubWriter(IGitHubClient client) { _client = client; }

    public async Task PostCommentAsync(string repo, int issueNumber, string body, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        await _client.Issue.Comment.Create(owner, name, issueNumber, body);
    }

    public async Task AddLabelAsync(string repo, int issueNumber, string label, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        await _client.Issue.Labels.AddToIssue(owner, name, issueNumber, [label]);
    }

    public async Task AssignAsync(string repo, int issueNumber, string user, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        await _client.Issue.Assignee.AddAssignees(owner, name, issueNumber, new AssigneesUpdate([user]));
    }

    private static (string Owner, string Name) SplitRepo(string repo)
    {
        var parts = repo.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : throw new ArgumentException($"Bad repo: {repo}");
    }
}
```

- [ ] **Step 2: Write the reader using Octokit GraphQL or REST**

Write `src/GHKanban.GitHub/GitHubReader.cs`:

```csharp
using GHKanban.Core.Models;
using Octokit;

namespace GHKanban.GitHub;

public sealed class GitHubReader : IGitHubReader
{
    private readonly IGitHubClient _client;

    public GitHubReader(string personalAccessToken)
    {
        var conn = new Connection(new ProductHeaderValue("GHKanban", "0.1"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
        _client = new GitHubClient(conn);
    }

    internal GitHubReader(IGitHubClient client) { _client = client; }

    public async Task<IssuePage> ListIssuesAsync(string repo, string? afterCursor, CancellationToken ct = default)
    {
        var (owner, name) = SplitRepo(repo);
        var options = new ApiOptions { PageSize = 100, PageCount = 1 };
        var req = new RepositoryIssueRequest { State = ItemStateFilter.All };
        var issues = await _client.Issue.GetAllForRepository(owner, name, req, options);

        var mapped = issues.Where(i => i.PullRequest is null).Select(Map).ToList();
        // v1 uses simple page-count-of-1; cursor is the last issue's updated_at (good enough for incremental polling)
        var next = mapped.Count > 0 ? mapped.Max(i => i.UpdatedAt).ToString("O") : null;
        return new IssuePage(mapped, next);
    }

    public async Task<string> GetCurrentUserLoginAsync(CancellationToken ct = default)
    {
        var user = await _client.User.Current();
        return user.Login;
    }

    private static IssueView Map(Issue i) => new(
        Repo: $"{i.Repository?.Owner.Login}/{i.Repository?.Name}",
        Number: i.Number,
        Title: i.Title,
        State: i.State.Value == ItemState.Closed ? IssueState.Closed : IssueState.Open,
        Labels: i.Labels.Select(l => l.Name).ToList(),
        Assignees: i.Assignees.Select(a => a.Login).ToList(),
        Milestone: i.Milestone?.Title,
        CreatedAt: i.CreatedAt,
        UpdatedAt: i.UpdatedAt ?? i.CreatedAt,
        HtmlUrl: i.HtmlUrl);

    private static (string Owner, string Name) SplitRepo(string repo)
    {
        var parts = repo.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : throw new ArgumentException($"Bad repo: {repo}");
    }
}
```

Note: The repo coming back from `GetAllForRepository` may have a null Repository on the Issue object — fall back to the requested repo string if so. Adjust the Map method to take repo as a parameter if needed:

```csharp
private static IssueView Map(Issue i, string repo) => new(
    Repo: repo, // ...
```

And update `ListIssuesAsync` accordingly to pass `repo` into Map.

- [ ] **Step 3: Verify build (no new tests for the live wrappers in this task)**

```pwsh
dotnet build src/GHKanban.GitHub/ --no-restore
```

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.GitHub/GitHubReader.cs src/GHKanban.GitHub/GitHubWriter.cs
git commit -m "feat(github): Octokit-backed reader and writer (spec §5)"
```

---

## Task 10: In-memory issue model store + observability

**Files:**
- Create: `src/GHKanban.Sync/IssueModelStore.cs`
- Test: `tests/GHKanban.Sync.Tests/IssueModelStoreTests.cs`

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.Sync.Tests/IssueModelStoreTests.cs`:

```csharp
using GHKanban.Core.Models;
using GHKanban.Sync;
using Xunit;

namespace GHKanban.Sync.Tests;

public class IssueModelStoreTests
{
    [Fact]
    public void StoresAndRetrievesIssues()
    {
        var store = new IssueModelStore();
        var i = MakeIssue(1);
        store.Upsert(i);

        var got = store.GetIssue("owner/repo", 1);

        Assert.Equal(i, got);
    }

    [Fact]
    public void ReturnsAllIssuesForRepos()
    {
        var store = new IssueModelStore();
        store.Upsert(MakeIssue(1, repo: "a/b"));
        store.Upsert(MakeIssue(2, repo: "a/b"));
        store.Upsert(MakeIssue(3, repo: "c/d"));

        var got = store.GetIssuesForRepos(["a/b"]).ToList();

        Assert.Equal(2, got.Count);
    }

    [Fact]
    public void RaisesChangeEventOnUpsert()
    {
        var store = new IssueModelStore();
        var fired = false;
        store.OnChange += () => fired = true;
        store.Upsert(MakeIssue(1));
        Assert.True(fired);
    }

    private static IssueView MakeIssue(int n, string repo = "owner/repo") => new(
        repo, n, "t", IssueState.Open, [], [], null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, $"https://github.com/{repo}/issues/{n}");
}
```

- [ ] **Step 2: Run tests, expect compile failure**

- [ ] **Step 3: Write the store**

Write `src/GHKanban.Sync/IssueModelStore.cs`:

```csharp
using System.Collections.Concurrent;
using GHKanban.Core.Models;

namespace GHKanban.Sync;

/// <summary>
/// Thread-safe in-memory cache of all known issues across configured repos.
/// Populated by the sync engine (polling + webhook). Read by the UI.
/// </summary>
public sealed class IssueModelStore
{
    private readonly ConcurrentDictionary<(string Repo, int Number), IssueView> _store = new();

    public event Action? OnChange;

    public void Upsert(IssueView issue)
    {
        _store[(issue.Repo, issue.Number)] = issue;
        OnChange?.Invoke();
    }

    public IssueView? GetIssue(string repo, int number)
        => _store.TryGetValue((repo, number), out var i) ? i : null;

    public IEnumerable<IssueView> GetIssuesForRepos(IEnumerable<string> repos)
    {
        var set = repos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _store.Values.Where(i => set.Contains(i.Repo));
    }

    public IEnumerable<IssueView> All() => _store.Values;
}
```

- [ ] **Step 4: Run tests, expect green**

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Sync/IssueModelStore.cs tests/GHKanban.Sync.Tests/IssueModelStoreTests.cs
git commit -m "feat(sync): in-memory issue model store with change events (spec §5)"
```

---

## Task 11: Polling + webhook + reconciler hosted services

**Files:**
- Create: `src/GHKanban.Sync/PollingService.cs`
- Create: `src/GHKanban.Sync/WebhookEventProcessor.cs`
- Create: `src/GHKanban.Sync/ReconcilerService.cs`
- Test: `tests/GHKanban.Sync.Tests/PollingServiceTests.cs`

Add `Microsoft.Extensions.Hosting` reference to `src/GHKanban.Sync/GHKanban.Sync.csproj`.

- [ ] **Step 1: Write the failing polling test using a fake reader**

Write `tests/GHKanban.Sync.Tests/PollingServiceTests.cs`:

```csharp
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
            new[] { new BoardConfig("b", "B", new BoardScope(new[]{"owner/repo"}, [], new Dictionary<string,string>()), []) },
            [], []));
        var svc = new PollingService(reader, store, cfg, NullLogger<PollingService>.Instance);

        await svc.PollOnceAsync(CancellationToken.None);

        Assert.Equal(2, store.All().Count());
    }

    private static IssueView MakeIssue(int n) =>
        new("owner/repo", n, "t", IssueState.Open, [], [], null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, $"https://github.com/owner/repo/issues/{n}");
}
```

- [ ] **Step 2: Write the three hosted services**

Write `src/GHKanban.Sync/PollingService.cs`:

```csharp
using GHKanban.Config;
using GHKanban.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

public sealed class PollingService : BackgroundService
{
    private readonly IGitHubReader _reader;
    private readonly IssueModelStore _store;
    private readonly ConfigStore _cfg;
    private readonly ILogger<PollingService> _log;

    public PollingService(IGitHubReader reader, IssueModelStore store, ConfigStore cfg, ILogger<PollingService> log)
    { _reader = reader; _store = store; _cfg = cfg; _log = log; }

    public async Task PollOnceAsync(CancellationToken ct)
    {
        var snap = _cfg.Current;
        var repos = snap.Boards.SelectMany(b => b.Scope.Repos).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in repos)
        {
            try
            {
                var page = await _reader.ListIssuesAsync(repo, afterCursor: null, ct);
                foreach (var i in page.Issues) _store.Upsert(i);
                _log.LogInformation("Polled {Count} issues for {Repo}", page.Issues.Count, repo);
            }
            catch (Exception ex) { _log.LogError(ex, "Poll failed for {Repo}", repo); }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOnceAsync(stoppingToken);
            try { await Task.Delay(_cfg.Current.GitHub.PollInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
```

Write `src/GHKanban.Sync/WebhookEventProcessor.cs`:

```csharp
using System.Threading.Channels;
using GHKanban.Core.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

public sealed class WebhookEventProcessor : BackgroundService
{
    private readonly Channel<IssueEvent> _channel;
    private readonly IssueModelStore _store;
    private readonly ILogger<WebhookEventProcessor> _log;

    public WebhookEventProcessor(IssueModelStore store, ILogger<WebhookEventProcessor> log)
    {
        _channel = Channel.CreateUnbounded<IssueEvent>();
        _store = store;
        _log = log;
    }

    public ChannelWriter<IssueEvent> Writer => _channel.Writer;
    public ChannelReader<IssueEvent> Reader => _channel.Reader;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var ev in _channel.Reader.ReadAllAsync(ct))
        {
            _store.Upsert(ev.Issue);
            _log.LogInformation("Webhook event {Type} for {Repo}#{Number}", ev.Type, ev.Issue.Repo, ev.Issue.Number);
        }
    }
}
```

Write `src/GHKanban.Sync/ReconcilerService.cs`:

```csharp
using GHKanban.Config;
using GHKanban.GitHub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.Sync;

public sealed class ReconcilerService : BackgroundService
{
    private readonly IGitHubReader _reader;
    private readonly IssueModelStore _store;
    private readonly ConfigStore _cfg;
    private readonly ILogger<ReconcilerService> _log;

    public ReconcilerService(IGitHubReader reader, IssueModelStore store, ConfigStore cfg, ILogger<ReconcilerService> log)
    { _reader = reader; _store = store; _cfg = cfg; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(_cfg.Current.GitHub.ReconcileInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }

            var snap = _cfg.Current;
            var repos = snap.Boards.SelectMany(b => b.Scope.Repos).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var repo in repos)
            {
                try
                {
                    var page = await _reader.ListIssuesAsync(repo, afterCursor: null, stoppingToken);
                    foreach (var i in page.Issues) _store.Upsert(i);
                    _log.LogInformation("Reconciled {Count} issues for {Repo}", page.Issues.Count, repo);
                }
                catch (Exception ex) { _log.LogError(ex, "Reconcile failed for {Repo}", repo); }
            }
        }
    }
}
```

- [ ] **Step 3: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Sync.Tests/ --no-restore
```

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.Sync/PollingService.cs src/GHKanban.Sync/WebhookEventProcessor.cs src/GHKanban.Sync/ReconcilerService.cs tests/GHKanban.Sync.Tests/PollingServiceTests.cs
git commit -m "feat(sync): polling + webhook + reconciler hosted services (spec §5)"
```

---

## Task 12: Agent abstractions and trigger evaluator

**Files:**
- Create: `src/GHKanban.Agents/IGHKanbanAgent.cs`
- Create: `src/GHKanban.Agents/AgentRegistry.cs`
- Create: `src/GHKanban.Agents/TriggerEvaluator.cs`
- Test: `tests/GHKanban.Agents.Tests/TriggerEvaluatorTests.cs`

Add `Microsoft.Agents.AI` reference to `src/GHKanban.Agents/GHKanban.Agents.csproj`. If the package doesn't resolve cleanly (MAF API can drift), the subagent should fall back to a minimal `AIAgent` shim in this project — the spec needs the abstraction shape, not the live MAF surface, for the stub to work.

Spec reference: §6 (Trigger pipeline), §7 (Agent runtime).

- [ ] **Step 1: Write the failing trigger evaluator test**

Write `tests/GHKanban.Agents.Tests/TriggerEvaluatorTests.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using Xunit;

namespace GHKanban.Agents.Tests;

public class TriggerEvaluatorTests
{
    [Fact]
    public void MatchesLabeledTriggerWithMatchingLabel()
    {
        var trigger = new TriggerSpec(On: "issue.labeled", When: "has-label(\"ai-pls\")");
        var issue = Issue(labels: ["ai-pls"]);
        var ev = new IssueEvent(EventType.IssueLabeled, issue, "ai-pls", null, null, DateTimeOffset.UtcNow);
        Assert.True(TriggerEvaluator.Matches(trigger, ev, currentUser: "me"));
    }

    [Fact]
    public void DoesNotMatchWhenEventTypeDiffers()
    {
        var trigger = new TriggerSpec(On: "issue.assigned", When: "has-label(\"ai-pls\")");
        var ev = new IssueEvent(EventType.IssueLabeled, Issue(labels:["ai-pls"]), "ai-pls", null, null, DateTimeOffset.UtcNow);
        Assert.False(TriggerEvaluator.Matches(trigger, ev, "me"));
    }

    [Fact]
    public void EmptyWhenIsAlwaysTrue()
    {
        var trigger = new TriggerSpec(On: "issue.opened", When: "true");
        var ev = new IssueEvent(EventType.IssueOpened, Issue(), null, null, null, DateTimeOffset.UtcNow);
        // "true" is a special-cased always-match; parser would reject it as identifier.
        // We require evaluator to treat null/empty/literal "true" as always-true.
        Assert.True(TriggerEvaluator.Matches(trigger, ev, "me"));
    }

    private static IssueView Issue(string[]? labels = null) =>
        new("owner/repo", 1, "t", IssueState.Open, labels ?? [], [], null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "");
}
```

- [ ] **Step 2: Write the abstractions and evaluator**

Write `src/GHKanban.Agents/IGHKanbanAgent.cs`:

```csharp
using GHKanban.Core.Models;

namespace GHKanban.Agents;

/// <summary>
/// Wraps a Microsoft Agent Framework agent with a trigger entrypoint scoped to issue events.
/// Implementations encapsulate the agent logic; the runtime invokes Trigger when a registered
/// trigger fires.
/// </summary>
public interface IGHKanbanAgent
{
    string Name { get; }
    Task<AgentRunResult> TriggerAsync(IssueContext context, CancellationToken ct = default);
}
```

Write `src/GHKanban.Agents/AgentRegistry.cs`:

```csharp
using System.Reflection;
using GHKanban.Core.Models;

namespace GHKanban.Agents;

/// <summary>
/// Resolves AgentConfig.Implementation strings to instantiated IGHKanbanAgent objects.
/// </summary>
public sealed class AgentRegistry
{
    private readonly IServiceProvider _services;

    public AgentRegistry(IServiceProvider services) { _services = services; }

    public IGHKanbanAgent Resolve(AgentConfig config)
    {
        var type = Type.GetType(config.Implementation)
                   ?? FindInLoadedAssemblies(config.Implementation)
                   ?? throw new InvalidOperationException($"Agent implementation not found: {config.Implementation}");

        var instance = ActivatorUtilities.CreateInstance(_services, type, config.Name);
        return (IGHKanbanAgent)instance;
    }

    private static Type? FindInLoadedAssemblies(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName))
            .FirstOrDefault(t => t is not null);
}
```

Add `Microsoft.Extensions.DependencyInjection.Abstractions` reference if `ActivatorUtilities` isn't transitively available.

Write `src/GHKanban.Agents/TriggerEvaluator.cs`:

```csharp
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using GHKanban.Rules;

namespace GHKanban.Agents;

public static class TriggerEvaluator
{
    public static bool Matches(TriggerSpec trigger, IssueEvent ev, string currentUser)
    {
        if (!EventNameMatches(trigger.On, ev.Type)) return false;
        if (string.IsNullOrWhiteSpace(trigger.When) || trigger.When.Trim() == "true") return true;
        try
        {
            var ast = RuleParser.Parse(trigger.When);
            return new RuleEvaluator(ev.At, currentUser).Evaluate(ast, ev.Issue);
        }
        catch { return false; }
    }

    private static bool EventNameMatches(string spec, EventType type) => spec switch
    {
        "issue.opened" => type == EventType.IssueOpened,
        "issue.labeled" => type == EventType.IssueLabeled,
        "issue.unlabeled" => type == EventType.IssueUnlabeled,
        "issue.assigned" => type == EventType.IssueAssigned,
        "issue.unassigned" => type == EventType.IssueUnassigned,
        "issue.closed" => type == EventType.IssueClosed,
        "issue.reopened" => type == EventType.IssueReopened,
        "issue.comment.created" => type == EventType.IssueCommentCreated,
        _ => false
    };
}
```

- [ ] **Step 3: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Agents.Tests/ --no-restore
```

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.Agents/ tests/GHKanban.Agents.Tests/TriggerEvaluatorTests.cs
git commit -m "feat(agents): IGHKanbanAgent + AgentRegistry + TriggerEvaluator (spec §6, §7)"
```

---

## Task 13: Stub agent + agent dispatcher

**Files:**
- Create: `src/GHKanban.Agents/StubAcknowledgeAgent.cs`
- Create: `src/GHKanban.Agents/AgentDispatcher.cs`
- Test: `tests/GHKanban.Agents.Tests/StubAcknowledgeAgentTests.cs`
- Test: `tests/GHKanban.Agents.Tests/AgentDispatcherTests.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/GHKanban.Agents.Tests/StubAcknowledgeAgentTests.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.Core.Models;
using GHKanban.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Agents.Tests;

public class StubAcknowledgeAgentTests
{
    [Fact]
    public async Task PostsExpectedCommentFormat()
    {
        var writer = Substitute.For<IGitHubWriter>();
        var agent = new StubAcknowledgeAgent("My Agent", writer, NullLogger<StubAcknowledgeAgent>.Instance);

        var context = new IssueContext(
            Issue: new IssueView("owner/repo", 42, "t", IssueState.Open, [], [], null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "https://github.com/owner/repo/issues/42"),
            TriggerEvent: "issue.labeled",
            MatchingRule: "has-label(\"ai-pls\")",
            AgentName: "My Agent");

        var result = await agent.TriggerAsync(context);

        Assert.Equal(AgentRunStatus.Success, result.Status);
        await writer.Received(1).PostCommentAsync(
            "owner/repo", 42,
            Arg.Is<string>(s =>
                s.Contains("My Agent") && s.Contains("issue.labeled") && s.Contains("has-label(\"ai-pls\")")),
            Arg.Any<CancellationToken>());
    }
}
```

Write `tests/GHKanban.Agents.Tests/AgentDispatcherTests.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.Config;
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using GHKanban.Sync;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Agents.Tests;

public class AgentDispatcherTests
{
    [Fact]
    public async Task DispatchesMatchingAgentAndRecordsRun()
    {
        var agent = Substitute.For<IGHKanbanAgent>();
        agent.Name.Returns("Stub");
        agent.TriggerAsync(Arg.Any<IssueContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentRunResult(AgentRunStatus.Success, "ok", null));

        var registry = Substitute.For<AgentRegistry>(Substitute.For<IServiceProvider>());
        // For this test we bypass AgentRegistry by injecting a pre-resolved map.

        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        SqliteSchema.Apply(conn);
        var store = new AgentRunStore(conn);

        var dispatcher = new AgentDispatcher(
            new Dictionary<string, IGHKanbanAgent>(StringComparer.OrdinalIgnoreCase) { ["stub"] = agent },
            store,
            currentUser: "me",
            NullLogger<AgentDispatcher>.Instance);

        var ev = new IssueEvent(
            EventType.IssueLabeled,
            new IssueView("owner/repo", 1, "t", IssueState.Open, ["ai-pls"], [], null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ""),
            "ai-pls", null, null, DateTimeOffset.UtcNow);

        var agentCfg = new AgentConfig("stub", "Stub", "Test",
            new[] { new TriggerSpec("issue.labeled", "has-label(\"ai-pls\")") });

        await dispatcher.DispatchAsync(ev, new[] { agentCfg }, CancellationToken.None);

        await agent.Received(1).TriggerAsync(Arg.Any<IssueContext>(), Arg.Any<CancellationToken>());
        Assert.Single(await store.GetRecentAsync(10));
    }
}
```

- [ ] **Step 2: Write the stub and dispatcher**

Write `src/GHKanban.Agents/StubAcknowledgeAgent.cs`:

```csharp
using GHKanban.Core.Models;
using GHKanban.GitHub;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

public sealed class StubAcknowledgeAgent : IGHKanbanAgent
{
    private readonly IGitHubWriter _writer;
    private readonly ILogger<StubAcknowledgeAgent> _log;

    public string Name { get; }

    public StubAcknowledgeAgent(string name, IGitHubWriter writer, ILogger<StubAcknowledgeAgent> log)
    { Name = name; _writer = writer; _log = log; }

    public async Task<AgentRunResult> TriggerAsync(IssueContext ctx, CancellationToken ct = default)
    {
        var body =
            $"🤖 GHKanban: agent **{Name}** triggered by `{ctx.TriggerEvent}` (rule: `{ctx.MatchingRule}`).\n" +
            $"This is a stub acknowledgement — no other action taken.";
        try
        {
            await _writer.PostCommentAsync(ctx.Issue.Repo, ctx.Issue.Number, body, ct);
            _log.LogInformation("Stub posted ack on {Repo}#{Num}", ctx.Issue.Repo, ctx.Issue.Number);
            return new AgentRunResult(AgentRunStatus.Success, body, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Stub failed to post on {Repo}#{Num}", ctx.Issue.Repo, ctx.Issue.Number);
            return new AgentRunResult(AgentRunStatus.Failed, null, ex.Message);
        }
    }
}
```

Write `src/GHKanban.Agents/AgentDispatcher.cs`:

```csharp
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

public sealed class AgentDispatcher
{
    private readonly IReadOnlyDictionary<string, IGHKanbanAgent> _agents;
    private readonly AgentRunStore _runs;
    private readonly string _currentUser;
    private readonly ILogger<AgentDispatcher> _log;

    public AgentDispatcher(IReadOnlyDictionary<string, IGHKanbanAgent> agents, AgentRunStore runs, string currentUser, ILogger<AgentDispatcher> log)
    { _agents = agents; _runs = runs; _currentUser = currentUser; _log = log; }

    public async Task DispatchAsync(IssueEvent ev, IEnumerable<AgentConfig> configs, CancellationToken ct)
    {
        foreach (var cfg in configs)
        {
            if (!_agents.TryGetValue(cfg.Id, out var agent))
            {
                _log.LogWarning("Agent {Id} configured but not registered; skipping", cfg.Id);
                continue;
            }
            foreach (var trigger in cfg.Triggers)
            {
                if (!TriggerEvaluator.Matches(trigger, ev, _currentUser)) continue;

                var ctx = new IssueContext(ev.Issue, trigger.On, trigger.When, cfg.Name);
                var started = DateTimeOffset.UtcNow;
                AgentRunResult result;
                try { result = await agent.TriggerAsync(ctx, ct); }
                catch (Exception ex)
                { result = new AgentRunResult(AgentRunStatus.Failed, null, ex.Message); }
                var finished = DateTimeOffset.UtcNow;

                await _runs.RecordAsync(new AgentRunRecord(
                    cfg.Name, trigger.On, ev.Issue.Repo, ev.Issue.Number,
                    started, finished, result.Status, result.Output, result.Error), ct);
            }
        }
    }
}
```

- [ ] **Step 3: Run tests, expect green**

```pwsh
dotnet test tests/GHKanban.Agents.Tests/ --no-restore
```

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.Agents/StubAcknowledgeAgent.cs src/GHKanban.Agents/AgentDispatcher.cs tests/GHKanban.Agents.Tests/
git commit -m "feat(agents): stub agent and dispatcher with run recording (spec §7)"
```

---

## Task 14: Blazor Server scaffolding + Program.cs + DI wiring

**Files:**
- Create: `src/GHKanban.Web/Program.cs`
- Create: `src/GHKanban.Web/FirstRunWizard.cs`
- Create: `src/GHKanban.Web/Components/App.razor`
- Create: `src/GHKanban.Web/Components/Routes.razor`
- Create: `src/GHKanban.Web/Components/_Imports.razor`
- Create: `src/GHKanban.Web/Components/Layout/MainLayout.razor`
- Create: `src/GHKanban.Web/Components/Layout/NavMenu.razor`
- Create: `src/GHKanban.Web/wwwroot/app.css`

Add Web SDK references to `src/GHKanban.Web/GHKanban.Web.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Serilog.AspNetCore" />
  <PackageReference Include="Serilog.Sinks.Console" />
  <PackageReference Include="Microsoft.Data.Sqlite" />
</ItemGroup>
```

Spec reference: §8 (UI), §9 (Distribution and runtime).

- [ ] **Step 1: Write Program.cs**

Write `src/GHKanban.Web/Program.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.Config;
using GHKanban.Core.Events;
using GHKanban.Core.Models;
using GHKanban.GitHub;
using GHKanban.Sync;
using GHKanban.Web;
using GHKanban.Web.Components;
using Microsoft.Data.Sqlite;
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
    var currentUser = reader.GetCurrentUserLoginAsync().GetAwaiter().GetResult();
    return new AgentDispatcher(agents, sp.GetRequiredService<AgentRunStore>(), currentUser, sp.GetRequiredService<ILogger<AgentDispatcher>>());
});

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

// Start config watcher
_ = app.Services.GetRequiredService<ConfigWatcher>();

// Webhook endpoint (registered in Task 15)

app.UseStaticFiles();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Urls.Add($"http://localhost:{Environment.GetEnvironmentVariable("GHKANBAN_PORT") ?? "5454"}");
app.Run();
```

- [ ] **Step 2: Write FirstRunWizard**

Write `src/GHKanban.Web/FirstRunWizard.cs`:

```csharp
namespace GHKanban.Web;

public static class FirstRunWizard
{
    public static void EnsureInitialised(string configRoot)
    {
        if (Directory.Exists(configRoot)) return;
        Directory.CreateDirectory(configRoot);
        Directory.CreateDirectory(Path.Combine(configRoot, "boards"));
        Directory.CreateDirectory(Path.Combine(configRoot, "agents"));

        File.WriteAllText(Path.Combine(configRoot, "github.yaml"), """
            # GHKanban GitHub configuration
            auth:
              # Name of an environment variable holding your GitHub Personal Access Token.
              # Required scopes: repo (and read:org for org-wide reads).
              pat-env: GHKANBAN_PAT
            webhook:
              # Optional. If set, the app will not auto-register webhooks on GitHub;
              # you must configure GH to POST to this URL. Use Tailscale Funnel / Cloudflare Tunnel / etc.
              # public-url: https://your-tailnet.ts.net/hook
              # secret-env: GHKANBAN_WEBHOOK_SECRET
            poll-interval: 5m
            reconcile-interval: 30m
            """);

        File.WriteAllText(Path.Combine(configRoot, "boards", "example.yaml"), """
            # Example board. Replace "your-org/your-repo" with a real repo.
            name: Example Board
            scope:
              repos: [your-org/your-repo]
            columns:
              - name: Inbox
                rule: not has-label("triage") and not has-label("in-progress")
              - name: Triage
                rule: has-label("triage")
              - name: In Progress
                rule: has-label("in-progress")
              - name: Stale
                rule: state == "open" and age-days > 30
            """);

        File.WriteAllText(Path.Combine(configRoot, "agents", "stub-ack.yaml"), """
            # Stub acknowledger — comments on issues that get the configured trigger.
            name: Stub Acknowledger
            implementation: GHKanban.Agents.StubAcknowledgeAgent
            triggers:
              - on: issue.labeled
                when: has-label("ai-pls")
            """);

        Console.WriteLine($"GHKanban: created configuration at {configRoot}");
        Console.WriteLine($"GHKanban: set the env var GHKANBAN_PAT to your GitHub Personal Access Token, then visit http://localhost:5454");
    }
}
```

- [ ] **Step 3: Write Blazor shell files**

Write `src/GHKanban.Web/Components/_Imports.razor`:

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using GHKanban.Core.Models
@using GHKanban.Config
@using GHKanban.Sync
@using GHKanban.Agents
@using GHKanban.Rules
```

Write `src/GHKanban.Web/Components/App.razor`:

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="app.css" />
    <title>GHKanban</title>
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

Write `src/GHKanban.Web/Components/Routes.razor`:

```razor
<Router AppAssembly="@typeof(Routes).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)" />
    </Found>
</Router>
```

Write `src/GHKanban.Web/Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<div class="layout">
    <NavMenu />
    <main>
        @Body
    </main>
</div>
```

Write `src/GHKanban.Web/Components/Layout/NavMenu.razor`:

```razor
<nav class="nav">
    <span class="brand">GHKanban</span>
    <a href="/">Boards</a>
    <a href="/activity">Activity</a>
    <a href="/config">Config</a>
</nav>
```

Write `src/GHKanban.Web/wwwroot/app.css`:

```css
body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; margin: 0; }
.layout { display: flex; flex-direction: column; min-height: 100vh; }
.nav { background: #24292f; color: white; padding: 0.5rem 1rem; display: flex; gap: 1rem; align-items: center; }
.nav .brand { font-weight: 700; }
.nav a { color: white; text-decoration: none; }
.nav a:hover { text-decoration: underline; }
main { padding: 1rem; }
.board { display: flex; gap: 1rem; overflow-x: auto; }
.column { background: #f6f8fa; border-radius: 6px; padding: 0.5rem; min-width: 280px; flex-shrink: 0; }
.column h3 { margin-top: 0; }
.card { background: white; border: 1px solid #d0d7de; border-radius: 6px; padding: 0.5rem; margin-bottom: 0.5rem; }
.card a { color: #0969da; text-decoration: none; }
.card .meta { font-size: 0.85rem; color: #57606a; margin-top: 0.25rem; }
.error { color: #cf222e; }
.activity-row { padding: 0.5rem; border-bottom: 1px solid #d0d7de; }
```

- [ ] **Step 4: Verify the app builds**

```pwsh
dotnet build src/GHKanban.Web/ --no-restore
```

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Web/Program.cs src/GHKanban.Web/FirstRunWizard.cs src/GHKanban.Web/Components/ src/GHKanban.Web/wwwroot/
git commit -m "feat(web): Blazor Server scaffolding, Program.cs, first-run wizard (spec §8, §9)"
```

---

## Task 15: Webhook endpoint registration

**Files:**
- Create: `src/GHKanban.Web/WebhookEndpoint.cs`
- Modify: `src/GHKanban.Web/Program.cs` (add `app.MapPost("/hook", …)`)

- [ ] **Step 1: Write the endpoint**

Write `src/GHKanban.Web/WebhookEndpoint.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.Config;
using GHKanban.GitHub;
using GHKanban.Sync;
using Microsoft.AspNetCore.Mvc;

namespace GHKanban.Web;

public static class WebhookEndpoint
{
    public static void MapWebhook(this WebApplication app)
    {
        app.MapPost("/hook", async (
            HttpContext ctx,
            ConfigStore configStore,
            WebhookEventProcessor processor,
            AgentDispatcher dispatcher,
            ILogger<WebhookEndpoint> log) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();

            var snap = configStore.Current;
            var secretEnv = snap.GitHub.Webhook.SecretEnv;
            if (secretEnv is not null)
            {
                var secret = Environment.GetEnvironmentVariable(secretEnv) ?? "";
                var sig = ctx.Request.Headers["X-Hub-Signature-256"].ToString();
                if (!WebhookSignatureValidator.Validate(secret, body, sig))
                {
                    log.LogWarning("Webhook signature validation failed");
                    return Results.Unauthorized();
                }
            }

            var eventName = ctx.Request.Headers["X-GitHub-Event"].ToString();
            var ev = EventMapper.MapIssueEvent(eventName, body);
            if (ev is null)
            {
                log.LogInformation("Webhook event {Event} not mapped; ignored", eventName);
                return Results.Ok();
            }

            await processor.Writer.WriteAsync(ev);
            await dispatcher.DispatchAsync(ev, snap.Agents, ctx.RequestAborted);
            return Results.Ok();
        });
    }
}
```

- [ ] **Step 2: Wire it into Program.cs**

In `src/GHKanban.Web/Program.cs`, just before `app.UseStaticFiles();`, add:

```csharp
app.MapWebhook();
```

And add this using at the top of Program.cs:

```csharp
using GHKanban.Web;
```

(Already there from earlier — verify no duplicate.)

- [ ] **Step 3: Build and verify**

```pwsh
dotnet build src/GHKanban.Web/ --no-restore
```

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.Web/WebhookEndpoint.cs src/GHKanban.Web/Program.cs
git commit -m "feat(web): /hook webhook endpoint with signature validation (spec §5, §6)"
```

---

## Task 16: Board picker page (`/`)

**Files:**
- Create: `src/GHKanban.Web/Components/Pages/BoardPicker.razor`

- [ ] **Step 1: Write the page**

Write `src/GHKanban.Web/Components/Pages/BoardPicker.razor`:

```razor
@page "/"
@inject ConfigStore Configs

<h1>Boards</h1>

@if (Configs.Current.Errors.Count > 0)
{
    <div class="error">
        <strong>Config errors:</strong>
        <ul>@foreach (var e in Configs.Current.Errors) { <li>@e</li> }</ul>
    </div>
}

@if (Configs.Current.Boards.Count == 0)
{
    <p>No boards configured. Create a YAML file under <code>~/.ghkanban/boards/</code>.</p>
}
else
{
    <ul>
        @foreach (var b in Configs.Current.Boards)
        {
            <li><a href="@($"/board/{b.Id}")">@b.Name</a></li>
        }
    </ul>
}
```

- [ ] **Step 2: Build and verify**

```pwsh
dotnet build src/GHKanban.Web/ --no-restore
```

- [ ] **Step 3: Commit**

```pwsh
git add src/GHKanban.Web/Components/Pages/BoardPicker.razor
git commit -m "feat(web): board picker page at / (spec §8)"
```

---

## Task 17: Board view page (`/board/{boardId}`)

**Files:**
- Create: `src/GHKanban.Web/Components/Pages/BoardView.razor`

- [ ] **Step 1: Write the page**

Write `src/GHKanban.Web/Components/Pages/BoardView.razor`:

```razor
@page "/board/{BoardId}"
@inject ConfigStore Configs
@inject IssueModelStore Issues
@inject IGitHubReader Reader
@implements IDisposable

@if (_board is null)
{
    <p>Board <code>@BoardId</code> not found.</p>
}
else
{
    <h1>@_board.Name</h1>
    <div class="board">
        @foreach (var col in _board.Columns)
        {
            <div class="column">
                <h3>@col.Name <span class="meta">(@_placement[col.Name].Count)</span></h3>
                @foreach (var i in _placement[col.Name])
                {
                    <div class="card">
                        <a href="@i.HtmlUrl" target="_blank">@i.Repo#@i.Number</a> — @i.Title
                        <div class="meta">@string.Join(", ", i.Labels) · @i.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm")</div>
                    </div>
                }
            </div>
        }
    </div>
}

@code {
    [Parameter] public string BoardId { get; set; } = "";

    private BoardConfig? _board;
    private Dictionary<string, List<IssueView>> _placement = new();
    private string _currentUser = "";

    protected override async Task OnParametersSetAsync()
    {
        _board = Configs.Current.Boards.FirstOrDefault(b => b.Id == BoardId);
        if (_board is null) return;

        try { _currentUser = await Reader.GetCurrentUserLoginAsync(); }
        catch { _currentUser = ""; }

        RecomputePlacement();
        Issues.OnChange += HandleChange;
    }

    private void HandleChange() => InvokeAsync(() => { RecomputePlacement(); StateHasChanged(); });

    private void RecomputePlacement()
    {
        if (_board is null) return;
        var now = DateTimeOffset.UtcNow;
        var evaluator = new RuleEvaluator(now, _currentUser);
        var parsed = _board.Columns
            .Select(c => (Col: c, Ast: RuleParser.Parse(c.Rule)))
            .ToList();

        _placement = _board.Columns.ToDictionary(c => c.Name, _ => new List<IssueView>());

        foreach (var issue in Issues.GetIssuesForRepos(_board.Scope.Repos))
        {
            var col = parsed.FirstOrDefault(p => evaluator.Evaluate(p.Ast, issue));
            if (col.Col is not null) _placement[col.Col.Name].Add(issue);
        }
    }

    public void Dispose() => Issues.OnChange -= HandleChange;
}
```

- [ ] **Step 2: Build and verify**

```pwsh
dotnet build src/GHKanban.Web/ --no-restore
```

- [ ] **Step 3: Commit**

```pwsh
git add src/GHKanban.Web/Components/Pages/BoardView.razor
git commit -m "feat(web): board view with rule-based column placement and real-time updates (spec §8)"
```

---

## Task 18: Activity feed page (`/activity`)

**Files:**
- Create: `src/GHKanban.Web/Components/Pages/ActivityFeed.razor`

- [ ] **Step 1: Write the page**

Write `src/GHKanban.Web/Components/Pages/ActivityFeed.razor`:

```razor
@page "/activity"
@inject AgentRunStore Runs

<h1>Agent activity</h1>

<table>
    <thead><tr><th>When</th><th>Agent</th><th>Issue</th><th>Trigger</th><th>Status</th><th>Output</th></tr></thead>
    <tbody>
        @foreach (var r in _runs)
        {
            <tr class="activity-row">
                <td>@r.StartedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")</td>
                <td>@r.AgentName</td>
                <td>@r.Repo#@r.IssueNumber</td>
                <td>@r.TriggerEvent</td>
                <td>@r.Status</td>
                <td>@(r.Status == AgentRunStatus.Success ? "✓" : r.Error)</td>
            </tr>
        }
    </tbody>
</table>

@code {
    private IReadOnlyList<AgentRunRecord> _runs = Array.Empty<AgentRunRecord>();

    protected override async Task OnInitializedAsync()
    {
        _runs = await Runs.GetRecentAsync(50);
    }
}
```

- [ ] **Step 2: Build and verify**

```pwsh
dotnet build src/GHKanban.Web/ --no-restore
```

- [ ] **Step 3: Commit**

```pwsh
git add src/GHKanban.Web/Components/Pages/ActivityFeed.razor
git commit -m "feat(web): activity feed page (spec §8)"
```

---

## Task 19: Config view page (`/config`)

**Files:**
- Create: `src/GHKanban.Web/Components/Pages/ConfigView.razor`

- [ ] **Step 1: Write the page**

Write `src/GHKanban.Web/Components/Pages/ConfigView.razor`:

```razor
@page "/config"
@inject ConfigStore Configs

<h1>Configuration</h1>

@if (Configs.Current.Errors.Count > 0)
{
    <div class="error">
        <h2>Errors</h2>
        <ul>@foreach (var e in Configs.Current.Errors) { <li>@e</li> }</ul>
    </div>
}

<h2>GitHub</h2>
<ul>
    <li>PAT env var: <code>@Configs.Current.GitHub.Auth.PatEnv</code> (@(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Configs.Current.GitHub.Auth.PatEnv)) ? "❌ not set" : "✅ set"))</li>
    <li>Webhook URL: @(Configs.Current.GitHub.Webhook.PublicUrl ?? "(not configured — polling only)")</li>
    <li>Poll interval: @Configs.Current.GitHub.PollInterval</li>
    <li>Reconcile interval: @Configs.Current.GitHub.ReconcileInterval</li>
</ul>

<h2>Boards (@Configs.Current.Boards.Count)</h2>
<ul>
    @foreach (var b in Configs.Current.Boards)
    {
        <li><strong>@b.Name</strong> (@b.Id) — @b.Scope.Repos.Count repos, @b.Columns.Count columns</li>
    }
</ul>

<h2>Agents (@Configs.Current.Agents.Count)</h2>
<ul>
    @foreach (var a in Configs.Current.Agents)
    {
        <li><strong>@a.Name</strong> — @a.Implementation — @a.Triggers.Count triggers</li>
    }
</ul>
```

- [ ] **Step 2: Build and verify**

```pwsh
dotnet build src/GHKanban.Web/ --no-restore
```

- [ ] **Step 3: Commit**

```pwsh
git add src/GHKanban.Web/Components/Pages/ConfigView.razor
git commit -m "feat(web): read-only config view page (spec §8)"
```

---

## Task 20: Wire AgentDispatcher into polling (so label changes via polling also trigger agents)

**Files:**
- Modify: `src/GHKanban.Sync/PollingService.cs`

Polling currently upserts issues but doesn't synthesise events. For v1, polling-driven label/state changes should still trigger agents if matching triggers are registered. This task adds delta detection in the polling loop.

- [ ] **Step 1: Add delta detection to PollingService**

Replace the `PollOnceAsync` method in `src/GHKanban.Sync/PollingService.cs` with this delta-aware version:

```csharp
public async Task PollOnceAsync(CancellationToken ct)
{
    var snap = _cfg.Current;
    var repos = snap.Boards.SelectMany(b => b.Scope.Repos).Distinct(StringComparer.OrdinalIgnoreCase);
    foreach (var repo in repos)
    {
        try
        {
            var page = await _reader.ListIssuesAsync(repo, afterCursor: null, ct);
            foreach (var fresh in page.Issues)
            {
                var prior = _store.GetIssue(fresh.Repo, fresh.Number);
                _store.Upsert(fresh);

                if (prior is null) continue; // first-seen issues don't synthesise events in v1 (they were seen on startup poll)
                await EmitDeltaEventsAsync(prior, fresh, ct);
            }
            _log.LogInformation("Polled {Count} issues for {Repo}", page.Issues.Count, repo);
        }
        catch (Exception ex) { _log.LogError(ex, "Poll failed for {Repo}", repo); }
    }
}

private async Task EmitDeltaEventsAsync(IssueView prior, IssueView fresh, CancellationToken ct)
{
    foreach (var added in fresh.Labels.Except(prior.Labels, StringComparer.OrdinalIgnoreCase))
    {
        var ev = new IssueEvent(EventType.IssueLabeled, fresh, added, null, null, DateTimeOffset.UtcNow);
        await _events.WriteAsync(ev, ct);
    }
    foreach (var removed in prior.Labels.Except(fresh.Labels, StringComparer.OrdinalIgnoreCase))
    {
        var ev = new IssueEvent(EventType.IssueUnlabeled, fresh, removed, null, null, DateTimeOffset.UtcNow);
        await _events.WriteAsync(ev, ct);
    }
    foreach (var added in fresh.Assignees.Except(prior.Assignees, StringComparer.OrdinalIgnoreCase))
    {
        var ev = new IssueEvent(EventType.IssueAssigned, fresh, null, added, null, DateTimeOffset.UtcNow);
        await _events.WriteAsync(ev, ct);
    }
    if (prior.State == IssueState.Open && fresh.State == IssueState.Closed)
        await _events.WriteAsync(new IssueEvent(EventType.IssueClosed, fresh, null, null, null, DateTimeOffset.UtcNow), ct);
    if (prior.State == IssueState.Closed && fresh.State == IssueState.Open)
        await _events.WriteAsync(new IssueEvent(EventType.IssueReopened, fresh, null, null, null, DateTimeOffset.UtcNow), ct);
}
```

Update the PollingService constructor + fields to accept and store an event channel:

```csharp
private readonly System.Threading.Channels.ChannelWriter<GHKanban.Core.Events.IssueEvent> _events;

public PollingService(
    IGitHubReader reader,
    IssueModelStore store,
    ConfigStore cfg,
    WebhookEventProcessor eventProcessor,
    ILogger<PollingService> log)
{
    _reader = reader;
    _store = store;
    _cfg = cfg;
    _events = eventProcessor.Writer;
    _log = log;
}
```

Add the appropriate `using` at top: `using GHKanban.Core.Events;`. The PollingServiceTests test from Task 11 will need an extra constructor arg — update the test:

```csharp
var processor = new WebhookEventProcessor(store, NullLogger<WebhookEventProcessor>.Instance);
var svc = new PollingService(reader, store, cfg, processor, NullLogger<PollingService>.Instance);
```

- [ ] **Step 2: Build and run tests**

```pwsh
dotnet test --no-restore
```

Expected: all green.

- [ ] **Step 3: Commit**

```pwsh
git add src/GHKanban.Sync/PollingService.cs tests/GHKanban.Sync.Tests/PollingServiceTests.cs
git commit -m "feat(sync): polling synthesises label/assignee/state delta events to trigger agents (spec §6)"
```

---

## Task 21: Pack as dotnet tool + smoke launch verification

**Files:**
- Modify: `src/GHKanban.Web/GHKanban.Web.csproj` (verify PackAsTool already set in Task 1)
- Smoke run

- [ ] **Step 1: Verify the csproj has tool metadata**

`src/GHKanban.Web/GHKanban.Web.csproj` should contain:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>ghkanban</ToolCommandName>
<PackageId>GHKanban</PackageId>
```

If anything is missing, add it.

- [ ] **Step 2: Pack**

```pwsh
dotnet pack src/GHKanban.Web/ -c Release -o ./artifacts
```

Expected: `./artifacts/GHKanban.0.1.0-alpha.nupkg` created.

- [ ] **Step 3: Install locally and smoke-run**

```pwsh
dotnet tool install -g --add-source ./artifacts GHKanban --prerelease
$env:GHKANBAN_PAT = "dummy"   # so the app starts without crashing on missing PAT
ghkanban &
Start-Sleep -Seconds 4
Invoke-WebRequest -Uri http://localhost:5454/ -UseBasicParsing | Select-Object StatusCode
```

Expected: StatusCode 200. The page may show "PAT not set / config errors" — that's correct since we used a dummy PAT and the example repo doesn't exist. The point is: the binary launched, served HTTP, and rendered the BoardPicker page.

Then stop the process:

```pwsh
Get-Process ghkanban | Stop-Process
dotnet tool uninstall -g GHKanban
```

- [ ] **Step 4: Commit any csproj fixes**

If you adjusted the csproj in Step 1:

```pwsh
git add src/GHKanban.Web/GHKanban.Web.csproj
git commit -m "chore(web): finalise dotnet tool packaging metadata (spec §9)"
```

Otherwise no commit needed.

---

## Task 22: Acceptance-criteria final pass

This is the v1 test gate against spec §12. For each criterion, perform the check and record pass/fail. Fix any failures inline before the final commit.

- [ ] **Criterion 1:** `dnx ghkanban` launches successfully on a clean machine with only .NET 10 SDK installed

Run (in a fresh shell):

```pwsh
$env:GHKANBAN_PAT = "dummy"
dnx GHKanban --prerelease &
Start-Sleep -Seconds 4
Invoke-WebRequest http://localhost:5454/ | Select-Object StatusCode
```

Expected: 200. (Note: requires NuGet to find the package; if local-only, use the artifacts path or skip this criterion and rely on Task 21's local install smoke test.)

- [ ] **Criterion 2:** First-run wizard creates `~/.ghkanban/` with all four template files

```pwsh
Remove-Item -Recurse -Force $env:USERPROFILE\.ghkanban -ErrorAction SilentlyContinue
ghkanban &
Start-Sleep -Seconds 3
Get-Process ghkanban | Stop-Process
ls $env:USERPROFILE\.ghkanban
ls $env:USERPROFILE\.ghkanban\boards
ls $env:USERPROFILE\.ghkanban\agents
```

Expected: `github.yaml`, `boards/example.yaml`, `agents/stub-ack.yaml` exist. Console output shows setup instructions.

- [ ] **Criterion 3:** With a valid PAT and `boards/example.yaml` referencing real repos, the Kanban view renders issues in correct columns

Manual test — point `example.yaml` at a real repo, set `GHKANBAN_PAT`, launch, visit `/board/example`. Confirm cards appear and are placed by rule.

- [ ] **Criterion 4:** Column placement is correct against published rule grammar

Already covered by `RuleEvaluatorTests` (Task 4). Confirm `dotnet test` passes.

- [ ] **Criterion 5:** Adding a label updates the card position within polling/webhook latency

Manual test — with the app running, add a label to a tracked issue. Within the poll interval (default 5m, set to 30s for testing) the card should move. Verify by polling the board page.

- [ ] **Criterion 6:** Stub agent posts comment on labelled issue

Manual test — configure `triggers: [{on: issue.labeled, when: has-label("ai-pls")}]` and add `ai-pls` to a tracked issue. Within 30s a comment should appear matching the documented format.

- [ ] **Criterion 7:** Editing YAML hot-reloads without restart

Manual test — edit `boards/example.yaml`, change a column name. Within ~1s the change should be visible at `/config`. (Existing ConfigWatcher unit tests prove the load path.)

- [ ] **Criterion 8:** Invalid YAML or unknown rule syntax surfaces clear error in `/config`

Manual test — break a YAML file (e.g. invalid indentation). Visit `/config`, confirm the Errors section lists the file and reason.

- [ ] **Criterion 9:** Reconciler runs every reconcile-interval

Verify via log output: `Reconciled {Count} issues for {Repo}` should appear at the configured interval.

- [ ] **Criterion 10:** All projects build clean with `dotnet build -warnaserror`; all tests pass

```pwsh
dotnet build -warnaserror
dotnet test
```

Expected: 0 errors, 0 warnings, all tests green.

- [ ] **Final commit (only if any fixes were made in this task)**

```pwsh
git add .
git commit -m "chore: v1 acceptance pass — all spec §12 criteria met"
```

If no fixes needed, skip the commit.

---

## Self-Review (run by plan author before save)

**1. Spec coverage:**

| Spec section | Implemented by |
|---|---|
| §1 Goal | All tasks combined |
| §2 Components | Tasks 2-19 build each component |
| §3 Repo structure | Task 1 |
| §4 Data model (config + rules + persistence) | Tasks 2, 3, 4, 5, 6, 7 |
| §5 GitHub integration | Tasks 8, 9, 11, 15 |
| §6 Trigger pipeline | Tasks 12, 13, 20 |
| §7 Agent runtime | Tasks 12, 13 |
| §8 UI | Tasks 14, 16, 17, 18, 19 |
| §9 Distribution | Tasks 14, 21 |
| §10 Non-goals | Not in any task (correctly) |
| §11 Tech constraints | Task 1 (versions), Tasks 2+ (xUnit MTP) |
| §12 Acceptance criteria | Task 22 |
| §13 Open questions for Slice B | Not in any task (correctly out of scope) |

No gaps.

**2. Placeholder scan:**

No "TBD", "TODO", "implement later", or "add appropriate error handling" patterns found. Code blocks contain real, runnable code (or as close as possible given some library API drift between training cutoff and 2026-05). The note in Task 12 about MAF API drift is a permitted plan-time disclosure (calls out a known risk and tells the subagent what to do), not a placeholder.

**3. Type / name consistency:**

Cross-task verification:
- `IssueView` properties used identically in Tasks 2, 3, 4, 8, 10, 11, 13, 17, 20 ✓
- `AgentRunResult.Status` / `Output` / `Error` consistent across Tasks 2, 13 ✓
- `TriggerSpec.On` / `When` consistent across Tasks 2, 5, 12, 13 ✓
- `ConfigStore.Current` / `OnChange` / `Set` consistent in Tasks 5, 6, 14, 16, 17, 19 ✓
- `IGitHubReader.ListIssuesAsync(repo, afterCursor, ct)` consistent across Tasks 8, 9, 11, 14, 17 ✓
- `IGitHubWriter.PostCommentAsync(repo, issueNumber, body, ct)` consistent across Tasks 8, 9, 13, 15 ✓
- `WebhookEventProcessor.Writer` (ChannelWriter) referenced consistently in Tasks 11, 15, 20 ✓
- `AgentDispatcher.DispatchAsync(ev, configs, ct)` consistent across Tasks 13, 15 ✓
- `IssueEvent` constructor positional args consistent across Tasks 2, 8, 12, 13, 20 ✓
- Project name capitalisation: `GHKanban.*` (capital GH, capital K) consistent throughout ✓

Clean.

**Total tasks:** 22. Each is a single commit. Smoke-runnable app after Task 14; useful UI from Task 16; agents working from Task 13 + 15; full v1 acceptance gate at Task 22.
