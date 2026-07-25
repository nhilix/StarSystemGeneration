# Slice WG kickoff — wire the warm gates (and stop the scene churn)

You are opening **Slice WG**, the adopt-path follow-up Slice UP's verdict
earned. UP proved the warm-editor workflow is deterministic and ~30× faster on
the EditMode gate, and shipped the instruments; **WG makes it the way the
project actually works** — and pays off the one debt that makes cheap captures
annoying.

This is a **small, mechanical slice**. No sim behavior, no new subsystems. If it
grows past "wiring + one idempotency fix," you have drifted.

Read `.claude/skills/driving-the-unity-editor/SKILL.md` **first** — it is the
distilled operating manual and the thing this slice extends.

## Why this slice exists

UP left the warm path *proven but unwired*: every future slice still reads
CLAUDE.md's batchmode command lines and pays a cold launch per gate. And because
captures are now cheap, they will run **often** — which makes the scene-churn
debt (below) go from "an annoyance once a slice" to "a revert after every run."

## Hard constraints

- **Zero sim behavior.** No `src/Core` / `src/Inspector` / `tests/` changes.
  `dotnet test` stays green untouched; seed-42 golden byte-untouched. Assert it.
- **Batchmode must keep working.** It stays the canonical merge gate and the
  clean-clone path. Every change is "prefer warm, fall back to batchmode" —
  never "replace batchmode."
- **The editor assembly must keep compiling without `com.unity.pipeline`**
  (gitignored manifest — UP's Critical review finding). Re-prove it the way UP
  did: remove the manifest line, batchmode compile, confirm 0 `error CS` and that
  the built `StarGen.AtlasView.Editor.dll` contains `AtlasGrid` but not
  `Unity.Pipeline`/`CliCommand`.
- **Serial atlas editing.** One editor, one session touching `unity/`.
- **`unity/ProjectSettings` churn stays uncommitted**, always.

## Read first

1. `.claude/skills/driving-the-unity-editor/SKILL.md` — the five silent traps.
   Internalize trap 1 (`key=value` args are ignored *while reporting
   `success:true`*) before writing a single command line.
2. `docs/superpowers/plans/2026-07-24-slice-up-ledger.md` — measurements, the
   fix wave, and why each trap exists.
3. `unity/Assets/Editor/AtlasViewSceneSetup.cs` — the `SetupScene()` that
   rebuilds the scene graph every run. **This is WG1's target.**
4. `unity/Assets/Editor/AtlasGrid.cs` + `AtlasSmoke.cs` — both call
   `SetupScene()` unconditionally at entry.
5. `CLAUDE.md` (the Unity gate references) and
   `docs/superpowers/plans/2026-07-08-atlas-setup-knobs.md` — the batchmode
   command lines that currently define the gates.

## Tasks (ledger: `docs/superpowers/plans/YYYY-MM-DD-slice-wg-ledger.md`)

1. **WG1 — kill the scene churn.** `AtlasViewSceneSetup.SetupScene()` re-emits
   the whole object graph with fresh fileIDs on every call, so **every** capture
   run dirties `unity/Assets/Scenes/Atlas.unity` by ~650± lines of pure
   renumbering (semantically identical — verified in UP by normalizing the
   numbers: the added and removed line multisets match). UP hand-reverted this
   after every run.
   Make it **idempotent**: skip the rebuild when the open scene already matches
   the current layer set, with an explicit `force` path for a deliberate rebuild
   (K4/K5 precedent: the scene IS regenerated on purpose at slice end).
   Gate: run `atlas_grid` twice in a row and `git status` stays clean; a forced
   rebuild still produces a correct scene, verified by an AtlasSmoke run.
   **This is the highest-value task in the slice — do it first.**
2. **WG2 — wire the gates.** Update CLAUDE.md's Unity gate guidance to
   "warm-editor preferred, batchmode fallback," pointing at the skill rather than
   duplicating command lines (one source of truth — the skill). Add "start the
   editor once at slice start" to the slice-session workflow, including the
   `-automated` flag, and say plainly when batchmode is still mandatory (merge
   gate, clean clone, no editor).
3. **WG3 — make the grid the standard taste gate.** Decide (with the user) the
   default eyeball recipe: how many seeds, which lenses, generated how. Note the
   user's steer from UP's eyeball: *the set of lenses, seed count and capture
   viewpoint should be decided per investigation* — so WG3 is about giving the
   eyeball a good **default**, not freezing it. Document it in the workflow and
   in the skill. Consider a committed seed-set list so grids are comparable
   across slices.
4. **WG4 — one-command gate run (only if it earns its place).** A small helper
   that runs compile + EditMode + a chosen capture against the warm editor and
   reports pass/fail, honouring trap 2 (poll for artifacts, never trust the
   `menu` exit code). **Skip this if WG2 already makes the gates a two-line
   copy-paste** — do not build a framework for three commands.
5. **WG5 — wrap.** Ledger, HANDOFF, update the skill with anything learned,
   Trello, one fable whole-branch review, merge + push.

## Boundary (out of scope)

- Anything cloud. Still LOCAL ONLY.
- Sim behavior of any kind.
- New atlas surfaces or lenses — that is the **UI design pass**
  (`docs/superpowers/specs/2026-07-24-ui-design-pass-design.md`), a separate
  queued slice. WG only changes *how gates run*, never what the atlas shows.
- `unity shell`, `unity mcp`, PlayMode tests, builds — mapped in UP's reference
  doc, deliberately unexercised. Do not open those cans here.
- Re-mapping the CLI surface. UP's reference doc is a dated snapshot; if a
  command misbehaves, re-check its `--help` and note it — do not re-audit 141
  commands.

## Timebox

Half a session. WG1 alone is worth the slice; if WG1 lands and WG2 is written,
that is a successful outcome even if WG3/WG4 slip to a later pass.

## A note on sequencing

The user may prefer the **atlas UI design pass** first — it was queued before UP
and is the more interesting work. WG is small, unblocking, and makes the design
pass materially easier (cheap multi-seed captures with no scene churn to revert),
so doing WG first is the recommendation, not a requirement. Confirm at the scope
nod.
