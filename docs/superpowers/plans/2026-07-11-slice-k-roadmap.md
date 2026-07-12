# Slice K Roadmap — Unity Atlas in Five Sub-Slices

> **This is the governing plan for Slice K.** Row K of the epoch-sim
> implementation roadmap (`2026-07-09-implementation-roadmap.md`) is too large
> for one session; it is delivered as five sub-slices K1–K5, each a full slice
> session under the lighter protocol in `/CLAUDE.md` (own branch off main,
> three user gates, merge to main, kickoff prompt written for the next
> sub-slice). This document supersedes the single-session framing of
> `2026-07-11-slice-k-kickoff-prompt.md`; that prompt's reading list, "what J
> left ready," and boundary remain authoritative and are inherited by every
> sub-slice kickoff.

**Goal:** the Slice K deliverable unchanged — the atlas renders the simulated
history (domains/lanes/price/war/faction layers, panels, drill-down, timeline)
per the validated interface design.

**The design is fixed:** `docs/superpowers/specs/2026-07-11-unity-atlas-design.md`
+ the living diagram `docs/diagrams/unity-atlas-design.html`. Sub-slices change
*when* pieces land, never *what* lands. Deviations amend the spec in-branch.

**Approach (user-confirmed 2026-07-11):** walking skeleton first, then widen.
K1 ships a thin end-to-end instrument (artifact → read model → rendered map →
zoom); K2–K5 widen a working atlas. Rejected: horizontal layers (a complete
read model with no visual eyeball gate and no consumer — rework setup) and
fewer/bigger slices (re-creates the bite-off-too-much problem).

## Transition rules

1. **The PoC atlas dies in K1, entirely and up front** (user-confirmed:
   aggressive removal — Core has moved too far for the PoC to ever run again).
   `unity/Assets/Scripts/Atlas/` (incl. its Tests), `AtlasSceneSetup.cs`, and
   `AtlasAcceptance.cs` are deleted at K1 branch start. Rendering lessons (hex
   mesh, palette discipline, navigator, UI Toolkit panels) are salvaged by
   *reading* — git history is the archive — never by keeping stale code
   compiling.
2. **Core stays Unity-free and behavior-frozen.** All lens/panel value
   derivations live in `src/Core/Atlas` (plain C#, xUnit-covered); Unity code
   is draw + input only. K adds NO sim behavior: golden untouched, determinism
   byte-identity untouched, hex-tier suite green — in every sub-slice.
3. **Every new `src/Core` file carries its two-line `.meta`** (src/Core is the
   Unity package `com.stargen.core`).
4. **Each sub-slice merges a working (if partial) atlas to main.** No
   long-lived integration branch.
5. **Eyes from day one:** every read-model query is Eye-parameterized
   (`God | ActorId`) from K1, God-only implemented; the controller eye stays a
   reserved seam (play tier, not K).

## The sub-slices

| # | Sub-slice | Contents | Depends on | Eyeball gate |
|---|---|---|---|---|
| **K1** | **Skeleton instrument** | Delete PoC atlas outright · `src/Core/Atlas`: EyeContext, `AtlasReadModel` (minimal), LensQueries for nature rasters + domains + lanes · SimHost (artifact load in-editor) · MapSurface (hex/star rendering) · CameraRig/LODController (galaxy→domains→region→hex bands) · provisional lens toggle | — | Load seed 42: the P1 image — wilds dark, domain glows with organic borders and contested overlap, lanes as literal highways; zoom continuum galaxy→hex works |
| **K2** | **Lens catalog** | All remaining lenses: traffic, fleets, price-per-good (parameterized chip), war, tension, tech, plague, news, POIs · left-rail lens stack UI, grouped (POLITICAL / LOGISTICS / KNOWLEDGE / NARRATIVE / NATURE) · LensQueries port `EpochMapView` value/color derivations with xUnit parity tests | K1 | Flip through every lens on seed 42 next to REPL `emap` — same story, rendered |
| **K3** | **Selection & panels** | SelectionModel + hover hex tooltip · InspectorDock (pinnable) · typed panels + PanelQueries/HandoffQueries/ChronicleQueries · **Open Threads as the opening screen** · registry drawer (`find`/`stats`/`goods`/`knobs`) · top bar (eye chip, year/era, config stamp) | K2 | Click a port → polity/market panels populate with REPL-parity numbers; threads rows jump the camera to their subjects |
| **K4** | **Timeline** | TimeMachine (epoch keyframes as delta saves) · TimelineStrip (era bands, event-density sparkline, world-year scrubber, active tick) · play/step coarse + fine (`YearsPerEpoch`) · resolution change forks a branch from the current keyframe · SimHost run-seed in-editor | K1 + K2 (parallel with K3 possible — separate worktree) | Watch 40 epochs animate on the domains lens; scrub back to a mid-war year; step fine |
| **K5** | **System stage & closeout** | Hex→system LOD crossfade · SystemStage (orbit view: star, bodies, port, facilities; same selection model, same panels) · final PoC-remnant sweep · full acceptance scenario · roadmap close: HANDOFF points at the gap-list backlog (`2026-07-11-design-acceptance.md`) | K3 + K4 | **The taste gate from the K kickoff:** seed 42, watch 40 epochs, click the Alloys War siege hex and drill to its system, open the threads panel |

## Sequencing rationale

- **K1 retires the risk:** the Core-package-into-Unity pipeline, hex mesh at
  epoch-sim scale, and the LOD camera are the unknowns; everything after K1
  widens a proven instrument.
- **K1's lens trio is the signature image** (`space-and-travel.md` §P1): nature
  base + domains + lanes is the minimum that *looks like the game*, so the
  first eyeball gate is a real taste gate, not a tech demo.
- **K3 before K5:** the system stage reuses the selection model and panels;
  drill-down without inspectors would be scenery.
- **K4 needs only K1+K2** (the animated map is lenses over time) and may run
  parallel to K3 in a separate worktree — never a shared checkout.
- **Stepping waits for K4:** K1–K3 view a loaded artifact at a fixed year;
  SimHost grows run-seed/step when the timeline exists to drive it
  (artifact-load first, per the spec's data-source answer).

## Sub-slice gates (every sub-slice)

- `dotnet test` green — hex-tier suite + all epoch-sim suites untouched at 100%.
- Golden untouched, determinism byte-identity untouched (K adds no sim
  behavior; Core additions are read-only view helpers).
- Read-model logic xUnit-covered; Unity EditMode tests for presentation where
  they pay (the PoC's pattern).
- Eyeball acceptance as tabled above (the user runs Unity and looks).
- Spec conformance: the sub-slice matches the interface spec; deviations amend
  it in-branch, flagged. The living diagram
  (`docs/diagrams/unity-atlas-design.html`) is updated and republished as the
  build teaches us things.

## Process

Per `/CLAUDE.md`: one session per sub-slice, branch `slice-k<n>-<name>` from
main, committed task ledger, direct implementation from the spec (no
task-level plan documents), one fresh-eyes whole-branch review + one fix wave,
exactly three user gates, wrap-up writes the next sub-slice's kickoff prompt.
K5's wrap-up closes the epoch-sim roadmap itself.

- [x] K1 — Skeleton instrument
- [x] K2 — Lens catalog (accepted 2026-07-12 as foundational groundwork;
      carried: per-lens legends → K3 chrome, per-lens readability
      deep-dives → backlog)
- [x] K3 — Selection & panels (accepted 2026-07-12; four eyeball waves:
      screen-size scaling, hidden scrollers, menu scanline rebind, hex-ring
      selection; carried: credit-loop equilibrium flag → contract economy)
- [ ] K4 — Timeline
- [ ] K5 — System stage & closeout
