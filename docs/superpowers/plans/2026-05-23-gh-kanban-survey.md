# GH Kanban Survey Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a `README.md` at the repo root surveying GitHub-backed kanban tools and AI-agent integrations, with capability + tier-fit matrices, per-tier recommendations, and an explicit build-or-buy verdict.

**Architecture:** Single `README.md` at repo root. Three markdown tables (capability, tier-fit, AI-agent appendix). Tool research is done by web-fetching each tool's official homepage + features/docs (no signups, no third-party blog citations). Cells with non-obvious values carry `[ref-N]` footnotes resolving to a Sources section at the bottom (URL + access date). Tools that fail GitHub-sync verification are logged in an "Excluded" subsection rather than silently dropped.

**Tech Stack:** Markdown only. Web fetching via `WebFetch` / `mcp__fetch__fetch` tools. No runtime, no build, no tests in the code sense; the acceptance-criteria check at the end is the verification gate.

**Source spec:** `docs/superpowers/specs/2026-05-23-gh-kanban-survey-design.md` — read it before starting; every task here implements one slice of that spec.

---

## File Structure

| File | Responsibility |
|---|---|
| `README.md` | The survey itself. Single-file deliverable. All sections from spec §3 live here. |
| `docs/superpowers/specs/2026-05-23-gh-kanban-survey-design.md` | Already exists. The source of truth for what's required. Do not edit during execution; if it needs changes, raise them and re-run brainstorming. |
| `docs/superpowers/plans/2026-05-23-gh-kanban-survey.md` | This plan. Do not modify during execution. |

The README will grow large (~600-1000 lines once populated). That's acceptable for a single-page survey — splitting it would defeat the "scannable in one place" goal.

---

## Conventions used in this plan

**Reference IDs:** Every source gets an ID like `[ref-zenhub-features]` — slug, not a number, so insertions don't renumber. Format in Sources section:

```
[ref-zenhub-features]: https://www.zenhub.com/features — accessed 2026-05-23
```

**Cell encoding (recap from spec §4):**
- `✅` = present (natively or via documented first-party integration; for AI capability cells, see scoring note)
- `🟡` = partial / conditional — must include a brief parenthetical
- `❌` = absent
- `❓` = unknown after good-faith verification

**AI capability scoring (columns 9-12 of Table 1):** `✅` if delivered natively *or* via an officially documented integration by either party. `🟡` only via unofficial / community bridge. `❌` not available. When `✅` is via integration, the `[ref-…]` footnote must link to the integration doc and name the partner.

**Tier-fit scoring (Table 2):** see spec §5; render the rubric inline above the table.

**Commit cadence:** one commit per task. Each task is self-contained.

**Date stamp:** today is 2026-05-23. Use that for all "Last verified" cells and source access dates set during this execution. If execution spans multiple calendar days, use the actual fetch date for each cell.

---

## Task 1: Initialize README skeleton

**Files:**
- Create: `README.md`

- [ ] **Step 1: Create README.md with full section skeleton**

Write this exact content to `README.md`:

```markdown
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
```

- [ ] **Step 2: Commit skeleton**

```bash
git add README.md
git commit -m "docs: add README survey skeleton with section headers and matrix tables"
```

---

## Task 2: GitHub-native tools (group A)

**Tools:** GitHub Projects v2, GitHub Issues + Milestones.

**Files:**
- Modify: `README.md` (append rows to Table 1 §3, Table 2 §4, sources to §10)

- [ ] **Step 1: Fetch primary sources**

Use WebFetch on each of these URLs. For each tool, capture features, cross-repo behaviour, AI capabilities (if any), and self-host story.

- GitHub Projects v2: `https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects` and `https://github.com/features/issues`
- GitHub Issues + Milestones: `https://docs.github.com/en/issues/tracking-your-work-with-issues/about-issues` and `https://docs.github.com/en/issues/using-labels-and-milestones-to-track-work`

- [ ] **Step 2: Score columns 1-15 of Table 1 for each tool**

For each tool, decide the value of every column using the encoding rubric. Write the values down before editing the file. Spec §4 has the canonical column definitions; the AI scoring note from spec §4 applies.

Notes specific to these tools:
- GitHub Projects v2 *is* GitHub-native, so "GitHub as truth" = ✅ trivially.
- "Self-hostable" = ❌ for both unless GitHub Enterprise Server counts — it does; mark `🟡 (via GitHub Enterprise Server only)`.
- AI capabilities: as of 2026, GitHub natively integrates Copilot into Issues/PRs. Verify the specific capability columns against current docs — do not assume.

- [ ] **Step 3: Append Table 1 rows**

Append two rows under the Table 1 header in §3 of `README.md`. Use the form:

```markdown
| [GitHub Projects v2](https://github.com/features/issues) | Commercial[ref-gh-projects-docs] | SaaS (GHES self-host)[ref-ghes] | Native GH board | <value> | ✅ | 🟡 (GHES) | <value> | <value> | <value> | <value> | <value> | <value> | <value> | 2026-05-23 |
| [GitHub Issues](https://docs.github.com/en/issues) | Commercial[ref-gh-issues-docs] | SaaS (GHES self-host)[ref-ghes] | Issues list (no board) | <value> | ✅ | 🟡 (GHES) | <value> | <value> | <value> | <value> | <value> | <value> | <value> | 2026-05-23 |
```

Replace each `<value>` with the actual encoding decided in Step 2.

- [ ] **Step 4: Append Table 2 rows**

Append two rows under the Table 2 header in §4. Form:

```markdown
| GitHub Projects v2 | <verdict + ≤12-word note> | <verdict + note> | <verdict + note> | <verdict + note> |
| GitHub Issues | <verdict + note> | <verdict + note> | <verdict + note> | <verdict + note> |
```

- [ ] **Step 5: Append sources**

Append to the Sources section in §10:

```markdown
[ref-gh-projects-docs]: https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects — accessed 2026-05-23
[ref-gh-issues-docs]: https://docs.github.com/en/issues/tracking-your-work-with-issues/about-issues — accessed 2026-05-23
[ref-ghes]: https://github.com/enterprise — accessed 2026-05-23
```

Add any additional `[ref-…]` entries used to support AI-capability cell values (e.g. `[ref-copilot-coding-agent]` if cited).

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: add GitHub-native tools (Projects v2, Issues) to survey matrices"
```

---

## Task 3: GitHub-overlay kanban — ZenHub + Waffle

**Tools:** ZenHub (Cloud + Enterprise), Waffle.io (defunct — historical entry).

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Fetch sources**

- ZenHub: `https://www.zenhub.com/`, `https://www.zenhub.com/features`, `https://www.zenhub.com/product/enterprise` (or the current equivalents).
- Waffle.io: try `https://waffle.io/` (likely 404 / parked). Cross-check with a fetch of `https://github.com/waffleio` if it still exists, and reference the published shutdown announcement if accessible. This entry is historical — its purpose is to record that this once-obvious answer is gone.

- [ ] **Step 2: Score Table 1 columns for ZenHub**

Specific known facts to verify (do not assume — confirm against current docs):
- ZenHub Workspaces aggregate cross-repo *within a workspace boundary* → column 5 likely `🟡 (within workspace)`.
- "GitHub as truth": ZenHub historically used GH issues + a parallel store for board metadata. Verify the current state — if anything board-side (sprints, pipelines, estimates) lives only in ZenHub's DB, mark `🟡` with a note in the footnote.
- Self-host: ZenHub Enterprise exists as on-prem. Confirm it's currently sold.
- AI capabilities: ZenHub has shipped AI features (AI sprint planning, AI issue summarisation). Score columns 9-12 only for capabilities backed by a current docs page.

- [ ] **Step 3: Score Table 1 for Waffle.io**

If the homepage is dead / project shut down, score every must-have / capability column as `❌` and the "Last verified" cell with today's date. The row exists to document the historical record.

- [ ] **Step 4: Append two Table 1 rows**

Same form as Task 2 Step 3. Footnote IDs: `[ref-zenhub-features]`, `[ref-zenhub-enterprise]`, `[ref-zenhub-ai]` (if AI features cited), `[ref-waffle-shutdown]` if a shutdown reference is found, otherwise just cite the dead homepage.

- [ ] **Step 5: Append two Table 2 rows**

Same form as Task 2 Step 4.

- [ ] **Step 6: Append sources**

Append the `[ref-…]` entries used.

- [ ] **Step 7: Commit**

```bash
git add README.md
git commit -m "docs: add ZenHub and Waffle.io to survey matrices"
```

---

## Task 4: Commercial PM tools group 1 — Linear, Jira, Shortcut, Height

**Tools:** Linear, Jira (Cloud + GitHub for Jira), Shortcut, Height.

**All four are SaaS-only — expect ❌ on column 7 (self-host), and likely ❌ on most tier-fit cells. They are in the matrix to represent the commercial state-of-the-art.**

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Fetch sources** (one fetch per tool, with at least one features/docs page)

- Linear: `https://linear.app/`, `https://linear.app/integrations/github`
- Jira (+ GitHub for Jira): `https://www.atlassian.com/software/jira`, `https://marketplace.atlassian.com/apps/1219592/github-for-jira`
- Shortcut: `https://www.shortcut.com/`, `https://www.shortcut.com/integrations/github`
- Height: `https://height.app/`, `https://height.app/integrations/github` (or current docs path)

- [ ] **Step 2: Score Table 1 columns for all four tools**

Pay particular attention to:
- "GitHub as truth" — for all four, the proprietary tool *is* the source of truth and GH is mirrored in. Mark `❌` and footnote.
- "Cross-repo" — generally `✅` (their projects span repos because they don't think in repos).
- "PR↔Issue sync" — usually `✅` via the GH app integration; verify the current state.
- AI capabilities — Linear has shipped AI triage / similar issue features; Jira has Atlassian Intelligence; check what's currently shipped.

- [ ] **Step 3: Append four Table 1 rows**

Same form as Task 2 Step 3. Use ref slugs like `[ref-linear-gh]`, `[ref-jira-gh-app]`, `[ref-shortcut-gh]`, `[ref-height-gh]`.

- [ ] **Step 4: Append four Table 2 rows**

Self-host=❌ likely tips Personal and Small Team to `🟡` or `❌` per the rubric. OSS Maintainer should be `❌` for all four (per-seat pricing on public repos doesn't scale).

- [ ] **Step 5: Append sources**

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: add Linear/Jira/Shortcut/Height to survey matrices"
```

---

## Task 5: Commercial PM tools group 2 — Trello, Notion, YouTrack

**Tools:** Trello (+ GitHub power-up), Notion (+ 2-way GH sync), YouTrack (has on-prem free tier).

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Fetch sources**

- Trello: `https://trello.com/`, `https://trello.com/power-ups/55a5d916446f517774210011/github` (or current GH power-up URL).
- Notion: `https://www.notion.so/`, `https://www.notion.so/integrations/github`
- YouTrack: `https://www.jetbrains.com/youtrack/`, `https://www.jetbrains.com/help/youtrack/server/Integration-with-GitHub.html`

- [ ] **Step 2: Score Table 1 columns**

Things to verify:
- YouTrack has both on-prem (paid + free for ≤10 users) and SaaS — column 7 likely `✅` (with free-tier caveat noted).
- Trello power-up offers issue linking but typically not a true PR↔Issue *swim-lane sync* — likely `🟡` or `❌` on column 8.
- Notion 2-way GH sync — verify current state of features (auto-update of issue properties, etc.).
- AI: Notion AI is generic LLM, not issue-specific — likely `❌` on AI capability columns. Atlassian/JetBrains AI features should be checked against current docs.

- [ ] **Step 3: Append three Table 1 rows**

- [ ] **Step 4: Append three Table 2 rows**

- [ ] **Step 5: Append sources**

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: add Trello/Notion/YouTrack to survey matrices"
```

---

## Task 6: OSS group 1 — Plane.so, Taiga, OpenProject, Kanboard

**Tools:** Plane.so, Taiga, OpenProject, Kanboard.

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Fetch sources**

- Plane.so: `https://plane.so/`, `https://docs.plane.so/`, GH repo `https://github.com/makeplane/plane`
- Taiga: `https://www.taiga.io/`, GitHub integration docs.
- OpenProject: `https://www.openproject.org/`, `https://www.openproject.org/docs/system-admin-guide/integrations/github-integration/`
- Kanboard: `https://kanboard.org/`, look for any GH integration plugin (likely community plugin only).

- [ ] **Step 2: Score Table 1 columns**

For each tool, the critical first check is **does it sync with GitHub at all?**

- If a tool has **no official or first-party GitHub sync**, do not put it in Table 1. Instead, in Task 6 Step 4 below, append it to §6 "Excluded — no GitHub sync (verified)" and skip its Table 1 / Table 2 rows.
- Borderline cases (community plugin exists but unmaintained) → still goes to Excluded with the verification note.

Kanboard in particular: check whether GH sync exists as a first-party plugin (likely not) — likely an Excluded entry.

- [ ] **Step 3: Append Table 1 + Table 2 rows for tools that pass the GH-sync check**

Same form as previous tasks.

- [ ] **Step 4: Append Excluded entries for tools that fail**

Append to §6 in this exact form, one paragraph per excluded tool:

```markdown
- **[Kanboard](https://kanboard.org/)** — verified 2026-05-23. No first-party GitHub sync; community plugin `kanboard-github` is unmaintained / no longer compatible[ref-kanboard-plugins]. Excluded.
```

- [ ] **Step 5: Append sources**

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: add OSS group 1 (Plane/Taiga/OpenProject/Kanboard) to survey"
```

---

## Task 7: OSS group 2 — Focalboard, Vikunja, WeKan

**Tools:** Focalboard (post-Mattermost), Vikunja, WeKan.

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Fetch sources**

- Focalboard: `https://www.focalboard.com/` (may redirect — verify project status post-Mattermost handover), GH repo `https://github.com/mattermost/focalboard`.
- Vikunja: `https://vikunja.io/`, GH repo `https://github.com/go-vikunja/vikunja`.
- WeKan: `https://wekan.github.io/`, GH repo `https://github.com/wekan/wekan`.

- [ ] **Step 2: Score Table 1 columns — same GH-sync gate as Task 6**

For each tool: if no first-party GitHub sync, append to §6 Excluded and skip table rows.

These tools are general-purpose kanbans (not GH-specific), so GH sync is the make-or-break check. Expect at least one Excluded entry from this group.

- [ ] **Step 3: Append Table 1 + Table 2 rows for tools that pass**

- [ ] **Step 4: Append Excluded entries for tools that fail**

Same form as Task 6 Step 4.

- [ ] **Step 5: Append sources**

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: add OSS group 2 (Focalboard/Vikunja/WeKan) to survey"
```

---

## Task 8: OSS group 3 — Leantime, GitScrum, Redmine

**Tools:** Leantime, GitScrum, Redmine (+ GitHub plugin).

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Fetch sources**

- Leantime: `https://leantime.io/`, GH repo `https://github.com/Leantime/leantime`.
- GitScrum: `https://gitscrum.com/`, marketing site features page.
- Redmine: `https://www.redmine.org/`, plugin directory entry for any GitHub integration plugin.

- [ ] **Step 2: Score Table 1 — same GH-sync gate as Task 6**

- GitScrum's name is misleading — verify whether it actually syncs *with* GitHub or just uses GH-style flow internally. If only the latter, → Excluded.
- Redmine's GH integration is plugin-based; verify the plugin is maintained.

- [ ] **Step 3: Append Table 1 + Table 2 rows for tools that pass**

- [ ] **Step 4: Append Excluded entries for tools that fail**

- [ ] **Step 5: Append sources**

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: add OSS group 3 (Leantime/GitScrum/Redmine) to survey"
```

---

## Task 9: AI-Agent appendix (Table 3) — all 11 candidates

**Agents:** GitHub Copilot Coding Agent / Copilot Workspace, Devin, Claude Code + GitHub integration, OpenHands, Sweep AI, Codegen, Cursor background agents, CodeRabbit, Greptile, Aider, Augment Code.

**Files:**
- Modify: `README.md` (append rows to Table 3 in §7, sources to §10)

- [ ] **Step 1: Fetch sources — one per agent**

- GitHub Copilot Coding Agent: `https://github.com/features/copilot`, plus the current Copilot Coding Agent docs page.
- Devin: `https://devin.ai/` and Cognition Labs docs.
- Claude Code: `https://www.anthropic.com/claude-code`, GH integration docs (`https://docs.claude.com/en/docs/claude-code/github-actions` or current path).
- OpenHands: `https://github.com/All-Hands-AI/OpenHands`.
- Sweep AI: `https://sweep.dev/` (verify status — Sweep transitioned/wound-down at one point).
- Codegen: `https://codegen.com/`.
- Cursor background agents: `https://cursor.com/`, background-agents docs page.
- CodeRabbit: `https://www.coderabbit.ai/`, integration docs.
- Greptile: `https://www.greptile.com/`.
- Aider: `https://aider.chat/`.
- Augment Code: `https://www.augmentcode.com/`.

- [ ] **Step 2: Score columns for each agent**

For Table 3 columns: Vendor / License · Surface (CLI / IDE / GitHub App / web / API) · Triage · Repro · Autonomous resolution · PR review · Board integrations · Pricing model · Last verified.

**Board integrations is the key column.** For each agent, check whether any of the tools surveyed in Tasks 2-8 explicitly mention an integration with this agent in their docs — and whether the agent's own docs mention any board tool. List all that match.

- [ ] **Step 3: Append 11 rows to Table 3**

Same row form as Table 1, but using Table 3's columns. Use ref slugs like `[ref-copilot-agent]`, `[ref-devin]`, `[ref-claude-code-gh]`, etc.

- [ ] **Step 4: Append sources**

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: add AI-agent appendix (Table 3) with 11 agent platforms"
```

---

## Task 10: Tool deep dives (§5)

**Files:**
- Modify: `README.md` (§5 Tool deep dives)

- [ ] **Step 1: Identify tools that scored ✅ or 🟡 on at least one Table 2 tier**

Scan Table 2 and list every tool with at least one non-❌ tier cell. These get a deep-dive entry. Tools that scored ❌ on every tier do not.

- [ ] **Step 2: Write a deep-dive entry per qualifying tool**

3-6 sentences per tool, covering:
- What the tool is, in one sentence
- Where it sits in the matrix (which tiers it scored well on, headline strengths/weaknesses)
- Notable AI integration story (if any)
- Pricing / deployment specifics relevant to the recommendation

Format:

```markdown
### <Tool Name>

<3-6 sentences as above.>

**Links:** [Homepage](…) · [GitHub integration docs](…) · [Pricing](…)
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add deep-dive sections for surviving tools"
```

---

## Task 11: Recommendations per tier + cross-tier finding (§8)

**Files:**
- Modify: `README.md` (§8 Recommendation per tier)

- [ ] **Step 1: Decide recommendation per tier**

For each tier (Personal, Small Team, OSS Maintainer, Enterprise), pick the best tool/stack from Table 2 and from the AI appendix (Table 3). The stack may be `<tool> + <agent>`.

For each tier, decide the build-our-own verdict:
- **Justified** — no surveyed tool/stack delivers the must-haves + AI capabilities at this tier, even with integration.
- **Marginal** — a workable stack exists but with significant friction or feature gaps; build vs adopt is a judgment call.
- **Not justified** — at least one surveyed stack covers must-haves + AI capabilities well at this tier.

- [ ] **Step 2: Write four recommendation blocks using the template**

Append to §8 in this exact form (one block per tier):

```markdown
### Tier: <Personal / Small Team / OSS Maintainer / Enterprise>

**Recommended stack:** <tool> [+ <agent>]
**Why:** <one paragraph, ≤80 words>
**Gaps vs your must-haves:** <bullet list, or "none">
**Build-our-own verdict:** <Justified | Marginal | Not justified>
**If building:** <one paragraph: what's missing in the ecosystem that a new tool would have to provide to be worth it; reference §9 Technology constraints>
```

- [ ] **Step 3: Write cross-tier finding paragraph**

Append a `### Cross-tier finding` subsection at the end of §8 with a single paragraph: does *one* stack serve all four tiers, or does the right answer change per tier? If different per tier, that itself argues toward building. Be explicit either way.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: add per-tier recommendations and cross-tier finding"
```

---

## Task 12: Acceptance-criteria final pass (the verification gate)

**Files:**
- Modify: `README.md` (only if fixes are required)

- [ ] **Step 1: Run each acceptance criterion from spec §12**

For each criterion below, read the README and answer pass/fail. Record failures, then fix them before commit.

| # | Criterion | Pass? |
|---|-----------|-------|
| 1 | README.md exists at repo root with all sections from spec §3 populated | ☐ |
| 2 | Every cell in Tables 1, 2, 3 has a value (✅/🟡/❌/❓) — no blanks | ☐ |
| 3 | Every non-obvious cell has a `[ref-…]` footnote resolving to §10 Sources | ☐ |
| 4 | Each tool in §10 Sources has a URL and access date no earlier than survey start (2026-05-23) | ☐ |
| 5 | Each of the four tiers has a recommendation block with all five sub-fields populated, including an explicit Justified/Marginal/Not justified build verdict | ☐ |
| 6 | §6 "Excluded — no GitHub sync (verified)" records each pruned tool with URL + date | ☐ |
| 7 | The Cross-tier finding paragraph is present and answers the unified-vs-divergent question explicitly | ☐ |

- [ ] **Step 2: Fix any failures**

If any criterion fails: identify the gap, fill it, re-check. Do not commit until all seven pass.

- [ ] **Step 3: Run a markdown lint scan (manual)**

Visually scan the rendered Markdown (open in a viewer or `grip` it locally) and confirm:
- All three tables render correctly (column alignment).
- No `<value>` placeholders left over from the templates.
- No `[ref-…]` references that don't have a matching definition in §10.
- No definitions in §10 that aren't cited anywhere.

- [ ] **Step 4: Commit (final)**

```bash
git add README.md
git commit -m "docs: complete survey — all acceptance criteria pass"
```

---

## Self-Review (run by plan author, completed inline before save)

**1. Spec coverage:**

| Spec section | Covered by task(s) |
|---|---|
| §1 Purpose | Task 1 (skeleton text) |
| §2.1 Tiers | Task 1 (Requirements summary in skeleton), Task 11 (per-tier recs) |
| §2.2 Must-haves | Tasks 1, 2-8 (Table 1 columns 5-8) |
| §2.3 AI capabilities | Tasks 1, 2-8 (Table 1 cols 9-12), Task 9 (Table 3) |
| §3 Output structure | Task 1 (creates all 10 sections); Tasks 2-11 populate them |
| §4 Capability matrix | Tasks 2-8 |
| §5 Tier-fit matrix | Tasks 2-8 |
| §6 Recommendation rubric | Task 11 |
| §7 AI-Agent appendix | Task 9 |
| §8 Technology constraints | Task 1 (skeleton populates this section directly from the spec, since it's not research-driven) |
| §9 Research methodology | Task 1 (Purpose & method paragraph) |
| §10 Tool shortlist | Tasks 2-9 cover every tool from spec §10 (groups A-E) |
| §11 Out of scope | Reflected in Task 1's Purpose & method paragraph and the methodology |
| §12 Acceptance criteria | Task 12 |

No gaps.

**2. Placeholder scan:**

Searched for "TBD", "TODO", "implement later", "fill in details", "appropriate error handling". None of these appear as plan failures — the only `<value>` placeholders are inside row-template code blocks where they are explicitly described as substitutable in the surrounding step instructions. Clean.

**3. Type / name consistency:**

- Footnote slug convention is consistent (`[ref-<kebab-slug>]` throughout).
- Table column names match between the skeleton in Task 1 and the row-append instructions in Tasks 2-9.
- "Excluded — no GitHub sync (verified)" is the §6 heading in Task 1's skeleton AND in Task 6/7/8 append instructions AND in Task 12 acceptance check — consistent.
- Build-verdict trichotomy is consistently `Justified / Marginal / Not justified` in Tasks 11 and 12 (matches spec §6 and §12).

Clean.
