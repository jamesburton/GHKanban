# GHKanban v1 — Slice A + Stubbed Agent — Design Spec

**Date:** 2026-05-23
**Owner:** James Burton
**Status:** Draft for review
**Supersedes survey conclusion:** The survey (`README.md`) recommended building for the OSS-Maintainer tier and called the build verdict at other tiers Marginal-or-better. With the user's vision for a GitHub-Issues-based Kanban + agent-mesh orchestrator added to the requirements, the build verdict is **Justified across all four tiers** — this design is the first slice toward that vision.

## 1. Goal

Ship a `dnx`-installable, single-user, BA/Manager-friendly Kanban view over GitHub Issues, with a stubbed Microsoft Agent Framework (MAF) agent that proves the full **trigger → agent → GitHub-write** loop end-to-end.

The first slice deliberately scopes down to a single in-process .NET app to nail:
1. The GitHub integration layer (read, write, webhook, polling, reconciliation)
2. The configurable board/column/rule model
3. The agent abstraction (MAF) and trigger pipeline

Container-based agent runtime, agent mesh networking, A2A communication, shared-memory volumes, and multi-tenant auth are all explicitly **out of scope for v1** (see §10) — they belong to later slices that build on this foundation.

## 2. Architecture

Single .NET 10 process. Vanilla `Host.CreateApplicationBuilder` (no Aspire in the runtime path; an optional dev-time `AppHost` project may be added later for orchestrating real container agents). Packaged as a `dotnet tool`; primary invocation is `dnx ghkanban`.

### Components

| Component | Responsibility | Tech |
|---|---|---|
| **Blazor Server UI** | Kanban view, board navigation. v1 placement is read-only (columns derived from rules; no drag-and-drop) | Blazor Server + SignalR |
| **GitHub adapter** | GraphQL reads (issue lists, labels, assignees); REST writes (comments, labels, assignment); webhook receiver | Octokit.NET + custom GraphQL queries |
| **Sync engine** | Webhook-when-available, polling fallback, periodic reconciler | Hosted background services |
| **Rule evaluator** | Maps `IssueView` records to columns based on declarative rules | In-process expression eval |
| **Trigger dispatcher** | Watches issue events, applies trigger rules, hands off to agent runtime | In-process `Channel<T>` + handler pipeline |
| **Agent runtime** | MAF `AIAgent` host; v1 hosts the stub agent inline | Microsoft.Agents.AI |
| **Config loader** | Reads `~/.ghkanban/config/*.yaml`, hot-reloads on file change | `FileSystemWatcher` + `YamlDotNet` |
| **Persistence** | Ephemeral state (last-synced cursors, agent run history). **Not** config — config lives in YAML | SQLite via `Microsoft.Data.Sqlite` |

### Dependency posture

Zero non-.NET runtime dependencies. Everything in-process, single binary. The complete dependency list for v1:

- .NET 10 runtime (provided by `dnx` host)
- NuGet: `Microsoft.AspNetCore.App` (Blazor Server), `Octokit`, `Microsoft.Agents.AI`, `YamlDotNet`, `Microsoft.Data.Sqlite`
- File system: `~/.ghkanban/` for config + SQLite DB

No Docker, Postgres, Redis, message queue, or external service required to run v1.

## 3. Repository structure

```
src/
  GHKanban.Core/          domain models (Board, Column, Rule, IssueView, TriggerSpec)
  GHKanban.GitHub/        Octokit adapter, GraphQL queries, webhook receiver
  GHKanban.Sync/          webhook + polling + reconciler hosted services
  GHKanban.Rules/         rule grammar + evaluator
  GHKanban.Agents/        MAF agent abstractions, stub implementation, dispatcher
  GHKanban.Config/        YAML loader + hot-reload
  GHKanban.Web/           Blazor Server UI + dotnet-tool entry point
tests/
  GHKanban.Core.Tests/
  GHKanban.GitHub.Tests/
  GHKanban.Sync.Tests/
  GHKanban.Rules.Tests/
  GHKanban.Agents.Tests/
  GHKanban.Config.Tests/
  GHKanban.Web.Tests/
```

Each project is its own NuGet-able assembly. `GHKanban.Web` is the only one packaged as a `dotnet tool`; it references the others.

## 4. Data model

### Config (YAML, file-based)

Three config file types under `~/.ghkanban/`:

#### `github.yaml` (one file, app-wide)

```yaml
auth:
  pat-env: GHKANBAN_PAT     # PAT read from env var at startup
webhook:
  public-url: https://my-tailnet.ts.net/ghkanban/hook    # optional; if absent, polling-only
  secret-env: GHKANBAN_WEBHOOK_SECRET                    # required if public-url set
poll-interval: 5m
reconcile-interval: 30m
```

#### `boards/<board-id>.yaml` (one file per board)

```yaml
name: My Board
scope:
  repos: [myorg/foo, myorg/bar]            # explicit repos
  orgs: []                                  # OR all-repos-in-org
  filters:
    state: open                             # default filter applied to all columns
columns:
  - name: Inbox
    rule: not has-label("triage") and not has-label("in-progress")
  - name: Triage
    rule: has-label("triage")
  - name: In Progress
    rule: has-label("in-progress") or assignee-of-mine
  - name: Stale
    rule: state == "open" and age-days > 30
```

Column matching is **first-match-wins** in column order. An issue not matched by any column is excluded from the board.

#### `agents/<agent-id>.yaml` (one file per agent registration)

```yaml
name: Stub Acknowledger
implementation: GHKanban.Agents.StubAcknowledgeAgent    # fully-qualified .NET type
triggers:
  - on: issue.labeled
    when: label == "ai-pls"
  - on: issue.assigned
    when: assignee == "ghkanban-bot"        # placeholder for Slice B bot identity
```

### Rule grammar (v1)

Declarative, no Turing-completeness. Supported expressions:

- **Predicates:** `has-label("X")`, `assignee == "name"`, `assignee-of-mine`, `state == "open"|"closed"`, `age-days > N`, `age-days < N`, `milestone == "X"`, `repo == "owner/name"`
- **Boolean composition:** `and`, `or`, `not`, parentheses
- **Literals:** string (double-quoted), integer

Anything outside this grammar is a config error caught at load time with a clear diagnostic (file, line, column).

### Persistence (SQLite, ephemeral state)

| Table | Purpose |
|---|---|
| `sync_cursor` | Per-repo last-synced GraphQL cursor + ETag for incremental polling |
| `webhook_events` | Inbound webhook event log (last 7 days, for dedup and replay) |
| `agent_runs` | Agent invocation history (trigger, agent, started-at, finished-at, status, output) |

SQLite file path: `~/.ghkanban/state.db`. Migrations via simple SQL scripts shipped with the assembly.

## 5. GitHub integration

### Auth (v1)

- Single PAT read from environment variable named in `github.yaml/auth.pat-env`
- App acts AS the user (no separate bot identity in v1)
- Required PAT scopes: `repo` (read/write issues, post comments, manage labels). For org-wide reads also `read:org`.
- Trigger configs that reference `assignee == "ghkanban-bot"` are valid syntax but match nothing in v1 (the bot identity arrives in Slice B). They are not config errors — they're forward-compatible.

### Reads (GraphQL)

- Per-repo issue listings via batched GraphQL queries (`issues(first: 100, after: $cursor)`)
- Cursor + ETag stored in SQLite for incremental polling
- One query per board refresh; pagination handled internally

### Writes (REST via Octokit)

- Post comment, add/remove label, assign user, change state (close/reopen)
- All writes are explicit user actions or agent-triggered side-effects; the board view itself never writes spontaneously

### Sync engine

Three concurrent loops:

| Loop | Trigger | Action |
|---|---|---|
| **Webhook receiver** | Inbound HTTP POST to `/hook` | Validate signature against `webhook.secret-env`; enqueue event; update affected `IssueView` in-memory; push to UI via SignalR |
| **Polling loop** | Timer (`poll-interval`, default 5m) | Per repo: fetch issues updated since last cursor; merge into in-memory model; push to UI |
| **Reconciler** | Timer (`reconcile-interval`, default 30m) | Full sweep of in-scope repos; reconcile any drift from missed events or webhook drops |

The webhook receiver and polling loop **both** update the same in-memory model; the polling loop is no-op when webhooks are working (cursor doesn't advance unless GH returns changes). The reconciler catches anything else.

If `webhook.public-url` is absent in `github.yaml`, the webhook receiver still runs (binds to `/hook`) but the app does not auto-register webhooks on GitHub; polling + reconciler carry the freshness story alone.

## 6. Trigger pipeline

```
GitHub webhook → IssueEvent → Trigger evaluator → matching Agent registrations →
  Agent dispatcher → MAF AIAgent.RunAsync(issueContext) → agent action (e.g. post comment) →
  agent_runs table updated → UI activity feed reflects run
```

Event types in v1: `issue.opened`, `issue.labeled`, `issue.unlabeled`, `issue.assigned`, `issue.unassigned`, `issue.closed`, `issue.reopened`, `issue_comment.created`.

Trigger matching is **independent-per-agent**: when an event arrives, every registered agent's triggers are evaluated; any agent whose `when` clause matches is dispatched. Multiple agents can fire on the same event. (Contrast with column matching in §4, which is first-match-wins because an issue has exactly one column.)

Agent runs are **best-effort, fire-and-forget** in v1 — no retry on failure, no dead-letter queue, no rate limiting beyond GitHub's own. The `agent_runs` table records outcomes for visibility; failed runs surface in the UI activity feed.

## 7. Agent runtime

### Abstractions

v1 ships an interface `GHKanban.Agents.IGHKanbanAgent` that wraps `Microsoft.Agents.AI.AIAgent` and adds a `Trigger(IssueContext) → Task<AgentRunResult>` method. All agent implementations in v1 implement this interface; the underlying `AIAgent` provides:
- Tool registration (MAF's tool model, MCP-compatible)
- Thread/conversation state (`AgentThread`)
- Multi-agent orchestration primitives for Slice C

The `implementation` field in `agents/*.yaml` is the fully-qualified .NET type name. The Config loader resolves the type via `Type.GetType` against assemblies in the app's load context. v1 only loads types from `GHKanban.Agents` (the built-in assembly); v2 will support `dotnet add package` drop-in of third-party agent assemblies.

### Stub agent (`GHKanban.Agents.StubAcknowledgeAgent`)

Behaviour on trigger: post a comment on the triggering issue with format:

```
🤖 GHKanban: agent **{agent-name}** triggered by `{trigger-event}` (rule: `{matching-rule}`).
This is a stub acknowledgement — no other action taken.
```

Run outcome recorded in `agent_runs`. This proves end-to-end:
- Trigger pipeline wired correctly
- Agent dispatch works
- GitHub write path works
- MAF abstraction works (the stub IS a real MAF agent, not a special case)
- UI activity feed reflects the run

The stub is intentionally trivial in logic but exercises every layer.

## 8. UI (Blazor Server, v1 scope)

### Pages

- `/` — board picker (lists boards defined in `~/.ghkanban/boards/`)
- `/board/{board-id}` — Kanban view
- `/activity` — agent run activity feed (chronological list of `agent_runs` entries)
- `/config` — read-only view of loaded config (boards, agents, GH settings) with config-error diagnostics

### Kanban view behaviour

- Columns rendered left-to-right in YAML order
- Issues rendered as cards within their matched column; click opens GitHub link in new tab
- Issue card shows: title, number, repo, labels, assignee avatars, age, last-activity time
- **No drag-and-drop in v1** — issue placement is purely rule-derived. (To move an issue between columns, user adds/removes labels in GitHub directly; webhook reflects the change.)
- Real-time updates via SignalR push when sync engine receives an event

### Navigation

Top bar: app name, board picker dropdown, link to `/activity`, link to `/config`, `Reload config` button (triggers hot-reload manually if file watching missed something).

## 9. Distribution and runtime

### Packaging

- Solution publishes `GHKanban.Web` as a `dotnet tool` (NuGet package id `GHKanban`)
- Auto-version + auto-publish from CI on `main` push (semantic-versioned tags drive releases)
- Tool entry point: `ghkanban` command

### Invocation

- Primary: `dnx ghkanban` (zero-install, .NET 10+ users)
- Secondary: `dotnet tool install -g GHKanban` then `ghkanban` (pinned local copy)

### First-run wizard

If `~/.ghkanban/` doesn't exist on launch:
1. Create directory
2. Write `github.yaml` template with comments explaining each field
3. Write `boards/example.yaml` (a sample board referencing `your-org/your-repo`)
4. Write `agents/stub-ack.yaml` (the stub agent registration, disabled by default)
5. Print to console: setup instructions, PAT env var to set, URL to visit
6. Launch Blazor app; the `/config` page surfaces "please configure GitHub PAT" until valid

### Listening

- Binds to `http://localhost:5454` by default (port configurable via `--port` or `GHKANBAN_PORT` env)
- Webhook endpoint `/hook` is bound regardless; users who configure Tailscale Funnel point it at this app

## 10. Out of scope for v1 (explicit non-goals)

- ❌ Aspire in the runtime path (Aspire AppHost may be added as a dev-only project for Slice B+; v1 ships without it)
- ❌ Multi-user auth / GitHub OAuth (localhost/tailnet-bound, single-user)
- ❌ Container-based agent runtime (the stub runs inline; container path is Slice B)
- ❌ Drag-and-drop column moves (placement is rule-derived only)
- ❌ GitHub-App-per-agent / bot identities (start as-user; bot identities arrive with Slice B)
- ❌ Tailscale auto-setup / Funnel provisioning (consumed via user-supplied URL only)
- ❌ Agent memory layer / shared volumes / NFS
- ❌ Inter-agent (A2A) communication
- ❌ Cluster mode / leader election (data model is cluster-ready, runtime is single-node)
- ❌ UI for editing config (config is YAML files only in v1; `/config` page is read-only)
- ❌ Issue creation/editing from the UI (read views only; mutations are via agent triggers)
- ❌ Migrations from external Kanban tools

## 11. Technology constraints (inherited from survey spec §9)

- **Runtime:** .NET 10 (C#)
- **Test framework:** xUnit v3 with Microsoft Testing Platform (MTP)
- **Packaging:** `dotnet tool` published to NuGet, auto-versioned from CI
- **Primary invocation:** `dnx ghkanban`; `dotnet tool install -g` as secondary
- **Non-.NET dependencies:** **none** in v1 (intentional, to keep `dnx` zero-install viable)
- **AI orchestration:** Microsoft Agent Framework (MAF) as the agent abstraction
- **GitHub auth:** PAT via env var in v1; GitHub-App-per-agent in Slice B; `gh` CLI token discovery as a convenience fallback (planned, not v1-blocking)
- **UI:** Blazor Server (SignalR makes real-time webhook updates trivial; single-node deployment suits the architecture)
- **Persistence:** SQLite for ephemeral state only; config is file-based YAML

## 12. Acceptance criteria for v1

1. `dnx ghkanban` launches successfully on a clean machine with only .NET 10 SDK installed; no other prerequisites.
2. First-run wizard creates `~/.ghkanban/` (with `boards/` and `agents/` subdirectories) plus three template files — `github.yaml`, `boards/example.yaml`, `agents/stub-ack.yaml` — and prints setup instructions.
3. With a valid PAT and `boards/example.yaml` referencing real repos, the Kanban view at `/board/example` renders all in-scope issues, placed in their first-matching column.
4. Column placement is correct against the published rule grammar — a curated test set of issues placed in expected columns.
5. Adding a label to an issue in GitHub updates the corresponding card's position on the board within `poll-interval` (default 5m) without webhook, or within 5s with webhook configured.
6. Configuring the stub agent's trigger (`on: issue.labeled, when: has-label("ai-pls")`) and adding that label to an issue results in the stub posting a comment on the issue within 30s, with the comment text matching the documented format.
7. Editing any YAML config file is reflected in the running app without restart (hot-reload).
8. Invalid YAML or unknown rule syntax produces a clear error in `/config` view with file path + line/column info; the rest of the app continues running on the last-valid config.
9. Reconciler runs every `reconcile-interval` and surfaces any reconciled state in the activity feed.
10. All projects build clean with no warnings under `dotnet build -warnaserror`; all tests pass under xUnit v3 MTP.

## 13. Open questions to revisit before Slice B

- Container runtime: Docker socket vs Podman vs containerd? Decide before Slice B.
- Tailscale automation: out-of-scope for v1, but Slice B should consider Tailscale Funnel provisioning helpers.
- GitHub App registration UX: each agent needs an App; how is creation/install streamlined?
- Memory layer: SQLite-backed embeddings vs filesystem + vector index vs MAF's own memory abstractions?
- A2A protocol selection: MAF's own multi-agent primitives, Google A2A, or MCP-based?

These do not block v1 but their answers shape Slice B's design — surface them in Slice B's brainstorming.
