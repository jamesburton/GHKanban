# GH Kanban Survey — Design Spec

**Date:** 2026-05-23
**Owner:** James Burton
**Status:** Draft for review

## 1. Purpose

Produce a `README.md` at the repo root that surveys existing attempts at providing a kanban view over GitHub-hosted projects and their issues, with particular attention to AI-agent integration for monitoring, reviewing, and resolving issues. The survey culminates in a per-tier recommendation and a build-vs-buy verdict.

The repo currently contains only `.gitignore` and an initial commit; this spec defines the structure and methodology for the survey, and the technology constraints that apply *if* the build path is chosen.

## 2. Requirements (anchor the matrix)

### 2.1 Tiers to evaluate (verdict required for each)

| Tier | Description |
|---|---|
| Personal / Solo | One developer, many own repos across orgs |
| Small Team (2–10) | Shared board, basic permissions |
| OSS Maintainer | Public repos, high-volume inbound from strangers |
| Enterprise / Multi-team | SSO/SAML, RBAC, audit, hundreds of repos |

### 2.2 Must-have features (filter columns)

- **Cross-repo board** — single board aggregates issues from many repos.
- **GitHub as source of truth** — no shadow database that drifts.
- **PR ↔ Issue linking & status sync** — board reflects PR state automatically.
- **Self-hostable / open source** — runnable on own infra; source available for customisation.

### 2.3 AI-agent capabilities (all four tracked as distinct matrix columns)

- **Triage / classification / labelling** — auto-label, dedupe, prioritise, route to owners.
- **Reproduction & diagnosis** — reproduces reported bugs, posts findings on the issue.
- **Autonomous resolution** — drafts code, opens PRs, iterates on review feedback.
- **PR review & code-quality feedback** — comments on PRs, gates merges.

## 3. Output structure (`README.md` sections, in order)

1. **Purpose & method** — one paragraph; what this doc is, what "must-have" means, how tools were verified (web fetch + cite).
2. **Requirements summary** — restate §2.2 and §2.3 concisely.
3. **Capability matrix** — Table 1 (see §4).
4. **Tier-fit matrix** — Table 2 (see §5).
5. **Tool deep dives** — short subsection per tool that scored ✅ or 🟡 on at least one tier. 3–6 sentences each + links.
6. **Excluded — no GitHub sync (verified)** — pruned tools with URL and date checked.
7. **AI-Agent Appendix** — Table 3 (see §7).
8. **Recommendation per tier** — rubric per tier + cross-tier finding (see §6).
9. **Technology constraints (if we build)** — see §8.
10. **Sources** — full citation list (URLs + access dates).

## 4. Capability matrix (Table 1) columns

Cells use `✅` / `🟡` / `❌` / `❓` (unknown after verify). Non-obvious cells carry a `[ref]` footnote pointing to §Sources.

**Identity & shape**
| # | Column | Captures |
|---|---|---|
| 1 | Tool | Name + link |
| 2 | License | OSS license / Commercial / Freemium |
| 3 | Hosting | SaaS-only / Self-host / Both |
| 4 | Primary surface | Native board / GitHub Projects overlay / 3rd-party board |

**Must-haves (filter)**
| # | Column | Captures |
|---|---|---|
| 5 | Cross-repo board | Single board aggregates issues from many repos |
| 6 | GitHub as truth | No shadow DB; reads/writes GH directly |
| 7 | Self-hostable | Runnable on own infra |
| 8 | PR↔Issue sync | Board reflects PR state automatically |

**AI capabilities**
| # | Column | Captures |
|---|---|---|
| 9 | Triage / labelling | Auto-label, dedupe, prioritise, route |
| 10 | Repro & diagnosis | Reproduces bugs, posts findings |
| 11 | Autonomous resolution | Drafts PRs for fixes |
| 12 | PR review | Comments on PRs |

**Operational**
| # | Column | Captures |
|---|---|---|
| 13 | Multi-user / RBAC | Org permissions beyond a single user |
| 14 | Extensibility | API / webhooks / plugin model for custom agents |
| 15 | Last verified | Date the source was fetched |

Encoding notes:
- `🟡` = partial / conditional (e.g. ZenHub Workspaces aggregate cross-repo only within a workspace boundary → `🟡` on column 5).
- **AI capability cells (columns 9–12):** `✅` if delivered natively *or* via an officially documented integration by either party (the kanban tool or the agent). `🟡` if the capability exists only via an unofficial / community-maintained bridge, or requires significant glue to wire up. `❌` if not available at all. When `✅` is via integration, the `[ref]` footnote must link to the integration doc and name the partner.
- Splitting into two sub-tables (must-haves + ops; AI capabilities) is permitted if rendering gets cramped.

## 5. Tier-fit matrix (Table 2)

Columns: `Personal / Solo`, `Small Team (2–10)`, `OSS Maintainer`, `Enterprise / Multi-team`.

Each cell: `✅` / `🟡` / `❌` + ≤12-word verdict note.

**Scoring rules (rendered inline above the table so verdicts are reproducible):**

- **❌ Hard fail:** missing a must-have that's essential at this tier (e.g. no cross-repo aggregation at OSS-maintainer tier; no RBAC at enterprise tier).
- **🟡 Workable with caveats:** all must-haves present but a notable friction — pricing model wrong for tier (SaaS per-seat at solo tier), AI capabilities require integrating a separate agent platform, or self-hosting is community-edition only.
- **✅ Good fit:** all must-haves + at least one AI capability natively or via documented first-party integration, and the pricing/deployment model matches the tier.

**Tier-specific weights (what tips 🟡 to ❌ at each tier):**

- **Personal:** simple setup; free or near-free; works for one person across orgs.
- **Small team:** affordable seat pricing OR self-hostable without ops burden; basic permissions.
- **OSS maintainer:** must handle high-volume inbound from strangers; triage AI weighted heavily; works on public repos.
- **Enterprise:** SSO/SAML, audit logs, RBAC, SOC2-or-equivalent; scales to hundreds of repos.

## 6. Recommendation per tier (decision rubric)

For each of the four tiers, this fixed template — so the reasoning is reviewable, not vibes:

```
### Tier: <Personal | Small Team | OSS Maintainer | Enterprise>

**Recommended stack:** <tool> [+ <agent>]
**Why:** <one paragraph, ≤80 words>
**Gaps vs your must-haves:** <bullet list, or "none">
**Build-our-own verdict:** Justified / Marginal / Not justified
**If building:** <one paragraph: what's missing in the ecosystem that a new tool would have to provide to be worth it; references §8 Technology constraints>
```

Closing sub-section: **Cross-tier finding** — single paragraph stating whether one stack serves all four tiers, or whether the right answer changes by tier. If every tier needs a different answer, that itself is a signal that a unified tool may be worth building.

## 7. AI-Agent Appendix (Table 3)

Same encoding as Table 1. Columns:

| Column | Captures |
|---|---|
| Agent | Name + link |
| Vendor / License | Anthropic / OpenAI / Cognition / OSS / etc. |
| Surface | CLI / IDE / GitHub App / web / API |
| Triage | Auto-label, dedupe, prioritise |
| Repro & diagnosis | Reproduces bugs, posts findings on the issue |
| Autonomous resolution | Drafts a PR for a fix |
| PR review | Comments on PRs |
| Board integrations | Which kanban tools (from Table 1) surface this agent's activity |
| Pricing model | Per-seat / per-run / OSS / freemium |
| Last verified | Date |

The **Board integrations** column is the bridge from agent landscape back to tooling choices — the reason the appendix exists rather than being a separate document.

## 8. Technology constraints (if we build)

This section appears in the README (after recommendations, before Sources) and only applies *if* the build path is chosen. The recommendation rubric's "If building" paragraph references it.

- **Runtime:** .NET 10 (C#).
- **Test framework:** xUnit v3 with Microsoft Testing Platform (MTP).
- **Packaging:** publish as `dotnet tool` packages; auto-version + auto-publish to NuGet from CI.
- **Primary invocation:** `dnx` zero-install execution is the headline UX; `dotnet tool install -g` is a secondary path for users who want a pinned local copy.
- **Non-.NET dependencies (if any):** orchestrate via .NET Aspire.
- **AI orchestration:** prefer Microsoft Agent Framework where its features fit; fall back to direct SDK calls only when the framework can't express the workflow cleanly.
- **GitHub authentication:** use `gh` CLI's stored token where available; otherwise document storing PATs in OS secret store (DPAPI on Windows, libsecret on Linux, Keychain on macOS). Multiple logins must be supported via simple toggle or rapid logout/login swap.
- **UI:** Blazor (Server or WASM — to be decided during planning). Same .NET stack as the rest; pairs with `dnx`-as-primary for self-hosted users.

## 9. Research methodology

1. For each tool: fetch the official homepage + features/docs page + (if open source) the repo README. Map features to matrix columns.
2. For AI-integration claims specifically: fetch the vendor's actual integration/docs page (not third-party blog posts) and quote / link the specific page that backs the claim.
3. Every cell with a non-obvious value gets a `[ref]` footnote pointing to §Sources; the source entry has URL + access date.
4. For tools that fail verification (no GH sync, 404 homepage, dead project): move to §6 of the README, "Excluded — no GitHub sync (verified)", with URL and date checked. Do not silently drop.
5. Verify only — do not register for SaaS trials or run any tool. If a feature cannot be confirmed from public docs, mark `❓` rather than guess.

The methodology paragraph + the "Last verified" column make the document auditable and re-runnable later.

## 10. Initial tool shortlist

Subject to pruning during verification. Tools that fail GitHub-sync verification move to the "Excluded" subsection (not deleted).

**A. GitHub-native (baseline)**
- GitHub Projects v2 (board view)
- GitHub Issues + Milestones — the "do nothing" baseline

**B. GitHub-overlay kanban (dedicated)**
- ZenHub (Cloud + Enterprise)
- Waffle.io — historical, defunct; included as a "what happened" note

**C. PM tools with GitHub sync** *(most will fail self-host filter; included to represent commercial state-of-the-art)*
- Linear
- Jira (Cloud + GitHub for Jira)
- Shortcut
- Height
- Trello (+ GitHub power-up)
- Notion (+ 2-way GH sync)
- YouTrack (has on-prem free tier — may pass self-host filter)

**D. Self-hostable / OSS with GitHub integration**
- Plane.so
- Taiga
- OpenProject
- Kanboard
- Focalboard
- Vikunja
- WeKan
- Leantime
- GitScrum
- Redmine (with GitHub plugin)

**E. AI-Agent appendix candidates** *(Table 3, not Table 1)*
- GitHub Copilot Coding Agent / Copilot Workspace
- Devin (Cognition)
- Claude Code (Anthropic) + GitHub integration
- OpenHands (formerly OpenDevin)
- Sweep AI
- Codegen
- Cursor background agents
- CodeRabbit (PR review)
- Greptile (PR review)
- Aider (CLI; included for completeness)
- Augment Code

Total: ~21 candidates in the main matrix + ~11 in the appendix.

## 11. Out of scope

- Generic project-management tools without any GitHub integration (Asana, Monday, ClickUp etc.).
- IDE-only AI coding assistants without a server-side / GitHub-App mode (Cline, Continue, base Copilot completions).
- Internal-only / unreleased tools.
- Hands-on trials, paid signups, or running any of the surveyed tools.

## 12. Acceptance criteria for the survey

- `README.md` exists at repo root with all sections from §3 populated.
- Every cell in every matrix has a value (`✅` / `🟡` / `❌` / `❓`) — no blanks.
- Every non-obvious cell has a `[ref]` footnote resolving to §Sources.
- Each tool in §Sources has a URL and an access date no earlier than the survey start.
- Each of the four tiers has a recommendation block with all five sub-fields populated, including an explicit "Justified / Marginal / Not justified" build verdict.
- The "Excluded — no GitHub sync (verified)" subsection records each pruned tool with URL + date.
- The Cross-tier finding paragraph is present and answers the unified-vs-divergent question explicitly.
