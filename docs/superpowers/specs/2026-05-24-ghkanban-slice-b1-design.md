# GHKanban v2 — Slice B1 — Container Runtime + First Containerised Agent

**Date:** 2026-05-24
**Owner:** James Burton
**Status:** Draft for review
**Supersedes:** Extends the Slice A design (`2026-05-23-ghkanban-slice-a-design.md`); does not replace it.

## 1. Goal

Move the agent execution path out-of-process. Replace the in-process `StubAcknowledgeAgent` pattern with **containerised agents** spawned per-trigger on Docker (or any Docker-API-compatible runtime). Ship one real agent — an LLM-powered issue summariser — to prove the infrastructure end-to-end and deliver tangible user value.

The slice deliberately scopes down to a single Docker-on-localhost runtime, a single shared GitHub PAT, and a single reference agent image. Multi-node mesh, GitHub-App-per-agent identity, real triage logic, agent memory, A2A communication, and Kubernetes are all **explicitly out of scope** (see §10) — they belong to later sub-slices that build on this foundation.

## 2. Architecture

```
┌─────────────────────── ghkanban (control plane) ────────────────────────┐
│                                                                         │
│  IssueEvent ─► AgentDispatcher                                          │
│                    │                                                    │
│                    ▼                                                    │
│           ContainerAgent (implements IGHKanbanAgent)                    │
│                    │                                                    │
│                    ▼                                                    │
│           IContainerRuntime  ◄──── DockerContainerRuntime (v1 impl)    │
│                    │                                                    │
└────────────────────┼────────────────────────────────────────────────────┘
                     │ Docker REST (unix socket / npipe / TCP)
                     ▼
            ┌────────────────┐
            │ Docker Engine  │  ← Podman exposes the same socket API
            └────────┬───────┘
                     │ spawns
                     ▼
            ┌──────────────────────────────────────────────────┐
            │ ghcr.io/jamesburton/ghkanban-agent:<version>     │
            │  ─ reads /skill/agent.yaml + /event.json         │
            │  ─ resolves IChatClient from `container.llm`     │
            │  ─ interpolates prompt + calls model             │
            │  ─ invokes github.post-comment tool              │
            │  ─ exits 0/non-zero                              │
            └──────────────────────────────────────────────────┘
```

**Lifecycle:** ephemeral. Each trigger → `create` → `start` → `wait` → ingest logs → `delete`. No per-agent long-running process. No service discovery. No port allocation. Concurrency-safe — Docker isolates each invocation.

**Existing trigger pipeline is unchanged.** Only the `IGHKanbanAgent` implementation moves out-of-process. `AgentRegistry`, `TriggerEvaluator`, `AgentDispatcher`, `AgentRunStore`, and the entire UI keep working exactly as in Slice A.

## 3. Repository structure (new projects)

```
src/
  GHKanban.ContainerRuntime/           NEW — interface + Docker REST adapter
    GHKanban.ContainerRuntime.csproj
    IContainerRuntime.cs               public interface (testable)
    DockerContainerRuntime.cs          HttpClient against docker socket / npipe
    DockerSocketLocator.cs             cross-platform socket path resolution
    ContainerSpec.cs                   value type: image, binds, env, resources, timeout
    ContainerRunResult.cs              value type: exit code, stdout, stderr
    ContainerJanitor.cs                BackgroundService — orphan cleanup
  GHKanban.Agents/                     EXISTING — gains ContainerAgent
    ContainerAgent.cs                  NEW — wraps IContainerRuntime, implements IGHKanbanAgent
    AgentRegistry.cs                   MODIFIED — resolves "container" implementation
  GHKanban.AgentImage/                 NEW — the agent runtime program
    GHKanban.AgentImage.csproj         packs nothing to NuGet; built by Docker
    Program.cs                         entrypoint: reads mounts, runs MAF agent, exits
    Dockerfile                         multi-stage; published to ghcr.io
    Config/
      SkillConfig.cs                   maps agent.yaml's container section
      PromptTemplate.cs                {{token}} interpolation
    Providers/
      ChatClientFactory.cs             switch on llm.provider → IChatClient
    Tools/
      IAgentTool.cs                    name + invoke signature
      GitHubPostCommentTool.cs         the one tool shipped in B1
  GHKanban.Web/                        EXISTING — DI changes for IContainerRuntime
    Program.cs                         MODIFIED — register DockerContainerRuntime
tests/
  GHKanban.ContainerRuntime.Tests/     NEW — fake runtime + DockerContainerRuntime smoke
  GHKanban.AgentImage.Tests/           NEW — prompt interpolation, ChatClientFactory, tool dispatch
```

**Dependency graph additions:**
- `GHKanban.ContainerRuntime` → `GHKanban.Core` (for the existing models referenced in container specs)
- `GHKanban.Agents` → `GHKanban.ContainerRuntime` (so `ContainerAgent` can call it)
- `GHKanban.Web` → `GHKanban.ContainerRuntime` (DI registration)
- `GHKanban.AgentImage` is standalone — no project refs into the main solution. It is its own deployable.

## 4. Agent definition (YAML schema)

`~/.ghkanban/agents/<id>.yaml` gains an `implementation: container` mode. The v1 in-process stub mode remains as `implementation: stub` (the existing path) for backward compatibility and tests.

```yaml
name: Issue Summariser
implementation: container          # stub (existing) | container (NEW)
triggers:
  - on: issue.opened
    when: not has-label("nosummary")

container:                         # required when implementation == container
  image: ghcr.io/jamesburton/ghkanban-agent:0.2.0
  llm:
    provider: anthropic            # none | anthropic | openai
    model: claude-sonnet-4-6
    api-key-env: ANTHROPIC_API_KEY # control plane reads → writes to secrets file → mounts
  prompt:
    system: ./files/system.md      # path relative to ~/.ghkanban/agents/<id>/
    user: |
      Summarise this GitHub issue in 2-3 sentences. Focus on what the user wants and why.

      Title: {{issue.title}}
      Body: {{issue.body}}
  tools:                            # explicit allowlist; everything else denied
    - github.post-comment
  timeout: 60s                      # container killed if exceeded
  resources:                        # optional; defaults shown
    cpu: 1
    memory: 512m
```

**Template interpolation tokens (v1):**
- `{{issue.title}}`, `{{issue.body}}`, `{{issue.labels}}` (comma-joined), `{{issue.repo}}`, `{{issue.number}}`, `{{issue.html_url}}`, `{{issue.assignees}}` (comma-joined)
- `{{trigger.event}}`, `{{trigger.rule}}`, `{{trigger.agent_name}}`
- `{{run.id}}` — the correlation id

Straight string substitution against the rendered values. No conditionals, no loops, no Turing-completeness. Richer prompt content (instructions, few-shot examples, tone guidance) belongs in the `prompt.system` file, which has no token-budget constraints from us.

**`provider: none`** is a v1-supported short-circuit: when set, the agent skips the LLM call entirely and uses the rendered `prompt.user` text directly as the comment body. Useful for templated notifiers, smoke-testing container infrastructure without burning LLM tokens, and reproducing the Slice A stub semantics in container mode.

**Tools shipped in B1:** `github.post-comment` only. Posts a comment on the triggering issue with the agent's final text output (or with an explicit `body` argument if the LLM tool-calls it directly).

**Tool registration is gated.** Anything not in the agent's `tools:` allowlist is unavailable to the model — the tool registry filters by name before exposing to MAF.

## 5. Mounts, env vars, runtime contract

**Mounts (read-only unless noted):**

| Container path | Source on host | Purpose |
|---|---|---|
| `/skill/agent.yaml` | `<configRoot>/agents/<id>.yaml` | Agent definition |
| `/skill/files/` | `<configRoot>/agents/<id>/` (if directory exists) | Free-form skill files referenced by prompts |
| `/event.json` | `<runDir>/event.json` (tmpfs, fresh per invocation) | The IssueEvent + IssueContext for this run |
| `/secrets/github-pat` | `<configRoot>/secrets/github-pat` (mode 0600) | Auth for github.* tools |
| `/secrets/llm-api-key` | `<configRoot>/secrets/<container.llm.api-key-env>` (mode 0600) | Auth for the LLM provider |

**Env vars per invocation:**
- `GHKANBAN_RUN_ID` — correlation id for log lines + activity feed
- `GHKANBAN_GH_REPO`, `GHKANBAN_GH_ISSUE` — convenience for the GitHub-post tool
- `GHKANBAN_LOG_FORMAT=json` — structured JSON-per-line logs on stdout

**Network:** `--network bridge` (full outbound) in B1, configurable later. A `network: none` knob is *recognised* in the YAML schema but defaults to bridge; v1 doesn't ship an LLM-provider allowlist resolver.

**Exit-code contract:** `0` = success. Non-zero = failure (stderr captured as `AgentRunResult.Error`). Always written to `agent_runs`.

**Stdout parsing:** every line captured. Lines that are valid JSON containing an `"event"` field are treated as structured control signals (`event="complete"` carries the final result; `event="error"` carries a structured failure). All other lines are kept as a free-form log excerpt and surfaced in `/activity`. Agents are free to log progress as plain text; only the terminal `event="complete"` line is contractually required on success.

## 6. Spawn flow (control plane → Docker → cleanup)

`ContainerAgent.TriggerAsync(IssueContext)`:

1. Generate `runId = Guid.NewGuid().ToString("N")`.
2. Write `event.json` to `<tempRoot>/ghkanban/runs/<runId>/event.json`.
3. `POST /containers/create` with `ContainerSpec` built from `agent.container` config; label container `ghkanban=true`, `ghkanban.agent=<id>`, `ghkanban.run-id=<runId>`.
4. `POST /containers/{id}/start`.
5. `POST /containers/{id}/wait?condition=not-running` with HTTP client timeout = `agent.container.timeout + 5s` grace.
6. On timeout → `POST /containers/{id}/kill` and record `AgentRunStatus.Failed`.
7. `GET /containers/{id}/logs?stdout=1&stderr=1` — parse JSON-per-line on stdout; collect stderr as a single string.
8. `DELETE /containers/{id}?force=true`.
9. Delete `<runDir>` (the tmpfs event file is no longer needed).
10. Return `AgentRunResult(Status, Output=last-stdout-json-line, Error=stderr-or-null)`.

`AgentDispatcher.DispatchAsync` (existing) then calls `AgentRunStore.RecordAsync` with the result, just as it does for the in-process stub today. No changes to the dispatcher logic.

`ContainerJanitor` (new `BackgroundService`) — every 5 minutes:
- `GET /containers/json?all=true&filters={"label":["ghkanban=true"]}` 
- For each container in state `exited` AND finished `> 30m` ago AND not currently tracked by an active run: `DELETE`.
- Defensive against control-plane crashes leaving orphans.

## 7. Inside the container (`GHKanban.AgentImage` entrypoint)

```
1. Read /skill/agent.yaml, /event.json
2. Read /secrets/github-pat, /secrets/llm-api-key
3. ChatClientFactory.Create(config.Llm) → IChatClient
4. PromptTemplate.Render(config.Prompt.User, eventJson) → user text
5. Load config.Prompt.System (if path set) from /skill/files/...
6. Build a MAF ChatAgent with:
     - IChatClient from (3)
     - System prompt from (5)
     - Tool registry filtered by config.Tools allowlist; each tool's invoke
       function uses /secrets/github-pat for github.* tools
7. agent.RunAsync(userText, cancellationToken=timeoutFromYaml)
8. If the model invoked github.post-comment as a tool call → tool handled it
9. Else → call github.post-comment ourselves with the model's text response
10. Emit final structured stdout line: {"event":"complete","run_id":"...","comment_url":"..."}
11. Exit 0
   (On exception: emit {"event":"error","message":"..."} on stderr, exit 1)
```

The entrypoint is intentionally thin. All variation lives in the YAML + skill files.

## 8. LLM provider abstraction

```csharp
// GHKanban.AgentImage/Providers/ChatClientFactory.cs
public static IChatClient? Create(LlmConfig cfg, string apiKey) => cfg.Provider switch
{
    "none"      => null,   // caller short-circuits the model invocation; uses rendered prompt as output
    "anthropic" => new Anthropic.SDK.AnthropicClient(apiKey).Messages.AsChatClient(cfg.Model),
    "openai"    => new OpenAI.OpenAIClient(apiKey).GetChatClient(cfg.Model).AsIChatClient(),
    _ => throw new InvalidOperationException($"Unknown llm.provider: {cfg.Provider}")
};
```

**Shipped in v1's image:** `none`, `anthropic`, `openai`. Azure OpenAI is intentionally deferred — see §11.

**Adding a new provider** is a one-NuGet + one-switch-arm change inside `GHKanban.AgentImage`, then republishing the agent image. We accept that providers are baked into the image (vs runtime-pluggable) because the alternative would force users to build custom Dockerfiles to add provider SDKs — defeating the "edit YAML, no Dockerfile" ergonomic.

**Custom images:** users can `FROM ghcr.io/jamesburton/ghkanban-agent:0.2.0` to add SDKs in their own published image, and reference that in `container.image`. This is documented but not first-class supported in B1.

## 9. Secret handling + trust model (v1)

| Secret | Source on host | Reaches container as |
|---|---|---|
| **GitHub PAT** | Env var named in `github.yaml/auth.pat-env` (existing) | Control plane writes to `<configRoot>/secrets/github-pat` (mode 0600) on first launch; mounted at `/secrets/github-pat:ro`. |
| **LLM API key** | Env var named in each agent's `container.llm.api-key-env` | Control plane writes to `<configRoot>/secrets/<env-name>` (mode 0600) on first launch + on config change; mounted per-agent at `/secrets/llm-api-key:ro`. |

**Why files not env vars:** Docker env vars leak via `docker inspect` and process listings; mounted files don't. Also positions us for B2+B4 (GH-App-per-agent + proper OS secret store).

**Trust model — documented loudly in the README and the wizard:**
- All container-mode agents share one GitHub PAT (full `repo` scope).
- LLM keys are per-agent but readable by anyone with FS access to `~/.ghkanban/secrets/`.
- `~/.ghkanban/` directory mode `0700`; secrets mode `0600` (enforced at first-launch wizard).
- Windows: ACL the user as exclusive owner via `System.IO.FileSystemAclExtensions`.
- B2 (GitHub-App-per-agent) replaces shared PAT with per-agent installation tokens.
- B4 (memory layer) brings OS-native secret stores (DPAPI / libsecret / Keychain).

## 10. Build + publish (CI additions)

**Two new artifacts** alongside the existing `GHKanban` NuGet:

| Artifact | Where | When |
|---|---|---|
| `ghcr.io/jamesburton/ghkanban-agent:<version>` | GitHub Container Registry | On `v*` tag |
| `ghcr.io/jamesburton/ghkanban-agent:latest` | GHCR | When the tagged version is the highest |

**New `publish-image` job** in `.github/workflows/ci.yml`:

```yaml
publish-image:
  needs: build
  if: startsWith(github.ref, 'refs/tags/v')
  runs-on: ubuntu-latest
  permissions:
    contents: read
    packages: write
  steps:
    - uses: actions/checkout@v4
    - uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}
    - uses: docker/setup-qemu-action@v3
    - uses: docker/setup-buildx-action@v3
    - uses: docker/build-push-action@v6
      with:
        context: ./src/GHKanban.AgentImage
        push: true
        tags: |
          ghcr.io/jamesburton/ghkanban-agent:${{ github.ref_name }}
          ghcr.io/jamesburton/ghkanban-agent:latest
        platforms: linux/amd64,linux/arm64
```

**Dockerfile (multi-stage):**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "GHKanban.AgentImage.dll"]
```

GHCR images are public — anyone running the tool can pull without auth.

## 11. Non-goals for B1 (explicit; cleanly defer)

| ❌ Non-goal | Why deferred | Lands in |
|---|---|---|
| GitHub-App-per-agent identity | Shared PAT works for v1; App registration UX needs its own brainstorm | B2 |
| Real triage agent | Summariser is the demo agent; triage is a separate logic problem | B3 |
| Tailscale mesh / multi-node | Single-node only in B1 | B4 |
| Persistent memory volumes / vector store | Each container is fresh; no cross-run state | B5 |
| A2A agent communication | One agent per trigger; no inter-agent calls | B6 |
| k3s / Kubernetes / cluster mode | Docker Engine API only via `IContainerRuntime`; new impl can land later | B7 |
| Network egress allowlist | Container gets `--network bridge`; allowlist is a config knob added later | hardening pass |
| Custom-image first-class support (FROM ghkanban-agent) | Documented as possible but no built-in registry / UX | as needed |
| Warm container pools | Ephemeral suffices until latency complaints arrive | as needed |
| Agent image hash pinning (`@sha256:...`) for supply-chain integrity | Use the tag, trust GHCR for v1 | hardening pass |
| Tools beyond `github.post-comment` | `add-label`, `assign-user`, `find-similar-issues`, MCP custom tools | B3+ |
| Aspire in the runtime path | Same reason as Slice A: breaks dnx zero-install | not planned |
| Azure OpenAI provider | YAGNI for v1; Anthropic + OpenAI cover the primary use cases. Add when a real need surfaces. | as needed |

## 12. Technology constraints (inherited)

Same as Slice A spec §11, plus:
- **Container runtime:** Docker Engine REST API (Docker Desktop, Docker Engine, or Podman with `podman system service`). No hard k3s/Kubernetes dependency.
- **Container registry:** GitHub Container Registry (ghcr.io) — public images, no auth required to pull.
- **Image OS base:** `mcr.microsoft.com/dotnet/runtime:10.0-alpine` — small footprint, ARM64 + AMD64 multi-arch.
- **LLM SDKs:** `Anthropic.SDK` + `OpenAI` baked into the agent image. `Microsoft.Extensions.AI.IChatClient` as the abstraction.

## 13. Acceptance criteria for B1

1. With `implementation: container` + `container.llm.provider: none` set on `stub-ack.yaml`, the trigger pipeline runs the agent in a Docker container (not in-process), the rendered `prompt.user` template is posted as a comment, and the round-trip completes within 30s.
2. A new `summariser.yaml` with `container.llm.provider: anthropic` + the API key env var set produces a 2-3 sentence summary comment on a real GitHub issue within 60s.
3. Container `agent_runs` entries appear in `/activity` with the run id, status, and a captured log excerpt (last stdout JSON line or stderr).
4. After several invocations, `docker ps -a --filter label=ghkanban=true` shows no orphan containers older than 30m (Janitor working).
5. Container timeout works: if an agent's `timeout` is exceeded, the container is killed, `agent_runs` shows `Failed` with a "killed by timeout" error, and the next trigger still runs cleanly.
6. The agent image builds for `linux/amd64` and `linux/arm64`, publishes to GHCR on tag, and is publicly pullable (`docker pull ghcr.io/jamesburton/ghkanban-agent:<version>` works without auth).
7. `IContainerRuntime` is a clean interface — `tests/GHKanban.Agents.Tests/ContainerAgentTests.cs` uses a fake runtime that implements it without Docker references.
8. All Slice A acceptance criteria (spec §12) still pass.
9. `dnx GHKanban --prerelease` still works on a clean machine. Container-mode agents additionally require Docker installed; absent Docker, the control plane logs a clear error and the agent reports `Failed`, but the control plane itself does not crash.
10. Full build clean (`dotnet build -warnaserror`), all tests pass under MTP runner.
11. Spawning N triggers in parallel (N=10) produces N concurrent containers (Docker isolated), N `agent_runs` records, and N comments — no race conditions in spawn/log/cleanup.

## 14. Open questions to revisit before B2+

- **GitHub App per agent:** what's the registration UX? Auto-create via GH GraphQL? Manual install link with QR code? Per-org vs per-repo install model?
- **Per-agent secrets:** today every agent reads the same `/secrets/github-pat`. With GH Apps that becomes per-agent installation tokens — does the secret mount path change?
- **MCP tools:** B3+ tools may be MCP servers (e.g. internal docs lookup, JIRA bridge). What's the registration + auth model?
- **Streaming output:** today the agent waits for the full model response before posting. Should we stream the comment as the model generates? (Useful for long agents.)
- **Tailscale (B4):** when agents distribute across nodes, does the control plane spawn them locally and let Tailscale route the events, or does it spawn them on the target node via remote Docker socket?
- **Pricing visibility:** the summariser will burn LLM tokens per issue. Should the activity feed show estimated cost per run?

These do not block B1 but their answers shape B2-B7 and are worth surfacing in the relevant brainstorming.
