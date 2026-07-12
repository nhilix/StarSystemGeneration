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
      no stars (stage renders a void placard). DECISION: deterministic
      layout angles are a pure hash of (hex, slot index) — view-only, no
      RollChannel consumed.
- [x] **T2 — FacilityPanel, TDD** (Core + PanelViews card, 4 tests):
      facility click opens a compact typed card (type/family/tier/
      condition/active/owner/produces + its market link) — the K3
      Project/Shipment precedent; §9 gains a Facility row at wrap-up.
      DECISION (design amendment, flagged): "same panels" in the spec
      reads as "the same panel SYSTEM"; a facility click routing to the
      Market panel would bury the subject. SelectionKind.Facility added.
- [ ] **T3 — LodBands System band** (+ EditMode tests): `LodBand.System`
      below Hex; `SystemFloorAbs` absolute-distance threshold + hysteresis
      note; `MapFade`/`StageFade` crossfade curves (map fades out, stage
      fades in across the window above the floor). BandFor keeps its
      (distance, extent) signature.
- [ ] **T4 — SystemStage** (`unity/Assets/Atlas/SystemStage.cs`, scene
      fragment): builds the orbit view from SystemInfo at the camera's
      focus hex when the band enters System — star(s) as tinted SoftDot
      billboards, orbit rings as thin line-loop meshes (the hex-ring
      pattern), bodies as SolidDot billboards (kind-tinted, size-scaled),
      port ring + facility glyphs docked at their attached orbits,
      in-flight site glyphs; crossfade drives stage alpha + map-layer
      alpha via T3 curves; focus-hex change while in-band rebuilds;
      rides Loaded/TimeChanged; all runtime meshes/materials
      HideAndDontSave, destroyed on teardown.
- [ ] **T5 — Stage picking + panels**: SelectionModel consults the stage
      first when it's live — nearest pickable within a screen-px radius
      (port → Market+Polity exactly as on the map; facility →
      SelectionKind.Facility → FacilityPanel; body/star → Hex panel;
      site → Project). Hover over a pickable feeds the tooltip a stage
      line (body name · kind · settlement) through the existing
      rest-delay path.
- [ ] **T6 — PoC-remnant sweep**: empty `unity/Assets/Scripts` dir +
      .meta removed; grep for PoC-era symbols
      (AtlasController/GalaxyService/SystemView/OrbitLayout/CellView…) —
      none compile anywhere; K1 runtime meshes/textures sweep: every
      `new Mesh`/`new Texture2D`/`new Material` in Assets/Atlas carries
      HideAndDontSave or an owning OnDestroy (the carried-since-K2 flag —
      close it).
- [ ] **T7 — Scene rebuild + gates**: scene setup grows the SystemStage
      GO; Atlas.unity rebuilt + committed (ProjectSettings churn NOT);
      `dotnet test` green ×3 · goldens untouched (`git status` clean) ·
      determinism suites in the count · EditMode green · AtlasSmoke every
      lens.
- [ ] **T8 — Fresh-eyes whole-branch review** + one fix wave.
- [ ] **T9 — USER: the K taste gate** — seed 42, watch 40 epochs, click
      the Alloys War siege hex, drill to its system, open the threads
      panel. (The whole-K acceptance, not just K5's.)
- [ ] **T10 — Wrap-up**: merge via scratch worktree · HANDOFF (roadmap
      CLOSED → gap-list backlog `2026-07-11-design-acceptance.md`; keep
      the CE-chained K6 pointer) · tick K5 + "Slice K complete" ·
      diagram §7/§9 rows + zoom caption amended, republished · push on
      say-so.

## Decisions / deviations

(recorded as they happen)

## Carried flags (inherited)

- Credit-loop equilibrium → contract economy (landed in CE; verify the
  seed-42 taste-gate world reflects it).
- Unbounded keyframe memory during unattended play — note stands.
- Per-lens readability deep-dives → gap list.
- Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-K).
- Branch switch-back UI for timeline forks → backlog unless free here.
