# Atlas UI inventory

**What this document is.** A census of every UI element in the Unity atlas as
it exists on `main` at `cbb892d`, recorded **as evidence, never as constraint**.
It is the Tier-0 output of the atlas UI design pass
(`docs/superpowers/specs/2026-07-24-ui-design-pass-design.md`). The accepted
per-group designs land beside it as Tier 1 completes; until then this is the
only doc in `docs/design/ui/`, and it describes *what is*, not *what should be*.

Nothing here is a defence of the status quo. Where an encoding costs something,
the cost is recorded next to it.

Every claim cites a `path:line` or a named shot. **Shots are gitignored and
disposable** — each is cited with the recipe that regenerates it (below), never
by filename alone.

---

## The evidence base

Three capture paths exist. All were regenerated against `ui-pass-tier0`
(= `cbb892d` atlas code) with a warm editor, per
`.claude/skills/driving-the-unity-editor/SKILL.md`.

| Set | Contents | Regenerate |
|---|---|---|
| `atlas-grid/` | 6 seeds × 6 lenses, 1200×750, radius 21 | recipe **G** |
| `atlas-grid-degen/` | 3 degenerate galaxies × 6 lenses | recipe **D** |
| `atlas-smoke*.png` | 18 shots, 1600×1000, the radius-**12** seed-42 golden | recipe **S** |

**Recipe G** — the standard eyeball grid. Sim artifacts first, then render:
```bash
mkdir -p runs/atlas-grid && printf 'epoch 42 40 21\nesave runs/atlas-grid/seed-42.txt\nepoch 7 40 21\nesave runs/atlas-grid/seed-7.txt\nepoch 1234 40 21\nesave runs/atlas-grid/seed-1234.txt\nepoch 9091 40 21\nesave runs/atlas-grid/seed-9091.txt\nepoch 31337 40 21\nesave runs/atlas-grid/seed-31337.txt\nepoch 2718 40 21\nesave runs/atlas-grid/seed-2718.txt\nquit\n' | dotnet run --project src/Inspector
```
```powershell
unity command atlas_grid --project-path $P --format json
```
Defaults: lenses `galaxy,domains,trade,price,war,works`; `galaxy` is fit-to-bounds,
the other five are mid-zoom at `extent × 0.30`, pitch 62°, anchor centroid
(`unity/Assets/Editor/AtlasGrid.cs:73`, `:315`).

**Recipe D** — the degenerate grid. Three distinct degeneracies:

| Row | Config | Degeneracy probed |
|---|---|---|
| `seed-42` | `epoch 42 2 21` | young galaxy at full extent — 2 polities in a radius-21 field |
| `seed-7` | `epoch 7 5 21` | young **and** peaceful — no wars, no trade |
| `seed-1234` | `epoch 1234 40 5` | tiny — mature but radius 5, ~6 ports, 1 lane |

```bash
mkdir -p runs/atlas-degen && printf 'epoch 42 2 21\nesave runs/atlas-degen/seed-42.txt\nepoch 7 5 21\nesave runs/atlas-degen/seed-7.txt\nepoch 1234 40 5\nesave runs/atlas-degen/seed-1234.txt\nquit\n' | dotnet run --project src/Inspector
```
```powershell
unity command atlas_grid --input runs/atlas-degen --output atlas-grid-degen --seeds 42,7,1234 --project-path $P --format json
```

**Recipe S** — the committed acceptance suite:
```powershell
unity command menu --path 'StarGen/Atlas Smoke Shots' --project-path $P --format json
```
(Poll for the PNGs; `menu` reports a 30s timeout while the editor finishes fine.)

**Seed 42 is two different galaxies.** The grid's seed-42 row is radius **21**;
`SimHost` loads the radius-**12** golden by default
(`unity/Assets/Atlas/SimHost.cs:23`, `GCONFIG|42|12|…`), which is what every
`atlas-smoke` shot renders. Never cite one as evidence for the other.

### Two gaps in the apparatus — one solved, one open

**1. The existing capture paths render no chrome — SOLVED, see §11.** Both
`AtlasSmoke` and `AtlasGrid` capture with `cam.Render()` into a RenderTexture
(`unity/Assets/Editor/AtlasSmoke.cs:283`), which bypasses the UI Toolkit overlay
panel entirely. TopBar, LensRail, LegendPanel, TimelineStrip, HexTooltip and
every InspectorDock panel appear in **zero of the 72 map shots**, so all their
entries below are code-cited. A working chrome-inclusive capture path was found
and proven during Tier 0 — §11 has the recipe and the proof shot.

**2. The acceptance suite's framing under-samples the world.** *(Restating a
Tier-0 error: an earlier draft called the golden "a near-empty world". It is
not.)* The radius-12 golden at y1000 carries **72 ports, 69 lanes and 15 open
threads**, including two live wars — measured directly, not inferred. What is
sparse is the **framing**: `AtlasSmoke` shoots every lens at `extent × 0.30`
anchored on port 0 (`AtlasSmoke.cs:151-154`), a tight neighbourhood of a
72-port galaxy. So `atlas-smoke-fleets` showing one fleet and
`atlas-smoke-plague` showing none are facts about that neighbourhood, not about
the world. The suite reads as an empty galaxy while photographing a busy one —
which is its own finding, and a sharper one than the error it replaces.

---

## 1. Camera & navigation

`CameraRig` — `unity/Assets/Atlas/CameraRig.cs:14`.

- **Projection.** Perspective, FOV 50°, near 0.3, far 3000 (`:25`, `:47-48`).
  Described as focus-point-on-plane + distance + pitch.
- **Zoom continuum.** Distance, clamped `[2.5, fit × 1.3]` (`:58-59`) where
  `fit = extent / tan(25°) × 1.05` (`:56-57`). Scroll dollies by `1.25^±1` per
  notch **toward the cursor's plane intersection**, so the point under the
  cursor stays put (`:114-126`).
- **Pitch.** Clamped 25°–90° (`:70`); 90° is pure top-down. Middle-drag tilts at
  0.2°/px (`:146-150`). Fit sets 65° (`:60`).
- **Pan.** Right-drag holds the grabbed world point under the cursor
  (`:128-140`); WASD pans at `0.9 × distance` per second (`:153-166`).
- **Damping.** Exponential toward targets, half-life 0.09 s (`:26`, `:81`).
  `SetView` is a **jump cut** — no easing — and is what tooling and panel
  `JumpTo` use (`:65-74`, `InspectorDock.cs:169`).
- **Chrome owns the pointer.** `AtlasPointerGuard.Blocks` suppresses scroll and
  drag over chrome; keyboard pan stays live (`:105-110`).

**Known debts.**
- **No yaw.** There is no rotation binding at all — only pitch. The map has a
  permanent fixed north, and no code path can turn it.
- **Pan is unbounded.** `_targetFocus` is never clamped (`:124`, `:134`, `:164`);
  only distance is. The camera can be panned arbitrarily far off the galaxy into
  empty space with no rubber-band and no way back but manual re-pan.
- **Framing never fits content.** `FitTo` frames the **disc bounds** — every
  cell in the model padded by 48 world units (`AtlasGeometry.cs:23-41`) — not the
  inhabited region. On `epoch 7 5 21` (recipe D) all content sits in the
  top-left corner with ~90% of the frame empty starfield.
- **`JumpTo` hardcodes distance 24** regardless of galaxy scale or what was
  jumped to (`InspectorDock.cs:169`).
- No focus-on-selection, no "frame all", no zoom-to-fit-selection.

---

## 2. The LOD spine

`LodBands` — `unity/Assets/Atlas/LodBands.cs:12`. Pure math, EditMode-tested
(`unity/Assets/Atlas/Tests/LodBandsTests.cs`). **This is the spine the whole
atlas hangs from**: what *resolves* is banded, how things *scale* is continuous.

**Bands** (`LodBand`, `:7`), keyed on `distance / galaxyExtent`:

| Band | Enters at | Note |
|---|---|---|
| Galaxy | `f ≥ 1.10` | `GalaxyFloor` (`:17`) |
| Domains | `f ≥ 0.45` | `DomainsFloor` (`:18`) |
| Region | `f ≥ 0.14` | `RegionFloor` (`:19`) |
| Hex | below Region, above the System floor | |
| System | `distance < min(5.0, 0.14 × extent × 0.6)` | **absolute**, not relative — one hex is a fixed √3 world units (`:21-29`) |

**Fade curves** — all multiply in `MapFade`, so the whole map dissolves together
into the orbit view:

| Curve | Range | Window |
|---|---|---|
| `MapFade` (`:46`) | 1 → 0 | `floor … 2×floor` |
| `StageFade` (`:57`) | `1 − MapFade` | the orbit stage's master alpha |
| `LaneFade` (`:61`) | 0.40 → 1 | `f` from 1.10 → 0.45 |
| `GlyphFade` (`:73`) | 0 → 1 | `f` from 0.63 → 0.315 |
| `LatticeAlpha` (`:87`) | 0 → **0.12 max** | `f` from 0.224 → 0.084 |

**Band × layer matrix.** Which layers carry which curve
(wiring: `AtlasRoot.OnZoomChanged`, `AtlasRoot.cs:159-190`):

| Layer | Curve | Effect across the continuum |
|---|---|---|
| Starfield | **none** | full strength at every distance, including inside the orbit view (deliberate — `AtlasRoot.cs:181-182`) |
| DomainField, NatureField, PriceField | `MapFade` | dissolve into the stage |
| PortLayer, DomainInterior, OutpostLayer | `MapFade` | dissolve into the stage |
| NewsLayer | `MapFade` | additive, so the fade scales emitted light (`NewsLayer.cs:69-70`) |
| LaneLayer, FlowTrail, CrawlPath | `LaneFade` | ghosted at altitude, full by Domains |
| Fleet, POI, Works, Plague, War glyphs | `GlyphFade` | invisible above `f=0.63`, resolved by `f=0.315` |
| LatticeLayer | `LatticeAlpha` | invisible above Region, built lazily on first approach |
| SystemStage | `StageFade` | fades up as everything else dies |
| Selection highlight | **none** | never LOD-fades, by design (`SelectionModel.cs:307`) |

**Known debts.**
- **No hysteresis.** `BandFor` is a bare threshold function (`:31-39`). Nothing
  damps a camera resting exactly on a boundary. The continuous fades hide this
  for *styling*, but anything gated on `Band` itself will chatter.
- Band thresholds are also not surfaced anywhere in the UI — the player has no
  indication that crossing a threshold is what changed the map.

---

## 3. Map fields

All fields are plane quads over `AtlasGeometry.DiscBounds` at small **positive**
z (away from camera); marks sit at negative z.

### 3.1 Starfield
`StarfieldLayer.cs:15` · source `StarfieldLens.Stars(model)` (`:57`).

- **Renders** additive soft-dot billboards, one per star in the density raster.
- **Encoding.** World size `0.22 + 0.55·brightness`; pixel floor
  `0.8 + 1.8·brightness`; alpha `(28 + 210·brightness)/255` (`:71-80`).
  `_MaxPx` 5 (`:32`). Tint by `StellarLean`: Balanced `(200,214,240)`,
  YoungBright `(160,195,255)`, OldDim `(255,205,160)`, RemnantGraveyard
  `(215,165,235)` (`:20-23`).
- **LOD.** None — no `OnZoom` method exists on this layer.
- **Debt — the decorative layer buries the informational one.** Star brightness
  is absolute, never relative to how much content sits on top of it. On
  `epoch 42 2 21` (recipe D, galaxy lens) the two real ports are all but
  invisible inside a dense bright star disc. The sparser the galaxy, the more
  completely the decoration wins.

### 3.2 Domain field — and the four accents
`DomainFieldLayer.cs:23` · shader `StarGen/DomainField` · source
`DomainLens.PolitySlots` + `PortLens.Markers` (`:140`, `:147`).

This is the atlas's densest single element. It is a **per-pixel shader over a
port registry**, not a drawn polygon: union fills, border outlines and Venn
overlaps are all shader emergents.

- **Uploads.** Up to 512 ports as `(x, y, serviceRadius × 1.05, slot)` (`:25`,
  `:30`, `:153-159`); up to 32 polity slot colors; a 32×32 pairwise
  `OverlapShade` lookup texture (`:170-198`).
- **Intensities.** Fill **0.13**, overlap 0.26, border **0.50**, border width
  1.6 px (`:44-47`).
- **The "port glows" belong to this layer**, not `PortLayer` — they are the
  service-radius fills. `PortLayer` draws only the dot.
- **Accents** (`DomainAccent`, `:14`) — one fill mode at a time:

| Accent | Fill source | Palette |
|---|---|---|
| Owner | `AtlasPalette.OwnerColor(slot)` | golden-ratio hue, S 0.72 V 0.78 (`AtlasPalette.cs:39-43`) |
| War | belligerents keep owner hue; peace → ash `(58,62,72)` | `:115-117` |
| Tension | `TensionLens.HeatColor` | Cold `(95,105,130)` → Ember `(240,130,50)` (`TensionLens.cs:14-15`) |
| Tech | `TechLens.TierColor` | Low `(120,95,70)` → High `(170,215,255)`, saturating at tier 6 (`TechLens.cs:13-16`) |
| Currency | `CurrencyLens.CurrencyColor` ?? `AtlasPalette.Floor` — a retired currency visibly disappears (`:123-125`) | |

- **Debt — tension and tech are indistinguishable at the shipped fill intensity.**
  In `atlas-smoke-tension.png` and `atlas-smoke-tech.png` (recipe S) the two
  images are visually identical grey. The cause is arithmetic: at heat 0 tension
  is `(95,105,130)` and at tier 0 tech is `(120,95,70)`; at `_FillIntensity 0.13`
  those become ≈`(12,14,17)` and ≈`(16,12,9)` — a couple of 8-bit steps apart,
  over near-black. Both ramps have ample range on paper; the fill intensity
  throws it away at the low end, which is exactly where a young or small galaxy
  lives. `atlas-smoke-currency.png` is the same image in red.
- **Debt — polities past 32 silently collapse.** Any polity beyond `MaxSlots`
  folds into the *last* slot and inherits its color (`:155-158`). No warning
  surfaces; the map simply lies about ownership.
- **Debt — owner hues are collision-resistant, not perceptually separated.**
  Golden-ratio hue at fixed S/V gives stable ids but no guarantee that two
  *adjacent* domains differ visibly, and no colorblind safety. In
  `atlas-grid/seed-42-domains.png` (recipe G) neighbouring greens are hard to
  separate; red/green adjacency is everywhere.

### 3.3 Domain interior — worked dust & outposts
`DomainInteriorLayer.cs:11` (z −0.11) and `OutpostLayer.cs:11` (z −0.13), both
on the shared `DotMarkLayer` billboard base (`DotMarkLayer.cs:38`). Source
`DomainInteriorMarks.Build` over `DomainInteriorQuery` (`DomainInteriorMarks.cs:96`).

- **Worked hexes.** Owner-tinted dot, world `0.14 × HexStep`, 4.5 px floor,
  alpha 0.55 — deliberately subordinate to the port keystone
  (`DomainInteriorLayer.cs:27-28`).
- **Outposts.** Owner hue lifted a quarter toward white, world `0.22 × HexStep`,
  5.5 px, alpha 0.9 (`OutpostLayer.cs:25-30`). Always on. Named on hover,
  selectable.
- Every inhabited hex carries **exactly one** mark: an outpost hex is never also
  drawn as worked dust, and a graduated outpost is a port
  (`DomainInteriorMarks.cs:115-128`).
- **LOD.** `MapFade` only — these do not carry `GlyphFade`, so they are live at
  galaxy altitude where they are sub-pixel.
- Worked dust rides the **domains** chip; outposts have no chip at all
  (`LensRail.cs:246-247`).

### 3.4 Nature fields
`NatureFieldLayer.cs:13` · source `NatureFieldSampler` (`:91`).

- 320² data texture, bilinear, `Sprites/Default`, z 0.10, field alpha 150
  (`:15-17`). The sampler does Gaussian cross-cell blending, presence-scaled
  alpha, void feathering and cloud noise.
- **Off by default**; one layer at a time via `Select(NatureLayer?)` (`:74`).
- `atlas-smoke-nature.png` (recipe S) is the best-looking shot in the whole
  suite — a genuine nebular spiral, not a hex board.
- **Debt.** All nature chips share one rail swatch color `0x5A6E9E`
  (`LensRail.cs:161-168`), so the rail cannot say which nature layer is which.
  The legend is equally generic — "low/high, the raster's floor/peak" — for every
  nature layer (`LegendQuery.cs`, default branch).

### 3.5 Price field
`PriceFieldLayer.cs:13` · source `PriceLens.CellShades(model, eye, good)` (`:91`).

- 256² texture over the whole disc, **bilinear**, `Sprites/Default`, z 0.02
  (`:15-16`, `:29`). Default good Provisions (`:24`); the rail's dropdown
  re-bakes on change (`:82-87`).
- Sampling is **nearest-cell**: each texel looks up `HexGrid.CellOf(WorldToHex(…))`
  and takes that cell's shade; unserviced cells write transparent (`:111-115`).
- **Debt — this is the loudest element in the atlas.** In
  `atlas-grid/seed-42-price.png` and `seed-7-price.png` (recipe G) the field is
  hard-edged, fully-saturated pink/blue/orange/teal blocks covering ~40% of the
  frame at full opacity, obliterating domains, lanes and marks beneath. Nearest-cell
  quantisation plus bilinear smear produces the blocky-with-soft-shoulder
  artefact visible at every zoom.
- **Debt — the encoding reads categorical for a scalar question.** "Where is
  this good dear?" is a magnitude, but the rendered palette reads as unordered
  identity colors. `atlas-grid-degen/seed-42-price.png` (recipe D) shows two
  polities landing on flat teal and flat blue with no sense of which is dearer.
- **Debt — two geometry idioms for one truth.** The price field draws the
  polity's claimed cells as hard hex blocks while the domain field draws the
  same claim as a soft rounded blob. They disagree visibly wherever both are on.

### 3.6 Lattice
`LatticeLayer.cs:13`.

- Every hex of every cell as GPU lines, colour `(140,160,200)`, alpha capped at
  **0.12** by `LatticeAlpha` (`:16-17`, `LodBands.cs:94`). z −0.02. Built lazily
  on first approach and cached (`:65`, `:72`).
- **Debt.** `Build` walks `HexGrid.Spiral(center, CellRadius)` for *every* cell in
  the disc (`:79-98`) — the full-galaxy lattice mesh, built in one frame the
  first time the camera descends past `f = 0.224`. At Region zoom in
  `atlas-smoke-region.png` (recipe S) the result reads as dense texture noise
  across the whole frame rather than a locating grid.

---

## 4. Lanes, flows and crawls

### 4.1 Lanes
`LaneLayer.cs:23`. Screen-constant quads on the plane, base width 1.4 px, z −0.05
(`:25-26`). Rebuilds only when width drifts >8% (`:113`).

**Five modes** (`LaneMode`, `:16`) — the rail exposes four:

| Mode | Source | Width factor |
|---|---|---|
| Status | `LaneLens.Segments` | 1 |
| Traffic | `TrafficLens.Segments` | `0.45 + 2.55 × weight` (`:141`) |
| Trade | `TradeLens.Segments`, matched **by LaneId** | `0.45 + 2.55 × weight`; unread lanes draw idle margin-gold at 0.45 (`:155-184`) |
| QuarantineOnly | `LaneLens.Segments` filtered to Quarantined | 1.8 (`:192`) |
| War | `WarLens.ContestedLanes` | 1.6 (`:129-133`) |

- Trade reads well: in `atlas-grid/seed-42-trade.png` (recipe G) the cream flow
  strokes are the clearest single encoding in the grid.
- **Debt — trade encodes volume in width only, one colour for all.** There is no
  direction, no commodity, no origin/destination read on the stroke itself.
- **Debt — QuarantineOnly hides the network with no explanation.** In
  `atlas-smoke-plague.png` (recipe S) the only lane simply vanishes; the player
  cannot distinguish "no quarantines" from "lanes turned off".

### 4.2 Flow trails and crawl paths
`FlowTrailLayer.cs:19` (z −0.03, 1.0 px, solid — a *memory* of what moved) and
`CrawlPathLayer.cs:19` (z −0.028, 1.0 px, **dashed** — a live off-lane crossing).
Sources `RecentFlowQuery.Trails` and `WorksLens.CrawlPaths`.

- The two never double-draw the same shipment: an in-flight shipment's trail is
  suppressed so the crawl owns it (`FlowTrailLayer.cs:68-72`).
- Both ride the **works** chip — no rail key of their own
  (`LensRail.cs:264-271`).
- **Debt.** Trails come from the TimeMachine's per-keyframe in-memory capture, so
  a freshly loaded artifact has **none** until a step runs (`FlowTrailLayer.cs:14-17`).
  This is correct behaviour that reads as a broken lens: open the atlas, turn on
  works, see nothing.

---

## 5. Marks & billboards

### 5.1 The shared machinery
`GlyphLayerBase` — `unity/Assets/Atlas/GlyphLayer.cs:36`. One `StarGen/AtlasGlyph`
material over the authored atlas, one billboard mesh, `GlyphFade`, `_MaxPx` 56
(`:57`). Subclasses only translate lens data into `GlyphInstance`s.

- **Dual sizing** everywhere: a world size that resolves as the camera descends
  plus a **pixel floor** that keeps the mark visible at altitude
  (`GlyphLayer.cs:12-28`).
- **The contrast chip.** Every glyph is drawn over a dark backing disc
  `(9,11,17,195)` at 1.45× scale, as the first quad of the pair
  (`:106-108`, `:128-129`) — the fix for an owner-tinted glyph sitting on an
  owner-tinted port dot.
- Queue bias past the base layers so port dots don't cover same-hex glyphs
  (`:46-48`); War biases 120, Plague 110 (`WarLayer.cs:15`, `PlagueLayer.cs:14`).

### 5.2 The glyph vocabulary
`AtlasGlyphs.cs:10` — 16 authored icons from **game-icons.net (CC BY 3.0)**,
credited in `GLYPH-CREDITS.md`, laid out row-major 4×5 in
`Resources/AtlasGlyphs.png`. **Order is the atlas layout; append, never reorder**
(`:9`).

| Cell | Glyph | Meaning | Drawn by |
|---|---|---|---|
| 0–5 | FleetPosted · Escort · Patrol · Blockade · Expedition · Reserve | fleet posture | `FleetLayer`, `WarLayer` |
| 6–10 | PoiBattlefield · Ruins · RuinedCapital · Memorial · Precursor | POI type | `PoiLayer` |
| 11–13 | WorkSite · WorkFreight · WorkConvoy | construction / freight / convoy | `WorksLayer` |
| 14–15 | PlagueInfected · PlagueImmune | port plague status | `PlagueLayer` |
| 16 | Backing | the contrast chip — generated, not sourced | every glyph layer |

**Generated (deliberately not identity glyphs)** — `AtlasTextures`,
`AtlasGeometry.cs:49`: `SolidDot` (64², AA rim), `SoftDot` (radial smoothstep),
`Ring` (band at 0.86 diameter, ~0.10 wide), `ThinRing` (~4% band at 0.90),
`SquareRing` (Chebyshev band at 0.80). The stated boundary: *"a disc is a
point-spread function, not an identity"* (`AtlasGlyphs.cs:28-32`).

### 5.3 The mark layers

| Layer | z | Source | Size (world / px) | Tint |
|---|---|---|---|---|
| `PortLayer.cs:13` | −0.15 | `PortLens.Markers` | `(0.35+0.25·tier)·HexStep` / `(2+1.4·tier)·2` (`:75-76`) | owner hue, alpha 1 |
| `FleetLayer.cs:10` | −0.20 | `FleetLens.Markers` | `0.85·HexStep` / `13 + min(7, hulls·0.5)` (`:20-21`) | owner |
| `PoiLayer.cs:10` | −0.18 | `PoiLens.Marks` | `0.8·HexStep` / `12 + min(8, magnitude·0.25)` (`:20-21`) | type color, **+40 per channel when dormant** (`:22-27`) |
| `WorksLayer.cs:11` | −0.22 | `WorksLens.Sites/Freight/Convoys` | sites `0.9·HexStep`/15; freight `0.65`/11–15; convoys `0.8`/13 (`:33-52`) | site cools amber→ember by `FedFraction` (`:27-32`); freight by purpose, war convoys largest |
| `WarLayer.cs:12` | −0.24 | `WarLens.Stations` | `0.9·HexStep` / 16 blockade, 14 else (`:22-25`) | burns hot regardless of owner |
| `PlagueLayer.cs:11` | −0.25 | `PlagueLens.Marks` | `0.85·HexStep` / 16 infected, 13 immune (`:21-25`) | status color |

**Known debts, marks as a family.**
- **No declutter of any kind.** No collision avoidance, no importance culling,
  no top-N. Marks pile up wherever the sim puts them. `PoiLens` in
  `atlas-smoke-pois.png` (recipe S) already scatters a dozen glyphs at 12–20 px
  across two small domains; at radius-21 density this is unbounded.
- **Glyph type is unreadable at working zoom.** In `atlas-grid/seed-42-works.png`
  (recipe G) the works glyphs read as identical orange smudges — the shape
  channel is spent, but nothing of it survives to the eye at the grid's framing.
  Every distinction those 16 authored icons encode is lost above the Region band.
- **Size is doing double duty and neither read is calibrated.** Fleet size
  encodes hulls, POI size encodes magnitude, works size encodes purpose+stall,
  war size encodes posture — four different meanings for one channel, with no
  legend entry stating any of the scales.

### 5.4 News pulses
`NewsLayer.cs:14` · source `NewsLens.Pulses`.

- Expanding additive ring fronts from each pulse origin. Radius
  `min(10, 1 + 1.15·√ageYears)` hexes (`:19-20`, `:87-88`); alpha
  `0.35 × (1 − age/40) × clamp(0.35 + 0.65·magnitude)` (`:93-95`). z −0.12,
  `_MaxPx` 4096 — spatial, never pixel-capped (`:39`).
- **Display cutoff at 40 years** even though Core keeps pulses live to 150 —
  "a century-old ring is history, not news". The first smoke drowned in 597
  lifetime rings (`:21-25`). A recorded, deliberate compromise.
- In `atlas-smoke-news.png` (recipe S) a single pulse reads as a heavy olive
  band across the domain; the "story is where rings cluster" intent (`:91-92`)
  is untestable on a one-pulse world.

---

## 6. Chrome

**All chrome is code-built onto hosts owned by `AtlasChrome`** — there are no
UXML files for the atlas. One stylesheet: `Resources/AtlasChrome.uss` (604 lines).

`AtlasChrome.cs:15` builds six named hosts in paint order — rail, dock, timeline,
top bar, legend, tooltip (`:96-101`) — and owns the single `AtlasPointerGuard`
test for all chrome (`:36`, `:116-123`). The document root is
`PickingMode.Ignore` or the guard would report chrome everywhere (`:56`).
Scrollbars are hidden everywhere by policy (`:105-111`).

**Token conformance is excellent** — and this corrects the obvious guess.
`AtlasChrome.uss` uses `var(--…)` **120 times** and contains exactly **one**
literal color, `rgba(0,0,0,0)` at `:473` — a transparent, not a color. Palette
rides the PanelSettings theme (SSG-Ice); the stylesheet carries structure only.
The Ice token set (`unity/Assets/UI/Themes/SSGPalette-Ice.uss`):

`--ssg-shell #04070D` · `--ssg-ground #060A12` · `--ssg-panel #0A1120` ·
`--ssg-panel2 #0E1728` · `--ssg-line1 #1C2A40` · `--ssg-line2 #22304A` ·
`--ssg-ink1 #E6EEFA` · `--ssg-ink2 #9FB2CA` · `--ssg-ink3 #5A6F8C` ·
`--ssg-acc #86D7FF` · `--ssg-acc-dim #35507A` · `--ssg-warn #FFB000` ·
`--ssg-good #7DDBA0` · `--ssg-bad #FF7A6B` · `--ssg-quit-line #4A3F22` ·
`--ssg-quit-ink #D99A1E` · `--ssg-quit-fkey #8A6412` · `--ssg-title-bloom`.

A Phosphor preset exists as a swappable sibling (`SSGPalette-Phosphor.uss`).

### 6.1 LensRail
`LensRail.cs:28` — the left rail, five labelled groups.

| Group | Chips (swatch) |
|---|---|
| POLITICAL | domains `#46B5A4` · war `#E05555` · tension `#E08A4A` · currency `#B08AE0` |
| LOGISTICS | lanes `#56C4DC` · traffic `#2E7E96` · trade (MarginGold) · fleets `#C7D3EA` · works `#F0C35F` · `price ▾ <good>` `#8FBF6A` + a goods dropdown |
| KNOWLEDGE | tech `#7FA6E8` · plague `#B9E86F` · news `#E8D66F` |
| NARRATIVE | POIs `#D8B46F` |
| NATURE | one chip per `NatureLayer`, all `#5A6E9E` |

- **Defaults**: domains and lanes on, everything else off (`:33-35`).
- **Radio groups**: `{war, tension, tech, currency}` are mutually exclusive (one
  domain fill at a time); `{lanes, traffic, trade}` are mutually exclusive (one
  stroke mode).
- **Implicit forcing**: `domainsVisible = domains ∨ war ∨ tension ∨ tech ∨ currency`
  (`:237`); `lanesVisible = lanes ∨ traffic ∨ trade ∨ plague ∨ war` (`:253`).
- **Debt — grouping contradicts behaviour.** `tech` sits under KNOWLEDGE but is
  radio-exclusive with the three POLITICAL accents (`:148-150`). Clicking a
  KNOWLEDGE chip silently turns off a POLITICAL one.
- **Debt — no keyboard access.** Chips are click-only; there are no lens
  shortcuts anywhere in the atlas.
- **Debt — sibling layers have no chip.** Flow trails and crawl paths ride
  `works`; worked dust rides `domains`; outposts are always-on. Four rendered
  things the rail cannot address.

### 6.2 TopBar
`TopBar.cs:14`. Left to right: eye chip `GOD ▮` · clock
`y{WorldYear} · epoch {EpochIndex}` · era name · stamp
`seed {MasterSeed} · r{radius} · {artifact}` · spacer · drawer buttons
**THREADS · CONTRACTS · STATS · GOODS · KNOBS** · a search field (Enter opens
`PanelType.Find`) · an artifact-path field + **LOAD** button (`:55-109`).

- Era resolves as the last detected era covering the live epoch (`:137-145`).
- **Debt — the eye is hardcoded.** `SimHost.Eye` is always `EyeContext.God(…)`
  (`SimHost.cs:32`) and the chip is a static label reading "GOD ▮" with
  "controller reserved" in the doc comment (`:8`). The structure-follows-Eye seam
  the spec parks is, in the shipped atlas, not a seam at all — there is exactly
  one eye and no path to another.
- **Debt — dev affordances in the shipping bar.** A raw artifact **path text
  field** and a LOAD button sit permanently in the top bar. So does the RUN SEED
  cluster in the timeline strip (below). These are inspector controls living in
  player chrome.

### 6.3 LegendPanel
`LegendPanel.cs:15` · source `LegendQuery.For(activeKey, priceGood)`.

- Entries come from **the same Core constants the layers draw with** — drift-proof
  by construction, and there is an EditMode test guarding it
  (`unity/Assets/Atlas/Tests/LegendDriftTests.cs`).
- Four swatch kinds: `Fill`, `Stroke`, `Glyph`, `Ring` (`LegendQuery.cs:9`).
  Glyph rows crop the sprite sheet by enum name via `Enum.TryParse` and tint it
  (`LegendPanel.cs:67-89`).
- Hides itself entirely when a lens returns no entries (`:42-46`).
- **15 legend keys**: domains · war · tension · currency · lanes · traffic ·
  trade · fleets · works · price · tech · plague · news · pois · ports, plus a
  `nature*` prefix branch.
- **Debt — only one lens's legend is ever visible.** `ActiveLegendKey` is a
  fixed priority chain (`LensRail.cs:46-61`): with war *and* works *and* POIs all
  on, only "war" gets a legend. The other two vocabularies are simply absent.
- **Debt — the `ports` legend is unreachable.** `ActiveLegendKey` never returns
  `"ports"`, so that entry can never render. Ports — an always-on element — have
  no legend on any lens.
- **Debt — the nature legend is generic.** Every nature layer gets the same
  two-entry "low / high — the raster's floor / peak" card.

### 6.4 TimelineStrip
`TimelineStrip.cs:15` — bottom chrome, three rows.

- **Transport bar**: `|<` · `<` · PLAY/PAUSE · `>` (step 1) · `>>` (step 5) ·
  keyframe readout `kf n/total` · resolution chips (generation · 5y · 1y — a
  change mid-run **forks a branch**) · fork badge when branches > 1 · SEED / R /
  EP fields + **RUN SEED** (`:75-149`).
- **Track**: era bands classed by `EraKind` (expansion / treaty / upheaval / war /
  quiet, `:323-331`), an event-density sparkline from `TimelineQueries.EventDensity`
  normalised to its own max (`:215-228`), keyframe ticks, and the active-year
  marker.
- **Scrubbing**: pointer down/move snaps to the **nearest keyframe**, not to a
  free year (`:335-353`). A `_dragging` latch keeps the strip from rebuilding
  mid-drag, with `PointerCaptureOut` and `PointerCancel` both unwedging it
  (`:268-279`) — carefully handled.
- **Axis**: `y0` · live `y… · epoch …` · `y{end}`, where end never shrinks on a
  scrub back (`:301-311`).
- **Debt.** The whole strip rebuilds on **every** step and time change (`:37-38`,
  `:52`), which is why half-typed seed values need explicit preservation
  (`:129-134`). Scrub granularity is keyframes only — there is no way to land on
  an arbitrary year.

### 6.5 HexTooltip
`HexTooltip.cs:13` · source `SelectionModel.HoverInfo` (`HexQuery.At`).

- **Rest delay 0.45 s** before showing — the recorded fix for tips spamming
  every hex crossed (`:19`).
- Content: system summary as title, then hex coords (dim), owner line
  (`domain of X` / `contested: A · B` / `the wilds`), port line, outpost line with
  candidacy gloss, and one line per live POI (`:72-88`).
- In the orbit view the hovered **thing** leads and hex context dims below it
  (`:63-71`).
- Follows the cursor at +14/+10 px, flipping to stay on screen (`:115-122`).
- Rides the picking-ignored tooltip layer, so it never blocks the map.

---

## 7. Panels & selection

### 7.1 SelectionModel
`SelectionModel.cs:34`. Plane-intersection picking — **no colliders**, stated as
the PoC lesson (`:29-30`).

- **10 selection kinds** (`:10-11`): None · Hex · Port · Outpost · Project ·
  Shipment · Fleet · Poi · Facility · System.
- **Resolution order, most specific first** (`Resolve`, `:232-249`):
  port → outpost → project → freight → fleet → live POI → bare hex. Pure over the
  read model, so it is EditMode-coverable without the pointer stack.
- **Click vs drag.** A click is a press and release within 25 px² — drags belong
  to the camera (`:88-96`). Right-click (no wander) clears the highlight
  (`:115-125`).
- **Hover and selection are distinct states.** Hover drives the tooltip only;
  selection drives the hex ring plus the dock.
- **The highlight**: a hexagonal ring mesh on the lattice's own `CornerOffsets`,
  colour `#86D7FF E6` — explicitly *the UI accent, an affordance over the map,
  not a data color* (`:309-311`). Screen-constant ~3 px, rebuilt only when the
  stroke drifts >15%, **never LOD-faded** (`:344-354`).
- **Stage picking**: while `SystemStage.Live`, the stage wins. Nearest pickable
  within its world radius or ~9 px of grace, ties going to higher `Priority`
  (`:174-212`).

**Known debts.**
- **No multi-select, no selection history, no keyboard navigation** between
  selections. One subject at a time, mouse only.
- **Right-click clears the ring but not the dock** — the panels stay open with
  their own X buttons (`:128-129`). Two different "deselect" gestures with two
  different scopes.
- **No hover highlight on the map at all.** Hovering produces a tooltip but no
  visual mark on the hovered hex — the only map-side feedback is the tooltip
  appearing 0.45 s later.

### 7.2 InspectorDock
`InspectorDock.cs:61` — the right-side dock.

- **28 panel types** (`PanelType`, `:12-18`), **27 builders** wired in
  `PanelViews.Build` (`PanelViews.cs:14-49`).
- **PIN / X.** Opening a panel clears all *unpinned* panels; pinned ones stay for
  comparison (`:235-247`). A port click deliberately opens **two** panels — the
  owner's Polity and the port's Market — by passing `clearUnpinned: false` on the
  second (`:178-189`).
- **Time behaviour is the interesting part.** On a step or scrub, unpinned panels
  re-query against the new moment while **pinned panels keep their captured
  moment** — comparison across time (`:90-92`, `:125-153`). A new world closes
  everything, pins included, and Open Threads greets (`:112-118`).
- **Staleness is handled**: a subject the new moment doesn't know renders its
  panel's missing placeholder; only a build that throws closes the panel
  (`:120-124`, `:135-148`).
- Panel builders can open further panels and jump the camera (`PanelContext`,
  `:48-54`).

### 7.3 The panel family
`PanelViews.cs` (1312 lines) over `DockKit` primitives (`DockKit.cs:9`:
`Sect` · `Kv` · `Line` · `Meter` · `Row` · `Tag` · `Link` · `Table`/`TableRow`/
`Cell`/`CellStack` with reusable width buckets w36…w84).

The 27 builders: Hex · Polity · Market · Project · Shipment · Fleet · Designs ·
Wars · War · Relations · Character · Corporations · Poi · Beliefs · News ·
Stances · Chronicle · ChroniclePlace · Eras · Threads · Contracts · Find ·
Goods · Knobs · Stats · Facility · System.

*(The Tier-0 kickoff's completeness floor named five — polity, market, war,
contracts, order book. The real family is five times that.)*

- **Market** is the flagship (`:420-512`): larder, a six-column goods table
  stating its currency **once at the header** (`:451-460`), the resting order
  book at order granularity, segments, facilities, lanes-to. A selected outpost
  renders as a leading section inside its parent's Market, keyed by `SubId` —
  an outpost is not an actor and has no market of its own (`:430-435`, `:575-615`).
- **Number formatting** is centralised through `DockKit.Inv` (invariant culture),
  used on every interpolated numeric string.
- **Empty states are a genuine strength here** — and the sharpest contrast in the
  whole inventory. Panels carry specific, voiced placeholders:
  *"(a tidied museum — nothing is in motion; this should worry you more than a
  war)"* (`:61`), *"(bare book — no resting orders)"* (`:547`),
  *"(unpeopled — a claim, not yet a home)"* (`:611`), *"(a quiet board — nothing
  posted)"* (`:110`), *"(still in transit — nobody has heard)"* (`:1122`),
  *"(no chronicle presence — a quiet life)"* (`:1011`). Missing subjects route
  through a shared `Missing(body, …)` helper.
- **REPL parity is the stated contract**: *"No derivations here; Core owns every
  number"* (`:10-11`). Each builder is a view over one Core query, the same query
  the Inspector REPL prints (`ebook`, `econtracts`, `emap`, `InteriorView` are
  named in the comments).

---

## 8. SystemStage

`SystemStage.cs:49` — the orbit view the map crossfades into at System LOD.

- **Every visible system renders at once** — no pop-in; zooming magnifies one
  until it fills the view. Capped at `MaxVisibleHexes` 160 (`:61`). Each system
  is scaled to fit **inside its own hex**: `FitRadius` 0.78 against a ~0.866
  inradius, shrink-only (`:59-60`, `:348`).
- **Coplanar with the lattice** at `StageZ = −0.02`; draw order rides
  `renderQueue`, not depth, so a lifted stage doesn't parallax against the grid
  (`:51-56`).
- **Geometry**: ring radius `0.30 + 0.115 × slot` for the primary,
  `0.10 + 0.05 × slot` for companions (`:249-251`); 96 segments; belts dashed
  2-on-1-off (`:586`).
- **Palette** (`:255-281`): star tint by type id — ember_dwarf `#FF8A5C`,
  amber_dwarf `#FFC066`, gold_main `#FFD066`, white_blaze `#EDF2FF`, blue_titan
  `#7FA6E8`, ashen_remnant `#9AA7C0`, collapsed_core `#C9B8E8`; body tint by kind
  — rocky `#C9A06A`, ice `#A8D8E8`, gas giant `#E08840`, wreckage `#A88FB8`;
  rings `#262C3F`, belts `#9A8F7A B4`, **habitable band `#3FBF7F` at alpha 0x10**,
  moons `#B9BFD0`, settled `#FFBF4F`, facilities `#D8B46F`.
- **Marks**: additive halo + solid core per star; body dots sized
  `0.028 + 0.009 × size`; up to 4 moons hugging each body; settled worlds get an
  accent ring at 2.9× the body; the port is an owner-colored ring around its body;
  facilities and in-flight sites are gold **squares** spread around their anchor.
- **Scaffold children are `HideFlags.DontSave`** (`:640`) — load-bearing since
  Slice WG, because captures now run inside the committed scene and a plain
  GameObject would be junk the next Ctrl+S commits.
- `atlas-smoke-system.png` and `atlas-smoke-system-field.png` (recipe S) are the
  evidence; the multi-system field mid-crossfade is one of the strongest images
  the atlas produces.

**Known debts.**
- **Bodies are not selectable subjects.** Every star and body pickable is
  `SelectionKind.System` with `Id = −1` (`:399`, `:426`, `:451`) — clicking any
  body opens the System panel for the hex. Only ports, facilities and projects
  carry typed ids. The orbit view renders per-body detail it cannot address.
- **The habitable band is nearly invisible** at alpha `0x10` (16/255) — the one
  piece of genuinely decision-relevant information in the orbit view is the
  faintest thing drawn.
- **Facilities are untextured square outlines** at 7 px — in
  `atlas-smoke-system.png` they read as placeholder artefacts, not icons. They
  are the only stage marks with no authored glyph.
- The full rebuild is keyed on a FNV hash of the visible hex set (`:233-245`), so
  any camera move that changes the set rebuilds every system in view.

---

## 9. Cross-cutting

### 9.1 Colour discipline
`AtlasPalette` (`src/Core/Atlas/AtlasPalette.cs:16`) is the single value→color
authority, engine-free so every palette decision is xUnit-coverable. `Void`
`(10,10,14)` · `Floor` `(24,26,32)` · `Clear` · `Ramp(base, v)` from the common
floor · `OwnerColor` by golden-ratio hue.

Every layer CPU-linearizes vertex colors before upload (`GlyphLayer.cs:126-127`,
`SystemStage.cs:569-573`, and each billboard layer) — the project renders linear
and tints are authored sRGB. The recorded failure mode: *"#262C3F rings came out
lavender, the 9% hab band solid"*.

### 9.2 The two empty-state regimes
This is the sharpest cross-cutting finding in the inventory.

- **Panels** have specific, voiced, deliberate empty states throughout (§7.3).
- **The map has none at all.** No lens says "no wars", "no trade", "no plague",
  "no works". Evidence: `atlas-grid-degen/seed-7-war.png` is a grey wash;
  `seed-42-trade.png` is two circles and empty space; `seed-1234-works.png` is
  four domain ellipses with no glyphs; `atlas-smoke-plague.png` is
  indistinguishable from the base view. In every case the player cannot
  distinguish *"the sim has nothing here"* from *"this lens is broken"*.

### 9.3 Where the evidence strains a standing decision
Recorded for Tier 1, not challenged here.

- **"Cassette × Ice."** The chrome honours it thoroughly (§6, 120 tokens / 1
  literal). The **map** does not participate in the token system at all — every
  map color is a C# constant in a layer or a Core lens. The two halves of the
  atlas are governed by different colour authorities with no bridge.
- **"Fields computed, glyphs authored, placement always data."** Held cleanly
  everywhere, including the deliberate refusal to treat generated discs and rings
  as identity glyphs. The strain is downstream: the authored shape channel is
  spent on 16 icons that are unreadable above the Region band (§5.3).
- **The 2.5D space/glows/billboards grammar.** Honoured. The strain is that the
  price field's hard hex blocks (§3.5) are the one element that reads as the old
  hex board rather than as space.
- **Structure-follows-Eye (instrument = god eye, cassette = controller eye).**
  There is no seam to decide: `SimHost.Eye` is hardcoded to `EyeContext.God`
  and the TopBar's eye chip is a static label.

---

## 10. Proposed group partition for Tier 1

The spec's expected five groups are adjustable on evidence. The evidence says
**six**, and the change is driven by two findings: camera & navigation has no
home in the five, and the LOD spine is not a property of any one group — it is
the thing every group's "how it reads" section must answer to.

**Proposed partition:**

| # | Group | Covers | Why |
|---|---|---|---|
| 1 | **Camera, navigation & the LOD spine** | `CameraRig`, `LodBands`, the band × layer matrix, `AtlasRoot` update/cost model, framing & anchor behaviour | §1–2. This must go **first**: every other group's encoding questions ("is this readable?", "when does this resolve?") are answered in terms of bands and fades. Deciding it after the fact would force rework in all five. |
| 2 | **Map fields & lenses** | starfield, domain field + 5 accents, domain interior, nature, price, lattice, lens gating | §3. Coherent — all are plane quads/rasters competing for the same pixels. |
| 3 | **Marks, billboards & the glyph vocabulary** | the `GlyphLayerBase`/`DotMarkLayer` machinery, ports, fleets, POIs, works, war, plague, outposts, worked dust, news rings, and the 16-icon vocabulary | §5. Feeds Tier 2's icon manifest directly. |
| 4 | **Lanes, flows & motion** | `LaneLayer` (5 modes), flow trails, crawl paths, news pulse growth, and the atlas's motion grammar as a whole | §4. **Split out of "map fields"**: strokes are a different encoding problem from rasters (width/dash/direction vs area/hue), they are the only elements carrying *time* in their form, and they are where the pass's motion questions actually live. |
| 5 | **Chrome** | `AtlasChrome`, LensRail, TopBar, LegendPanel, TimelineStrip, HexTooltip, token conformance | §6. Unchanged from the spec. Prerequisite: a chrome-inclusive capture path. |
| 6 | **Panels & selection** | `SelectionModel` end-to-end, `InspectorDock`, the 27-builder family, `DockKit`, REPL parity | §7. Unchanged from the spec. |

**SystemStage is folded in, not dropped.** Its four concerns distribute cleanly —
its LOD/crossfade to group 1, its orbit geometry and palette to group 2, its
marks and the facility-square gap to group 3, its picking and the System panel to
group 6. Keeping it as a sixth silo would mean deciding the same encoding
questions twice. *If the user prefers it standalone, it works as a seventh group
scheduled after 1–3 rather than in parallel.*

**Ordering constraint:** group 1 before all others. Groups 2–4 are mutually
independent. Groups 5 and 6 need the §11 capture path built as a committed tool
first. Group 6 is independent of 5 but benefits from following it.

---

## 11. Capturing the chrome

**The problem.** Every existing capture path renders through `cam.Render()`,
which draws the scene and nothing else. A UI Toolkit **overlay** panel
(`m_RenderMode: 0` in `unity/Assets/Atlas/PanelSettings.asset:18`) is composited
by the runtime UI system, not by any camera, so it can never appear in a camera
render. `screenshot --view game` does not help either — tested, it also returns
a camera render with no chrome.

There is a second, deeper problem behind it. The chrome modules build themselves
in `OnEnable` (`AtlasChrome.cs:33`, `LensRail.cs:69`, `TopBar.cs:29`,
`LegendPanel.cs:23`, `TimelineStrip.cs:33`, `HexTooltip.cs:30`) and none of them
is `[ExecuteAlways]`. In EditMode — where `AtlasSmoke` and `AtlasGrid` run —
those never fire, so **there is no chrome to capture in the first place**. This
is the same edit-mode gap that forces `AtlasSmoke.SetAndStyle` to hand-mirror
`AtlasRoot.OnZoomChanged` (`AtlasSmoke.cs:240-273`).

**The solution: capture from play mode, via the panel's target texture.**
Proven end-to-end during Tier 0 against `cbb892d`, editor `6000.5.2f1`, pipeline
`0.4.0-exp.1`. Five steps:

1. **`unity command set_autotick --enable true --interval_ms 16`** — a
   background editor does not tick, so play mode would never advance a frame.
2. **`unity command editor_play`** — every `OnEnable` fires for real. `SimHost`
   loads its artifact, the rail builds its chips, the dock opens Open Threads.
   Verified: `rootChildren=6`, `topbarKids=14`, `model=True`. This is the
   genuine chrome, not a hand-mirrored reconstruction — which also makes it
   immune to the `SetAndStyle` drift risk.
3. **Assign a RenderTexture to `PanelSettings.targetTexture`.** The panel then
   renders into that RT instead of the screen overlay, independent of any Game
   view window. Measured: 611,412 of 1,600,000 pixels with non-zero alpha at
   1600×1000 — a correct, alpha-carrying UI layer.
4. **Render the camera into a second RT** (the existing `AtlasSmoke` framing
   code applies unchanged, `_AtlasFocalY` / `_AtlasViewportPx` globals included).
5. **Alpha-composite UI over map** and encode. One frame, everything in it.

**Driving it is the point.** `InspectorDock.Show(PanelRequest, clearUnpinned)`
is public, as is `PanelRequest`, so any of the 27 panels can be opened on demand
and shot — proven by opening Market #3 + its owner's Polity + Relations together
and capturing the result. Selection, lens state (`LensRail`), epoch
(`SimHost.StepEpochs` / `ScrubTo`) and camera (`CameraRig.SetView`) are all
drivable the same way. That is what makes a panels design pass possible at all:
the panel family is 27 builders over live Core queries, and every one of them
can now be photographed with real data.

**Two cautions.**
- `PanelSettings` is a **committed asset**. Assigning `targetTexture` mutates it,
  and asset edits made during play mode persist in the editor session. **Null it
  before leaving play mode** and verify `m_TargetTexture: {fileID: 0}` on disk.
  Tier 0 did; the asset is clean.
- Autotick at 16 ms pegs a core. Disable it when the capture run ends.

**What this needs to become.** The spike ran through `unity command eval`
(Roslyn, no files touched), which was right for proving it and wrong for
repeated use. Turning it into a committed `AtlasChromeShots` editor tool —
`[MenuItem]` + `RunFromCli()` + `[CliCommand]` twin, per the pattern in
`.claude/skills/driving-the-unity-editor/SKILL.md` — is **atlas tooling work
outside this pass's boundary** (`unity/Assets` is out of scope for a design
pass). It wants its own small slice, and it should land before Tier 1 reaches
groups 5 and 6.
