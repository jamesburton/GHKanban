# GHKanban Slice B1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move agent execution out-of-process by spawning ephemeral Docker containers per trigger. Replace the in-process stub pattern with a containerised agent image that can run a `provider: none` template stub OR a real LLM-powered summariser. Single-node only; multi-node mesh, GitHub-App-per-agent, real triage logic and memory are all later sub-slices.

**Architecture:** Add `GHKanban.ContainerRuntime` (interface + DockerContainerRuntime via Docker REST) and `GHKanban.AgentImage` (a separately-deployable .NET 10 console app published as `ghcr.io/jamesburton/ghkanban-agent:<version>`). Existing `IGHKanbanAgent` gets a `ContainerAgent` implementation that translates `IssueContext` into a container spec and shells out via `IContainerRuntime`. Existing trigger pipeline (dispatcher, run store, UI) is unchanged.

**Tech Stack:** .NET 10 · xUnit v3 + MTP · Docker.DotNet (REST adapter) · Microsoft.Extensions.AI · Anthropic.SDK · OpenAI · YamlDotNet · Octokit (in agent image for `github.post-comment`) · `mcr.microsoft.com/dotnet/runtime:10.0-alpine` (image base) · GitHub Container Registry · multi-arch buildx (amd64+arm64).

**Source spec:** `docs/superpowers/specs/2026-05-24-ghkanban-slice-b1-design.md` — every task implements one slice of that spec.

---

## File structure (locked in here)

```
src/
  GHKanban.ContainerRuntime/                 NEW
    GHKanban.ContainerRuntime.csproj
    IContainerRuntime.cs                     interface
    DockerContainerRuntime.cs                Docker REST impl via Docker.DotNet
    DockerSocketLocator.cs                   cross-platform socket URI
    ContainerSpec.cs                         value type: image, mounts, env, resources, timeout
    ContainerRunResult.cs                    value type: exit code, stdout, stderr
    ContainerJanitor.cs                      BackgroundService — orphan cleanup
  GHKanban.AgentImage/                       NEW (separate deployable)
    GHKanban.AgentImage.csproj               <IsPackable>false</IsPackable>
    Dockerfile                               multi-stage; published to ghcr.io
    .dockerignore
    Program.cs                               entrypoint
    Config/
      SkillConfig.cs                         POCOs for /skill/agent.yaml
      SkillConfigLoader.cs                   YAML parsing (image-local, no dep on GHKanban.Config)
      PromptTemplate.cs                      {{token}} interpolation
    Providers/
      ChatClientFactory.cs                   none | anthropic | openai
    Tools/
      IAgentTool.cs
      GitHubPostCommentTool.cs
      ToolRegistry.cs                        builds allowlist-filtered registry
  GHKanban.Core/                             EXISTING
    Models/AgentConfig.cs                    MODIFIED — add optional ContainerSpec (control-plane facing)
  GHKanban.Config/                           EXISTING
    YamlConfigLoader.cs                      MODIFIED — parse new container section
  GHKanban.Agents/                           EXISTING
    ContainerAgent.cs                        NEW — IGHKanbanAgent wrapping IContainerRuntime
    AgentRegistry.cs                         MODIFIED — case for implementation: container
  GHKanban.Web/                              EXISTING
    Program.cs                               MODIFIED — register IContainerRuntime, ContainerJanitor, secret-file plumbing
    FirstRunWizard.cs                        MODIFIED — emit secrets dir with correct permissions
tests/
  GHKanban.ContainerRuntime.Tests/           NEW
  GHKanban.AgentImage.Tests/                 NEW
.github/workflows/ci.yml                     MODIFIED — add publish-image job
```

**Dependency edges added:**
- `GHKanban.ContainerRuntime` → `GHKanban.Core` (uses no Core types directly; ref for symmetry)
- `GHKanban.Agents` → `GHKanban.ContainerRuntime`
- `GHKanban.Web` → `GHKanban.ContainerRuntime`
- `GHKanban.AgentImage` → **nothing in the main solution** (deliberate: it's a separately-deployable image; duplicates ~30 lines of POCOs + YAML parsing rather than coupling)

---

## Conventions used in this plan

**Commits:** one per task. Message format `feat(<scope>):` or `chore(<scope>):` matching Slice A.

**Tests:** xUnit v3 MTP. Run via `dotnet run --project tests/<Name>.Tests/` (NOT `dotnet test` — MTP runner). Use `TestContext.Current.CancellationToken` for async tests.

**Async:** `async Task` everywhere I/O is involved. Pass `CancellationToken` through.

**Branch:** all commits on `main`.

---

## Task 1: Scaffolding — new projects + package versions

**Files:**
- Create: `src/GHKanban.ContainerRuntime/GHKanban.ContainerRuntime.csproj`
- Create: `src/GHKanban.AgentImage/GHKanban.AgentImage.csproj`
- Create: `tests/GHKanban.ContainerRuntime.Tests/GHKanban.ContainerRuntime.Tests.csproj`
- Create: `tests/GHKanban.AgentImage.Tests/GHKanban.AgentImage.Tests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `GHKanban.slnx` (via `dotnet sln add`)

- [ ] **Step 1: Add new package versions to `Directory.Packages.props`**

Insert these `<PackageVersion>` entries into the existing `<ItemGroup>` (alphabetically appropriate):

```xml
<PackageVersion Include="Docker.DotNet" Version="3.125.15" />
<PackageVersion Include="Anthropic.SDK" Version="5.2.0" />
<PackageVersion Include="OpenAI" Version="2.4.0" />
<PackageVersion Include="Microsoft.Extensions.AI" Version="9.7.0" />
<PackageVersion Include="Microsoft.Extensions.AI.OpenAI" Version="9.7.0-preview.1.25356.2" />
```

If any version doesn't resolve cleanly on `dotnet restore`, find the closest stable version and update. Note any deviations in the task commit message.

- [ ] **Step 2: Create `src/GHKanban.ContainerRuntime/GHKanban.ContainerRuntime.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Docker.DotNet" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\GHKanban.Core\GHKanban.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `src/GHKanban.AgentImage/GHKanban.AgentImage.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <RootNamespace>GHKanban.AgentImage</RootNamespace>
    <AssemblyName>GHKanban.AgentImage</AssemblyName>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" />
    <PackageReference Include="Octokit" />
    <PackageReference Include="Anthropic.SDK" />
    <PackageReference Include="OpenAI" />
    <PackageReference Include="Microsoft.Extensions.AI" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" />
  </ItemGroup>
</Project>
```

Note: no project references — image is standalone.

- [ ] **Step 4: Create the two test csproj files**

`tests/GHKanban.ContainerRuntime.Tests/GHKanban.ContainerRuntime.Tests.csproj`:

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
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\GHKanban.ContainerRuntime\GHKanban.ContainerRuntime.csproj" />
  </ItemGroup>
</Project>
```

`tests/GHKanban.AgentImage.Tests/GHKanban.AgentImage.Tests.csproj`:

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
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\GHKanban.AgentImage\GHKanban.AgentImage.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Add new projects to the solution + wire references**

```pwsh
cd c:/Development/GHKanban
dotnet sln add src/GHKanban.ContainerRuntime/GHKanban.ContainerRuntime.csproj
dotnet sln add src/GHKanban.AgentImage/GHKanban.AgentImage.csproj
dotnet sln add tests/GHKanban.ContainerRuntime.Tests/GHKanban.ContainerRuntime.Tests.csproj
dotnet sln add tests/GHKanban.AgentImage.Tests/GHKanban.AgentImage.Tests.csproj

# New dependency edges
dotnet add src/GHKanban.Agents/ reference src/GHKanban.ContainerRuntime/
dotnet add src/GHKanban.Web/ reference src/GHKanban.ContainerRuntime/
```

- [ ] **Step 6: Add a stub `Program.cs` to the new image project so it builds**

Write `src/GHKanban.AgentImage/Program.cs`:

```csharp
// Replaced fully in Task 13. This placeholder lets the project compile during scaffolding.
System.Console.WriteLine("GHKanban.AgentImage placeholder — replaced in Task 13");
return 0;
```

- [ ] **Step 7: Verify the solution builds**

```pwsh
dotnet restore
dotnet build --no-restore
```

Expected: 0 errors, 0 warnings. (Empty test projects are fine.)

- [ ] **Step 8: Commit**

```pwsh
git add Directory.Packages.props GHKanban.slnx src/GHKanban.ContainerRuntime/ src/GHKanban.AgentImage/ tests/GHKanban.ContainerRuntime.Tests/ tests/GHKanban.AgentImage.Tests/ src/GHKanban.Agents/GHKanban.Agents.csproj src/GHKanban.Web/GHKanban.Web.csproj
git commit -m "chore(scaffold): add ContainerRuntime + AgentImage projects (spec §3)"
```

---

## Task 2: ContainerSpec + ContainerRunResult value types

**Files:**
- Create: `src/GHKanban.ContainerRuntime/ContainerSpec.cs`
- Create: `src/GHKanban.ContainerRuntime/ContainerRunResult.cs`
- Test: `tests/GHKanban.ContainerRuntime.Tests/ContainerSpecTests.cs`

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.ContainerRuntime.Tests/ContainerSpecTests.cs`:

```csharp
using GHKanban.ContainerRuntime;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class ContainerSpecTests
{
    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var a = new ContainerSpec(
            Image: "ghcr.io/x/agent:0.1",
            Mounts: new[] { new ContainerMount("/host/foo", "/container/foo", ReadOnly: true) },
            Env: new Dictionary<string, string> { ["KEY"] = "value" },
            Labels: new Dictionary<string, string> { ["ghkanban"] = "true" },
            Timeout: TimeSpan.FromSeconds(60),
            CpuLimit: 1.0,
            MemoryBytes: 512L * 1024 * 1024);
        var b = a with { };
        Assert.Equal(a, b);
    }

    [Fact]
    public void RunResult_includes_exit_stdout_stderr()
    {
        var r = new ContainerRunResult(ExitCode: 0, Stdout: "ok", Stderr: null, TimedOut: false);
        Assert.Equal(0, r.ExitCode);
        Assert.False(r.TimedOut);
    }
}
```

- [ ] **Step 2: Run, expect compile failure**

```pwsh
dotnet test tests/GHKanban.ContainerRuntime.Tests/ --no-restore 2>&1 | Select-String "error|FAIL" | Select-Object -First 5
```

(Note: `dotnet test` for MTP returns 8 if no tests found; we want compile errors here.)

- [ ] **Step 3: Write `ContainerSpec.cs`**

```csharp
namespace GHKanban.ContainerRuntime;

public sealed record ContainerMount(string HostPath, string ContainerPath, bool ReadOnly);

public sealed record ContainerSpec(
    string Image,
    IReadOnlyList<ContainerMount> Mounts,
    IReadOnlyDictionary<string, string> Env,
    IReadOnlyDictionary<string, string> Labels,
    TimeSpan Timeout,
    double CpuLimit,
    long MemoryBytes);
```

- [ ] **Step 4: Write `ContainerRunResult.cs`**

```csharp
namespace GHKanban.ContainerRuntime;

public sealed record ContainerRunResult(
    int ExitCode,
    string Stdout,
    string? Stderr,
    bool TimedOut);
```

- [ ] **Step 5: Run tests via MTP**

```pwsh
dotnet run --project tests/GHKanban.ContainerRuntime.Tests/
```

Expected: 2 tests pass.

- [ ] **Step 6: Commit**

```pwsh
git add src/GHKanban.ContainerRuntime/ContainerSpec.cs src/GHKanban.ContainerRuntime/ContainerRunResult.cs tests/GHKanban.ContainerRuntime.Tests/ContainerSpecTests.cs
git commit -m "feat(container): ContainerSpec + ContainerRunResult value types (spec §3, §5)"
```

---

## Task 3: IContainerRuntime interface + DockerSocketLocator

**Files:**
- Create: `src/GHKanban.ContainerRuntime/IContainerRuntime.cs`
- Create: `src/GHKanban.ContainerRuntime/DockerSocketLocator.cs`
- Test: `tests/GHKanban.ContainerRuntime.Tests/DockerSocketLocatorTests.cs`

- [ ] **Step 1: Write the failing test for socket locator**

Write `tests/GHKanban.ContainerRuntime.Tests/DockerSocketLocatorTests.cs`:

```csharp
using GHKanban.ContainerRuntime;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class DockerSocketLocatorTests
{
    [Fact]
    public void DockerHost_env_var_takes_precedence()
    {
        var prev = Environment.GetEnvironmentVariable("DOCKER_HOST");
        try
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://localhost:2375");
            Assert.Equal("tcp://localhost:2375", DockerSocketLocator.Resolve().ToString().TrimEnd('/'));
        }
        finally { Environment.SetEnvironmentVariable("DOCKER_HOST", prev); }
    }

    [Fact]
    public void Defaults_to_platform_default_when_env_unset()
    {
        var prev = Environment.GetEnvironmentVariable("DOCKER_HOST");
        try
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", null);
            var uri = DockerSocketLocator.Resolve();
            // Windows: npipe; Linux/Mac: unix
            Assert.True(uri.Scheme is "npipe" or "unix" or "http", $"unexpected scheme: {uri.Scheme}");
        }
        finally { Environment.SetEnvironmentVariable("DOCKER_HOST", prev); }
    }
}
```

- [ ] **Step 2: Run, expect compile failure**

- [ ] **Step 3: Write `IContainerRuntime.cs`**

```csharp
namespace GHKanban.ContainerRuntime;

/// <summary>
/// Spawns ephemeral containers per invocation. Single-node only in B1; alternative
/// implementations (k3s, Nomad, remote Docker) plug in behind this interface in later slices.
/// </summary>
public interface IContainerRuntime
{
    /// <summary>
    /// Create + start + wait + collect logs + delete. Returns when the container exits or times out.
    /// Caller is responsible for ensuring host paths in <see cref="ContainerSpec.Mounts"/> exist.
    /// </summary>
    Task<ContainerRunResult> RunAsync(ContainerSpec spec, CancellationToken ct = default);

    /// <summary>
    /// Lists existing containers with the given label key=value. Used by ContainerJanitor.
    /// </summary>
    Task<IReadOnlyList<ContainerHandle>> ListLabeledAsync(string labelKey, string labelValue, CancellationToken ct = default);

    /// <summary>
    /// Forcibly removes a container by ID. Idempotent: missing containers are not an error.
    /// </summary>
    Task RemoveAsync(string id, CancellationToken ct = default);
}

public sealed record ContainerHandle(string Id, string State, DateTimeOffset FinishedAt);
```

- [ ] **Step 4: Write `DockerSocketLocator.cs`**

```csharp
using System.Runtime.InteropServices;

namespace GHKanban.ContainerRuntime;

public static class DockerSocketLocator
{
    public static Uri Resolve()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost))
            return new Uri(dockerHost);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new Uri("npipe://./pipe/docker_engine");

        return new Uri("unix:///var/run/docker.sock");
    }
}
```

- [ ] **Step 5: Run tests**

```pwsh
dotnet run --project tests/GHKanban.ContainerRuntime.Tests/
```

Expected: 4 tests pass (2 from Task 2 + 2 from this task).

- [ ] **Step 6: Commit**

```pwsh
git add src/GHKanban.ContainerRuntime/IContainerRuntime.cs src/GHKanban.ContainerRuntime/DockerSocketLocator.cs tests/GHKanban.ContainerRuntime.Tests/DockerSocketLocatorTests.cs
git commit -m "feat(container): IContainerRuntime interface + cross-platform socket locator (spec §2, §5)"
```

---

## Task 4: DockerContainerRuntime implementation

**Files:**
- Create: `src/GHKanban.ContainerRuntime/DockerContainerRuntime.cs`
- Test: `tests/GHKanban.ContainerRuntime.Tests/DockerContainerRuntimeTests.cs`

The test in this task runs a real `hello-world` container if Docker is available. If Docker isn't available, the test is skipped (not failed) so CI can run without Docker if we choose. CI uses Ubuntu runners which have Docker preinstalled.

- [ ] **Step 1: Write the failing/skippable integration test**

Write `tests/GHKanban.ContainerRuntime.Tests/DockerContainerRuntimeTests.cs`:

```csharp
using GHKanban.ContainerRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class DockerContainerRuntimeTests
{
    private static bool DockerAvailable()
    {
        try
        {
            using var client = new Docker.DotNet.DockerClientConfiguration(DockerSocketLocator.Resolve()).CreateClient();
            client.System.PingAsync().GetAwaiter().GetResult();
            return true;
        }
        catch { return false; }
    }

    [Fact]
    public async Task Runs_hello_world_container_returns_exit_zero()
    {
        if (!DockerAvailable())
        {
            // Skip: no Docker daemon reachable in this environment.
            return;
        }

        var runtime = new DockerContainerRuntime(NullLogger<DockerContainerRuntime>.Instance);
        var spec = new ContainerSpec(
            Image: "hello-world",
            Mounts: Array.Empty<ContainerMount>(),
            Env: new Dictionary<string, string>(),
            Labels: new Dictionary<string, string> { ["ghkanban"] = "test", ["ghkanban.test"] = "smoke" },
            Timeout: TimeSpan.FromSeconds(30),
            CpuLimit: 1.0,
            MemoryBytes: 64L * 1024 * 1024);

        var result = await runtime.RunAsync(spec, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("Hello from Docker", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_times_out_when_container_hangs()
    {
        if (!DockerAvailable()) return;

        var runtime = new DockerContainerRuntime(NullLogger<DockerContainerRuntime>.Instance);
        var spec = new ContainerSpec(
            Image: "alpine",
            Mounts: Array.Empty<ContainerMount>(),
            Env: new Dictionary<string, string>(),
            Labels: new Dictionary<string, string> { ["ghkanban"] = "test" },
            Timeout: TimeSpan.FromSeconds(2),
            CpuLimit: 1.0,
            MemoryBytes: 64L * 1024 * 1024);

        // Override command via a separate property would be cleaner, but for now we use the
        // default alpine entrypoint which is `sh`; passing no args makes it sleep waiting on tty.
        // (This test exercises the timeout path; precise command isn't important.)
        var result = await runtime.RunAsync(spec, TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut, "expected timeout to be set");
    }
}
```

- [ ] **Step 2: Write `DockerContainerRuntime.cs`**

```csharp
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace GHKanban.ContainerRuntime;

public sealed class DockerContainerRuntime : IContainerRuntime, IDisposable
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerContainerRuntime> _log;

    public DockerContainerRuntime(ILogger<DockerContainerRuntime> log)
    {
        _client = new DockerClientConfiguration(DockerSocketLocator.Resolve()).CreateClient();
        _log = log;
    }

    public async Task<ContainerRunResult> RunAsync(ContainerSpec spec, CancellationToken ct = default)
    {
        // Ensure image is pulled.
        try
        {
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = spec.Image },
                authConfig: null,
                progress: new Progress<JSONMessage>(),
                cancellationToken: ct);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Some registries return 404 for already-cached images; ignore.
        }

        // Create container.
        var binds = spec.Mounts.Select(m => $"{m.HostPath}:{m.ContainerPath}:{(m.ReadOnly ? "ro" : "rw")}").ToList();

        var createResponse = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = spec.Image,
            Env = spec.Env.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            Labels = new Dictionary<string, string>(spec.Labels),
            HostConfig = new HostConfig
            {
                Binds = binds,
                AutoRemove = false,
                NanoCPUs = (long)(spec.CpuLimit * 1_000_000_000L),
                Memory = spec.MemoryBytes,
                NetworkMode = "bridge",
            },
            AttachStdout = true,
            AttachStderr = true,
        }, ct);

        var id = createResponse.ID;

        try
        {
            await _client.Containers.StartContainerAsync(id, new ContainerStartParameters(), ct);

            // Wait with timeout.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(spec.Timeout);

            bool timedOut = false;
            ContainerWaitResponse? waitResp = null;
            try
            {
                waitResp = await _client.Containers.WaitContainerAsync(id, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                timedOut = true;
                try { await _client.Containers.KillContainerAsync(id, new ContainerKillParameters(), CancellationToken.None); }
                catch (Exception killEx) { _log.LogWarning(killEx, "Kill failed for timed-out container {Id}", id); }
            }

            // Collect logs.
            var (stdout, stderr) = await ReadAllLogsAsync(id, ct);

            return new ContainerRunResult(
                ExitCode: timedOut ? -1 : (int)(waitResp?.StatusCode ?? -1),
                Stdout: stdout,
                Stderr: string.IsNullOrEmpty(stderr) ? null : stderr,
                TimedOut: timedOut);
        }
        finally
        {
            try { await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true }, CancellationToken.None); }
            catch (Exception ex) { _log.LogWarning(ex, "Container removal failed for {Id}", id); }
        }
    }

    public async Task<IReadOnlyList<ContainerHandle>> ListLabeledAsync(string labelKey, string labelValue, CancellationToken ct = default)
    {
        var resp = await _client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [$"{labelKey}={labelValue}"] = true }
            }
        }, ct);

        return resp.Select(c => new ContainerHandle(
            Id: c.ID,
            State: c.State,
            FinishedAt: c.Created)).ToList();
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true }, ct);
        }
        catch (DockerContainerNotFoundException) { /* idempotent */ }
    }

    private async Task<(string stdout, string stderr)> ReadAllLogsAsync(string id, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var errSb = new StringBuilder();

        var stream = await _client.Containers.GetContainerLogsAsync(id, tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = false }, ct);

        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
            if (read.EOF) break;
            var text = Encoding.UTF8.GetString(buffer, 0, read.Count);
            if (read.Target == MultiplexedStream.TargetStream.StandardError) errSb.Append(text);
            else sb.Append(text);
        }

        return (sb.ToString(), errSb.ToString());
    }

    public void Dispose() => _client.Dispose();
}
```

- [ ] **Step 3: Run tests (Docker required for actual pass; tests no-op if Docker absent)**

```pwsh
dotnet run --project tests/GHKanban.ContainerRuntime.Tests/
```

Expected on Windows with Docker Desktop: hello-world + timeout tests pass. Without Docker: same tests "pass" (early return). Plus 4 previously-passing tests.

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.ContainerRuntime/DockerContainerRuntime.cs tests/GHKanban.ContainerRuntime.Tests/DockerContainerRuntimeTests.cs
git commit -m "feat(container): Docker REST adapter via Docker.DotNet (spec §2, §6)"
```

---

## Task 5: ContainerJanitor BackgroundService

**Files:**
- Create: `src/GHKanban.ContainerRuntime/ContainerJanitor.cs`
- Test: `tests/GHKanban.ContainerRuntime.Tests/ContainerJanitorTests.cs`

- [ ] **Step 1: Write the failing test using a fake runtime**

Write `tests/GHKanban.ContainerRuntime.Tests/ContainerJanitorTests.cs`:

```csharp
using GHKanban.ContainerRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class ContainerJanitorTests
{
    [Fact]
    public async Task CleansExitedContainersOlderThanThreshold()
    {
        var runtime = Substitute.For<IContainerRuntime>();
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var old = new ContainerHandle("a", "exited", now.AddMinutes(-45));
        var young = new ContainerHandle("b", "exited", now.AddMinutes(-5));
        var running = new ContainerHandle("c", "running", now.AddMinutes(-60));
        runtime.ListLabeledAsync("ghkanban", "true", Arg.Any<CancellationToken>())
            .Returns(new[] { old, young, running });

        await ContainerJanitor.SweepOnceAsync(runtime, now, threshold: TimeSpan.FromMinutes(30),
            NullLogger.Instance, TestContext.Current.CancellationToken);

        await runtime.Received(1).RemoveAsync("a", Arg.Any<CancellationToken>());
        await runtime.DidNotReceive().RemoveAsync("b", Arg.Any<CancellationToken>());
        await runtime.DidNotReceive().RemoveAsync("c", Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Write `ContainerJanitor.cs`**

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GHKanban.ContainerRuntime;

public sealed class ContainerJanitor : BackgroundService
{
    private readonly IContainerRuntime _runtime;
    private readonly ILogger<ContainerJanitor> _log;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OrphanThreshold = TimeSpan.FromMinutes(30);

    public ContainerJanitor(IContainerRuntime runtime, ILogger<ContainerJanitor> log)
    { _runtime = runtime; _log = log; }

    public static async Task SweepOnceAsync(IContainerRuntime runtime, DateTimeOffset now, TimeSpan threshold, ILogger log, CancellationToken ct)
    {
        var handles = await runtime.ListLabeledAsync("ghkanban", "true", ct);
        foreach (var h in handles)
        {
            if (h.State != "exited") continue;
            if (now - h.FinishedAt < threshold) continue;
            try
            {
                await runtime.RemoveAsync(h.Id, ct);
                log.LogInformation("Janitor removed orphan container {Id} (state={State}, finished={FinishedAt})", h.Id, h.State, h.FinishedAt);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Janitor failed to remove {Id}", h.Id);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try { await SweepOnceAsync(_runtime, DateTimeOffset.UtcNow, OrphanThreshold, _log, stoppingToken); }
            catch (Exception ex) { _log.LogError(ex, "Janitor sweep failed"); }
        }
    }
}
```

- [ ] **Step 3: Run tests**

```pwsh
dotnet run --project tests/GHKanban.ContainerRuntime.Tests/
```

Expected: 1 new test passes + all previous tests still pass.

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.ContainerRuntime/ContainerJanitor.cs tests/GHKanban.ContainerRuntime.Tests/ContainerJanitorTests.cs
git commit -m "feat(container): ContainerJanitor BackgroundService for orphan cleanup (spec §6)"
```

---

## Task 6: Extend Core models + YAML loader for container section

**Files:**
- Modify: `src/GHKanban.Core/Models/AgentConfig.cs`
- Modify: `src/GHKanban.Config/YamlConfigLoader.cs`
- Test: `tests/GHKanban.Config.Tests/YamlConfigLoaderTests.cs`

- [ ] **Step 1: Extend `AgentConfig`**

Replace `src/GHKanban.Core/Models/AgentConfig.cs` with:

```csharp
namespace GHKanban.Core.Models;

public sealed record AgentConfig(
    string Id,
    string Name,
    string Implementation,
    IReadOnlyList<TriggerSpec> Triggers,
    ContainerAgentSpec? Container = null);

public sealed record ContainerAgentSpec(
    string Image,
    ContainerLlmSpec Llm,
    ContainerPromptSpec Prompt,
    IReadOnlyList<string> Tools,
    TimeSpan Timeout,
    double CpuLimit,
    long MemoryBytes);

public sealed record ContainerLlmSpec(
    string Provider,        // none | anthropic | openai
    string? Model,
    string? ApiKeyEnv);

public sealed record ContainerPromptSpec(
    string? SystemFile,     // path relative to agents/<id>/
    string User);
```

- [ ] **Step 2: Add a failing test for container YAML parsing**

Append to `tests/GHKanban.Config.Tests/YamlConfigLoaderTests.cs`:

```csharp
[Fact]
public void LoadsAgentConfigWithContainerSection()
{
    var yaml = """
        name: Summariser
        implementation: container
        triggers:
          - on: issue.opened
            when: not has-label("nosummary")
        container:
          image: ghcr.io/jamesburton/ghkanban-agent:0.2.0
          llm:
            provider: anthropic
            model: claude-sonnet-4-6
            api-key-env: ANTHROPIC_API_KEY
          prompt:
            system: ./files/system.md
            user: |
              Summarise: {{issue.title}}
          tools:
            - github.post-comment
          timeout: 60s
          resources:
            cpu: 1
            memory: 512m
        """;
    var cfg = YamlConfigLoader.LoadAgentConfig("summariser", yaml);
    Assert.Equal("container", cfg.Implementation);
    Assert.NotNull(cfg.Container);
    Assert.Equal("ghcr.io/jamesburton/ghkanban-agent:0.2.0", cfg.Container!.Image);
    Assert.Equal("anthropic", cfg.Container.Llm.Provider);
    Assert.Equal("ANTHROPIC_API_KEY", cfg.Container.Llm.ApiKeyEnv);
    Assert.Equal("./files/system.md", cfg.Container.Prompt.SystemFile);
    Assert.Contains("Summarise:", cfg.Container.Prompt.User);
    Assert.Single(cfg.Container.Tools);
    Assert.Equal(TimeSpan.FromSeconds(60), cfg.Container.Timeout);
    Assert.Equal(1.0, cfg.Container.CpuLimit);
    Assert.Equal(512L * 1024 * 1024, cfg.Container.MemoryBytes);
}

[Fact]
public void LoadsAgentConfigWithoutContainerSection()
{
    var yaml = """
        name: Stub
        implementation: stub
        triggers:
          - on: issue.labeled
            when: has-label("x")
        """;
    var cfg = YamlConfigLoader.LoadAgentConfig("stub", yaml);
    Assert.Equal("stub", cfg.Implementation);
    Assert.Null(cfg.Container);
}
```

- [ ] **Step 3: Update `YamlConfigLoader.cs`**

In `src/GHKanban.Config/YamlConfigLoader.cs`, add these private classes inside the `YamlConfigLoader` class (above `LoadAgentConfig`):

```csharp
private sealed class RawContainer
{
    public string? Image { get; set; }
    public RawLlm? Llm { get; set; }
    public RawPrompt? Prompt { get; set; }
    public List<string>? Tools { get; set; }
    public string? Timeout { get; set; }
    public RawResources? Resources { get; set; }
}
private sealed class RawLlm { public string? Provider { get; set; } public string? Model { get; set; } public string? ApiKeyEnv { get; set; } }
private sealed class RawPrompt { public string? System { get; set; } public string? User { get; set; } }
private sealed class RawResources { public double? Cpu { get; set; } public string? Memory { get; set; } }
```

Update the existing `RawAgent` class to add a `Container` property:

```csharp
private sealed class RawAgent
{
    public string? Name { get; set; }
    public string? Implementation { get; set; }
    public List<RawTrigger>? Triggers { get; set; }
    public RawContainer? Container { get; set; }
}
```

Replace the body of `LoadAgentConfig` with:

```csharp
public static AgentConfig LoadAgentConfig(string id, string yaml)
{
    var raw = _deserializer.Deserialize<RawAgent>(yaml) ?? throw new InvalidOperationException("empty agent yaml");
    var triggers = (raw.Triggers ?? new())
        .Select(t => new TriggerSpec(t.On ?? throw new InvalidOperationException("trigger.on required"),
                                     t.When ?? "true"))
        .ToList();

    ContainerAgentSpec? container = null;
    if (raw.Container is not null)
    {
        var c = raw.Container;
        container = new ContainerAgentSpec(
            Image: c.Image ?? throw new InvalidOperationException("container.image required"),
            Llm: new ContainerLlmSpec(
                Provider: c.Llm?.Provider ?? "none",
                Model: c.Llm?.Model,
                ApiKeyEnv: c.Llm?.ApiKeyEnv),
            Prompt: new ContainerPromptSpec(
                SystemFile: c.Prompt?.System,
                User: c.Prompt?.User ?? throw new InvalidOperationException("container.prompt.user required")),
            Tools: c.Tools ?? new List<string>(),
            Timeout: ParseDuration(c.Timeout) ?? TimeSpan.FromSeconds(60),
            CpuLimit: c.Resources?.Cpu ?? 1.0,
            MemoryBytes: ParseMemory(c.Resources?.Memory) ?? 512L * 1024 * 1024);
    }

    return new AgentConfig(
        Id: id,
        Name: raw.Name ?? id,
        Implementation: raw.Implementation ?? "stub",
        Triggers: triggers,
        Container: container);
}

private static long? ParseMemory(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return null;
    s = s.Trim().ToLowerInvariant();
    if (s.EndsWith("g")) return long.Parse(s[..^1]) * 1024L * 1024 * 1024;
    if (s.EndsWith("m")) return long.Parse(s[..^1]) * 1024L * 1024;
    if (s.EndsWith("k")) return long.Parse(s[..^1]) * 1024;
    return long.Parse(s);
}
```

- [ ] **Step 4: Run tests**

```pwsh
dotnet run --project tests/GHKanban.Config.Tests/
```

Expected: 8 tests pass (6 existing + 2 new).

- [ ] **Step 5: Verify the full build**

```pwsh
dotnet build --no-restore
```

Other projects that reference `AgentConfig` may need to be checked — the new `Container` parameter has a default of `null` so all existing call sites continue to compile.

- [ ] **Step 6: Commit**

```pwsh
git add src/GHKanban.Core/Models/AgentConfig.cs src/GHKanban.Config/YamlConfigLoader.cs tests/GHKanban.Config.Tests/YamlConfigLoaderTests.cs
git commit -m "feat(config): extend AgentConfig + YAML loader for container section (spec §4)"
```

---

## Task 7: Agent image — SkillConfig POCOs + YAML loader (image-local)

**Files:**
- Create: `src/GHKanban.AgentImage/Config/SkillConfig.cs`
- Create: `src/GHKanban.AgentImage/Config/SkillConfigLoader.cs`
- Test: `tests/GHKanban.AgentImage.Tests/SkillConfigLoaderTests.cs`

The agent image deliberately duplicates these POCOs rather than referencing GHKanban.Core (so the image remains a standalone deployable).

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.AgentImage.Tests/SkillConfigLoaderTests.cs`:

```csharp
using GHKanban.AgentImage.Config;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class SkillConfigLoaderTests
{
    [Fact]
    public void Parses_full_agent_yaml_with_container_block()
    {
        var yaml = """
            name: Summariser
            implementation: container
            container:
              image: ghcr.io/x/agent:0.1
              llm:
                provider: anthropic
                model: claude-sonnet-4-6
                api-key-env: ANTHROPIC_API_KEY
              prompt:
                system: ./files/system.md
                user: Summarise {{issue.title}}
              tools:
                - github.post-comment
              timeout: 60s
            """;
        var cfg = SkillConfigLoader.Load(yaml);
        Assert.Equal("anthropic", cfg.Llm.Provider);
        Assert.Equal("./files/system.md", cfg.Prompt.SystemFile);
        Assert.Equal("Summarise {{issue.title}}", cfg.Prompt.User);
        Assert.Contains("github.post-comment", cfg.Tools);
    }

    [Fact]
    public void Defaults_provider_to_none_when_omitted()
    {
        var yaml = """
            name: x
            implementation: container
            container:
              image: i
              prompt:
                user: hi
            """;
        var cfg = SkillConfigLoader.Load(yaml);
        Assert.Equal("none", cfg.Llm.Provider);
    }
}
```

- [ ] **Step 2: Write `SkillConfig.cs`**

```csharp
namespace GHKanban.AgentImage.Config;

public sealed record SkillConfig(
    SkillLlm Llm,
    SkillPrompt Prompt,
    IReadOnlyList<string> Tools);

public sealed record SkillLlm(string Provider, string? Model);

public sealed record SkillPrompt(string? SystemFile, string User);
```

- [ ] **Step 3: Write `SkillConfigLoader.cs`**

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GHKanban.AgentImage.Config;

public static class SkillConfigLoader
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private sealed class RawAgent { public RawContainer? Container { get; set; } }
    private sealed class RawContainer { public RawLlm? Llm { get; set; } public RawPrompt? Prompt { get; set; } public List<string>? Tools { get; set; } }
    private sealed class RawLlm { public string? Provider { get; set; } public string? Model { get; set; } }
    private sealed class RawPrompt { public string? System { get; set; } public string? User { get; set; } }

    public static SkillConfig Load(string yaml)
    {
        var raw = _deserializer.Deserialize<RawAgent>(yaml) ?? throw new InvalidOperationException("empty");
        var c = raw.Container ?? throw new InvalidOperationException("container section required for agent image");

        return new SkillConfig(
            Llm: new SkillLlm(c.Llm?.Provider ?? "none", c.Llm?.Model),
            Prompt: new SkillPrompt(c.Prompt?.System, c.Prompt?.User ?? throw new InvalidOperationException("prompt.user required")),
            Tools: c.Tools ?? new List<string>());
    }
}
```

- [ ] **Step 4: Run tests**

```pwsh
dotnet run --project tests/GHKanban.AgentImage.Tests/
```

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.AgentImage/Config/ tests/GHKanban.AgentImage.Tests/SkillConfigLoaderTests.cs
git commit -m "feat(image): SkillConfig POCOs + image-local YAML loader (spec §4, §7)"
```

---

## Task 8: Agent image — PromptTemplate interpolation

**Files:**
- Create: `src/GHKanban.AgentImage/Config/PromptTemplate.cs`
- Test: `tests/GHKanban.AgentImage.Tests/PromptTemplateTests.cs`

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.AgentImage.Tests/PromptTemplateTests.cs`:

```csharp
using GHKanban.AgentImage.Config;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class PromptTemplateTests
{
    [Fact]
    public void Interpolates_known_tokens()
    {
        var ctx = new Dictionary<string, string>
        {
            ["issue.title"] = "Login broken",
            ["issue.body"] = "Steps:",
            ["issue.labels"] = "bug, auth",
            ["issue.repo"] = "owner/repo",
            ["issue.number"] = "42",
            ["trigger.event"] = "issue.opened",
            ["trigger.rule"] = "not has-label(\"x\")",
            ["run.id"] = "abc123",
        };
        var rendered = PromptTemplate.Render(
            "Issue {{issue.repo}}#{{issue.number}} ({{trigger.event}}): {{issue.title}}", ctx);

        Assert.Equal("Issue owner/repo#42 (issue.opened): Login broken", rendered);
    }

    [Fact]
    public void Leaves_unknown_tokens_unreplaced()
    {
        var rendered = PromptTemplate.Render("Hello {{unknown.token}}", new Dictionary<string, string>());
        Assert.Equal("Hello {{unknown.token}}", rendered);
    }
}
```

- [ ] **Step 2: Write `PromptTemplate.cs`**

```csharp
using System.Text.RegularExpressions;

namespace GHKanban.AgentImage.Config;

public static class PromptTemplate
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*([a-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return tokens.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}
```

- [ ] **Step 3: Run tests, commit**

```pwsh
dotnet run --project tests/GHKanban.AgentImage.Tests/
git add src/GHKanban.AgentImage/Config/PromptTemplate.cs tests/GHKanban.AgentImage.Tests/PromptTemplateTests.cs
git commit -m "feat(image): PromptTemplate {{token}} interpolation (spec §4)"
```

---

## Task 9: Agent image — ChatClientFactory (none, anthropic, openai)

**Files:**
- Create: `src/GHKanban.AgentImage/Providers/ChatClientFactory.cs`
- Test: `tests/GHKanban.AgentImage.Tests/ChatClientFactoryTests.cs`

The factory returns `IChatClient?` — `null` for the `none` provider (caller short-circuits).

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.AgentImage.Tests/ChatClientFactoryTests.cs`:

```csharp
using GHKanban.AgentImage.Config;
using GHKanban.AgentImage.Providers;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class ChatClientFactoryTests
{
    [Fact]
    public void Returns_null_for_none_provider()
    {
        var llm = new SkillLlm("none", null);
        Assert.Null(ChatClientFactory.Create(llm, apiKey: ""));
    }

    [Fact]
    public void Throws_for_unknown_provider()
    {
        var llm = new SkillLlm("bogus", "model-x");
        Assert.Throws<InvalidOperationException>(() => ChatClientFactory.Create(llm, "key"));
    }

    [Fact]
    public void Creates_anthropic_client_for_anthropic_provider()
    {
        // We're not making a network call; just checking construction succeeds.
        var llm = new SkillLlm("anthropic", "claude-sonnet-4-6");
        using var client = ChatClientFactory.Create(llm, apiKey: "sk-ant-test");
        Assert.NotNull(client);
    }
}
```

- [ ] **Step 2: Write `ChatClientFactory.cs`**

The exact API surface for the Anthropic + OpenAI SDKs may shift between versions; if the canonical "AsChatClient" extension method has moved, adjust accordingly. The structure stays the same.

```csharp
using GHKanban.AgentImage.Config;
using Microsoft.Extensions.AI;

namespace GHKanban.AgentImage.Providers;

public static class ChatClientFactory
{
    public static IChatClient? Create(SkillLlm cfg, string apiKey) => cfg.Provider switch
    {
        "none"      => null,
        "anthropic" => CreateAnthropic(cfg, apiKey),
        "openai"    => CreateOpenAi(cfg, apiKey),
        _ => throw new InvalidOperationException($"Unknown llm.provider: {cfg.Provider}")
    };

    private static IChatClient CreateAnthropic(SkillLlm cfg, string apiKey)
    {
        // Anthropic.SDK exposes AsChatClient via its Messages client.
        var anthropic = new Anthropic.SDK.AnthropicClient(apiKey);
        return anthropic.Messages.AsChatClient(cfg.Model ?? "claude-sonnet-4-6");
    }

    private static IChatClient CreateOpenAi(SkillLlm cfg, string apiKey)
    {
        var openai = new OpenAI.OpenAIClient(apiKey);
        return openai.GetChatClient(cfg.Model ?? "gpt-5").AsIChatClient();
    }
}
```

If `AsChatClient` / `AsIChatClient` extension methods aren't available at the versions resolved, you may need to manually wrap in a thin `IChatClient` implementation. Document any such deviation in the commit.

- [ ] **Step 3: Run tests, commit**

```pwsh
dotnet run --project tests/GHKanban.AgentImage.Tests/
git add src/GHKanban.AgentImage/Providers/ChatClientFactory.cs tests/GHKanban.AgentImage.Tests/ChatClientFactoryTests.cs
git commit -m "feat(image): ChatClientFactory for none/anthropic/openai providers (spec §8)"
```

---

## Task 10: Agent image — IAgentTool + GitHubPostCommentTool

**Files:**
- Create: `src/GHKanban.AgentImage/Tools/IAgentTool.cs`
- Create: `src/GHKanban.AgentImage/Tools/GitHubPostCommentTool.cs`
- Create: `src/GHKanban.AgentImage/Tools/ToolRegistry.cs`
- Test: `tests/GHKanban.AgentImage.Tests/ToolRegistryTests.cs`

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.AgentImage.Tests/ToolRegistryTests.cs`:

```csharp
using GHKanban.AgentImage.Tools;
using Xunit;

namespace GHKanban.AgentImage.Tests;

public class ToolRegistryTests
{
    [Fact]
    public void Returns_only_allowed_tools()
    {
        var fakeA = new FakeTool("github.post-comment");
        var fakeB = new FakeTool("github.add-label");
        var registry = new ToolRegistry(new IAgentTool[] { fakeA, fakeB });

        var allowed = registry.GetAllowed(new[] { "github.post-comment" }).ToList();

        Assert.Single(allowed);
        Assert.Equal("github.post-comment", allowed[0].Name);
    }

    private sealed class FakeTool : IAgentTool
    {
        public string Name { get; }
        public FakeTool(string name) => Name = name;
        public Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default) => Task.FromResult("");
    }
}
```

- [ ] **Step 2: Write `IAgentTool.cs`**

```csharp
namespace GHKanban.AgentImage.Tools;

public interface IAgentTool
{
    string Name { get; }
    Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default);
}
```

- [ ] **Step 3: Write `ToolRegistry.cs`**

```csharp
namespace GHKanban.AgentImage.Tools;

public sealed class ToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _byName;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _byName = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<IAgentTool> GetAllowed(IEnumerable<string> allowlist)
    {
        foreach (var name in allowlist)
        {
            if (_byName.TryGetValue(name, out var tool)) yield return tool;
        }
    }
}
```

- [ ] **Step 4: Write `GitHubPostCommentTool.cs`**

```csharp
using System.Text.Json;
using Octokit;

namespace GHKanban.AgentImage.Tools;

public sealed class GitHubPostCommentTool : IAgentTool
{
    private readonly IGitHubClient _client;
    private readonly string _repo;
    private readonly int _issueNumber;

    public string Name => "github.post-comment";

    public GitHubPostCommentTool(string personalAccessToken, string repo, int issueNumber)
    {
        var conn = new Connection(new ProductHeaderValue("GHKanban-Agent", "0.2"))
        {
            Credentials = new Credentials(personalAccessToken)
        };
        _client = new GitHubClient(conn);
        _repo = repo;
        _issueNumber = issueNumber;
    }

    /// <summary>
    /// Posts a comment. Accepts either a JSON object {"body": "..."} (when called as a tool by the model)
    /// or a plain text body (when invoked directly by the entrypoint with the model's text output).
    /// Returns the created comment URL.
    /// </summary>
    public async Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default)
    {
        string body;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            body = doc.RootElement.TryGetProperty("body", out var b) ? b.GetString() ?? "" : argumentsJson;
        }
        catch (JsonException)
        {
            body = argumentsJson;
        }

        var parts = _repo.Split('/', 2);
        if (parts.Length != 2) throw new ArgumentException($"Bad repo: {_repo}");

        var comment = await _client.Issue.Comment.Create(parts[0], parts[1], _issueNumber, body);
        return comment.HtmlUrl;
    }
}
```

- [ ] **Step 5: Run tests, commit**

```pwsh
dotnet run --project tests/GHKanban.AgentImage.Tests/
git add src/GHKanban.AgentImage/Tools/ tests/GHKanban.AgentImage.Tests/ToolRegistryTests.cs
git commit -m "feat(image): IAgentTool + GitHubPostCommentTool + allowlist registry (spec §4, §7)"
```

---

## Task 11: Agent image — Entrypoint orchestration (Program.cs)

**Files:**
- Modify: `src/GHKanban.AgentImage/Program.cs` (full replacement of the placeholder)

There are limited unit tests for the entrypoint itself — its behaviour is observable through integration testing (Task 20). The components it composes are all unit-tested.

- [ ] **Step 1: Replace `Program.cs`**

```csharp
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

        var response = await chatClient.GetResponseAsync(messages);
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
        ["issue.labels"] = string.Join(", ", issue.GetProperty("Labels").EnumerateArray().Select(l => l.GetString())),
        ["issue.assignees"] = string.Join(", ", issue.GetProperty("Assignees").EnumerateArray().Select(a => a.GetString())),
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
```

Note: this assumes the control plane writes `event.json` as a serialised `IssueContext` (which has `Issue`, `TriggerEvent`, `MatchingRule`, `AgentName` properties). Task 13 will write this format from the control-plane side.

- [ ] **Step 2: Build the image project**

```pwsh
dotnet build src/GHKanban.AgentImage/
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```pwsh
git add src/GHKanban.AgentImage/Program.cs
git commit -m "feat(image): full entrypoint orchestration — load, render, run, post, exit (spec §7)"
```

---

## Task 12: Agent image — Dockerfile + local build verification

**Files:**
- Create: `src/GHKanban.AgentImage/Dockerfile`
- Create: `src/GHKanban.AgentImage/.dockerignore`

- [ ] **Step 1: Write `.dockerignore`**

```
bin/
obj/
*.user
.vs/
```

- [ ] **Step 2: Write `Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY GHKanban.AgentImage.csproj ./
RUN dotnet restore GHKanban.AgentImage.csproj
COPY . .
RUN dotnet publish GHKanban.AgentImage.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app .

# The image expects /skill, /event.json, /secrets/* to be mounted at runtime by the control plane.
ENTRYPOINT ["dotnet", "GHKanban.AgentImage.dll"]
```

The build needs the central-package-management context to resolve versions. We solve that by copying the root `Directory.Packages.props` and `Directory.Build.props` into the build context. Adjust the Dockerfile if your local Docker can't see them:

If `dotnet restore` fails because it can't find `Directory.Packages.props`, the simplest fix is to copy the relevant files in. Update the build stage:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
# Copy build props from the repo root (build context = repo root for this image build)
COPY Directory.Build.props Directory.Packages.props NuGet.config ./
COPY src/GHKanban.AgentImage/ src/GHKanban.AgentImage/
WORKDIR /src/src/GHKanban.AgentImage
RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-self-contained
```

The CI workflow in Task 14 sets the build context to the repo root for exactly this reason.

- [ ] **Step 3: Build the image locally (Docker required)**

```pwsh
# If Docker is installed, build from repo root:
docker build -f src/GHKanban.AgentImage/Dockerfile -t ghkanban-agent:dev .
```

Expected: image builds successfully. If Docker isn't installed, skip this step — Task 14 (CI) builds the image automatically.

- [ ] **Step 4: Commit**

```pwsh
git add src/GHKanban.AgentImage/Dockerfile src/GHKanban.AgentImage/.dockerignore
git commit -m "feat(image): multi-stage Dockerfile + .dockerignore (spec §10)"
```

---

## Task 13: Control plane — ContainerAgent + AgentRegistry update

**Files:**
- Create: `src/GHKanban.Agents/ContainerAgent.cs`
- Modify: `src/GHKanban.Agents/AgentRegistry.cs`
- Test: `tests/GHKanban.Agents.Tests/ContainerAgentTests.cs`

- [ ] **Step 1: Write the failing test**

Write `tests/GHKanban.Agents.Tests/ContainerAgentTests.cs`:

```csharp
using GHKanban.Agents;
using GHKanban.ContainerRuntime;
using GHKanban.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GHKanban.Agents.Tests;

public class ContainerAgentTests
{
    [Fact]
    public async Task Builds_correct_ContainerSpec_and_records_success()
    {
        var runtime = Substitute.For<IContainerRuntime>();
        runtime.RunAsync(Arg.Any<ContainerSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ContainerRunResult(ExitCode: 0,
                Stdout: "{\"event\":\"complete\",\"comment_url\":\"https://github.com/x/y/issues/1#issuecomment-1\"}",
                Stderr: null, TimedOut: false));

        var cfg = new AgentConfig(
            Id: "summariser",
            Name: "Summariser",
            Implementation: "container",
            Triggers: new[] { new TriggerSpec("issue.opened", "true") },
            Container: new ContainerAgentSpec(
                Image: "ghcr.io/x/agent:1",
                Llm: new ContainerLlmSpec("none", null, null),
                Prompt: new ContainerPromptSpec(null, "hello"),
                Tools: new[] { "github.post-comment" },
                Timeout: TimeSpan.FromSeconds(30),
                CpuLimit: 1.0,
                MemoryBytes: 256L * 1024 * 1024));

        var dirs = new ContainerAgentDirs(
            ConfigRoot: Path.GetTempPath(),
            RunsRoot: Path.Combine(Path.GetTempPath(), $"runs-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(dirs.RunsRoot);

        try
        {
            var agent = new ContainerAgent("Summariser", cfg, runtime, dirs, NullLogger<ContainerAgent>.Instance);
            var ctx = new IssueContext(
                Issue: new IssueView("x/y", 1, "t", IssueState.Open, [], [], null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "https://github.com/x/y/issues/1"),
                TriggerEvent: "issue.opened",
                MatchingRule: "true",
                AgentName: "Summariser");

            var result = await agent.TriggerAsync(ctx, TestContext.Current.CancellationToken);

            Assert.Equal(AgentRunStatus.Success, result.Status);
            await runtime.Received(1).RunAsync(Arg.Is<ContainerSpec>(s =>
                s.Image == "ghcr.io/x/agent:1" &&
                s.Labels.ContainsKey("ghkanban") &&
                s.Mounts.Any(m => m.ContainerPath == "/event.json")
            ), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(dirs.RunsRoot)) Directory.Delete(dirs.RunsRoot, true);
        }
    }

    [Fact]
    public async Task Records_failed_when_container_exits_nonzero()
    {
        var runtime = Substitute.For<IContainerRuntime>();
        runtime.RunAsync(Arg.Any<ContainerSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ContainerRunResult(ExitCode: 1, Stdout: "", Stderr: "boom", TimedOut: false));

        var cfg = new AgentConfig("x", "X", "container",
            new[] { new TriggerSpec("issue.opened", "true") },
            Container: new ContainerAgentSpec("img", new("none", null, null), new(null, "hi"),
                new[] { "github.post-comment" }, TimeSpan.FromSeconds(10), 1.0, 256L * 1024 * 1024));

        var dirs = new ContainerAgentDirs(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), $"runs-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(dirs.RunsRoot);
        try
        {
            var agent = new ContainerAgent("X", cfg, runtime, dirs, NullLogger<ContainerAgent>.Instance);
            var ctx = new IssueContext(
                Issue: new IssueView("x/y", 1, "t", IssueState.Open, [], [], null,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ""),
                TriggerEvent: "issue.opened", MatchingRule: "true", AgentName: "X");

            var result = await agent.TriggerAsync(ctx, TestContext.Current.CancellationToken);

            Assert.Equal(AgentRunStatus.Failed, result.Status);
            Assert.Contains("boom", result.Error);
        }
        finally { Directory.Delete(dirs.RunsRoot, true); }
    }
}
```

- [ ] **Step 2: Write `ContainerAgent.cs`**

```csharp
using System.Text.Json;
using GHKanban.ContainerRuntime;
using GHKanban.Core.Models;
using Microsoft.Extensions.Logging;

namespace GHKanban.Agents;

public sealed record ContainerAgentDirs(string ConfigRoot, string RunsRoot);

public sealed class ContainerAgent : IGHKanbanAgent
{
    private readonly AgentConfig _config;
    private readonly IContainerRuntime _runtime;
    private readonly ContainerAgentDirs _dirs;
    private readonly ILogger<ContainerAgent> _log;

    public string Name { get; }

    public ContainerAgent(string name, AgentConfig config, IContainerRuntime runtime, ContainerAgentDirs dirs, ILogger<ContainerAgent> log)
    {
        Name = name;
        _config = config;
        _runtime = runtime;
        _dirs = dirs;
        _log = log;
    }

    public async Task<AgentRunResult> TriggerAsync(IssueContext ctx, CancellationToken ct = default)
    {
        var container = _config.Container
            ?? throw new InvalidOperationException($"Agent {_config.Id} has no container config");

        var runId = Guid.NewGuid().ToString("N");
        var runDir = Path.Combine(_dirs.RunsRoot, runId);
        Directory.CreateDirectory(runDir);
        var eventPath = Path.Combine(runDir, "event.json");

        try
        {
            await File.WriteAllTextAsync(eventPath, JsonSerializer.Serialize(ctx), ct);

            var mounts = BuildMounts(eventPath, container);
            var env = BuildEnv(ctx, runId);
            var labels = new Dictionary<string, string>
            {
                ["ghkanban"] = "true",
                ["ghkanban.agent"] = _config.Id,
                ["ghkanban.run-id"] = runId,
            };

            var spec = new ContainerSpec(
                Image: container.Image,
                Mounts: mounts,
                Env: env,
                Labels: labels,
                Timeout: container.Timeout,
                CpuLimit: container.CpuLimit,
                MemoryBytes: container.MemoryBytes);

            var result = await _runtime.RunAsync(spec, ct);

            if (result.TimedOut)
                return new AgentRunResult(AgentRunStatus.Failed, null, $"container timed out after {container.Timeout}");

            if (result.ExitCode != 0)
                return new AgentRunResult(AgentRunStatus.Failed, TrimForLog(result.Stdout), result.Stderr ?? "non-zero exit");

            return new AgentRunResult(AgentRunStatus.Success, TrimForLog(result.Stdout), null);
        }
        finally
        {
            try { Directory.Delete(runDir, true); }
            catch (Exception ex) { _log.LogWarning(ex, "Failed to clean up run dir {RunDir}", runDir); }
        }
    }

    private IReadOnlyList<ContainerMount> BuildMounts(string eventPath, ContainerAgentSpec container)
    {
        var mounts = new List<ContainerMount>
        {
            new(Path.Combine(_dirs.ConfigRoot, "agents", $"{_config.Id}.yaml"), "/skill/agent.yaml", ReadOnly: true),
            new(eventPath, "/event.json", ReadOnly: true),
            new(Path.Combine(_dirs.ConfigRoot, "secrets", "github-pat"), "/secrets/github-pat", ReadOnly: true),
        };

        var skillFilesDir = Path.Combine(_dirs.ConfigRoot, "agents", _config.Id);
        if (Directory.Exists(skillFilesDir))
            mounts.Add(new(skillFilesDir, "/skill/files", ReadOnly: true));

        if (container.Llm.ApiKeyEnv is not null)
        {
            var keyPath = Path.Combine(_dirs.ConfigRoot, "secrets", container.Llm.ApiKeyEnv);
            if (File.Exists(keyPath))
                mounts.Add(new(keyPath, "/secrets/llm-api-key", ReadOnly: true));
        }

        return mounts;
    }

    private static IReadOnlyDictionary<string, string> BuildEnv(IssueContext ctx, string runId) => new Dictionary<string, string>
    {
        ["GHKANBAN_RUN_ID"] = runId,
        ["GHKANBAN_GH_REPO"] = ctx.Issue.Repo,
        ["GHKANBAN_GH_ISSUE"] = ctx.Issue.Number.ToString(),
        ["GHKANBAN_LOG_FORMAT"] = "json",
    };

    private static string? TrimForLog(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        const int max = 2000;
        return s.Length <= max ? s : s[..max] + " …(truncated)";
    }
}
```

- [ ] **Step 3: Update `AgentRegistry.cs`**

Modify `AgentRegistry.Resolve` to handle the new `container` implementation. Look at the existing file; the current resolver uses `Type.GetType` to instantiate by string name. Container agents are constructed differently (they need the AgentConfig + IContainerRuntime + ContainerAgentDirs). Replace the existing `Resolve` method with:

```csharp
public IGHKanbanAgent Resolve(AgentConfig config)
{
    if (string.Equals(config.Implementation, "container", StringComparison.OrdinalIgnoreCase))
    {
        var runtime = (IContainerRuntime)_services.GetService(typeof(IContainerRuntime))
            ?? throw new InvalidOperationException("IContainerRuntime not registered");
        var dirs = (ContainerAgentDirs)_services.GetService(typeof(ContainerAgentDirs))
            ?? throw new InvalidOperationException("ContainerAgentDirs not registered");
        var loggerFactory = (ILoggerFactory)_services.GetService(typeof(ILoggerFactory))
            ?? throw new InvalidOperationException("ILoggerFactory not registered");
        var logger = loggerFactory.CreateLogger<ContainerAgent>();
        return new ContainerAgent(config.Name, config, runtime, dirs, logger);
    }

    // Existing in-process implementation lookup (Slice A path).
    var type = Type.GetType(config.Implementation)
               ?? FindInLoadedAssemblies(config.Implementation)
               ?? throw new InvalidOperationException($"Agent implementation not found: {config.Implementation}");

    var instance = ActivatorUtilities.CreateInstance(_services, type, config.Name);
    return (IGHKanbanAgent)instance;
}
```

Add the required usings at the top:

```csharp
using GHKanban.ContainerRuntime;
using Microsoft.Extensions.Logging;
```

- [ ] **Step 4: Run tests, commit**

```pwsh
dotnet run --project tests/GHKanban.Agents.Tests/
```

Expected: existing 6 Agents tests still pass + 2 new ContainerAgent tests.

```pwsh
git add src/GHKanban.Agents/ContainerAgent.cs src/GHKanban.Agents/AgentRegistry.cs tests/GHKanban.Agents.Tests/ContainerAgentTests.cs
git commit -m "feat(agents): ContainerAgent + AgentRegistry routes container impl (spec §3, §6)"
```

---

## Task 14: Control plane — Program.cs wiring + secret-file plumbing + first-run wizard update

**Files:**
- Modify: `src/GHKanban.Web/Program.cs`
- Modify: `src/GHKanban.Web/FirstRunWizard.cs`
- Test: existing `FirstRunWizardTests` will need a small update; otherwise add as we go

- [ ] **Step 1: Update `Program.cs`**

Open `src/GHKanban.Web/Program.cs`. After the existing `using` block, add:

```csharp
using GHKanban.ContainerRuntime;
```

After the `// SQLite state` block (around the existing `AgentRunStore` registration), insert:

```csharp
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
```

Append a new file `src/GHKanban.Web/SecretFilePlumbing.cs`:

```csharp
using GHKanban.Config;

namespace GHKanban.Web;

internal static class SecretFilePlumbing
{
    public static void WriteAll(string secretsDir, ConfigSnapshot snap)
    {
        Directory.CreateDirectory(secretsDir);
        ApplyDirectoryPermissions(secretsDir);

        // GitHub PAT (mandatory; written even if env var is empty so the mount exists)
        var pat = Environment.GetEnvironmentVariable(snap.GitHub.Auth.PatEnv) ?? "";
        var patPath = Path.Combine(secretsDir, "github-pat");
        File.WriteAllText(patPath, pat);
        ApplyFilePermissions(patPath);

        // Per-agent LLM API keys
        foreach (var agent in snap.Agents)
        {
            if (agent.Container?.Llm.ApiKeyEnv is { } envName)
            {
                var key = Environment.GetEnvironmentVariable(envName) ?? "";
                var keyPath = Path.Combine(secretsDir, envName);
                File.WriteAllText(keyPath, key);
                ApplyFilePermissions(keyPath);
            }
        }
    }

    private static void ApplyDirectoryPermissions(string dir)
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, default ACLs inherit from parent (user profile) — acceptable for v1.
            // Production-grade ACL hardening lives in a later slice.
            return;
        }
        try { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch { /* best-effort */ }
    }

    private static void ApplyFilePermissions(string file)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { /* best-effort */ }
    }
}
```

- [ ] **Step 2: Update `FirstRunWizard.cs`**

Open `src/GHKanban.Web/FirstRunWizard.cs`. The existing wizard creates `boards/` and `agents/` dirs. Add `secrets/` to the list:

```csharp
Directory.CreateDirectory(Path.Combine(configRoot, "secrets"));
```

After the line that creates the secrets directory, add (if not on Windows) a UnixFileMode `0700`:

```csharp
if (!OperatingSystem.IsWindows())
{
    try { File.SetUnixFileMode(Path.Combine(configRoot, "secrets"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    catch { /* best-effort */ }
}
```

Existing FirstRunWizardTests test that github.yaml/boards/example.yaml/agents/stub-ack.yaml are created — they remain valid. No new test needed (the secrets dir is created idempotently; tested in step 4 by the smoke run).

- [ ] **Step 3: Build everything**

```pwsh
dotnet build --no-restore
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Run all tests to confirm no regression**

```pwsh
Get-ChildItem tests -Directory | ForEach-Object {
  $out = dotnet run --project $_.FullName -c Debug --no-build 2>&1 | Out-String
  if ($out -match 'total:\s*(\d+)') { "$($_.Name): $($Matches[1])" }
}
```

Expected: every project still passes (no regression from B1 changes).

- [ ] **Step 5: Commit**

```pwsh
git add src/GHKanban.Web/Program.cs src/GHKanban.Web/FirstRunWizard.cs src/GHKanban.Web/SecretFilePlumbing.cs
git commit -m "feat(web): DI for IContainerRuntime + secret-file plumbing + wizard secrets dir (spec §5, §9)"
```

---

## Task 15: CI — publish-image job + image build verification

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Add the new job to `ci.yml`**

In `.github/workflows/ci.yml`, after the existing `publish` job, append:

```yaml
  publish-image:
    name: Publish agent image to GHCR
    needs: build
    # Only build/push on tag pushes (matches the publish-to-NuGet job's trigger).
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4

      - uses: docker/setup-qemu-action@v3
      - uses: docker/setup-buildx-action@v3

      - name: Log in to ghcr.io
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build + push agent image
        uses: docker/build-push-action@v6
        with:
          context: .                                    # repo root, so Directory.Packages.props is available
          file: src/GHKanban.AgentImage/Dockerfile
          push: true
          tags: |
            ghcr.io/jamesburton/ghkanban-agent:${{ github.ref_name }}
            ghcr.io/jamesburton/ghkanban-agent:latest
          platforms: linux/amd64,linux/arm64
```

Also extend the **build** job's `Test` step to include the two new test projects. Since the loop is already `for proj in tests/*Tests/*.csproj`, the new test projects (matching the same glob) are picked up automatically — no change needed.

- [ ] **Step 2: Verify on the next push (this is the actual test of the workflow)**

When the eventual tag push happens, `gh run watch <id>` will confirm the image was built + pushed. For this task, the commit + push that lands the workflow change triggers the regular `build` job (which now tests 9 projects total) — confirm that's green.

- [ ] **Step 3: Commit + push**

```pwsh
git add .github/workflows/ci.yml
git commit -m "ci: add publish-image job that builds + pushes ghcr.io/jamesburton/ghkanban-agent on tag (spec §10)"
git push
```

After push, watch the CI run:

```pwsh
sleep 8
gh run list --limit 1
gh run watch <run-id> --interval 15 --exit-status
```

Expected: green build + test job, no image-publish (no tag).

---

## Task 16: End-to-end smoke test — `provider: none` round trip

This is a manual + scripted verification rather than a unit test. It exercises the full container path with a real Docker daemon, the `none` provider (no LLM costs), and a real GitHub repo.

**Pre-requisites:**
- Docker Desktop / Docker Engine running locally
- `gh auth status` returns logged-in
- A test repo with at least one open issue (use `jamesburton/ghkanban-test` from Slice A validation if it still exists; otherwise create one with `gh repo create jamesburton/ghkanban-test --private`)

- [ ] **Step 1: Pack + install the tool from current source**

```pwsh
dotnet pack src/GHKanban.Web/ -c Release -o ./artifacts
dotnet tool uninstall -g GHKanban -ErrorAction SilentlyContinue
dotnet tool install -g --add-source (Resolve-Path .\artifacts).Path GHKanban --prerelease
```

- [ ] **Step 2: Back up any existing `~/.ghkanban` and reset**

```pwsh
if (Test-Path "$env:USERPROFILE\.ghkanban") {
    Rename-Item "$env:USERPROFILE\.ghkanban" "$env:USERPROFILE\.ghkanban.backup-b1-$(Get-Date -Format yyyyMMddHHmmss)"
}
```

- [ ] **Step 3: Launch once to trigger first-run wizard**

```pwsh
$env:GHKANBAN_PAT = (gh auth token)
Start-Process ghkanban -RedirectStandardOutput "$env:TEMP\ghkanban-b1-stdout.log" -RedirectStandardError "$env:TEMP\ghkanban-b1-stderr.log"
Start-Sleep -Seconds 4
Get-Process ghkanban | Stop-Process -Force
```

- [ ] **Step 4: Build the agent image locally**

```pwsh
docker build -f src/GHKanban.AgentImage/Dockerfile -t ghkanban-agent:dev .
```

Expected: image built successfully (named `ghkanban-agent:dev`).

- [ ] **Step 5: Write a container-mode agent config with `provider: none`**

Write `C:\Users\<you>\.ghkanban\agents\stub-container.yaml`:

```yaml
name: Stub Container
implementation: container
triggers:
  - on: issue.labeled
    when: has-label("ai-pls")
container:
  image: ghkanban-agent:dev
  llm:
    provider: none
  prompt:
    user: |
      🤖 Container-mode stub triggered on {{trigger.event}} for {{issue.repo}}#{{issue.number}}.
      Title: {{issue.title}}
  tools:
    - github.post-comment
  timeout: 30s
```

Point `boards/example.yaml` at your test repo:

```yaml
name: B1 Validation
scope:
  repos: [jamesburton/ghkanban-test]
columns:
  - name: All open
    rule: state == "open"
```

Set short poll interval in `github.yaml`:

```yaml
poll-interval: 10s
reconcile-interval: 60s
```

- [ ] **Step 6: Relaunch and trigger**

```pwsh
$env:GHKANBAN_PAT = (gh auth token)
Start-Process ghkanban -RedirectStandardOutput "$env:TEMP\ghkanban-b1-stdout.log" -RedirectStandardError "$env:TEMP\ghkanban-b1-stderr.log"
Start-Sleep -Seconds 12  # let it poll once

# Pick an issue to label
$issueNum = (gh issue list --repo jamesburton/ghkanban-test --state open --limit 1 --json number | ConvertFrom-Json)[0].number
gh issue edit $issueNum --repo jamesburton/ghkanban-test --remove-label ai-pls 2>&1 | Out-Null
Start-Sleep -Seconds 12

# Add ai-pls to trigger the agent
gh issue edit $issueNum --repo jamesburton/ghkanban-test --add-label ai-pls

# Watch for the comment (up to 30s)
$start = Get-Date
for ($i = 0; $i -lt 15; $i++) {
    Start-Sleep -Seconds 2
    $comments = gh issue view $issueNum --repo jamesburton/ghkanban-test --json comments --jq '.comments[].body'
    if ($comments -match 'Container-mode stub triggered') {
        $elapsed = ((Get-Date) - $start).TotalSeconds
        "PASS — comment posted after $([int]$elapsed)s"
        break
    }
}
```

Expected: comment posted within ~30 seconds. Body matches the rendered template (issue.repo, issue.number, issue.title interpolated).

- [ ] **Step 7: Inspect cleanup**

```pwsh
docker ps -a --filter "label=ghkanban=true"
```

Expected: container exited; ContainerAgent's `finally` block deleted the run dir. Container itself was removed by the `DockerContainerRuntime.RunAsync` finally. After 30+ minutes (or by manually invoking the Janitor), no orphan containers.

- [ ] **Step 8: Stop the app + record outcome**

```pwsh
Get-Process ghkanban | Stop-Process -Force
```

If the smoke test passed cleanly, no commit needed for this task — it's pure verification. If you hit a bug, fix and commit per normal.

---

## Task 17: End-to-end smoke test — `provider: anthropic` with a real LLM

**Pre-requisites:**
- Same as Task 16, plus an Anthropic API key in `ANTHROPIC_API_KEY` env var

- [ ] **Step 1: Add a summariser agent config**

Write `C:\Users\<you>\.ghkanban\agents\summariser.yaml`:

```yaml
name: Summariser
implementation: container
triggers:
  - on: issue.opened
    when: not has-label("nosummary")
container:
  image: ghkanban-agent:dev
  llm:
    provider: anthropic
    model: claude-sonnet-4-6
    api-key-env: ANTHROPIC_API_KEY
  prompt:
    user: |
      Summarise this GitHub issue in 2-3 sentences. Focus on what the user wants and why.

      Title: {{issue.title}}
      Body: {{issue.body}}
  tools:
    - github.post-comment
  timeout: 60s
```

Set the env var BEFORE launching the app (the control plane materialises it to a secrets file at startup):

```pwsh
$env:GHKANBAN_PAT = (gh auth token)
$env:ANTHROPIC_API_KEY = "<your key>"
Start-Process ghkanban -RedirectStandardOutput "$env:TEMP\ghkanban-b1-stdout.log" -RedirectStandardError "$env:TEMP\ghkanban-b1-stderr.log"
Start-Sleep -Seconds 4
```

- [ ] **Step 2: Trigger a new issue**

```pwsh
$result = gh issue create --repo jamesburton/ghkanban-test `
    --title "Login button is unresponsive on Safari" `
    --body "When I click the Login button in Safari 17, nothing happens. The button briefly shows a focus ring but no network request is made. Chrome and Firefox both work."

# Capture issue number
$num = ($result -split '/')[-1]
"Created issue #$num — watching for summariser comment"
```

- [ ] **Step 3: Wait + verify the summary appears**

```pwsh
$start = Get-Date
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    $comments = gh issue view $num --repo jamesburton/ghkanban-test --json comments --jq '.comments[].body'
    if ($comments -and $comments.Length -gt 30) {
        "Comment received after $([int]((Get-Date) - $start).TotalSeconds)s:"
        ($comments -split "`n") | Select-Object -First 6 | ForEach-Object { "  $_" }
        break
    }
}
```

Expected: 2-3 sentence summary posted within 60s. Sentences reflect that the issue is about Safari-specific Login button behaviour.

- [ ] **Step 4: Stop the app**

```pwsh
Get-Process ghkanban | Stop-Process -Force
```

No commit unless a bug was found and fixed.

---

## Task 18: Concurrency stress test (acceptance criterion 11)

- [ ] **Step 1: With the app + stub-container.yaml from Task 16 in place, label 10 issues in parallel**

```pwsh
$env:GHKANBAN_PAT = (gh auth token)
Start-Process ghkanban -RedirectStandardOutput "$env:TEMP\ghkanban-b1-stress.log" -RedirectStandardError "$env:TEMP\ghkanban-b1-stress-err.log"
Start-Sleep -Seconds 8

# Create 10 fresh issues
$issues = 1..10 | ForEach-Object {
    $r = gh issue create --repo jamesburton/ghkanban-test --title "Stress test $_" --body "Stress test body $_"
    ($r -split '/')[-1]
}

# Remove ai-pls from any (defensive)
$issues | ForEach-Object { gh issue edit $_ --repo jamesburton/ghkanban-test --remove-label ai-pls 2>&1 | Out-Null }
Start-Sleep -Seconds 15  # baseline poll

# Add ai-pls to all 10 in parallel
$jobs = $issues | ForEach-Object {
    $num = $_
    Start-Job -ScriptBlock { param($n) gh issue edit $n --repo jamesburton/ghkanban-test --add-label ai-pls } -ArgumentList $num
}
$jobs | Wait-Job | Receive-Job | Out-Null

# Watch for all 10 comments
$start = Get-Date
$done = 0
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $done = ($issues | ForEach-Object {
        $c = gh issue view $_ --repo jamesburton/ghkanban-test --json comments --jq '.comments[].body'
        if ($c -match 'Container-mode stub triggered') { 1 } else { 0 }
    } | Measure-Object -Sum).Sum
    if ($done -eq 10) { "All 10 comments posted after $([int]((Get-Date) - $start).TotalSeconds)s"; break }
}
if ($done -ne 10) { "FAIL: only $done of 10 comments posted" }

# Cleanup
Get-Process ghkanban | Stop-Process -Force
$issues | ForEach-Object { gh issue close $_ --repo jamesburton/ghkanban-test 2>&1 | Out-Null }
```

Expected: 10/10 comments posted. No `agent_runs` records lost. No leftover containers (`docker ps -a --filter label=ghkanban=true` should show zero, or only janitor-pending entries < 30 min old).

If FAIL: investigate the logs (`$env:TEMP\ghkanban-b1-stress.log`) for race conditions in `IContainerRuntime.RunAsync` or `ContainerAgent.TriggerAsync`. Likely culprits: shared mutable state, file path collisions in `runs/`, Docker socket connection limits.

---

## Task 19: Acceptance-criteria final pass

Run each of the 11 acceptance criteria from spec §13 and record pass/fail with evidence. Fix any failures before final commit.

| # | Criterion | How to verify |
|---|---|---|
| 1 | stub-container with `provider: none` posts templated comment in <30s | Task 16 |
| 2 | summariser with `provider: anthropic` posts 2-3 sentence summary in <60s | Task 17 |
| 3 | `agent_runs` entries appear in `/activity` with run id, status, log excerpt | Visit `http://localhost:5454/activity` after Tasks 16/17 |
| 4 | No orphan containers > 30 min after several invocations | `docker ps -a --filter label=ghkanban=true` |
| 5 | Container timeout: agent killed when timeout exceeded; next trigger works | Set `timeout: 1s` on stub-container.yaml, retrigger, verify Failed status |
| 6 | Image builds for amd64+arm64 and is publicly pullable | Will verify on actual tag push; manual: `docker build` succeeds locally |
| 7 | `IContainerRuntime` is a clean interface; ContainerAgentTests use a fake | Confirmed by Task 13's test using `Substitute.For<IContainerRuntime>()` |
| 8 | All Slice A acceptance criteria still pass | Run all test projects; verify board/activity/config pages render |
| 9 | `dnx GHKanban --prerelease` works on clean machine; absent Docker → clear error not crash | Manual: install on clean machine; uninstall Docker, attempt container-mode agent — should log error, not crash |
| 10 | Full build clean + all tests pass under MTP | `dotnet build -warnaserror` + each test project via `dotnet run --project` |
| 11 | N=10 concurrent triggers produce N runs, no race conditions | Task 18 |

- [ ] **Step 1: Walk the criteria**

For each row, run the verification and record PASS/FAIL with one-line evidence.

- [ ] **Step 2: Fix any failures inline**

If a criterion fails: identify root cause, fix, retest, commit the fix. Don't move on until that criterion passes.

- [ ] **Step 3: Restore the original `~/.ghkanban`**

```pwsh
Get-Process ghkanban -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet tool uninstall -g GHKanban
Remove-Item "$env:USERPROFILE\.ghkanban" -Recurse -Force
$backup = Get-ChildItem $env:USERPROFILE -Filter ".ghkanban.backup-b1-*" -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($backup) { Rename-Item $backup.FullName "$env:USERPROFILE\.ghkanban" }
```

- [ ] **Step 4: Final commit if any fixes were needed**

```pwsh
git add .
git commit -m "chore: B1 acceptance pass — all spec §13 criteria met"
git push
```

If no fixes were needed, don't create an empty commit — just summarise the results.

- [ ] **Step 5: Tag the release**

```pwsh
git tag v0.2.0-alpha -m "v0.2.0-alpha — Slice B1: containerised agents"
git push origin v0.2.0-alpha
```

This triggers both `publish` (to NuGet) and `publish-image` (to GHCR) jobs. Watch:

```pwsh
sleep 8
gh run list --limit 1
gh run watch <id> --interval 15 --exit-status
```

Expected: both jobs green. Image visible at `https://github.com/jamesburton/GHKanban/pkgs/container/ghkanban-agent`.

---

## Self-Review (run by plan author, completed inline before save)

**1. Spec coverage:**

| Spec section | Implemented by |
|---|---|
| §1 Goal | All tasks combined |
| §2 Architecture | Tasks 2-5 (runtime), 11 (image), 13 (control-plane wrapper) |
| §3 Repo structure | Task 1 (scaffolding); subsequent tasks fill each file |
| §4 YAML schema | Tasks 6 (control-plane), 7 (agent-image) |
| §5 Mounts + env + exit codes | Tasks 11 (image entrypoint), 13 (mount construction) |
| §6 Spawn flow + Janitor | Tasks 4 (Docker REST), 5 (Janitor), 13 (orchestration) |
| §7 Inside container | Task 11 |
| §8 LLM provider abstraction | Task 9 |
| §9 Secrets + trust model | Tasks 14 (secret-file plumbing), 13 (mount paths) |
| §10 CI publish-image | Task 15 |
| §11 Non-goals | Not in any task (correctly excluded) |
| §12 Tech constraints | Task 1 (versions) |
| §13 Acceptance criteria | Tasks 16-19 |
| §14 Open questions | Out of scope for B1 (correctly) |

No gaps.

**2. Placeholder scan:**

Searched for "TBD", "TODO", "implement later", "appropriate error handling". The Task 9 caveat about Anthropic.SDK / OpenAI SDK API surface drift is an acknowledged-risk disclosure (the implementer is told what to do if APIs differ), not a placeholder. Same for the Task 12 Dockerfile alternative for `Directory.Packages.props`.

**3. Type / name consistency:**

- `ContainerSpec(Image, Mounts, Env, Labels, Timeout, CpuLimit, MemoryBytes)` consistent across Tasks 2, 4, 13 ✓
- `ContainerMount(HostPath, ContainerPath, ReadOnly)` consistent across Tasks 2, 4, 13 ✓
- `ContainerRunResult(ExitCode, Stdout, Stderr, TimedOut)` consistent across Tasks 2, 4, 13 ✓
- `IContainerRuntime.RunAsync(ContainerSpec, CT) → Task<ContainerRunResult>` consistent across Tasks 3, 4, 5, 13 ✓
- `IContainerRuntime.ListLabeledAsync(string, string, CT) → Task<IReadOnlyList<ContainerHandle>>` consistent across Tasks 3, 5 ✓
- `ContainerHandle(Id, State, FinishedAt)` consistent across Tasks 3, 5 ✓
- `SkillConfig(Llm, Prompt, Tools)` consistent across Tasks 7, 9, 11 ✓
- `SkillLlm(Provider, Model)` (no ApiKeyEnv — image gets key from /secrets/llm-api-key file) consistent across Tasks 7, 9, 11 ✓
- `ContainerAgentSpec(Image, Llm, Prompt, Tools, Timeout, CpuLimit, MemoryBytes)` consistent across Tasks 6, 13 ✓
- `ContainerLlmSpec(Provider, Model, ApiKeyEnv)` (control-plane side needs ApiKeyEnv to know which env var to materialise) consistent across Tasks 6, 13, 14 ✓
- `ContainerAgentDirs(ConfigRoot, RunsRoot)` consistent across Tasks 13, 14 ✓
- `IAgentTool(Name, InvokeAsync)` consistent across Tasks 10, 11 ✓

Note on the asymmetry between control-plane `ContainerLlmSpec` (has `ApiKeyEnv`) and image-side `SkillLlm` (no `ApiKeyEnv`): this is intentional. The control plane needs the env var name to materialise the key to disk before container spawn. The image-side parser only ever sees the resolved key file at `/secrets/llm-api-key` — it doesn't need to know the env var name. Both POCOs parse the same YAML; they just extract different subsets.

Clean.

**Total tasks:** 19. One commit per task (except Tasks 16-18 which are verification-only and only commit if a bug is fixed). Smoke-runnable agent after Task 14; full container path verified by Task 16 (none) and Task 17 (anthropic).
