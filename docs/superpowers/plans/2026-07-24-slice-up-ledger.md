# Slice UP ledger — local Unity Pipeline spike

**Branch** `slice-up-unity-pipeline` from main `9d51673`.
**Kickoff** `docs/superpowers/plans/2026-07-22-slice-up-kickoff-prompt.md`.
**Research** memory `unity-cli-pipeline-research-artifact` →
`https://claude.ai/code/artifact/ba6f69d5-5423-4ead-a795-9ea5aff0caab`.

This is a **spike**: success is knowledge + a working prototype, not polish.
A documented dead end is a successful outcome. Batchmode remains the canonical
merge gate for all slices unless UP6's verdict says otherwise.

## Hard constraints (user-set, non-negotiable)

- **LOCAL ONLY.** No cloud services, no Build/Pipeline Automation, no beta
  sign-ups. The ONE permitted account touchpoint is `unity auth login`. Anything
  beyond it (org enrollment, cloud project link, closed-beta approval) ⇒ **stop,
  document the wall, report**.
- **Serial atlas editing.** One editor, one session touching `unity/`.
- **Zero sim behavior.** No `src/Core` changes. `dotnet test` green untouched;
  seed-42 golden byte-untouched.
- **Everything pre-1.0.** Pin exact versions. Trust `--help` over docs.unity.com.

## Environment baseline (recorded at slice start, 2026-07-24)

- `unity` CLI: **NOT installed** (`Get-Command unity` → not found; not on PATH).
- Unity editor: 6000.5.2f1 (project target).
- `unity/Packages/manifest.json` + `packages-lock.json` are **gitignored** —
  `unity pipeline install` mutates local-only state; the manifest delta is
  recorded verbatim in UP3 so a fresh checkout can reproduce it.

## Task ledger

| # | Task | Gate | Status |
|---|---|---|---|
| UP1 | Install & pin the CLI (Windows route) | `unity editors -i --format json` finds 6000.5.2f1 | ☐ |
| UP2 | Map the command surface → committed reference doc | reference doc committed | ☐ |
| UP3 | `unity auth login` + `unity pipeline install`; append pipeline command inventory | `unity pipeline list` shows Installed | ☐ |
| UP4 | Prove warm-editor gates (`run_tests` ×3 vs batchmode baseline, `recompile`, `menu`) | deterministic across repeats (flakiness documented = finding) | ☐ |
| UP5 | Eyeball grid prototype (multi-seed atlas contact sheet) | user opens one HTML file, sees every seed | ☐ |
| UP6 | Verdict + wrap (HANDOFF, fable review, merge, push) | three-checkpoint protocol | ☐ |

## User checkpoints

1. **Scope nod** — ✅ accepted 2026-07-24.
2. **Eyeball** — the UP5 grid itself.
3. **Merge decision.**

## Log

### 2026-07-24 — slice opened

Branch cut from main `9d51673` (clean). Scope nodded. Two known user-in-the-loop
pauses flagged at the nod: `unity auth login` (UP3, interactive browser) and
launching the editor for UP4's warm-editor gates.
