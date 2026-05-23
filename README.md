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
| [Linear](https://linear.app/) | Commercial (Freemium)[^ref-linear-pricing] | SaaS only | 3rd-party PM board | ✅[^ref-linear-gh] | ❌[^ref-linear-gh] | ❌ | ✅[^ref-linear-gh] | ✅[^ref-linear-ai] | ❌ | 🟡[^ref-linear-agents] | ❌ | ✅[^ref-linear-pricing] | ✅[^ref-linear-api] | 2026-05-23 |
| [Jira](https://www.atlassian.com/software/jira) | Commercial[^ref-jira-pricing] | SaaS + 🟡 Data Center (end-of-sale Mar 2026)[^ref-jira-pricing] | 3rd-party PM board | ✅ | ❌[^ref-jira-gh-app] | 🟡 (Data Center; end-of-sale)[^ref-jira-pricing] | ✅[^ref-jira-gh-app] | 🟡[^ref-jira-ai] | ❌ | ❌ | ❌ | ✅ | ✅[^ref-jira-gh-app] | 2026-05-23 |
| [Shortcut](https://www.shortcut.com/) | Commercial (Freemium)[^ref-shortcut-pricing] | SaaS only | 3rd-party PM board | ✅ | ❌[^ref-shortcut-gh] | ❌ | ✅[^ref-shortcut-gh] | 🟡[^ref-shortcut-korey] | ❌ | ❌ | ❌ | ✅[^ref-shortcut-pricing] | ✅[^ref-shortcut-api] | 2026-05-23 |
| [Height](https://height.app/) (defunct) | Commercial (defunct)[^ref-height-shutdown] | SaaS (defunct) | 3rd-party PM board (defunct) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 2026-05-23 |
| [Trello](https://trello.com/) | Commercial (Freemium)[^ref-trello-pricing] | SaaS only[^ref-trello-pricing] | 3rd-party kanban board | ❌[^ref-trello-gh-powerup] | ❌[^ref-trello-gh-powerup] | ❌[^ref-trello-pricing] | ❌[^ref-trello-gh-powerup] | ❌ | ❌ | ❌ | ❌ | ✅[^ref-trello-pricing] | ✅[^ref-trello-gh-powerup] | 2026-05-23 |
| [Notion](https://www.notion.com/) | Commercial (Freemium)[^ref-notion-pricing] | SaaS only[^ref-notion-pricing] | 3rd-party docs/DB workspace | 🟡 (one repo per synced DB; multi-repo aggregation unconfirmed)[^ref-notion-gh-sync] | ✅[^ref-notion-gh-sync] | ❌[^ref-notion-pricing] | ✅ (one-way GH→Notion; PR status auto-updates)[^ref-notion-gh-sync] | ❌ | ❌ | ❌ | ❌ | ✅[^ref-notion-pricing] | ✅[^ref-notion-gh-sync] | 2026-05-23 |
| [YouTrack](https://www.jetbrains.com/youtrack/) | Commercial (Freemium)[^ref-youtrack-pricing] | SaaS + Self-host[^ref-youtrack-pricing] | 3rd-party PM board | ✅ | ❌[^ref-youtrack-gh] | ✅[^ref-youtrack-pricing] | ✅[^ref-youtrack-gh] | ❌ | ❌ | ❌ | ❌ | ✅[^ref-youtrack-pricing] | ✅[^ref-youtrack-gh] | 2026-05-23 |

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
| Linear | 🟡 Free capped at 250 issues; Triage AI needs Business plan | 🟡 Affordable per-seat; SaaS-only limits self-hosted teams | ❌ Per-seat SaaS pricing unviable for anonymous public contributors | ✅ Enterprise SAML/SCIM; strong cross-repo board and AI triage |
| Jira | ❌ Per-seat pricing; 10-user free cap; overkill for solo use | 🟡 Standard plan includes Rovo AI; SaaS-only for new buys | ❌ Per-seat SaaS; no triage AI on free tier; OSS unscalable | ✅ Enterprise plan; SSO/SAML; RBAC; audit; 3000+ integrations |
| Shortcut | 🟡 Free tier 10 users; no cross-repo verified; SaaS only | 🟡 Affordable Team plan; SaaS-only; basic RBAC | ❌ Per-seat SaaS; Korey triage unverified on public repos | ✅ Enterprise SSO/SAML/SCIM; SOC 2; strong GitHub sync |
| Height | ❌ Service defunct since September 2025 | ❌ Service defunct since September 2025 | ❌ Service defunct since September 2025 | ❌ Service defunct since September 2025 |
| Trello | 🟡 Free tier; Power-Up links GH items; no GH-issue board | ❌ No GH-issue board or PR↔card sync; SaaS only | ❌ No cross-repo GH board; no triage; per-seat scales poorly | 🟡 Enterprise SSO via separate Atlassian Guard; no GH board |
| Notion | 🟡 Synced DB free-tier limited; AI generic; no two-way GH writes | ❌ Synced DB requires Business plan; no issue tracking board | ❌ No GH-issue board; no triage AI; Business plan per-seat costly | 🟡 Enterprise SSO/SAML/SCIM/audit; but no GH-native board or triage |
| YouTrack | ✅ Free ≤10 users; self-host option; Kanban board; GH commit link | ✅ Free ≤10 users or self-hosted; cross-repo board; GH PR display | 🟡 Cross-repo board; PR display in issues; no triage AI | ✅ Cloud + on-prem; SSO/SAML; RBAC; GH VCS integration |

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
[^ref-linear-pricing]: https://linear.app/pricing — accessed 2026-05-23; Freemium model: Free (unlimited members, 2 teams, 250 issues), Basic ($10/user/month annual, unlimited issues), Business ($16/user/month annual, Triage Intelligence, Linear Agent automations), Enterprise (custom, SAML/SCIM). Triage Intelligence and advanced AI features require Business plan or above.
[^ref-linear-gh]: https://linear.app/docs/github — accessed 2026-05-23; Linear GitHub integration: PR↔Issue sync is bidirectional — issues auto-update from In Progress to Done as PRs move from draft to merged; multiple repos can connect to one Linear team for one-way sync; only one repo per team supports two-way sync at a time; Linear stores teams, cycles, projects, and priorities natively in its own database, making Linear (not GitHub) the primary source of truth.
[^ref-linear-ai]: https://linear.app/ai — accessed 2026-05-23; Triage Intelligence is a native Linear feature (Business plan+): auto-suggests and applies assignees, labels, teams, and projects based on historical patterns; also performs duplicate detection and links similar issues. No external AI partner is required for triage; a separate Linear MCP layer connects Cursor, Claude, and ChatGPT for agent workflows.
[^ref-linear-agents]: https://linear.app/integrations/github — accessed 2026-05-23; "Linear for Agents" and the Linear Agent platform allow AI agents (including GitHub Copilot, Cursor, Claude Code, and Codex) to work on issues end-to-end and create pull requests; autonomous PR creation is via officially documented integrations with named third-party agent platforms, not a native Linear code-execution feature.
[^ref-linear-api]: https://linear.app/developers — accessed 2026-05-23; Linear exposes a GraphQL API and webhooks for programmatic workspace access; MCP server support enables connecting AI tools (Cursor, Claude, ChatGPT) directly to a Linear workspace.
[^ref-jira-pricing]: https://www.atlassian.com/software/jira/pricing — accessed 2026-05-23; Jira Cloud plans: Free (up to 10 users), Standard ($7.91/user/month, includes Rovo AI), Premium ($14.54/user/month), Enterprise (custom). Data Center (self-managed) option existed but Atlassian ended new Data Center license sales on 2026-03-30; existing Data Center customers continue on support.
[^ref-jira-gh-app]: https://marketplace.atlassian.com/apps/1219592/github-for-jira — accessed 2026-05-23; GitHub for Jira app: listens to GitHub webhook events and syncs PRs, branches, builds, commits, and deployments into Jira in real time; supports GitHub Cloud, GitHub Enterprise Cloud, and GitHub Enterprise Server with unlimited orgs and repos; Jira is the primary project database — GitHub issues are not mirrored back; Smart Commits allow Jira transitions from commit messages.
[^ref-jira-ai]: https://support.atlassian.com/organization-administration/docs/atlassian-intelligence-features-in-jira-software/ — accessed 2026-05-23; Rovo AI in Jira (Standard plan+): auto-assign suggestions, child work item generation from parent descriptions, natural-language JQL, automation rule generation, comment summarisation, and similar-issue linking; full triage automation (route, classify, label) is not explicitly documented as a discrete feature — Rovo Agents can be configured for workflow automation, but this is not a zero-config native capability.
[^ref-shortcut-pricing]: https://www.shortcut.com/pricing — accessed 2026-05-23; Freemium model: Free (10 users, 1 team), Team ($8.50/user/month annual, unlimited users), Business ($12/user/month annual, unlimited workspaces), Enterprise (custom, SSO/SAML, SCIM, SOC 2 Type II). SSO/SAML available as add-on for Team/Business; SCIM is Enterprise-only.
[^ref-shortcut-gh]: https://www.shortcut.com/integrations/github — accessed 2026-05-23; Shortcut GitHub integration attaches commits, branches, and PRs to Stories; PR lifecycle events (opened, approved, merged) auto-advance Story workflow states; Shortcut is the primary project database — issues originate in Shortcut and GitHub activity is reflected back; cross-repo: Stories are repo-agnostic so a single board can reference PRs from multiple repositories.
[^ref-shortcut-korey]: https://www.shortcut.com/korey — accessed 2026-05-23; Korey is Shortcut's native AI product-management agent: converts unstructured input (Slack messages, verbal descriptions) into properly formatted Stories with acceptance criteria, assigns work to humans or AI teammates, and generates status summaries; no native auto-labelling triage or PR-review capability is documented; autonomous PR creation is not a Korey feature.
[^ref-shortcut-api]: https://www.shortcut.com/integrations — accessed 2026-05-23; Shortcut REST API v3 supports full programmatic access; MCP server available to connect Cursor, Claude Code, and other AI tools directly to a Shortcut workspace; 80+ native integrations including Slack, GitHub, GitLab, Figma, and Sentry.
[^ref-height-shutdown]: https://x.com/height_app/status/1903820182557999555 — accessed 2026-05-23; Height officially shut down on 2025-09-24 after a public announcement in March 2025; all user data was deleted after the shutdown date; the height.app domain returns a certificate error as of 2026-05-23.
[^ref-trello-pricing]: https://trello.com/pricing — accessed 2026-05-23; Freemium model: Free (up to 10 collaborators per Workspace, unlimited cards, 10 boards per Workspace), Standard ($5/user/month annual), Premium ($10/user/month annual), Enterprise ($17.50/user/month annual). Trello is a cloud-only product — no self-hosted option exists. Power-Ups (including GitHub) are available on all tiers. Enterprise SSO/SAML requires a separate Atlassian Guard subscription (from $4/user/month); it is not bundled into the base Enterprise price.
[^ref-trello-gh-powerup]: https://support.atlassian.com/trello/docs/using-the-github-power-up/ — accessed 2026-05-23; The Trello GitHub Power-Up allows attaching a Branch, Commit, Issue, or Pull Request to Trello cards; metadata (PR status, labels, assignees, comment counts) is displayed on the attachment badge; the Power-Up automatically leaves a comment in GitHub linking back to the Trello card. Explicitly no two-way sync, no swim-lane or board-state sync, and no real-time board updates driven by PR lifecycle. Supports GitHub.com and GitHub Enterprise (HTTPS-hosted instances). Trello is the primary database; GitHub issues are not aggregated into a cross-repo board view.
[^ref-notion-pricing]: https://www.notion.com/pricing — accessed 2026-05-23; Freemium model: Free (unlimited single-user blocks; limited multi-member blocks), Plus (~£8.50/member/month annual), Business (~£16.50/member/month annual, required for synced databases including GitHub sync), Enterprise (custom, SAML SSO, SCIM, audit logs, zero-data-retention LLM). Notion AI (chat, generate, autofill) is available in limited trial on Free/Plus; full access requires Business or Enterprise. No self-hosted option exists.
[^ref-notion-gh-sync]: https://www.notion.com/integrations/github — accessed 2026-05-23; Notion GitHub integration (requires Business plan) creates a synced database by pasting a GitHub repository URL; GitHub properties (issues, PRs, labels, status) are automatically pulled into Notion. Sync is continuous but strictly one-directional (GitHub → Notion); edits must be made in GitHub to appear in Notion. A "GitHub Pull Requests" property auto-updates Notion task status based on PR state. Multi-repo aggregation into a single Notion database is not documented. No GitHub-specific AI features are offered; Notion AI is a generic LLM assistant.
[^ref-youtrack-pricing]: https://www.jetbrains.com/youtrack/features/ — accessed 2026-05-23; YouTrack is free for up to 10 users on both YouTrack Cloud (SaaS) and YouTrack Server (self-hosted/on-premises); this free tier applies to both deployment models. Commercial pricing for additional users was not fetchable from public pages as of 2026-05-23. YouTrack Cloud and YouTrack Server both offer Kanban and Scrum boards, custom fields, agile reporting, and JetBrains AI features (Text-to-Issue, writing assist, summaries, reply suggestions) — none of which constitute issue triage/routing/classification AI. YouTrack Server supports on-premises deployment; YouTrack Cloud is the SaaS option.
[^ref-youtrack-gh]: https://www.jetbrains.com/help/youtrack/server/github-integration.html — accessed 2026-05-23; YouTrack GitHub VCS integration links commits and branches to YouTrack issues via commit messages; PR status is displayed in the YouTrack issue activity stream; commands can be applied to YouTrack issues directly from commit messages (e.g. close, assign). YouTrack is the primary project database — GitHub issues are not imported or mirrored. The integration is configured per repository (one GitHub repo mapped to one YouTrack project); cross-project boards in YouTrack aggregate across multiple projects, giving effective cross-repo visibility.
