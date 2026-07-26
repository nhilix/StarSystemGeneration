# Slice CS kickoff — chrome shots (make the panels photographable)

You are opening **Slice CS**. Today the atlas has no capture path that renders
its chrome: every existing tool goes through `cam.Render()`, which cannot see a
UI Toolkit overlay panel. TopBar, LensRail, LegendPanel, TimelineStrip,
HexTooltip and all **27 InspectorDock panel builders** appear in zero of the 72
committed map shots. CS builds the tool that fixes that.

This is a **tooling slice**. No sim behavior, no atlas redesign, no new atlas
surfaces. If it grows past "one editor tool + a capture plan + a default suite,"
you have drifted.

Read `.claude/skills/driving-the-unity-editor/SKILL.md` **first** — it is the
operating manual and the thing this slice extends with a third capture path.

## Why this slice exists

The atlas UI design pass (`docs/superpowers/specs/2026-07-24-ui-design-pass-design.md`)
reached its Tier-0 gate and found that its two most information-dense groups —
**Chrome** and **Panels & selection** — cannot be reviewed at all, because
nothing can photograph them. That is where relations, chronicle, markets and
polity summaries live. The pass is blocked on this for those groups.

**The hard part is already solved.** Tier 0 spiked a working end-to-end capture
through `unity command eval` and proved it: map + full chrome + a driven
Market/Polity/Relations stack, composited in one 1600×1000 frame. CS turns that
spike into a committed, repeatable tool.

## Read first

1. **`docs/design/ui/inventory.md` §11 — "Capturing the chrome".** The whole
   proven recipe, the measurements, and the two cautions. **Do not re-derive
   this**; it cost a Tier-0 detour to establish. Start from it.
2. `docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` §0.11 — what was tried
   and why the obvious paths fail.
3. `.claude/skills/driving-the-unity-editor/SKILL.md` — the five silent traps.
   Trap 2 (poll for artifacts, never trust the exit code) is load-bearing here.
4. `unity/Assets/Editor/AtlasSmoke.cs` — the framing, styling and capture code
   you are reusing, and the `SetAndStyle` hand-mirror you are **not** repeating.
5. `unity/Assets/Editor/AtlasGrid.cs` — the worked example for a
   `[MenuItem]` + `RunFromCli()` + `[CliCommand]` triple, and the `#if
   HAS_UNITY_PIPELINE` guard.
6. `unity/Assets/Atlas/InspectorDock.cs` (`Show`, `PanelRequest`) and
   `unity/Assets/Atlas/LensRail.cs` — the public surface you drive.

## What is already known to work

From inventory §11, condensed — the full detail is there:

- `set_autotick --enable true` → `editor_play` → assign
  `PanelSettings.targetTexture` → camera render to a second RT →
  alpha-composite.
- **Play mode is the unlock, twice over.** It is the only context where the UI
  renders at all, *and* the only one where the chrome exists: every chrome module
  builds in `OnEnable` and none is `[ExecuteAlways]`, so EditMode has nothing to
  photograph. It also means the captured chrome is genuine, not a reconstruction
  — this path does **not** inherit the `AtlasSmoke.SetAndStyle` drift risk.
- `InspectorDock.Show(PanelRequest, clearUnpinned)` and `PanelRequest` are
  public. Selection, lens state, epoch and camera are all drivable.
- Measured healthy: 611,412 of 1,600,000 px non-zero alpha at 1600×1000.

## The one real design problem: play mode and domain reload

The spike worked because a **human-paced sequence of separate CLI calls**
straddled the play-mode transition. A single synchronous `[CliCommand]` cannot:
entering play mode triggers a domain reload that tears down static state
mid-call.

Resolve this deliberately — it is the slice's central decision. Options, with a
recommendation:

- **A capture-plan file + a runtime runner (recommended).** Write a plan to disk
  (which shots, which panels/subjects, which lens state, which framing), enter
  play mode, and let a runtime component execute it across frames, write the
  PNGs and a done-marker, then stop. Survives domain reload by construction
  (the plan is on disk, not in memory), and the CLI just polls for artifacts —
  exactly trap 2's established pattern.
- **A documented multi-call CLI sequence.** Closest to the spike, cheapest to
  build, but leaves orchestration in the caller's hands forever and is
  awkward to run as one gate.
- **Disable domain reload** (Enter Play Mode Options). Makes a synchronous
  command *look* possible, but it changes project-wide play-mode semantics for
  everyone to serve one tool. Do not lead with this.

Pick one at the scope nod and say why in the ledger.

## Tasks (ledger: `docs/superpowers/plans/YYYY-MM-DD-slice-cs-ledger.md`)

1. **CS1 — the capture core.** Assign the panel target texture, render the
   camera, alpha-composite, encode. Reuse `AtlasSmoke`'s framing and the
   `_AtlasFocalY` / `_AtlasViewportPx` globals rather than re-deriving them.
   **Assert the UI layer is non-degenerate** — a minimum non-zero-alpha pixel
   fraction — and *throw* if it is not. An all-transparent capture must fail
   loudly, not produce a map shot that silently looks like the old ones.
   Gate: one composited PNG with chrome visibly present.
2. **CS2 — play-mode lifecycle, safely.** Enter/exit, autotick on/off, and the
   **asset-safety discipline**: `PanelSettings` is a committed asset,
   `targetTexture` mutates it, and play-mode asset edits persist. Null it on
   exit, and on the failure path too — a thrown exception must not leave the
   asset dirty. Gate: run the tool twice, then `git status` is clean and
   `unity/Assets/Atlas/PanelSettings.asset` still reads
   `m_TargetTexture: {fileID: 0}`.
3. **CS3 — the driving surface.** Parameterize what a shot contains: panel type
   + subject id (+ `SubId`), lens state, camera framing, epoch/keyframe. This is
   what makes the tool worth building — a fixed screenshot of Open Threads is
   near-useless; *any panel, any subject, on demand* is the deliverable.
   Gate: shoot three different panels of three different subjects from one run.
4. **CS4 — a default panel suite.** The `AtlasSmoke` equivalent for chrome:
   one shot per panel builder that has a natural subject in the golden, plus the
   bare chrome. **Choose subjects that are not empty** — see the sampling note
   below. Emit a self-contained contact sheet like `atlas_grid` does.
   Gate: the sheet covers every panel type you claim it covers; anything skipped
   is *logged as skipped*, never silently dropped.
5. **CS5 — resolution behavior.** `PanelSettings` is `ScaleMode 2`
   (ConstantPhysicalSize) with reference resolution 1920×1080 and fallback DPI 96
   (`unity/Assets/Atlas/PanelSettings.asset:21-27`). Capture the suite at two or
   three sizes and record what actually happens to layout and text. Tier 1's
   template asks "text legibility across resolutions" directly, and right now
   nobody knows the answer.
6. **CS6 — wrap.** Ledger · **update the skill** with the third capture path and
   any new silent traps · HANDOFF · Trello · one fable whole-branch review + fix
   wave · merge + push.

## A sampling note (learned the hard way in Tier 0)

`AtlasSmoke` shoots every lens at `extent × 0.30` anchored on port 0. That framing
made a **72-port, 69-lane, 15-open-thread galaxy with two live wars** read as a
near-empty world — an error that survived into a Tier-0 draft before direct
measurement caught it. When you pick default subjects for CS4, **query the state
for a subject that actually has content** (a market with stock, a polity with
factions, a war in progress) rather than taking index 0 and hoping. Log which
subject each shot used, so a future reader can tell "this panel is empty" from
"this subject was empty."

## Hard constraints

- **Zero sim behavior.** No `src/Core` / `src/Inspector` / `tests/` changes.
  `dotnet test` stays green untouched; seed-42 golden byte-untouched. Assert it.
- **Do not replace `AtlasSmoke` or `AtlasGrid`.** This is a *third* path, for
  chrome and panels. The map-only tools stay exactly as they are.
- **The editor assembly must keep compiling without `com.unity.pipeline`**
  (the manifest is gitignored). Guard every pipeline face with
  `#if HAS_UNITY_PIPELINE`, and re-prove the clean-clone compile the way WG did.
- **Validation must throw** (`ArgumentException`), never return
  `{success:false}` — the skill's rule; a false success still yields exit 0.
- **Captures must not write the scene asset.** Use `EnsureScene()`, never
  `SetupScene()` (Slice WG). And do not manually save the scene after a run.
- **`unity/ProjectSettings` churn stays uncommitted**, always.
- **Serial atlas editing.** One editor, one session touching `unity/`.

## Boundary (out of scope)

- **Any UI change whatsoever.** The first composited shot already shows truncated
  Market table headers (`PRI…`, `CLE…`, `BLACK BO…`). **Do not fix them.** Those
  are findings for the UI pass's Panels group; fixing them here pre-empts a
  design decision that has not been made and destroys the pass's evidence.
  Record what you notice in the ledger and move on.
- No new panels, no new lenses, no new atlas surfaces.
- The merge-gate story. This is an **evidence tool, not a gate** — it does not
  need a batchmode twin, and PlayMode tests stay the unopened can WG left shut.
  If a batchmode path falls out for free, note it; do not build for it.
- Re-auditing the CLI surface. `eval`, `editor_play`, `set_autotick` and
  `screenshot` are the commands in play; their behavior is recorded in inventory
  §11. If one misbehaves, re-check its `--help` and note it.

## Timebox

Half to one session. **CS1–CS3 are the slice**; if those land and the tool can
shoot any panel on demand, that is a success even if CS4's suite is thin and
CS5 slips. A tool that shoots one hardcoded panel is *not* a success — CS3 is
what the design pass actually needs.

## Sequencing

This slice **unblocks the UI design pass's Tier 1 groups 5 (Chrome) and 6
(Panels & selection)**. Tier 1 groups 1–4 (camera/LOD, map fields, marks, lanes)
do not depend on it and can proceed in parallel — they are map-side and the
existing grid/smoke evidence serves them. Confirm the ordering with the user at
the scope nod; the partition those group numbers refer to is
`docs/design/ui/inventory.md` §10, which was still awaiting the user's nod when
this prompt was written.
