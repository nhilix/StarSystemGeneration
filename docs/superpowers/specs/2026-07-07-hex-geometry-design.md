# Hex Geometry Foundation — Axial Coordinates, Superhex Cells, Origin-Centered Galaxy

Status: **draft — awaiting user review**
Date: 2026-07-07

## 1. Overview

Core's spatial model moves from a square grid with 4-neighbor adjacency to true
hexagonal geometry at every layer: hexes have six equidistant neighbors, region cells
become 91-hex hexagonal clusters, and the galaxy becomes an origin-centered hexagonal
disc. This supersedes the Traveller-derived rectangular conventions (32×40 sectors,
8×10 subsector cells), which were a jumping-off point, not a commitment.

**Why now:** grid anisotropy compounds. Everything specced for the sim's future —
expansion fronts, trade flows, news propagation, war fronts, travel — is the class of
system where square grids visibly bias along axes (current kingdom borders are
axis-aligned and blocky). Hex adjacency gives isotropic spread. The conversion
surface today is one sim stage and ~90 tests; after sim stages 2–6 it would be five
stages, a news graph, and a pathfinder. This is the cheapest this change will ever
be. It also unblocks the Unity atlas (companion spec), whose rendering needs hex
geometry regardless.

## 2. Coordinate System

- **`HexCoordinate` becomes axial `(Q, R)`** — still a two-int readonly struct, so
  equality, hashing, and `RollContext`'s ulong packing carry over unchanged (uint
  casts handle negative components bijectively; determinism machinery untouched).
- **Cube form** (`s = -q - r`) is derived inside algorithms, never stored.
- **Orientation: flat-top hexes** (echoes classic Traveller column maps). For
  display conversions the convention is **odd-q offset** (columns stagger by half a
  hex).
- Offset coordinates exist **only at presentation boundaries**: ASCII atlas row
  rendering and designation formatting. Core math never touches them.

### 2.1 The `HexGrid` utility

One static class in Core owning all geometry — consumed identically by the sim, the
inspector, and Unity rendering (one implementation; map and sim can never disagree):

| Member | Contract |
|---|---|
| `Neighbors(hex)` | the 6 adjacent hexes, fixed deterministic order, no parity branches |
| `Distance(a, b)` | cube distance (metric: symmetric, triangle inequality, 0 iff equal) |
| `Ring(center, radius)` | the ring at exactly `radius`, deterministic order |
| `Spiral(center, radius)` | center + rings 1..radius — the canonical deterministic enumeration (replaces row-major scans) |
| `HexToWorld(hex)` | flat-top 2×2 matrix → Cartesian center point (unit hex size; scale is the consumer's concern) |
| `WorldToHex(point)` | fractional axial + cube rounding (inverse of `HexToWorld`) |
| `CellOf(hex)` | which 91-hex cell a hex belongs to (scaled cube rounding — the standard superhex construction) |
| `CellCenter(cell)` | the center hex of a cell |
| `ToOffset(hex)` / `FromOffset(col, row)` | odd-q conversions, presentation use only |

## 3. Cell Lattice

- A **cell is a radius-5 hexagonal cluster: 91 hexes** (centered hexagonal number),
  replacing the 80-hex rectangular subsector. Cluster centers themselves form a
  coarser hex lattice; cell-to-cell adjacency is 6-neighbor.
- **The "sector" concept dissolves.** The zoom ladder is exactly three layers:
  galaxy (cell lattice) → cell (91 hexes) → hex (system). Everything sector-shaped
  (REPL `sector` command, 32-wide walk conventions) is superseded.
- `GalaxySkeleton.Cells` becomes a flat `List<RegionCell>` in **deterministic spiral
  order from the center cell**, plus a dictionary keyed by cell axial coord for O(1)
  lookup. `RegionCell.Cx/Cy` become the cell's axial `Q/R`. "Linear index" for
  determinism ordering = position in the spiral list.

## 4. Galaxy Shape

- **Origin-centered:** hex (0,0) is the galactic core. The galaxy is the hex-shaped
  disc of cells with `Distance(cellCoord, origin) <= GalaxyRadiusCells`.
- `GalaxyConfig.SizeSectors` is **replaced by `GalaxyRadiusCells`** (default **21**):
  1,387 cells × 91 hexes ≈ 126k hexes — nearly identical to the old 100-sector
  default, so density/tuning intuitions carry over. Same-seed galaxies still differ
  by radius, as before with size.
- `DensityField` normalizes by world distance: `r = |HexToWorld(hex)| / rimWorldRadius`
  — replaces the width/height arithmetic; the disc becomes genuinely circular. Noise
  sampling now uses the hex's world position (not raw q/r), so noise is isotropic in
  world space.
- Hexes beyond the rim do not exist: out-of-galaxy coordinates are empty space, as
  before.

## 5. Migration Map (what changes / what survives)

**Changes:**

| Area | Change |
|---|---|
| `HexCoordinate` | axial semantics (rename fields X/Y → Q/R) |
| Sim adjacency (`EpochSim`) | 6-neighbor via `HexGrid.Neighbors` over cell coords |
| Connectivity graph / chokepoints | same articulation algorithm, 6-neighbor edges |
| `RegionContext` smoothing | bilinear → **inverse-distance weighting over the hex's own cell + its 6 neighbors** (no corner cases, smoother) |
| Anchor placement | in-cell index 0–90 mapped via `Spiral(cellCenter, 5)` position; forward-probe collision rule unchanged |
| Density summaries | sample the cell's hexes via spiral enumeration (every 2nd spiral index ≈ 45 samples) |
| Serializer | **SchemaVersion 2**: cells keyed `q\|r`, config field `GalaxyRadiusCells`; v1 artifacts refuse to load with the existing version-mismatch path (regenerate — pre-release, no compat shim) |
| Designations | display-side bias so labels stay non-negative and stable-width: `SGC {q+2048:D4}-{r+2048:D4}` |
| ASCII atlas | flat-top rendering: odd columns drop half a line (standard flat-top idiom); galaxy map still one glyph per cell, doubled horizontally |
| REPL walk | `next`/`prev`/`find`/`stats` walk the galaxy's spiral enumeration instead of row-major; `sector` command replaced by `cell <q> <r>` zoom (already exists) |
| Inspector spike (`GalaxyMapSpike`) | repainted from cell lattice positions via `HexToWorld` (or retired in favor of the atlas — implementer's judgment) |

**Survives untouched:** the entire per-hex generation pipeline (never consults
neighbors — coordinate-shape-agnostic by construction), flatspace mode and the legacy
`Generate(seed, coord)` wrapper, Phase 1's 46 tests, `StableHash`/`RollContext`/
channel registry, all content tables, the overlay system, naming, `SystemFormatter`.

## 6. Determinism & Tests

- All sim outputs change (different adjacency → different history): goldens are
  **re-frozen once**, with the change called out in the commit. Shape acceptance
  bands (presence rate, claimed fraction, zone mix) are ratios — they stay as-is and
  must still pass, which is the guard that the hex conversion didn't distort the
  galaxy's character.
- New **`HexGrid` unit suite**: neighbor symmetry (b ∈ N(a) ⇔ a ∈ N(b)); distance
  metric properties; `Ring(c, r)` has exactly `6r` members; `Spiral(c, r)` has
  exactly `3r(r+1)+1`; `WorldToHex(HexToWorld(h)) == h` for a large sample;
  **cluster partition**: every hex maps to exactly one cell, `CellOf` agrees with
  `CellCenter` round-trips, and each cell contains exactly 91 hexes.
- Determinism suite unchanged in kind: same config → byte-identical artifact; same
  artifact → identical hexes; flatspace bit-identity preserved.

## 7. Out of Scope

- Continuous-zoom rendering, pathfinding, travel mechanics (future consumers of this
  geometry, not part of it).
- Any change to per-hex generation content or the sim's *rules* — this spec changes
  where things are, not what happens.
- v1→v2 artifact migration tooling (pre-release; regenerate).
