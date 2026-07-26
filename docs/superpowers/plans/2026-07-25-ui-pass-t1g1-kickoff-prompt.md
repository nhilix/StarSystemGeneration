# UI design pass — Tier 1, Group 1 kickoff: camera, navigation & the LOD spine

You are opening **the first Tier 1 deep dive** of the atlas UI design pass:
**camera, navigation & the LOD spine** — `CameraRig`, `LodBands`, the
band × layer matrix, `AtlasRoot`'s update/cost model, framing and anchor
behavior. This group runs **first by design**: every other group's "is this
readable / when does this resolve" questions are answered in bands and fades,
so what you decide here is load-bearing for all five dives that follow.

**Read first, in order:**
1. `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md` — the pass
   spec: principles, the Tier 1 template, deliverables. In full.
2. `docs/design/ui/inventory.md` **§1 (camera & navigation), §2 (the LOD
   spine), §9 (cross-cutting), §10 (partition)** — the evidence base. §8
   (SystemStage) for the crossfade you own; skim the rest for how other
   elements ride your curves.
3. `docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` — the pass ledger;
   continue it (Tier 1 / Group 1 section).
4. `.claude/skills/driving-the-unity-editor/SKILL.md` — before your first
   Unity command. You own the editor for this session (serial atlas access).

## What this is — and isn't

A **design** session. Outputs are a design doc + a mock artifact + ledger
evidence — **zero committed atlas code**. Runtime *feel experiments* in the
warm editor via `unity command eval` are encouraged (tweak damping, thresholds,
clamps in play mode; nothing lands in `unity/`) — Tier 0 proved eval is the
right spike vehicle: no files added, no assets dirtied.

**Nothing in the current atlas is sacred** (governing principle 1). The
inventory documents what IS as evidence, never as constraint. Every number in
`LodBands` and `CameraRig` is a choice someone made under different
information — re-derive or replace freely, with evidence.

## The design questions this dive must answer

**Navigation verbs (inventory §1 debts — all live):**
- **Yaw**: the map has a permanent fixed north with no code path to turn it.
  Decide: is fixed north a *feature* (map legibility, glyph billboarding) or a
  gap? If a gap, what's the binding and the reset gesture?
- **Pan bounds**: `_targetFocus` is unclamped — the player can pan into empty
  void with no rubber-band and no way home. Design the boundary behavior.
- **Framing**: `FitTo` frames disc bounds (every cell + 48u padding), not
  inhabited content — recipe D (`epoch 7 5 21`) puts ~90% empty starfield in
  frame. Design content-aware framing, and decide what "fit" means on
  degenerate galaxies.
- **Missing verbs**: no focus-on-selection, no frame-all, no
  zoom-to-fit-selection; `JumpTo` hardcodes distance 24 regardless of galaxy
  scale. Define the full verb set the atlas *should* have, each with its
  easing (the `SetView` jump-cut vs damped-glide distinction is currently
  accidental — make it a designed grammar).

**The LOD spine (inventory §2):**
- **The band × layer matrix as a designed object.** Which layer resolves at
  which band, and why — currently five fade curves with hand-tuned windows
  and no stated rationale. Re-derive the matrix from information-priority
  arguments (what should a player see at galaxy altitude? at region?), then
  keep/move the numbers.
- **Hysteresis**: `BandFor` is a bare threshold — anything gated on `Band`
  chatters at rest on a boundary. Design the damping.
- **Threshold feedback**: crossing a band silently changes the map; the
  player gets no signal that *zoom* is what changed it. Decide whether/how
  crossings should be legible (and whether band names belong in chrome —
  coordinate with Group 5, don't design their widget).
- **Scale invariance**: bands are extent-relative but the System floor is
  absolute, and grid radius 12 vs 21 are different galaxies. Verify the
  continuum feels equivalent across radii; design for the range, not seed 42.
- **The signature transition**: the whole map dissolves into SystemStage
  through `MapFade`/`StageFade`. This is the atlas's single biggest motion
  moment — own it: curve shapes, what leads/lags, starfield's deliberate
  persistence, selection highlight's deliberate non-fading.

**Cost model:** `AtlasRoot.OnZoomChanged` drives every layer per zoom tick —
note what your proposals cost per frame and where lazy builds (lattice)
constrain fade windows.

**Template completeness:** the spec's seven template sections all apply —
weight them honestly (this group is interaction- and motion-heavy, icon-light;
"custom icons/elements pay" here likely means navigation affordances: a
band/altitude indicator, a compass-if-yaw, a "you are lost" rescue). Empty and
degenerate states are first-class: what does navigation feel like on
`atlas-grid-degen`'s galaxies? Where the evidence strains a standing decision
(§9.3), challenge explicitly with a mock.

## Evidence

Generate what you cite (skill has the recipes; cite by **regeneration recipe,
never filename** — grid output is disposable):
- A **zoom series** (fixed seed, stepped `zoom`) and a **pitch series** — the
  continuum as strips, per band boundary.
- At least one series on a **degenerate** galaxy and one at a different
  radius.
- **Measure, don't infer from a framed shot** (Tier 0's lesson: the "near-
  empty" golden had 72 ports — the sparseness was the framing). Counts come
  from queries, looks come from captures.

## Deliverables & gate

- **`docs/design/ui/camera-nav-lod.md`** — the accepted design, present
  tense, spec's design-is-the-spec rules. The band × layer matrix and the
  navigation verb set are its centerpieces.
- **Mock artifact** — for this group an **interactive simulator** beats
  static token blocks: the zoom continuum with live band boundaries and fade
  curves (current values vs proposed side-by-side), scrubbing altitude and
  watching layers resolve. Follow artifact conventions (new artifact, own
  favicon; note URL in the ledger).
- **Gate (one checkpoint):** user eyeballs the mock + doc — offer them a
  live-editor feel pass too (eval-applied proposed values in play mode) since
  camera feel doesn't fully survive a static mock.

## Wrap-up (after the eyeball)

- Commit doc + ledger; update `docs/HANDOFF.md`; push. Sync Trello if
  reachable (Tier 1 card https://trello.com/c/xEym8e27 — note G1 done).
- **Do NOT write the Group 2 kickoff** — tier/group kickoffs are
  orchestrator-authored after each gate (user decision, 2026-07-25). Hand
  back: doc path, artifact URL, and the decisions Groups 2–4 inherit (their
  layers ride your curves).
- Release the editor (close or leave clean; no manual Ctrl+S of the atlas
  scene after captures).

## Boundary

- No committed atlas code, no sim behavior, no `docs/design/` edits outside
  `docs/design/ui/`.
- Don't design Group 5's chrome widgets or Group 2–4's encodings — where your
  decisions touch them (threshold feedback chrome, fade windows), state the
  interface and leave the design to their dives.
- Slice CS (chrome capture tool) is a separate queued slice blocking Groups
  5–6 only — not your concern.
