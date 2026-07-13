# Slice K5 Ledger — System Stage & Closeout

Branch `slice-k5-system` off main (`9fb40cc`, the CE wrap-up), in worktree
`.claude/worktrees/slice-k3-panels` (rebranched — the K3 branch merged long
ago; the folder name is cosmetic). Governing plan:
`2026-07-11-slice-k-roadmap.md` row K5; kickoff:
`2026-07-12-slice-k5-kickoff-prompt.md`; design of record:
`docs/superpowers/specs/2026-07-11-unity-atlas-design.md` §zoom (five LOD
bands, hex→orbit crossfade) + diagram §3 (zoom row, orbit mock draw code)
/ §7 (SystemStage row: "orbit-view scene fragment swapped in at system
LOD: star, bodies, port, facilities", deps AtlasReadModel + CameraRig).

Scope nod 2026-07-12: kickoff scope confirmed unamended — hex→system LOD
crossfade (fifth band), SystemStage (orbit view: star, bodies, the port,
facilities; same SelectionModel semantics, same InspectorDock panels),
final PoC-remnant sweep (incl. the K1 HideAndDontSave sweep carried since
K2), full acceptance scenario (seed 42: watch 40 epochs, click the Alloys
War siege hex, drill to its system, open the threads panel), roadmap
close. Boundary: no new sim mechanics · no controller HUD · carried flags
stay carried · fork switch-back UI stays backlog unless trivially free.

## Key surfaces (mapped at kickoff)

- Hex tier IS the orbit data: `Generator.Generate(context, hex)` →
  `HexResult.System` (StarSystem: Designation/GivenName/Arrangement/
  Stars; Star: TypeId/TypeName/Age/Slots/CompanionSlotIndex; OrbitSlot:
  Index/Band(Inner|Habitable|Outer)/Body; Body: Kind/Size/Atmosphere/
  Hydrographics/Biosphere/Settlement/Satellites/Name). Pure function,
  computed on demand, never persisted — HexQuery.SystemSummary is the
  one-line precedent.
- Epoch overlays at a hex: `state.Ports` (tier, owner), `state.Facilities`
  (TypeId → `Infrastructure.Get((InfraTypeId)f.TypeId)`, Tier, Condition,
  OwnerActorId; `MarketEngine.IsActive/AttachedMarketIndex`), projects
  (InFlight at hex), fleets (TotalHulls > 0 at hex), live POIs — all in
  HexQuery/SelectionModel already.
- `LodBands`: enum Galaxy/Domains/Region/Hex + extent-relative floors;
  fade curves are pure statics. CameraRig publishes BandChanged +
  ZoomChanged; `_minDistance = 2.5f`; hex step ≈ 1.732 world units, so
  the System band keys on ABSOLUTE distance (one hex is fixed-size).
- SelectionModel: plane picking → WorldToHex, click = press w/o wander,
  priority port→site→freight→fleet→POI→hex; dock routes SelectionKind →
  PanelType (port opens Market AND owner Polity, clearUnpinned:false).
- No text labels on the map anywhere — identity lives in the tooltip and
  panels (HexTooltip rest-delay 0.45s). The stage follows.
- Layers = one mesh of billboards (StarGen/AtlasBillboard, corner UV +
  world/px dual size), generated textures (SolidDot/Ring/SoftDot in
  AtlasTextures), authored glyph atlas (AtlasGlyph 17 cells).
- SimHost events: `Loaded` (new world) / `TimeChanged` (same world, new
  moment) — SystemStage subscribes both, like every layer.
- Scene is REBUILT from code: `AtlasViewSceneSetup.RunFromCli` /
  `StarGen > Setup Atlas Scene`; the rebuilt Atlas.unity is committed.

## Tasks

- [x] **T0 — Branch + baseline**: `slice-k5-system` @ 9fb40cc, gitignored
      trio already present (worktree reused from K3), `dotnet test`
      832/832 green at branch.
- [x] **T1 — Core SystemQuery, TDD** (`src/Core/Atlas/SystemQuery.cs` +
      .meta, 13 tests, 845/845): `SystemQuery.At(model, eye, hex)` → SystemInfo —
      the hex-tier system laid out for the stage (stars w/ companion
      slots, orbit rows: slot index/band/body kind/size/name/settlement/
      satellites) + epoch overlays ATTACHED to orbits deterministically
      (port → most-settled body else habitable else star orbit; facility
      by type affinity: Mine→belt/rocky, Skimmer→gas giant, Agri→best
      biosphere, Excavation→ruins-adjacent else rocky, processing/heavy/
      support→port body; id-order stable). Empty reach → SystemInfo with
      no stars (the stage draws nothing but overlays on the deep-space
      ring — no text on the map). DECISION: deterministic
      layout angles are a pure hash of (hex, slot index) — view-only, no
      RollChannel consumed.
- [x] **T2 — FacilityPanel, TDD** (Core + PanelViews card, 4 tests):
      facility click opens a compact typed card (type/family/tier/
      condition/active/owner/produces + its market link) — the K3
      Project/Shipment precedent; §9 gains a Facility row at wrap-up.
      DECISION (design amendment, flagged): "same panels" in the spec
      reads as "the same panel SYSTEM"; a facility click routing to the
      Market panel would bury the subject. SelectionKind.Facility added.
- [x] **T3 — LodBands System band** (+ 3 EditMode tests, 14/14):
      `LodBand.System` below Hex; `SystemFloorAbs = 5` ABSOLUTE distance
      (one hex is fixed-size) guarded `min(5, 0.6·RegionFloor·extent)` so
      a toy galaxy keeps its Hex band; `MapFade` (1→0 over floor·2→floor)
      / `StageFade` complementary. MapFade folds INTO LaneFade/GlyphFade/
      LatticeAlpha (those layers fade for free); new OnZoom hooks fade
      ports (billboard `_Tint`, shader gained the uniform), news
      (additive: rgb×fade too), domain field (`_MapFade` uniform),
      nature/price fields (Sprites/Default material color). Starfield
      deliberately stays — space is still space under the orbit view.
- [x] **T4 — SystemStage** (`unity/Assets/Atlas/SystemStage.cs`, scene
      fragment): builds the orbit view from SystemInfo at the camera's
      focus hex when the band enters System — star(s) as tinted SoftDot
      billboards, orbit rings as thin line-loop meshes (the hex-ring
      pattern), bodies as SolidDot billboards (kind-tinted, size-scaled),
      port ring + facility glyphs docked at their attached orbits,
      in-flight site glyphs; crossfade drives stage alpha + map-layer
      alpha via T3 curves; focus-hex change while in-band rebuilds;
      rides Loaded/TimeChanged; all runtime meshes/materials
      HideAndDontSave, destroyed on teardown.
- [x] **T5 — Stage picking + panels**: SelectionModel consults the stage
      first when it's live — nearest pickable within a screen-px radius
      (port → Market+Polity exactly as on the map; facility →
      SelectionKind.Facility → FacilityPanel; body/star → Hex panel;
      site → Project). Hover over a pickable feeds the tooltip a stage
      line (body name · kind · settlement) through the existing
      rest-delay path.
- [x] **T6 — PoC-remnant sweep**: empty `unity/Assets/Scripts` dir +
      .meta removed; grep for PoC-era symbols
      (AtlasController/GalaxyService/SystemView/OrbitLayout/CellView…) —
      none compile anywhere; K1 runtime meshes/textures sweep: every
      `new Mesh`/`new Texture2D`/`new Material` in Assets/Atlas carries
      HideAndDontSave or an owning OnDestroy (the carried-since-K2 flag —
      close it).
- [x] **T7 — Scene rebuild + gates**: scene setup grows the SystemStage
      GO; Atlas.unity rebuilt + committed (ProjectSettings churn NOT);
      `dotnet test` green ×3 · goldens untouched (`git status` clean) ·
      determinism suites in the count · EditMode green · AtlasSmoke every
      lens.
- [x] **T8 — Fresh-eyes whole-branch review** + one fix wave. Verdict:
      "NOT READY — 2 confirmed bugs"; every mechanical gate verified
      holding by running it (Core purity, 283 unique meta guids, goldens/
      non-Atlas Core untouched, no RollChannel in SystemQuery, id-order,
      event symmetry, leak-free rebuild path, crossfade math, boundary).
      Fix wave (all landed, test-first, 851/851):
      1. Facility owner link opened the wrong CORPORATION — corp panel
         subjects are registry ids, the card carried the actor id
         (id spaces differ). FacilityCard gains OwnerCorpId; PanelViews
         routes with it (pinned by ACorpOwnerCarriesItsRegistryId test).
      2. Under-construction facilities rendered TWICE (registry row
         exists at groundbreaking) with an always-false "idle" label —
         SystemQuery now folds uncommissioned rows into their sites
         (one thing, one mark; pinned by AnUncommissionedFacility test);
         dead idle arms removed stage- and panel-side (Active ≡
         Commissioned today).
      3. (plausible→fixed) Port lost click ties to its own body at the
         shared center — StagePick gains Priority; near-ties go
         port-first (the map's priority order, kept).
      4. "1 moons" tooltip plural; ledger void-placard overstatement
         corrected.
      Declined-as-noted: additive starlight fades as fade² through the
      crossfade (cosmetic, reads fine) · EditMode/smoke re-ran post-fix
      instead of in-review.
- [x] **T9 — USER: the K taste gate — ACCEPTED** — seed 42, watch 40 epochs, click
      the Alloys War siege hex, drill to its system, open the threads
      panel. (The whole-K acceptance, not just K5's.)
  - Eyeball wave 1 (2026-07-12, five findings):
    (1) NEW System info panel — PanelType.System over SystemQuery
    (designation/name/arrangement/stars/every orbit row/port+facility+
    site links); stage star/body clicks open it (SelectionKind.System);
    the Hex panel's system line is now a link to it.
    (2) Design language re-based on the ORBIT DIAGRAM OPTION A artifact
    (claude.ai/code/artifact/236896d9-…): a thin #262C3F ring for EVERY
    slot (SystemInfo grows Rings — all slots, occupied or not; TDD),
    belts as dashed #9A8F7A rings (no body dot — the belt IS its ring),
    habitable band as a #3FBF7F @13% annulus, star = core + additive
    halo, moons as #B9BFD0 dots, settled worlds ringed #FFBF4F, mock
    body colors (rocky C9A06A / ice A8D8E8 / gas E08840). DIVERGENCE
    kept deliberately: no text labels on the stage (the mock has them) —
    names live in the tooltip + the System panel, per the no-map-text
    grammar. Flagged to the user.
    (3) Fit-to-hex: each system's layout scales so the outermost ring
    stays inside its hex (FitRadius 0.78 < inradius) — no bleed.
    (4) Pop-in killed: the stage renders EVERY visible system hex while
    the crossfade is live (world-space meshes, rebuild keyed on the
    visible-hex set, cap 160; StagePick carries its hex; SystemQuery is
    re-run per hex per rebuild — cache if panning ever janks).
    (5) Selection ring stroke is screen-constant ~3px (the lattice's
    weight) instead of the bulky 0.08+ clamp.
  - Eyeball wave 2 (2026-07-12): stage now COPLANAR with the lattice
    (z −0.02, was −0.30 — the lift parallaxed against the grid; draw
    order rides renderQueue) · belt dashes fine-grained (2-on-1-off of
    96 segments, was 2-on-7-off scattered ticks) · orbit rings hairline
    (0.0045, belts 0.010) · moons hug their body's rim (0.68×half-size
    offset; the old 1.8×full-size offset scattered gas-giant moons a
    ring-gap away). TRAP RE-LEARNED: a Unity batch launched while the
    editor holds the project dies in ~2s with exit 1 and a 1KB log —
    and an `echo exit: $?` tail masks the code. Two smoke runs read as
    green that never rendered; verify the LOG SIZE + png mtimes, and
    put the real exit code in the task's failure path.
- [x] **T10 — Wrap-up**: merge via scratch worktree · HANDOFF (roadmap
      CLOSED → gap-list backlog `2026-07-11-design-acceptance.md`; keep
      the CE-chained K6 pointer) · tick K5 + "Slice K complete" ·
      diagram §7/§9 rows + zoom caption amended, republished · push on
      say-so.

## Decisions / deviations

- **`SystemInfo` name collision**: UnityEngine.SystemInfo shadows the
  Core record in Unity code — SystemStage aliases it
  (`using SystemInfo = StarGen.Core.Atlas.SystemInfo`). Core name kept
  (right name in Core's own vocabulary).
- **Facility affinity, not facility addresses**: facilities are
  hex-anchored in the registries; the stage docks them at bodies by TYPE
  affinity (SystemQuery.FacilityOrbit) — a view-side layout rule, no new
  sim state. Deep-space station ring for attachments with no body.
- **Belt renders as its ring**: PlanetoidBelt draws 6 fragments riding a
  wider, brighter orbit ring — a belt is not a dot.
- **Stage labels ride the tooltip** (no text on the map — the K1–K3
  grammar holds): hovering a stage pickable retitles the hex tooltip
  with the thing's line; hex context dims below.
- Both events feed one dirty flag: Loaded and TimeChanged just mark the
  stage dirty; the rebuild happens in Update only while the stage is
  live (crossfade begun) at the camera's focus hex.

## Carried flags (inherited)

- Credit-loop equilibrium → contract economy (landed in CE; verify the
  seed-42 taste-gate world reflects it).
- Unbounded keyframe memory during unattended play — note stands.
- Per-lens readability deep-dives → gap list.
- Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-K).
- Branch switch-back UI for timeline forks → backlog unless free here.
