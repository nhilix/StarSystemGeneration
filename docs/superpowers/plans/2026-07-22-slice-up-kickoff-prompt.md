# Slice UP kickoff — local Unity Pipeline spike (warm-editor gates + the eyeball grid)

You are opening **Slice UP**: a small, time-boxed **spike slice** that installs
the experimental Unity CLI + `com.unity.pipeline` package locally, maps their
real command surface, and proves a new warm-editor workflow — ending with a
prototype **eyeball grid**: atlas smoke shots across many seeds, not just
seed-42, so the taste gate can see the reach of new atlas work at a glance.
Full research record (what these tools are, what's known/unknown):
`https://claude.ai/code/artifact/ba6f69d5-5423-4ead-a795-9ea5aff0caab` and the
memory note `unity-cli-pipeline-research-artifact`.

Branch `slice-up-unity-pipeline` from main. This is a spike: success is
**knowledge + a working prototype**, not polish. Batchmode remains the
canonical merge gate for all slices until this spike's findings say otherwise.

## Hard constraints (user-set, non-negotiable)

- **LOCAL ONLY — no cloud of any kind.** No Unity Cloud services, no Build
  Automation, no Pipeline Automation, no beta sign-ups. The ONE permitted
  account touchpoint is `unity auth login` IF `unity pipeline install` hard-
  requires it (research says it does). If anything beyond that one-time login
  turns out to be required — org enrollment, cloud project linking, closed-beta
  approval — **stop, document the wall in the ledger, and report**; do not
  work around it.
- **Serial atlas editing.** This slice runs ONLY after Slice AC is merged and
  its worker retired. One editor instance, one session touching `unity/` at a
  time. Never run this alongside another atlas session.
- **Zero sim behavior.** No `src/Core` sim changes. `dotnet test` stays green
  untouched; seed-42 golden byte-untouched. New code is editor-side tooling
  (`unity/Assets/Editor/`) and possibly a small Inspector-side artifact-listing
  helper — nothing the sim reads.
- **Everything is pre-1.0** (CLI 0.1.0-beta.x, package 0.3.x-exp). Pin and
  record exact versions of both in the ledger. Trust `--help` output over
  docs.unity.com — the reference docs are known to lag shipped commands.

## Read first, in order

1. The research memo (artifact above) — the capability map, the doc-lag
   warning, the Windows `install.ps1` caveat, the known unknowns (licensing
   tier, EditMode-vs-PlayMode split in `run_tests`).
2. `unity/Assets/Editor/AtlasSmoke.cs` and `AtlasViewSceneSetup.cs` — the two
   existing batchmode entry points; the eyeball grid extends the AtlasSmoke
   pattern (`SimHost.LoadArtifact()` → capture loop).
3. `docs/HANDOFF.md` (Slice AC wrap section, once it exists) — current
   Unity-side state, EditMode base count, any new AC layers the grid should
   capture.
4. `src/Inspector/SweepRunner.cs` + `runs/sweeps/` layout — how sweep variants
   and their artifacts are organized; the grid consumes artifacts, it does not
   run sweeps.
5. `CLAUDE.md` — batchmode command lines (docs/superpowers/plans/2026-07-08-*
   quote the exact invocations), worktree manifest-copy trap, REPL piping rule.

Note: `unity/Packages/manifest.json` + `packages-lock.json` are **gitignored**
in this repo — `unity pipeline install` mutates local-only state. Record the
manifest delta verbatim in the ledger so the install is reproducible on a
fresh checkout/worktree.

## Tasks, in order (ledger: `docs/superpowers/plans/YYYY-MM-DD-slice-up-ledger.md`)

1. **UP1 — Install & pin the CLI.** Find the real Windows install route
   (release notes say `install.ps1`; the docs page shows a bash one-liner —
   verify from the live page, not the research summary). Install, record
   `unity --version`, pin the channel. Gate: `unity editors -i --format json`
   returns our 6000.5.2f1.
2. **UP2 — Map the command surface.** Enumerate `unity --help` and every
   subcommand's `--help` (including release-notes-only commands: `test`,
   `build`, `run`, `eval`, `license`, `status`, `doctor`, `env`, `logs` —
   confirm which actually exist in the installed version). Commit the findings
   as `docs/superpowers/specs/YYYY-MM-DD-unity-cli-pipeline-command-reference.md`
   — the "good understanding of the cli and pipeline commands available" the
   user asked for. Raw help text + a short annotated table; note divergences
   from docs.unity.com.
3. **UP3 — Install the pipeline package.** `unity auth login` (the one
   permitted login) → `unity pipeline install` → `unity pipeline list` shows
   Installed. Open the editor once so the package compiles. Then enumerate the
   registered pipeline commands via `unity command` (list form) and append the
   full command inventory to the UP2 reference doc — categories, args,
   dry_run/confirm flags, auth token handling.
4. **UP4 — Prove the warm-editor gates.** With the editor OPEN (the exact
   scenario batchmode cannot handle):
   - `run_tests` (EditMode) 3×: results identical to a batchmode
     `-runTests` baseline run, and to each other. Record wall-clock for both
     paths.
   - `recompile` after touching a comment in an editor script: clean result,
     console readable.
   - `menu`-fire "StarGen/Atlas Smoke Shots": all PNGs land, editor survives.
   Gate: all three behave deterministically across repeats. Any flakiness or
   hang gets documented with logs — that IS a spike finding, not a failure.
5. **UP5 — The eyeball grid prototype.** A new editor tool (menu +
   `RunFromCli` twin, following the AtlasSmoke pattern) that takes a directory
   of sim artifacts (start: generate 4–6 seeds' artifacts via the Inspector),
   loops `SimHost.LoadArtifact()` → captures a chosen shot set per seed →
   writes `atlas-grid/<seed>-<lens>.png` plus a single self-contained
   `atlas-grid/index.html` contact sheet (seeds × lenses). Drive it once via
   the warm-editor `menu` command end-to-end. Gate: user can open one HTML
   file and eyeball every seed. Keep output gitignored (like `atlas-smoke*`).
6. **UP6 — Verdict & wrap.** Ledger verdict section: is the warm-editor path
   deterministic and pleasant enough to (a) wire into slice-session Unity
   gates as the preferred path with batchmode fallback, and (b) grow the grid
   into the standard multi-seed taste gate? Update `docs/HANDOFF.md`; author a
   follow-up kickoff ONLY if the verdict is adopt (otherwise the reference doc
   + ledger are the deliverable); merge per the standard three-checkpoint
   protocol (scope nod · eyeball = the UP5 grid itself · merge decision), one
   fable whole-branch review before merge, push on merge.

## Timebox & bail-out

One session, roughly half a day of work. If UP1–UP3 hit a wall (broken Windows
install, cloud-gated install, package won't compile against 6000.5.2f1), stop
there: commit the reference doc for whatever WAS reachable, write the wall
into the ledger + HANDOFF, and wrap. A documented dead end is a successful
spike outcome. UP5 without UP4's determinism proof is still worth shipping —
the grid can fall back to a batchmode `-executeMethod` loop (slower, editor
closed) if the warm-editor path disappoints.

## Boundary (out of scope)

- Anything cloud (see hard constraints).
- Rewiring existing slice gates to the new path (that's the follow-up slice,
  if the verdict says adopt).
- Sweep-runner changes — SweepRunner stays pure dotnet; the grid consumes
  artifacts it's given.
- PlayMode tests, builds, asset-generation MCP tools, and the unity-mcp
  bridge (it coexists untouched; do not uninstall or reconfigure it).
- Any `unity/ProjectSettings` commits (standing rule — churn stays local).
