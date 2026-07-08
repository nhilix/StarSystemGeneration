# Hex Geometry Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert Core's spatial model to true hexagonal geometry per `docs/superpowers/specs/2026-07-07-hex-geometry-design.md`: axial coordinates, one `HexGrid` utility, 91-hex superhex cells, an origin-centered hexagonal galaxy disc, 6-neighbor sim adjacency, serializer schema v2, and hex-aware inspector rendering.

**Architecture:** `HexCoordinate` stays a two-int struct but becomes axial `(Q, R)` with flat-top orientation; every geometric operation (neighbors, distance, rings/spirals, world transforms, cluster assignment, offset display conversion) lives in a new static `HexGrid`. The rectangular sector/cell lattice is replaced by a hex lattice of radius-5 clusters; the sim, skeleton, seeding passes, region context, serializer, and ASCII atlas migrate on top. The per-hex generation pipeline is untouched by construction (it never consults neighbors).

**Tech Stack:** unchanged — netstandard2.1 Core (zero packages), net10.0 tests/inspector, xUnit.

## Global Constraints

- `src/Core` stays **netstandard2.1**, zero package references; no `System.Random`/`DateTime`; deterministic iteration everywhere (spiral order replaces row-major).
- `RollChannel` values **never renumbered or reused** — this plan adds NO channels and changes NO channel usage semantics (indexes/subIndexes keep their meanings; the *values fed to them* change where placement math changes, which is expected output change, not channel drift).
- **Flat-top hexes; odd-q offset for display only** (spec §2). Axial directions, ring/spiral order, and the world matrix are pinned in Task 1 and never restated differently.
- Cells are **radius-5 clusters, exactly 91 hexes** (spec §3). Cluster lattice basis: `A = (11, -5)`, `B = (5, 6)` (determinant 91).
- `GalaxyConfig.SizeSectors` is replaced by **`GalaxyRadiusCells` (default 21)**; `HomeworldRatePerSector` by **`HomeworldRatePerCell` (default 0.02)** (≈ same polity counts).
- Serializer bumps to **SchemaVersion 2**; v1 artifacts refuse to load via the existing mismatch path (no migration shim, spec §7).
- Sim outputs change once: **goldens re-frozen with the change called out**; all ratio-based shape acceptance bands (presence mean, claimed fraction [0.2, 0.85], zone/lean mixes) must still pass unmodified — they are the guard that hex conversion preserved the galaxy's character. Never widen a band; tune only where a task's tuning clause names a knob.
- The per-hex pipeline, flatspace mode, `StableHash`/`RollContext`, content tables, overlays, naming, and `SystemFormatter` are **not modified** (spec §5). One explicitly allowed Phase 1 test amendment: `PresenceTests.Designation_Format` updates to the new biased designation (spec migration map overrides the blanket "untouched" for exactly this test).
- Commits: conventional style ending with the trailer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## File Structure

```
src/Core/Galaxy/HexGrid.cs            # NEW: all hex math (Tasks 1-3)
src/Core/Model/HexCoordinate.cs       # X/Y -> Q/R rename (Task 1)
src/Core/Galaxy/GalaxyConfig.cs       # GalaxyRadiusCells, HomeworldRatePerCell (Task 4)
src/Core/Galaxy/DensityField.cs       # origin-centered, world-distance normalized (Task 4)
src/Core/Galaxy/RegionCell.cs         # Q/R + SpiralIndex (Task 5)
src/Core/Galaxy/GalaxySkeleton.cs     # spiral list + dict lattice (Task 5)
src/Core/Galaxy/SkeletonBuilder.cs    # passes over hex lattice (Tasks 5-6)
src/Core/Galaxy/EpochSim.cs           # 6-neighbor adjacency (Task 7)
src/Core/Galaxy/RegionContext.cs      # IDW smoothing over cell+6 neighbors (Task 8)
src/Core/Naming/Designation.cs        # +2048 bias format (Task 8)
src/Core/Galaxy/SkeletonSerializer.cs # SchemaVersion 2 (Task 9)
src/Core/Galaxy/GalaxyEnumerator.cs   # NEW: deterministic all-hexes walk (Task 10)
src/Inspector/GalaxyMapView.cs        # flat-top staggered rendering (Task 10)
src/Inspector/Repl.cs                 # spiral walk, cell zoom, sector removed (Task 10)
src/Inspector/StatsReport.cs          # enumerator-based walk (Task 10)
unity/Assets/Scripts/GalaxyMapSpike.cs        # DELETED (Task 10; atlas replaces it)
unity/Assets/Editor/StarGenSpikeMenu.cs       # DELETED (Task 10)
docs/DESIGN.md                        # sector-language cleanup (Task 11)
tests/Core.Tests/Galaxy/HexGridTests.cs       # NEW (Tasks 1-3)
(all existing Galaxy test files)      # migrated per task
```

Scene note: deleting the spike scripts leaves a missing-script reference on the
`GalaxyMap` object in `SampleScene.unity`. Scene cleanup is performed by the
controller via the Unity MCP bridge after merge — implementers do not hand-edit
scene YAML.

Reference config used by migrated tests: `seed 42, GalaxyRadiusCells = 8` (217
cells ≈ the old SizeSectors-4 fixture's 256). Golden config: `seed 7,
GalaxyRadiusCells = 3` (37 cells).

---
### Task 1: Axial HexCoordinate + HexGrid neighbors/distance/rings

**Files:**
- Modify: `src/Core/Model/HexCoordinate.cs`
- Create: `src/Core/Galaxy/HexGrid.cs`
- Test: `tests/Core.Tests/Galaxy/HexGridTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `HexCoordinate` — fields renamed `X`→`Q`, `Y`→`R` (ctor `HexCoordinate(int q, int r)`, `ToString` → `"(q,r)"`); equality/hash bodies unchanged in form.
  - `static class HexGrid` (namespace `StarGen.Core.Galaxy`): `static readonly HexCoordinate[] Directions` (exactly 6, order pinned below), `HexCoordinate Neighbor(HexCoordinate h, int direction)`, `IEnumerable<HexCoordinate> Neighbors(HexCoordinate h)`, `int Distance(HexCoordinate a, HexCoordinate b)`, `IEnumerable<HexCoordinate> Ring(HexCoordinate center, int radius)` (exactly `6*radius` members, radius ≥ 1), `IEnumerable<HexCoordinate> Spiral(HexCoordinate center, int radius)` (center then rings 1..radius; `3r(r+1)+1` members).
- **Pinned constants (never restated differently):** flat-top axial directions in order `D0=(+1,0), D1=(+1,-1), D2=(0,-1), D3=(-1,0), D4=(-1,+1), D5=(0,+1)`. `Ring` starts at `center + D4 * radius` and walks directions `D0..D5`, `radius` steps each.

- [ ] **Step 1: Rename HexCoordinate fields.** Replace `src/Core/Model/HexCoordinate.cs` contents:

```csharp
using System;

namespace StarGen.Core.Model;

/// <summary>Axial hex coordinate (flat-top orientation). Two ints, so equality,
/// hashing, and RollContext's ulong packing behave exactly as before.</summary>
public readonly struct HexCoordinate : IEquatable<HexCoordinate>
{
    public int Q { get; }
    public int R { get; }
    public HexCoordinate(int q, int r) { Q = q; R = r; }

    public bool Equals(HexCoordinate other) => Q == other.Q && R == other.R;
    public override bool Equals(object? obj) => obj is HexCoordinate h && Equals(h);
    public override int GetHashCode() => (Q * 397) ^ R;
    public override string ToString() => $"({Q},{R})";
}
```

Then fix ALL compile breaks from the rename mechanically: every `.X` → `.Q`, `.Y` → `.R` on `HexCoordinate` across `src/` and `tests/` (RollContext packing, Designation, DensityField, SkeletonBuilder, EpochSim, RegionContext, serializer, inspector, tests). Do not change any logic while renaming — this step is rename-only; later tasks rewrite the logic. `dotnet build` must succeed and `dotnet test` must be green (87) before proceeding, because rename-only cannot change behavior.

- [ ] **Step 2: Write the failing HexGrid tests** — `tests/Core.Tests/Galaxy/HexGridTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class HexGridTests
{
    [Fact]
    public void Directions_AreTheSixPinnedFlatTopVectors()
    {
        var expected = new[]
        {
            new HexCoordinate(1, 0), new HexCoordinate(1, -1), new HexCoordinate(0, -1),
            new HexCoordinate(-1, 0), new HexCoordinate(-1, 1), new HexCoordinate(0, 1),
        };
        Assert.Equal(expected, HexGrid.Directions);
    }

    [Fact]
    public void Neighbors_AreSymmetric()
    {
        var a = new HexCoordinate(3, -7);
        foreach (var b in HexGrid.Neighbors(a))
            Assert.Contains(a, HexGrid.Neighbors(b));
    }

    [Fact]
    public void Distance_IsAMetric()
    {
        var hexes = new List<HexCoordinate>();
        for (int q = -4; q <= 4; q += 2)
            for (int r = -4; r <= 4; r += 2)
                hexes.Add(new HexCoordinate(q, r));
        foreach (var a in hexes)
            foreach (var b in hexes)
            {
                Assert.Equal(HexGrid.Distance(a, b), HexGrid.Distance(b, a));
                Assert.True(HexGrid.Distance(a, b) == 0 == a.Equals(b));
                foreach (var c in hexes)
                    Assert.True(HexGrid.Distance(a, c)
                        <= HexGrid.Distance(a, b) + HexGrid.Distance(b, c));
            }
        Assert.Equal(1, HexGrid.Distance(new HexCoordinate(0, 0), new HexCoordinate(1, -1)));
        Assert.Equal(7, HexGrid.Distance(new HexCoordinate(0, 0), new HexCoordinate(7, -3)));
    }

    [Fact]
    public void Ring_HasExactly6R_AllAtDistanceR()
    {
        var center = new HexCoordinate(2, 1);
        for (int radius = 1; radius <= 6; radius++)
        {
            var ring = HexGrid.Ring(center, radius).ToList();
            Assert.Equal(6 * radius, ring.Count);
            Assert.Equal(ring.Count, ring.Distinct().Count());
            Assert.All(ring, h => Assert.Equal(radius, HexGrid.Distance(center, h)));
        }
    }

    [Fact]
    public void Spiral_HasCenteredHexagonalCount_AndIsDeterministic()
    {
        var center = new HexCoordinate(0, 0);
        for (int radius = 0; radius <= 6; radius++)
        {
            var spiral = HexGrid.Spiral(center, radius).ToList();
            Assert.Equal(3 * radius * (radius + 1) + 1, spiral.Count);
            Assert.Equal(spiral.Count, spiral.Distinct().Count());
            Assert.Equal(center, spiral[0]);
        }
        Assert.Equal(HexGrid.Spiral(center, 5).ToList(), HexGrid.Spiral(center, 5).ToList());
    }
}
```

- [ ] **Step 3: Run** — `dotnet test --filter HexGridTests` — Expected: FAIL (HexGrid missing).

- [ ] **Step 4: Implement** — `src/Core/Galaxy/HexGrid.cs`:

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>
/// All hex geometry (spec §2.1). Axial coordinates, flat-top orientation.
/// The single authority consumed by the sim, the inspector, and Unity rendering.
/// </summary>
public static class HexGrid
{
    /// <summary>Pinned flat-top direction order (spec: D0..D5). Never reorder.</summary>
    public static readonly HexCoordinate[] Directions =
    {
        new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1),
    };

    public static HexCoordinate Neighbor(HexCoordinate h, int direction)
    {
        var d = Directions[direction];
        return new HexCoordinate(h.Q + d.Q, h.R + d.R);
    }

    public static IEnumerable<HexCoordinate> Neighbors(HexCoordinate h)
    {
        for (int i = 0; i < 6; i++) yield return Neighbor(h, i);
    }

    public static int Distance(HexCoordinate a, HexCoordinate b)
    {
        int dq = a.Q - b.Q, dr = a.R - b.R, ds = -dq - dr;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
    }

    /// <summary>The ring at exactly <paramref name="radius"/> (≥ 1): starts at
    /// center + D4*radius, walks D0..D5, radius steps each. Deterministic.</summary>
    public static IEnumerable<HexCoordinate> Ring(HexCoordinate center, int radius)
    {
        var hex = new HexCoordinate(center.Q + Directions[4].Q * radius,
                                    center.R + Directions[4].R * radius);
        for (int direction = 0; direction < 6; direction++)
            for (int step = 0; step < radius; step++)
            {
                yield return hex;
                hex = Neighbor(hex, direction);
            }
    }

    /// <summary>Center, then rings 1..radius — the canonical deterministic
    /// enumeration (3r(r+1)+1 hexes).</summary>
    public static IEnumerable<HexCoordinate> Spiral(HexCoordinate center, int radius)
    {
        yield return center;
        for (int r = 1; r <= radius; r++)
            foreach (var hex in Ring(center, r))
                yield return hex;
    }
}
```

- [ ] **Step 5: Run** — `dotnet test --filter HexGridTests` — Expected: PASS (5 tests). Full suite: 92 green.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: axial HexCoordinate and HexGrid neighbors/distance/rings"`

---

### Task 2: HexGrid world transforms + offset conversions

**Files:**
- Modify: `src/Core/Galaxy/HexGrid.cs` (append members)
- Test: append to `tests/Core.Tests/Galaxy/HexGridTests.cs`

**Interfaces:**
- Produces: `static (double X, double Y) HexToWorld(HexCoordinate h)` — flat-top unit-size matrix `x = 1.5*q`, `y = sqrt(3)*(r + q/2.0)`; `static HexCoordinate WorldToHex(double x, double y)` — inverse via fractional axial + cube rounding; `static (int Col, int Row) ToOffset(HexCoordinate h)` — odd-q: `col = q`, `row = r + (q - (q & 1)) / 2`; `static HexCoordinate FromOffset(int col, int row)` — inverse. (Offset is presentation-only, spec §2.) Also `static readonly (double X, double Y)[] CornerOffsets` — six flat-top unit-hex corner offsets (corner 0 due east, CCW), the atlas's mesh-vertex source.

- [ ] **Step 1: Write the failing tests** — append to `HexGridTests`:

```csharp
    [Fact]
    public void WorldToHex_InvertsHexToWorld()
    {
        for (int q = -15; q <= 15; q += 3)
            for (int r = -15; r <= 15; r += 3)
            {
                var hex = new HexCoordinate(q, r);
                var (x, y) = HexGrid.HexToWorld(hex);
                Assert.Equal(hex, HexGrid.WorldToHex(x, y));
                // points near the center still round to the same hex
                Assert.Equal(hex, HexGrid.WorldToHex(x + 0.3, y - 0.3));
            }
    }

    [Fact]
    public void HexToWorld_NeighborsAreEquidistant()
    {
        var origin = new HexCoordinate(0, 0);
        var (ox, oy) = HexGrid.HexToWorld(origin);
        var distances = HexGrid.Neighbors(origin).Select(n =>
        {
            var (x, y) = HexGrid.HexToWorld(n);
            return System.Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
        }).ToList();
        Assert.All(distances, d => Assert.Equal(distances[0], d, 9));
    }

    [Fact]
    public void OffsetConversions_RoundTrip_AndStaggerOddColumns()
    {
        for (int q = -9; q <= 9; q++)
            for (int r = -9; r <= 9; r++)
            {
                var hex = new HexCoordinate(q, r);
                var (col, row) = HexGrid.ToOffset(hex);
                Assert.Equal(q, col);
                Assert.Equal(hex, HexGrid.FromOffset(col, row));
            }
        // odd-q: hex (1,0) sits half a hex lower in world y than (0,0)
        var y0 = HexGrid.HexToWorld(new HexCoordinate(0, 0)).Y;
        var y1 = HexGrid.HexToWorld(new HexCoordinate(1, 0)).Y;
        Assert.True(y1 > y0, "odd columns stagger downward in world space");
    }

    [Fact]
    public void Corners_SixUnitOffsets_SharedBetweenNeighbors()
    {
        Assert.Equal(6, HexGrid.CornerOffsets.Length);
        // flat-top: first corner due east at unit distance, 60° apart
        Assert.Equal(1.0, HexGrid.CornerOffsets[0].X, 9);
        Assert.Equal(0.0, HexGrid.CornerOffsets[0].Y, 9);
        foreach (var (x, y) in HexGrid.CornerOffsets)
            Assert.Equal(1.0, System.Math.Sqrt(x * x + y * y), 9);
        // adjacent hexes share exactly two corner positions
        var a = HexGrid.HexToWorld(new HexCoordinate(0, 0));
        var b = HexGrid.HexToWorld(new HexCoordinate(1, 0));
        int shared = 0;
        foreach (var ca in HexGrid.CornerOffsets)
            foreach (var cb in HexGrid.CornerOffsets)
                if (System.Math.Abs(a.X + ca.X - (b.X + cb.X)) < 1e-9
                    && System.Math.Abs(a.Y + ca.Y - (b.Y + cb.Y)) < 1e-9)
                    shared++;
        Assert.Equal(2, shared);
    }
```

- [ ] **Step 2: Run** — `dotnet test --filter HexGridTests` — Expected: 4 new FAIL.

- [ ] **Step 3: Implement** — append to `HexGrid`:

```csharp
    private static readonly double Sqrt3 = Math.Sqrt(3.0);

    /// <summary>Flat-top unit-size world position of the hex center (spec §2.1).</summary>
    public static (double X, double Y) HexToWorld(HexCoordinate h) =>
        (1.5 * h.Q, Sqrt3 * (h.R + h.Q / 2.0));

    /// <summary>Inverse of HexToWorld: fractional axial, then cube rounding.</summary>
    public static HexCoordinate WorldToHex(double x, double y)
    {
        double q = x * (2.0 / 3.0);
        double r = y / Sqrt3 - q / 2.0;
        return CubeRound(q, r);
    }

    private static HexCoordinate CubeRound(double q, double r)
    {
        double s = -q - r;
        int rq = (int)Math.Round(q), rr = (int)Math.Round(r), rs = (int)Math.Round(s);
        double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;
        return new HexCoordinate(rq, rr);
    }

    /// <summary>Odd-q offset (display only, spec §2): col = q, odd columns shifted.</summary>
    public static (int Col, int Row) ToOffset(HexCoordinate h) =>
        (h.Q, h.R + (h.Q - (h.Q & 1)) / 2);

    public static HexCoordinate FromOffset(int col, int row) =>
        new(col, row - (col - (col & 1)) / 2);

    /// <summary>The six corner offsets of a flat-top unit hex, corner 0 due east,
    /// counter-clockwise 60° apart. Add to HexToWorld(hex) for mesh vertices —
    /// the atlas builds its triangulation from exactly these (single geometry
    /// authority, atlas spec §2).</summary>
    public static readonly (double X, double Y)[] CornerOffsets = BuildCorners();

    private static (double X, double Y)[] BuildCorners()
    {
        var corners = new (double X, double Y)[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3.0 * i;   // flat-top: corner 0 at 0°
            corners[i] = (Math.Cos(angle), Math.Sin(angle));
        }
        return corners;
    }
```

Note on `(q & 1)` with negative q in C#: `-3 & 1 == 1`, so odd negative columns
stagger identically to odd positive ones — the round-trip test covers negatives.

- [ ] **Step 4: Run** — `dotnet test --filter HexGridTests` — Expected: PASS (9 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: hex world transforms and odd-q offset conversions"`

---

### Task 3: Superhex clusters (CellOf / CellCenter / cell lattice)

**Files:**
- Modify: `src/Core/Galaxy/HexGrid.cs` (append members)
- Test: append to `tests/Core.Tests/Galaxy/HexGridTests.cs`

**Interfaces:**
- Produces: `const int CellRadius = 5;` (91-hex cells), `static HexCoordinate CellCenter(HexCoordinate cell)` — `cell.Q * A + cell.R * B` with basis `A = (11, -5)`, `B = (5, 6)`; `static HexCoordinate CellOf(HexCoordinate hex)` — which cell claims the hex (partition; algorithm below); cell-lattice adjacency reuses `Neighbors` on cell coords (the cluster lattice is itself axial).
- **Algorithm (CellOf):** fractional cell coords via the inverse basis matrix `i = (6q - 5r)/91`, `j = (5q + 11r)/91`; round both; if `Distance(hex, CellCenter(rounded)) <= 5` return it; otherwise return the unique candidate among the rounded cell's 6 lattice neighbors whose center is within distance 5. If none or several qualify, throw `InvalidOperationException` — the partition tests would catch a broken basis, and silent fallback would corrupt determinism.

- [ ] **Step 1: Write the failing tests** — append to `HexGridTests`:

```csharp
    [Fact]
    public void CellCenter_UsesThePinnedBasis()
    {
        Assert.Equal(new HexCoordinate(0, 0), HexGrid.CellCenter(new HexCoordinate(0, 0)));
        Assert.Equal(new HexCoordinate(11, -5), HexGrid.CellCenter(new HexCoordinate(1, 0)));
        Assert.Equal(new HexCoordinate(5, 6), HexGrid.CellCenter(new HexCoordinate(0, 1)));
        Assert.Equal(new HexCoordinate(16, 1), HexGrid.CellCenter(new HexCoordinate(1, 1)));
    }

    [Fact]
    public void CellOf_IsAPartition_EveryHexExactlyOneCell()
    {
        // every hex within a big disc maps to a cell whose center is within 5,
        // and CellOf(CellCenter(c)) == c
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 30))
        {
            var cell = HexGrid.CellOf(hex);
            Assert.True(HexGrid.Distance(hex, HexGrid.CellCenter(cell)) <= HexGrid.CellRadius,
                $"hex {hex} claimed by cell {cell} but is too far from its center");
        }
        foreach (var cell in HexGrid.Spiral(new HexCoordinate(0, 0), 3))
            Assert.Equal(cell, HexGrid.CellOf(HexGrid.CellCenter(cell)));
    }

    [Fact]
    public void EveryCell_HasExactly91Hexes()
    {
        // count membership by brute force over a region that fully contains cell (0,0)
        // and its 6 neighbors
        var counts = new Dictionary<HexCoordinate, int>();
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 22))
        {
            var cell = HexGrid.CellOf(hex);
            counts[cell] = counts.TryGetValue(cell, out var v) ? v + 1 : 1;
        }
        // cells fully inside the radius-22 disc must have exactly 91 members
        foreach (var cell in HexGrid.Spiral(new HexCoordinate(0, 0), 1))
            Assert.Equal(91, counts[cell]);
    }
```

- [ ] **Step 2: Run** — `dotnet test --filter HexGridTests` — Expected: 3 new FAIL.

- [ ] **Step 3: Implement** — append to `HexGrid`:

```csharp
    /// <summary>Cells are radius-5 superhex clusters: exactly 91 hexes (spec §3).</summary>
    public const int CellRadius = 5;

    // Cluster lattice basis (determinant 91): A = (11,-5), B = (5,6).
    private const int AQ = 11, AR = -5, BQ = 5, BR = 6;

    public static HexCoordinate CellCenter(HexCoordinate cell) =>
        new(cell.Q * AQ + cell.R * BQ, cell.Q * AR + cell.R * BR);

    public static HexCoordinate CellOf(HexCoordinate hex)
    {
        // Inverse basis: [i]   1/91 [ BR -BQ ] [q]   =  (6q - 5r)/91, (5q + 11r)/91
        //                [j] =      [-AR  AQ ] [r]
        double i = (6.0 * hex.Q - 5.0 * hex.R) / 91.0;
        double j = (5.0 * hex.Q + 11.0 * hex.R) / 91.0;
        var candidate = new HexCoordinate((int)Math.Round(i), (int)Math.Round(j));
        if (Distance(hex, CellCenter(candidate)) <= CellRadius) return candidate;

        HexCoordinate? found = null;
        foreach (var neighborCell in Neighbors(candidate))
            if (Distance(hex, CellCenter(neighborCell)) <= CellRadius)
            {
                if (found != null)
                    throw new InvalidOperationException(
                        $"hex {hex} claimed by two cells — cluster basis broken");
                found = neighborCell;
            }
        return found ?? throw new InvalidOperationException(
            $"hex {hex} claimed by no cell — cluster basis broken");
    }
```

- [ ] **Step 4: Run** — `dotnet test --filter HexGridTests` — Expected: PASS (12 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: 91-hex superhex cluster assignment"`

---
### Task 4: GalaxyConfig + origin-centered DensityField

**Files:**
- Modify: `src/Core/Galaxy/GalaxyConfig.cs`, `src/Core/Galaxy/DensityField.cs`
- Test: replace `tests/Core.Tests/Galaxy/DensityFieldTests.cs`; fix `GalaxyPresenceTests` fixture
- Note: this task breaks compilation of skeleton/sim/inspector code that used `SizeSectors`/`CellsX` — those call sites are rewritten in Tasks 5–10. To keep the build green task-by-task, this task ALSO applies the mechanical fixes listed in Step 4.

**Interfaces:**
- Produces:
  - `GalaxyConfig`: `int GalaxyRadiusCells = 21` and `double HomeworldRatePerCell = 0.02` **replace** `SizeSectors` and `HomeworldRatePerSector`; `WidthHexes/HeightHexes/CellsX/CellsY` are **deleted**. Everything else unchanged.
  - `DensityField`: `double At(GalaxyConfig config, HexCoordinate hex)` (0 outside the galaxy); `bool InGalaxy(GalaxyConfig config, HexCoordinate hex)` — `HexGrid.Distance(HexGrid.CellOf(hex), origin) <= GalaxyRadiusCells`; `double WorldRimRadius(GalaxyConfig config)` — `|HexToWorld(CellCenter((GalaxyRadiusCells + 1, 0)))|`; `double ShapeAt(GalaxyConfig config, double nx, double ny)` — **body unchanged from today** (it already takes normalized coordinates; only its callers change).

- [ ] **Step 1: Write the failing tests.** Replace `tests/Core.Tests/Galaxy/DensityFieldTests.cs` with:

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class DensityFieldTests
{
    private static GalaxyConfig Config(ulong seed = 42) => new() { MasterSeed = seed };

    [Fact]
    public void At_IsDeterministic_AndBounded()
    {
        var config = Config();
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 40).Where((_, i) => i % 7 == 0))
        {
            var v = DensityField.At(config, hex);
            Assert.Equal(v, DensityField.At(config, hex));
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void OutsideGalaxy_IsZero_AndNotInGalaxy()
    {
        var config = Config();   // radius 21 cells -> rim well inside |q| ~ 250
        var far = new HexCoordinate(400, 0);
        Assert.False(DensityField.InGalaxy(config, far));
        Assert.Equal(0.0, DensityField.At(config, far));
        Assert.True(DensityField.InGalaxy(config, new HexCoordinate(0, 0)));
    }

    [Fact]
    public void Core_IsDenserThanMidDisc()
    {
        var config = Config();
        double Avg(HexCoordinate center) =>
            HexGrid.Spiral(center, 6).Average(h => DensityField.At(config, h));
        // mid-disc reference: a hex roughly 60% of the way to the rim along +q
        int midQ = (int)(0.6 * 2.0 / 3.0 * DensityField.WorldRimRadius(config));
        double coreAvg = Avg(new HexCoordinate(0, 0));
        double midAvg = Avg(new HexCoordinate(midQ, -midQ / 2));
        Assert.True(coreAvg > midAvg, $"core {coreAvg:F3} should exceed mid-disc {midAvg:F3}");
    }

    [Fact]
    public void MeanInsideDisc_NearTarget()
    {
        var config = Config();
        double rim = DensityField.WorldRimRadius(config);
        double sum = 0; int count = 0;
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 230).Where((_, i) => i % 16 == 0))
        {
            var (wx, wy) = HexGrid.HexToWorld(hex);
            if (System.Math.Sqrt(wx * wx + wy * wy) > 0.9 * rim) continue;
            if (!DensityField.InGalaxy(config, hex)) continue;
            sum += DensityField.At(config, hex);
            count++;
        }
        Assert.True(count > 3000, $"sample too small: {count}");
        Assert.InRange(sum / count, config.MeanDensityTarget - 0.12, config.MeanDensityTarget + 0.12);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter DensityFieldTests` — Expected: FAIL (API missing).

- [ ] **Step 3: Implement.** `src/Core/Galaxy/GalaxyConfig.cs` becomes:

```csharp
namespace StarGen.Core.Galaxy;

/// <summary>Generation input: identity + tuning knobs (spec §3). Recorded in artifact stamps.</summary>
public sealed class GalaxyConfig
{
    public ulong MasterSeed { get; set; }
    /// <summary>Radius of the hexagonal cell-lattice disc (spec §4). Default 21
    /// -> 1,387 cells x 91 hexes ~ 126k hexes (~ the old 100-sector galaxy).</summary>
    public int GalaxyRadiusCells { get; set; } = 21;
    public double MeanDensityTarget { get; set; } = 0.5;
    public int ArmCount { get; set; } = 3;
    public double ArmTightness { get; set; } = 0.35;
    public double ArmWidth { get; set; } = 0.18;
    public int EpochCount { get; set; } = 12;
    public int YearsPerEpoch { get; set; } = 50;
    public double HomeworldRatePerCell { get; set; } = 0.02;     // ~28 polities at radius 21
    public double TraversabilityThreshold { get; set; } = 0.25;
}
```

`src/Core/Galaxy/DensityField.cs`: keep `ShapeAt` byte-identical (including the tuned
`0.45` disc weight); replace the rest of the class with:

```csharp
    private static readonly HexCoordinate Origin = new(0, 0);

    public static bool InGalaxy(GalaxyConfig config, HexCoordinate hex) =>
        HexGrid.Distance(HexGrid.CellOf(hex), Origin) <= config.GalaxyRadiusCells;

    /// <summary>World radius used to normalize the shape function: one cell ring
    /// beyond the lattice, so density falls smoothly toward the membership rim.</summary>
    public static double WorldRimRadius(GalaxyConfig config)
    {
        var (x, y) = HexGrid.HexToWorld(
            HexGrid.CellCenter(new HexCoordinate(config.GalaxyRadiusCells + 1, 0)));
        return Math.Sqrt(x * x + y * y);
    }

    public static double At(GalaxyConfig config, HexCoordinate hex)
    {
        if (!InGalaxy(config, hex)) return 0.0;

        var (wx, wy) = HexGrid.HexToWorld(hex);
        double rim = WorldRimRadius(config);
        double shape = ShapeAt(config, wx / rim, wy / rim);
        if (shape <= 0) return 0.0;

        double noise = ValueNoise.Warped(config.MasterSeed,
            RollChannel.NoiseDensityLattice, RollChannel.NoiseWarpLattice,
            wx, wy, octaves: 3, frequency: 0.02, warpStrength: 30.0);

        double v = shape * (0.25 + 1.5 * noise);
        v *= config.MeanDensityTarget / 0.5;
        return Math.Clamp(v, 0.0, 1.0);
    }
```

(Noise now samples **world coordinates**; frequency 0.02 and warp 30 keep clump
sizes roughly the old scale since a hex spans ~1.5–1.7 world units.)

**Tuning clause:** if `MeanInsideDisc_NearTarget` fails, adjust ONLY noise
`frequency` (0.015–0.03) and/or the `ShapeAt` disc weight (currently 0.45) until it
passes; record final values. If `Core_IsDenserThanMidDisc` fails, the mid-disc probe
may sit on an arm ridge — move the probe's `midQ` fraction from 0.6 to 0.55 or 0.65
(test-side, comment why). Never widen bands.

- [ ] **Step 4: Mechanical unblocking of downstream files** (logic rewrites come in
their own tasks; these edits only keep the build green):
  - `SkeletonBuilder`, `GalaxySkeleton`, `EpochSim`, `RegionContext`, `SkeletonSerializer`, `Repl`, `StatsReport`, `GalaxyMapView`, `GalaxyMapSpike`: where they referenced `config.SizeSectors`/`CellsX`/`CellsY`/`WidthHexes`/`HeightHexes`, substitute temporary equivalents that compile: `GalaxyRadiusCells` where a size knob is meant; for lattice iteration/indexing that genuinely needs the old rectangular model, mark the block with `#warning HEXMIGRATION` and substitute `HexGrid`-based equivalents *if trivial*, otherwise leave the block compiling but logically wrong — its own task replaces it. All `#warning HEXMIGRATION` markers MUST be gone by end of Task 10 (Task 10 Step 6 greps for them).
  - `GalaxyPresenceTests`: fixture `new GalaxyConfig { MasterSeed = 42 }`; "corners empty" assertions become `Assert.True(Generator.Generate(galaxy, new HexCoordinate(400, 0)).IsEmpty)`; the core-density cluster centers on (0,0).
  - Expect some Galaxy suite tests to fail at this point (skeleton/sim still rectangular-wrong): run `dotnet test --filter "DensityFieldTests|GalaxyPresenceTests|HexGridTests"` — these MUST pass; note the full-suite failure count in the commit body (the only allowed red window in this plan; Tasks 5–9 burn it down; full suite green is required from Task 9 on).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: origin-centered hexagonal galaxy config and density field"`

---

### Task 5: GalaxySkeleton hex lattice + seeding passes 1–2

**Files:**
- Modify: `src/Core/Galaxy/RegionCell.cs`, `src/Core/Galaxy/GalaxySkeleton.cs`, `src/Core/Galaxy/SkeletonBuilder.cs` (passes 1–2 only)
- Test: replace fixtures + passes-1/2 tests in `tests/Core.Tests/Galaxy/SeedingPassTests.cs`, `SkeletonModelTests.cs`

**Interfaces:**
- Produces:
  - `RegionCell`: `int Q, R` (replace `Cx/Cy`), computed `HexCoordinate Coord => new(Q, R)`, `int SpiralIndex { get; set; }` (position in the skeleton's spiral list — the determinism ordering key, replaces `LinearIndex(config)`); all other fields unchanged.
  - `GalaxySkeleton`: ctor builds `List<RegionCell> Cells` by enumerating `HexGrid.Spiral(origin, config.GalaxyRadiusCells)` over CELL-lattice coords (SpiralIndex = list position) plus a private `Dictionary<HexCoordinate, RegionCell>`; `RegionCell CellAt(HexCoordinate cellCoord)` (throws KeyNotFound for outside — callers guard); `bool TryGetCell(HexCoordinate cellCoord, out RegionCell cell)`; `RegionCell CellForHex(HexCoordinate hex)` => `CellAt(HexGrid.CellOf(hex))`. `Cells` type changes from array to `IReadOnlyList<RegionCell>`.
  - `SkeletonBuilder.PassDensitySummary`: per cell, sample `HexGrid.Spiral(CellCenter(cell.Coord), 5)` at even spiral indices (46 samples) through `DensityField.At`; `IsVoid` as before. `MarkChokepoints`: same articulation algorithm, node index = `SpiralIndex`, edges = cell-lattice `HexGrid.Neighbors` filtered through `TryGetCell`.
  - `PassStellarPopulation`: noise sampled at `HexGrid.HexToWorld(CellCenter(cell.Coord))` (world coords), same thresholds/frequencies as today (0.02 / 0.015).

- [ ] **Step 1: Update tests.** In `SkeletonModelTests.cs` replace the lookup test:

```csharp
    [Fact]
    public void Skeleton_CellLookups_Work()
    {
        var config = new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 };
        var skeleton = new GalaxySkeleton(config);
        Assert.Equal(3 * 3 * 4 + 1, skeleton.Cells.Count);   // 37 cells
        Assert.Equal(0, skeleton.Cells[0].SpiralIndex);
        Assert.Equal(new HexCoordinate(0, 0), skeleton.Cells[0].Coord);
        var cell = skeleton.CellAt(new HexCoordinate(2, -1));
        Assert.Equal(2, cell.Q);
        Assert.Equal(-1, cell.R);
        // any hex inside that cell's cluster maps back to it
        var member = HexGrid.CellCenter(new HexCoordinate(2, -1));
        Assert.Same(cell, skeleton.CellForHex(member));
        Assert.False(skeleton.TryGetCell(new HexCoordinate(99, 0), out _));
    }
```

(keep `RegionCell_Defaults`, updating `Cx = 1, Cy = 2` initializers to `Q = 1, R = 2`).
In `SeedingPassTests.cs` change the shared fixture to:

```csharp
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 8 });
```

and keep the pass-1/2 tests (`Build_IsDeterministic`, `DensitySummary_HasStructure`,
`Chokepoints_AreNonVoid_AndScarcerThanOrdinaryCells`,
`StellarPopulation_AllLeansOccur_AndBalancedDominates`, `Metallicity_IsBounded_AndVaries`)
with one mechanical change: `s.Cells.Length` → `s.Cells.Count`.

- [ ] **Step 2: Run** — `dotnet test --filter "SkeletonModelTests|SeedingPassTests"` — Expected: FAIL.

- [ ] **Step 3: Implement.** `RegionCell.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

public enum StellarLean { Balanced, YoungBright, OldDim, RemnantGraveyard }

public sealed class RegionCell
{
    public int Q { get; set; }
    public int R { get; set; }
    public HexCoordinate Coord => new(Q, R);
    /// <summary>Position in the skeleton's spiral enumeration — the determinism
    /// ordering key (replaces the rectangular linear index).</summary>
    public int SpiralIndex { get; set; }
    public double MeanDensity { get; set; }
    public bool IsVoid { get; set; }
    public bool IsChokepoint { get; set; }
    public StellarLean Lean { get; set; }
    public double Metallicity { get; set; }
    public List<Anchor> Anchors { get; } = new();
    public int OwnerPolityId { get; set; } = -1;
    public int DevelopmentTier { get; set; }
    public bool Contested { get; set; }
    public bool WarScarred { get; set; }
}
```

`GalaxySkeleton.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The persisted Tier 2 artifact root (spec §3.1). Cells live on a hex
/// lattice of radius GalaxyRadiusCells, in deterministic spiral order.</summary>
public sealed class GalaxySkeleton
{
    public const int SchemaVersion = 2;

    public GalaxyConfig Config { get; }
    public IReadOnlyList<RegionCell> Cells => _cells;
    public List<SpeciesProfile> Species { get; } = new();
    public List<Polity> Polities { get; } = new();
    public List<GalaxyEvent> Events { get; } = new();

    private readonly List<RegionCell> _cells = new();
    private readonly Dictionary<HexCoordinate, RegionCell> _byCoord = new();

    public GalaxySkeleton(GalaxyConfig config)
    {
        Config = config;
        foreach (var coord in HexGrid.Spiral(new HexCoordinate(0, 0), config.GalaxyRadiusCells))
        {
            var cell = new RegionCell { Q = coord.Q, R = coord.R, SpiralIndex = _cells.Count };
            _cells.Add(cell);
            _byCoord[coord] = cell;
        }
    }

    public RegionCell CellAt(HexCoordinate cellCoord) => _byCoord[cellCoord];

    public bool TryGetCell(HexCoordinate cellCoord, out RegionCell cell) =>
        _byCoord.TryGetValue(cellCoord, out cell!);

    public RegionCell CellForHex(HexCoordinate hex) => CellAt(HexGrid.CellOf(hex));
}
```

(Note: SchemaVersion moves to 2 here; the serializer task rewrites the format to
match — until Task 9 the serializer tests are part of the allowed red window.)

`SkeletonBuilder` passes 1–2 (replace the corresponding methods; `Build` unchanged
in structure):

```csharp
    internal static void PassDensitySummary(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            double sum = 0; int n = 0;
            int i = 0;
            foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius))
            {
                if (i++ % 2 != 0) continue;      // 46 of 91 hexes
                sum += DensityField.At(config, hex);
                n++;
            }
            cell.MeanDensity = sum / n;
            cell.IsVoid = cell.MeanDensity < config.TraversabilityThreshold;
        }
        MarkChokepoints(s);
    }

    private static void MarkChokepoints(GalaxySkeleton s)
    {
        int n = s.Cells.Count;
        int[] disc = new int[n], low = new int[n], parent = new int[n];
        bool[] visited = new bool[n], articulation = new bool[n];
        for (int i = 0; i < n; i++) parent[i] = -1;
        int timer = 0;

        IEnumerable<int> Neighbors(int idx)
        {
            foreach (var neighborCoord in HexGrid.Neighbors(s.Cells[idx].Coord))
                if (s.TryGetCell(neighborCoord, out var neighbor) && !neighbor.IsVoid)
                    yield return neighbor.SpiralIndex;
        }
        // ... the DFS body is IDENTICAL to the current implementation (explicit
        // stack, low-link, root-children rule) — keep it byte-for-byte, only the
        // Neighbors local function above changes.
    }
```

(The comment in the snippet is instructional: the existing DFS body is preserved
verbatim; only the neighbor enumeration is replaced. Do not retype the DFS.)

`PassStellarPopulation`: replace the two sample-coordinate lines with

```csharp
            var (wx, wy) = HexGrid.HexToWorld(HexGrid.CellCenter(cell.Coord));
            double stellar = ValueNoise.Sample(config.MasterSeed,
                RollChannel.NoiseStellarLattice, wx, wy, 2, 0.02);
            // ... thresholds unchanged ...
            cell.Metallicity = ValueNoise.Sample(config.MasterSeed,
                RollChannel.NoiseMetalLattice, wx, wy, 2, 0.015);
```

Passes 3–4 (anchors/homeworlds) will not compile against the new types yet — apply
the same `#warning HEXMIGRATION` + minimal mechanical substitution rule from Task 4
(cell coords via `cell.Coord`, `PickAnchorHex` temporarily using spiral index 0 —
Task 6 rewrites them properly and removes the markers).

- [ ] **Step 4: Run** — `dotnet test --filter "SkeletonModelTests|SeedingPassTests"` — Expected: pass-1/2 + model tests PASS (anchor/homeworld tests may still be red — they're Task 6's). Note the failure set in the commit body.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: hex-lattice skeleton with spiral ordering and hex-aware passes 1-2"`

---
### Task 6: Anchors + homeworlds on the hex lattice

**Files:**
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (passes 3–4), `src/Core/Galaxy/Polity.cs`
- Test: update anchor/homeworld tests in `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Consumes: `HexGrid.Spiral/Distance/Neighbors`, `RegionCell.Coord/SpiralIndex`, `skeleton.TryGetCell`.
- Produces:
  - `Polity`: `CapitalCx/CapitalCy` → `int CapitalQ, CapitalR` + computed `HexCoordinate CapitalCoord => new(CapitalQ, CapitalR)`.
  - `PickAnchorHex(s, cell, drawIndex)`: draws `local = ctx.NextInt(AnchorPlacement, 0, 91, drawIndex)` (upper bound 80 → **91**), maps through the cell's materialized spiral (`HexGrid.Spiral(CellCenter(cell.Coord), 5)` → list, index `(local + probe) % 91`), forward-probe collision rule unchanged. Cell-keyed `RollContext` uses `cell.Coord` (was `(Cx, Cy)` — same channel, new coordinate values: expected output change).
  - `PassHomeworlds`: target = `Math.Max(2, (int)Math.Round(config.HomeworldRatePerCell * s.Cells.Count))`; spacing = `HexGrid.Distance(cell.Coord, polity.CapitalCoord) >= minSpacing` with `minSpacing = Math.Max(2, config.GalaxyRadiusCells / Math.Max(1, (int)Math.Ceiling(Math.Sqrt(target))))`; candidate ordering `OrderBy(hash).ThenBy(SpiralIndex)`; `NeighborhoodHasPrecursor` walks `HexGrid.Neighbors(cell.Coord)` + the cell itself via `TryGetCell`.
- All `#warning HEXMIGRATION` markers in passes 3–4 are removed by this task.

- [ ] **Step 1: Update tests.** In `SeedingPassTests`, replace the anchor placement containment assertions (old rectangular in-cell ranges) and the homeworld tests:

```csharp
    [Fact]
    public void Anchors_ArePlaced_OnePerHex_InsideTheirCell()
    {
        var s = Build();
        var all = s.Cells.SelectMany(c => c.Anchors.Select(a => (c, a))).ToList();
        Assert.True(all.Count(x => x.a.Type == AnchorType.MineralRich) > 5, "mineral anchors should exist");
        Assert.Contains(all, x => x.a.Type == AnchorType.PrecursorSite);
        var hexes = all.Select(x => x.a.Hex).ToList();
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        foreach (var (c, a) in all)
            Assert.True(HexGrid.Distance(a.Hex, HexGrid.CellCenter(c.Coord)) <= HexGrid.CellRadius,
                $"anchor at {a.Hex} outside cell {c.Coord}");
    }

    [Fact]
    public void Homeworlds_CountAndSpacing()
    {
        var s = Build();
        int expected = System.Math.Max(2, (int)System.Math.Round(
            s.Config.HomeworldRatePerCell * s.Cells.Count));
        Assert.InRange(s.Polities.Count, 2, expected);
        Assert.True(s.Polities.Count >= expected / 2, $"got {s.Polities.Count}, want >= {expected / 2}");
        foreach (var a in s.Polities)
            foreach (var b in s.Polities)
                if (a.Id != b.Id)
                    Assert.True(HexGrid.Distance(a.CapitalCoord, b.CapitalCoord) >= 2,
                        "capitals must not be adjacent on the cell lattice");
    }
```

`MineralAnchors_FollowMetallicity` is unchanged. `Homeworlds_HaveSpeciesAnchorsAndOwnership` changes only `s.CellAt(polity.CapitalCx, polity.CapitalCy)` → `s.CellAt(polity.CapitalCoord)`.

- [ ] **Step 2: Run** — `dotnet test --filter SeedingPassTests` — Expected: anchor/homeworld tests FAIL.

- [ ] **Step 3: Implement.** `Polity.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Registry entry. Extinct polities are retained, flagged (spec §7 lifecycle).</summary>
public sealed class Polity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SpeciesId { get; set; }
    public int CapitalQ { get; set; }
    public int CapitalR { get; set; }
    public HexCoordinate CapitalCoord => new(CapitalQ, CapitalR);
    public bool Extinct { get; set; }
}
```

In `SkeletonBuilder`, rewrite `PickAnchorHex` and the pass-3/4 bodies:

```csharp
    /// <summary>Deterministic in-cell hex pick over the cell's 91-hex spiral,
    /// with forward-probe collision handling (one anchor per hex).</summary>
    internal static HexCoordinate PickAnchorHex(GalaxySkeleton s, RegionCell cell, int drawIndex)
    {
        var ctx = new RollContext(s.Config.MasterSeed, cell.Coord);
        var members = new List<HexCoordinate>(
            HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius));
        int local = ctx.NextInt(RollChannel.AnchorPlacement, 0, members.Count, drawIndex);
        for (int probe = 0; probe < members.Count; probe++)
        {
            var hex = members[(local + probe) % members.Count];
            bool taken = false;
            foreach (var a in cell.Anchors)
                if (a.Hex.Equals(hex)) { taken = true; break; }
            if (!taken) return hex;
        }
        return members[0];   // unreachable: a cell never carries 91 anchors
    }
```

`PassResourceAnchors`: only two changes — the cell-keyed context becomes
`new RollContext(config.MasterSeed, cell.Coord)`, and everything else (chance
formulas, draw indexes 0/1, void gating) stays byte-identical.

`PassHomeworlds` / `RollSpecies` / `FitsCell` / `NeighborhoodHasPrecursor`:

```csharp
    internal static void PassHomeworlds(GalaxySkeleton s)
    {
        var config = s.Config;
        int target = Math.Max(2, (int)Math.Round(config.HomeworldRatePerCell * s.Cells.Count));
        int minSpacing = Math.Max(2, config.GalaxyRadiusCells
            / Math.Max(1, (int)Math.Ceiling(Math.Sqrt(target))));

        var candidates = s.Cells.Where(c => !c.IsVoid)
            .Select(c => (cell: c,
                order: new RollContext(config.MasterSeed, c.Coord)
                    .NextDouble(RollChannel.HomeworldPlacement)))
            .OrderBy(t => t.order).ThenBy(t => t.cell.SpiralIndex)
            .Select(t => t.cell);

        foreach (var cell in candidates)
        {
            if (s.Polities.Count >= target) break;
            bool tooClose = s.Polities.Any(p =>
                HexGrid.Distance(p.CapitalCoord, cell.Coord) < minSpacing);
            if (tooClose) continue;

            int id = s.Polities.Count;
            var species = RollSpecies(s, cell, id);
            s.Species.Add(species);
            s.Polities.Add(new Polity
            {
                Id = id, Name = species.Name, SpeciesId = id,
                CapitalQ = cell.Q, CapitalR = cell.R,
            });
            cell.Anchors.Add(new Anchor
            {
                Type = AnchorType.Homeworld, Hex = PickAnchorHex(s, cell, 2), SpeciesId = id,
            });
            cell.OwnerPolityId = id;
            cell.DevelopmentTier = 2;
        }
    }

    private static bool NeighborhoodHasPrecursor(GalaxySkeleton s, RegionCell cell)
    {
        if (cell.Anchors.Any(a => a.Type == AnchorType.PrecursorSite)) return true;
        foreach (var neighborCoord in HexGrid.Neighbors(cell.Coord))
            if (s.TryGetCell(neighborCoord, out var neighbor)
                && neighbor.Anchors.Any(a => a.Type == AnchorType.PrecursorSite))
                return true;
        return false;
    }
```

(`RollSpecies` and `FitsCell` change only their `RollContext` construction to
`cell.Coord` — embodiment weights, temperament formula, hive rule, species naming
all byte-identical.)

- [ ] **Step 4: Run** — `dotnet test --filter "SeedingPassTests|SkeletonModelTests|HexGridTests|DensityFieldTests"` — Expected: PASS. Grep `#warning HEXMIGRATION` in `SkeletonBuilder.cs` — none remain.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: anchor and homeworld seeding on the hex lattice"`

---

### Task 7: EpochSim 6-neighbor adjacency

**Files:**
- Modify: `src/Core/Galaxy/EpochSim.cs`, `src/Core/Galaxy/GalaxyEvent.cs`
- Test: update `tests/Core.Tests/Galaxy/EpochSimTests.cs`

**Interfaces:**
- Produces:
  - `GalaxyEvent`: `Cx/Cy` → `int Q, R` (event location cell coord).
  - `EpochSim`: `Adjacent(s, cell)` yields existing cells at `HexGrid.Neighbors(cell.Coord)` via `TryGetCell`; all determinism orderings switch `LinearIndex(config)` → `SpiralIndex`; the per-polity `RollContext` keys on `polity.CapitalCoord`; capital relocation writes `CapitalQ/CapitalR`. Expansion/development/war formulas, budgets, channel keying (`SimDevelopment(epoch, cell.SpiralIndex)`, `SimWar(epoch, polity.Id)`), event emission, lifecycle — all byte-identical otherwise.

- [ ] **Step 1: Update tests.** In `EpochSimTests`: fixture becomes `GalaxyRadiusCells = 8`; `a.Cells.Length` → `a.Cells.Count` and index-parallel comparisons stay (spiral order is deterministic); `EventLog_IsChronological_AndReferentiallyIntact` bounds-check becomes:

```csharp
            Assert.True(s.TryGetCell(new HexCoordinate(e.Q, e.R), out _),
                $"event references cell ({e.Q},{e.R}) outside the lattice");
```

`ClaimedFraction_AtReferenceConfig_IsWithinAcceptanceBand` keeps its [0.2, 0.85] band **unchanged** — this is the character guard for the whole conversion.

- [ ] **Step 2: Run** — `dotnet test --filter EpochSimTests` — Expected: FAIL (compile + adjacency).

- [ ] **Step 3: Implement.** `GalaxyEvent.cs` — rename `Cx/Cy` to `Q/R` (types/comments unchanged). In `EpochSim.cs`:

```csharp
    private static RollContext Ctx(GalaxySkeleton s, Polity p) =>
        new(s.Config.MasterSeed, p.CapitalCoord);

    private static IEnumerable<RegionCell> Adjacent(GalaxySkeleton s, RegionCell cell)
    {
        foreach (var neighborCoord in HexGrid.Neighbors(cell.Coord))
            if (s.TryGetCell(neighborCoord, out var neighbor))
                yield return neighbor;
    }
```

Then mechanically: every `.LinearIndex(s.Config)` → `.SpiralIndex`; every event
construction `Cx = target.Cx, Cy = target.Cy` → `Q = target.Q, R = target.R`;
capital relocation `defender.CapitalCx = remaining.Cx; defender.CapitalCy = remaining.Cy;`
→ `defender.CapitalQ = remaining.Q; defender.CapitalR = remaining.R;`;
capital-fall check `defender.CapitalCx == target.Cx && defender.CapitalCy == target.Cy`
→ `defender.CapitalCoord.Equals(target.Coord)`. Formulas untouched.

- [ ] **Step 4: Run** — `dotnet test --filter EpochSimTests` — Expected: PASS (6 tests) including the claimed-fraction band. **Tuning clause:** if the band fails (hex adjacency yields 6 frontier options vs 4 — faster spread is plausible), adjust ONLY the expansion budget constant (`2`, direction: down to no less than 1.0) and/or `EpochCount` default is NOT a knob here — budget only. Record the final constant. Never touch the band.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: 6-neighbor epoch sim on the hex lattice"`

---
### Task 8: RegionContext IDW smoothing + designation bias + integration regression

**Files:**
- Modify: `src/Core/Galaxy/RegionContext.cs`, `src/Core/Naming/Designation.cs`
- Test: update `tests/Core.Tests/Galaxy/RegionIntegrationTests.cs`, `tests/Core.Tests/Generation/PresenceTests.cs` (Designation_Format — the one allowed Phase 1 amendment)

**Interfaces:**
- Produces:
  - `Designation.For(hex)` → `$"SGC {hex.Q + 2048:D4}-{hex.R + 2048:D4}"` (spec §5: display bias keeps labels non-negative and stable-width; origin = `SGC 2048-2048`).
  - `RegionContext.For(galaxy, hex)`: membership check via `DensityField.InGalaxy`; cell via `skeleton.CellForHex`; `SettlementScale` = **inverse-distance weighting** over the hex's own cell + its up-to-6 lattice neighbors (spec §5): `weight = 1 / (1 + worldDistance(hexCenter, cellCenter))`, `scale = Σ(w·cellScale) / Σw`, `cellScale` per-cell formula unchanged (`1 + 0.8·dev` owned; `0.4` war-scarred unowned; else `1.0`). Star-type/belt modifiers and `AnchorAt` unchanged (own cell).

- [ ] **Step 1: Update tests.** `PresenceTests.Designation_Format`:

```csharp
    [Fact]
    public void Designation_Format() =>
        Assert.Equal("SGC 2060-2082", Designation.For(new HexCoordinate(12, 34)));
```

(the other three PresenceTests are coordinate-agnostic and stay untouched).
`RegionIntegrationTests`: fixture `GalaxyRadiusCells = 8`; `RemnantGraveyards_SkewTowardDeadStars` and `SettlementScale_RaisesSettlementInsidePolities` iterate cell members via `HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius)` (every hex, or every 2nd for the settlement test) instead of rectangular x/y loops; all other assertions byte-identical, including `Flatspace_RemainsBitIdentical_ToLegacy` (which must pass **unchanged** — flatspace never touches skeleton or designation-relative expectations since both sides print the same new designations).

- [ ] **Step 2: Run** — `dotnet test --filter "RegionIntegrationTests|PresenceTests"` — Expected: FAIL.

- [ ] **Step 3: Implement.** `Designation.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Core.Naming;

/// <summary>Catalog designation (spec §5): axial coords with a +2048 display bias
/// so labels stay non-negative and stable-width. Origin = SGC 2048-2048.</summary>
public static class Designation
{
    public static string For(HexCoordinate coord) =>
        $"SGC {coord.Q + 2048:D4}-{coord.R + 2048:D4}";
}
```

`RegionContext.For` and the smoothing method:

```csharp
    public static RegionContext? For(GalaxyContext galaxy, HexCoordinate hex)
    {
        if (galaxy.IsFlatspace || galaxy.Skeleton == null) return null;
        if (!DensityField.InGalaxy(galaxy.Config, hex)) return null;
        var s = galaxy.Skeleton;
        var cell = s.CellForHex(hex);

        var region = new RegionContext
        {
            StarTypeModifier = LeanModifier(cell.Lean),
            BeltModifier = k => k == BodyKind.PlanetoidBelt ? 0.5 + cell.Metallicity : 1.0,
            SettlementScale = SmoothedSettlementScale(s, hex, cell),
            OwnerPolityId = cell.OwnerPolityId,
            WarScarred = cell.WarScarred,
        };
        foreach (var anchor in cell.Anchors)
            if (anchor.Hex.Equals(hex)) { region.AnchorAt = anchor; break; }
        return region;
    }

    /// <summary>Inverse-distance weighting over the hex's own cell + its existing
    /// lattice neighbors (spec §5) — smoother than bilinear, no corner cases.</summary>
    private static double SmoothedSettlementScale(GalaxySkeleton s, HexCoordinate hex, RegionCell own)
    {
        var (hx, hy) = HexGrid.HexToWorld(hex);
        double weightSum = 0, scaleSum = 0;

        void Accumulate(RegionCell cell)
        {
            var (cx, cy) = HexGrid.HexToWorld(HexGrid.CellCenter(cell.Coord));
            double dist = Math.Sqrt((hx - cx) * (hx - cx) + (hy - cy) * (hy - cy));
            double weight = 1.0 / (1.0 + dist);
            double cellScale = cell.OwnerPolityId >= 0 ? 1.0 + 0.8 * cell.DevelopmentTier
                : cell.WarScarred ? 0.4 : 1.0;
            weightSum += weight;
            scaleSum += weight * cellScale;
        }

        Accumulate(own);
        foreach (var neighborCoord in HexGrid.Neighbors(own.Coord))
            if (s.TryGetCell(neighborCoord, out var neighbor))
                Accumulate(neighbor);
        return scaleSum / weightSum;
    }
```

(`LeanModifier` and the rest of the class are untouched. `Generator.cs` needs no
change — it already routes through `RegionContext.For` and `DensityField.At`.)

- [ ] **Step 4: Run** — `dotnet test` (FULL suite) — Expected: everything except serializer/golden tests green (those are Task 9's); list the exact remaining failures in the commit body. `Flatspace_RemainsBitIdentical_ToLegacy` and the whole Phase 1 suite (with the one amended designation test) MUST be green here.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: IDW settlement smoothing and biased axial designations"`

---

### Task 9: Serializer SchemaVersion 2 + golden re-freeze

**Files:**
- Modify: `src/Core/Galaxy/SkeletonSerializer.cs`
- Test: update `tests/Core.Tests/Galaxy/SerializerTests.cs`

**Interfaces:**
- Produces: format v2 — header `STARGEN-SKELETON|2`; `CONFIG|seed|radiusCells|meanDensity|armCount|armTightness|armWidth|epochs|yearsPerEpoch|homeworldRatePerCell|traversability`; `POLITY|id|name|speciesId|capQ|capR|extinct`; `CELL|q|r|meanDensity|isVoid|isChokepoint|lean|metallicity|owner|dev|contested|warScarred`; `ANCHOR|cellQ|cellR|type|hexQ|hexR|speciesId`; `EVENT|epoch|type|actor|target|q|r|magnitude`; SPECIES line unchanged. Cells written in spiral order (list order); `Load` reconstructs via `CellAt(new HexCoordinate(q, r))`. Malformed-input hardening (try/catch → `InvalidDataException`, record-before-CONFIG guard, TryParse header) carried over verbatim.

- [ ] **Step 1: Update tests.** In `SerializerTests`: fixture `GalaxyRadiusCells = 8`; `original.Cells.Length` → `.Count`; the tamper test string becomes `"STARGEN-SKELETON|2"` → `"STARGEN-SKELETON|999"`; golden test config becomes `new GalaxyConfig { MasterSeed = 7, GalaxyRadiusCells = 3 }` with header assertion `"STARGEN-SKELETON|2"` and the two golden literals **re-frozen from observation** (build once, record `s.Polities.Count` and `s.Events.Count`, replace the old literals `2`/`23`, and note both old→new values in the commit body — this is the plan's sanctioned one-time re-freeze).

- [ ] **Step 2: Run** — `dotnet test --filter SerializerTests` — Expected: FAIL.

- [ ] **Step 3: Implement.** In `Save`: emit the v2 CONFIG line

```csharp
        w.WriteLine(string.Join("|", "CONFIG",
            c.MasterSeed.ToString(Inv), c.GalaxyRadiusCells.ToString(Inv),
            c.MeanDensityTarget.ToString("R", Inv), c.ArmCount.ToString(Inv),
            c.ArmTightness.ToString("R", Inv), c.ArmWidth.ToString("R", Inv),
            c.EpochCount.ToString(Inv), c.YearsPerEpoch.ToString(Inv),
            c.HomeworldRatePerCell.ToString("R", Inv),
            c.TraversabilityThreshold.ToString("R", Inv)));
```

and the renamed fields per record (`p.CapitalQ/CapitalR`, `cell.Q/cell.R`,
`e.Q/e.R`); in `Load`, mirror the field indexes (CONFIG: `GalaxyRadiusCells =
int.Parse(f[2])`, `HomeworldRatePerCell = double.Parse(f[9])`; CELL resolves
`CellAt(new HexCoordinate(int.Parse(f[1]), int.Parse(f[2])))`; ANCHOR/EVENT/POLITY
likewise). Everything else — ordering, culture, "R" doubles, hardening — unchanged.

- [ ] **Step 4: Run** — `dotnet test` (FULL suite) — Expected: **all green, no
exceptions**. From this task onward the red window is closed; any failure is a
defect in Tasks 4–9, not migration debt.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: skeleton artifact schema v2 with re-frozen goldens"` (commit body: old→new golden values + full-suite count).

---
### Task 10: Inspector — flat-top rendering, spiral walk, spike retirement

**Files:**
- Create: `src/Core/Galaxy/GalaxyEnumerator.cs`
- Modify: `src/Inspector/GalaxyMapView.cs`, `src/Inspector/Repl.cs`, `src/Inspector/StatsReport.cs`
- Delete: `unity/Assets/Scripts/GalaxyMapSpike.cs`, `unity/Assets/Editor/StarGenSpikeMenu.cs` (+ their `.meta` files)
- Test: `tests/Core.Tests/Galaxy/GalaxyEnumeratorTests.cs` (new); manual REPL verification

**Interfaces:**
- Produces:
  - `static class GalaxyEnumerator` (Core): `HexCoordinate SpiralAt(int index)` — the hex at position `index` of the infinite spiral from the origin (index 0 = (0,0)); `int SpiralIndexOf(HexCoordinate hex)` — its inverse. Closed form: for `index > 0`, ring `d` satisfies `3d(d-1)+1 <= index < 3d(d+1)+1`; `pos = index - (3d(d-1)+1)`; `side = pos / d`, `step = pos % d`; hex = `origin + D4*d + Σ_{i<side} D_i*d + D_side*step` (matches `HexGrid.Ring`'s pinned order exactly). `SpiralIndexOf` computes `d = Distance(origin, hex)` and scans `Ring(origin, d)` for the position (O(6d), fine).
  - REPL walk = the infinite origin spiral (unifies flatspace and galaxy modes; beyond-rim hexes are simply empty, same as flatspace voids). `_x/_y` state is replaced by `int _spiralIndex`; `goto <q> <r>` sets it via `SpiralIndexOf`.
  - `GalaxyMapView.CellMap(skeleton, layer)` — offset-canvas rendering: for each cell, `(col, row) = HexGrid.ToOffset(cell.Coord)`; normalize by min col/row; glyph (doubled) at canvas position `[2*row + (col & 1)][col]` — a half-line stagger per odd column (flat-top idiom). `CellZoom(galaxy, cellCoord)` — same canvas technique over the cell's 91 member hexes with the existing empty/system/settled/anchored glyphs. `SectorMap` is **deleted** (sectors no longer exist).
  - `StatsReport.Build(GalaxyContext galaxy, int startIndex, int n)` — walks `SpiralAt(startIndex + i)`.
  - REPL: `sector` command removed; `cell <q> <r>` keeps its data dump + `CellZoom`; `galaxy <seed> [radiusCells]` (default 21); help text updated.

- [ ] **Step 1: Write the failing enumerator tests** — `tests/Core.Tests/Galaxy/GalaxyEnumeratorTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class GalaxyEnumeratorTests
{
    [Fact]
    public void SpiralAt_MatchesHexGridSpiral()
    {
        var expected = HexGrid.Spiral(new HexCoordinate(0, 0), 9).ToList();
        for (int i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], GalaxyEnumerator.SpiralAt(i));
    }

    [Fact]
    public void SpiralIndexOf_InvertsSpiralAt()
    {
        for (int i = 0; i < 500; i++)
            Assert.Equal(i, GalaxyEnumerator.SpiralIndexOf(GalaxyEnumerator.SpiralAt(i)));
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter GalaxyEnumeratorTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/GalaxyEnumerator.cs`:

```csharp
using System.Linq;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The infinite deterministic hex walk from the origin — the inspector's
/// linear position space (replaces row-major walks; galaxy and flatspace share it).</summary>
public static class GalaxyEnumerator
{
    public static HexCoordinate SpiralAt(int index)
    {
        if (index <= 0) return new HexCoordinate(0, 0);
        int d = 1;
        while (3 * d * (d + 1) + 1 <= index) d++;
        int pos = index - (3 * d * (d - 1) + 1);
        int side = pos / d, step = pos % d;

        var hex = new HexCoordinate(HexGrid.Directions[4].Q * d, HexGrid.Directions[4].R * d);
        for (int i = 0; i < side; i++)
            hex = new HexCoordinate(hex.Q + HexGrid.Directions[i].Q * d,
                                    hex.R + HexGrid.Directions[i].R * d);
        return new HexCoordinate(hex.Q + HexGrid.Directions[side].Q * step,
                                 hex.R + HexGrid.Directions[side].R * step);
    }

    public static int SpiralIndexOf(HexCoordinate hex)
    {
        var origin = new HexCoordinate(0, 0);
        int d = HexGrid.Distance(origin, hex);
        if (d == 0) return 0;
        int pos = HexGrid.Ring(origin, d).ToList().IndexOf(hex);
        return 3 * d * (d - 1) + 1 + pos;
    }
}
```

- [ ] **Step 4: Run** — `dotnet test --filter GalaxyEnumeratorTests` — Expected: PASS. Full suite green.

- [ ] **Step 5: Rewire the inspector.** `GalaxyMapView.cs` — replace `CellMap`'s
row loops and `SectorMap` with:

```csharp
    public static string CellMap(GalaxySkeleton s, string layer)
    {
        var offsets = s.Cells.Select(c => (cell: c, off: HexGrid.ToOffset(c.Coord))).ToList();
        int minCol = offsets.Min(t => t.off.Col), maxCol = offsets.Max(t => t.off.Col);
        int minRow = offsets.Min(t => t.off.Row), maxRow = offsets.Max(t => t.off.Row);
        int width = (maxCol - minCol + 1) * 2, height = (maxRow - minRow) * 2 + 2;
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) canvas[y, x] = ' ';

        foreach (var (cell, off) in offsets)
        {
            char glyph = CellChar(s, cell, layer);
            int col = off.Col - minCol, row = off.Row - minRow;
            int y = 2 * row + (off.Col & 1);          // odd columns drop half a line
            canvas[y, col * 2] = glyph;
            canvas[y, col * 2 + 1] = glyph;
        }

        var sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) sb.Append(canvas[y, x]);
            sb.AppendLine();
        }
        sb.AppendLine(Legend(s, layer));
        return sb.ToString();
    }

    public static string CellZoom(GalaxyContext galaxy, HexCoordinate cellCoord)
    {
        var members = HexGrid.Spiral(HexGrid.CellCenter(cellCoord), HexGrid.CellRadius)
            .Select(h => (hex: h, off: HexGrid.ToOffset(h))).ToList();
        int minCol = members.Min(t => t.off.Col), minRow = members.Min(t => t.off.Row);
        int width = (members.Max(t => t.off.Col) - minCol + 1) * 2;
        int height = (members.Max(t => t.off.Row) - minRow) * 2 + 2;
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) canvas[y, x] = ' ';

        var skeleton = galaxy.Skeleton;
        foreach (var (hex, off) in members)
        {
            bool anchored = skeleton != null &&
                skeleton.CellForHex(hex).Anchors.Any(a => a.Hex.Equals(hex));
            var system = Generator.Generate(galaxy, hex).System;
            char glyph = system == null ? '·'
                : anchored ? '@'
                : SystemIsSettled(system) ? 'o' : '*';
            int col = off.Col - minCol, row = off.Row - minRow;
            int y = 2 * row + (off.Col & 1);
            canvas[y, (col) * 2] = glyph;
            canvas[y, (col) * 2 + 1] = glyph;
        }

        var sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) sb.Append(canvas[y, x]);
            sb.AppendLine();
        }
        sb.AppendLine("·=empty *=system o=settled @=anchored");
        return sb.ToString();
    }
```

(`CellChar`/`Legend`/`SystemIsSettled` unchanged; delete `SectorMap` and the old
`HexMap` helper.) `Repl.cs`: `_x/_y` → `int _spiralIndex`; `Step(dir)` = clamp
`_spiralIndex + dir` at 0; `goto <q> <r>` → `_spiralIndex =
GalaxyEnumerator.SpiralIndexOf(new HexCoordinate(q, r))`; `Show()` generates at
`GalaxyEnumerator.SpiralAt(_spiralIndex)`; `find` steps the spiral (cap 50,000
unchanged); the `sector` case is deleted and `cell <q> <r>` calls
`GalaxyMapView.CellZoom(_galaxy!, new HexCoordinate(q, r))` after its data dump
(guard: cell exists via `TryGetCell`, else "cell out of range"); `galaxy <seed>
[radiusCells]` default 21; help updated to `map [layer] | cell <q> <r>`.
`StatsReport.Build(GalaxyContext galaxy, int startIndex, int n)` walks
`GalaxyEnumerator.SpiralAt(startIndex + i)`; REPL passes `_spiralIndex`.
Delete the two Unity spike scripts and their `.meta` files.

- [ ] **Step 6: Verify.** `grep -rn "HEXMIGRATION" src/ tests/` → no matches.
`dotnet test` → all green. Manual capture (in the report):
`printf 'galaxy 42 8\nmap\nmap polity\ncell 0 0\ngoto 3 -2\nnext\nstats 2000\nquit\n' | dotnet run --project src/Inspector`
Expected by eyeball: hexagonal (roughly round) galaxy outline with staggered
columns; polity blobs contiguous under 6-neighbor adjacency (less blocky than the
old square maps); `cell 0 0` prints core-cell data + a 91-hex staggered zoom;
`stats` presence well above the old rim-heavy walks (the spiral starts in the
dense core).

- [ ] **Step 7: Commit** — `git add -A && git commit -m "feat: hex-aware inspector with spiral walk; retire Unity spike"`

---

### Task 11: DESIGN.md hex-lattice language

**Files:**
- Modify: `docs/DESIGN.md`

- [ ] **Step 1: Apply two edits.**
1. In §3 (Data Model), replace the paragraph beginning `The galaxy/sector/subsector/hex spatial layer mirrors Traveller's canonical sector structure` with: `The spatial layer is hexagonal at every level (hex geometry spec): star hexes on an axial flat-top grid with 6-neighbor adjacency, aggregated into 91-hex hexagonal region cells whose centers form a coarser hex lattice, composing an origin-centered hexagonal galaxy disc (GalaxyRadiusCells, default 21 ≈ 126k hexes). The earlier Traveller sector/subsector convention is retired; the zoom ladder is galaxy → cell → hex.`
2. In §4 (Roadmap) phase 3, replace `**Sector/subsector map** — hex grid navigation, lazy per-hex generation, system summary icons, drill-down into system view. Proves on-demand generation + caching.` with `**Cell map** — hex-lattice navigation, lazy per-hex generation, system summary icons, drill-down into system view (delivered with the Unity atlas). Proves on-demand generation + caching.` and in its done-when, `panning a full sector (1,280 hexes)` becomes `panning a full cell neighborhood (~1,000 hexes)`.

- [ ] **Step 2: Verify + commit** — `dotnet test` green (habit); read the amended sections once. `git add -A && git commit -m "docs: hex-lattice spatial language in DESIGN.md"`

---

## Self-Review Notes

- **Spec coverage:** axial `HexCoordinate` + pinned flat-top constants (T1, spec §2), world/offset transforms (T2, §2.1), 91-hex clusters + basis + partition tests (T3, §3), `GalaxyRadiusCells` origin-centered disc + world-normalized density (T4, §4), spiral-ordered skeleton + 6-neighbor chokepoints + world-coord stellar noise (T5, §5), spiral anchors + lattice homeworld spacing (T6, §5), 6-neighbor sim + SpiralIndex ordering (T7, §5), IDW smoothing + designation bias + flatspace regression + the one sanctioned Phase 1 test amendment (T8, §5–6), schema v2 + hardened load + golden re-freeze (T9, §5–6), flat-top ASCII + unified spiral walk + spike retirement (T10, §5), DESIGN.md language (T11). HexGrid unit suite covers all §6 bullets (symmetry, metric, ring/spiral counts, world round-trip, partition/91-count).
- **The red window:** Tasks 4–8 legitimately leave later-task suites failing (marked `#warning HEXMIGRATION`, enumerated in commit bodies); Task 9 Step 4 closes it — full green is a hard gate there and at every step after. Reviewers of Tasks 4–8 should verify the *named* filters pass and the failure list is honest, not demand full green early.
- **Type consistency:** `cell.Coord`/`SpiralIndex`, `Polity.CapitalCoord`, `GalaxyEvent.Q/R`, `HexGrid.CellRadius`, `GalaxyEnumerator.SpiralAt/SpiralIndexOf` used uniformly across T5–T10; `Directions` order pinned once (T1) and referenced by T10's closed-form spiral.
- **Known accepted risks:** (a) claimed-fraction band under 6-neighbor expansion may need the budget-constant tune (T7 names the knob and floor); (b) density mean band may need frequency/disc-weight tuning (T4 names both); (c) `CellOf` throws on basis breakage rather than guessing — partition tests would catch it at T3 before anything consumes it; (d) REPL `stats` semantics shift (spiral from core vs old rim-heavy walk) — expected, noted in T10's manual verification.
