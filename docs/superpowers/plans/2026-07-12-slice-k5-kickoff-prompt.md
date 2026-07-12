# Slice K5 Kickoff — Session Prompt

You are starting **Slice K5 (System stage & closeout)** — the last of five
sub-slices delivering the Unity atlas, under the lighter protocol in
`/CLAUDE.md` (read it first). K1 shipped the skeleton instrument, K2 the
lens catalog, K3 selection & panels (the inspectable atlas), K4 the
timeline (TimeMachine keyframes, TimelineStrip, play/step/scrub,
resolution forks, run-seed from genesis). K5 delivers the **hex→system
LOD crossfade and the SystemStage orbit view**, sweeps the last PoC
remnants, runs the full acceptance scenario, and **closes the epoch-sim
implementation roadmap**.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn
   stays uncommitted, always).
2. `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md` — row K5 and
   the gates; K5's wrap-up closes the roadmap itself.
3. **The interface spec**:
   `docs/superpowers/specs/2026-07-11-unity-atlas-design.md` — the zoom
   continuum (five LOD bands, hex→orbit crossfade) and SystemStage rows;
   living diagram `docs/diagrams/unity-atlas-design.html` §6 (zoom) + §7
   (SystemStage row).
4. **The K4 ledger** — REQUIRED:
   `docs/superpowers/plans/2026-07-12-slice-k4-ledger.md` (the time
   model as built, the two-event split, worktree traps, review
   declined-as-noted list).
5. The K3 ledger `2026-07-12-slice-k3-ledger.md` (SelectionModel/dock
   architecture the system stage REUSES — same selection, same panels).
6. `docs/superpowers/plans/2026-07-11-slice-k-kickoff-prompt.md` — the
   whole-K inherited context, the final taste gate.
7. `docs/HANDOFF.md` — current state.
8. Gap-list backlog for the close:
   `docs/superpowers/specs/2026-07-11-design-acceptance.md`.

## What K4 left ready (build on this, don't reinvent)

- **Core** (`src/Core/Atlas`, 800-test suite): TimeMachine (keyframes as
  delta saves, byte-identical scrub, replay-not-truncate, resolution
  forks as TimelineBranch; a genesis-base artifact round-trips — the
  run-seed flow), TimelineQueries (density buckets), plus everything
  K1–K3 (15 lens queries, every §9 PanelQuery, Era/Chronicle/Handoff/
  Registry/Legend/Hex queries).
- **SimHost is the one writer**, now with TWO events: `Loaded` = new
  world (camera refit, Open Threads greets, dock closes every panel) ·
  `TimeChanged` = same world new moment (layers ShowAll, no refit; dock
  rebuilds unpinned panels in place — PINNED panels intentionally keep
  their captured moment). SystemStage must ride these same events.
  RunSeed(seed, radius, epochs) bases the timeline on the UNSTEPPED
  genesis world and auto-plays to the target (`_playUntilEpoch`).
- **Chrome** (`unity/Assets/Atlas`): AtlasChrome owns the one UIDocument
  + guard + named hosts (top bar / rail / dock / tooltip / legend /
  timeline). TimelineStrip is the host pattern to copy. Scrollers
  hidden by convention. Legend sits at bottom 122px (above the strip).
- **CameraRig/LodBands**: bands Galaxy→Domains→Region→Hex exist
  (`LodBands.BandFor`); K5 adds the System band below Hex and the
  crossfade. SelectionModel picks on a plane (no colliders) — the
  system stage swaps in a scene fragment; decide picking there early.
- **AtlasSmoke** renders every lens in batch; UI Toolkit still does NOT
  render in batch captures — panel/orbit chrome is eyeballed in-editor.

## Scope (K5, roadmap row)

- **Hex→system LOD crossfade** — the fifth band; the map fades out, the
  orbit view fades in at one hex's depth.
- **SystemStage** — the orbit view scene fragment: star, bodies, the
  port, facilities; same SelectionModel semantics, same InspectorDock
  panels (a facility click opens the same typed panels).
- **Final PoC-remnant sweep** — anything `unity/Assets/Scripts`-era
  left, plus the K1 runtime meshes/textures HideAndDontSave sweep
  (carried since K2).
- **Full acceptance scenario** (the K taste gate): seed 42 — watch 40
  epochs, click the Alloys War siege hex, drill to its system, open the
  threads panel.
- **Roadmap close**: HANDOFF points at the gap-list backlog
  (`2026-07-11-design-acceptance.md`); the 11-slice greenfield roadmap
  is done.

**Boundary:** no new sim mechanics · no controller HUD (play tier) ·
carried flags stay carried (credit-loop → contract economy; per-lens
readability deep-dives → gap list) · branch switch-back UI for timeline
forks stays backlog unless trivially cheap while touching the strip.

## Traps K3/K4 hit (save yourself the hour)

- **Worktree setup**: `unity/Packages/manifest.json`,
  `packages-lock.json`, `src/Core/csc.rsp` are GITIGNORED — copy all
  three from the main checkout BEFORE any Unity batch run.
- **Batch vs editor**: batchmode cannot run while an editor holds the
  project (check `unity/Temp/UnityLockfile`); stale test-results XML
  lies — delete before re-runs. The editor MCP bridge verifies compiles
  live BUT its approval is per-project: a fresh worktree starts revoked
  (Project Settings > AI > Unity MCP) — ask the user to approve it
  early if the editor will be open.
- **CRLF**: the goldens check out CRLF on Windows; anything diffing
  file text against `ArtifactSerializer.ToText` must normalize
  (SimHost.LoadArtifact does).
- **Main is usually not checked out anywhere** (root checkout belongs
  to a parallel session): merge via a scratch worktree
  (`git worktree add <scratchpad>/k5-merge main`), then remove it.
  `git worktree prune` first — stale registrations linger.
- The scene is REBUILT from code (`StarGen > Setup Atlas Scene` /
  AtlasViewSceneSetup.RunFromCli) — new components must be added there,
  and the rebuilt `Assets/Scenes/Atlas.unity` is committed (settings
  churn is not).

## Carried flags (know they exist)

- **Credit-loop equilibrium** → contract-economy slice (session may be
  live in the root checkout — never share it).
- Unbounded keyframe memory during unattended play (delta strings
  accrue per step) — fine for sessions, cap someday.
- Per-lens readability deep-dives → gap list.
- Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-K).

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-k5-system` from main **in a fresh worktree**; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-k5-ledger.md`.
3. TDD any Core-side additions (orbit-view read model if needed —
   check what HexQuery/system data already expose before adding);
   EditMode where it pays; the crossfade/orbit eyeball is in-editor.
4. Gates: `dotnet test` green · goldens untouched · determinism
   untouched · EditMode green · AtlasSmoke every lens.
5. User gates: scope nod · **the K taste gate** (seed 42: watch 40
   epochs, click the Alloys War siege hex, drill to its system, open
   threads) · merge decision.
6. Wrap-up: merge · HANDOFF (→ gap-list backlog; roadmap CLOSED) ·
   tick K5 · republish the diagram · push only on say-so.

- [ ] Slice K5 complete
