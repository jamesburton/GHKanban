# GHKanban — A Survey of GitHub-Backed Kanban Tools and AI-Agent Integrations

> **Status:** Draft survey (started 2026-05-23). Each row's "Last verified" cell is the source-of-truth on freshness.

## 1. Purpose & method

This document surveys existing attempts to provide a kanban view over GitHub-hosted projects and their issues, with particular attention to AI-agent integration for monitoring, reviewing, and resolving issues. The goal is a per-tier recommendation (Personal / Small Team / OSS Maintainer / Enterprise) and an explicit build-or-buy verdict.

Tools were verified by fetching each tool's official homepage and features/docs (or repo README for open-source projects). AI-integration claims were cross-checked against the vendor's actual integration page — third-party blog posts are not cited. Cells with non-obvious values carry `[ref-…]` footnotes resolving to the Sources section at the bottom. Tools that failed GitHub-sync verification are recorded in §6, not silently dropped.

## 2. Requirements summary

**Tiers evaluated:**
- **Personal / Solo** — one developer, many own repos across orgs
- **Small Team (2–10)** — shared board, basic permissions
- **OSS Maintainer** — public repos, high-volume inbound from strangers
- **Enterprise / Multi-team** — SSO/SAML, RBAC, audit, hundreds of repos

**Must-haves (filter columns in Table 1):**
- Cross-repo board (single board across many repos)
- GitHub as source of truth (no shadow DB)
- PR ↔ Issue linking & status sync
- Self-hostable / open source

**AI-agent capabilities tracked as distinct columns:**
- Triage / classification / labelling
- Reproduction & diagnosis
- Autonomous resolution (code PRs)
- PR review & code-quality feedback

## 3. Capability matrix (Table 1)

> Encoding: `✅` present · `🟡` partial/conditional · `❌` absent · `❓` unknown after good-faith verification.
> AI capability cells: `✅` for native or officially-documented integration; `🟡` for unofficial bridges only; footnote names the partner when integration-based.

<!-- TABLE 1 ROWS APPENDED IN TASKS 2-8 -->

| Tool | License | Hosting | Primary surface | Cross-repo | GH as truth | Self-host | PR↔Issue | Triage | Repro | Autonomous PR | PR review | RBAC | Extensibility | Last verified |
|------|---------|---------|-----------------|------------|-------------|-----------|----------|--------|-------|---------------|-----------|------|---------------|---------------|
| [GitHub Projects v2](https://github.com/features/issues) | Commercial[^ref-gh-projects-docs] | SaaS (GHES self-host)[^ref-ghes] | Native GH board | ✅ | ✅ | 🟡 (GHES only) | ✅ | ❌ | ❌ | ✅[^ref-copilot-coding-agent] | ✅[^ref-copilot-code-review] | ✅ | ✅ | 2026-05-23 |
| [GitHub Issues](https://docs.github.com/en/issues) | Commercial[^ref-gh-issues-docs] | SaaS (GHES self-host)[^ref-ghes] | Issues list (no board) | 🟡 (search/API cross-repo; no board) | ✅ | 🟡 (GHES only) | 🟡 (keyword linking; no board state sync) | ❌ | ❌ | ✅[^ref-copilot-coding-agent] | ✅[^ref-copilot-code-review] | 🟡 (repo-level only) | ✅ | 2026-05-23 |
| [ZenHub](https://www.zenhub.com/) | Commercial (Freemium)[^ref-zenhub-pricing] | SaaS + Self-host (Enterprise on-prem)[^ref-zenhub-enterprise] | GitHub-overlay board (browser ext / GitHub App) | 🟡 (within workspace; Enterprise up to 10 GH orgs)[^ref-zenhub-workspaces] | 🟡 (GH issues canonical; sprints, epics, estimates in ZenHub DB)[^ref-zenhub-api] | ✅[^ref-zenhub-enterprise] | ✅ | ✅[^ref-zenhub-ai] | ❌ | ❌ | ❌ | ✅[^ref-zenhub-enterprise] | ✅[^ref-zenhub-api] | 2026-05-23 |
| [Waffle.io](https://waffle.io/) (defunct) | Commercial (defunct)[^ref-waffle-shutdown] | SaaS (defunct) | GitHub-overlay board (defunct) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 2026-05-23 |

## 4. Tier-fit matrix (Table 2)

**Scoring rules:**
- **❌ Hard fail:** missing a must-have essential at this tier (e.g. no cross-repo at OSS-maintainer tier; no RBAC at enterprise tier).
- **🟡 Workable with caveats:** all must-haves present but notable friction (wrong pricing model for tier, AI requires separate agent platform, self-hosting is community-edition only).
- **✅ Good fit:** all must-haves + at least one AI capability natively or via documented first-party integration, and pricing/deployment model matches the tier.

**Tier-specific weights** (what tips 🟡 to ❌):
- **Personal:** simple setup; free or near-free; works for one person across orgs.
- **Small team:** affordable seat pricing OR self-hostable without ops burden; basic permissions.
- **OSS maintainer:** high-volume inbound triage weighted heavily; works on public repos.
- **Enterprise:** SSO/SAML, audit logs, RBAC, SOC2-or-equivalent; scales to hundreds of repos.

<!-- TABLE 2 ROWS APPENDED IN TASKS 2-8 -->

| Tool | Personal / Solo | Small Team (2–10) | OSS Maintainer | Enterprise / Multi-team |
|------|------------------|---------------------|-----------------|---------------------------|
| GitHub Projects v2 | ✅ Free tier; cross-repo board; Copilot AI available | ✅ Org projects; native AI review; reasonable seat pricing | 🟡 No auto-triage; high-volume labelling remains manual | 🟡 GHE plan needed for SSO/SAML; Projects feature itself lacks enterprise auth |
| GitHub Issues | 🟡 Simple and free; no board or cross-repo aggregation | 🟡 Adequate for basics; no shared board view | ❌ No cross-repo board; high-volume manual triage does not scale | ❌ No project-level RBAC, audit logs, or board |
| ZenHub | 🟡 Free tier capped at 2 repos; paid plan needed | ✅ Teams plan; cross-repo board; AI labels native | 🟡 Cross-repo board within workspace; no auto-routing triage | ✅ Enterprise on-prem; SSO/SAML; RBAC; audit logs |
| Waffle.io | ❌ Service defunct; domain parked | ❌ Service defunct; domain parked | ❌ Service defunct; domain parked | ❌ Service defunct; domain parked |

## 5. Tool deep dives

<!-- POPULATED IN TASK 10 — one short subsection per tool scoring ✅ or 🟡 on at least one tier -->

## 6. Excluded — no GitHub sync (verified)

<!-- POPULATED INLINE IN TASKS 2-8 — tools whose verification fell short -->

## 7. AI-Agent Appendix (Table 3)

> Same encoding as Table 1. The "Board integrations" column is the bridge back to the tool landscape.

<!-- TABLE 3 ROWS APPENDED IN TASK 9 -->

| Agent | Vendor / License | Surface | Triage | Repro & diagnosis | Autonomous resolution | PR review | Board integrations | Pricing model | Last verified |
|-------|------------------|---------|--------|-------------------|------------------------|-----------|---------------------|---------------|---------------|

## 8. Recommendation per tier

<!-- POPULATED IN TASK 11 — one block per tier + cross-tier finding -->

## 9. Technology constraints (if we build)

Applies only if the recommendation rubric concludes that building is justified.

- **Runtime:** .NET 10 (C#).
- **Test framework:** xUnit v3 with Microsoft Testing Platform (MTP).
- **Packaging:** publish as `dotnet tool` packages; auto-version + auto-publish to NuGet from CI.
- **Primary invocation:** `dnx` zero-install execution is the headline UX; `dotnet tool install -g` is a secondary path.
- **Non-.NET dependencies (if any):** orchestrate via .NET Aspire.
- **AI orchestration:** prefer Microsoft Agent Framework where its features fit; fall back to direct SDK calls only when the framework can't express the workflow cleanly.
- **GitHub authentication:** use `gh` CLI's stored token where available; otherwise document storing PATs in the OS secret store (DPAPI on Windows, libsecret on Linux, Keychain on macOS). Multiple logins must be supported via simple toggle or rapid logout/login swap.
- **UI:** Blazor (Server or WASM — to be decided during planning of the build itself).

## 10. Sources

<!-- POPULATED INCREMENTALLY THROUGHOUT TASKS 2-9 -->

[^ref-gh-projects-docs]: https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects — accessed 2026-05-23
[^ref-gh-issues-docs]: https://docs.github.com/en/issues/tracking-your-work-with-issues/about-issues — accessed 2026-05-23
[^ref-ghes]: https://github.com/enterprise — accessed 2026-05-23
[^ref-copilot-coding-agent]: https://docs.github.com/en/copilot/using-github-copilot/using-copilot-coding-agent-to-work-on-tasks — accessed 2026-05-23; GitHub Copilot Coding Agent (official GitHub Copilot feature): researches a repository, creates implementation plans, makes code changes on a branch, and opens a pull request for review. Autonomous PR creation is documented; triage/labelling and repro are not.
[^ref-copilot-code-review]: https://docs.github.com/en/copilot/using-github-copilot/code-review/using-copilot-code-review — accessed 2026-05-23; GitHub Copilot Code Review (official native GitHub feature): reviews pull requests and provides inline suggestions; triggered via Reviewers panel, CLI (`--reviewer @copilot`), or IDE integration.
[^ref-zenhub-pricing]: https://www.zenhub.com/pricing — accessed 2026-05-23; Freemium model: Free (2 repos, 50 users), Teams ($4.99/user/month billed annually), Enterprise (custom pricing, self-hosted option available).
[^ref-zenhub-enterprise]: https://www.zenhub.com/enterprise — accessed 2026-05-23; ZenHub Enterprise Server currently sold; supports Kubernetes and VM deployments (AWS EC2, VMware, Hyper-V, Azure, KVM, GCP); features SSO/SAML/LDAP, custom roles and permissions, audit logs, up to 10 connected GitHub organizations.
[^ref-zenhub-workspaces]: https://www.zenhub.com/workspaces — accessed 2026-05-23; A workspace aggregates issues from multiple GitHub repositories into a single board; boards, reports, and roadmaps are workspace-scoped; cross-workspace aggregation is not supported.
[^ref-zenhub-api]: https://www.zenhub.com/developers — accessed 2026-05-23; ZenHub GraphQL API allows programmatic updates to sprints, epics, and estimates — these are ZenHub-native constructs stored in ZenHub's own database, not in GitHub issues; GitHub issues remain the canonical source for issue content.
[^ref-zenhub-ai]: https://www.zenhub.com/ai — accessed 2026-05-23; Native ZenHub AI features: AI Labels (suggests labels from issue data), AI Sprint Reviews (auto-generates sprint summaries), AI Acceptance Criteria (suggests acceptance criteria from issue description); no specific AI partner disclosed; ZenHub uses commercial and fine-tuned open-source models.
[^ref-waffle-shutdown]: https://github.com/waffleio — accessed 2026-05-23; GitHub organization archived by administrator on 2025-07-24, marked as no longer maintained; homepage waffle.io is a parked domain for sale.
