# StarSystemGeneration — Claude Code Instructions

Deterministic procedural galaxy generator + epoch history simulation. C# —
`src/Core` (netstandard2.1, no Unity deps), `src/Inspector` (REPL, net8.0),
`tests/Core.Tests` (xUnit), `unity/` (**the atlas** — a read-only viewer over
sim artifacts; built by slices K1–K5, caught up to the sim by AC). Build/test:
`dotnet test StarSystemGeneration.sln`. Unity gates: see **Unity gates** below.

## Documentation planes (never mix them)

- `docs/design/` — **the living systems design** (final feature designs, present
  tense, no process). This is the spec for all implementation. Start at
  `docs/design/README.md`.
- `docs/superpowers/specs/` — dated decision records; `docs/superpowers/plans/` —
  roadmap, kickoff prompts, slice ledgers.
- `docs/HANDOFF.md` (uppercase!) — current session state and next-up pointer.
- Governing meta-plan: `docs/superpowers/plans/2026-07-09-implementation-roadmap.md`
  (11 greenfield slices A–K).
- **Trello** (`StarSystemGeneration` board, MCP server `trello` in
  `.mcp.json` at project scope — travels into worktrees, needs a one-time
  OAuth approval per machine) tracks live task/todo status: Backlog →
  Kickoff Ready → In Progress → Eyeball / Merge Gate → Merged. Advisory,
  not a gate — HANDOFF.md stays the authoritative resumability record if
  Trello is ever unreachable.

## The slice-session workflow (lighter protocol, adopted 2026-07-09)

One session per slice. The orchestrator spawns each slice session as a fresh
`claude` window-worker in the primary psmux session (see **Spawning worker
Claude sessions** below), pointed at the slice's kickoff prompt. Within the
slice session, subagents handle individual task work and parallel lanes
(subagent-driven-development) — the two layers never swap roles: the
orchestrator doesn't implement slices inline, and slice sessions don't spawn
psmux windows of their own. Each slice session:

1. **Read the kickoff prompt** named in HANDOFF's next-up (reading list, scope,
   boundary). Confirm scope with the user in one message (the **scope nod**).
2. **Branch** `slice-<x>-<name>` from main.
3. **Implement directly from the design tree** — no full writing-plans document
   (exception: Slice B, the state-model rewrite, gets a written plan the user
   reviews first). TDD, frequent commits. Maintain a committed **task ledger**
   (`docs/superpowers/plans/YYYY-MM-DD-slice-<x>-ledger.md`): ordered checklist
   with gates, updated as you work — the resumability record if the session dies.
   Always routed through subagent-driven-development, never inline edits by
   the slice session itself — see **Model usage** below.
4. **Subagents only where they pay**: genuinely parallel independent lanes
   (e.g., independent catalogs), plus exactly one **fresh-eyes whole-branch
   review** subagent before merge, followed by one fix wave. The review
   subagent is pinned to `model: fable` — see **Model usage** below.
5. **Gates** (all mechanical, all mandatory): `dotnet test` green — the hex-tier
   (Phase-1 generation) suite **never** breaks; determinism byte-identity for
   same config; new goldens frozen once at slice end (red-window inside the
   slice); the slice's REPL surface works. **If the slice touches `unity/`**:
   compile clean + EditMode green — start the editor once at slice start and run
   them warm (see **Unity gates** below), with a batchmode pass at the merge gate.
6. **User checkpoints — exactly three**: scope nod (start), **REPL eyeball
   acceptance** (the taste gate: does it *look right* — user runs/views it),
   merge decision. Don't add approval gates between tasks. How to *write* a
   checkpoint: **Talking to the user at checkpoints** below — it is binding.
7. **Wrap-up, in order**: merge to main locally · update `docs/HANDOFF.md` ·
   **write the next slice's kickoff prompt** (see below) · sync the Trello
   board (move the finished card to Merged, file anything new surfaced
   mid-session) if reachable · **push on merge by default** (user standing
   preference, 2026-07-20: "push whenever you merge unless I say otherwise").

**Each session writes the next session's kickoff prompt** — informed by what
just landed (real file paths, real interfaces, surprises encountered). Pattern:
`docs/superpowers/plans/YYYY-MM-DD-slice-<x>-kickoff-prompt.md`, modeled on the
Slice A one. This chain is how context flows between clean sessions. A kickoff
prompt states **what each of its gates asks the user for** — the decision, not
"present findings" — so the worker knows at the start what the brief must
contain and can gather the evidence that answers it.

## Talking to the user at checkpoints (adopted 2026-07-27)

The user's judgment steers this project and their reading time is the scarce
resource. **The ledger, the design doc and HANDOFF carry the dense record; a
question to the user is not the place to reproduce it.** These apply to the
orchestrator and every worker.

- **Decision briefs — ≤150 words before the question.** One sentence on what
  is being decided · the options, one line each · your recommendation with a
  one-line why · a **pointer** to the evidence (ledger §, doc path, artifact
  URL), never the evidence itself. If it won't fit, the ask isn't ready: the
  fix is to decide the parts you can and ask only the residue — not to write
  more.
- **`AskUserQuestion` whenever the choice is discrete.** Rendered options beat
  prose the user must mine for what the options even were. Long content can't
  ride in the same turn (it gets hidden) — present it, then ask next turn.
- **Ask before the dive, not after.** A direction question is short when asked
  early and long when asked late, because a finished investigation has to be
  re-narrated to be ratified. Surface forks when they appear.
- **Show first for anything visual.** Eyeball gates lead with the capture, the
  mock or the artifact; prose is a caption, not a preamble.
- **Fewer, better asks — and own the tradeoff.** Decide cheap-to-reverse calls
  yourself, record them in the ledger, list them in wrap-up. Escalate what is
  expensive to reverse, and *say which it is* in the brief.
- **Wrap-up reports the same way**: a short headline (what landed · what needs
  the user · what's next), then the pointer. HANDOFF is the record — the chat
  message should not duplicate it.

## Model usage

Full rationale: `docs/superpowers/specs/2026-07-12-model-usage-guidelines-design.md`.

The main session is the **orchestrator** and stays on Fable (weekly-capped,
user-driven via `/model` — not something Claude switches itself). Keep the
orchestrator's own direct spend to low-volume, high-judgment work only: user
dialogue, brainstorming Q&A, slice sequencing and spawn decisions, reviewing
the next kickoff prompt before spawning. Slice-session duties (scope nod,
task ledger, HANDOFF.md wrap-up, authoring the next kickoff prompt) belong to
the spawned worker. Slices are delegated whole, as window-worker sessions;
those in turn delegate task work to subagents with an explicit model per
role, so volume work never touches the Fable budget:

| Role | Model |
|---|---|
| Slice session (window-worker; runs the slice, dispatches its subagents) | Opus |
| Spec authoring (brainstorming's design doc) | Opus |
| Plan authoring (writing-plans' implementation plan) | Opus |
| Slice implementation tasks (subagent-driven-development) | Sonnet default, Opus escalation |
| Fresh-eyes whole-branch review (pre-merge) | Fable |
| Explore/research lookups | Sonnet |

Escalate a single implementation task from Sonnet to Opus when it: touches
conservation/determinism invariants (money, hash rolls, iteration order);
spans multiple `src/Core` subsystems in one task; or is itself a design
judgment call rather than mechanical implementation. Decide this per task at
dispatch time, not up front for the whole slice.

## Spawning worker Claude sessions (psmux)

This machine runs psmux (Windows tmux clone; `tmux`-compatible CLI). The
orchestrator spawns full `claude` sessions as **windows inside the primary
attached session** so the user can flip to any worker and watch. The canonical
use is kicking off a slice: one window per slice session, named `slice-<x>`,
opened with that slice's kickoff prompt. Only the orchestrator spawns windows;
slice sessions delegate downward via subagents, not sideways via psmux.

- **Never `new-session`** — no headless/detached sessions. Workers are always
  windows (or panes) of the session this orchestrator is running in. Get the
  current session name with `psmux display -p '#S'` and target `<session>:<name>`.
- Spawn without stealing focus, then kick off (quoting rules are load-bearing:
  send command text with `-l`, and Enter as a **separate** send-keys call —
  combined forms get mangled going through PowerShell):

  ```powershell
  psmux new-window -d -n slice-c -c C:\Users\Jaaco\Documents\Dev\StarSystemGeneration
  psmux send-keys -t "$(psmux display -p '#S'):slice-c" -l 'claude --model opus --permission-mode auto --remote-control slice-c "Read docs/superpowers/plans/2026-XX-XX-slice-c-kickoff-prompt.md and run the slice-session workflow from CLAUDE.md."'
  psmux send-keys -t "$(psmux display -p '#S'):slice-c" Enter
  ```

  The worker's first act is the scope nod (workflow step 1) — it will state
  scope and wait; the user confirms by flipping to the window or from their
  phone via Remote Control. Announce the spawn to the user so they know a
  worker is waiting on them.

- **Wait for the shell prompt before sending ANY keys to a new window**
  (root-caused 2026-07-22): keys sent while pwsh is still initializing
  permanently desync the pane's rendering — the window looks dead (raw
  command text, no `PS >` prefix, no repaint ever) while input still
  executes invisibly (claude genuinely launches and runs blind). There is
  no recovery (resize/repaint don't help): kill the window and respawn
  with a fresh name. Prevention: after `new-window`, poll `capture-pane`
  until a `PS …>` prompt appears (pwsh cold start can exceed 5s; a
  `list-windows` round trip is NOT enough of a wait), only then send-keys.
- An untrusted working dir shows a folder-trust prompt ~5–10s after launch; a
  plain `Enter` via send-keys accepts it (one-time per folder).
- Launch workers with `--permission-mode auto` (the user's standing preference:
  full autonomy for all claude sessions) — a worker in manual mode stalls on
  the first prompt nobody's watching.
- Launch workers with `--remote-control <window-name>` (another standing
  preference) so the user can monitor threads and answer worker questions —
  scope nods included — from their phone. Always pass the name explicitly
  (matching the psmux window name), otherwise the kickoff prompt gets parsed
  as the optional session-name argument.
- Long kickoff prompts: point the worker at a committed kickoff-prompt file
  (the existing pattern) rather than inlining multi-line text through send-keys.
- **One psmux command per PowerShell invocation, verified after each.**
  Chaining psmux calls in one invocation (`kill-window; new-window`,
  `new-window && send-keys`) can silently drop a command. Worse, an
  unresolved `-t` target makes send-keys/capture-pane fall back to the
  **active** window — the orchestrator captures its own pane and reads it as
  a running worker (this produced a phantom "spawned" report once). After
  `new-window`, confirm with `list-windows` and target by **index**
  (`<session>:<index>`); confirm a launch by pane content you recognize from
  the kickoff (or `pane_pid`'s child processes), never by footer chrome,
  which looks identical across claude sessions.
- Monitor with `psmux capture-pane -p -t <target>`; clean up finished workers
  with `psmux kill-window -t <target>`.
- **A recently-killed window name cannot be reused**: `new-window -n <name>`
  with the dead window's name fails silently (twice-confirmed). Pick a fresh
  name (e.g. `dx-worker` after `slice-dx` died) — the `--remote-control` name
  can still be the canonical slice name; only the psmux window name must differ.

## Unity gates (warm editor preferred, batchmode fallback)

**Read the `driving-the-unity-editor` skill before your first Unity command.**
It is the single source of truth for the command lines — do not duplicate them
here or in kickoff prompts. Its failure modes are *silent*: the worst is that
`unity command` takes **flag-style** args (`--path X`), while a `key=value` form
is **ignored while still reporting `success: true`**.

- **Start the editor once at slice start**, with `-automated` (mandatory — else
  modal popups can block a run). Then the whole session's Unity gates run warm:
  EditMode in **~1.5s** instead of ~50s, atlas captures in ~3s instead of a full
  editor launch. Proven equivalent to batchmode in Slice UP (identical sorted
  result *sets*, not just counts).
- **Batchmode stays the canonical merge gate**, and is the only path when the
  editor is closed or on a clean clone. Verdict revisit is due once the tooling
  passes 1.0 — it is pre-1.0 and moved a full version in two days.
- **`unity/Packages/manifest.json` is gitignored**, so a fresh clone/worktree
  lacks `com.unity.pipeline`. The editor assembly still compiles without it
  (`#if HAS_UNITY_PIPELINE`); you just lose `unity command`. The skill has the
  one-line restore.
- **Captures no longer rewrite the scene asset** (Slice WG). `AtlasSmoke`/
  `AtlasGrid` call `AtlasViewSceneSetup.EnsureScene()`, which loads-and-verifies
  without writing. Only the deliberate **`StarGen/Setup Atlas Scene`** rebuild
  saves — that one is intended and commits as `chore: atlas scene rebuilt`.
  Captures do run *inside* the committed scene, so the in-memory scene diverges
  (toggled lens visibility, camera framing); Unity doesn't mark it dirty, so
  nothing prompts — but **don't manually save it after a capture**.
- **The multi-seed eyeball**: `unity command atlas_grid` renders seeds × lenses
  to a self-contained contact sheet. Lens set, seed count and viewpoint are
  **decided per investigation** — the defaults are a starting point, not a fixed
  gate.

## Hard rules

- **The design is the spec.** Do not re-open design questions during
  implementation; a genuine deviation requires amending the affected
  `docs/design/` doc in the same branch, flagged to the user.
- **Greenfield**: the prototype sim (`EpochSim`, `Sim/*`, per-cell political
  state, v5 serializer) and the *original PoC hex-board* atlas are reference-only
  — replaced outright, deleted as superseded, no compatibility adapters, no
  old-golden preservation. Git history is the archive. **The replacement already
  landed**: `unity/` now holds the real 2.5D atlas (K1–K5, caught up by AC), and
  is product, not PoC.
- **Determinism discipline**: stateless hash rolls keyed (step, actor id,
  channel); fixed iteration order; config artifact-stamped; hex tier never
  persisted.
- `unity/ProjectSettings` churn stays uncommitted, always.
- Piping to the REPL: use bash `printf 'cmd\n' | dotnet run --project
  src/Inspector` (PowerShell mangles the first stdin line).
- Grep/diff can render `//` comments as `\ ` — Read the file before "fixing"
  phantom malformed comments.
