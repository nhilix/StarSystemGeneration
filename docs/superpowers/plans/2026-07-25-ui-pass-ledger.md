# Atlas UI design pass — pass ledger

The resumability record for **all three tiers** of the atlas UI design pass.
Governing spec: `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md`.
Tier 0 kickoff: `docs/superpowers/plans/2026-07-25-ui-pass-tier0-kickoff-prompt.md`.

**This is a design pass.** No atlas code, no sim behavior, no `unity/Assets`
edits, `dotnet test` untouched. Outputs are `docs/design/ui/*` + mock artifacts.

Branch: `ui-pass-tier0` off main `cbb892d`.

---

## Tier 0 — Inventory

| # | Task | Gate | Status |
|---|---|---|---|
| 0.1 | Scope nod | user | ✅ nodded |
| 0.2 | Branch `ui-pass-tier0` | — | ✅ |
| 0.3 | Start this ledger | — | ✅ |
| 0.4 | Code sweep — **inline**, all ~7.9k lines of `unity/Assets/Atlas` + the Core lenses it leans on | — | ✅ |
| 0.5 | Generate evidence — standard grid + degenerate grid + smoke suite | — | ✅ |
| 0.6 | Read the evidence (images, not recall) — 22 shots read | — | ✅ |
| 0.7 | Write `docs/design/ui/inventory.md` | — | ✅ |
| 0.8 | Fix the group partition (incl. camera & navigation) | — | ✅ proposed: 6 groups |
| 0.9 | **User checkpoint** — completeness skim + partition nod | **user** | ✅ **NODDED** 2026-07-25 ("the partition is good") |
| 0.10 | Wrap-up: commit, HANDOFF, push, Trello | — | ✅ |
| 0.11 | Chrome capture investigation (user-directed, post-gate) | — | ✅ solved |
| 0.12 | Slice CS kickoff (user-requested) | — | ✅ |

**Tier 0 does NOT write the Tier 1 kickoff** — the orchestrator authors tier
kickoffs after each gate (user decision, 2026-07-25). Tier 0 hands back:
inventory path, nodded partition, and Tier-1 notes the inventory doesn't carry.

### 0.4 — the sweep was done inline, not by subagents

**What happened.** Seven parallel Sonnet Explore subagents were dispatched for
the sweep lanes. **None of them ever started**, and the session locked up and had
to be restarted. The branch, the ledger and all 74 evidence artifacts survived;
the sweep did not.

The sweep was then redone **inline** — `unity/Assets/Atlas` is only ~7,900 lines
across 38 files, plus the handful of `src/Core/Atlas` lenses the encodings lean
on. Reading it directly cost more tokens than a fan-out but is deterministic, and
at this size that is the right trade. **A future tier should default to inline
reading for this codebase** rather than re-risking the fan-out.

Two harness notes worth keeping:

- Passing `name:` to the Agent tool makes an agent addressable, but this harness
  also spawns a psmux pane per named agent with *nothing piped into it* — bare
  `PS>` shells that shred the window layout for no benefit. Spawn unnamed.
- The panes vanish with a session restart, so no manual cleanup was needed.

### 0.4b — what the inline sweep covered

`LodBands` · `CameraRig` · `AtlasRoot` · `SimHost` · `AtlasGeometry`/`AtlasTextures` ·
`AtlasGlyphs` · `GlyphLayerBase` · `DotMarkLayer` · `StarfieldLayer` ·
`DomainFieldLayer` · `DomainInteriorLayer`/`Marks` · `OutpostLayer` ·
`NatureFieldLayer` · `PriceFieldLayer` · `LatticeLayer` · `LaneLayer` ·
`FlowTrailLayer` · `CrawlPathLayer` · `PortLayer` · `FleetLayer` · `PoiLayer` ·
`WorksLayer` · `WarLayer` · `PlagueLayer` · `NewsLayer` · `AtlasChrome` ·
`LensRail` · `TopBar` · `LegendPanel` · `TimelineStrip` · `HexTooltip` ·
`SelectionModel` · `InspectorDock` · `DockKit` · `PanelViews` · `SystemStage` ·
`AtlasChrome.uss` · `SSGPalette-Ice.uss`, plus `AtlasPalette`, `TensionLens`,
`TechLens`, `LegendQuery` on the Core side, and both editor capture tools.

### 0.5 — evidence recipes (reproducible; output is gitignored)

Both grids were rendered against branch `ui-pass-tier0` (= main `cbb892d`
atlas code) with a **warm editor** on port 7800, per
`.claude/skills/driving-the-unity-editor/SKILL.md`.

**Standard six-seed × six-lens grid** → `atlas-grid/` (36 PNG + `index.html`):

```bash
mkdir -p runs/atlas-grid && printf 'epoch 42 40 21\nesave runs/atlas-grid/seed-42.txt\nepoch 7 40 21\nesave runs/atlas-grid/seed-7.txt\nepoch 1234 40 21\nesave runs/atlas-grid/seed-1234.txt\nepoch 9091 40 21\nesave runs/atlas-grid/seed-9091.txt\nepoch 31337 40 21\nesave runs/atlas-grid/seed-31337.txt\nepoch 2718 40 21\nesave runs/atlas-grid/seed-2718.txt\nquit\n' | dotnet run --project src/Inspector
```
```powershell
unity command atlas_grid --project-path $P --format json
```
Defaults: lenses `galaxy,domains,trade,price,war,works`, 1200×750.

**Degenerate/sparse grid** → `atlas-grid-degen/` (18 PNG + `index.html`).
Three distinct degeneracies, deliberately chosen:

| File | Config | The degeneracy it probes |
|---|---|---|
| `seed-42` | `epoch 42 2 21` | **young galaxy, full extent** — 2 polities in a radius-21 field |
| `seed-7` | `epoch 7 5 21` | **young + peaceful** — no wars, no trade, content in one corner |
| `seed-1234` | `epoch 1234 40 5` | **tiny galaxy** — mature but radius 5, ~6 ports, 1 lane |

```bash
mkdir -p runs/atlas-degen && printf 'epoch 42 2 21\nesave runs/atlas-degen/seed-42.txt\nepoch 7 5 21\nesave runs/atlas-degen/seed-7.txt\nepoch 1234 40 5\nesave runs/atlas-degen/seed-1234.txt\nquit\n' | dotnet run --project src/Inspector
```
```powershell
unity command atlas_grid --input runs/atlas-degen --output atlas-grid-degen --seeds 42,7,1234 --project-path $P --format json
```

`atlas-grid-degen/` matches the `atlas-grid*/` gitignore glob — output stays
untracked, as intended.

**Seed-42 warning, carried from the skill:** the grid's seed-42 row is
radius **21**; the committed golden `SimHost` loads is radius **12**
(`tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt`, `GCONFIG|42|12|…`).
Same seed, different galaxy. Never cite one as evidence for the other.

Also regenerated: the **smoke suite** (`unity command menu --path 'StarGen/Atlas
Smoke Shots'`), 18 shots at 1600×1000. It carries ten lenses the grid's six never
show — nature, currency, tension, tech, plague, news, POIs, fleets, traffic,
region — plus both SystemStage views. Grid + smoke together are the census.

### 0.6 — two gaps in the evidence apparatus itself

1. **No capture path renders the chrome** — `cam.Render()` bypasses the UI
   Toolkit overlay, and in EditMode the chrome's `OnEnable` builders never fire,
   so there is nothing to capture either. **SOLVED in 0.11** (user-directed
   detour); inventory §11.
2. **The acceptance suite's framing under-samples the world.** Corrected
   mid-session — an earlier draft called the golden "a near-empty world" on the
   strength of the zoomed smoke shots. Measured directly, the radius-12 golden
   at y1000 has **72 ports, 69 lanes, 15 open threads and two live wars**. The
   sparseness is `AtlasSmoke`'s framing: every lens shoots at `extent × 0.30`
   anchored on port 0 (`AtlasSmoke.cs:151-154`). The suite reads as an empty
   galaxy while photographing a busy one.

### 0.11 — chrome capture, solved (user-directed, 2026-07-25)

The user redirected after the Tier-0 gate: the chrome gap blocks the group where
most of the game's information lives (relations, chronicle, markets, polity
summaries), so it had to be addressed before Tier 1 planning.

**Proven working**, end-to-end, at `cbb892d` / editor `6000.5.2f1` / pipeline
`0.4.0-exp.1`: `set_autotick` → `editor_play` → assign
`PanelSettings.targetTexture` → camera render to a second RT → alpha-composite.
Full recipe, measurements and cautions in `docs/design/ui/inventory.md` §11.

- The spike ran entirely through **`unity command eval`** (Roslyn) — no files
  added, no assets dirtied. `eval` was previously unused in this repo and is the
  right tool for this class of question.
- **Play mode is the unlock.** Every chrome `OnEnable` fires for real, so the
  captured chrome is genuine rather than hand-mirrored — it does not inherit the
  `AtlasSmoke.SetAndStyle` drift risk.
- **Panels are drivable**: `InspectorDock.Show` and `PanelRequest` are public,
  so any of the 27 panels can be opened and shot on demand. Proven with
  Market #3 + Polity + Relations in one frame.
- **Asset-safety caution**: `PanelSettings` is committed; `targetTexture` must be
  nulled before leaving play mode. Verified clean afterwards
  (`m_TargetTexture: {fileID: 0}`).

**Not implemented as a tool** — that is `unity/Assets` work, outside a design
pass's boundary. It wants its own small slice, landing before Tier 1 reaches
groups 5 and 6. Recommended name: `AtlasChromeShots`, following the
`[MenuItem]` + `RunFromCli()` + `[CliCommand]` pattern in the
`driving-the-unity-editor` skill.

---

### 0.8 — proposed partition (awaiting the nod)

Six groups, not the spec's five. Full rationale in
`docs/design/ui/inventory.md` §10.

1. **Camera, navigation & the LOD spine** — *must run first*; every other
   group's readability questions are answered in terms of bands and fades.
2. **Map fields & lenses**
3. **Marks, billboards & the glyph vocabulary**
4. **Lanes, flows & motion** — split out of the spec's "map fields": strokes are
   a different encoding problem from rasters, and they are the only elements
   carrying time in their form.
5. **Chrome** — blocked on the chrome-capture gap (0.6).
6. **Panels & selection**

**SystemStage is folded in, not dropped** — its concerns distribute across 1, 2,
3 and 6. It works as a standalone seventh group if the user prefers, scheduled
after 1–3.

Ordering: 1 before all. 2–4 mutually independent. 6 benefits from following 5.

---

## Tier 1 — Deep dive per group

One session per group, each ending in a user eyeball. Groups fixed by the
Tier-0 partition nod (task 0.8/0.9).

**Carry into Tier 1** (not in the inventory itself):
- The chrome-capture gap must be closed before group 5 can do a real audit.
- Prefer inline reading over subagent fan-out in this codebase (see 0.4).

---

### Group 1 — camera, navigation & the LOD spine

Branch `ui-pass-t1g1` off main `99ac6c1`. Kickoff:
`docs/superpowers/plans/2026-07-25-ui-pass-t1g1-kickoff-prompt.md`.
Deliverables: `docs/design/ui/camera-nav-lod.md` + an interactive continuum
simulator artifact. **Zero atlas code**, as the pass requires.

| # | Task | Gate | Status |
|---|---|---|---|
| 1.1 | Scope nod | user | ✅ nodded 2026-07-27 ("looks good") |
| 1.2 | Branch `ui-pass-t1g1`; continue this ledger | — | ✅ |
| 1.3 | Read the spine inline — `CameraRig`, `LodBands`, `AtlasRoot`, `LatticeLayer`, `SystemStage`, both capture tools, `LodBandsTests` | — | ✅ |
| 1.4 | Build the continuum harness (`eval_file`) and shoot the zoom + pitch series | — | ✅ |
| 1.5 | Measure framing, cost and scale-invariance (queries, not shots) | — | ✅ |
| 1.6 | Write `docs/design/ui/camera-nav-lod.md` | — | ✅ |
| 1.7 | Build the interactive simulator artifact | — | ✅ |
| 1.8 | **User checkpoint** — eyeball mock + doc + live feel pass | **user** | ✅ **ACCEPTED** 2026-07-27 |
| 1.9 | Wrap-up: commit, HANDOFF, push, Trello (card xEym8e27), release the editor | — | ✅ |

#### 1.7 — the mock

**https://claude.ai/code/artifact/901f11a7-8a19-4ab4-b864-a6efba7f8b82** (🛰️)

A simulator rather than a token block, because this group's subject is a
*continuum* and static frames cannot show one. It runs the two spines' actual
curves over a deterministic 218-port mock galaxy, on the project's own
Cassette × Ice tokens. Single-theme dark deliberately — a light variant would
misrepresent a dark instrument and would break comparability with the UI
Language Lab mocks.

Verified in-browser (rendered, scrubbed, both spines, all three radii). Two
fidelity bugs found and fixed by comparing against the real captures: the mock
initially **summed** overlapping territory where the shader **unions** (a
per-polity offscreen mask + erosion now gives union fill at `_FillIntensity`
0.13 and the border at 0.50), and the lattice stopped halfway across the frame
at a fixed iteration cap.

#### 1.8 — decided in-session, not escalated (all cheap to reverse)

Per the checkpoint protocol, these were called without a gate and are listed
here rather than in the brief:

- **Glide vs cut as a two-easing grammar**, with panel `JumpTo` moving to
  glide. Defect-shaped, not taste-shaped.
- **Focus destinations expressed in `f` or hexes-across-frame**, never world
  units (the hardcoded 24 lands in different bands on different galaxies).
- **The pan leash clamps the target, not the position** — the rubber-band then
  falls out of the existing damping for free.
- **Hysteresis at ±8%**, sized against the 25% scroll notch so a deadband can
  never make a notch fail to cross a boundary.
- **Stroke widths quantize to the zoom lattice** (one rebuild per notch instead
  of 2.9, ≤±11% width error).
- **The lattice builds at load** rather than on approach.
- **The pitch floor is redefined as `fov / 2`** rather than the literal 25.
- **No yaw**, with four recorded reasons — revisitable, but recorded as a
  decision rather than an omission.

**Escalated to the gate** (expensive to reverse — Groups 2–6 inherit it): the
four-band re-derivation and the resolve steps it implies.

#### 1.8b — the gate, and what it surfaced

**Four bands ACCEPTED** (Realm / Domains / Reach / Ground; Hex deleted).
The live feel pass ran: `set_autotick` → `editor_play` → an
`EditorApplication.update` handler installed by `eval_file` that clamped
`_targetPitch` by band, leashed `_targetFocus`, bound `H` to a content-fit
glide, and drove the staged handover over the shipped 5→10 window. Verified
biting: `_targetPitch` forced to 25° at `f = 1.40` was pushed back to the 70°
Realm floor, and the camera glided from d 73 to the content fit at 553.
Nothing was written to `unity/Assets`; leaving play mode removed all of it.

**Feel: accepted.** The user's one reservation is a real design hole, now
recorded as **design §4.1**: the map→system handover is weakest at the lanes.
A port hands over because it lives *in* a hex; a lane lives *between* hexes and
the orbit view has no form for one, so the descent's last beat loses which
lanes touch this system, where they go, and whether it is a hub or a dead end.
No fade order can fix that — it needs a **system-scale terminus mark at the
system rim on the lane's true bearing**, which is Group 4's encoding to design
together with the fuller system rendering. The requirement is stated in §4.1
and §9; the encoding deliberately is not.

Harness teardown verified: play mode exited, autotick disabled,
`SimHost.ArtifactPath` restored to the golden, `Atlas.unity` byte-clean, no
`unity/Assets` diff, no `src`/`tests` diff across the branch.
`unity/ProjectSettings` churn left uncommitted per the standing rule.

#### 1.4 — the evidence apparatus (reproducible; output gitignored)

Everything below ran against branch `ui-pass-t1g1` (= main `99ac6c1` atlas
code) with a **warm editor** on port 7800, per
`.claude/skills/driving-the-unity-editor/SKILL.md`. Sim artifacts are Tier 0's
recipes **G** and **D** (inventory "The evidence base") — unchanged, regenerate
from there.

`atlas_grid` cannot shoot a continuum: its `--zoom` is one distance per run and
it never renders `SystemStage`. So Group 1 used **`unity command eval_file`** —
the Tier-0 spike vehicle — to drive `CameraRig.SetView` across a stepped
altitude list, hand-mirror `AtlasRoot.OnZoomChanged`, render the stage when
`StageFade > 0`, and emit both the PNGs and the **measured curve values** for
each step. Look and number come from the same pass, which is what makes the
strip citable.

**⚠ `eval`/`eval_file` inject the script as a METHOD BODY** — `using`
directives do not parse (`'System' is a namespace but is used like a type`).
Every type must be fully qualified. This cost a round trip; it belongs in the
skill.

Four harnesses, all in the session scratchpad (disposable; the recipes are the
record):

| Harness | What it produces |
|---|---|
| `series.cs` | a zoom or pitch strip: PNG per step + `index.html` + the five curve values per step |
| `framing.cs` | disc bounds vs **inhabited** bounds for all nine artifacts — the framing audit |
| `cost.cs` | the lattice's lazy build (verts, indices, ms) and the stroke-rebuild cadence |
| `stagecost.cs` | `SystemStage`'s visible-hex set across the crossfade at three pitches, plus rebuild ms by area |

Output directories (all match the `atlas-grid*/` gitignore glob):
`atlas-grid-zoom-r21/` (18 steps, seed-42 r21) · `atlas-grid-pitch-r21/`
(8 steps at f=0.30) · `atlas-grid-zoom-degen/` (`epoch 42 2 21`) ·
`atlas-grid-zoom-r5/` (`epoch 1234 40 5`).

#### 1.5 — what the measurements said

Full argument in the design doc; the numbers, so a later session need not
re-shoot:

- **`extent = 48 + 16.5 × radius` exactly** (r21 → 394.5, r5 → 130.5). Every
  band threshold is a fraction of this, so it is the spine's one calibration.
- **Scroll-notch budget (×1.25 per notch), r21:** Galaxy 4.4 · Domains 4.0 ·
  Region 5.2 · Hex **10.8** · System 3.1 = 27.5 total.
- **36% of the zoom range has no LOD response at all**: 4.4 notches above
  `f = 1.10` (every curve pinned) and 5.4 notches between the lattice
  completing (`f = 0.084`, d = 33) and the crossfade starting (d = 10).
- **`FitTo` frames 36–45% content on mature worlds, 10% on `epoch 7 5 21`,
  and 1.4% on `epoch 42 2 21`** (content extent 51.5 against disc extent
  394.5 — and 0.36 extents off-centre).
- **`LodBands.SystemFloor`'s `Math.Min` is a dead branch.** The relative term
  only wins below extent 59.5, i.e. radius < 0.7. `LodBandsTests.
  ATinyGalaxyKeepsItsHexBand` pins it at extent 30 — a galaxy that cannot exist.
- **Lattice**: 1615 cells × 91 hexes = 881,790 verts / 1,763,580 line indices,
  built in **30.1 ms in one frame** as the camera crosses d = 88.4 (r21).
  r5: 49,686 verts, 1.8 ms.
- **Stroke meshes rebuild ~2.9× per scroll notch** (8% width gate against a
  25% notch), for `LaneLayer` + `FlowTrailLayer` + `CrawlPathLayer`.
- **`SystemStage` rebuild ≈ 0.12 ms/hex**, whenever the visible hex *set*
  changes — 66 hexes (~8 ms) mid-crossfade at pitch 62; the `MaxVisibleHexes`
  160 cap already binds at pitch 62 / d = 13.
- **`CameraRig.Band` and `BandChanged` have zero consumers.** Grepped the whole
  of `unity/Assets`: the only reads are two `Debug.Log`s in `AtlasSmoke`
  (`PanelViews.cs:200` and `SystemStage.cs:363` are `OrbitBand`, a different
  type). The bands are a vestigial classification; all visible behaviour is the
  four continuous curves.
- **`ViewportPx` is written only inside `AtlasRoot.OnZoomChanged`**, so a window
  resize leaves all three stroke layers' screen-constant widths stale until the
  next scroll. (Billboards are safe — `CameraRig.Apply` writes
  `_AtlasViewportPx` every frame.)
- **The 25° pitch floor is exactly `FovDegrees / 2`** — an undocumented
  coupling that puts the horizon precisely at the top edge of the frame. It also
  degenerates `SystemStage.ComputeVisibleHexes` (the top frustum corners stop
  intersecting the plane), so at low pitch the stage builds only the near band.

---

### Group 2 — map fields & lenses

Branch `ui-pass-t1g2` off main `b916081`. Kickoff:
`docs/superpowers/plans/2026-07-27-ui-pass-t1g2-kickoff-prompt.md`.
Deliverables: `docs/design/ui/map-fields-lenses.md` + a colour/composite mock
artifact. **Zero atlas code.**

| # | Task | Gate | Status |
|---|---|---|---|
| 2.1 | Scope nod | user | ✅ nodded 2026-07-27 |
| 2.2 | Branch `ui-pass-t1g2`; continue this ledger | — | ✅ |
| 2.3 | Read the field layers + Core lenses inline | — | ✅ |
| 2.4 | Measure (`counts.cs`, `fold.cs`) and capture (`fields.cs`, `sweep.cs`) | — | ✅ |
| 2.5 | Read the evidence — images, not recall | — | ✅ |
| 2.6 | Write `docs/design/ui/map-fields-lenses.md` | — | ✅ |
| 2.7 | Build the mock artifact | — | ✅ |
| 2.8 | **User checkpoint** — the four escalated decisions | **user** | ⬜ |
| 2.9 | Wrap-up: commit, HANDOFF, merge, push, Trello, release the editor | — | ⬜ |

#### 2.4 — the evidence apparatus (reproducible; output gitignored)

Sim artifacts are Tier 0's recipes **G** and **D** (inventory "The evidence
base") — unchanged, already on disk at `runs/atlas-grid/` and
`runs/atlas-degen/`; regenerate from there. Everything below ran against
branch `ui-pass-t1g2` (= main `b916081` atlas code) with a **warm editor** on
port 7800, through `unity command eval_file` — no files added to `unity/`, no
assets dirtied. Four harnesses, all in the session scratchpad (disposable; the
recipes and the numbers below are the record):

| Harness | What it produces |
|---|---|
| `counts.cs` | per-artifact census: polity slots, accent ΔE spreads at the shipped fill, spatially-adjacent owner-hue ΔE, price-band histogram, star count |
| `fold.cs` | rasterizes the shader's own union field twice (32-slot fold vs unlimited) at 512² over the disc and counts the samples whose identity differs |
| `fields.cs` | 66 shots: accent sweep, the compositing stack added a layer at a time, the fold flagged in magenta, degenerate lenses |
| `sweep.cs` | 17 shots: nature/price at Realm framing, and a fill×border sweep over three accents — plus the ΔE-vs-fill curve |
| `export.cs` | seed-42's field as JSON (ports, slots, stars, gas raster) — the mock's data source |

Output: `atlas-grid-fields/` (83 PNG + `index.html`) — matches the
`atlas-grid*/` gitignore glob, so it stays untracked.

**ΔE is CIE76 over sRGB→Lab**, computed on the *rendered* 8-bit fill
(`slotColour × _FillIntensity`), which is where the collapse happens — not on
the ramp endpoints. Census numbers are at the artifact year (y1000); the
`sweep.cs` numbers are one epoch later (y+25), because the capture path steps
once before shooting (`AtlasSmoke`/`AtlasGrid` convention). Cite the two
separately: seed-42 tension spread is ΔE 9.40 at y1000 and 6.68 at y1025.

#### 2.5 — what the measurements said

The argument is in the design doc; the numbers, so a later session need not
re-measure:

- **The 32-slot cap is crossed on every mature world.** Distinct port-owner
  polities: 63 · 46 · 45 · 50 · 56 · 56 across the six radius-21 seeds against
  `MaxSlots = 32`. The code comment claiming "seed-scale galaxies stay well
  under 32" (`DomainFieldLayer.cs:156`) is false for every seed in the grid.
- **21.8–39.9% of all drawn territory is misattributed** — rasterized, not
  inferred: 5,973 of 27,339 covered samples on seed-42, 10,858 of 27,233 on
  seed-1234. 44–80 of 175–215 ports fold. Territory covers only 8.6–10.5% of
  the disc, so the fold is ~2–4% of the *frame* but ~1/3 of everything the
  lens draws.
- **Tech is dead at any intensity.** Real Astrogation tiers span **2–3** on
  every mature seed against `TechLens.RampCap = 6`, so the lens renders **two**
  distinct fills: ΔE **1.56** at the shipped fill 0.13, and still only ΔE
  **10.24** at fill 1.0. Intensity cannot fix a ramp the data doesn't span.
- **Tension is rescued by intensity alone.** Spread ΔE 6.68 → 21.41 → 30.23 →
  38.63 at fills 0.13 / 0.30 / 0.45 / 0.60. Tension-vs-tech mean separation
  goes 2.74 → 8.18 → 11.52 → 14.43 over the same range: at the shipped fill the
  two lenses genuinely *are* one image.
- **Owner hue is collision-resistant, not separated.** Across 50–63 slots the
  closest pair is ΔE 0.28–0.40 at the rendered fill. Restricted to
  **spatially adjacent** polities (service circles that overlap), the worst
  pair is ΔE 4.0–10.1 at full colour, with 0–5 pairs under ΔE 10 and 3–9 under
  ΔE 20 per seed.
- **The adjacency graph is sparse, which is what makes an allocated palette
  work.** Seed-42: 50 polities, **58** adjacent pairs, maximum degree **7**,
  two-hop neighbourhood **13**. Greedy over adjacency alone needs three colours;
  greedy with the two-hop set as a soft constraint uses all 16 with zero
  collisions at either distance. Both numbers computed twice — in the mock and
  independently offline — and they agree.
- **The price field's data lands in its loudest bands.** Provisions band
  histogram over serviced cells, seed-42: famine 86 · glut 63 · par 39 ·
  cheap 25 · dear 18 · spike 17 · scarce 10. **Famine is the largest band on
  all six mature seeds** (73–139 cells) and par is 4–15%. Famine draws at
  alpha 240 hot pink, glut at 190 deep blue; par — the quiet one — is where
  almost nothing lands.
- **Price is not a raster, it is a Voronoi.** `PriceLens.RatioAt` returns the
  *nearest servicing port's* market price, so every hex in one port's service
  area carries one identical value, quantized to raster cells on the way out.
  At `f = 0.10` the field is a single flat blue plane across 70% of the frame.
- **Only 13–17% of cells are serviced** (209–268 of 1615) and 40% are void.
- **The starfield is 31,000 additive billboards on every radius-21 world**,
  independent of content: on `epoch 42 2 21` that is 15,483 stars per port.
- **`LensStack.Composite` — the Core-side "compositing rule the tests pin" —
  has no caller in the atlas.** Grepped: only its own xUnit test. All real
  compositing is GPU z-order plus per-material blend mode, stated nowhere.
- **Draw order is not what the kickoff assumed.** Camera sits at −z
  (`CameraRig.Apply`: `position = focus − forward × distance` with
  `rotation = Euler(pitch−90,0,0)`), so **larger +z is farther**. Back to
  front: nature `+0.10` → domain field `+0.05` → price `+0.02` → **starfield
  `0.00`** → lattice `−0.02` → crawls/trails/lanes → marks. The starfield draws
  *over* all three field rasters, additively; the nature field — the best
  image the atlas makes — is behind everything.
- **The war legend drifts from the war layer, unguarded.** `LegendQuery`
  advertises `DomainLens.WarShade` (225,70,60) for a belligerent domain and
  `AtlasPalette.Floor` (24,26,32) for a peaceful one; `DomainFieldLayer`
  draws `AtlasPalette.OwnerColor(slot)` and `(58,62,72)`. `LegendDriftTests`
  checks glyph-key names and non-emptiness only — never colour parity.
- **Nature reads at Realm, not at Reach.** `sweep-nature-fit-gas.png` is a
  full nebular spiral; the same layer at `extent × 0.30`
  (`seed-42-stack-1-nature.png`) is a flat blue-grey wash. This contradicts
  `camera-nav-lod.md` §2 ("price / nature rasters: off · off · on · on") and
  is the one amendment this group asks for.
- **Currency is the owner lens with different hues.** Distinct currencies
  equals distinct port-owner polities on all nine artifacts (63/63, 46/46,
  45/45, 50/50, 56/56, 56/56, 2/2, 2/2, 5/5) — zero consolidations, so the
  lens's whole subject is absent and nothing says so.

#### 2.7 — the mock

**https://claude.ai/code/artifact/1f18c59c-e86f-4445-8d18-c8dfd4a47221** (🎨)

Not a token block: seed-42's **real field** — 214 ports, their service radii,
the 50 slots' tension heats, tech tiers, currency ids and belligerence, 2,581
of 30,966 stars and the gas nature raster as a PNG — exported from the warm
editor (`export.cs`) and re-rendered in the browser, so every "shipped vs
proposed" pair is the same world under two encodings rather than an
illustration. Built on the project's Cassette × Ice tokens, single-theme dark
for the same reason Group 1's simulator is.

The **greedy allocator runs in the page**, and its readout matches an
independent Python computation over the same export: 50 polities, 58 adjacent
pairs, busiest domain touching 7 and seeing 13 within two hops → **16 hues used,
zero collisions at one hop and zero at two**. Adjacency alone finishes in
**three** colours, which is why the two-hop soft constraint exists.

**Fidelity notes** (the technique, and where it stops being the shader):
territory unions are per-slot masks with an erosion border — G1's simulator
technique — so overlaps read as summed light rather than the shader's relation
shading. The shipped price panel is the real nearest-servicing-port algorithm at
reduced resolution, which is what reproduces the hex blocks.

**Verification.** Rendering and layout verified in-browser; the allocator
verified against the offline computation; every canvas configuration verified to
draw via a compact all-canvases build (published separately, disposable).
**The interactive controls were not exercised** — this harness cannot deliver
scroll or click into the artifact's sandboxed iframe. That is the one thing the
eyeball gate should poke at first.

**Harness lesson worth keeping:** an artifact that does heavy canvas work at
load reads as a *blank page* — the viewer shows nothing and CDP screenshots time
out with "the renderer may be frozen". The first build rendered nine canvases
eagerly and looked broken. Render the visible one on `requestAnimationFrame` and
the rest behind an `IntersectionObserver`; bbox-sized scratch canvases instead
of frame-sized masks cut the work by ~50×.

#### 2.8 — decided in-session, not escalated (all cheap to reverse)

Listed here rather than in the gate brief, per the checkpoint protocol:
starfield attenuation shape · nature promoted to a Realm read (the one G1
amendment, flagged) · the lattice's role and alpha · nature chip swatches
carrying their layer's base hue · the war legend's colour drift fixed at the
legend · outposts/worked dust keeping their encoding · `LensStack.Composite`
retired in favour of a stated GPU stacking rule.

## Tier 2 — Synthesis

Not started. Icon manifest · token conformance · interaction grammar ·
ranked implementation kickoffs.
