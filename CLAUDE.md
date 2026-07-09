# StarSystemGeneration — Claude Code Instructions

Deterministic procedural galaxy generator + epoch history simulation. C# —
`src/Core` (netstandard2.1, no Unity deps), `src/Inspector` (REPL, net8.0),
`tests/Core.Tests` (xUnit), `unity/` (superseded PoC atlas). Build/test:
`dotnet test StarSystemGeneration.sln`.

## Documentation planes (never mix them)

- `docs/design/` — **the living systems design** (final feature designs, present
  tense, no process). This is the spec for all implementation. Start at
  `docs/design/README.md`.
- `docs/superpowers/specs/` — dated decision records; `docs/superpowers/plans/` —
  roadmap, kickoff prompts, slice ledgers.
- `docs/HANDOFF.md` (uppercase!) — current session state and next-up pointer.
- Governing meta-plan: `docs/superpowers/plans/2026-07-09-implementation-roadmap.md`
  (11 greenfield slices A–K).

## The slice-session workflow (lighter protocol, adopted 2026-07-09)

One session per slice. Each session:

1. **Read the kickoff prompt** named in HANDOFF's next-up (reading list, scope,
   boundary). Confirm scope with the user in one message (the **scope nod**).
2. **Branch** `slice-<x>-<name>` from main.
3. **Implement directly from the design tree** — no full writing-plans document
   (exception: Slice B, the state-model rewrite, gets a written plan the user
   reviews first). TDD, frequent commits. Maintain a committed **task ledger**
   (`docs/superpowers/plans/YYYY-MM-DD-slice-<x>-ledger.md`): ordered checklist
   with gates, updated as you work — the resumability record if the session dies.
4. **Subagents only where they pay**: genuinely parallel independent lanes
   (e.g., independent catalogs), plus exactly one **fresh-eyes whole-branch
   review** subagent before merge, followed by one fix wave.
5. **Gates** (all mechanical, all mandatory): `dotnet test` green — the hex-tier
   (Phase-1 generation) suite **never** breaks; determinism byte-identity for
   same config; new goldens frozen once at slice end (red-window inside the
   slice); the slice's REPL surface works.
6. **User checkpoints — exactly three**: scope nod (start), **REPL eyeball
   acceptance** (the taste gate: does it *look right* — user runs/views it),
   merge decision. Don't add approval gates between tasks.
7. **Wrap-up, in order**: merge to main locally · update `docs/HANDOFF.md` ·
   **write the next slice's kickoff prompt** (see below) · push only when the
   user says to.

**Each session writes the next session's kickoff prompt** — informed by what
just landed (real file paths, real interfaces, surprises encountered). Pattern:
`docs/superpowers/plans/YYYY-MM-DD-slice-<x>-kickoff-prompt.md`, modeled on the
Slice A one. This chain is how context flows between clean sessions.

## Hard rules

- **The design is the spec.** Do not re-open design questions during
  implementation; a genuine deviation requires amending the affected
  `docs/design/` doc in the same branch, flagged to the user.
- **Greenfield**: the prototype sim (`EpochSim`, `Sim/*`, per-cell political
  state, v5 serializer) and Unity atlas are reference-only PoC — replaced
  outright, deleted as superseded, no compatibility adapters, no old-golden
  preservation. Git history is the archive.
- **Determinism discipline**: stateless hash rolls keyed (step, actor id,
  channel); fixed iteration order; config artifact-stamped; hex tier never
  persisted.
- `unity/ProjectSettings` churn stays uncommitted, always.
- Piping to the REPL: use bash `printf 'cmd\n' | dotnet run --project
  src/Inspector` (PowerShell mangles the first stdin line).
- Grep/diff can render `//` comments as `\ ` — Read the file before "fixing"
  phantom malformed comments.
