# Regional Generation Slice 1: Visible Galaxy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace homogeneous generation with a shaped, structured galaxy: density field (galactic shape × noise), skeleton seeding (chokepoints, stellar populations, anchors, homeworlds/species), the stage-1 epoch sim (expansion/development/war on flat budgets), per-hex integration, artifact serialization, and an ASCII galaxy atlas in the inspector.

**Architecture:** Three tiers per spec §3: pure density field functions (Tier 1), a `GalaxySkeleton` built by ordered seeding passes + the epoch sim and persisted as a versioned artifact (Tier 2), and per-hex reads via `RegionContext` modifier bundles + pre-commitments composed onto the existing Phase 1 pipeline (Tier 3). Legacy `Generate(seed, coord)` becomes a flatspace wrapper so all 46 existing tests survive unchanged.

**Tech Stack:** C# (LangVersion latest), StarGen.Core netstandard2.1 (zero packages), StarGen.Inspector + tests net10.0, xUnit.

## Global Constraints

- `src/Core` targets **netstandard2.1**, zero package references, no Unity/project references.
- Determinism absolute in Core: no `System.Random`, no `DateTime`, no iteration-order-dependent draws. Fixed iteration order in the sim: cells by linear index, polities by id (spec §7).
- `RollChannel` values are **stable constants — never renumber or reuse**; this plan appends channels 24–35 (spec §8 of the Phase 1 spec).
- Existing 46 tests must stay green after every task (flatspace back-compat, spec §8/§10).
- Skeleton artifact: versioned, serializable, deterministic — same `GalaxyConfig` → byte-identical serialized bytes (spec §3.1, §10).
- Pre-commitments are the only hard top-down channel; anchored hexes are excluded from the random overlay roll (spec §5, §8).
- Natural modifiers bias only star-type and belt/anchor weights — never direct body-kind/biosphere painting; political modifiers never touch nature rolls (spec §5 pass 2, §8).
- Model types remain plain mutable classes (no records). Serialization uses invariant culture.
- Commits: conventional style, each ending with the trailer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

**Slice boundary (spec §7.9):** sim stage 1 only — expansion, development, minimal war, flat budgets. NO military stockpiles, commodities, relations ladder, news, event→POI compiler, or world-state handoff (follow-up plans). The event log and its types exist now (the sim emits claim/war/extinction events) because stage 1 already produces history the atlas shows.

## File Structure

```
src/Core/Galaxy/GalaxyConfig.cs      # identity + tuning knobs (Task 1)
src/Core/Galaxy/ValueNoise.cs        # hash-based value noise + fbm + warp (Task 1)
src/Core/Galaxy/DensityField.cs      # shape × noise → [0,1] (Task 2)
src/Core/Galaxy/GalaxyContext.cs     # config + skeleton? + flatspace (Task 3)
src/Core/Galaxy/RegionCell.cs        # cell state (Task 4)
src/Core/Galaxy/Anchor.cs            # anchor vocabulary (Task 4)
src/Core/Galaxy/SpeciesProfile.cs    # embodiment + temperament (Task 4)
src/Core/Galaxy/Polity.cs            # polity registry entry (Task 4)
src/Core/Galaxy/GalaxyEvent.cs       # global event log record (Task 4)
src/Core/Galaxy/GalaxySkeleton.cs    # artifact root + indexes (Task 4)
src/Core/Galaxy/SkeletonBuilder.cs   # seeding passes 1-4 (Tasks 5-8)
src/Core/Galaxy/EpochSim.cs          # stage-1 loop (Task 9)
src/Core/Galaxy/SkeletonSerializer.cs# versioned text artifact (Task 10)
src/Core/Galaxy/RegionContext.cs     # per-hex modifier bundle + precommits (Task 11)
src/Core/Generation/Generator.cs     # galaxy-aware overload (Tasks 3, 11)
src/Core/Rng/RollChannel.cs          # channels 24-35 appended (Task 1)
src/Inspector/Repl.cs                # galaxy/cell/gsave/gload/map commands (Tasks 12-13)
src/Inspector/GalaxyMapView.cs       # ASCII map renderer (Task 13)
docs/DESIGN.md                       # spec §12 amendments (Task 14)
```

Coordinate conventions used throughout: a galaxy is `SizeSectors × SizeSectors` sectors; each sector is 32×40 hexes; each region cell is an 8×10-hex subsector, so the cell grid is `(SizeSectors*4) × (SizeSectors*4)`. `WidthHexes = SizeSectors*32`, `HeightHexes = SizeSectors*40`. Cell of hex = `(hex.X/8, hex.Y/10)`. Cell linear index = `cy * CellsX + cx`.

---
### Task 1: GalaxyConfig, new RollChannels, and hash-based value noise

**Files:**
- Create: `src/Core/Galaxy/GalaxyConfig.cs`, `src/Core/Galaxy/ValueNoise.cs`
- Modify: `src/Core/Rng/RollChannel.cs` (append members 24–35; change nothing existing)
- Test: `tests/Core.Tests/Galaxy/ValueNoiseTests.cs`

**Interfaces:**
- Consumes: `StableHash.Mix(ulong,ulong,ulong,ulong)`, `RollChannel`.
- Produces:
  - `sealed class GalaxyConfig` — properties (all `get; set;` with defaults): `ulong MasterSeed`, `int SizeSectors = 10`, `double MeanDensityTarget = 0.5`, `int ArmCount = 3`, `double ArmTightness = 0.35`, `double ArmWidth = 0.18`, `int EpochCount = 12`, `int YearsPerEpoch = 50`, `double HomeworldRatePerSector = 0.25`, `double TraversabilityThreshold = 0.25`; computed: `int WidthHexes => SizeSectors * 32`, `int HeightHexes => SizeSectors * 40`, `int CellsX => SizeSectors * 4`, `int CellsY => SizeSectors * 4`.
  - `static class ValueNoise` — `double Sample(ulong seed, RollChannel channel, double x, double y, int octaves, double frequency)` returning [0,1]; `double Warped(ulong seed, RollChannel valueChannel, RollChannel warpChannel, double x, double y, int octaves, double frequency, double warpStrength)`.
  - New `RollChannel` members: `NoiseDensityLattice = 24, NoiseWarpLattice = 25, NoiseStellarLattice = 26, NoiseMetalLattice = 27, AnchorPlacement = 28, AnchorKind = 29, HomeworldPlacement = 30, SpeciesEmbodiment = 31, SpeciesTemperament = 32, SimExpansion = 33, SimDevelopment = 34, SimWar = 35`.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/ValueNoiseTests.cs`:

```csharp
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class ValueNoiseTests
{
    [Fact]
    public void Sample_IsDeterministic()
    {
        var a = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 12.34, 56.78, 3, 0.05);
        var b = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 12.34, 56.78, 3, 0.05);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Sample_StaysInUnitInterval()
    {
        for (int i = 0; i < 2000; i++)
        {
            var v = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, i * 0.37, i * 0.91, 3, 0.05);
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void Sample_VariesSpatially_AndIsContinuous()
    {
        var a = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 10.0, 10.0, 3, 0.05);
        var far = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 300.0, 470.0, 3, 0.05);
        var near = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 10.3, 10.0, 3, 0.05);
        Assert.NotEqual(a, far);
        Assert.True(System.Math.Abs(a - near) < 0.25, $"continuity: |{a}-{near}| too large");
    }

    [Fact]
    public void DifferentSeedsOrChannels_Differ()
    {
        var a = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 40.0, 40.0, 3, 0.05);
        Assert.NotEqual(a, ValueNoise.Sample(8, RollChannel.NoiseDensityLattice, 40.0, 40.0, 3, 0.05));
        Assert.NotEqual(a, ValueNoise.Sample(7, RollChannel.NoiseStellarLattice, 40.0, 40.0, 3, 0.05));
    }

    [Fact]
    public void GalaxyConfig_Defaults()
    {
        var config = new GalaxyConfig { MasterSeed = 42 };
        Assert.Equal(10, config.SizeSectors);
        Assert.Equal(320, config.WidthHexes);
        Assert.Equal(400, config.HeightHexes);
        Assert.Equal(40, config.CellsX);
        Assert.Equal(40, config.CellsY);
        Assert.Equal(0.5, config.MeanDensityTarget);
        Assert.Equal(12, config.EpochCount);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter ValueNoiseTests` — Expected: FAIL (types missing).

- [ ] **Step 3: Implement.** In `src/Core/Rng/RollChannel.cs`, append inside the enum after `OverlayPick = 23,`:

```csharp
    // --- Regional generation (slice 1). Values stable per registry discipline. ---
    NoiseDensityLattice = 24,  // lattice draws: index = lattice x, subIndex = lattice y (via ValueNoise packing)
    NoiseWarpLattice = 25,
    NoiseStellarLattice = 26,
    NoiseMetalLattice = 27,
    AnchorPlacement = 28,      // cell-keyed: RollContext coordinate = cell coords
    AnchorKind = 29,
    HomeworldPlacement = 30,
    SpeciesEmbodiment = 31,
    SpeciesTemperament = 32,   // subIndex = temperament axis ordinal
    SimExpansion = 33,         // index = epoch, subIndex = polity id
    SimDevelopment = 34,       // index = epoch, subIndex = cell linear index
    SimWar = 35,               // index = epoch, subIndex = polity id
```

Create `src/Core/Galaxy/GalaxyConfig.cs`:

```csharp
namespace StarGen.Core.Galaxy;

/// <summary>Generation input: identity + tuning knobs (spec §3). Recorded in artifact stamps.</summary>
public sealed class GalaxyConfig
{
    public ulong MasterSeed { get; set; }
    public int SizeSectors { get; set; } = 10;          // galaxy is SizeSectors x SizeSectors sectors
    public double MeanDensityTarget { get; set; } = 0.5;
    public int ArmCount { get; set; } = 3;
    public double ArmTightness { get; set; } = 0.35;
    public double ArmWidth { get; set; } = 0.18;
    public int EpochCount { get; set; } = 12;
    public int YearsPerEpoch { get; set; } = 50;
    public double HomeworldRatePerSector { get; set; } = 0.25;   // ~1 per 4 sectors (spec §5)
    public double TraversabilityThreshold { get; set; } = 0.25;  // connectivity edge gate (spec §5)

    public int WidthHexes => SizeSectors * 32;
    public int HeightHexes => SizeSectors * 40;
    public int CellsX => SizeSectors * 4;   // 8-hex-wide subsector cells
    public int CellsY => SizeSectors * 4;   // 10-hex-tall subsector cells
}
```

Create `src/Core/Galaxy/ValueNoise.cs`:

```csharp
using System;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>
/// Hash-based value noise (spec §4): lattice values from StableHash, bilinear
/// interpolation with smoothstep, fractal octaves, optional domain warp.
/// No external noise library — Core stays dependency-free.
/// </summary>
public static class ValueNoise
{
    public static double Sample(ulong seed, RollChannel channel, double x, double y,
                                int octaves, double frequency)
    {
        double sum = 0, amplitude = 1, totalAmplitude = 0;
        double f = frequency;
        for (int o = 0; o < octaves; o++)
        {
            sum += amplitude * Single(seed, channel, x * f, y * f, o);
            totalAmplitude += amplitude;
            amplitude *= 0.5;
            f *= 2.0;
        }
        return sum / totalAmplitude;
    }

    public static double Warped(ulong seed, RollChannel valueChannel, RollChannel warpChannel,
                                double x, double y, int octaves, double frequency,
                                double warpStrength)
    {
        double wx = Sample(seed, warpChannel, x + 31.7, y, 2, frequency) - 0.5;
        double wy = Sample(seed, warpChannel, x, y + 67.3, 2, frequency) - 0.5;
        return Sample(seed, valueChannel, x + wx * warpStrength, y + wy * warpStrength,
                      octaves, frequency);
    }

    private static double Single(ulong seed, RollChannel channel, double x, double y, int octave)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = SmoothStep(x - x0), ty = SmoothStep(y - y0);
        double v00 = Lattice(seed, channel, x0, y0, octave);
        double v10 = Lattice(seed, channel, x0 + 1, y0, octave);
        double v01 = Lattice(seed, channel, x0, y0 + 1, octave);
        double v11 = Lattice(seed, channel, x0 + 1, y0 + 1, octave);
        double a = v00 + (v10 - v00) * tx;
        double b = v01 + (v11 - v01) * tx;
        return a + (b - a) * ty;
    }

    private static double Lattice(ulong seed, RollChannel channel, int lx, int ly, int octave)
    {
        // Pack lattice coords the same way RollContext packs its coordinate.
        ulong coord = ((ulong)(uint)lx << 32) | (uint)ly;
        ulong h = StableHash.Mix(seed, coord, (ulong)channel, (ulong)(uint)octave);
        return (h >> 11) * (1.0 / (1UL << 53));
    }

    private static double SmoothStep(double t) => t * t * (3 - 2 * t);
}
```

- [ ] **Step 4: Run** — `dotnet test --filter ValueNoiseTests` — Expected: PASS (5 tests). Full `dotnet test`: 51 green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: galaxy config, regional roll channels, hash value noise"`

---

### Task 2: DensityField (galactic shape × noise)

**Files:**
- Create: `src/Core/Galaxy/DensityField.cs`
- Test: `tests/Core.Tests/Galaxy/DensityFieldTests.cs`

**Interfaces:**
- Consumes: `GalaxyConfig`, `ValueNoise`, `HexCoordinate`.
- Produces: `static class DensityField` — `double At(GalaxyConfig config, HexCoordinate hex)` in [0,1]; `double ShapeAt(GalaxyConfig config, double nx, double ny)` (normalized coords, |n|=1 at rim; exposed for tests/atlas).

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/DensityFieldTests.cs`:

```csharp
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
        for (int i = 0; i < 500; i++)
        {
            var hex = new HexCoordinate((i * 13) % config.WidthHexes, (i * 29) % config.HeightHexes);
            var v = DensityField.At(config, hex);
            Assert.Equal(v, DensityField.At(config, hex));
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void BeyondRim_IsZero()
    {
        var config = Config();
        Assert.Equal(0.0, DensityField.At(config, new HexCoordinate(0, 0)));
        Assert.Equal(0.0, DensityField.At(config, new HexCoordinate(config.WidthHexes - 1, 0)));
    }

    [Fact]
    public void Core_IsDenserThanMidDisc()
    {
        var config = Config();
        var center = new HexCoordinate(config.WidthHexes / 2, config.HeightHexes / 2);
        double Avg(HexCoordinate c, int radius)
        {
            double sum = 0; int n = 0;
            for (int dx = -radius; dx <= radius; dx += 2)
                for (int dy = -radius; dy <= radius; dy += 2)
                { sum += DensityField.At(config, new HexCoordinate(c.X + dx, c.Y + dy)); n++; }
            return sum / n;
        }
        double coreAvg = Avg(center, 8);
        double midAvg = Avg(new HexCoordinate(center.X + config.WidthHexes / 3, center.Y), 8);
        Assert.True(coreAvg > midAvg, $"core {coreAvg:F3} should exceed mid-disc {midAvg:F3}");
    }

    [Fact]
    public void MeanInsideDisc_NearTarget()
    {
        var config = Config();
        double sum = 0; int count = 0;
        for (int x = 0; x < config.WidthHexes; x += 4)
            for (int y = 0; y < config.HeightHexes; y += 4)
            {
                double nx = (x - config.WidthHexes / 2.0) / (config.WidthHexes / 2.0);
                double ny = (y - config.HeightHexes / 2.0) / (config.HeightHexes / 2.0);
                if (nx * nx + ny * ny > 0.81) continue;   // inside the disc only
                sum += DensityField.At(config, new HexCoordinate(x, y));
                count++;
            }
        Assert.InRange(sum / count, config.MeanDensityTarget - 0.12, config.MeanDensityTarget + 0.12);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter DensityFieldTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/DensityField.cs`:

```csharp
using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 1 (spec §4): pure density field = galactic shape × local noise.</summary>
public static class DensityField
{
    public static double At(GalaxyConfig config, HexCoordinate hex)
    {
        double nx = (hex.X - config.WidthHexes / 2.0) / (config.WidthHexes / 2.0);
        double ny = (hex.Y - config.HeightHexes / 2.0) / (config.HeightHexes / 2.0);
        double shape = ShapeAt(config, nx, ny);
        if (shape <= 0) return 0.0;

        double noise = ValueNoise.Warped(config.MasterSeed,
            RollChannel.NoiseDensityLattice, RollChannel.NoiseWarpLattice,
            hex.X, hex.Y, octaves: 3, frequency: 0.035, warpStrength: 18.0);

        // Shape sets the envelope; noise carves clumps/filaments/voids inside it.
        // 0.25 + 1.5*noise spans [0.25, 1.75]: voids suppress, ridges overshoot (clamped).
        double v = shape * (0.25 + 1.5 * noise);
        v *= config.MeanDensityTarget / 0.5;   // shape's disc mean is calibrated to ~0.5
        return Math.Clamp(v, 0.0, 1.0);
    }

    /// <summary>Shape only, normalized coords (|n| = 1 at rim). Disc mean ≈ 0.5.</summary>
    public static double ShapeAt(GalaxyConfig config, double nx, double ny)
    {
        double r = Math.Sqrt(nx * nx + ny * ny);
        if (r >= 1.0) return 0.0;                       // hard zero beyond the rim

        double theta = Math.Atan2(ny, nx);
        double core = Math.Exp(-(r * r) / (2 * 0.18 * 0.18));            // bright center
        double disc = Math.Exp(-(r * r) / (2 * 0.55 * 0.55));            // broad falloff

        // Log-spiral arms: angular distance to the nearest arm ridge at this radius.
        double armAngle = Math.Log(Math.Max(r, 0.05)) / config.ArmTightness;
        double phase = (theta - armAngle) * config.ArmCount / (2 * Math.PI);
        double toRidge = Math.Abs(phase - Math.Round(phase)) * 2;        // 0 at ridge, 1 between
        double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
                      * (1 - core) * 0.9;

        return Math.Clamp(core + disc * 0.35 + arms * disc, 0.0, 1.0);
    }
}
```

Tuning note: if `MeanInsideDisc_NearTarget` lands outside its ±0.12 band on first run, adjust the literals `0.55` (disc sigma) and `0.35` (disc weight) until it passes and record the final values in your report. Do not widen the test band.

- [ ] **Step 4: Run** — `dotnet test --filter DensityFieldTests` — Expected: PASS (4 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: density field with galactic shape and warped noise"`

---
### Task 3: GalaxyContext, density-driven presence, flatspace wrapper

**Files:**
- Create: `src/Core/Galaxy/GalaxyContext.cs`
- Modify: `src/Core/Generation/Generator.cs`
- Test: `tests/Core.Tests/Galaxy/GalaxyPresenceTests.cs`

**Interfaces:**
- Consumes: `GalaxyConfig`, `DensityField`, `Generator`, `RollContext`, `RollChannel.Presence`.
- Produces:
  - `sealed class GalaxyContext` — `GalaxyConfig Config { get; }`, `GalaxySkeleton? Skeleton { get; set; }` (null until Task 4 exists — declare the property in Task 4's step; in this task the class holds only Config), ctor `GalaxyContext(GalaxyConfig config)`, `static GalaxyContext Flatspace(ulong masterSeed)` returning a context whose `IsFlatspace` property is true.
  - `Generator.Generate(GalaxyContext galaxy, HexCoordinate coord)` — presence threshold = `galaxy.IsFlatspace ? StellarDensity : DensityField.At(galaxy.Config, coord)`; rest of pipeline unchanged in this task.
  - Legacy `Generator.Generate(ulong masterSeed, HexCoordinate coord)` becomes exactly `Generate(GalaxyContext.Flatspace(masterSeed), coord)`.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/GalaxyPresenceTests.cs`:

```csharp
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class GalaxyPresenceTests
{
    [Fact]
    public void Flatspace_MatchesLegacySignature_Exactly()
    {
        var flat = GalaxyContext.Flatspace(17);
        for (int x = 0; x < 300; x++)
        {
            var coord = new HexCoordinate(x, 5);
            var viaLegacy = Generator.Generate(17, coord);
            var viaContext = Generator.Generate(flat, coord);
            Assert.Equal(viaLegacy.IsEmpty, viaContext.IsEmpty);
            Assert.Equal(viaLegacy.System?.Designation, viaContext.System?.Designation);
        }
    }

    [Fact]
    public void ShapedGalaxy_CornersEmpty_CoreDense()
    {
        var galaxy = new GalaxyContext(new GalaxyConfig { MasterSeed = 42 });
        var config = galaxy.Config;
        // corners are beyond the rim: always empty
        Assert.True(Generator.Generate(galaxy, new HexCoordinate(0, 0)).IsEmpty);
        Assert.True(Generator.Generate(galaxy, new HexCoordinate(config.WidthHexes - 1, config.HeightHexes - 1)).IsEmpty);
        // galactic core: far denser than flat 50%
        int present = 0, total = 0;
        int cx = config.WidthHexes / 2, cy = config.HeightHexes / 2;
        for (int dx = -10; dx <= 10; dx++)
            for (int dy = -10; dy <= 10; dy++)
            {
                total++;
                if (!Generator.Generate(galaxy, new HexCoordinate(cx + dx, cy + dy)).IsEmpty) present++;
            }
        Assert.True(present / (double)total > 0.6, $"core presence {present}/{total} should exceed 60%");
    }

    [Fact]
    public void ShapedGalaxy_IsDeterministic()
    {
        var a = new GalaxyContext(new GalaxyConfig { MasterSeed = 99 });
        var b = new GalaxyContext(new GalaxyConfig { MasterSeed = 99 });
        for (int i = 0; i < 200; i++)
        {
            var coord = new HexCoordinate(100 + i, 200);
            Assert.Equal(Generator.Generate(a, coord).IsEmpty, Generator.Generate(b, coord).IsEmpty);
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter GalaxyPresenceTests` — Expected: FAIL.

- [ ] **Step 3: Implement.** Create `src/Core/Galaxy/GalaxyContext.cs`:

```csharp
namespace StarGen.Core.Galaxy;

/// <summary>Generation handle: config + (later) skeleton. Flatspace = Phase 1 behavior (spec §8).</summary>
public sealed class GalaxyContext
{
    public GalaxyConfig Config { get; }
    public bool IsFlatspace { get; }

    public GalaxyContext(GalaxyConfig config)
    {
        Config = config;
        IsFlatspace = false;
    }

    private GalaxyContext(GalaxyConfig config, bool flatspace)
    {
        Config = config;
        IsFlatspace = flatspace;
    }

    public static GalaxyContext Flatspace(ulong masterSeed) =>
        new(new GalaxyConfig { MasterSeed = masterSeed }, flatspace: true);
}
```

In `src/Core/Generation/Generator.cs`, replace the whole class body with:

```csharp
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using StarGen.Core.Overlays;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class Generator
{
    /// <summary>Flatspace stellar density (spec §4 stage 0 of the Phase 1 spec). Tunable.</summary>
    public const double StellarDensity = 0.5;

    /// <summary>Legacy Phase 1 signature — exactly flatspace (regional spec §8).</summary>
    public static HexResult Generate(ulong masterSeed, HexCoordinate coord) =>
        Generate(GalaxyContext.Flatspace(masterSeed), coord);

    public static HexResult Generate(GalaxyContext galaxy, HexCoordinate coord)
    {
        var ctx = new RollContext(galaxy.Config.MasterSeed, coord);

        double presenceThreshold = galaxy.IsFlatspace
            ? StellarDensity
            : DensityField.At(galaxy.Config, coord);
        if (ctx.NextDouble(RollChannel.Presence) >= presenceThreshold)
            return new HexResult(coord, null);

        var system = new StarSystem(Designation.For(coord));
        StarGenerator.Generate(ctx, system);
        BodyGenerator.Generate(ctx, system);
        SocietyGenerator.Generate(ctx, system);
        NameGenerator.AssignNames(ctx, system);
        OverlayResolver.Resolve(ctx, system);
        return new HexResult(coord, system);
    }
}
```

Note: the existing `Presence` channel draw is unchanged — only the threshold varies (spec §4), so flatspace output is bit-identical to Phase 1.

- [ ] **Step 4: Run** — `dotnet test` (full suite) — Expected: all green including the original 46 (flatspace regression is the point of this task).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: galaxy context with density-driven presence and flatspace wrapper"`

---

### Task 4: Skeleton data model

**Files:**
- Create: `src/Core/Galaxy/RegionCell.cs`, `src/Core/Galaxy/Anchor.cs`, `src/Core/Galaxy/SpeciesProfile.cs`, `src/Core/Galaxy/Polity.cs`, `src/Core/Galaxy/GalaxyEvent.cs`, `src/Core/Galaxy/GalaxySkeleton.cs`
- Modify: `src/Core/Galaxy/GalaxyContext.cs` (add `GalaxySkeleton? Skeleton { get; set; }`)
- Test: `tests/Core.Tests/Galaxy/SkeletonModelTests.cs`

**Interfaces:**
- Produces (all `namespace StarGen.Core.Galaxy`, plain mutable classes):
  - Enums: `StellarLean { Balanced, YoungBright, OldDim, RemnantGraveyard }`, `AnchorType { MineralRich, PrecursorSite, Homeworld }`, `Embodiment { TerranAnalog, Aquatic, Cryophilic, Lithic, Hive, Machine }`, `GalaxyEventType { CellClaimed, WarStarted, CellTaken, LostCapital, PolityExtinct }`.
  - `RegionCell`: `int Cx, Cy`; `double MeanDensity`; `bool IsVoid`; `bool IsChokepoint`; `StellarLean Lean`; `double Metallicity`; `List<Anchor> Anchors`; `int OwnerPolityId = -1` (-1 = unclaimed); `int DevelopmentTier`; `bool Contested`; `bool WarScarred`; `int LinearIndex(GalaxyConfig c) => Cy * c.CellsX + Cx`.
  - `Anchor`: `AnchorType Type`; `HexCoordinate Hex`; `int SpeciesId = -1` (homeworlds only).
  - `SpeciesProfile`: `int Id`; `string Name`; `Embodiment Embodiment`; doubles `Expansionism, Cohesion, Militancy, Openness, Industry, Adaptability` (all 0..1).
  - `Polity`: `int Id`; `string Name`; `int SpeciesId`; `int CapitalCx, CapitalCy`; `bool Extinct`.
  - `GalaxyEvent`: `int Epoch`; `GalaxyEventType Type`; `int ActorPolityId`; `int TargetPolityId = -1`; `int Cx, Cy`; `double Magnitude`.
  - `GalaxySkeleton`: `GalaxyConfig Config`; `RegionCell[] Cells` (row-major, `CellsY * CellsX`); `List<SpeciesProfile> Species`; `List<Polity> Polities`; `List<GalaxyEvent> Events`; helpers `RegionCell CellAt(int cx, int cy)`, `RegionCell CellForHex(HexCoordinate hex)`; consts `public const int SchemaVersion = 1;`.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/SkeletonModelTests.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SkeletonModelTests
{
    [Fact]
    public void Skeleton_CellLookups_Work()
    {
        var config = new GalaxyConfig { MasterSeed = 1, SizeSectors = 2 };  // 8x8 cells
        var skeleton = new GalaxySkeleton(config);
        Assert.Equal(config.CellsX * config.CellsY, skeleton.Cells.Length);
        var cell = skeleton.CellAt(3, 5);
        Assert.Equal(3, cell.Cx);
        Assert.Equal(5, cell.Cy);
        // hex (25, 52) -> cell (25/8, 52/10) = (3, 5)
        Assert.Same(cell, skeleton.CellForHex(new HexCoordinate(25, 52)));
    }

    [Fact]
    public void RegionCell_Defaults()
    {
        var cell = new RegionCell { Cx = 1, Cy = 2 };
        Assert.Equal(-1, cell.OwnerPolityId);
        Assert.Empty(cell.Anchors);
        Assert.False(cell.Contested);
        Assert.False(cell.WarScarred);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter SkeletonModelTests` — Expected: FAIL.

- [ ] **Step 3: Implement.** Create `src/Core/Galaxy/RegionCell.cs`:

```csharp
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

public enum StellarLean { Balanced, YoungBright, OldDim, RemnantGraveyard }

public sealed class RegionCell
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public double MeanDensity { get; set; }
    public bool IsVoid { get; set; }
    public bool IsChokepoint { get; set; }
    public StellarLean Lean { get; set; }
    public double Metallicity { get; set; }
    public List<Anchor> Anchors { get; } = new();
    public int OwnerPolityId { get; set; } = -1;   // -1 = unclaimed
    public int DevelopmentTier { get; set; }
    public bool Contested { get; set; }
    public bool WarScarred { get; set; }

    public int LinearIndex(GalaxyConfig config) => Cy * config.CellsX + Cx;
}
```

Create `src/Core/Galaxy/Anchor.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Closed, versioned anchor vocabulary (spec §5). One anchor per hex.</summary>
public enum AnchorType { MineralRich, PrecursorSite, Homeworld }

public sealed class Anchor
{
    public AnchorType Type { get; set; }
    public HexCoordinate Hex { get; set; }
    public int SpeciesId { get; set; } = -1;   // homeworlds only
}
```

Create `src/Core/Galaxy/SpeciesProfile.cs`:

```csharp
namespace StarGen.Core.Galaxy;

public enum Embodiment { TerranAnalog, Aquatic, Cryophilic, Lithic, Hive, Machine }

/// <summary>Simulation-legible species traits (spec §6). Compact by design.</summary>
public sealed class SpeciesProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Embodiment Embodiment { get; set; }
    public double Expansionism { get; set; }
    public double Cohesion { get; set; }
    public double Militancy { get; set; }
    public double Openness { get; set; }
    public double Industry { get; set; }
    public double Adaptability { get; set; }
}
```

Create `src/Core/Galaxy/Polity.cs`:

```csharp
namespace StarGen.Core.Galaxy;

/// <summary>Registry entry. Extinct polities are retained, flagged (spec §7 lifecycle).</summary>
public sealed class Polity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SpeciesId { get; set; }
    public int CapitalCx { get; set; }
    public int CapitalCy { get; set; }
    public bool Extinct { get; set; }
}
```

Create `src/Core/Galaxy/GalaxyEvent.cs`:

```csharp
namespace StarGen.Core.Galaxy;

public enum GalaxyEventType { CellClaimed, WarStarted, CellTaken, LostCapital, PolityExtinct }

/// <summary>One record of the single global append-only event log (spec §7 State).</summary>
public sealed class GalaxyEvent
{
    public int Epoch { get; set; }
    public GalaxyEventType Type { get; set; }
    public int ActorPolityId { get; set; }
    public int TargetPolityId { get; set; } = -1;
    public int Cx { get; set; }
    public int Cy { get; set; }
    public double Magnitude { get; set; }
}
```

Create `src/Core/Galaxy/GalaxySkeleton.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The persisted Tier 2 artifact root (spec §3.1).</summary>
public sealed class GalaxySkeleton
{
    public const int SchemaVersion = 1;

    public GalaxyConfig Config { get; }
    public RegionCell[] Cells { get; }
    public List<SpeciesProfile> Species { get; } = new();
    public List<Polity> Polities { get; } = new();
    public List<GalaxyEvent> Events { get; } = new();

    public GalaxySkeleton(GalaxyConfig config)
    {
        Config = config;
        Cells = new RegionCell[config.CellsX * config.CellsY];
        for (int cy = 0; cy < config.CellsY; cy++)
            for (int cx = 0; cx < config.CellsX; cx++)
                Cells[cy * config.CellsX + cx] = new RegionCell { Cx = cx, Cy = cy };
    }

    public RegionCell CellAt(int cx, int cy) => Cells[cy * Config.CellsX + cx];

    public RegionCell CellForHex(HexCoordinate hex) => CellAt(hex.X / 8, hex.Y / 10);
}
```

In `src/Core/Galaxy/GalaxyContext.cs`, add to the class:

```csharp
    public GalaxySkeleton? Skeleton { get; set; }
```

- [ ] **Step 4: Run** — `dotnet test --filter SkeletonModelTests` — Expected: PASS (2 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: galaxy skeleton data model"`

---
### Task 5: SkeletonBuilder pass 1 — density summary, voids, chokepoints

**Files:**
- Create: `src/Core/Galaxy/SkeletonBuilder.cs`
- Test: `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Consumes: `GalaxyConfig`, `GalaxySkeleton`, `DensityField`, `HexCoordinate`.
- Produces: `static class SkeletonBuilder` with `static GalaxySkeleton Build(GalaxyConfig config)` (runs all passes that exist so far; later tasks append pass calls inside it, marked by comments exactly like Phase 1's `Generator` markers) and `internal static void PassDensitySummary(GalaxySkeleton s)`. After pass 1: every cell has `MeanDensity` (average of `DensityField.At` over the cell's 8×10 hexes, sampled every 2nd hex both axes = 20 samples), `IsVoid` (`MeanDensity < config.TraversabilityThreshold`), `IsChokepoint` (articulation cells of the traversability graph, spec §5: nodes = non-void cells, edges between 4-neighbor non-void cells; compute articulation points with the standard iterative DFS low-link algorithm).

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/SeedingPassTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SeedingPassTests
{
    // SizeSectors = 4 keeps builds fast (16x16 cells) while big enough for structure.
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, SizeSectors = 4 });

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = Build();
        var b = Build();
        for (int i = 0; i < a.Cells.Length; i++)
        {
            Assert.Equal(a.Cells[i].MeanDensity, b.Cells[i].MeanDensity);
            Assert.Equal(a.Cells[i].IsVoid, b.Cells[i].IsVoid);
            Assert.Equal(a.Cells[i].IsChokepoint, b.Cells[i].IsChokepoint);
        }
    }

    [Fact]
    public void DensitySummary_HasStructure()
    {
        var s = Build();
        Assert.Contains(s.Cells, c => c.IsVoid);                       // rim/void cells exist
        Assert.Contains(s.Cells, c => c.MeanDensity > 0.5);            // dense cells exist
        Assert.All(s.Cells, c => Assert.InRange(c.MeanDensity, 0.0, 1.0));
    }

    [Fact]
    public void Chokepoints_AreNonVoid_AndScarcerThanOrdinaryCells()
    {
        var s = Build();
        var chokepoints = s.Cells.Where(c => c.IsChokepoint).ToList();
        Assert.All(chokepoints, c => Assert.False(c.IsVoid));
        Assert.True(chokepoints.Count < s.Cells.Count(c => !c.IsVoid) / 2,
            "chokepoints should be a minority of traversable cells");
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter SeedingPassTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/SkeletonBuilder.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 2 builder: ordered seeding passes, then the epoch sim (spec §5, §7).</summary>
public static class SkeletonBuilder
{
    public static GalaxySkeleton Build(GalaxyConfig config)
    {
        var skeleton = new GalaxySkeleton(config);
        PassDensitySummary(skeleton);
        // PASSES (later tasks append here, in order):
        // PassStellarPopulation(skeleton);
        // PassResourceAnchors(skeleton);
        // PassHomeworlds(skeleton);
        // EpochSim.Run(skeleton);
        return skeleton;
    }

    internal static void PassDensitySummary(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            double sum = 0; int n = 0;
            for (int hx = cell.Cx * 8; hx < cell.Cx * 8 + 8; hx += 2)
                for (int hy = cell.Cy * 10; hy < cell.Cy * 10 + 10; hy += 2)
                {
                    sum += DensityField.At(config, new HexCoordinate(hx, hy));
                    n++;
                }
            cell.MeanDensity = sum / n;
            cell.IsVoid = cell.MeanDensity < config.TraversabilityThreshold;
        }
        MarkChokepoints(s);
    }

    /// <summary>Articulation points of the traversability graph (spec §5 pass 1).</summary>
    private static void MarkChokepoints(GalaxySkeleton s)
    {
        var config = s.Config;
        int w = config.CellsX, h = config.CellsY, n = w * h;
        int[] disc = new int[n], low = new int[n], parent = new int[n];
        bool[] visited = new bool[n], articulation = new bool[n];
        for (int i = 0; i < n; i++) parent[i] = -1;
        int timer = 0;

        IEnumerable<int> Neighbors(int idx)
        {
            int cx = idx % w, cy = idx / w;
            if (cx > 0 && !s.Cells[idx - 1].IsVoid) yield return idx - 1;
            if (cx < w - 1 && !s.Cells[idx + 1].IsVoid) yield return idx + 1;
            if (cy > 0 && !s.Cells[idx - w].IsVoid) yield return idx - w;
            if (cy < h - 1 && !s.Cells[idx + w].IsVoid) yield return idx + w;
        }

        for (int root = 0; root < n; root++)
        {
            if (visited[root] || s.Cells[root].IsVoid) continue;
            // Iterative DFS with explicit stack (recursion depth could hit thousands).
            var stack = new Stack<(int node, IEnumerator<int> it)>();
            visited[root] = true; disc[root] = low[root] = timer++;
            stack.Push((root, Neighbors(root).GetEnumerator()));
            int rootChildren = 0;

            while (stack.Count > 0)
            {
                var (node, it) = stack.Peek();
                if (it.MoveNext())
                {
                    int next = it.Current;
                    if (!visited[next])
                    {
                        parent[next] = node;
                        if (node == root) rootChildren++;
                        visited[next] = true; disc[next] = low[next] = timer++;
                        stack.Push((next, Neighbors(next).GetEnumerator()));
                    }
                    else if (next != parent[node] && disc[next] < low[node])
                        low[node] = disc[next];
                }
                else
                {
                    stack.Pop();
                    if (stack.Count > 0)
                    {
                        int p = stack.Peek().node;
                        if (low[node] < low[p]) low[p] = low[node];
                        if (p != root && low[node] >= disc[p]) articulation[p] = true;
                    }
                }
            }
            if (rootChildren > 1) articulation[root] = true;
        }

        for (int i = 0; i < n; i++) s.Cells[i].IsChokepoint = articulation[i];
    }
}
```

- [ ] **Step 4: Run** — `dotnet test --filter SeedingPassTests` — Expected: PASS (3 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: skeleton density summary with voids and chokepoints"`

---

### Task 6: SkeletonBuilder pass 2 — stellar population & metallicity

**Files:**
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (add `PassStellarPopulation`, replace its marker)
- Test: append to `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Consumes: `ValueNoise`, `RollChannel.NoiseStellarLattice/NoiseMetalLattice`.
- Produces: `internal static void PassStellarPopulation(GalaxySkeleton s)` — sets each cell's `Lean` and `Metallicity`. Lean derivation: sample `stellar = ValueNoise.Sample(seed, NoiseStellarLattice, cellCenterHexX, cellCenterHexY, 2, 0.02)`, then exactly: `stellar < 0.12 → RemnantGraveyard`, `< 0.40 → OldDim`, `> 0.72 → YoungBright`, else `Balanced` (graveyards rarest). `Metallicity = ValueNoise.Sample(seed, NoiseMetalLattice, cellCenterHexX, cellCenterHexY, 2, 0.015)`. Cell center hex = `(Cx*8+4, Cy*10+5)`.

- [ ] **Step 1: Write the failing tests** — append to `SeedingPassTests`:

```csharp
    [Fact]
    public void StellarPopulation_AllLeansOccur_AndBalancedDominates()
    {
        var s = Build();
        var counts = s.Cells.GroupBy(c => c.Lean).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(counts.TryGetValue(StellarLean.Balanced, out var balanced) && balanced > s.Cells.Length / 3,
            "Balanced should be the most common lean");
        Assert.Contains(StellarLean.YoungBright, counts.Keys);
        Assert.Contains(StellarLean.OldDim, counts.Keys);
        // RemnantGraveyard is rare (~12%) — at 256 cells it should appear but stay a small minority
        if (counts.TryGetValue(StellarLean.RemnantGraveyard, out var graveyards))
            Assert.True(graveyards < s.Cells.Length / 4);
    }

    [Fact]
    public void Metallicity_IsBounded_AndVaries()
    {
        var s = Build();
        Assert.All(s.Cells, c => Assert.InRange(c.Metallicity, 0.0, 1.0));
        Assert.True(s.Cells.Select(c => System.Math.Round(c.Metallicity, 2)).Distinct().Count() > 10,
            "metallicity should vary across cells");
    }
```

- [ ] **Step 2: Run** — `dotnet test --filter SeedingPassTests` — Expected: 2 new FAIL.

- [ ] **Step 3: Implement.** In `SkeletonBuilder.Build`, replace the `// PassStellarPopulation(skeleton);` marker with the real call, and add:

```csharp
    /// <summary>Spec §5 pass 2: stellar-population & metallicity leans. Never paints
    /// body kinds — world character emerges via the star->band->body causality.</summary>
    internal static void PassStellarPopulation(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            double hx = cell.Cx * 8 + 4, hy = cell.Cy * 10 + 5;
            double stellar = ValueNoise.Sample(config.MasterSeed,
                Rng.RollChannel.NoiseStellarLattice, hx, hy, 2, 0.02);
            cell.Lean = stellar < 0.12 ? StellarLean.RemnantGraveyard
                      : stellar < 0.40 ? StellarLean.OldDim
                      : stellar > 0.72 ? StellarLean.YoungBright
                      : StellarLean.Balanced;
            cell.Metallicity = ValueNoise.Sample(config.MasterSeed,
                Rng.RollChannel.NoiseMetalLattice, hx, hy, 2, 0.015);
        }
    }
```

- [ ] **Step 4: Run** — `dotnet test --filter SeedingPassTests` — Expected: PASS (5 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: stellar population and metallicity seeding pass"`

---
### Task 7: SkeletonBuilder pass 3 — resource anchors

**Files:**
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (add `PassResourceAnchors` + shared `PickAnchorHex` helper, replace marker)
- Test: append to `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Consumes: `RollContext` (cell-keyed: `new RollContext(seed, new HexCoordinate(cell.Cx, cell.Cy))`), `RollChannel.AnchorPlacement/AnchorKind`.
- Produces:
  - `internal static void PassResourceAnchors(GalaxySkeleton s)` — per non-void cell: chance of a MineralRich anchor = `0.10 + 0.25 * cell.Metallicity` (metal-poor regions are resource deserts, spec §5); independent chance of a PrecursorSite = `0.02`, plus `0.02` more when `Lean == RemnantGraveyard` (old space hides old things) — precursor placement ignores density deliberately (rare void-sites are stories, so precursor anchors may land in void cells too: roll for them on every cell, not just non-void).
  - `internal static HexCoordinate PickAnchorHex(GalaxySkeleton s, RegionCell cell, int drawIndex)` — hash-draws a hex inside the cell (`RollContext` on the cell coords, `AnchorPlacement` channel, `index = drawIndex`) giving `(cell.Cx*8 + roll0..7, cell.Cy*10 + roll0..9)`; **collision rule (spec §5): one anchor per hex** — if taken, probe forward deterministically (`hx+1` wrapping within the cell row, then next row) until a free hex is found (a cell has 80 hexes and ≤ a handful of anchors; always terminates).

- [ ] **Step 1: Write the failing tests** — append to `SeedingPassTests`:

```csharp
    [Fact]
    public void Anchors_ArePlaced_OnePerHex_InsideTheirCell()
    {
        var s = Build();
        var all = s.Cells.SelectMany(c => c.Anchors.Select(a => (c, a))).ToList();
        Assert.True(all.Count(x => x.a.Type == AnchorType.MineralRich) > 5, "mineral anchors should exist");
        Assert.Contains(all, x => x.a.Type == AnchorType.PrecursorSite);
        // one anchor per hex, and each anchor's hex lies inside its cell
        var hexes = all.Select(x => x.a.Hex).ToList();
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        foreach (var (c, a) in all)
        {
            Assert.InRange(a.Hex.X, c.Cx * 8, c.Cx * 8 + 7);
            Assert.InRange(a.Hex.Y, c.Cy * 10, c.Cy * 10 + 9);
        }
    }

    [Fact]
    public void MineralAnchors_FollowMetallicity()
    {
        var s = Build();
        var richCells = s.Cells.Where(c => !c.IsVoid && c.Metallicity > 0.6).ToList();
        var poorCells = s.Cells.Where(c => !c.IsVoid && c.Metallicity < 0.4).ToList();
        double richRate = richCells.Count(c => c.Anchors.Any(a => a.Type == AnchorType.MineralRich)) / (double)richCells.Count;
        double poorRate = poorCells.Count(c => c.Anchors.Any(a => a.Type == AnchorType.MineralRich)) / (double)poorCells.Count;
        Assert.True(richRate > poorRate, $"metal-rich cells ({richRate:F2}) should out-anchor metal-poor ({poorRate:F2})");
    }
```

- [ ] **Step 2: Run** — `dotnet test --filter SeedingPassTests` — Expected: 2 new FAIL.

- [ ] **Step 3: Implement.** Replace the `// PassResourceAnchors(skeleton);` marker with the call, and add to `SkeletonBuilder` (also add `using StarGen.Core.Rng;` at the top of the file):

```csharp
    /// <summary>Spec §5 pass 3: strategic anchors. Closed vocabulary, one per hex.</summary>
    internal static void PassResourceAnchors(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            var ctx = new RollContext(config.MasterSeed, new HexCoordinate(cell.Cx, cell.Cy));

            if (!cell.IsVoid)
            {
                double mineralChance = 0.10 + 0.25 * cell.Metallicity;
                if (ctx.NextDouble(RollChannel.AnchorKind, 0) < mineralChance)
                    cell.Anchors.Add(new Anchor
                    {
                        Type = AnchorType.MineralRich,
                        Hex = PickAnchorHex(s, cell, 0),
                    });
            }

            // Precursor sites roll everywhere — a site deep in a void is a story (spec §5).
            double precursorChance = 0.02 + (cell.Lean == StellarLean.RemnantGraveyard ? 0.02 : 0.0);
            if (ctx.NextDouble(RollChannel.AnchorKind, 1) < precursorChance)
                cell.Anchors.Add(new Anchor
                {
                    Type = AnchorType.PrecursorSite,
                    Hex = PickAnchorHex(s, cell, 1),
                });
        }
    }

    /// <summary>Deterministic in-cell hex pick with forward-probe collision handling.</summary>
    internal static HexCoordinate PickAnchorHex(GalaxySkeleton s, RegionCell cell, int drawIndex)
    {
        var ctx = new RollContext(s.Config.MasterSeed, new HexCoordinate(cell.Cx, cell.Cy));
        int local = ctx.NextInt(RollChannel.AnchorPlacement, 0, 80, drawIndex);
        for (int probe = 0; probe < 80; probe++)
        {
            int slot = (local + probe) % 80;
            var hex = new HexCoordinate(cell.Cx * 8 + slot % 8, cell.Cy * 10 + slot / 8);
            bool taken = false;
            foreach (var a in cell.Anchors)
                if (a.Hex.Equals(hex)) { taken = true; break; }
            if (!taken) return hex;
        }
        // Unreachable: a cell never carries 80 anchors.
        return new HexCoordinate(cell.Cx * 8, cell.Cy * 10);
    }
```

- [ ] **Step 4: Run** — `dotnet test --filter SeedingPassTests` — Expected: PASS (7 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: resource anchor seeding with metallicity weighting"`

---

### Task 8: SkeletonBuilder pass 4 — homeworlds and species profiles

**Files:**
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (add `PassHomeworlds`, replace marker)
- Test: append to `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Consumes: `NameGenerator`-style syllables? No — species names use `Content.NameTables.Syllables` directly via cell-keyed rolls (`RollChannel.NameSyllable` with `index = 1000 + speciesId` to avoid colliding with system-name draws, which use index 0).
- Produces: `internal static void PassHomeworlds(GalaxySkeleton s)` — target count = `max(2, round(config.HomeworldRatePerSector * SizeSectors²))`; candidate cells = non-void, ordered by linear index; selection walks candidates in a deterministic hash-shuffled order (order key = `ctx.NextDouble(RollChannel.HomeworldPlacement)` per cell, ties by linear index) and accepts a cell iff its Chebyshev distance to every already-accepted homeworld cell ≥ `max(2, CellsX / (2 * targetCount) + 2)` (spacing-aware, spec §5); each accepted cell gets: a `SpeciesProfile` (embodiment from a weighted draw — TerranAnalog 40, Aquatic 15, Cryophilic 12, Lithic 15, Hive 10, Machine 8, but **embodiment is re-drawn up to 3 times if it mismatches the cell** — Cryophilic wants `OldDim`, Aquatic/Terran want `Balanced/YoungBright`, Machine wants a cell whose 8-neighborhood contains a PrecursorSite anchor, Lithic wants `Metallicity > 0.4`, Hive accepts anything; after 3 re-draws keep the last), six temperament scalars = `0.15 + 0.7 * ctx.NextDouble(SpeciesTemperament, subIndex: axis)` with hive-correlation override `Cohesion = max(Cohesion, 0.75)` when Hive; a `Polity` (id = speciesId = accept order, name = the species name (polity naming flavor is a follow-up), capital = the cell); a Homeworld `Anchor` at `PickAnchorHex(s, cell, 2)` with `SpeciesId` set; and `cell.OwnerPolityId = polity.Id`, `cell.DevelopmentTier = 2`.

- [ ] **Step 1: Write the failing tests** — append to `SeedingPassTests`:

```csharp
    [Fact]
    public void Homeworlds_CountAndSpacing()
    {
        var s = Build();
        int expected = System.Math.Max(2, (int)System.Math.Round(
            s.Config.HomeworldRatePerSector * s.Config.SizeSectors * s.Config.SizeSectors));
        Assert.InRange(s.Polities.Count, 2, expected);   // spacing may reject a few below target
        Assert.True(s.Polities.Count >= expected / 2, $"got {s.Polities.Count}, want >= {expected / 2}");
        var capitals = s.Polities.Select(p => (p.CapitalCx, p.CapitalCy)).ToList();
        foreach (var a in capitals)
            foreach (var b in capitals)
                if (a != b)
                    Assert.True(System.Math.Max(System.Math.Abs(a.CapitalCx - b.CapitalCx),
                                                System.Math.Abs(a.CapitalCy - b.CapitalCy)) >= 2,
                        "capitals must not be adjacent");
    }

    [Fact]
    public void Homeworlds_HaveSpeciesAnchorsAndOwnership()
    {
        var s = Build();
        foreach (var polity in s.Polities)
        {
            var species = s.Species.Single(sp => sp.Id == polity.SpeciesId);
            Assert.False(string.IsNullOrEmpty(species.Name));
            Assert.InRange(species.Cohesion, 0.0, 1.0);
            if (species.Embodiment == Embodiment.Hive) Assert.True(species.Cohesion >= 0.75);
            var cell = s.CellAt(polity.CapitalCx, polity.CapitalCy);
            Assert.Equal(polity.Id, cell.OwnerPolityId);
            Assert.Equal(2, cell.DevelopmentTier);
            Assert.Contains(cell.Anchors, a => a.Type == AnchorType.Homeworld && a.SpeciesId == species.Id);
        }
    }
```

- [ ] **Step 2: Run** — Expected: 2 new FAIL.

- [ ] **Step 3: Implement.** Replace the `// PassHomeworlds(skeleton);` marker and add (also `using System;`, `using System.Linq;`, `using StarGen.Core.Content;`, `using System.Globalization;`):

```csharp
    /// <summary>Spec §5 pass 4 + §6: homeworlds, species profiles, founding polities.</summary>
    internal static void PassHomeworlds(GalaxySkeleton s)
    {
        var config = s.Config;
        int target = Math.Max(2, (int)Math.Round(
            config.HomeworldRatePerSector * config.SizeSectors * config.SizeSectors));
        int minSpacing = Math.Max(2, config.CellsX / (2 * target) + 2);

        var candidates = s.Cells.Where(c => !c.IsVoid)
            .Select(c => (cell: c,
                order: new RollContext(config.MasterSeed, new HexCoordinate(c.Cx, c.Cy))
                    .NextDouble(RollChannel.HomeworldPlacement)))
            .OrderBy(t => t.order).ThenBy(t => t.cell.LinearIndex(config))
            .Select(t => t.cell);

        foreach (var cell in candidates)
        {
            if (s.Polities.Count >= target) break;
            bool tooClose = s.Polities.Any(p =>
                Math.Max(Math.Abs(p.CapitalCx - cell.Cx), Math.Abs(p.CapitalCy - cell.Cy)) < minSpacing);
            if (tooClose) continue;

            int id = s.Polities.Count;
            var species = RollSpecies(s, cell, id);
            s.Species.Add(species);
            s.Polities.Add(new Polity
            {
                Id = id, Name = species.Name, SpeciesId = id,
                CapitalCx = cell.Cx, CapitalCy = cell.Cy,
            });
            cell.Anchors.Add(new Anchor
            {
                Type = AnchorType.Homeworld, Hex = PickAnchorHex(s, cell, 2), SpeciesId = id,
            });
            cell.OwnerPolityId = id;
            cell.DevelopmentTier = 2;
        }
    }

    private static SpeciesProfile RollSpecies(GalaxySkeleton s, RegionCell cell, int id)
    {
        var config = s.Config;
        var ctx = new RollContext(config.MasterSeed, new HexCoordinate(cell.Cx, cell.Cy));
        var embodimentTable = new Tables.WeightedTable<Embodiment>(
            (Embodiment.TerranAnalog, 40), (Embodiment.Aquatic, 15), (Embodiment.Cryophilic, 12),
            (Embodiment.Lithic, 15), (Embodiment.Hive, 10), (Embodiment.Machine, 8));

        Embodiment embodiment = Embodiment.TerranAnalog;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            embodiment = embodimentTable.Pick(ctx.NextDouble(RollChannel.SpeciesEmbodiment, attempt));
            if (FitsCell(s, cell, embodiment)) break;
        }

        double Axis(int i) => 0.15 + 0.7 * ctx.NextDouble(RollChannel.SpeciesTemperament, 0, i);
        var species = new SpeciesProfile
        {
            Id = id, Embodiment = embodiment,
            Expansionism = Axis(0), Cohesion = Axis(1), Militancy = Axis(2),
            Openness = Axis(3), Industry = Axis(4), Adaptability = Axis(5),
            Name = SpeciesName(ctx, id),
        };
        if (embodiment == Embodiment.Hive)
            species.Cohesion = Math.Max(species.Cohesion, 0.75);   // hive correlation (spec §6)
        return species;
    }

    private static bool FitsCell(GalaxySkeleton s, RegionCell cell, Embodiment e) => e switch
    {
        Embodiment.Cryophilic => cell.Lean == StellarLean.OldDim,
        Embodiment.Aquatic or Embodiment.TerranAnalog =>
            cell.Lean == StellarLean.Balanced || cell.Lean == StellarLean.YoungBright,
        Embodiment.Lithic => cell.Metallicity > 0.4,
        Embodiment.Machine => NeighborhoodHasPrecursor(s, cell),
        _ => true,
    };

    private static bool NeighborhoodHasPrecursor(GalaxySkeleton s, RegionCell cell)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int cx = cell.Cx + dx, cy = cell.Cy + dy;
                if (cx < 0 || cy < 0 || cx >= s.Config.CellsX || cy >= s.Config.CellsY) continue;
                if (s.CellAt(cx, cy).Anchors.Any(a => a.Type == AnchorType.PrecursorSite)) return true;
            }
        return false;
    }

    private static string SpeciesName(RollContext ctx, int id)
    {
        int syllables = 2 + (ctx.NextDouble(RollChannel.NameLength, 1000 + id) < 0.4 ? 1 : 0);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(ctx.NextDouble(RollChannel.NameSyllable, 1000 + id, i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
```

- [ ] **Step 4: Run** — `dotnet test --filter SeedingPassTests` — Expected: PASS (9 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: homeworld seeding with species profiles and founding polities"`

---
### Task 9: EpochSim stage 1 — expansion, development, minimal war

**Files:**
- Create: `src/Core/Galaxy/EpochSim.cs`
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (replace the `// EpochSim.Run(skeleton);` marker)
- Test: `tests/Core.Tests/Galaxy/EpochSimTests.cs`

**Interfaces:**
- Consumes: skeleton model, `RollContext`, `RollChannel.SimExpansion/SimDevelopment/SimWar`.
- Produces: `static class EpochSim` with `static void Run(GalaxySkeleton s)` — `config.EpochCount` epochs, fixed order (polities by id; a polity's candidate cells by linear index). Per epoch, per living polity:
  1. **Expansion** — budget = `2 + min(3, totalDevelopment / 10)` points. Frontier = unclaimed non-void cells 4-adjacent to owned cells. Cost per cell = `1.0 / (0.05 + cell.MeanDensity * Affinity(species, cell))` (+2.0 if `cell.IsChokepoint` — defended ground). Claim cheapest-first (ties by linear index) while budget covers cost; each claim sets owner, `DevelopmentTier = 1`, emits `CellClaimed`.
  2. **Development** — each owned cell: if `ctx.NextDouble(SimDevelopment, epoch, cellLinearIndex) < species.Industry * 0.5` then `DevelopmentTier = min(5, DevelopmentTier + 1)`.
  3. **War (minimal stage 1)** — if the polity borders a foreign polity and `ctx.NextDouble(SimWar, epoch, polityId) < species.Militancy * 0.25`: pick the lowest-linear-index adjacent foreign cell; attacker strength = `totalDev * (0.5 + militancy)`, defender strength = defender's `totalDev * (0.5 + defMilitancy)`; if attacker strength > defender strength the cell flips (`CellTaken`, `WarScarred = true`, and `WarStarted` emitted the first time this pair fights this epoch); else the target cell only gets `WarScarred = true`. If the flipped cell was the defender's capital: `LostCapital` event + capital relocates to the defender's highest-development remaining cell (ties by linear index); if the defender has no cells left: `Extinct = true` + `PolityExtinct` event (registry retains it, spec §7 lifecycle).
  - `Affinity(SpeciesProfile species, RegionCell cell)` (internal, also used by tests): base by embodiment×lean — TerranAnalog: Balanced 1.0 / YoungBright 1.15 / OldDim 0.8 / Remnant 0.4; Aquatic: 1.0 / 1.3 / 0.6 / 0.3; Cryophilic: 0.7 / 0.6 / 1.3 / 0.9; Lithic: `0.5 + cell.Metallicity` regardless of lean; Hive: 1.0 flat; Machine: 1.0 flat; then `affinity = base + (1 - base) * species.Adaptability * 0.5` (adaptability discounts hostility, spec §6.2).
  - The sim uses one `RollContext` per polity-epoch decision keyed on the polity's **capital cell coords** — `new RollContext(seed, new HexCoordinate(capitalCx, capitalCy))` — with epoch/ids in index/subIndex as annotated on the channels.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/EpochSimTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class EpochSimTests
{
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, SizeSectors = 4 });

    [Fact]
    public void Sim_IsDeterministic()
    {
        var a = Build();
        var b = Build();
        for (int i = 0; i < a.Cells.Length; i++)
        {
            Assert.Equal(a.Cells[i].OwnerPolityId, b.Cells[i].OwnerPolityId);
            Assert.Equal(a.Cells[i].DevelopmentTier, b.Cells[i].DevelopmentTier);
        }
        Assert.Equal(a.Events.Count, b.Events.Count);
    }

    [Fact]
    public void Polities_Expand_ButWildsRemain()
    {
        var s = Build();
        var claimable = s.Cells.Where(c => !c.IsVoid).ToList();
        int claimed = claimable.Count(c => c.OwnerPolityId >= 0);
        Assert.True(claimed > s.Polities.Count, "polities should grow beyond their homeworlds");
        Assert.True(claimed < claimable.Count, "unclaimed wilds must remain (spec §7.8)");
    }

    [Fact]
    public void OwnedCells_TraceToRegistry_AndVoidsStayUnclaimed()
    {
        var s = Build();
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId >= 0)
                Assert.Contains(s.Polities, p => p.Id == cell.OwnerPolityId);
            if (cell.IsVoid)
                Assert.Equal(-1, cell.OwnerPolityId);
        }
    }

    [Fact]
    public void EventLog_IsChronological_AndReferentiallyIntact()
    {
        var s = Build();
        Assert.NotEmpty(s.Events);
        int lastEpoch = 0;
        foreach (var e in s.Events)
        {
            Assert.True(e.Epoch >= lastEpoch, "event log must be chronological");
            lastEpoch = e.Epoch;
            Assert.Contains(s.Polities, p => p.Id == e.ActorPolityId);
            Assert.InRange(e.Cx, 0, s.Config.CellsX - 1);
            Assert.InRange(e.Cy, 0, s.Config.CellsY - 1);
        }
    }

    [Fact]
    public void ExtinctPolities_AreRetainedInRegistry()
    {
        // Whether extinction happens depends on the seed; assert the invariant holds
        // across several seeds and that at least the registry never shrinks.
        for (ulong seed = 40; seed < 46; seed++)
        {
            var s = Build(seed);
            foreach (var e in s.Events.Where(e => e.Type == GalaxyEventType.PolityExtinct))
            {
                var polity = s.Polities.Single(p => p.Id == e.TargetPolityId);
                Assert.True(polity.Extinct);
                Assert.DoesNotContain(s.Cells, c => c.OwnerPolityId == polity.Id);
            }
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter EpochSimTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/EpochSim.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Stage-1 epoch loop (spec §7.9 stage 1): expansion, development, minimal
/// war, flat budgets. Stockpiles/commodities/diplomacy arrive in later stages.</summary>
public static class EpochSim
{
    public static void Run(GalaxySkeleton s)
    {
        for (int epoch = 0; epoch < s.Config.EpochCount; epoch++)
            foreach (var polity in s.Polities)   // fixed order: by id
            {
                if (polity.Extinct) continue;
                Expand(s, polity, epoch);
                Develop(s, polity, epoch);
                War(s, polity, epoch);
            }
    }

    private static RollContext Ctx(GalaxySkeleton s, Polity p) =>
        new(s.Config.MasterSeed, new HexCoordinate(p.CapitalCx, p.CapitalCy));

    private static List<RegionCell> Owned(GalaxySkeleton s, Polity p) =>
        s.Cells.Where(c => c.OwnerPolityId == p.Id).ToList();

    private static IEnumerable<RegionCell> Adjacent(GalaxySkeleton s, RegionCell cell)
    {
        if (cell.Cx > 0) yield return s.CellAt(cell.Cx - 1, cell.Cy);
        if (cell.Cx < s.Config.CellsX - 1) yield return s.CellAt(cell.Cx + 1, cell.Cy);
        if (cell.Cy > 0) yield return s.CellAt(cell.Cx, cell.Cy - 1);
        if (cell.Cy < s.Config.CellsY - 1) yield return s.CellAt(cell.Cx, cell.Cy + 1);
    }

    internal static double Affinity(SpeciesProfile species, RegionCell cell)
    {
        double baseAffinity = species.Embodiment switch
        {
            Embodiment.TerranAnalog => cell.Lean switch
            {
                StellarLean.YoungBright => 1.15, StellarLean.OldDim => 0.8,
                StellarLean.RemnantGraveyard => 0.4, _ => 1.0,
            },
            Embodiment.Aquatic => cell.Lean switch
            {
                StellarLean.YoungBright => 1.3, StellarLean.OldDim => 0.6,
                StellarLean.RemnantGraveyard => 0.3, _ => 1.0,
            },
            Embodiment.Cryophilic => cell.Lean switch
            {
                StellarLean.YoungBright => 0.6, StellarLean.OldDim => 1.3,
                StellarLean.RemnantGraveyard => 0.9, _ => 0.7,
            },
            Embodiment.Lithic => 0.5 + cell.Metallicity,
            _ => 1.0,   // Hive, Machine: broad tolerance
        };
        return baseAffinity + (1 - baseAffinity) * species.Adaptability * 0.5;
    }

    private static void Expand(GalaxySkeleton s, Polity polity, int epoch)
    {
        var species = s.Species[polity.SpeciesId];
        var owned = Owned(s, polity);
        if (owned.Count == 0) return;
        double budget = 2 + Math.Min(3, owned.Sum(c => c.DevelopmentTier) / 10.0);

        var frontier = owned.SelectMany(c => Adjacent(s, c))
            .Where(c => c.OwnerPolityId < 0 && !c.IsVoid)
            .Distinct()
            .Select(c => (cell: c, cost: Cost(species, c)))
            .OrderBy(t => t.cost).ThenBy(t => t.cell.LinearIndex(s.Config))
            .ToList();

        foreach (var (cell, cost) in frontier)
        {
            if (budget < cost) break;
            budget -= cost;
            cell.OwnerPolityId = polity.Id;
            cell.DevelopmentTier = 1;
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.CellClaimed,
                ActorPolityId = polity.Id, Cx = cell.Cx, Cy = cell.Cy,
            });
        }
    }

    private static double Cost(SpeciesProfile species, RegionCell cell) =>
        1.0 / (0.05 + cell.MeanDensity * Affinity(species, cell))
        + (cell.IsChokepoint ? 2.0 : 0.0);

    private static void Develop(GalaxySkeleton s, Polity polity, int epoch)
    {
        var species = s.Species[polity.SpeciesId];
        var ctx = Ctx(s, polity);
        foreach (var cell in Owned(s, polity))
            if (ctx.NextDouble(RollChannel.SimDevelopment, epoch, cell.LinearIndex(s.Config))
                < species.Industry * 0.5)
                cell.DevelopmentTier = Math.Min(5, cell.DevelopmentTier + 1);
    }

    private static void War(GalaxySkeleton s, Polity polity, int epoch)
    {
        var species = s.Species[polity.SpeciesId];
        var ctx = Ctx(s, polity);
        if (ctx.NextDouble(RollChannel.SimWar, epoch, polity.Id) >= species.Militancy * 0.25)
            return;

        var owned = Owned(s, polity);
        var target = owned.SelectMany(c => Adjacent(s, c))
            .Where(c => c.OwnerPolityId >= 0 && c.OwnerPolityId != polity.Id)
            .OrderBy(c => c.LinearIndex(s.Config))
            .FirstOrDefault();
        if (target == null) return;

        var defender = s.Polities[target.OwnerPolityId];
        var defSpecies = s.Species[defender.SpeciesId];
        double attack = owned.Sum(c => c.DevelopmentTier) * (0.5 + species.Militancy);
        double defense = Owned(s, defender).Sum(c => c.DevelopmentTier) * (0.5 + defSpecies.Militancy);

        s.Events.Add(new GalaxyEvent
        {
            Epoch = epoch, Type = GalaxyEventType.WarStarted,
            ActorPolityId = polity.Id, TargetPolityId = defender.Id,
            Cx = target.Cx, Cy = target.Cy, Magnitude = attack + defense,
        });
        target.WarScarred = true;
        if (attack <= defense) return;

        target.OwnerPolityId = polity.Id;
        s.Events.Add(new GalaxyEvent
        {
            Epoch = epoch, Type = GalaxyEventType.CellTaken,
            ActorPolityId = polity.Id, TargetPolityId = defender.Id,
            Cx = target.Cx, Cy = target.Cy, Magnitude = attack - defense,
        });

        if (defender.CapitalCx == target.Cx && defender.CapitalCy == target.Cy)
        {
            var remaining = Owned(s, defender)
                .OrderByDescending(c => c.DevelopmentTier)
                .ThenBy(c => c.LinearIndex(s.Config))
                .FirstOrDefault();
            if (remaining != null)
            {
                defender.CapitalCx = remaining.Cx;
                defender.CapitalCy = remaining.Cy;
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.LostCapital,
                    ActorPolityId = polity.Id, TargetPolityId = defender.Id,
                    Cx = target.Cx, Cy = target.Cy,
                });
            }
        }
        if (!Owned(s, defender).Any())
        {
            defender.Extinct = true;   // retained in registry, flagged (spec §7)
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.PolityExtinct,
                ActorPolityId = polity.Id, TargetPolityId = defender.Id,
                Cx = target.Cx, Cy = target.Cy,
            });
        }
    }
}
```

In `SkeletonBuilder.Build`, replace `// EpochSim.Run(skeleton);` with the real call.

- [ ] **Step 4: Run** — `dotnet test --filter EpochSimTests` — Expected: PASS (5 tests). Full suite green. If `Polities_Expand_ButWildsRemain` fails on the wilds side, lower the expansion budget constant `2` to `1.5`; if on the growth side, raise it to `3`. Record any change.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: stage-1 epoch sim with expansion, development, minimal war"`

---

### Task 10: Skeleton serialization with version stamps + golden snapshot

**Files:**
- Create: `src/Core/Galaxy/SkeletonSerializer.cs`
- Test: `tests/Core.Tests/Galaxy/SerializerTests.cs`

**Interfaces:**
- Consumes: full skeleton model.
- Produces: `static class SkeletonSerializer` — `void Save(GalaxySkeleton s, TextWriter writer)`, `GalaxySkeleton Load(TextReader reader)` (throws `InvalidDataException` on schema-version mismatch — the caller decides rebuild-vs-keep, spec §3.1; loading never silently rebuilds), `string ToText(GalaxySkeleton s)` convenience. Format: line-based invariant-culture text. Layout (one item per line, pipe-separated fields):

```
STARGEN-SKELETON|1                      <- format tag | SchemaVersion
CONFIG|seed|sizeSectors|meanDensity|armCount|armTightness|armWidth|epochs|yearsPerEpoch|homeworldRate|traversability
SPECIES|id|name|embodiment|exp|coh|mil|open|ind|adapt
POLITY|id|name|speciesId|capCx|capCy|extinct
CELL|cx|cy|meanDensity|isVoid|isChokepoint|lean|metallicity|owner|dev|contested|warScarred
ANCHOR|cx|cy|type|hexX|hexY|speciesId
EVENT|epoch|type|actor|target|cx|cy|magnitude
END
```

Doubles use `R` round-trip format with `CultureInfo.InvariantCulture`; bools as 0/1; enums by integer value. Cells written in linear-index order; anchors written after their cell ordering (cell linear order, then list order); species/polities/events in list order. This fixed ordering is what makes serialization byte-identical for identical skeletons.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/SerializerTests.cs`:

```csharp
using System.IO;
using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SerializerTests
{
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, SizeSectors = 4 });

    [Fact]
    public void SameConfig_ByteIdenticalSerialization()
    {
        Assert.Equal(SkeletonSerializer.ToText(Build()), SkeletonSerializer.ToText(Build()));
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = Build();
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(original)));
        Assert.Equal(SkeletonSerializer.ToText(original), SkeletonSerializer.ToText(loaded));
        Assert.Equal(original.Polities.Count, loaded.Polities.Count);
        Assert.Equal(original.Events.Count, loaded.Events.Count);
        Assert.Equal(original.Cells.Length, loaded.Cells.Length);
        Assert.Equal(original.Config.MasterSeed, loaded.Config.MasterSeed);
    }

    [Fact]
    public void SchemaVersionMismatch_Throws_NeverSilentlyRebuilds()
    {
        var text = SkeletonSerializer.ToText(Build());
        var tampered = text.Replace("STARGEN-SKELETON|1", "STARGEN-SKELETON|999");
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(tampered)));
    }

    [Fact]
    public void GoldenSnapshot_SmallGalaxyHeader()
    {
        // Golden guard against unintended drift (spec §10). If this fails because of an
        // INTENTIONAL generation change, update the literal and say so in the commit.
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 7, SizeSectors = 2 });
        var lines = SkeletonSerializer.ToText(s).Split('\n');
        Assert.Equal("STARGEN-SKELETON|1", lines[0].TrimEnd('\r'));
        // Golden facts recorded at implementation time — fill the two literals with the
        // observed values on first run, then they are frozen:
        // Assert.Equal(<observed polity count>, s.Polities.Count);
        // Assert.Equal(<observed event count>, s.Events.Count);
    }
}
```

Implementation note for Step 3 of the golden test: run once, print `s.Polities.Count` and `s.Events.Count`, replace the two commented assertions with the real observed literals, and keep them. That is the golden freeze.

- [ ] **Step 2: Run** — `dotnet test --filter SerializerTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/SkeletonSerializer.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Versioned artifact serialization (spec §3.1). Line-based, invariant culture,
/// fixed ordering so identical skeletons serialize byte-identically.</summary>
public static class SkeletonSerializer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string ToText(GalaxySkeleton s)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        Save(s, writer);
        return writer.ToString();
    }

    public static void Save(GalaxySkeleton s, TextWriter w)
    {
        w.NewLine = "\n";
        var c = s.Config;
        w.WriteLine($"STARGEN-SKELETON|{GalaxySkeleton.SchemaVersion}");
        w.WriteLine(string.Join("|", "CONFIG",
            c.MasterSeed.ToString(Inv), c.SizeSectors.ToString(Inv),
            c.MeanDensityTarget.ToString("R", Inv), c.ArmCount.ToString(Inv),
            c.ArmTightness.ToString("R", Inv), c.ArmWidth.ToString("R", Inv),
            c.EpochCount.ToString(Inv), c.YearsPerEpoch.ToString(Inv),
            c.HomeworldRatePerSector.ToString("R", Inv),
            c.TraversabilityThreshold.ToString("R", Inv)));
        foreach (var sp in s.Species)
            w.WriteLine(string.Join("|", "SPECIES", sp.Id.ToString(Inv), sp.Name,
                ((int)sp.Embodiment).ToString(Inv),
                sp.Expansionism.ToString("R", Inv), sp.Cohesion.ToString("R", Inv),
                sp.Militancy.ToString("R", Inv), sp.Openness.ToString("R", Inv),
                sp.Industry.ToString("R", Inv), sp.Adaptability.ToString("R", Inv)));
        foreach (var p in s.Polities)
            w.WriteLine(string.Join("|", "POLITY", p.Id.ToString(Inv), p.Name,
                p.SpeciesId.ToString(Inv), p.CapitalCx.ToString(Inv),
                p.CapitalCy.ToString(Inv), p.Extinct ? "1" : "0"));
        foreach (var cell in s.Cells)
        {
            w.WriteLine(string.Join("|", "CELL", cell.Cx.ToString(Inv), cell.Cy.ToString(Inv),
                cell.MeanDensity.ToString("R", Inv), cell.IsVoid ? "1" : "0",
                cell.IsChokepoint ? "1" : "0", ((int)cell.Lean).ToString(Inv),
                cell.Metallicity.ToString("R", Inv), cell.OwnerPolityId.ToString(Inv),
                cell.DevelopmentTier.ToString(Inv), cell.Contested ? "1" : "0",
                cell.WarScarred ? "1" : "0"));
            foreach (var a in cell.Anchors)
                w.WriteLine(string.Join("|", "ANCHOR", cell.Cx.ToString(Inv),
                    cell.Cy.ToString(Inv), ((int)a.Type).ToString(Inv),
                    a.Hex.X.ToString(Inv), a.Hex.Y.ToString(Inv), a.SpeciesId.ToString(Inv)));
        }
        foreach (var e in s.Events)
            w.WriteLine(string.Join("|", "EVENT", e.Epoch.ToString(Inv),
                ((int)e.Type).ToString(Inv), e.ActorPolityId.ToString(Inv),
                e.TargetPolityId.ToString(Inv), e.Cx.ToString(Inv), e.Cy.ToString(Inv),
                e.Magnitude.ToString("R", Inv)));
        w.WriteLine("END");
    }

    public static GalaxySkeleton Load(TextReader reader)
    {
        string header = reader.ReadLine()
            ?? throw new InvalidDataException("empty skeleton artifact");
        var headerParts = header.Split('|');
        if (headerParts.Length != 2 || headerParts[0] != "STARGEN-SKELETON")
            throw new InvalidDataException("not a skeleton artifact");
        if (int.Parse(headerParts[1], Inv) != GalaxySkeleton.SchemaVersion)
            throw new InvalidDataException(
                $"schema version {headerParts[1]} != {GalaxySkeleton.SchemaVersion}; " +
                "keep the artifact with matching code or explicitly regenerate (spec §3.1)");

        GalaxySkeleton? s = null;
        string? line;
        while ((line = reader.ReadLine()) != null && line != "END")
        {
            var f = line.Split('|');
            switch (f[0])
            {
                case "CONFIG":
                    s = new GalaxySkeleton(new GalaxyConfig
                    {
                        MasterSeed = ulong.Parse(f[1], Inv), SizeSectors = int.Parse(f[2], Inv),
                        MeanDensityTarget = double.Parse(f[3], Inv), ArmCount = int.Parse(f[4], Inv),
                        ArmTightness = double.Parse(f[5], Inv), ArmWidth = double.Parse(f[6], Inv),
                        EpochCount = int.Parse(f[7], Inv), YearsPerEpoch = int.Parse(f[8], Inv),
                        HomeworldRatePerSector = double.Parse(f[9], Inv),
                        TraversabilityThreshold = double.Parse(f[10], Inv),
                    });
                    break;
                case "SPECIES":
                    s!.Species.Add(new SpeciesProfile
                    {
                        Id = int.Parse(f[1], Inv), Name = f[2],
                        Embodiment = (Embodiment)int.Parse(f[3], Inv),
                        Expansionism = double.Parse(f[4], Inv), Cohesion = double.Parse(f[5], Inv),
                        Militancy = double.Parse(f[6], Inv), Openness = double.Parse(f[7], Inv),
                        Industry = double.Parse(f[8], Inv), Adaptability = double.Parse(f[9], Inv),
                    });
                    break;
                case "POLITY":
                    s!.Polities.Add(new Polity
                    {
                        Id = int.Parse(f[1], Inv), Name = f[2], SpeciesId = int.Parse(f[3], Inv),
                        CapitalCx = int.Parse(f[4], Inv), CapitalCy = int.Parse(f[5], Inv),
                        Extinct = f[6] == "1",
                    });
                    break;
                case "CELL":
                    var cell = s!.CellAt(int.Parse(f[1], Inv), int.Parse(f[2], Inv));
                    cell.MeanDensity = double.Parse(f[3], Inv);
                    cell.IsVoid = f[4] == "1";
                    cell.IsChokepoint = f[5] == "1";
                    cell.Lean = (StellarLean)int.Parse(f[6], Inv);
                    cell.Metallicity = double.Parse(f[7], Inv);
                    cell.OwnerPolityId = int.Parse(f[8], Inv);
                    cell.DevelopmentTier = int.Parse(f[9], Inv);
                    cell.Contested = f[10] == "1";
                    cell.WarScarred = f[11] == "1";
                    break;
                case "ANCHOR":
                    s!.CellAt(int.Parse(f[1], Inv), int.Parse(f[2], Inv)).Anchors.Add(new Anchor
                    {
                        Type = (AnchorType)int.Parse(f[3], Inv),
                        Hex = new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                        SpeciesId = int.Parse(f[6], Inv),
                    });
                    break;
                case "EVENT":
                    s!.Events.Add(new GalaxyEvent
                    {
                        Epoch = int.Parse(f[1], Inv), Type = (GalaxyEventType)int.Parse(f[2], Inv),
                        ActorPolityId = int.Parse(f[3], Inv), TargetPolityId = int.Parse(f[4], Inv),
                        Cx = int.Parse(f[5], Inv), Cy = int.Parse(f[6], Inv),
                        Magnitude = double.Parse(f[7], Inv),
                    });
                    break;
            }
        }
        return s ?? throw new InvalidDataException("artifact missing CONFIG line");
    }
}
```

Note: species/polity names must not contain `|` — the syllable tables can't produce one, so no escaping is needed; assert nothing, it's structurally impossible.

- [ ] **Step 4: Run + freeze golden.** `dotnet test --filter SerializerTests` — after filling the golden literals per the note in Step 1, Expected: PASS (4 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: versioned skeleton serialization with golden snapshot"`

---
### Task 11: RegionContext — per-hex integration (modifiers + pre-commitments)

**Files:**
- Create: `src/Core/Galaxy/RegionContext.cs`
- Modify: `src/Core/Generation/Generator.cs`, `src/Core/Generation/StarGenerator.cs`, `src/Core/Generation/BodyGenerator.cs`, `src/Core/Overlays/OverlayResolver.cs`
- Test: `tests/Core.Tests/Galaxy/RegionIntegrationTests.cs`

**Interfaces:**
- Consumes: skeleton model, existing pipeline, `WeightedTable` modifier hooks, `StarTypeDef` (has `Id` — e.g. `"ember_dwarf"`, `"gold_main"`, `"ashen_remnant"`, `"collapsed_core"`, `"white_blaze"`, `"blue_titan"`, `"amber_dwarf"`), `BodyTables.Kind` (`WeightedTable<BodyKind?>`).
- Produces:
  - `sealed class RegionContext` — built by `static RegionContext? For(GalaxyContext galaxy, HexCoordinate hex)` (null when flatspace or no skeleton). Members:
    - `Func<StarTypeDef, double> StarTypeModifier` — from cell `Lean`: YoungBright → `gold_main`/`white_blaze`/`blue_titan` ×2.0, `ashen_remnant`/`collapsed_core` ×0.3; OldDim → `ember_dwarf`/`amber_dwarf` ×2.0, `gold_main` ×0.6, `white_blaze`/`blue_titan` ×0.2; RemnantGraveyard → `ashen_remnant`/`collapsed_core` ×4.0, all main-sequence ×0.4; Balanced → 1.0. (Natural modifier: star-type only — spec §8.)
    - `Func<BodyKind?, double> BeltModifier` — `PlanetoidBelt` ×`(0.5 + Metallicity)`; everything else ×1.0. (Natural: belt weights follow metallicity.)
    - `double SettlementScale` — political: **bilinear interpolation over the four nearest cell centers** (spec §8 smoothing) of per-cell `devScale = cell.OwnerPolityId >= 0 ? 1.0 + 0.8 * cell.DevelopmentTier : (cell.WarScarred ? 0.4 : 1.0)`; cells out of bounds contribute their nearest in-bounds neighbor's value. Applied as a multiplier on settled tiers only (never `Settlement.None`).
    - `Anchor? AnchorAt` — the cell's anchor at exactly this hex, if any.
    - `int OwnerPolityId`, `bool WarScarred` — pass-through display/state facts (`OwnerPolityId` from the hex's own cell, not interpolated — borders are hard, spec §8).
  - Pipeline changes:
    - `StarGenerator.Generate(RollContext ctx, StarSystem system, RegionContext? region = null)` — the two `StarTypes.Table.Pick(roll)` calls become `Pick(roll, region?.StarTypeModifier)` (a null modifier arg is the existing overload behavior — `Pick(roll, null)` is already legal).
    - `BodyGenerator.Generate(RollContext ctx, StarSystem system, RegionContext? region = null)` — the `BodyTables.Kind.Pick(roll, KindModifier(band))` call becomes a composed modifier: `k => BodyTables.KindModifier(slot.Band)(k) * (region?.BeltModifier(k) ?? 1.0)`; the settlement pick inside `GenerateBody` needs the scale, so `GenerateBody` gains `double settlementScale = 1.0` parameter and composes `st => BodyTables.SettlementModifier(bio, band)(st) * (st == Settlement.None ? 1.0 : settlementScale)`.
    - `OverlayResolver.Resolve(RollContext ctx, StarSystem system, bool anchored = false)` — anchored systems skip the overlay roll entirely (spec §8: historical POIs and random mysteries don't pile up).
    - `Generator.Generate(GalaxyContext, coord)` — builds `region = RegionContext.For(galaxy, coord)`; passes it to Star/Body generators; **pre-commitments applied after naming, before overlays**: if `region?.AnchorAt` is a `MineralRich` anchor → ensure presence was forced (see below) + `system.Tags.Add("mineral-rich")`; `PrecursorSite` → `system.Tags.Add("precursor site")`; `Homeworld` → tag `"homeworld"` + force the best rocky/ice world (highest `Size`, ties by orbit order; if none exists, the first body of any kind — a homeworld system generated starless is impossible since presence is forced): `Biosphere = Sapient`, `Settlement = MajorWorld`, re-run `SocietyGenerator.Generate` + `NameGenerator.AssignNames` afterward so society/names reflect the forced facts (both are deterministic and idempotent-safe to re-run: society attaches only where missing? NO — society would double-attach; instead call the targeted helpers: set `body.Society = null` for the forced body before re-running `SocietyGenerator.Generate(ctx, system)`, and `NameGenerator` only fills nulls so calling `AssignNames` again is safe). **Presence forcing:** if `region?.AnchorAt != null`, skip the presence check entirely — anchored hexes always have systems (spec §5).
- The composed-modifier approach means **no existing RollChannel usage changes** — flatspace output stays bit-identical (modifiers are all ×1.0 or absent in flatspace).

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/RegionIntegrationTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class RegionIntegrationTests
{
    private static GalaxyContext Galaxy(ulong seed = 42)
    {
        var config = new GalaxyConfig { MasterSeed = seed, SizeSectors = 4 };
        return new GalaxyContext(config) { Skeleton = SkeletonBuilder.Build(config) };
    }

    [Fact]
    public void AnchoredHexes_AlwaysHaveSystems_WithAnchorTags()
    {
        var galaxy = Galaxy();
        foreach (var cell in galaxy.Skeleton!.Cells)
            foreach (var anchor in cell.Anchors)
            {
                var result = Generator.Generate(galaxy, anchor.Hex);
                Assert.False(result.IsEmpty, $"anchored hex {anchor.Hex} must have a system");
                string expectedTag = anchor.Type switch
                {
                    AnchorType.MineralRich => "mineral-rich",
                    AnchorType.PrecursorSite => "precursor site",
                    _ => "homeworld",
                };
                Assert.Contains(expectedTag, result.System!.Tags);
                Assert.Null(result.System.OverlayId);   // anchored => no random overlay
            }
    }

    [Fact]
    public void Homeworlds_HaveSapientMajorWorld_AndName()
    {
        var galaxy = Galaxy();
        var homeworlds = galaxy.Skeleton!.Cells.SelectMany(c => c.Anchors)
            .Where(a => a.Type == AnchorType.Homeworld).ToList();
        Assert.NotEmpty(homeworlds);
        foreach (var anchor in homeworlds)
        {
            var system = Generator.Generate(galaxy, anchor.Hex).System!;
            var forced = system.Stars.SelectMany(st => st.Slots)
                .Where(sl => sl.Body != null).Select(sl => sl.Body!)
                .FirstOrDefault(b => b.Biosphere == Biosphere.Sapient
                                     && b.Settlement == Settlement.MajorWorld);
            Assert.NotNull(forced);
            Assert.NotNull(forced!.Society);
            Assert.NotNull(system.GivenName);
        }
    }

    [Fact]
    public void RemnantGraveyards_SkewTowardDeadStars()
    {
        var galaxy = Galaxy();
        int deadInGraveyards = 0, totalInGraveyards = 0, deadElsewhere = 0, totalElsewhere = 0;
        foreach (var cell in galaxy.Skeleton!.Cells)
        {
            if (cell.MeanDensity < 0.2) continue;
            for (int hx = cell.Cx * 8; hx < cell.Cx * 8 + 8; hx++)
                for (int hy = cell.Cy * 10; hy < cell.Cy * 10 + 10; hy++)
                {
                    var system = Generator.Generate(galaxy, new HexCoordinate(hx, hy)).System;
                    if (system == null) continue;
                    bool dead = system.Stars[0].TypeId is "ashen_remnant" or "collapsed_core";
                    if (cell.Lean == StellarLean.RemnantGraveyard) { totalInGraveyards++; if (dead) deadInGraveyards++; }
                    else if (cell.Lean == StellarLean.Balanced) { totalElsewhere++; if (dead) deadElsewhere++; }
                }
        }
        if (totalInGraveyards < 30) return;   // seed produced too few graveyard systems to compare
        Assert.True(deadInGraveyards / (double)totalInGraveyards > deadElsewhere / (double)totalElsewhere,
            "graveyard cells should host more dead stars than balanced cells");
    }

    [Fact]
    public void SettlementScale_RaisesSettlementInsidePolities()
    {
        var galaxy = Galaxy();
        int settledOwned = 0, totalOwned = 0, settledWild = 0, totalWild = 0;
        foreach (var cell in galaxy.Skeleton!.Cells.Where(c => !c.IsVoid))
        {
            bool owned = cell.OwnerPolityId >= 0 && cell.DevelopmentTier >= 3;
            bool wild = cell.OwnerPolityId < 0;
            if (!owned && !wild) continue;
            for (int hx = cell.Cx * 8; hx < cell.Cx * 8 + 8; hx += 2)
                for (int hy = cell.Cy * 10; hy < cell.Cy * 10 + 10; hy += 2)
                {
                    var system = Generator.Generate(galaxy, new HexCoordinate(hx, hy)).System;
                    if (system == null) continue;
                    bool settled = system.Stars.SelectMany(st => st.Slots)
                        .Any(sl => sl.Body != null && sl.Body.Settlement != Settlement.None);
                    if (owned) { totalOwned++; if (settled) settledOwned++; }
                    else { totalWild++; if (settled) settledWild++; }
                }
        }
        Assert.True(totalOwned > 20 && totalWild > 20, "need enough samples on both sides");
        Assert.True(settledOwned / (double)totalOwned > settledWild / (double)totalWild,
            $"developed cells ({settledOwned}/{totalOwned}) should out-settle wilds ({settledWild}/{totalWild})");
    }

    [Fact]
    public void Flatspace_RemainsBitIdentical_ToLegacy()
    {
        for (int x = 0; x < 200; x++)
        {
            var coord = new HexCoordinate(x, 7);
            var legacy = Generator.Generate(17UL, coord);
            var flat = Generator.Generate(GalaxyContext.Flatspace(17), coord);
            Assert.Equal(
                StarGen.Core.Text.SystemFormatter.Format(legacy),
                StarGen.Core.Text.SystemFormatter.Format(flat));
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter RegionIntegrationTests` — Expected: FAIL.

- [ ] **Step 3: Implement.** Create `src/Core/Galaxy/RegionContext.cs`:

```csharp
using System;
using StarGen.Core.Content;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Per-hex regional read (spec §8): natural + political modifiers, pre-commitments.</summary>
public sealed class RegionContext
{
    public Func<StarTypeDef, double> StarTypeModifier { get; private set; } = _ => 1.0;
    public Func<BodyKind?, double> BeltModifier { get; private set; } = _ => 1.0;
    public double SettlementScale { get; private set; } = 1.0;
    public Anchor? AnchorAt { get; private set; }
    public int OwnerPolityId { get; private set; } = -1;
    public bool WarScarred { get; private set; }

    public static RegionContext? For(GalaxyContext galaxy, HexCoordinate hex)
    {
        if (galaxy.IsFlatspace || galaxy.Skeleton == null) return null;
        var s = galaxy.Skeleton;
        if (hex.X < 0 || hex.Y < 0
            || hex.X >= galaxy.Config.WidthHexes || hex.Y >= galaxy.Config.HeightHexes)
            return null;
        var cell = s.CellForHex(hex);

        var region = new RegionContext
        {
            StarTypeModifier = LeanModifier(cell.Lean),
            BeltModifier = k => k == BodyKind.PlanetoidBelt ? 0.5 + cell.Metallicity : 1.0,
            SettlementScale = InterpolatedSettlementScale(s, hex),
            OwnerPolityId = cell.OwnerPolityId,
            WarScarred = cell.WarScarred,
        };
        foreach (var anchor in cell.Anchors)
            if (anchor.Hex.Equals(hex)) { region.AnchorAt = anchor; break; }
        return region;
    }

    private static Func<StarTypeDef, double> LeanModifier(StellarLean lean) => lean switch
    {
        StellarLean.YoungBright => def => def.Id switch
        {
            "gold_main" or "white_blaze" or "blue_titan" => 2.0,
            "ashen_remnant" or "collapsed_core" => 0.3,
            _ => 1.0,
        },
        StellarLean.OldDim => def => def.Id switch
        {
            "ember_dwarf" or "amber_dwarf" => 2.0,
            "gold_main" => 0.6,
            "white_blaze" or "blue_titan" => 0.2,
            _ => 1.0,
        },
        StellarLean.RemnantGraveyard => def => def.Id switch
        {
            "ashen_remnant" or "collapsed_core" => 4.0,
            _ => 0.4,
        },
        _ => _ => 1.0,
    };

    /// <summary>Bilinear over the 4 nearest cell centers (spec §8 smoothing).</summary>
    private static double InterpolatedSettlementScale(GalaxySkeleton s, HexCoordinate hex)
    {
        double CellScale(int cx, int cy)
        {
            cx = Math.Clamp(cx, 0, s.Config.CellsX - 1);
            cy = Math.Clamp(cy, 0, s.Config.CellsY - 1);
            var cell = s.CellAt(cx, cy);
            if (cell.OwnerPolityId >= 0) return 1.0 + 0.8 * cell.DevelopmentTier;
            return cell.WarScarred ? 0.4 : 1.0;
        }
        // Position in cell-center space: cell centers sit at (cx*8+4, cy*10+5).
        double fx = (hex.X - 4.0) / 8.0, fy = (hex.Y - 5.0) / 10.0;
        int cx0 = (int)Math.Floor(fx), cy0 = (int)Math.Floor(fy);
        double tx = fx - cx0, ty = fy - cy0;
        double a = CellScale(cx0, cy0) * (1 - tx) + CellScale(cx0 + 1, cy0) * tx;
        double b = CellScale(cx0, cy0 + 1) * (1 - tx) + CellScale(cx0 + 1, cy0 + 1) * tx;
        return a * (1 - ty) + b * ty;
    }
}
```

Modify `src/Core/Generation/StarGenerator.cs`: signature becomes `public static void Generate(RollContext ctx, StarSystem system, StarGen.Core.Galaxy.RegionContext? region = null)`; the two `StarTypes.Table.Pick(ctx.NextDouble(RollChannel.StarType, 0, i))` calls become `StarTypes.Table.Pick(ctx.NextDouble(RollChannel.StarType, 0, i), region == null ? null : region.StarTypeModifier)`.

Modify `src/Core/Generation/BodyGenerator.cs`: `Generate` gains `RegionContext? region = null` and `double settlementScale = region?.SettlementScale ?? 1.0` captured once; the kind pick becomes

```csharp
var kind = BodyTables.Kind.Pick(
    ctx.NextDouble(RollChannel.BodyKind, idx),
    k => BodyTables.KindModifier(slot.Band)(k) * (region?.BeltModifier(k) ?? 1.0));
```

`GenerateBody` gains a final parameter `double settlementScale = 1.0` (pass it from `Generate`; `AddSatellites` passes it through too) and its settlement pick becomes

```csharp
body.Settlement = BodyTables.SettlementTable.Pick(
    ctx.NextDouble(RollChannel.Settlement, idx, sat),
    st => BodyTables.SettlementModifier(body.Biosphere, band)(st)
          * (st == Settlement.None ? 1.0 : settlementScale));
```

(Check `AddSatellites`'s call into `GenerateBody` and thread the parameter — keep the existing `presetSize` parameter untouched.)

Modify `src/Core/Overlays/OverlayResolver.cs`: `Resolve(RollContext ctx, StarSystem system, bool anchored = false)`; first line: `if (anchored) return;`.

Modify `src/Core/Generation/Generator.cs` — the galaxy overload becomes:

```csharp
    public static HexResult Generate(GalaxyContext galaxy, HexCoordinate coord)
    {
        var ctx = new RollContext(galaxy.Config.MasterSeed, coord);
        var region = RegionContext.For(galaxy, coord);

        bool anchored = region?.AnchorAt != null;
        if (!anchored)
        {
            double presenceThreshold = galaxy.IsFlatspace
                ? StellarDensity
                : DensityField.At(galaxy.Config, coord);
            if (ctx.NextDouble(RollChannel.Presence) >= presenceThreshold)
                return new HexResult(coord, null);
        }

        var system = new StarSystem(Designation.For(coord));
        StarGenerator.Generate(ctx, system, region);
        BodyGenerator.Generate(ctx, system, region);
        SocietyGenerator.Generate(ctx, system);
        NameGenerator.AssignNames(ctx, system);
        if (anchored) ApplyPreCommitment(ctx, system, region!.AnchorAt!);
        OverlayResolver.Resolve(ctx, system, anchored);
        return new HexResult(coord, system);
    }

    private static void ApplyPreCommitment(RollContext ctx, StarSystem system, Anchor anchor)
    {
        switch (anchor.Type)
        {
            case AnchorType.MineralRich:
                system.Tags.Add("mineral-rich");
                break;
            case AnchorType.PrecursorSite:
                system.Tags.Add("precursor site");
                break;
            case AnchorType.Homeworld:
                system.Tags.Add("homeworld");
                var world = BestWorld(system);
                if (world != null)
                {
                    world.Biosphere = Biosphere.Sapient;
                    world.Settlement = Settlement.MajorWorld;
                    world.Society = null;                       // re-attach with forced facts
                    SocietyGenerator.Generate(ctx, system);     // fills only missing societies
                    NameGenerator.AssignNames(ctx, system);     // fills only missing names
                }
                break;
        }
    }

    private static Body? BestWorld(StarSystem system)
    {
        Body? best = null;
        foreach (var star in system.Stars)
            foreach (var slot in star.Slots)
            {
                var b = slot.Body;
                if (b == null) continue;
                if (b.Kind != BodyKind.RockyWorld && b.Kind != BodyKind.IceWorld)
                {
                    if (best == null) best = b;   // fallback: any body at all
                    continue;
                }
                if (best == null || best.Kind == BodyKind.GasGiant
                    || best.Kind == BodyKind.PlanetoidBelt || b.Size > best.Size)
                    best = b;
            }
        return best;
    }
```

**Prerequisite check inside this task:** `SocietyGenerator.Attach` must skip bodies that already have a Society (so the re-run only fills the nulled one). Open `src/Core/Generation/SocietyGenerator.cs`: if `Attach` lacks a `if (body.Society != null) return;` guard, add it as the first line after the `IsInhabited` check. Likewise `NameGenerator.AssignNames` already skips named systems/bodies via its null checks — verify, don't change.

- [ ] **Step 4: Run** — `dotnet test` (full suite) — Expected: all green, including `Flatspace_RemainsBitIdentical_ToLegacy` and the untouched Phase 1 suite.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: per-hex regional integration with modifiers and pre-commitments"`

---
### Task 12: Inspector — galaxy, cell, gsave, gload commands

**Files:**
- Modify: `src/Inspector/Repl.cs`
- Test: manual (console app; Core behavior already covered).

**Interfaces:**
- Consumes: `SkeletonBuilder.Build`, `SkeletonSerializer.Save/Load`, `GalaxyContext`, existing REPL state.
- Produces: REPL gains a `GalaxyContext? _galaxy` field (null = flatspace, the prior behavior). New commands:
  - `galaxy <seed> [sizeSectors]` — builds a skeleton (`SkeletonBuilder.Build`), stores `_galaxy`, sets `_seed`, prints build stats: cell count, polity count (living/extinct), event count, claimed/unclaimed non-void %, chokepoint count, build wall-time (`System.Diagnostics.Stopwatch` — Inspector may use it; Core still may not).
  - `cell <cx> <cy>` — prints one cell's full state: density, void/chokepoint, lean, metallicity, owner polity name + development, war-scarring, anchors (type + hex), and the cell's events from the log.
  - `gsave <path>` / `gload <path>` — `File.WriteAllText(path, SkeletonSerializer.ToText(...))` / load + adopt config/seed. `gload` catches `InvalidDataException` and prints the message (the explicit keep-vs-regenerate choice stays with the user, spec §3.1).
  - `goto`/`next`/`prev`/`find`/`stats` now generate via `_galaxy ?? GalaxyContext.Flatspace(_seed)` — one shared helper `HexResult Gen(HexCoordinate c)`. `seed <n>` clears `_galaxy` (explicit return to flatspace) and prints a note saying so.
- Update `help` text to list the new commands.

- [ ] **Step 1: Implement.** In `src/Inspector/Repl.cs`: add fields `private GalaxyContext? _galaxy;`; add the shared generator helper; rewire `Show`, `Find`, and the `stats` dispatch to use it (`StatsReport.Build` gains a `GalaxyContext` parameter — change its signature to `Build(GalaxyContext galaxy, int startX, int startY, int n, int sectorWidth)` and generate via `Generator.Generate(galaxy, coord)`; the call site passes `_galaxy ?? GalaxyContext.Flatspace(_seed)`). Add the command cases:

```csharp
case "galaxy" when parts.Length >= 2 && ulong.TryParse(parts[1], out var gseed):
{
    int size = parts.Length >= 3 && int.TryParse(parts[2], out var sz) ? sz : 10;
    var config = new GalaxyConfig { MasterSeed = gseed, SizeSectors = size };
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var skeleton = SkeletonBuilder.Build(config);
    sw.Stop();
    _seed = gseed;
    _galaxy = new GalaxyContext(config) { Skeleton = skeleton };
    int nonVoid = 0, claimed = 0, chokepoints = 0;
    foreach (var c in skeleton.Cells)
    {
        if (c.IsChokepoint) chokepoints++;
        if (c.IsVoid) continue;
        nonVoid++;
        if (c.OwnerPolityId >= 0) claimed++;
    }
    int living = skeleton.Polities.Count(p => !p.Extinct);
    Console.WriteLine($"galaxy built in {sw.ElapsedMilliseconds} ms: {skeleton.Cells.Length} cells, "
        + $"{living} living / {skeleton.Polities.Count - living} extinct polities, "
        + $"{skeleton.Events.Count} events, {100.0 * claimed / nonVoid:F1}% of space claimed, "
        + $"{chokepoints} chokepoints");
    break;
}
case "cell" when parts.Length == 3 && _galaxy?.Skeleton is { } sk
        && int.TryParse(parts[1], out var qcx) && int.TryParse(parts[2], out var qcy):
{
    if (qcx < 0 || qcy < 0 || qcx >= _galaxy.Config.CellsX || qcy >= _galaxy.Config.CellsY)
    { Console.WriteLine("cell out of range"); break; }
    var cell = sk.CellAt(qcx, qcy);
    string owner = cell.OwnerPolityId >= 0 ? sk.Polities[cell.OwnerPolityId].Name : "unclaimed";
    Console.WriteLine($"cell [{qcx},{qcy}] density {cell.MeanDensity:F2}"
        + (cell.IsVoid ? " VOID" : "") + (cell.IsChokepoint ? " CHOKEPOINT" : "")
        + $" · {cell.Lean} · metallicity {cell.Metallicity:F2}");
    Console.WriteLine($"  owner: {owner} · dev {cell.DevelopmentTier}"
        + (cell.WarScarred ? " · war-scarred" : ""));
    foreach (var a in cell.Anchors)
        Console.WriteLine($"  anchor: {a.Type} at [{a.Hex.X:D4}-{a.Hex.Y:D4}]"
            + (a.SpeciesId >= 0 ? $" (species {sk.Species[a.SpeciesId].Name})" : ""));
    foreach (var e in sk.Events)
        if (e.Cx == qcx && e.Cy == qcy)
            Console.WriteLine($"  epoch {e.Epoch}: {e.Type} by {sk.Polities[e.ActorPolityId].Name}"
                + (e.TargetPolityId >= 0 ? $" vs {sk.Polities[e.TargetPolityId].Name}" : ""));
    break;
}
case "gsave" when parts.Length == 2 && _galaxy?.Skeleton != null:
    System.IO.File.WriteAllText(parts[1], SkeletonSerializer.ToText(_galaxy.Skeleton));
    Console.WriteLine($"saved to {parts[1]}");
    break;
case "gload" when parts.Length == 2:
    try
    {
        using (var reader = new System.IO.StreamReader(parts[1]))
        {
            var skeleton = SkeletonSerializer.Load(reader);
            _galaxy = new GalaxyContext(skeleton.Config) { Skeleton = skeleton };
            _seed = skeleton.Config.MasterSeed;
            Console.WriteLine($"loaded galaxy seed {_seed}, {skeleton.Polities.Count} polities");
        }
    }
    catch (System.IO.InvalidDataException ex) { Console.WriteLine($"refused: {ex.Message}"); }
    catch (System.IO.FileNotFoundException) { Console.WriteLine("file not found"); }
    break;
```

Add `using System.Linq;`, `using StarGen.Core.Galaxy;` and update `help`:

```csharp
Console.WriteLine("seed <n> | galaxy <seed> [sectors] | goto <x> <y> | next | prev | reroll");
Console.WriteLine("find <criterion> | stats <n> | cell <cx> <cy> | map [layer] | map <sx> <sy>");
Console.WriteLine("gsave <path> | gload <path> | quit    (map arrives with the atlas task)");
```

- [ ] **Step 2: Verify manually.**

Run: `printf 'galaxy 42 4\ncell 8 8\nstats 2000\ngsave /tmp/g.txt\ngload /tmp/g.txt\nquit\n' | dotnet run --project src/Inspector`
Expected: build stats line (sub-second build), a coherent cell dump, stats consistent with a shaped galaxy (presence well below flatspace 50% overall because of voids/rim), save+load round trip without complaint. Capture the session in the report. Then `dotnet test` — full suite green.

- [ ] **Step 3: Commit** — `git add -A && git commit -m "feat: inspector galaxy build, cell inspection, artifact save/load"`

---

### Task 13: Inspector — ASCII galaxy map

**Files:**
- Create: `src/Inspector/GalaxyMapView.cs`
- Modify: `src/Inspector/Repl.cs` (add `map` command cases)
- Test: manual.

**Interfaces:**
- Consumes: skeleton, `Generator.Generate`, `SystemFormatter` conventions.
- Produces: `static class GalaxyMapView` with:
  - `string CellMap(GalaxySkeleton s, string layer)` — one character per region cell, `CellsX` wide, `CellsY` rows. Layers:
    - `"density"` (default): ` .:-=+*#%@` indexed by `(int)(MeanDensity * 9.999)`.
    - `"polity"`: unclaimed non-void `.`, void ` `, owned = polity letter `(char)('A' + polityId % 26)` (lowercase when `polityId >= 26`); capitals rendered as `*`.
    - `"zone"`: void ` `, war-scarred `!`, chokepoint `^`, contested `?`, else `.`.
    - `"dev"`: digits `0-5` from DevelopmentTier, void ` `, unclaimed non-void `.`.
    - `"lean"`: Balanced `.`, YoungBright `+`, OldDim `-`, RemnantGraveyard `x`, void ` `.
    Each map is followed by a legend line listing living polities (`A=Veshara (12 cells)` style) for the polity layer, or the character key for other layers.
  - `string SectorMap(GalaxyContext galaxy, int sx, int sy)` — hex-resolution 32×40 map of sector (sx, sy): empty hex `·` (middle dot), system `*`, settled system `o`, homeworld/anchor `@`, out of range message if the sector is outside the galaxy. Generates each hex via `Generator.Generate` (1,280 calls — fine).
- REPL: `map` → density cell map; `map <layer>` → that layer; `map <sx> <sy>` → sector map. Requires `_galaxy`; prints "build a galaxy first (galaxy <seed>)" otherwise.

- [ ] **Step 1: Implement** — `src/Inspector/GalaxyMapView.cs`:

```csharp
using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>ASCII galaxy atlas (spec §9): the visual counterpart of stats.</summary>
public static class GalaxyMapView
{
    private const string DensityRamp = " .:-=+*#%@";

    public static string CellMap(GalaxySkeleton s, string layer)
    {
        var sb = new StringBuilder();
        for (int cy = 0; cy < s.Config.CellsY; cy++)
        {
            for (int cx = 0; cx < s.Config.CellsX; cx++)
                sb.Append(CellChar(s, s.CellAt(cx, cy), layer));
            sb.AppendLine();
        }
        sb.AppendLine(Legend(s, layer));
        return sb.ToString();
    }

    private static char CellChar(GalaxySkeleton s, RegionCell c, string layer) => layer switch
    {
        "polity" => c.IsVoid ? ' '
            : s.Polities.Any(p => !p.Extinct && p.CapitalCx == c.Cx && p.CapitalCy == c.Cy) ? '*'
            : c.OwnerPolityId < 0 ? '.'
            : c.OwnerPolityId < 26 ? (char)('A' + c.OwnerPolityId)
            : (char)('a' + c.OwnerPolityId % 26),
        "zone" => c.IsVoid ? ' ' : c.WarScarred ? '!' : c.IsChokepoint ? '^'
            : c.Contested ? '?' : '.',
        "dev" => c.IsVoid ? ' ' : c.OwnerPolityId < 0 ? '.'
            : (char)('0' + System.Math.Min(5, c.DevelopmentTier)),
        "lean" => c.IsVoid ? ' ' : c.Lean switch
        {
            StellarLean.YoungBright => '+', StellarLean.OldDim => '-',
            StellarLean.RemnantGraveyard => 'x', _ => '.',
        },
        _ => DensityRamp[(int)(System.Math.Clamp(c.MeanDensity, 0, 0.9999) * 10)],
    };

    private static string Legend(GalaxySkeleton s, string layer) => layer switch
    {
        "polity" => string.Join("  ", s.Polities.Where(p => !p.Extinct).Select(p =>
            $"{(p.Id < 26 ? (char)('A' + p.Id) : (char)('a' + p.Id % 26))}={p.Name} "
            + $"({s.Cells.Count(c => c.OwnerPolityId == p.Id)} cells)"))
            + "   *=capital .=unclaimed",
        "zone" => "!=war-scarred ^=chokepoint ?=contested .=quiet",
        "dev" => "0-5=development .=unclaimed",
        "lean" => "+=young-bright -=old-dim x=remnant-graveyard .=balanced",
        _ => "density: ' " + DensityRamp + " ' low->high",
    };

    public static string SectorMap(GalaxyContext galaxy, int sx, int sy)
    {
        if (sx < 0 || sy < 0 || sx >= galaxy.Config.SizeSectors || sy >= galaxy.Config.SizeSectors)
            return "sector out of range";
        var sb = new StringBuilder();
        var skeleton = galaxy.Skeleton;
        for (int hy = sy * 40; hy < sy * 40 + 40; hy++)
        {
            for (int hx = sx * 32; hx < sx * 32 + 32; hx++)
            {
                var coord = new HexCoordinate(hx, hy);
                bool anchored = skeleton != null &&
                    skeleton.CellForHex(coord).Anchors.Any(a => a.Hex.Equals(coord));
                var system = Generator.Generate(galaxy, coord).System;
                sb.Append(system == null ? '·'
                    : anchored ? '@'
                    : SystemIsSettled(system) ? 'o' : '*');
            }
            sb.AppendLine();
        }
        sb.AppendLine("·=empty *=system o=settled @=anchored");
        return sb.ToString();
    }

    private static bool SystemIsSettled(StarSystem system)
    {
        foreach (var star in system.Stars)
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                if (slot.Body.Settlement != Settlement.None) return true;
                foreach (var sat in slot.Body.Satellites)
                    if (sat.Settlement != Settlement.None) return true;
            }
        return false;
    }
}
```

REPL cases (inside the switch):

```csharp
case "map" when _galaxy?.Skeleton == null:
    Console.WriteLine("build a galaxy first (galaxy <seed>)");
    break;
case "map" when parts.Length == 3
        && int.TryParse(parts[1], out var msx) && int.TryParse(parts[2], out var msy):
    Console.WriteLine(GalaxyMapView.SectorMap(_galaxy!, msx, msy));
    break;
case "map":
    Console.WriteLine(GalaxyMapView.CellMap(_galaxy!.Skeleton!,
        parts.Length >= 2 ? parts[1] : "density"));
    break;
```

(Order matters: the null-guard case must precede the others.)

- [ ] **Step 2: Verify manually — the acceptance moment of the whole slice.**

Run: `printf 'galaxy 42 10\nmap\nmap polity\nmap zone\nmap lean\nmap 5 5\nquit\n' | dotnet run --project src/Inspector`
Expected, by eyeball (capture all output in the report):
- `map` (density): recognizable galaxy — bright core, spiral arm ridges, dark rim/corners, noise clumps.
- `map polity`: contiguous letter-blobs (kingdoms), `*` capitals inside them, `.` wilds between them, blank voids as natural borders.
- `map zone`: `!` scattered along polity contact lines, `^` at narrow passages.
- `map 5 5`: a near-core sector dense with `*`/`o`, anchored `@`s visible.
Then `dotnet test` — full suite green.

- [ ] **Step 3: Commit** — `git add -A && git commit -m "feat: ASCII galaxy atlas with density/polity/zone/dev/lean layers"`

---

### Task 14: DESIGN.md amendments (spec §12)

**Files:**
- Modify: `docs/DESIGN.md`

**Interfaces:** documentation only.

- [ ] **Step 1: Apply the five §12 amendments.**

1. In §2 (Architecture), **Determinism & seeding** paragraph: append the sentence: `Generation input is a GalaxyConfig (master seed + galaxy size + tuning knobs), not a bare seed; the same seed at different sizes intentionally yields different galaxies.`
2. In §2, **Game-layer readiness** paragraph: replace the final sentence `Core stays a pure generator; mutable state is a separate concern layered on top.` with: `Core stays a pure generator; mutable state is a separate concern layered on top — and per the regional spec (§7.7), the future game layer inherits a continuing simulation seeded by the world-state handoff, with deltas recording player-visible divergence from it.`
3. In §3 (Data Model), after the hex-layer paragraph, add: `Above the hex layer sits the persisted galaxy structure artifact (regional spec §3.1): region-cell state, species/polity registries, and the event log — built once per GalaxyConfig, versioned, and loaded rather than regenerated so existing galaxies stay stable under newer generator code. Coordinates have a defined galaxy extent; hexes beyond the rim are empty space.`
4. In §4 (Roadmap) phase 4: change `persistence of seed + deltas only (not full generated data)` to `persistence of GalaxyConfig + the galaxy structure artifact + deltas (regional spec §3.1)` and the phase-4 done-when's `a save file contains only the master seed plus delta records` to `a save file contains the GalaxyConfig, the galaxy structure artifact, and delta records`.
5. Replace the entire `**Cross-cutting design phase (scheduled, spec not yet written): regional / spatial generation.**` block (all of its bullet list and closing paragraph) with: `**Regional / spatial generation** is specced in docs/superpowers/specs/2026-07-07-regional-generation-design.md (three-tier architecture: density fields, persisted galaxy skeleton with an epoch history simulation, per-hex integration) and implemented in slices — slice 1 (visible galaxy) covers Tier 1, seeding, sim stage 1, and the inspector atlas.`

- [ ] **Step 2: Verify** — `dotnet test` still green (docs-only, but the suite is the pre-commit habit); read the amended sections once for coherence.

- [ ] **Step 3: Commit** — `git add -A && git commit -m "docs: apply regional spec amendments to DESIGN.md"`

---

## Self-Review Notes

- **Spec coverage (slice scope):** Tier 1 field (T1–T2 vs spec §4), presence integration + flatspace (T3, spec §8), lattice + all four seeding passes (T4–T8, spec §5–§6), stage-1 sim + lifecycle + global event log (T9, spec §7/§7.9-stage-1), artifact + stamps + golden (T10, spec §3.1/§10), RegionContext modifiers/pre-commitments/smoothing/hard-borders (T11, spec §8), atlas + cell/chronicle-lite + save/load (T12–T13, spec §9), DESIGN.md §12 (T14). Deliberately out (later slices per spec §7.9): budgets/stockpiles, commodities, relations ladder, news, POI compiler, world-state handoff, `polity`/`chronicle` REPL commands (arrive with the sim stages that give them content).
- **Type consistency verified:** `RollContext.NextDouble(channel, index, subIndex)` signatures; `WeightedTable.Pick(double, Func<T,double>?)`; `StarTypeDef.Id` strings match Phase 1's `StarTypes` content exactly; `GalaxySkeleton.CellAt/CellForHex` used consistently; `PickAnchorHex(s, cell, drawIndex)` draw indexes: 0 = mineral, 1 = precursor, 2 = homeworld — distinct per cell, matching the one-anchor-per-hex probe.
- **Known accepted risks:** (a) statistical test bands (density mean ±0.12, expansion wilds, graveyard skew) may need the documented constant tuning on first run — each test's task says which constant to adjust and forbids widening bands; (b) `SocietyGenerator.Attach` idempotency guard is verified/added inside T11 where it's needed; (c) sim performance at SizeSectors=10 (1,600 cells × 12 epochs, `Owned()` scans = ~O(polities×cells) per epoch) is well under a second — revisit only if `galaxy` build stats say otherwise.
