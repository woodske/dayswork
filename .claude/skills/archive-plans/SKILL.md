---
name: archive-plans
description: Archive implemented plans from docs/plans into concise artifacts under docs/plans/archive, update both indexes, repoint cross-references, and delete the archived plan files. Use when the user asks to archive plans, clean up the plans folder, or fold completed plans into the archive.
---

# Archive plans

Fold **implemented** plans from `docs/plans/` into concise artifacts under `docs/plans/archive/`,
then delete the originals. Status lives in `docs/plans/index.md`; the *why and the decisions* live in
the archive; the *how* lives in the code.

Only run this when explicitly invoked. Never archive as a side effect of other work.

## What qualifies for archiving

Archive a plan when its **headline deliverable has shipped**. Deferred sub-phases and open questions
do not block archiving — they get carried forward as open threads.

Do **not** archive:

- A plan whose main deliverable has not been built, even if a measurement or scaffolding phase has
  (e.g. a measure-only Phase 0 with the real gate still disabled). It stays active.
- A plan that was never started — a deferred design record or an analysis doc. It stays in
  `docs/plans/` with a status row in `index.md`.

If a plan is ambiguous, ask rather than guess. Deleting a plan is not reversible from the user's
point of view even though git has it.

## Steps

### 1. Survey

Read every file in `docs/plans/` and `docs/plans/archive/index.md` if it exists. For each plan,
determine from the plan text what was actually built. Then **verify against the code** — a plan's own
status line is a claim, not a fact. Grep for the types, files, and methods the plan says it created.

Present the classification to the user (archive / stays active / ambiguous) and confirm before
writing anything, unless they already specified which plans to archive.

### 2. Group

Multiple plans belong in one artifact when they were one effort or one subsystem — a numbered review
whose items shipped together, or a feature and the sibling feature spun out of it. Prefer few
artifacts over many; a single-plan artifact is fine when the plan stands alone.

Name artifacts for the theme, not the file they came from: `architecture-review-2026-07-07.md`,
`machine-and-pond-management.md`.

### 3. Write the artifact

**Concise. The archive is not a second copy of the plan.** Drop the step-by-step approach, file
maps, milestone lists, test checklists, and code sketches — the code is the record of what was built.

Keep, for each plan folded in:

- **Why the work was done** — the problem in concrete terms, including numbers the plan measured.
- **Decisions and discussions the plan captured** — what was chosen, what was rejected, and the
  reasoning. Anything marked "decided with the user", "locked", or "declined" is the highest-value
  content in the whole exercise; preserve it.
- **Divergences** — where the implementation departed from the plan's design, and why. These are
  easy to lose and expensive to re-derive.
- **Accepted imperfections, non-goals, and risks** the plan recorded.

**Never invent a why.** If the plan did not record why something was done, say nothing about it. Do
not infer motivation from the code, from commit messages, or from what would be reasonable. An
artifact with three recorded decisions and no speculation beats one with ten plausible ones.

Structure each artifact as:

```markdown
# Archived: <theme>

<1–3 sentences: what these plans covered, when they landed.>

Original plans: `<file>.md`, `<file>.md`.

---

## <Plan or item name>

**Why:** ...

**What was decided:** ...

## Open threads carried out of these plans

<Deferred items, unanswered open questions. Say they are unscheduled.>
```

### 4. Update `docs/plans/archive/index.md`

Create it if absent. It carries:

- A one-line-per-artifact table: artifact link + what it covers.
- An **original plan → artifact** mapping table, so old references by filename still resolve.
- **Open threads** consolidated from the artifacts.
- Anything **declined**, so it isn't re-proposed.

### 5. Update `docs/plans/index.md`

Create it if absent. It is the only place plan status lives. It carries:

- A status legend.
- An **active plans** table: plan, status, dependencies, and a **Verify** command that detects the
  plan's done-state *from the code alone* (a grep or a file check — not a reference to the doc).
- A **recently completed** table pointing into the archive.
- A maintenance protocol ending with "archive via the `archive-plans` skill".

Update the *last verified* date whenever you touch the table.

### 6. Strip status from surviving plan files

A plan file must not carry its own status — that is what the index is for. Replace any status block
in the plans that stayed with a line pointing at `index.md`, keeping any substantive content that was
mixed into it.

### 7. Repoint cross-references

Before deleting anything, find every reference to the plan files being archived:

```bash
grep -rn "docs/plans" --include=*.md --include=*.cs . | grep -v "^./docs/plans/"
```

Also check the surviving plan files for links to archived ones. Repoint each to the archive artifact
(and the relevant section within it). `AGENTS.md` and `docs/*.md` are the usual holders.

### 8. Delete and verify

`git rm` the archived plan files. Then confirm no dangling links remain:

```bash
grep -rn "docs/plans/[a-z-]*\.md" --include=*.md . | grep -v "docs/plans/archive/"
```

Every hit must name a plan that still exists. Report what was archived, what stayed active and why,
and which cross-references moved. Do not commit unless asked.

## Project notes

- `AGENTS.md` is the mod's AI-context file and references plan docs in its "Verified game-content
  references" and "Current state" sections — check both.
- Some shipped work has no plan file at all (it is tracked only in `AGENTS.md` → "Current state").
  Do not manufacture archive artifacts for it; note it under "recently completed" in the index if it
  helps orient a reader.
