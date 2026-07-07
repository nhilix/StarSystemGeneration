# Phase 1: Core Generation Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the system/body generation layer specced in `docs/superpowers/specs/2026-07-07-generation-rules-design.md` — deterministic hash-based generation of star systems (presence, stars, orbits, bodies, satellites, society, names, overlays) plus an interactive inspector REPL.

**Architecture:** A pure `netstandard2.1` class library (`StarGen.Core`) where every random draw is a stateless hash of `(masterSeed, coordinate, channel, index, subIndex)`; content lives in `WeightedTable<T>` data with cross-influence expressed as call-site weight-multiplier functions. A `net10.0` console REPL (`StarGen.Inspector`) and xUnit test project consume Core.

**Tech Stack:** C# (LangVersion latest), .NET SDK 10, netstandard2.1 (Core only), xUnit, no NuGet dependencies in Core.

## Global Constraints

- `src/Core` targets **netstandard2.1**, has **zero package references**, and never references Unity or the other projects.
- `src/Inspector` and `tests/Core.Tests` target **net10.0** (current LTS installed on the dev machine; DESIGN.md's ".NET 8 (or current LTS)").
- `RollChannel` values are **stable constants — never renumber or reuse** a shipped value; new rolls get new channels (spec §8).
- Core model types are **plain mutable classes** (no C# records) — netstandard2.1 lacks `IsExternalInit` and overlays mutate systems in place.
- Determinism is absolute: no `System.Random`, no `DateTime`, no iteration-order-dependent draws anywhere in Core.
- Commit after every task with a conventional-commit message ending in the Claude co-author trailer.

**Resolved spec questions (locked in by this plan):**
1. *Cross-influence:* `WeightedTable<T>.Pick(double roll01, Func<T,double>? modifier)` — modifier multiplies an entry's weight at draw time; `0` excludes the entry; tables stay pure data.
2. *Orbit slots / habitable band:* each star-type table entry carries `MinSlots`, `MaxSlots`, `HabStart`, `HabEnd` (slot indices; `-1/-1` = no habitable band). Slot band = Inner below `HabStart`, Habitable within, Outer above.
3. *Satellites:* per-body-kind `WeightedTable<int>` count tables (gas giant 0–4; rocky/ice size ≥ 4 gets 0–3; all else 0). Satellite kinds restricted to rocky/ice, size capped below parent, no satellites of satellites.

---

### Task 1: Solution scaffolding

**Files:**
- Create: `StarSystemGeneration.sln`, `src/Core/StarGen.Core.csproj`, `src/Inspector/StarGen.Inspector.csproj`, `tests/Core.Tests/StarGen.Core.Tests.csproj`, `.gitignore`
- Delete: template-generated `Class1.cs` / `UnitTest1.cs` contents (replaced below)

**Interfaces:**
- Consumes: nothing.
- Produces: buildable solution; `StarGen.Core` / `StarGen.Core.Tests` namespaces all later tasks build in.

- [ ] **Step 1: Scaffold projects**

```bash
cd /c/Users/Jaaco/Documents/Dev/StarSystemGeneration
dotnet new gitignore
dotnet new sln -n StarSystemGeneration
dotnet new classlib -n StarGen.Core -o src/Core
dotnet new console  -n StarGen.Inspector -o src/Inspector
dotnet new xunit    -n StarGen.Core.Tests -o tests/Core.Tests
dotnet sln add src/Core src/Inspector tests/Core.Tests
dotnet add src/Inspector reference src/Core
dotnet add tests/Core.Tests reference src/Core
rm src/Core/Class1.cs tests/Core.Tests/UnitTest1.cs
```

- [ ] **Step 2: Pin target frameworks**

Replace `src/Core/StarGen.Core.csproj` contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <RootNamespace>StarGen.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

In `src/Inspector/StarGen.Inspector.csproj` and `tests/Core.Tests/StarGen.Core.Tests.csproj`, set `<TargetFramework>net10.0</TargetFramework>` (leave other template properties/package refs as generated).

- [ ] **Step 3: Add smoke test**

Create `tests/Core.Tests/SmokeTests.cs`:

```csharp
using Xunit;

namespace StarGen.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void SolutionWiring_Builds() => Assert.True(true);
}
```

- [ ] **Step 4: Verify build and test**

Run: `dotnet test`
Expected: 1 test passes.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore: scaffold solution (Core netstandard2.1, Inspector/tests net10.0)"
```

---

### Task 2: StableHash

**Files:**
- Create: `src/Core/Rng/StableHash.cs`
- Test: `tests/Core.Tests/Rng/StableHashTests.cs`

**Interfaces:**
- Produces: `static ulong StableHash.Mix(ulong a, ulong b, ulong c, ulong d)` — deterministic SplitMix64-chain mix used by `RollContext`.

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Rng/StableHashTests.cs`:

```csharp
using StarGen.Core.Rng;
using Xunit;

namespace StarGen.Core.Tests.Rng;

public class StableHashTests
{
    [Fact]
    public void SameInputs_SameOutput() =>
        Assert.Equal(StableHash.Mix(1, 2, 3, 4), StableHash.Mix(1, 2, 3, 4));

    [Fact]
    public void AnySingleInputChange_ChangesOutput()
    {
        var baseline = StableHash.Mix(1, 2, 3, 4);
        Assert.NotEqual(baseline, StableHash.Mix(9, 2, 3, 4));
        Assert.NotEqual(baseline, StableHash.Mix(1, 9, 3, 4));
        Assert.NotEqual(baseline, StableHash.Mix(1, 2, 9, 4));
        Assert.NotEqual(baseline, StableHash.Mix(1, 2, 3, 9));
    }

    [Fact]
    public void ZeroInputs_DoNotCollapse() =>
        Assert.NotEqual(StableHash.Mix(0, 0, 0, 0), StableHash.Mix(0, 0, 0, 1));
}
```

- [ ] **Step 2: Run** — `dotnet test --filter StableHashTests` — Expected: FAIL (type does not exist).

- [ ] **Step 3: Implement** — `src/Core/Rng/StableHash.cs`:

```csharp
namespace StarGen.Core.Rng;

/// <summary>Deterministic mixing for stateless roll derivation (spec §8).</summary>
public static class StableHash
{
    public static ulong Mix(ulong a, ulong b, ulong c, ulong d)
    {
        ulong h = SplitMix64(a);
        h = SplitMix64(h ^ b);
        h = SplitMix64(h ^ c);
        h = SplitMix64(h ^ d);
        return h;
    }

    private static ulong SplitMix64(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
```

- [ ] **Step 4: Run** — `dotnet test --filter StableHashTests` — Expected: PASS (3 tests).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: stable SplitMix64-chain hash"`

---

### Task 3: HexCoordinate, RollChannel, RollContext

**Files:**
- Create: `src/Core/Model/HexCoordinate.cs`, `src/Core/Rng/RollChannel.cs`, `src/Core/Rng/RollContext.cs`
- Test: `tests/Core.Tests/Rng/RollContextTests.cs`

**Interfaces:**
- Consumes: `StableHash.Mix`.
- Produces:
  - `readonly struct HexCoordinate(int X, int Y)` with equality.
  - `enum RollChannel : ulong` — the channel registry (stable values).
  - `readonly struct RollContext(ulong masterSeed, HexCoordinate coord)` with `double NextDouble(RollChannel ch, int index = 0, int subIndex = 0)` (in `[0,1)`) and `int NextInt(RollChannel ch, int minInclusive, int maxExclusive, int index = 0, int subIndex = 0)`.

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Rng/RollContextTests.cs`:

```csharp
using StarGen.Core.Model;
using StarGen.Core.Rng;
using Xunit;

namespace StarGen.Core.Tests.Rng;

public class RollContextTests
{
    private static RollContext Ctx(ulong seed = 42, int x = 3, int y = 7) =>
        new(seed, new HexCoordinate(x, y));

    [Fact]
    public void NextDouble_IsDeterministic_AndOrderIndependent()
    {
        var a = Ctx().NextDouble(RollChannel.Presence);
        Ctx().NextDouble(RollChannel.BodyKind, 5); // unrelated draw in between
        var b = Ctx().NextDouble(RollChannel.Presence);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NextDouble_IsInUnitInterval()
    {
        for (int i = 0; i < 1000; i++)
        {
            var v = Ctx().NextDouble(RollChannel.BodyKind, i);
            Assert.InRange(v, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void DifferentChannelIndexSubIndexSeedCoord_AllDiffer()
    {
        var baseline = Ctx().NextDouble(RollChannel.BodyKind, 1, 1);
        Assert.NotEqual(baseline, Ctx().NextDouble(RollChannel.BodySize, 1, 1));
        Assert.NotEqual(baseline, Ctx().NextDouble(RollChannel.BodyKind, 2, 1));
        Assert.NotEqual(baseline, Ctx().NextDouble(RollChannel.BodyKind, 1, 2));
        Assert.NotEqual(baseline, Ctx(seed: 43).NextDouble(RollChannel.BodyKind, 1, 1));
        Assert.NotEqual(baseline, Ctx(x: 4).NextDouble(RollChannel.BodyKind, 1, 1));
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            var v = Ctx().NextInt(RollChannel.SlotCount, 3, 9, i);
            Assert.InRange(v, 3, 8);
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter RollContextTests` — Expected: FAIL (types missing).

- [ ] **Step 3: Implement** — three files:

`src/Core/Model/HexCoordinate.cs`:

```csharp
using System;

namespace StarGen.Core.Model;

public readonly struct HexCoordinate : IEquatable<HexCoordinate>
{
    public int X { get; }
    public int Y { get; }
    public HexCoordinate(int x, int y) { X = x; Y = y; }

    public bool Equals(HexCoordinate other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is HexCoordinate h && Equals(h);
    public override int GetHashCode() => (X * 397) ^ Y;
    public override string ToString() => $"({X},{Y})";
}
```

`src/Core/Rng/RollChannel.cs`:

```csharp
namespace StarGen.Core.Rng;

/// <summary>
/// Channel registry (spec §8). Values are STABLE: never renumber or reuse a
/// shipped value. New rolls append new values; removed rolls retire theirs.
/// </summary>
public enum RollChannel : ulong
{
    Presence = 1,
    StarArrangement = 2,
    StarType = 3,          // subIndex = star index (0 primary, 1..2 companions)
    StarAge = 4,
    SlotCount = 5,
    CompanionSlot = 6,     // subIndex = companion index
    BodyKind = 7,          // index = star index, subIndex = slot index
    BodySize = 8,
    Atmosphere = 9,
    Hydrographics = 10,
    Biosphere = 11,
    Settlement = 12,
    PopulationTier = 13,
    Government = 14,
    OrderTier = 15,
    PortTier = 16,
    SatelliteCount = 17,
    SatelliteKind = 18,    // index = slot index, subIndex = satellite index
    SatelliteSize = 19,
    NameLength = 20,
    NameSyllable = 21,     // index = name slot, subIndex = syllable position
    OverlayChance = 22,
    OverlayPick = 23,
}
```

`src/Core/Rng/RollContext.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Core.Rng;

/// <summary>Stateless roll source: every draw is a pure hash (spec §8).</summary>
public readonly struct RollContext
{
    private readonly ulong _masterSeed;
    public HexCoordinate Coordinate { get; }

    public RollContext(ulong masterSeed, HexCoordinate coordinate)
    {
        _masterSeed = masterSeed;
        Coordinate = coordinate;
    }

    public double NextDouble(RollChannel channel, int index = 0, int subIndex = 0)
    {
        ulong coord = ((ulong)(uint)Coordinate.X << 32) | (uint)Coordinate.Y;
        ulong idx = ((ulong)(uint)index << 32) | (uint)subIndex;
        ulong h = StableHash.Mix(_masterSeed, coord, (ulong)channel, idx);
        return (h >> 11) * (1.0 / (1UL << 53)); // top 53 bits -> [0,1)
    }

    public int NextInt(RollChannel channel, int minInclusive, int maxExclusive,
                       int index = 0, int subIndex = 0) =>
        minInclusive + (int)(NextDouble(channel, index, subIndex) * (maxExclusive - minInclusive));
}
```

- [ ] **Step 4: Run** — `dotnet test --filter RollContextTests` — Expected: PASS (4 tests).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: hex coordinate, channel registry, stateless roll context"`

---

### Task 4: WeightedTable&lt;T&gt;

**Files:**
- Create: `src/Core/Tables/WeightedTable.cs`
- Test: `tests/Core.Tests/Tables/WeightedTableTests.cs`

**Interfaces:**
- Produces: `sealed class WeightedTable<T>` with ctor `WeightedTable(params (T Item, double Weight)[] entries)` (throws on empty/negative/all-zero), `T Pick(double roll01)`, `T Pick(double roll01, Func<T,double>? modifier)`. Modifier multiplies weights at draw time; effective total ≤ 0 throws `InvalidOperationException`. Also exposes `IReadOnlyList<(T Item, double Weight)> Entries` (for the REPL stats command).

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Tables/WeightedTableTests.cs`:

```csharp
using System;
using System.Linq;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Tables;
using Xunit;

namespace StarGen.Core.Tests.Tables;

public class WeightedTableTests
{
    [Fact]
    public void Pick_MapsRollAcrossCumulativeWeights()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 3));
        Assert.Equal("a", table.Pick(0.0));
        Assert.Equal("a", table.Pick(0.24));
        Assert.Equal("b", table.Pick(0.26));
        Assert.Equal("b", table.Pick(0.999));
    }

    [Fact]
    public void Modifier_ZeroWeight_ExcludesEntry()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 1));
        for (double r = 0; r < 1; r += 0.05)
            Assert.Equal("b", table.Pick(r, item => item == "a" ? 0 : 1));
    }

    [Fact]
    public void Modifier_ShiftsDistribution()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 1));
        var ctx = new RollContext(7, new HexCoordinate(0, 0));
        int aCount = 0;
        for (int i = 0; i < 4000; i++)
            if (table.Pick(ctx.NextDouble(RollChannel.BodyKind, i), it => it == "a" ? 3 : 1) == "a")
                aCount++;
        Assert.InRange(aCount / 4000.0, 0.70, 0.80); // expect ~0.75
    }

    [Fact]
    public void SamplingWithoutModifier_RoughlyMatchesWeights()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 4));
        var ctx = new RollContext(11, new HexCoordinate(1, 1));
        int aCount = 0;
        for (int i = 0; i < 5000; i++)
            if (table.Pick(ctx.NextDouble(RollChannel.BodySize, i)) == "a") aCount++;
        Assert.InRange(aCount / 5000.0, 0.16, 0.24); // expect ~0.20
    }

    [Fact]
    public void InvalidConstruction_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WeightedTable<string>());
        Assert.Throws<ArgumentException>(() => new WeightedTable<string>(("a", -1)));
        Assert.Throws<ArgumentException>(() => new WeightedTable<string>(("a", 0), ("b", 0)));
    }

    [Fact]
    public void AllWeightsZeroedByModifier_Throws()
    {
        var table = new WeightedTable<string>(("a", 1));
        Assert.Throws<InvalidOperationException>(() => table.Pick(0.5, _ => 0));
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter WeightedTableTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Tables/WeightedTable.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StarGen.Core.Tables;

/// <summary>
/// Immutable weighted lookup: content is data, cross-influence is a call-site
/// modifier multiplying weights at draw time (spec §5; plan resolution #1).
/// </summary>
public sealed class WeightedTable<T>
{
    private readonly (T Item, double Weight)[] _entries;

    public IReadOnlyList<(T Item, double Weight)> Entries => _entries;

    public WeightedTable(params (T Item, double Weight)[] entries)
    {
        if (entries.Length == 0)
            throw new ArgumentException("Weighted table requires at least one entry.");
        double total = 0;
        foreach (var (_, weight) in entries)
        {
            if (weight < 0) throw new ArgumentException("Weights must be non-negative.");
            total += weight;
        }
        if (total <= 0) throw new ArgumentException("Total weight must be positive.");
        _entries = entries;
    }

    public T Pick(double roll01) => Pick(roll01, null);

    public T Pick(double roll01, Func<T, double>? modifier)
    {
        double total = 0;
        foreach (var (item, weight) in _entries)
            total += weight * (modifier?.Invoke(item) ?? 1.0);
        if (total <= 0)
            throw new InvalidOperationException("All weights are zero after applying modifier.");

        double target = roll01 * total, acc = 0;
        foreach (var (item, weight) in _entries)
        {
            acc += weight * (modifier?.Invoke(item) ?? 1.0);
            if (target < acc) return item;
        }
        return _entries[_entries.Length - 1].Item; // roll01 == upper edge
    }
}
```

- [ ] **Step 4: Run** — `dotnet test --filter WeightedTableTests` — Expected: PASS (6 tests).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: WeightedTable with draw-time weight modifiers"`

---

### Task 5: Domain model

**Files:**
- Create: `src/Core/Model/Enums.cs`, `src/Core/Model/StarSystem.cs`, `src/Core/Model/Star.cs`, `src/Core/Model/OrbitSlot.cs`, `src/Core/Model/Body.cs`, `src/Core/Model/Society.cs`
- Test: `tests/Core.Tests/Model/ModelTests.cs`

**Interfaces:**
- Produces (all `namespace StarGen.Core.Model`, plain mutable classes):
  - Enums: `StarArrangement { Single, Binary, Trinary }`, `StarAge { Young, Mature, Old }`, `OrbitBand { Inner, Habitable, Outer }`, `BodyKind { RockyWorld, IceWorld, GasGiant, PlanetoidBelt, Wreckage }`, `Atmosphere { None, Trace, Thin, Breathable, Dense, Toxic, Corrosive }`, `Biosphere { Barren, Microbial, Flourishing, Sapient }`, `Settlement { None, Outpost, Colony, MajorWorld }`, `OrderTier { Lawless, Loose, Orderly, Strict, Regimented }`, `PortTier { None, Field, Station, Orbital, Nexus }`
  - `StarSystem`: `string Designation` (ctor arg), `string? GivenName`, `StarArrangement Arrangement`, `List<Star> Stars`, `string? OverlayId`, `List<string> Tags`.
  - `Star`: `string TypeId`, `string TypeName`, `StarAge Age`, `List<OrbitSlot> Slots`, `int? CompanionSlotIndex` (null for primary).
  - `OrbitSlot`: `int Index`, `OrbitBand Band`, `Body? Body`.
  - `Body`: `BodyKind Kind`, `int Size`, `Atmosphere Atmosphere`, `int Hydrographics` (0–100), `Biosphere Biosphere`, `Settlement Settlement`, `Society? Society`, `List<Body> Satellites`, `List<string> Tags`, `string? Name`, plus `bool IsInhabited => Settlement != Settlement.None || Biosphere == Biosphere.Sapient`.
  - `Society`: `int PopulationTier` (0–9), `string Government`, `OrderTier Order`, `PortTier Port`, `List<string> PointsOfInterest`.

- [ ] **Step 1: Write failing test** — `tests/Core.Tests/Model/ModelTests.cs`:

```csharp
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Model;

public class ModelTests
{
    [Fact]
    public void Body_IsInhabited_TracksSettlementOrSapience()
    {
        var body = new Body { Kind = BodyKind.RockyWorld };
        Assert.False(body.IsInhabited);
        body.Settlement = Settlement.Outpost;
        Assert.True(body.IsInhabited);                    // colony on a dead rock
        body.Settlement = Settlement.None;
        body.Biosphere = Biosphere.Sapient;
        Assert.True(body.IsInhabited);                    // native society
    }

    [Fact]
    public void StarSystem_InitializesCollections()
    {
        var system = new StarSystem("SGC 0001-0002");
        Assert.Equal("SGC 0001-0002", system.Designation);
        Assert.Empty(system.Stars);
        Assert.Empty(system.Tags);
        Assert.Null(system.GivenName);
        Assert.Null(system.OverlayId);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter ModelTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Model/Enums.cs`:

```csharp
namespace StarGen.Core.Model;

public enum StarArrangement { Single, Binary, Trinary }
public enum StarAge { Young, Mature, Old }
public enum OrbitBand { Inner, Habitable, Outer }
public enum BodyKind { RockyWorld, IceWorld, GasGiant, PlanetoidBelt, Wreckage }
public enum Atmosphere { None, Trace, Thin, Breathable, Dense, Toxic, Corrosive }
public enum Biosphere { Barren, Microbial, Flourishing, Sapient }
public enum Settlement { None, Outpost, Colony, MajorWorld }
public enum OrderTier { Lawless, Loose, Orderly, Strict, Regimented }
public enum PortTier { None, Field, Station, Orbital, Nexus }
```

`src/Core/Model/StarSystem.cs`:

```csharp
using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class StarSystem
{
    public string Designation { get; }
    public string? GivenName { get; set; }
    public StarArrangement Arrangement { get; set; }
    public List<Star> Stars { get; } = new();
    public string? OverlayId { get; set; }
    public List<string> Tags { get; } = new();

    public StarSystem(string designation) => Designation = designation;
}
```

`src/Core/Model/Star.cs`:

```csharp
using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class Star
{
    public string TypeId { get; set; } = "";
    public string TypeName { get; set; } = "";
    public StarAge Age { get; set; }
    public List<OrbitSlot> Slots { get; } = new();
    /// <summary>Primary-star slot this companion occupies; null for the primary.</summary>
    public int? CompanionSlotIndex { get; set; }
}
```

`src/Core/Model/OrbitSlot.cs`:

```csharp
namespace StarGen.Core.Model;

public sealed class OrbitSlot
{
    public int Index { get; set; }
    public OrbitBand Band { get; set; }
    public Body? Body { get; set; }
}
```

`src/Core/Model/Body.cs`:

```csharp
using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class Body
{
    public BodyKind Kind { get; set; }
    public int Size { get; set; }
    public Atmosphere Atmosphere { get; set; }
    public int Hydrographics { get; set; }      // 0-100 surface coverage %
    public Biosphere Biosphere { get; set; }
    public Settlement Settlement { get; set; }
    public Society? Society { get; set; }
    public List<Body> Satellites { get; } = new();
    public List<string> Tags { get; } = new();
    public string? Name { get; set; }

    /// <summary>Society exists when settled or natively sapient (spec §5).</summary>
    public bool IsInhabited => Settlement != Settlement.None || Biosphere == Biosphere.Sapient;
}
```

`src/Core/Model/Society.cs`:

```csharp
using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class Society
{
    public int PopulationTier { get; set; }     // 0-9
    public string Government { get; set; } = "";
    public OrderTier Order { get; set; }
    public PortTier Port { get; set; }
    public List<string> PointsOfInterest { get; } = new();
}
```

- [ ] **Step 4: Run** — `dotnet test --filter ModelTests` — Expected: PASS (2 tests).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: domain model for systems, stars, bodies, society"`

---

### Task 6: Generator skeleton — presence roll + designation

**Files:**
- Create: `src/Core/Generation/Generator.cs`, `src/Core/Generation/HexResult.cs`, `src/Core/Naming/Designation.cs`
- Test: `tests/Core.Tests/Generation/PresenceTests.cs`

**Interfaces:**
- Consumes: `RollContext`, `RollChannel.Presence`, model types.
- Produces:
  - `sealed class HexResult`: `HexCoordinate Coordinate`, `StarSystem? System`, `bool IsEmpty => System == null`; ctor `HexResult(HexCoordinate, StarSystem?)`.
  - `static class Generator`: `const double StellarDensity = 0.5`, `static HexResult Generate(ulong masterSeed, HexCoordinate coord)`. Later tasks append pipeline calls inside `Generate` — the marker comment shows where.
  - `static class Designation`: `static string For(HexCoordinate coord)` returning `"SGC {X:D4}-{Y:D4}"` (Phase 1 assumes non-negative coordinates).

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Generation/PresenceTests.cs`:

```csharp
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using Xunit;

namespace StarGen.Core.Tests.Generation;

public class PresenceTests
{
    [Fact]
    public void EmptyHex_IsStable()
    {
        // find an empty hex, then re-check it several times
        for (int x = 0; x < 200; x++)
        {
            var coord = new HexCoordinate(x, 0);
            if (!Generator.Generate(1, coord).IsEmpty) continue;
            for (int i = 0; i < 5; i++)
                Assert.True(Generator.Generate(1, coord).IsEmpty);
            return;
        }
        Assert.Fail("No empty hex found in 200 tries — presence roll broken.");
    }

    [Fact]
    public void PresenceRate_IsNearDensity()
    {
        int present = 0;
        for (int x = 0; x < 100; x++)
            for (int y = 0; y < 40; y++)
                if (!Generator.Generate(7, new HexCoordinate(x, y)).IsEmpty) present++;
        Assert.InRange(present / 4000.0, 0.45, 0.55);
    }

    [Fact]
    public void NonEmptyHex_HasDesignation()
    {
        for (int x = 0; x < 200; x++)
        {
            var result = Generator.Generate(1, new HexCoordinate(x, 1));
            if (result.IsEmpty) continue;
            Assert.Equal(Designation.For(result.Coordinate), result.System!.Designation);
            return;
        }
        Assert.Fail("No system found in 200 tries.");
    }

    [Fact]
    public void Designation_Format() =>
        Assert.Equal("SGC 0012-0034", Designation.For(new HexCoordinate(12, 34)));
}
```

- [ ] **Step 2: Run** — `dotnet test --filter PresenceTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Naming/Designation.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Core.Naming;

/// <summary>Catalog designation: deterministic, coordinate-derived (spec §7).</summary>
public static class Designation
{
    public static string For(HexCoordinate coord) => $"SGC {coord.X:D4}-{coord.Y:D4}";
}
```

`src/Core/Generation/HexResult.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Core.Generation;

public sealed class HexResult
{
    public HexCoordinate Coordinate { get; }
    public StarSystem? System { get; }
    public bool IsEmpty => System == null;

    public HexResult(HexCoordinate coordinate, StarSystem? system)
    {
        Coordinate = coordinate;
        System = system;
    }
}
```

`src/Core/Generation/Generator.cs`:

```csharp
using StarGen.Core.Model;
using StarGen.Core.Naming;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class Generator
{
    /// <summary>Baseline stellar density (spec §4 stage 0). Tunable.</summary>
    public const double StellarDensity = 0.5;

    public static HexResult Generate(ulong masterSeed, HexCoordinate coord)
    {
        var ctx = new RollContext(masterSeed, coord);

        if (ctx.NextDouble(RollChannel.Presence) >= StellarDensity)
            return new HexResult(coord, null);

        var system = new StarSystem(Designation.For(coord));
        // PIPELINE (later tasks append stages here, in order):
        // StarGenerator.Generate(ctx, system);
        // BodyGenerator.Generate(ctx, system);
        // SocietyGenerator.Generate(ctx, system);
        // NameGenerator.AssignNames(ctx, system);
        // OverlayResolver.Resolve(ctx, system);
        return new HexResult(coord, system);
    }
}
```

- [ ] **Step 4: Run** — `dotnet test --filter PresenceTests` — Expected: PASS (4 tests).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: generator skeleton with presence roll and designation"`

---

### Task 7: Star generation (arrangement, types, ages, orbit slots)

**Files:**
- Create: `src/Core/Content/StarTypes.cs`, `src/Core/Generation/StarGenerator.cs`
- Modify: `src/Core/Generation/Generator.cs` (uncomment/insert `StarGenerator.Generate(ctx, system);`)
- Test: `tests/Core.Tests/Generation/StarGeneratorTests.cs`

**Interfaces:**
- Consumes: `RollContext`, `WeightedTable<T>`, model types.
- Produces:
  - `sealed class StarTypeDef`: `string Id`, `string DisplayName`, `int MinSlots`, `int MaxSlots`, `int HabStart`, `int HabEnd` (`-1/-1` = no habitable band); ctor with all six.
  - `static class StarTypes`: `static readonly WeightedTable<StarTypeDef> Table`, `static readonly WeightedTable<StarArrangement> Arrangement`, `static readonly WeightedTable<StarAge> Age`.
  - `static class StarGenerator`: `static void Generate(RollContext ctx, StarSystem system)` — fills `Arrangement`, `Stars` (primary + companions), each star's `Slots` with band assignment; companions get `CompanionSlotIndex` in the outer half of the primary's slots and 0–3 slots of their own.

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Generation/StarGeneratorTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Generation;

public class StarGeneratorTests
{
    private static List<StarSystem> Sample(ulong seed, int count)
    {
        var systems = new List<StarSystem>();
        for (int x = 0; systems.Count < count && x < count * 4; x++)
        {
            var r = Generator.Generate(seed, new HexCoordinate(x % 100, x / 100));
            if (r.System != null) systems.Add(r.System);
        }
        return systems;
    }

    [Fact]
    public void EverySystem_HasPrimaryWithSlots()
    {
        foreach (var s in Sample(3, 300))
        {
            Assert.NotEmpty(s.Stars);
            Assert.Null(s.Stars[0].CompanionSlotIndex);
            Assert.NotEmpty(s.Stars[0].Slots);
            Assert.False(string.IsNullOrEmpty(s.Stars[0].TypeName));
        }
    }

    [Fact]
    public void StarCount_MatchesArrangement()
    {
        foreach (var s in Sample(3, 300))
        {
            int expected = s.Arrangement switch
            {
                StarArrangement.Single => 1,
                StarArrangement.Binary => 2,
                _ => 3,
            };
            Assert.Equal(expected, s.Stars.Count);
        }
    }

    [Fact]
    public void Bands_AreOrderedInnerHabitableOuter()
    {
        foreach (var star in Sample(3, 300).SelectMany(s => s.Stars))
        {
            OrbitBand last = OrbitBand.Inner;
            foreach (var slot in star.Slots)
            {
                Assert.True(slot.Band >= last, "bands must never regress inner<-hab<-outer");
                last = slot.Band;
            }
        }
    }

    [Fact]
    public void Companions_OccupyValidPrimarySlot_AndDontNest()
    {
        foreach (var s in Sample(5, 300))
        {
            var primary = s.Stars[0];
            foreach (var companion in s.Stars.Skip(1))
            {
                Assert.NotNull(companion.CompanionSlotIndex);
                Assert.InRange(companion.CompanionSlotIndex!.Value, 0, primary.Slots.Count - 1);
                Assert.True(companion.Slots.Count <= 3);
            }
        }
    }

    [Fact]
    public void AllArrangements_Occur()
    {
        var arrangements = Sample(3, 500).Select(s => s.Arrangement).Distinct().ToList();
        Assert.Contains(StarArrangement.Single, arrangements);
        Assert.Contains(StarArrangement.Binary, arrangements);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter StarGeneratorTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Content/StarTypes.cs`:

```csharp
using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

public sealed class StarTypeDef
{
    public string Id { get; }
    public string DisplayName { get; }
    public int MinSlots { get; }
    public int MaxSlots { get; }   // inclusive
    public int HabStart { get; }   // slot index; -1 = no habitable band
    public int HabEnd { get; }     // inclusive

    public StarTypeDef(string id, string displayName, int minSlots, int maxSlots,
                       int habStart, int habEnd)
    {
        Id = id; DisplayName = displayName;
        MinSlots = minSlots; MaxSlots = maxSlots;
        HabStart = habStart; HabEnd = habEnd;
    }
}

/// <summary>First-draft star content — tunable data, original terminology.</summary>
public static class StarTypes
{
    public static readonly WeightedTable<StarTypeDef> Table = new(
        (new StarTypeDef("ember_dwarf",    "ember dwarf",        3,  6, 1, 1), 30),
        (new StarTypeDef("amber_dwarf",    "amber dwarf",        4,  8, 2, 3), 25),
        (new StarTypeDef("gold_main",      "gold main-sequence", 5, 10, 3, 4), 20),
        (new StarTypeDef("white_blaze",    "white blaze",        6, 11, 5, 6), 10),
        (new StarTypeDef("blue_titan",     "blue titan",         6, 12, 8, 9),  4),
        (new StarTypeDef("ashen_remnant",  "ashen remnant",      2,  5, -1, -1), 8),
        (new StarTypeDef("collapsed_core", "collapsed core",     1,  4, -1, -1), 3));

    public static readonly WeightedTable<StarArrangement> Arrangement = new(
        (StarArrangement.Single, 70),
        (StarArrangement.Binary, 25),
        (StarArrangement.Trinary, 5));

    public static readonly WeightedTable<StarAge> Age = new(
        (StarAge.Young, 20),
        (StarAge.Mature, 55),
        (StarAge.Old, 25));
}
```

`src/Core/Generation/StarGenerator.cs`:

```csharp
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class StarGenerator
{
    public static void Generate(RollContext ctx, StarSystem system)
    {
        system.Arrangement = StarTypes.Arrangement.Pick(ctx.NextDouble(RollChannel.StarArrangement));
        int starCount = system.Arrangement switch
        {
            StarArrangement.Single => 1,
            StarArrangement.Binary => 2,
            _ => 3,
        };

        for (int i = 0; i < starCount; i++)
        {
            var def = StarTypes.Table.Pick(ctx.NextDouble(RollChannel.StarType, 0, i));
            var star = new Star
            {
                TypeId = def.Id,
                TypeName = def.DisplayName,
                Age = StarTypes.Age.Pick(ctx.NextDouble(RollChannel.StarAge, 0, i)),
            };

            // Companions carry a small close-in slot set; primaries a full one.
            int slotCount = i == 0
                ? ctx.NextInt(RollChannel.SlotCount, def.MinSlots, def.MaxSlots + 1, 0, i)
                : ctx.NextInt(RollChannel.SlotCount, 0, 4, 0, i);

            for (int s = 0; s < slotCount; s++)
                star.Slots.Add(new OrbitSlot { Index = s, Band = BandFor(def, s) });

            system.Stars.Add(star);
        }

        // Place companions in the outer half of the primary's slots (spec §5).
        var primary = system.Stars[0];
        for (int i = 1; i < system.Stars.Count; i++)
        {
            int half = primary.Slots.Count / 2;
            int slot = ctx.NextInt(RollChannel.CompanionSlot, half,
                                   primary.Slots.Count, 0, i);
            system.Stars[i].CompanionSlotIndex = slot;
        }
    }

    private static OrbitBand BandFor(StarTypeDef def, int slotIndex)
    {
        if (def.HabStart < 0 || slotIndex < def.HabStart) return
            def.HabStart < 0 && slotIndex >= 0 ? OrbitBand.Outer   // no-band stars: all outer
            : OrbitBand.Inner;
        if (slotIndex <= def.HabEnd) return OrbitBand.Habitable;
        return OrbitBand.Outer;
    }
}
```

Note the no-habitable-band rule: `ashen_remnant` / `collapsed_core` slots are all `Outer` (cold, dead systems) — the ternary in `BandFor` handles this; keep it exactly as written.

- [ ] **Step 4: Wire into pipeline** — in `Generator.Generate`, replace the marker comment line `// StarGenerator.Generate(ctx, system);` with the real call `StarGenerator.Generate(ctx, system);`.

- [ ] **Step 5: Run** — `dotnet test --filter StarGeneratorTests` — Expected: PASS (5 tests). Also run full `dotnet test` — all previous tests still pass.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: star arrangement, types, ages, orbit slots with bands"`

---

### Task 8: Body generation (kind, size, atmosphere, hydrographics, biosphere, settlement)

**Files:**
- Create: `src/Core/Content/BodyTables.cs`, `src/Core/Generation/BodyGenerator.cs`
- Modify: `src/Core/Generation/Generator.cs` (insert `BodyGenerator.Generate(ctx, system);` after the star stage)
- Test: `tests/Core.Tests/Generation/BodyGeneratorTests.cs`

**Interfaces:**
- Consumes: model, `RollContext`, `WeightedTable<T>`, `StarTypes`.
- Produces:
  - `static class BodyTables`: `WeightedTable<BodyKind?> Kind` (null = empty slot; `Wreckage` never appears here — overlay-only), `WeightedTable<int> RockySize`, `WeightedTable<int> GasGiantSize`, `WeightedTable<Atmosphere> Atmo`, `WeightedTable<Biosphere> Bio`, `WeightedTable<Settlement> SettlementTable`; plus modifier factories `Func<BodyKind?,double> KindModifier(OrbitBand)`, `Func<Atmosphere,double> AtmoModifier(int size, OrbitBand)`, `Func<Biosphere,double> BioModifier(Atmosphere, OrbitBand)`, `Func<Settlement,double> SettlementModifier(Biosphere, OrbitBand)`.
  - `static class BodyGenerator`: `static void Generate(RollContext ctx, StarSystem system)` — fills every slot of every star; also `static Body GenerateBody(RollContext ctx, BodyKind kind, OrbitBand band, int starIndex, int slotIndex)` (reused by satellites in Task 9; satellite draws pass distinct index/subIndex — see channel comments).

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Generation/BodyGeneratorTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Generation;

public class BodyGeneratorTests
{
    public static List<Body> SampleBodies(ulong seed, int hexes)
    {
        var bodies = new List<Body>();
        for (int x = 0; x < hexes; x++)
        {
            var r = Generator.Generate(seed, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            bodies.AddRange(r.System.Stars
                .SelectMany(st => st.Slots)
                .Where(sl => sl.Body != null)
                .Select(sl => sl.Body!));
        }
        return bodies;
    }

    [Fact]
    public void Bodies_AreGenerated_AndSomeSlotsAreEmpty()
    {
        var bodies = SampleBodies(9, 600);
        Assert.NotEmpty(bodies);
        int slots = 0, filled = 0;
        for (int x = 0; x < 600; x++)
        {
            var r = Generator.Generate(9, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            foreach (var sl in r.System.Stars.SelectMany(st => st.Slots))
            {
                slots++;
                if (sl.Body != null) filled++;
            }
        }
        Assert.True(filled < slots, "some slots must stay empty (derelict-fleet overlay needs them)");
    }

    [Fact]
    public void Wreckage_NeverGeneratedInBaseline() =>
        Assert.DoesNotContain(SampleBodies(9, 600), b => b.Kind == BodyKind.Wreckage);

    [Fact]
    public void GasGiants_AreDenseAtmosphere_And_Belts_AreBarrenSizeZero()
    {
        foreach (var b in SampleBodies(9, 600))
        {
            if (b.Kind == BodyKind.GasGiant) Assert.Equal(Atmosphere.Dense, b.Atmosphere);
            if (b.Kind == BodyKind.PlanetoidBelt)
            {
                Assert.Equal(0, b.Size);
                Assert.Equal(Biosphere.Barren, b.Biosphere);
            }
        }
    }

    [Fact]
    public void RichBiospheres_ClusterInHabitableBand()
    {
        int habFlourish = 0, otherFlourish = 0;
        for (int x = 0; x < 3000; x++)
        {
            var r = Generator.Generate(9, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            foreach (var sl in r.System.Stars.SelectMany(st => st.Slots))
            {
                if (sl.Body == null || sl.Body.Biosphere < Biosphere.Flourishing) continue;
                if (sl.Band == OrbitBand.Habitable) habFlourish++; else otherFlourish++;
            }
        }
        Assert.True(habFlourish > otherFlourish,
            $"flourishing+ biospheres should cluster in habitable band (hab {habFlourish} vs other {otherFlourish})");
    }

    [Fact]
    public void SettlementWithoutBiosphere_Occurs()
    {
        // "colony on a dead rock" must be possible (spec §5)
        Assert.Contains(SampleBodies(9, 3000),
            b => b.Settlement != Settlement.None && b.Biosphere == Biosphere.Barren);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter BodyGeneratorTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Content/BodyTables.cs`:

```csharp
using System;
using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

/// <summary>First-draft body content and cross-influence modifiers (spec §5).</summary>
public static class BodyTables
{
    // null = slot stays empty. Wreckage is overlay-only and never appears here.
    public static readonly WeightedTable<BodyKind?> Kind = new(
        ((BodyKind?)null, 25),
        (BodyKind.RockyWorld, 30),
        (BodyKind.IceWorld, 15),
        (BodyKind.GasGiant, 15),
        (BodyKind.PlanetoidBelt, 10));

    public static readonly WeightedTable<int> RockySize = new(
        (1, 5), (2, 8), (3, 10), (4, 12), (5, 12), (6, 10), (7, 8), (8, 5), (9, 3));

    public static readonly WeightedTable<int> GasGiantSize = new(
        (10, 3), (11, 5), (12, 5), (13, 3), (14, 2));

    public static readonly WeightedTable<Atmosphere> Atmo = new(
        (Atmosphere.None, 25), (Atmosphere.Trace, 15), (Atmosphere.Thin, 18),
        (Atmosphere.Breathable, 15), (Atmosphere.Dense, 12),
        (Atmosphere.Toxic, 10), (Atmosphere.Corrosive, 5));

    public static readonly WeightedTable<Biosphere> Bio = new(
        (Biosphere.Barren, 50), (Biosphere.Microbial, 30),
        (Biosphere.Flourishing, 15), (Biosphere.Sapient, 5));

    public static readonly WeightedTable<Settlement> SettlementTable = new(
        (Settlement.None, 70), (Settlement.Outpost, 18),
        (Settlement.Colony, 9), (Settlement.MajorWorld, 3));

    public static Func<BodyKind?, double> KindModifier(OrbitBand band) => kind => (band, kind) switch
    {
        (OrbitBand.Inner, BodyKind.IceWorld) => 0.1,
        (OrbitBand.Inner, BodyKind.GasGiant) => 0.5,
        (OrbitBand.Habitable, BodyKind.RockyWorld) => 1.5,
        (OrbitBand.Habitable, BodyKind.IceWorld) => 0.5,
        (OrbitBand.Outer, BodyKind.IceWorld) => 2.0,
        (OrbitBand.Outer, BodyKind.GasGiant) => 1.5,
        (OrbitBand.Outer, BodyKind.RockyWorld) => 0.5,
        _ => 1.0,
    };

    public static Func<Atmosphere, double> AtmoModifier(int size, OrbitBand band) => atmo =>
    {
        double m = 1.0;
        if (size < 4) m *= atmo switch          // small worlds hold little air
        {
            Atmosphere.None => 3.0,
            Atmosphere.Trace => 2.0,
            Atmosphere.Breathable or Atmosphere.Dense => 0.2,
            _ => 1.0,
        };
        if (band != OrbitBand.Habitable && atmo == Atmosphere.Breathable) m *= 0.3;
        if (band == OrbitBand.Inner && (atmo == Atmosphere.Toxic || atmo == Atmosphere.Corrosive)) m *= 1.5;
        return m;
    };

    public static Func<Biosphere, double> BioModifier(Atmosphere atmo, OrbitBand band) => bio =>
    {
        if (bio == Biosphere.Barren) return 1.0;
        double m = 1.0;
        if (band != OrbitBand.Habitable) m *= bio switch
        {
            Biosphere.Microbial => 0.5,
            Biosphere.Flourishing => 0.2,
            _ => 0.05,                           // sapient off-band: vanishingly rare
        };
        if (atmo == Atmosphere.Breathable) m *= bio == Biosphere.Microbial ? 1.5 : 3.0;
        if (atmo == Atmosphere.None || atmo == Atmosphere.Corrosive) m *= 0.1;
        return m;
    };

    public static Func<Settlement, double> SettlementModifier(Biosphere bio, OrbitBand band) => s =>
    {
        if (s == Settlement.None) return 1.0;
        double m = 1.0;
        if (bio == Biosphere.Flourishing) m *= 2.0;   // people settle where it's pleasant
        if (band == OrbitBand.Habitable) m *= 1.5;
        return m;
    };
}
```

`src/Core/Generation/BodyGenerator.cs`:

```csharp
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class BodyGenerator
{
    public static void Generate(RollContext ctx, StarSystem system)
    {
        for (int starIndex = 0; starIndex < system.Stars.Count; starIndex++)
        {
            var star = system.Stars[starIndex];
            foreach (var slot in star.Slots)
            {
                // index/subIndex convention: body rolls use index = starIndex*100 + slotIndex.
                int idx = starIndex * 100 + slot.Index;
                var kind = BodyTables.Kind.Pick(
                    ctx.NextDouble(RollChannel.BodyKind, idx),
                    BodyTables.KindModifier(slot.Band));
                if (kind == null) continue;
                slot.Body = GenerateBody(ctx, kind.Value, slot.Band, idx, 0);
            }
        }
    }

    /// <summary>
    /// Shared body pipeline. idx encodes star+slot; sat = 0 for planets,
    /// 1 + satelliteIndex for satellites (Task 9), keeping draws distinct.
    /// </summary>
    public static Body GenerateBody(RollContext ctx, BodyKind kind, OrbitBand band, int idx, int sat)
    {
        var body = new Body { Kind = kind };

        switch (kind)
        {
            case BodyKind.PlanetoidBelt:
                body.Size = 0;
                body.Atmosphere = Atmosphere.None;
                body.Biosphere = Biosphere.Barren;
                break;
            case BodyKind.GasGiant:
                body.Size = BodyTables.GasGiantSize.Pick(ctx.NextDouble(RollChannel.BodySize, idx, sat));
                body.Atmosphere = Atmosphere.Dense;
                body.Biosphere = Biosphere.Barren;
                break;
            default: // RockyWorld, IceWorld (Wreckage never reaches baseline)
                body.Size = BodyTables.RockySize.Pick(ctx.NextDouble(RollChannel.BodySize, idx, sat));
                body.Atmosphere = BodyTables.Atmo.Pick(
                    ctx.NextDouble(RollChannel.Atmosphere, idx, sat),
                    BodyTables.AtmoModifier(body.Size, band));
                body.Hydrographics = RollHydro(ctx, body, band, idx, sat);
                body.Biosphere = BodyTables.Bio.Pick(
                    ctx.NextDouble(RollChannel.Biosphere, idx, sat),
                    BodyTables.BioModifier(body.Atmosphere, band));
                break;
        }

        body.Settlement = BodyTables.SettlementTable.Pick(
            ctx.NextDouble(RollChannel.Settlement, idx, sat),
            BodyTables.SettlementModifier(body.Biosphere, band));

        return body;
    }

    private static int RollHydro(RollContext ctx, Body body, OrbitBand band, int idx, int sat)
    {
        if (body.Atmosphere == Atmosphere.None || body.Atmosphere == Atmosphere.Trace) return 0;
        int hydro = ctx.NextInt(RollChannel.Hydrographics, 0, 101, idx, sat);
        return band == OrbitBand.Habitable ? hydro : hydro / 4;
    }
}
```

- [ ] **Step 4: Wire into pipeline** — in `Generator.Generate`, replace the `// BodyGenerator...` marker with `BodyGenerator.Generate(ctx, system);`.

- [ ] **Step 5: Run** — `dotnet test --filter BodyGeneratorTests` — Expected: PASS (5 tests). Full `dotnet test` still green.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: body generation with band-modified kind/atmo/bio/settlement"`

---

### Task 9: Satellites

**Files:**
- Create: `src/Core/Content/SatelliteTables.cs`
- Modify: `src/Core/Generation/BodyGenerator.cs` (satellite pass inside `Generate`)
- Test: `tests/Core.Tests/Generation/SatelliteTests.cs`

**Interfaces:**
- Consumes: `BodyGenerator.GenerateBody`, `WeightedTable<T>`.
- Produces: `static class SatelliteTables` with `WeightedTable<int> GasGiantCount`, `WeightedTable<int> WorldCount`, `WeightedTable<BodyKind> Kind` (rocky 70 / ice 30); satellite population inside `BodyGenerator.Generate` per plan resolution #3.

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Generation/SatelliteTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Model;
using Xunit;
using static StarGen.Core.Tests.Generation.BodyGeneratorTests;

namespace StarGen.Core.Tests.Generation;

public class SatelliteTests
{
    [Fact]
    public void Satellites_ExistSomewhere() =>
        Assert.Contains(SampleBodies(13, 800), b => b.Satellites.Count > 0);

    [Fact]
    public void SatelliteRules_Hold()
    {
        foreach (var body in SampleBodies(13, 800))
        {
            if (body.Kind == BodyKind.PlanetoidBelt || (body.Kind != BodyKind.GasGiant && body.Size < 4))
                Assert.Empty(body.Satellites);

            foreach (var sat in body.Satellites)
            {
                Assert.True(sat.Kind == BodyKind.RockyWorld || sat.Kind == BodyKind.IceWorld);
                Assert.True(sat.Size < body.Size || body.Kind == BodyKind.GasGiant);
                Assert.True(sat.Size >= 1);
                Assert.Empty(sat.Satellites); // no recursion
            }
        }
    }

    [Fact]
    public void GasGiants_CanHaveUpToFour()
    {
        var counts = SampleBodies(13, 3000)
            .Where(b => b.Kind == BodyKind.GasGiant)
            .Select(b => b.Satellites.Count).ToList();
        Assert.True(counts.Max() >= 3, "large moon families should occur in a big sample");
        Assert.True(counts.Max() <= 4);
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter SatelliteTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Content/SatelliteTables.cs`:

```csharp
using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

/// <summary>Satellite counts/eligibility (plan resolution #3).</summary>
public static class SatelliteTables
{
    public static readonly WeightedTable<int> GasGiantCount = new(
        (0, 10), (1, 25), (2, 30), (3, 20), (4, 15));

    public static readonly WeightedTable<int> WorldCount = new(
        (0, 40), (1, 35), (2, 20), (3, 5));

    public static readonly WeightedTable<BodyKind> Kind = new(
        (BodyKind.RockyWorld, 70), (BodyKind.IceWorld, 30));
}
```

In `BodyGenerator.Generate`, after `slot.Body = GenerateBody(...)`, add:

```csharp
AddSatellites(ctx, slot.Body, slot.Band, idx);
```

and add these methods to `BodyGenerator`:

```csharp
private static void AddSatellites(RollContext ctx, Body parent, OrbitBand band, int idx)
{
    var countTable = parent.Kind switch
    {
        BodyKind.GasGiant => SatelliteTables.GasGiantCount,
        BodyKind.RockyWorld or BodyKind.IceWorld when parent.Size >= 4 => SatelliteTables.WorldCount,
        _ => null,
    };
    if (countTable == null) return;

    int count = countTable.Pick(ctx.NextDouble(RollChannel.SatelliteCount, idx));
    for (int s = 0; s < count; s++)
    {
        var kind = SatelliteTables.Kind.Pick(ctx.NextDouble(RollChannel.SatelliteKind, idx, s));
        // sat parameter = 1 + s so satellite draws never collide with the parent's (sat = 0).
        var sat = GenerateBody(ctx, kind, band, idx, 1 + s);
        int maxSize = parent.Kind == BodyKind.GasGiant ? 4 : parent.Size - 1;
        sat.Size = 1 + ctx.NextInt(RollChannel.SatelliteSize, 0, maxSize, idx, s);
        sat.Satellites.Clear(); // guard: no satellites of satellites, ever
        parent.Satellites.Add(sat);
    }
}
```

(`GenerateBody` itself never adds satellites — only the slot pass calls `AddSatellites` — so the `Clear()` is a belt-and-braces guard, kept for the invariant test.)

- [ ] **Step 4: Run** — `dotnet test --filter SatelliteTests` — Expected: PASS (3 tests). Full suite green.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: satellite generation with kind/size/recursion constraints"`

---

### Task 10: Society generation

**Files:**
- Create: `src/Core/Content/SocietyTables.cs`, `src/Core/Generation/SocietyGenerator.cs`
- Modify: `src/Core/Generation/Generator.cs` (insert `SocietyGenerator.Generate(ctx, system);`)
- Test: `tests/Core.Tests/Generation/SocietyTests.cs`

**Interfaces:**
- Consumes: model, `RollContext`, tables.
- Produces:
  - `static class SocietyTables`: `WeightedTable<string> Government` (original archetypes below), `WeightedTable<OrderTier> Order`, `WeightedTable<PortTier> Port`, `Func<PortTier,double> PortModifier(int populationTier)`.
  - `static class SocietyGenerator`: `static void Generate(RollContext ctx, StarSystem system)` — walks every body (slots + satellites); when `body.IsInhabited`, attaches a `Society`. Population tier by settlement: Outpost 1–3, Colony 3–6, MajorWorld 6–9; native sapient (Settlement == None) 4–9.

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Generation/SocietyTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Model;
using Xunit;
using static StarGen.Core.Tests.Generation.BodyGeneratorTests;

namespace StarGen.Core.Tests.Generation;

public class SocietyTests
{
    [Fact]
    public void Society_PresentExactlyWhenInhabited()
    {
        foreach (var body in SampleBodies(21, 2000))
        {
            foreach (var b in body.Satellites.Prepend(body))
            {
                if (b.IsInhabited) Assert.NotNull(b.Society);
                else Assert.Null(b.Society);
            }
        }
    }

    [Fact]
    public void PopulationTier_MatchesSettlementScale()
    {
        foreach (var b in SampleBodies(21, 2000).Where(b => b.Society != null))
        {
            var (min, max) = b.Settlement switch
            {
                Settlement.Outpost => (1, 3),
                Settlement.Colony => (3, 6),
                Settlement.MajorWorld => (6, 9),
                _ => (4, 9), // native sapient
            };
            Assert.InRange(b.Society!.PopulationTier, min, max);
        }
    }

    [Fact]
    public void Governments_Vary()
    {
        var governments = SampleBodies(21, 3000)
            .Where(b => b.Society != null)
            .Select(b => b.Society!.Government).Distinct().ToList();
        Assert.True(governments.Count >= 4, $"only {governments.Count} distinct governments in sample");
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter SocietyTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Content/SocietyTables.cs`:

```csharp
using System;
using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

/// <summary>First-draft society content — original archetypes (spec §2).</summary>
public static class SocietyTables
{
    public static readonly WeightedTable<string> Government = new(
        ("council rule", 20),
        ("free assembly", 15),
        ("charter company", 15),
        ("steward dynasty", 12),
        ("autonomous collective", 10),
        ("faith communion", 10),
        ("warlord compact", 8),
        ("no rule", 8),
        ("machine regency", 2));

    public static readonly WeightedTable<OrderTier> Order = new(
        (OrderTier.Lawless, 10), (OrderTier.Loose, 25), (OrderTier.Orderly, 35),
        (OrderTier.Strict, 20), (OrderTier.Regimented, 10));

    public static readonly WeightedTable<PortTier> Port = new(
        (PortTier.None, 30), (PortTier.Field, 30), (PortTier.Station, 25),
        (PortTier.Orbital, 12), (PortTier.Nexus, 3));

    public static Func<PortTier, double> PortModifier(int populationTier) => port =>
        (populationTier, port) switch
        {
            ( >= 7, PortTier.Nexus) => 4.0,
            ( >= 7, PortTier.Orbital) => 2.0,
            ( >= 7, PortTier.None) => 0.1,
            ( <= 2, PortTier.Orbital) => 0.2,
            ( <= 2, PortTier.Nexus) => 0.0,
            _ => 1.0,
        };
}
```

`src/Core/Generation/SocietyGenerator.cs`:

```csharp
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class SocietyGenerator
{
    public static void Generate(RollContext ctx, StarSystem system)
    {
        for (int starIndex = 0; starIndex < system.Stars.Count; starIndex++)
        {
            var star = system.Stars[starIndex];
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                int idx = starIndex * 100 + slot.Index;
                Attach(ctx, slot.Body, idx, 0);
                for (int s = 0; s < slot.Body.Satellites.Count; s++)
                    Attach(ctx, slot.Body.Satellites[s], idx, 1 + s);
            }
        }
    }

    private static void Attach(RollContext ctx, Body body, int idx, int sat)
    {
        if (!body.IsInhabited) return;

        var (min, max) = body.Settlement switch
        {
            Settlement.Outpost => (1, 4),
            Settlement.Colony => (3, 7),
            Settlement.MajorWorld => (6, 10),
            _ => (4, 10), // native sapient, unsettled by others
        };

        int pop = ctx.NextInt(RollChannel.PopulationTier, min, max, idx, sat);
        body.Society = new Society
        {
            PopulationTier = pop,
            Government = SocietyTables.Government.Pick(ctx.NextDouble(RollChannel.Government, idx, sat)),
            Order = SocietyTables.Order.Pick(ctx.NextDouble(RollChannel.OrderTier, idx, sat)),
            Port = SocietyTables.Port.Pick(
                ctx.NextDouble(RollChannel.PortTier, idx, sat),
                SocietyTables.PortModifier(pop)),
        };
    }
}
```

- [ ] **Step 4: Wire into pipeline** — replace the `// SocietyGenerator...` marker in `Generator.Generate` with the real call.

- [ ] **Step 5: Run** — `dotnet test --filter SocietyTests` — Expected: PASS (3 tests). Full suite green.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: society generation gated on settlement/sapience"`

---

### Task 11: Naming

**Files:**
- Create: `src/Core/Content/NameTables.cs`, `src/Core/Naming/NameGenerator.cs`
- Modify: `src/Core/Generation/Generator.cs` (insert `NameGenerator.AssignNames(ctx, system);`)
- Test: `tests/Core.Tests/Naming/NamingTests.cs`

**Interfaces:**
- Consumes: model, `RollContext`, `WeightedTable<T>`.
- Produces:
  - `static class NameTables`: `WeightedTable<string> Syllables`.
  - `static class NameGenerator`: `static void AssignNames(RollContext ctx, StarSystem system)` — gives the system a `GivenName` iff any body `IsInhabited` (overlay-notable naming is handled in Task 13 by re-calling `EnsureNamed`); public `static void EnsureNamed(RollContext ctx, StarSystem system)` names an unnamed system; inhabited bodies get `Name = "{systemName} {roman(slotIndex+1)}"`, their inhabited satellites `"{parentName}-{letter}"` (a, b, ...).

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Naming/NamingTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Naming;

public class NamingTests
{
    [Fact]
    public void OnlyInhabitedSystems_GetGivenNames()
    {
        int named = 0, checkedSystems = 0;
        for (int x = 0; x < 2000; x++)
        {
            var r = Generator.Generate(31, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            checkedSystems++;
            bool inhabited = r.System.Stars.SelectMany(s => s.Slots)
                .Where(sl => sl.Body != null)
                .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!))
                .Any(b => b.IsInhabited);
            if (r.System.GivenName != null) named++;
            Assert.Equal(inhabited, r.System.GivenName != null);
        }
        Assert.True(named > 0 && named < checkedSystems, "names must be neither universal nor absent");
    }

    [Fact]
    public void GivenNames_AreDeterministic_AndPresentable()
    {
        for (int x = 0; x < 500; x++)
        {
            var a = Generator.Generate(31, new HexCoordinate(x, 2)).System;
            var b = Generator.Generate(31, new HexCoordinate(x, 2)).System;
            Assert.Equal(a?.GivenName, b?.GivenName);
            if (a?.GivenName is string name)
            {
                Assert.True(char.IsUpper(name[0]));
                Assert.InRange(name.Length, 3, 16);
            }
        }
    }

    [Fact]
    public void InhabitedBodies_GetDerivedNames()
    {
        for (int x = 0; x < 2000; x++)
        {
            var system = Generator.Generate(31, new HexCoordinate(x % 100, x / 100)).System;
            if (system?.GivenName == null) continue;
            foreach (var slot in system.Stars.SelectMany(s => s.Slots))
            {
                if (slot.Body == null) continue;
                if (slot.Body.IsInhabited)
                {
                    Assert.NotNull(slot.Body.Name);
                    Assert.StartsWith(system.GivenName, slot.Body.Name);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter NamingTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Content/NameTables.cs`:

```csharp
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

public static class NameTables
{
    public static readonly WeightedTable<string> Syllables = new(
        ("ka", 3), ("ve", 3), ("sha", 2), ("ra", 3), ("tor", 2), ("mi", 3),
        ("zen", 2), ("al", 3), ("or", 2), ("du", 2), ("ny", 2), ("bel", 2),
        ("cas", 2), ("tha", 2), ("lus", 2), ("rin", 2), ("vo", 2), ("hai", 1),
        ("mar", 2), ("sel", 2), ("qua", 1), ("dre", 2), ("no", 2), ("li", 3));
}
```

`src/Core/Naming/NameGenerator.cs`:

```csharp
using System.Globalization;
using System.Linq;
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Naming;

public static class NameGenerator
{
    private static readonly string[] Romans =
        { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV" };

    public static void AssignNames(RollContext ctx, StarSystem system)
    {
        bool inhabited = system.Stars.SelectMany(s => s.Slots)
            .Where(sl => sl.Body != null)
            .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!))
            .Any(b => b.IsInhabited);
        if (!inhabited) return;

        EnsureNamed(ctx, system);

        foreach (var slot in system.Stars.SelectMany(s => s.Slots))
        {
            if (slot.Body == null || !slot.Body.Satellites.Prepend(slot.Body).Any(b => b.IsInhabited))
                continue;
            slot.Body.Name = $"{system.GivenName} {Romans[slot.Index % Romans.Length]}";
            for (int s = 0; s < slot.Body.Satellites.Count; s++)
                if (slot.Body.Satellites[s].IsInhabited)
                    slot.Body.Satellites[s].Name = $"{slot.Body.Name}-{(char)('a' + s)}";
        }
    }

    /// <summary>Names a system if unnamed — also used when an overlay marks it notable.</summary>
    public static void EnsureNamed(RollContext ctx, StarSystem system)
    {
        if (system.GivenName != null) return;
        int syllables = ctx.NextInt(RollChannel.NameLength, 2, 4);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(ctx.NextDouble(RollChannel.NameSyllable, 0, i));
        system.GivenName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
```

- [ ] **Step 4: Wire into pipeline** — replace the `// NameGenerator...` marker in `Generator.Generate` with the real call.

- [ ] **Step 5: Run** — `dotnet test --filter NamingTests` — Expected: PASS (3 tests). Full suite green.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: two-layer naming (designation + procedural given names)"`

---

### Task 12: SystemFormatter + determinism snapshot

**Files:**
- Create: `src/Core/Text/SystemFormatter.cs`
- Test: `tests/Core.Tests/Text/FormatterTests.cs`

**Interfaces:**
- Consumes: full model.
- Produces: `static class SystemFormatter` with `static string Format(HexResult result)` — the human-readable dump used by the REPL *and* by determinism tests (string equality stands in for deep structural equality). Shape per spec §10 sketch: header line (`[coordinate] designation "name" · arrangement · overlay`), star lines, indented slot lines with band, kind, descriptors, society line, tags as `POI:` lines. Empty hex formats as `[coordinate] — empty`.

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Text/FormatterTests.cs`:

```csharp
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;
using Xunit;

namespace StarGen.Core.Tests.Text;

public class FormatterTests
{
    [Fact]
    public void Format_IsDeterministic_AcrossRegeneration()
    {
        for (int x = 0; x < 300; x++)
        {
            var coord = new HexCoordinate(x % 100, x / 100);
            var a = SystemFormatter.Format(Generator.Generate(17, coord));
            var b = SystemFormatter.Format(Generator.Generate(17, coord));
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void Format_EmptyHex() =>
        Assert.Contains("empty",
            SystemFormatter.Format(new HexResult(new HexCoordinate(1, 2), null)));

    [Fact]
    public void Format_NonEmpty_ShowsDesignationStarsAndSlots()
    {
        for (int x = 0; x < 200; x++)
        {
            var r = Generator.Generate(17, new HexCoordinate(x, 5));
            if (r.System == null) continue;
            var text = SystemFormatter.Format(r);
            Assert.Contains(r.System.Designation, text);
            Assert.Contains(r.System.Stars[0].TypeName, text);
            return;
        }
        Assert.Fail("no system found");
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter FormatterTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Text/SystemFormatter.cs`:

```csharp
using System.Text;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Core.Text;

/// <summary>Human-readable dump (spec §10) — also the determinism-test snapshot format.</summary>
public static class SystemFormatter
{
    public static string Format(HexResult result)
    {
        if (result.System == null)
            return $"[{result.Coordinate.X:D4}-{result.Coordinate.Y:D4}] — empty";

        var s = result.System;
        var sb = new StringBuilder();
        sb.Append($"[{result.Coordinate.X:D4}-{result.Coordinate.Y:D4}] {s.Designation}");
        if (s.GivenName != null) sb.Append($" \"{s.GivenName}\"");
        sb.Append($" · {s.Arrangement.ToString().ToLowerInvariant()}");
        if (s.OverlayId != null) sb.Append($" · overlay: {s.OverlayId}");
        sb.AppendLine();
        foreach (var tag in s.Tags) sb.AppendLine($"  ! {tag}");

        for (int i = 0; i < s.Stars.Count; i++)
        {
            var star = s.Stars[i];
            char label = (char)('A' + i);
            sb.Append($"  Star {label} — {star.TypeName}, {star.Age.ToString().ToLowerInvariant()}");
            if (star.CompanionSlotIndex is int cs) sb.Append($" (slot {cs})");
            sb.AppendLine();
            foreach (var slot in star.Slots) AppendSlot(sb, slot);
        }
        return sb.ToString();
    }

    private static void AppendSlot(StringBuilder sb, OrbitSlot slot)
    {
        string band = slot.Band switch
        {
            OrbitBand.Inner => "inner",
            OrbitBand.Habitable => "hab  ",
            _ => "outer",
        };
        if (slot.Body == null)
        {
            sb.AppendLine($"    {slot.Index} [{band}] —");
            return;
        }
        AppendBody(sb, slot.Body, $"    {slot.Index} [{band}] ", "              ");
        for (int i = 0; i < slot.Body.Satellites.Count; i++)
            AppendBody(sb, slot.Body.Satellites[i], $"        moon {(char)('a' + i)}: ", "              ");
    }

    private static void AppendBody(StringBuilder sb, Body body, string prefix, string indent)
    {
        var parts = new StringBuilder(prefix);
        parts.Append(Describe(body.Kind));
        if (body.Name != null) parts.Append($" \"{body.Name}\"");
        if (body.Size > 0) parts.Append($" · size {body.Size}");
        if (body.Kind == BodyKind.RockyWorld || body.Kind == BodyKind.IceWorld)
        {
            parts.Append($" · {Describe(body.Atmosphere)}");
            if (body.Hydrographics > 0) parts.Append($" · oceans {body.Hydrographics}%");
            if (body.Biosphere != Biosphere.Barren)
                parts.Append($" · {body.Biosphere.ToString().ToLowerInvariant()}");
        }
        sb.AppendLine(parts.ToString());

        if (body.Society is Society soc)
            sb.AppendLine($"{indent}{body.Settlement.ToString().ToLowerInvariant()} · pop tier {soc.PopulationTier}"
                + $" · {soc.Government} · {soc.Order.ToString().ToLowerInvariant()}"
                + $" · {soc.Port.ToString().ToLowerInvariant()} port");
        foreach (var tag in body.Tags) sb.AppendLine($"{indent}POI: {tag}");
    }

    private static string Describe(BodyKind kind) => kind switch
    {
        BodyKind.RockyWorld => "rocky world",
        BodyKind.IceWorld => "ice world",
        BodyKind.GasGiant => "gas giant",
        BodyKind.PlanetoidBelt => "planetoid belt",
        _ => "wreckage field",
    };

    private static string Describe(Atmosphere atmo) => atmo switch
    {
        Atmosphere.None => "no atmosphere",
        Atmosphere.Trace => "trace atmosphere",
        Atmosphere.Thin => "thin atmosphere",
        Atmosphere.Breathable => "breathable",
        Atmosphere.Dense => "dense atmosphere",
        Atmosphere.Toxic => "toxic atmosphere",
        _ => "corrosive atmosphere",
    };
}
```

- [ ] **Step 4: Run** — `dotnet test --filter FormatterTests` — Expected: PASS (3 tests).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: system formatter used as REPL output and determinism snapshot"`

---

### Task 13: Overlay system

**Files:**
- Create: `src/Core/Overlays/OverlayDefinition.cs`, `src/Core/Overlays/OverlayCatalog.cs`, `src/Core/Overlays/OverlayResolver.cs`
- Modify: `src/Core/Generation/Generator.cs` (insert `OverlayResolver.Resolve(ctx, system);` as the final stage)
- Test: `tests/Core.Tests/Overlays/OverlayTests.cs`

**Interfaces:**
- Consumes: model, `RollContext`, `WeightedTable<T>`, `NameGenerator.EnsureNamed`.
- Produces:
  - `sealed class OverlayDefinition`: `string Id`, `double Weight`, `Func<StarSystem,bool> IsEligible`, `Action<RollContext,StarSystem> Apply`; ctor with all four.
  - `static class OverlayCatalog`: `static readonly IReadOnlyList<OverlayDefinition> All` — `precursor_ruins`, `unstable_star`, `derelict_fleet`, `anomalous_signal` per spec §6.
  - `static class OverlayResolver`: `const double GlobalOverlayChance = 0.05`; `static void Resolve(RollContext ctx, StarSystem system)` — two-step resolution per spec §6; on apply sets `system.OverlayId` and calls `NameGenerator.EnsureNamed` (notable systems get names).

- [ ] **Step 1: Write failing tests** — `tests/Core.Tests/Overlays/OverlayTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Overlays;

public class OverlayTests
{
    private static List<StarSystem> Sample(ulong seed, int hexes)
    {
        var systems = new List<StarSystem>();
        for (int x = 0; x < hexes; x++)
        {
            var r = Generator.Generate(seed, new HexCoordinate(x % 100, x / 100));
            if (r.System != null) systems.Add(r.System);
        }
        return systems;
    }

    [Fact]
    public void Overlays_AreRare_ButPresent()
    {
        var systems = Sample(41, 4000);
        int withOverlay = systems.Count(s => s.OverlayId != null);
        Assert.InRange(withOverlay / (double)systems.Count, 0.005, 0.10);
    }

    [Fact]
    public void EligibilityInvariants_Hold()
    {
        foreach (var s in Sample(41, 6000).Where(s => s.OverlayId != null))
        {
            switch (s.OverlayId)
            {
                case "unstable_star":
                    Assert.NotEqual(StarAge.Mature, s.Stars[0].Age);
                    Assert.Contains(s.Tags, t => t.Contains("instability"));
                    break;
                case "derelict_fleet":
                    Assert.Contains(s.Stars.SelectMany(st => st.Slots),
                        sl => sl.Body?.Kind == BodyKind.Wreckage);
                    break;
                case "precursor_ruins":
                    Assert.Contains(s.Stars.SelectMany(st => st.Slots)
                        .Where(sl => sl.Body != null)
                        .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!)),
                        b => b.Tags.Contains("precursor ruins"));
                    break;
                case "anomalous_signal":
                    Assert.Contains("anomalous signal", s.Tags);
                    break;
                default:
                    Assert.Fail($"unknown overlay id {s.OverlayId}");
                    break;
            }
        }
    }

    [Fact]
    public void OverlaySystems_AreAlwaysNamed()
    {
        foreach (var s in Sample(41, 6000).Where(s => s.OverlayId != null))
            Assert.NotNull(s.GivenName);
    }

    [Fact]
    public void OverlayApplication_IsDeterministic()
    {
        for (int x = 0; x < 1500; x++)
        {
            var coord = new HexCoordinate(x % 100, x / 100);
            Assert.Equal(Generator.Generate(41, coord).System?.OverlayId,
                         Generator.Generate(41, coord).System?.OverlayId);
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter OverlayTests` — Expected: FAIL.

- [ ] **Step 3: Implement** — `src/Core/Overlays/OverlayDefinition.cs`:

```csharp
using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Overlays;

/// <summary>Curated exotic-phenomenon definition (spec §6). Pure data + functions.</summary>
public sealed class OverlayDefinition
{
    public string Id { get; }
    public double Weight { get; }
    public Func<StarSystem, bool> IsEligible { get; }
    public Action<RollContext, StarSystem> Apply { get; }

    public OverlayDefinition(string id, double weight,
                             Func<StarSystem, bool> isEligible,
                             Action<RollContext, StarSystem> apply)
    {
        Id = id; Weight = weight; IsEligible = isEligible; Apply = apply;
    }
}
```

`src/Core/Overlays/OverlayCatalog.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Model;

namespace StarGen.Core.Overlays;

/// <summary>Illustrative first-draft catalog (spec §6) — additive, pure data.</summary>
public static class OverlayCatalog
{
    public static readonly IReadOnlyList<OverlayDefinition> All = new List<OverlayDefinition>
    {
        new("precursor_ruins", 3,
            isEligible: s => Worlds(s).Any(),
            apply: (ctx, s) =>
            {
                Worlds(s).First().Tags.Add("precursor ruins");
                s.Tags.Add("notable: precursor ruins");
            }),

        new("unstable_star", 2,
            isEligible: s => s.Stars[0].Age != StarAge.Mature,
            apply: (ctx, s) =>
            {
                s.Tags.Add("stellar instability");
                foreach (var b in AllBodies(s)) b.Tags.Add("hazard: stellar instability");
            }),

        new("derelict_fleet", 2,
            isEligible: s => s.Stars.SelectMany(st => st.Slots).Any(sl => sl.Body == null),
            apply: (ctx, s) =>
            {
                var slot = s.Stars.SelectMany(st => st.Slots).First(sl => sl.Body == null);
                slot.Body = new Body { Kind = BodyKind.Wreckage, Size = 0 };
                slot.Body.Tags.Add("derelict fleet");
            }),

        new("anomalous_signal", 3,
            isEligible: _ => true,
            apply: (ctx, s) => s.Tags.Add("anomalous signal")),
    };

    private static IEnumerable<Body> Worlds(StarSystem s) => AllBodies(s)
        .Where(b => b.Kind == BodyKind.RockyWorld || b.Kind == BodyKind.IceWorld);

    private static IEnumerable<Body> AllBodies(StarSystem s) => s.Stars
        .SelectMany(st => st.Slots)
        .Where(sl => sl.Body != null)
        .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!));
}
```

`src/Core/Overlays/OverlayResolver.cs`:

```csharp
using System.Linq;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using StarGen.Core.Rng;
using StarGen.Core.Tables;

namespace StarGen.Core.Overlays;

public static class OverlayResolver
{
    /// <summary>Chance any overlay applies to a system (spec §6). Tunable.</summary>
    public const double GlobalOverlayChance = 0.05;

    public static void Resolve(RollContext ctx, StarSystem system)
    {
        // Step 1: does any overlay apply at all?
        if (ctx.NextDouble(RollChannel.OverlayChance) >= GlobalOverlayChance) return;

        // Step 2: weighted pick among eligible only; none eligible -> no overlay, no retry.
        var eligible = OverlayCatalog.All.Where(o => o.IsEligible(system))
            .Select(o => (o, o.Weight)).ToArray();
        if (eligible.Length == 0) return;

        var overlay = new WeightedTable<OverlayDefinition>(eligible)
            .Pick(ctx.NextDouble(RollChannel.OverlayPick));
        overlay.Apply(ctx, system);
        system.OverlayId = overlay.Id;
        NameGenerator.EnsureNamed(ctx, system); // notable systems have names (spec §7)
    }
}
```

- [ ] **Step 4: Wire into pipeline** — replace the `// OverlayResolver...` marker in `Generator.Generate` with the real call (it must be the **last** stage).

- [ ] **Step 5: Run** — `dotnet test --filter OverlayTests` — Expected: PASS (4 tests). Full suite green (formatter determinism now covers overlays too).

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: overlay system with two-step resolution and first catalog"`

---

### Task 14: Structural invariant suite

**Files:**
- Test: `tests/Core.Tests/Generation/StructuralInvariantTests.cs`

**Interfaces:**
- Consumes: everything. Produces: the spec §9 cross-cutting guarantees as one suite.

- [ ] **Step 1: Write the suite** — `tests/Core.Tests/Generation/StructuralInvariantTests.cs`:

```csharp
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;
using Xunit;

namespace StarGen.Core.Tests.Generation;

/// <summary>Spec §9 structural invariants over a large sample.</summary>
public class StructuralInvariantTests
{
    private const ulong Seed = 99;
    private const int Hexes = 5000;

    [Fact]
    public void AllInvariants_HoldOverLargeSample()
    {
        for (int x = 0; x < Hexes; x++)
        {
            var coord = new HexCoordinate(x % 100, x / 100);
            var result = Generator.Generate(Seed, coord);

            // determinism: full snapshot identical on regeneration
            Assert.Equal(SystemFormatter.Format(result),
                         SystemFormatter.Format(Generator.Generate(Seed, coord)));

            if (result.System == null) continue;
            var s = result.System;

            Assert.False(string.IsNullOrEmpty(s.Designation));

            foreach (var star in s.Stars.Skip(1))
                Assert.NotNull(star.CompanionSlotIndex); // exactly one primary

            foreach (var body in s.Stars.SelectMany(st => st.Slots)
                                        .Where(sl => sl.Body != null)
                                        .Select(sl => sl.Body!))
            {
                // society present exactly when inhabited
                foreach (var b in body.Satellites.Prepend(body))
                    Assert.Equal(b.IsInhabited, b.Society != null);
                // no satellite recursion
                foreach (var sat in body.Satellites)
                    Assert.Empty(sat.Satellites);
                // wreckage only via overlay
                if (body.Kind == BodyKind.Wreckage)
                    Assert.Equal("derelict_fleet", s.OverlayId);
            }
        }
    }
}
```

- [ ] **Step 2: Run** — `dotnet test --filter StructuralInvariantTests` — Expected: PASS. (If it fails, that's a real generation bug — debug it, don't loosen the test.)

- [ ] **Step 3: Commit** — `git add -A && git commit -m "test: structural invariant suite over 5000-hex sample"`

---

### Task 15: Inspector REPL

**Files:**
- Create: `src/Inspector/Repl.cs`, `src/Inspector/StatsReport.cs`
- Modify: `src/Inspector/Program.cs`
- Test: manual (console app); logic that matters (generation, formatting) is already covered in Core tests.

**Interfaces:**
- Consumes: `Generator.Generate`, `SystemFormatter.Format`, model.
- Produces: interactive console per spec §10 — commands `seed <n>`, `goto <x> <y>`, `next`, `prev`, `reroll`, `find <criterion>`, `stats <n>`, `help`, `quit`. Coordinate walk is row-major within a 32-wide sector column (x wraps at 32, then y increments), matching DESIGN.md's sector convention.

- [ ] **Step 1: Implement** — `src/Inspector/Program.cs`:

```csharp
using StarGen.Inspector;

new Repl().Run();
```

`src/Inspector/Repl.cs`:

```csharp
using System;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;

namespace StarGen.Inspector;

public sealed class Repl
{
    private const int SectorWidth = 32;
    private ulong _seed = 42;
    private int _x, _y;

    public void Run()
    {
        Console.WriteLine("StarGen inspector — 'help' for commands.");
        Show();
        while (true)
        {
            Console.Write("> ");
            var parts = (Console.ReadLine() ?? "quit")
                .Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0].ToLowerInvariant())
            {
                case "quit" or "exit": return;
                case "help":
                    Console.WriteLine("seed <n> | goto <x> <y> | next | prev | reroll | find <criterion> | stats <n> | quit");
                    Console.WriteLine("find criteria: overlay | <overlay-id> | settled | sapient");
                    break;
                case "seed" when parts.Length == 2 && ulong.TryParse(parts[1], out var s):
                    _seed = s; Show(); break;
                case "goto" when parts.Length == 3
                        && int.TryParse(parts[1], out var gx) && int.TryParse(parts[2], out var gy):
                    (_x, _y) = (Math.Max(0, gx), Math.Max(0, gy)); Show(); break;
                case "next": Step(+1); Show(); break;
                case "prev": Step(-1); Show(); break;
                case "reroll":
                    _seed = (ulong)Guid.NewGuid().GetHashCode() * 2654435761UL;
                    Console.WriteLine($"seed = {_seed}"); Show(); break;
                case "find" when parts.Length == 2: Find(parts[1]); break;
                case "stats" when parts.Length == 2 && int.TryParse(parts[1], out var n):
                    Console.WriteLine(StatsReport.Build(_seed, _x, _y, n, SectorWidth)); break;
                default:
                    Console.WriteLine("unrecognized — try 'help'"); break;
            }
        }
    }

    private void Step(int dir)
    {
        int linear = _y * SectorWidth + _x + dir;
        if (linear < 0) linear = 0;
        (_x, _y) = (linear % SectorWidth, linear / SectorWidth);
    }

    private void Show() =>
        Console.WriteLine(SystemFormatter.Format(
            Generator.Generate(_seed, new HexCoordinate(_x, _y))));

    private void Find(string criterion)
    {
        for (int i = 0; i < 50_000; i++)
        {
            Step(+1);
            var system = Generator.Generate(_seed, new HexCoordinate(_x, _y)).System;
            if (system != null && Matches(system, criterion)) { Show(); return; }
        }
        Console.WriteLine($"no match for '{criterion}' within 50,000 hexes");
    }

    private static bool Matches(StarSystem s, string criterion) => criterion switch
    {
        "overlay" => s.OverlayId != null,
        "settled" => AnyBody(s, b => b.Settlement != Settlement.None),
        "sapient" => AnyBody(s, b => b.Biosphere == Biosphere.Sapient),
        _ => s.OverlayId == criterion,
    };

    private static bool AnyBody(StarSystem s, Func<Body, bool> pred)
    {
        foreach (var star in s.Stars)
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                if (pred(slot.Body)) return true;
                foreach (var sat in slot.Body.Satellites)
                    if (pred(sat)) return true;
            }
        return false;
    }
}
```

`src/Inspector/StatsReport.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>The primary tuning instrument (spec §10): distribution summary over n hexes.</summary>
public static class StatsReport
{
    public static string Build(ulong seed, int startX, int startY, int n, int sectorWidth)
    {
        int present = 0, overlays = 0;
        var arrangements = new Dictionary<StarArrangement, int>();
        var kinds = new Dictionary<BodyKind, int>();
        var settlements = new Dictionary<Settlement, int>();
        var biospheres = new Dictionary<Biosphere, int>();

        int linear = startY * sectorWidth + startX;
        for (int i = 0; i < n; i++, linear++)
        {
            var coord = new HexCoordinate(linear % sectorWidth, linear / sectorWidth);
            var system = Generator.Generate(seed, coord).System;
            if (system == null) continue;
            present++;
            if (system.OverlayId != null) overlays++;
            Bump(arrangements, system.Arrangement);
            foreach (var star in system.Stars)
                foreach (var slot in star.Slots)
                {
                    if (slot.Body == null) continue;
                    Bump(kinds, slot.Body.Kind);
                    Bump(settlements, slot.Body.Settlement);
                    Bump(biospheres, slot.Body.Biosphere);
                }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"hexes: {n}   systems: {present} ({Pct(present, n)})   overlays: {overlays} ({Pct(overlays, present)})");
        Section(sb, "arrangements", arrangements, present);
        Section(sb, "body kinds", kinds, Total(kinds));
        Section(sb, "biospheres", biospheres, Total(biospheres));
        Section(sb, "settlements", settlements, Total(settlements));
        return sb.ToString();
    }

    private static void Bump<T>(Dictionary<T, int> d, T key) where T : notnull =>
        d[key] = d.TryGetValue(key, out var v) ? v + 1 : 1;

    private static int Total<T>(Dictionary<T, int> d) where T : notnull
    {
        int t = 0;
        foreach (var v in d.Values) t += v;
        return t;
    }

    private static string Pct(int part, int whole) =>
        whole == 0 ? "0%" : $"{100.0 * part / whole:F1}%";

    private static void Section<T>(StringBuilder sb, string title, Dictionary<T, int> d, int total)
        where T : notnull
    {
        sb.AppendLine($"{title}:");
        foreach (var kv in d)
            sb.AppendLine($"  {kv.Key,-16} {kv.Value,6}  {Pct(kv.Value, total)}");
    }
}
```

- [ ] **Step 2: Verify manually**

Run: `dotnet run --project src/Inspector`
Try: `goto 0 0`, `next` a few times, `find overlay`, `find settled`, `stats 2000`, `seed 7`, `quit`.
Expected: coherent dumps matching the spec §10 sketch shape; `stats 2000` shows presence near 50%, overlays 3–7%, settlements much rarer than biospheres.

- [ ] **Step 3: Run full suite** — `dotnet test` — Expected: all green.

- [ ] **Step 4: Commit** — `git add -A && git commit -m "feat: interactive inspector REPL with find and stats"`

---

### Task 16: CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:** none — infrastructure.

- [ ] **Step 1: Create workflow** — `.github/workflows/ci.yml`:

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build
```

- [ ] **Step 2: Verify locally** — `dotnet build --configuration Release && dotnet test --configuration Release --no-build` — Expected: green.

- [ ] **Step 3: Commit** — `git add -A && git commit -m "ci: build and test workflow"`

---

## Self-Review Notes

- **Spec coverage:** presence roll (T6), baseline pipeline (T7–T10), biosphere/settlement split (T8, tested in T8/T10), star arrangement + companion rules (T7), naming designation + given names + derived body names (T6, T11), overlay two-step resolution + catalog + never-contradict invariants (T13), stateless hash RNG + channel registry (T2–T3), all four spec §9 test categories (determinism T12/T14, distribution T4, eligibility T13, structural T14), REPL with all spec §10 commands including `find <criterion>` and `stats` (T15). Deferred per spec: factions, expanded overlay catalog, sector map, persistence.
- **Type consistency:** `RollContext.NextDouble(channel, index, subIndex)` used consistently; index convention (`starIndex*100 + slotIndex`, satellite `sat = 1 + s`) documented at both call sites (T8, T9, T10).
- **Known tuning debt (intentional):** all weights are first drafts; the Phase 1 exit criterion in DESIGN.md §4 (eyeball + `stats` review) is the tuning pass, done in the REPL after Task 15 — plan tasks assert ranges loose enough to survive tuning.
