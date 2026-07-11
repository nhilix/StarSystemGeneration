# Unity Atlas (Slice K) — Interface Design

**Date:** 2026-07-11 · **Status:** validated with user (design-only session)
**Visual companion:** `docs/diagrams/unity-atlas-design.html` — the living design
diagram artifact (interactive Galaxy-view mockup, zoom continuum, architecture),
published at https://claude.ai/code/artifact/b8ce4102-cf3d-41c6-8cb8-69c1eda3a081.
**Feeds:** the Slice K implementation session
(`docs/superpowers/plans/2026-07-11-slice-k-kickoff-prompt.md`). This spec answers
the two scope questions that kickoff poses (data source: both, artifact-load
first; skeleton layers survive as the nature lens group).

## Decision summary

The Unity layer is rebuilt greenfield (PoC atlas deleted as superseded) as **one
continuous instrument** over the epoch sim — a god-mode atlas first, with the
future character/controller view designed in from the start as a second **Eye**
over the same views, not a second UI.

Chosen approach (of three explored): **lens-stack over one continuous map**.
Rejected: screen-stack rebuild (the eyes/time requirements cut across every
screen and would be retrofitted into each); ledger workspace (the map must be
home, not one tab among instruments — its one good idea, registry browsing,
survives as a panel-system drawer).

## The five pillars

1. **One instrument.** Galaxy → domains → region → hex → system is a single
   camera on a single scene with five LOD bands — no screen stack.
2. **Eyes.** Every read-model query is parameterized by an observer context
   `(scope: God | ActorId, worldYear)`. God reads truth registries; a controller
   eye reads belief snapshots, news-delayed log, and fog beyond reach — through
   the same query API. Views never know which eye is looking.
3. **Lenses.** Each map layer is an independent renderer module — a pure function
   of (Eye, ReadModel) → visual primitives. Lenses stack and toggle freely.
4. **Panels.** Selection opens typed inspectors docked beside the map — the
   REPL's panels made visual and pinnable, each with checkable REPL parity.
5. **Timeline.** Era bands, event-density sparkline, world-year scrubber, active
   tick resolution; play/step at coarse or fine tick.

## The Galaxy view (the centerpiece)

Dark wilds, port-domain glows with organic borders, lanes as literal highways —
the map renders `space-and-travel.md` §P1 directly. Chrome around the map
surface:

- **Top bar:** Eye selector chip · world-year + epoch + era name · config stamp
  (seed, radius, artifact id).
- **Left rail:** the lens stack, grouped — POLITICAL (domains, war, tension),
  LOGISTICS (lanes, traffic, fleets, price-per-good), KNOWLEDGE (tech, plague,
  news), NARRATIVE (POIs), NATURE (the genesis rasters). Parameterized lenses
  carry their argument in the chip (`price ▾ provisions`).
- **Map surface:** stars, domain glows with visible contested overlap, lanes
  weighted by traffic, fleet glyphs, POI marks, war fronts and blockades. Hover =
  hex tooltip; click = select + open panel.
- **Right dock:** typed inspector panels, pinnable for comparison.
- **Bottom:** the timeline strip.

## The zoom continuum (five LOD bands)

| Band | What resolves |
|---|---|
| **Galaxy** | The entire disc in one view — arms, bulge, halo; the settled reach reads as one small cluster of domain glow. |
| **Domains** | The settled reach fills the screen: glow fields, lane highways, wilds dark. No grid, no labels. |
| **Region** | A handful of domains; ports labeled with tier, fleets and POIs resolve, contested overlap visible; grid only a hint. |
| **Hex** | One region fills the viewport and the lattice resolves: every `Hex(q,r)` shows its content — systems, belts, empty reaches, the port hex, sited facilities, service-radius tint. |
| **System** | A single hex fills the viewport and crossfades to the orbit view: star, bodies, the port, facilities. Same selection model, same panels. |

What fades per band is a property of each lens (fade curves owned by the
LODController), not global state.

## The physical truth — nature lenses

The REPL's `map` (natural raster, cell lattice) vs `emap` (political, hex
registries) duality is **not** two modes in the atlas: nature lenses are base
layers in the same stack, under the political ones. The full raster set rides
one lens group: `lean · gas · metal · age · minerals · bio · emergence ·
features`. Nature reads the same under every eye (surveyed-detail gating
reserved for the play tier). This is the "skeleton layers survive" answer.

## Time (grounded by Slice J's P7 certification)

- **Scrub.** Past map states are not stored; the `TimeMachine` captures epoch
  keyframes while stepping, **stored as delta saves** (`DeltaSerializer`: base +
  changed layers only — genesis strata never re-record). Scrubbing snaps to a
  keyframe and re-queries every lens and panel. Chronicle/era queries are
  log-backed and need no keyframe.
- **Play — coarse and fine.** The same engine steps a loaded artifact at any
  `YearsPerEpoch` (`GenerationYears` stays the calendar). 25-year epochs down to
  1-year fine ticks — `ewatch`/`estep n years`, rendered. Watching is certified
  not to perturb (byte-identity with unwatched runs).
- **One timeline, one history.** Coarse and fine agree in macro bands, not
  byte-for-byte (keyed-roll path divergence, band-bounded). A timeline belongs
  to one (config, tick-path) run: the strip shows the active tick, and changing
  resolution mid-run **forks a branch** from the current keyframe.

## Architecture

Three layers; Unity references Core in-process. `src/Core` **is** the Unity
package `com.stargen.core` (file reference, `noEngineReferences: true`), so the
read model ships in-package; every new Core file carries its two-line `.meta`.

1. **Core** (`src/Core` — untouched by Slice K beyond read-only view helpers):
   EpochSim (coarse + fine tick), registries, belief layer, event log + its four
   indexes (place/actor/war/character), Artifact I/O + `DeltaSerializer`,
   `HandoffView.OpenThreads`.
2. **Read model** (`src/Core/Atlas` — in-package by default; plain C#, zero
   Unity types, xUnit-coverable in `tests/Core.Tests`): `AtlasReadModel` (the single
   query surface, Eye-parameterized), LensQueries (porting `EpochMapView`'s
   value/color derivations, not the ASCII), PanelQueries, ChronicleQueries /
   EraQueries, HandoffQueries, TimeMachine.
3. **Presentation** (`unity/Assets/Atlas`, thin — draw + input only):
   MapSurface, LensStack + lens modules, CameraRig/LODController, InspectorDock +
   typed panels, TimelineStrip, SelectionModel, SystemStage; ControllerHUD
   reserved for the play tier.

**The Eye crosses layers 2–3.** The controller view is a new Eye, not new views.
Slice J certified the underlying slot mechanism (handover byte-invisible;
occupation is client state, never persisted), so the play-tier Eye rides proven
machinery.

**Writers:** `SimHost` (load artifact/delta, run seed, step) is the only
component that mutates sim state. The atlas is otherwise a viewer and never
writes bases.

## Component inventory

SimHost · EyeContext · AtlasReadModel · TimeMachine · MapSurface · LensStack ·
lens modules (one per layer) · CameraRig/LODController · SelectionModel ·
InspectorDock · typed panels · TimelineStrip · SystemStage — responsibilities
and dependencies tabled in the diagram artifact §7.

## Lens catalog (god eye vs controller eye)

Specified per lens in the diagram artifact §8 — each with its Core-side source
(all exist today; the REPL renders them) and both eyes' behavior. Highlights:
domains (own domain sharp; others as believed extent, stale-dated), war (enemy
strength = believed strength), price (staleness tinted), news (god sees all
pulses; a controller sees its inbox), nature (same under every eye).

## Panel catalog (REPL parity)

**Open Threads is the atlas's opening screen** (`threads` /
`HandoffView.OpenThreads`): half-won wars, loaded tensions, pending successions,
leveraged corporations, burning plagues — each row a jump-to on the map. Then:
Polity, Market, Fleet, War, Relations, Character/Bio, Corporation, POI,
Belief/News/Stances, Chronicle/Eras, and the registry drawer
(`find`/`stats`/`goods`/`knobs` — the ledger idea inside the panel system).
Full mapping in the diagram artifact §9.

## Non-goals & deferred

- **Deferred, seams reserved:** controller HUD + Intent-phase input (play
  tier); multi-eye comparison; save/delta management UI.
- **Non-goals:** editing world state from the atlas; 3D volumetric rendering;
  mobile/touch layout.
- **Replaced outright:** `unity/Assets/Scripts/Atlas` (PoC) — deleted as the new
  atlas lands; rendering lessons (hex mesh, palette discipline, UI Toolkit
  panels) salvaged by choice, not inertia.

## Implementation notes for the K session

- Branch `slice-k-atlas` from main per the kickoff; this spec + the diagram
  artifact are the design; the kickoff's reading list still applies.
- Keep every lens/panel value derivation in the read model so `dotnet test`
  covers it; Unity EditMode tests for the rest.
- Gates unchanged: hex-tier suite green, golden untouched (K adds no sim
  behavior), determinism byte-identity, atlas eyeball (seed 42: watch 40 epochs,
  click the Alloys War siege hex, open the threads panel).
- The diagram artifact is living: update `docs/diagrams/unity-atlas-design.html`
  and republish to its URL as the build teaches us things.
