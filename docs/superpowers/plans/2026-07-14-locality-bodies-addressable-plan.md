# Locality — Bodies Become Addressable (Foundation & Atlas) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Freeze a hex's generated system into real epoch state the first time construction touches it, give `Facility`/`Project` real body references decided once at groundbreaking, add a discrete `OrbitDistance` primitive, and make the atlas + extraction resolve against the decided placement instead of a per-render guess.

**Architecture:** A new epoch-tier `SettledSystems` registry memoizes the pure hex-tier generator's output per settled hex (idempotent, deterministic, re-derivable — so only the *set of settled hexes* persists, never the hex-tier bodies). `Facility`/`Project`/`PopulationSegment`/`Fleet` gain a `BodyRef (StarIndex, SlotIndex)` field. Facility body-assignment moves from `SystemQuery.FacilityOrbit`'s per-render type-affinity guess to a one-time, claim-aware decision at `ProjectOps.SpawnFacilityConstruction`. Extraction then reads the specific claimed body's richness. This plan is the addressing foundation; population body-assignment (§3) and off-lane routing (§5) ride a follow-on plan (`2026-07-14-locality-population-offlane-plan.md`).

**Tech Stack:** C# (`src/Core`, netstandard2.1 — C# 9 language level, so `record`/`record struct` only, no C# 10+ features), xUnit (`tests/Core.Tests`), the line-based versioned `ArtifactSerializer`. Build/test: `dotnet test StarSystemGeneration.sln`.

## Global Constraints

- **Determinism (CLAUDE.md):** stateless hash rolls keyed (step, actor id, channel); fixed iteration order everywhere (registries by id, cells by spiral index, dictionaries sorted before iterating — P6); config artifact-stamped; **the hex tier is never persisted** — the frozen bodies are re-derived from the generator on load, only the settled-hex *set* is serialized.
- **Conservation (P4):** this slice mints or sinks **nothing** — it is a locality/addressing change. No task may add a credit or goods mint/sink. Aggregate production shifting once extraction grade varies per body (Task 8) is an intended economic consequence, not a conservation violation; `ConservationTests` must stay green untouched.
- **The hex-tier generator is a pure function of `(GalaxyConfig, hex)`** — `Generator.Generate(GalaxyContext, HexCoordinate)`. Committing its result the first time anything touches the hex is deterministic regardless of trigger order; the commit must be idempotent (memoize-once).
- **Language level:** `src/Core` targets netstandard2.1 and the Unity package compiles it as C# 9. Use `readonly record struct`, `record`, switch expressions — do NOT use file-scoped-only C# 10 features, `required`, or list patterns.
- **Serializer discipline:** layers append fields, never reorder; a schema change bumps that layer's version in the `Layers` table; writer and reader change in lockstep; the reader tolerates a short record (old field count) with a length guard.
- **Knob discipline:** every calibration dial exists in `KnobRegistry` (name-sorted within its family) and is documented in `docs/TUNING.md`; `KnobRegistryTests` enforces order/uniqueness/round-trip. Structural constants are NOT knobs.
- **Metric discipline:** every macro metric exists in `MetricRegistry` (name-sorted) and is documented in `docs/SIMHEALTH.md`; `MetricRegistryTests` enforces it. `MetricRow` carries levels/counts only, a pure function of state.
- **TDD, frequent commits:** every task is failing-test → verify-fail → minimal impl → verify-pass → commit. Commit messages use conventional scopes (`feat(epoch):`, `refactor(atlas):`, `test:`), no Co-Authored-By trailer (the orchestrator adds it on merge).

---

### Task 1: `BodyRef` — the epoch-owned body address

**Files:**
- Create: `src/Core/Epoch/BodyRef.cs`
- Modify: `src/Core/Atlas/SystemQuery.cs:13-16` (delete the local `OrbitRef` record; add a using-alias)
- Test: `tests/Core.Tests/Epoch/BodyRefTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `readonly record struct StarGen.Core.Epoch.BodyRef(int StarIndex, int SlotIndex)` with `static readonly BodyRef None = new(-1, -1)` and `bool IsNone => StarIndex < 0 || SlotIndex < 0`.
  - Atlas keeps the name `OrbitRef` as a using-alias of `StarGen.Core.Epoch.BodyRef`, so all existing `OrbitRef` / `OrbitRef.None` usages and `SystemQueryTests` compile unchanged.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyRefTests.cs`:

```csharp
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyRefTests
{
    [Fact]
    public void None_IsNegativeOne_AndReportsIsNone()
    {
        Assert.Equal(-1, BodyRef.None.StarIndex);
        Assert.Equal(-1, BodyRef.None.SlotIndex);
        Assert.True(BodyRef.None.IsNone);
    }

    [Fact]
    public void RealAddress_IsNotNone_AndComparesByValue()
    {
        var a = new BodyRef(0, 2);
        var b = new BodyRef(0, 2);
        Assert.False(a.IsNone);
        Assert.Equal(a, b);
        Assert.NotEqual(a, new BodyRef(1, 2));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyRefTests`
Expected: FAIL — build error, `BodyRef` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Core/Epoch/BodyRef.cs`:

```csharp
namespace StarGen.Core.Epoch;

/// <summary>A body address inside a hex's system: which star, which orbit
/// slot. None (-1,-1) is the deep-space station orbit — a port or facility
/// with no body to dock at (bodiless system, empty reach). The epoch layer
/// owns this type; Atlas reuses it (locality slice §1: Atlas depends on
/// Epoch, never the reverse).</summary>
public readonly record struct BodyRef(int StarIndex, int SlotIndex)
{
    public static readonly BodyRef None = new(-1, -1);
    public bool IsNone => StarIndex < 0 || SlotIndex < 0;
}
```

Edit `src/Core/Atlas/SystemQuery.cs` — replace lines 13-16 (the `public readonly record struct OrbitRef(...)` block and its `None` member) with a using-alias at the top of the file. Delete:

```csharp
public readonly record struct OrbitRef(int StarIndex, int SlotIndex)
{
    public static readonly OrbitRef None = new(-1, -1);
}
```

and add, directly under the existing `using StarGen.Core.Substrate;` line near the top of the file (before `namespace StarGen.Core.Atlas;`):

```csharp
using OrbitRef = StarGen.Core.Epoch.BodyRef;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyRefTests|FullyQualifiedName~SystemQueryTests"`
Expected: PASS — `BodyRefTests` green and `SystemQueryTests` still green (the alias keeps `OrbitRef`/`OrbitRef.None` resolving to the same struct).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/BodyRef.cs src/Core/Atlas/SystemQuery.cs tests/Core.Tests/Epoch/BodyRefTests.cs
git commit -m "feat(epoch): BodyRef body address, Atlas OrbitRef becomes its alias"
```

---

### Task 2: Body-ref fields on the four site-anchored records

**Files:**
- Modify: `src/Core/Epoch/Facility.cs`
- Modify: `src/Core/Epoch/Project.cs`
- Modify: `src/Core/Epoch/PopulationSegment.cs`
- Modify: `src/Core/Epoch/FleetRecord.cs`
- Test: `tests/Core.Tests/Epoch/BodyRefTests.cs` (extend)

**Interfaces:**
- Consumes: `BodyRef`, `BodyRef.None` (Task 1).
- Produces: a settable `public BodyRef Body { get; set; } = BodyRef.None;` on each of `Facility`, `Project`, `PopulationSegment`, `FleetRecord`. Default `None` on construction (unassigned). Facility/Project are assigned at groundbreaking (Task 6); Segment/Fleet stay `None` this plan (assigned in the follow-on plan) but round-trip now (Task 4).

- [ ] **Step 1: Write the failing test**

Append to `tests/Core.Tests/Epoch/BodyRefTests.cs` (inside the class):

```csharp
    [Fact]
    public void SiteAnchoredRecords_DefaultBodyRef_IsNone()
    {
        var f = new StarGen.Core.Epoch.Facility(0, 0, 1,
            new StarGen.Core.Model.HexCoordinate(0, 0), 0, 100);
        var p = new StarGen.Core.Epoch.Project(0,
            StarGen.Core.Epoch.ProjectKind.FacilityConstruction, 1, 1, 0,
            new StarGen.Core.Model.HexCoordinate(0, 0), 4.0, 100);
        var seg = new StarGen.Core.Epoch.PopulationSegment(0, 0, 0, 0, 1.0);
        var fleet = new StarGen.Core.Epoch.FleetRecord(0, 0,
            new StarGen.Core.Model.HexCoordinate(0, 0));
        Assert.True(f.Body.IsNone);
        Assert.True(p.Body.IsNone);
        Assert.True(seg.Body.IsNone);
        Assert.True(fleet.Body.IsNone);
        f.Body = new BodyRef(0, 3);
        Assert.Equal(new BodyRef(0, 3), f.Body);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyRefTests`
Expected: FAIL — `Facility` / `Project` / `PopulationSegment` / `FleetRecord` have no `Body` member.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/Facility.cs`, add after the `public HexCoordinate Hex { get; }` line (around line 16):

```csharp
    /// <summary>The specific body this facility claimed within its hex's
    /// system — decided once at groundbreaking (locality slice §4).
    /// None until a real system is committed (bodiless system, or a
    /// gate/support asset that rides the port body).</summary>
    public BodyRef Body { get; set; } = BodyRef.None;
```

In `src/Core/Epoch/Project.cs`, add after the `public HexCoordinate Hex { get; }` line (around line 41):

```csharp
    /// <summary>The construction site's specific body — decided at
    /// groundbreaking alongside the Facility's (locality slice §4); the
    /// completed facility inherits it. None for travel/hull kinds and
    /// bodiless systems.</summary>
    public BodyRef Body { get; set; } = BodyRef.None;
```

In `src/Core/Epoch/PopulationSegment.cs`, add after the `public double LastSubsistence { get; set; } = 1.0;` line (around line 44):

```csharp
    /// <summary>The body within the port's domain this segment settled at —
    /// assigned at creation (locality slice §3, follow-on plan). None until
    /// then; the port id remains the administering domain.</summary>
    public BodyRef Body { get; set; } = BodyRef.None;
```

In `src/Core/Epoch/FleetRecord.cs`, add after the `public HexCoordinate Hex { get; set; }` line (around line 47):

```csharp
    /// <summary>The body this fleet is docked at within its hex — load-bearing
    /// for Patrol orbital coverage and off-lane staging (locality slice §2/§5,
    /// follow-on plan). None for Expedition/Convoy and until assigned.</summary>
    public BodyRef Body { get; set; } = BodyRef.None;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyRefTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/Facility.cs src/Core/Epoch/Project.cs src/Core/Epoch/PopulationSegment.cs src/Core/Epoch/FleetRecord.cs tests/Core.Tests/Epoch/BodyRefTests.cs
git commit -m "feat(epoch): body-ref fields on Facility/Project/Segment/Fleet"
```

---

### Task 3: `OrbitGeometry` — discrete `OrbitDistance` + local-hop pricing

**Files:**
- Create: `src/Core/Epoch/OrbitGeometry.cs`
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (two new `EconomyKnobs` dials)
- Modify: `src/Core/Epoch/KnobRegistry.cs` (two registry entries, name-sorted)
- Modify: `docs/design/frame/space-and-travel.md` (leg-type table: split intra-domain into hex-hop + local-hop)
- Modify: `docs/TUNING.md` (document the two knobs)
- Test: `tests/Core.Tests/Epoch/OrbitGeometryTests.cs`

**Interfaces:**
- Consumes: `BodyRef` (Task 1); `StarSystem`, `Star`, `OrbitSlot` (`StarGen.Core.Model`); `EconomyKnobs` (`StarGen.Core.Epoch`).
- Produces:
  - `static int OrbitGeometry.OrbitDistance(StarSystem system, BodyRef a, BodyRef b, int crossStarSteps)` — same star → `|a.SlotIndex − b.SlotIndex|`; different stars → `crossStarSteps + distToInner(a) + distToInner(b)` where `distToInner` is the body's slot index minus its star's innermost (min) slot index; returns `0` if either ref `IsNone`.
  - `static double OrbitGeometry.LocalHopYears(int orbitDistance, EconomyKnobs eco)` → `orbitDistance * eco.LocalHopYearsPerOrbitStep`.
  - `EconomyKnobs.CrossStarHopOrbitSteps` (double, default `8`) and `EconomyKnobs.LocalHopYearsPerOrbitStep` (double, default `0.05`), both knob-registered.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/OrbitGeometryTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class OrbitGeometryTests
{
    private static StarSystem TwoStar()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable });
        s0.Slots.Add(new OrbitSlot { Index = 2, Band = OrbitBand.Outer });
        sys.Stars.Add(s0);
        var s1 = new Star();
        s1.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner });
        s1.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable });
        sys.Stars.Add(s1);
        return sys;
    }

    [Fact]
    public void SameStar_IsSlotIndexGap()
    {
        var sys = TwoStar();
        Assert.Equal(2, OrbitGeometry.OrbitDistance(
            sys, new BodyRef(0, 0), new BodyRef(0, 2), crossStarSteps: 8));
        Assert.Equal(0, OrbitGeometry.OrbitDistance(
            sys, new BodyRef(0, 1), new BodyRef(0, 1), crossStarSteps: 8));
    }

    [Fact]
    public void CrossStar_AddsConstantPlusInnerDistances()
    {
        var sys = TwoStar();
        // star0 slot2 (2 from inner 0) + const 8 + star1 slot1 (1 from inner 0)
        Assert.Equal(11, OrbitGeometry.OrbitDistance(
            sys, new BodyRef(0, 2), new BodyRef(1, 1), crossStarSteps: 8));
    }

    [Fact]
    public void NoneRef_IsZeroDistance()
    {
        var sys = TwoStar();
        Assert.Equal(0, OrbitGeometry.OrbitDistance(
            sys, BodyRef.None, new BodyRef(0, 2), crossStarSteps: 8));
    }

    [Fact]
    public void LocalHopYears_ScalesWithDistanceAndKnob()
    {
        var eco = new EconomyKnobs { LocalHopYearsPerOrbitStep = 0.05 };
        Assert.Equal(0.15, OrbitGeometry.LocalHopYears(3, eco), 9);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~OrbitGeometryTests`
Expected: FAIL — `OrbitGeometry` and the two knobs do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Core/Epoch/OrbitGeometry.cs`:

```csharp
using System;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Intra-system geometry (locality slice §2): a deliberately
/// discrete orbit-distance metric, one level down from HexGrid.Distance —
/// slot-index gaps within a star, a fixed cross-star hop between stars.
/// Discrete on purpose: continuous orbital mechanics would be the one
/// outlier layer buying realism nothing else in the sim needs (P6/P7).</summary>
public static class OrbitGeometry
{
    /// <summary>Discrete distance between two bodies. Same star → the slot
    /// index gap; different stars → a fixed cross-star constant plus each
    /// body's distance to its own star's innermost slot. Zero if either ref
    /// is None (nothing to cross to).</summary>
    public static int OrbitDistance(StarSystem system, BodyRef a, BodyRef b,
                                    int crossStarSteps)
    {
        if (a.IsNone || b.IsNone) return 0;
        if (a.StarIndex == b.StarIndex)
            return Math.Abs(a.SlotIndex - b.SlotIndex);
        return crossStarSteps + DistToInner(system, a) + DistToInner(system, b);
    }

    private static int DistToInner(StarSystem system, BodyRef r)
    {
        if (r.StarIndex < 0 || r.StarIndex >= system.Stars.Count) return 0;
        var star = system.Stars[r.StarIndex];
        int min = int.MaxValue;
        foreach (var slot in star.Slots)
            if (slot.Index < min) min = slot.Index;
        return min == int.MaxValue ? 0 : Math.Abs(r.SlotIndex - min);
    }

    /// <summary>The local hop's transit years: OrbitDistance × the local-hop
    /// rate knob. Kept cheap relative to a lane-hop — an intra-system move is
    /// sub-step blur beside inter-port freight (locality slice §2).</summary>
    public static double LocalHopYears(int orbitDistance, EconomyKnobs eco) =>
        orbitDistance * eco.LocalHopYearsPerOrbitStep;
}
```

In `src/Core/Epoch/EpochSimConfig.cs`, in `EconomyKnobs`, add beside `OffLaneFreightHexesPerYear` (after the `RequisitionLeadYears` property, around line 696):

```csharp
    /// <summary>The fixed cross-star hop added to OrbitDistance between
    /// bodies in different stars of a multi-star system (locality slice §2) —
    /// a discrete constant, not orbital mechanics.</summary>
    public double CrossStarHopOrbitSteps { get; set; } = 8.0;
    /// <summary>World-years per unit of OrbitDistance for a local hop
    /// (intra-system body-to-body movement, locality slice §2). Small: the
    /// local hop is cheap beside a lane-hop.</summary>
    public double LocalHopYearsPerOrbitStep { get; set; } = 0.05;
```

In `src/Core/Epoch/KnobRegistry.cs`, add two entries in the `Economy.*` section, name-sorted (Economy entries are alphabetical; `CrossStarHopOrbitSteps` sorts near the top of the Economy block, `LocalHopYearsPerOrbitStep` in the L's — place each at its alphabetical position among the existing `Economy.*` keys):

```csharp
        K("Economy.CrossStarHopOrbitSteps",
          "fixed cross-star hop added to OrbitDistance in multi-star systems",
          c => c.Economy.CrossStarHopOrbitSteps,
          (c, v) => c.Economy.CrossStarHopOrbitSteps = v),
```

```csharp
        K("Economy.LocalHopYearsPerOrbitStep",
          "world-years per OrbitDistance unit for an intra-system local hop",
          c => c.Economy.LocalHopYearsPerOrbitStep,
          (c, v) => c.Economy.LocalHopYearsPerOrbitStep = v),
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~OrbitGeometryTests|FullyQualifiedName~KnobRegistryTests"`
Expected: PASS — geometry tests green and `KnobRegistryTests` still green (name-sorted, round-trips).

- [ ] **Step 5: Update the design + tuning docs**

In `docs/design/frame/space-and-travel.md`, find the leg-type table row for the intra-domain leg ("facility hex ↔ its port, hex distance, local") and split it into two composable pieces: the existing **hex-hop** (between hexes in a domain, hex distance) and a new **local hop** (between bodies within the arrival hex, priced by `OrbitDistance × Economy.LocalHopYearsPerOrbitStep`, kept cheap relative to a lane-hop). State that any leg resolving to a specific body composes hex-hop + local-hop. In `docs/TUNING.md`, add the two new `Economy.*` knobs with their consequence-of-turning notes (cross-star hop widens multi-star systems; local-hop rate makes intra-system distance cheaper/dearer).

- [ ] **Step 6: Commit**

```bash
git add src/Core/Epoch/OrbitGeometry.cs src/Core/Epoch/EpochSimConfig.cs src/Core/Epoch/KnobRegistry.cs docs/design/frame/space-and-travel.md docs/TUNING.md tests/Core.Tests/Epoch/OrbitGeometryTests.cs
git commit -m "feat(epoch): OrbitGeometry discrete OrbitDistance + local-hop pricing knobs"
```

---

### Task 4: Serializer round-trips the four body-ref fields

**Files:**
- Modify: `src/Core/Epoch/ArtifactSerializer.cs` (`Layers` table; FACILITY/FLEET/SEGMENT/PROJECT write + read)
- Test: `tests/Core.Tests/Epoch/BodyRefRoundTripTests.cs`

**Interfaces:**
- Consumes: `Facility.Body`, `Project.Body`, `PopulationSegment.Body`, `FleetRecord.Body` (Task 2).
- Produces: FACILITY/FLEET/SEGMENT/PROJECT records carry two trailing fields (`Body.StarIndex`, `Body.SlotIndex`); their layer versions bump to `("facilities", 3)`, `("fleets", 3)`, `("segments", 3)`, `("projects", 3)`. Readers parse the two trailing fields when present (length-guarded), defaulting to `BodyRef.None`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyRefRoundTripTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyRefRoundTripTests
{
    [Fact]
    public void FacilityAndProjectBodyRefs_RoundTripByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        var facility = new Facility(0,
            (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1, a0.Seat, a0.Id,
            state.WorldYear) { Body = new BodyRef(0, 2) };
        state.Facilities.Add(facility);
        var project = new Project(0, ProjectKind.FacilityConstruction,
            a0.Id, a0.Id, 0, a0.Seat, 4.0, state.WorldYear)
        { Body = new BodyRef(0, 2), TypeId = facility.TypeId, TargetId = 0 };
        state.Projects.Add(project);

        var text1 = ArtifactSerializer.ToText(state);
        var reloaded = ArtifactSerializer.FromText(text1);
        var text2 = ArtifactSerializer.ToText(reloaded);

        Assert.Equal(text1, text2);
        Assert.Equal(new BodyRef(0, 2), reloaded.Facilities[0].Body);
        Assert.Equal(new BodyRef(0, 2), reloaded.Projects[0].Body);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyRefRoundTripTests`
Expected: FAIL — the reloaded records read `BodyRef.None` (fields not written), assertion mismatch.

Note: confirm the reader entry point name in `ArtifactSerializer.cs` (search for `public static SimState FromText` / `Load`). If the public reader is named `Load(TextReader)` rather than `FromText(string)`, adapt the test's `FromText` call to the actual API (e.g. `ArtifactSerializer.Load(new StringReader(text1))`). Do not invent an API — match the file.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/ArtifactSerializer.cs`, bump the four layer versions in the `Layers` table (line ~29-33):

```csharp
        ("actors", 7), ("ports", 2), ("lanes", 3), ("facilities", 3),
        ("fleets", 3), ("segments", 3), ("events", 1), ("markets", 3),
```
```csharp
        ("pulses", 1), ("pois", 1), ("plagues", 1), ("projects", 3),
```

FACILITY write (line ~174): append the two body fields:

```csharp
            w.WriteLine(Join("FACILITY", f.Id.ToString(Inv), f.TypeId.ToString(Inv),
                f.Tier.ToString(Inv), f.Hex.Q.ToString(Inv), f.Hex.R.ToString(Inv),
                f.OwnerActorId.ToString(Inv), R(f.Condition), f.BuiltYear.ToString(Inv),
                f.CommissionedYear.ToString(Inv),
                f.Body.StarIndex.ToString(Inv), f.Body.SlotIndex.ToString(Inv)));
```

FLEET write (line ~195): append the two body fields (after `HullMap(f)`):

```csharp
            w.WriteLine(Join("FLEET", f.Id.ToString(Inv), f.OwnerActorId.ToString(Inv),
                f.Hex.Q.ToString(Inv), f.Hex.R.ToString(Inv),
                ((int)f.Posture).ToString(Inv), f.TargetId.ToString(Inv),
                f.HomePortId.ToString(Inv), R(f.Readiness),
                f.CommanderId.ToString(Inv), HullMap(f),
                f.Body.StarIndex.ToString(Inv), f.Body.SlotIndex.ToString(Inv)));
```

SEGMENT write (line ~207): append the two body fields:

```csharp
            w.WriteLine(Join("SEGMENT", s.Id.ToString(Inv), s.PortId.ToString(Inv),
                s.SpeciesId.ToString(Inv), s.CultureId.ToString(Inv), R(s.Size),
                R(s.SoL), R(s.Wealth), R(s.LastSubsistence),
                R(s.Ideology[0]), R(s.Ideology[1]), R(s.Ideology[2]),
                R(s.Ideology[3]),
                s.Body.StarIndex.ToString(Inv), s.Body.SlotIndex.ToString(Inv)));
```

PROJECT write: locate the `Join("PROJECT", ...)` line (around line 437's loop) and append `p.Body.StarIndex.ToString(Inv), p.Body.SlotIndex.ToString(Inv)` as the final two fields, matching the existing field list exactly.

FACILITY read (line ~1060): after the object-initializer sets `CommissionedYear`, set `Body` from the trailing fields with a length guard. Change the `new Facility(...) { ... }` block to capture the object then set Body:

```csharp
                    case "FACILITY":
                        if (int.Parse(f[1], Inv) != state!.Facilities.Count)
                            throw new InvalidDataException("facility ids out of order");
                        var facility = new Facility(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), int.Parse(f[3], Inv),
                            new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            int.Parse(f[6], Inv), int.Parse(f[8], Inv))
                        {
                            Condition = double.Parse(f[7], Inv),
                            CommissionedYear = long.Parse(f[9], Inv),
                        };
                        if (f.Length > 11)
                            facility.Body = new BodyRef(int.Parse(f[10], Inv),
                                                        int.Parse(f[11], Inv));
                        state.Facilities.Add(facility);
                        break;
```

FLEET read (line ~1092): after the hull-map parse and before `state.Fleets.Add(fleet)`, set Body from the fields after the hull map. The hull map is `f[10]`; the two body fields are `f[11]`, `f[12]`:

```csharp
                        if (f.Length > 12)
                            fleet.Body = new BodyRef(int.Parse(f[11], Inv),
                                                     int.Parse(f[12], Inv));
                        state.Fleets.Add(fleet);
```

SEGMENT read (line ~1135): after the ideology loop and before `state!.Segments.Add(segment)`, set Body. Ideology occupies `f[9]..f[12]`; the two body fields are `f[13]`, `f[14]`:

```csharp
                        for (int ax = 0; ax < 4; ax++)
                            segment.Ideology[ax] = double.Parse(f[9 + ax], Inv);
                        if (f.Length > 14)
                            segment.Body = new BodyRef(int.Parse(f[13], Inv),
                                                       int.Parse(f[14], Inv));
                        state!.Segments.Add(segment);
                        break;
```

PROJECT read (around line 1434-1478): after the project object is built and before `state.Projects.Add(project)`, set `project.Body` from the two trailing fields with a length guard, using the correct trailing indices for the PROJECT record's field count (count the fields written in the PROJECT write line; the body fields are the last two). Example shape:

```csharp
                        if (f.Length > BODYSTAR_INDEX + 1)
                            project.Body = new BodyRef(int.Parse(f[BODYSTAR_INDEX], Inv),
                                                       int.Parse(f[BODYSTAR_INDEX + 1], Inv));
```

Replace `BODYSTAR_INDEX` with the concrete integer index of `Body.StarIndex` in the PROJECT record (the field just before the last), determined by reading the actual PROJECT write line.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyRefRoundTripTests|FullyQualifiedName~ArtifactTests|FullyQualifiedName~ProjectTests"`
Expected: PASS — new round-trip green; `ArtifactTests` and `ProjectTests` (which round-trip facilities/projects) still green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/ArtifactSerializer.cs tests/Core.Tests/Epoch/BodyRefRoundTripTests.cs
git commit -m "feat(epoch): serialize body-ref fields (facilities/fleets/segments/projects v3)"
```

---

### Task 5: `SettledSystems` registry + idempotent commit + serialization

**Files:**
- Modify: `src/Core/Epoch/SimState.cs` (add the registry field)
- Create: `src/Core/Epoch/SystemRegistry.cs` (the commit op)
- Modify: `src/Core/Epoch/ArtifactSerializer.cs` (a new `settled` layer, sorted)
- Test: `tests/Core.Tests/Epoch/SettledSystemsTests.cs`

**Interfaces:**
- Consumes: `Generator.Generate(GalaxyContext, HexCoordinate)`, `GalaxyContext` (`StarGen.Core.Galaxy`), `HexResult.System` (`StarGen.Core.Generation`), `state.Skeleton`, `state.Skeleton.Config`.
- Produces:
  - `SimState.SettledSystems` — `public Dictionary<HexCoordinate, StarSystem> SettledSystems { get; }` (in-memory memoization; iterate sorted for any output).
  - `static StarSystem? SystemRegistry.Commit(SimState state, HexCoordinate hex)` — idempotent: on first call, generate the hex, store its `System` (which may be `null` for empty reach), return it; on later calls, return the stored value. A committed-but-empty hex stores a `null` system and is still "settled" (present in the dictionary as a key).
  - `static bool SystemRegistry.IsSettled(SimState state, HexCoordinate hex)` → `state.SettledSystems.ContainsKey(hex)`.
  - Serializer: a `settled` layer (`("settled", 1)`) writing one `SETTLED q r` line per settled hex, **sorted by (q, r)** (P6); the reader re-derives each hex's system via `SystemRegistry.Commit` (never serializing the bodies — the hex tier is never persisted).

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/SettledSystemsTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class SettledSystemsTests
{
    [Fact]
    public void Commit_IsIdempotent_ReturnsSameFrozenSystem()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var first = SystemRegistry.Commit(state, hex);
        var second = SystemRegistry.Commit(state, hex);
        Assert.True(SystemRegistry.IsSettled(state, hex));
        Assert.Same(first, second);          // memoized, not regenerated
    }

    [Fact]
    public void Commit_MatchesFreshGeneratorOutput()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var context = new StarGen.Core.Galaxy.GalaxyContext(skeleton.Config)
        { Skeleton = skeleton };
        var fresh = StarGen.Core.Generation.Generator.Generate(context, hex).System;
        var committed = SystemRegistry.Commit(state, hex);
        // deterministic pure function: same star count, same designation
        Assert.Equal(fresh?.Stars.Count ?? 0, committed?.Stars.Count ?? 0);
        Assert.Equal(fresh?.Designation, committed?.Designation);
    }

    [Fact]
    public void SettledSet_RoundTrips_AndReDerivesSystems()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        SystemRegistry.Commit(state, hex);

        var text1 = ArtifactSerializer.ToText(state);
        var reloaded = ArtifactSerializer.FromText(text1);
        var text2 = ArtifactSerializer.ToText(reloaded);

        Assert.Equal(text1, text2);
        Assert.True(SystemRegistry.IsSettled(reloaded, hex));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~SettledSystemsTests`
Expected: FAIL — `SimState.SettledSystems` / `SystemRegistry` do not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/SimState.cs`, add near the other registries (e.g. after the `Facilities` list, keeping `using StarGen.Core.Model;` already present):

```csharp
    /// <summary>Frozen hex-tier systems, keyed by hex (locality slice §1) —
    /// the first time construction/population touches a hex the generator is
    /// called once and its result memoized here. In-memory only: the bodies
    /// re-derive from the pure generator on load (the hex tier is never
    /// persisted), only the settled-hex SET is serialized. A committed empty
    /// reach stores a null system but stays a key (still "settled").
    /// Iterate SORTED for any output (P6).</summary>
    public Dictionary<HexCoordinate, StarSystem> SettledSystems { get; }
        = new Dictionary<HexCoordinate, StarSystem>();
```

(`StarSystem` is in `StarGen.Core.Model`, already imported; if the compiler flags the nullable value, declare the dictionary value type as `StarSystem?` — `Dictionary<HexCoordinate, StarSystem?>` — so an empty reach can store null. Match whichever the build accepts and keep the callers consistent.)

Create `src/Core/Epoch/SystemRegistry.cs`:

```csharp
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The commit trigger behind SimState.SettledSystems (locality
/// slice §1). The hex-tier generator is a pure function of (GalaxyConfig,
/// hex), so freezing its result the first time anything touches the hex is
/// deterministic regardless of trigger order — the commit only needs to be
/// idempotent (memoize-once).</summary>
public static class SystemRegistry
{
    /// <summary>Freeze a hex's system into state the first time it is
    /// touched; return the frozen system (null for an empty reach — still
    /// recorded as settled). Idempotent: later calls return the memoized
    /// value, never regenerate.</summary>
    public static StarSystem? Commit(SimState state, HexCoordinate hex)
    {
        if (state.SettledSystems.TryGetValue(hex, out var existing))
            return existing;
        var context = new GalaxyContext(state.Skeleton.Config)
        { Skeleton = state.Skeleton };
        var system = Generator.Generate(context, hex).System;
        state.SettledSystems[hex] = system;
        return system;
    }

    public static bool IsSettled(SimState state, HexCoordinate hex) =>
        state.SettledSystems.ContainsKey(hex);
}
```

In `src/Core/Epoch/ArtifactSerializer.cs`, add `("settled", 1)` to the `Layers` table (append at the end of the array, after `("couriers", 1)` — new layers append, never reorder):

```csharp
        ("shipments", 1), ("orders", 1), ("couriers", 1), ("settled", 1),
```

In `Save(...)`, after the couriers layer is written (find where the couriers layer ends), add:

```csharp
        Layer(w, "settled");
        // settled-hex SET only — the hex tier is never persisted (CLAUDE.md);
        // bodies re-derive from the generator on load. Sorted (q,r) for P6.
        var settledHexes = new List<HexCoordinate>(state.SettledSystems.Keys);
        settledHexes.Sort((x, y) =>
        {
            int c = x.Q.CompareTo(y.Q);
            return c != 0 ? c : x.R.CompareTo(y.R);
        });
        foreach (var h in settledHexes)
            w.WriteLine(Join("SETTLED", h.Q.ToString(Inv), h.R.ToString(Inv)));
```

In the reader's record switch, add a `SETTLED` case that re-derives via the registry:

```csharp
                    case "SETTLED":
                        SystemRegistry.Commit(state!,
                            new HexCoordinate(int.Parse(f[1], Inv),
                                              int.Parse(f[2], Inv)));
                        break;
```

(The reader has `state` fully built by the time a trailing layer parses; if `List<HexCoordinate>` needs a `using System.Collections.Generic;` in the serializer, it is already imported there.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~SettledSystemsTests|FullyQualifiedName~ArtifactTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/SimState.cs src/Core/Epoch/SystemRegistry.cs src/Core/Epoch/ArtifactSerializer.cs tests/Core.Tests/Epoch/SettledSystemsTests.cs
git commit -m "feat(epoch): SettledSystems registry, idempotent commit, settled-set serialization"
```

---

### Task 6: Body-assignment at groundbreaking (claim-aware — the two-mines fix)

**Files:**
- Create: `src/Core/Epoch/BodySiting.cs`
- Modify: `src/Core/Epoch/ProjectOps.cs` (`SpawnFacilityConstruction`, ~line 38-64)
- Test: `tests/Core.Tests/Epoch/BodySitingTests.cs`

**Interfaces:**
- Consumes: `SystemRegistry.Commit` (Task 5); `BodyRef` (Task 1); `Facility.Body`, `Project.Body` (Task 2); `StarSystem`, `BodyKind`, `Biosphere`, `OrbitBand` (`StarGen.Core.Model`); `InfraTypeId` (`StarGen.Core.Substrate`); `SystemQuery.PortOrbit` behaviour (reused as the port-body fallback — but re-implemented in Epoch to avoid an Atlas→Epoch dependency inversion).
- Produces:
  - `static BodyRef BodySiting.Assign(StarSystem? system, InfraTypeId type, BodyRef portBody, IEnumerable<BodyRef> claimed)` — the type-affinity rule (mine → belt else rock, skimmer → gas giant, agri → richest biosphere, excavation → wreckage else rock, everything else → `portBody`) evaluated against the real body list, **skipping bodies already in `claimed`** so a second same-type facility picks a different body when one exists; falls back to `portBody` (then `None`) when its substrate is absent or all claimed. Deterministic first-match in star/slot order.
  - `static BodyRef BodySiting.PortBody(StarSystem? system)` — the port's docking body (most-settled, else first habitable-band, else first body, else `None`) — the Epoch-side twin of `SystemQuery.PortOrbit`.
  - `SpawnFacilityConstruction` now commits the site hex and sets both `facility.Body` and the returned project's `Body` to the assigned `BodyRef`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodySitingTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using System.Collections.Generic;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodySitingTests
{
    private static StarSystem WithBelts()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 3 } });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 5 } });
        s0.Slots.Add(new OrbitSlot { Index = 2, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 4 } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void SecondMine_PicksADifferentBelt_WhenTheFirstIsClaimed()
    {
        var sys = WithBelts();
        var port = BodySiting.PortBody(sys);
        var first = BodySiting.Assign(sys, InfraTypeId.Mine, port,
            new List<BodyRef>());
        Assert.Equal(new BodyRef(0, 1), first);      // first belt in slot order
        var second = BodySiting.Assign(sys, InfraTypeId.Mine, port,
            new List<BodyRef> { first });
        Assert.Equal(new BodyRef(0, 2), second);     // the OTHER belt
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NonExtraction_RidesThePortBody()
    {
        var sys = WithBelts();
        var port = BodySiting.PortBody(sys);
        Assert.Equal(port, BodySiting.Assign(sys, InfraTypeId.Refinery, port,
            new List<BodyRef>()));
    }

    [Fact]
    public void NullSystem_IsNone()
    {
        Assert.True(BodySiting.Assign(null, InfraTypeId.Mine, BodyRef.None,
            new List<BodyRef>()).IsNone);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodySitingTests`
Expected: FAIL — `BodySiting` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Core/Epoch/BodySiting.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The one-time body assignment that used to be SystemQuery's
/// per-render guess (locality slice §4). Type affinity — mine → belt else
/// rock, skimmer → gas giant, agri → richest biosphere, excavation →
/// wreckage else rock — evaluated against the frozen body list, skipping
/// bodies already claimed so two same-type facilities don't collapse onto
/// one body. Deterministic first-match in star/slot order.</summary>
public static class BodySiting
{
    public static BodyRef Assign(StarSystem? system, InfraTypeId type,
                                 BodyRef portBody, IEnumerable<BodyRef> claimed)
    {
        if (system == null) return BodyRef.None;
        var taken = new HashSet<BodyRef>(claimed);
        switch (type)
        {
            case InfraTypeId.Mine:
                return FirstFree(system, BodyKind.PlanetoidBelt, taken)
                    ?? FirstFree(system, BodyKind.RockyWorld, taken) ?? portBody;
            case InfraTypeId.Skimmer:
                return FirstFree(system, BodyKind.GasGiant, taken) ?? portBody;
            case InfraTypeId.AgriComplex:
                return RichestBiosphere(system, taken) ?? portBody;
            case InfraTypeId.ExcavationSite:
                return FirstFree(system, BodyKind.Wreckage, taken)
                    ?? FirstFree(system, BodyKind.RockyWorld, taken) ?? portBody;
            default:
                return portBody;
        }
    }

    private static BodyRef? FirstFree(StarSystem system, BodyKind kind,
                                      HashSet<BodyRef> taken)
    {
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                if (slot.Body?.Kind != kind) continue;
                var r = new BodyRef(s, slot.Index);
                if (!taken.Contains(r)) return r;
            }
        return null;
    }

    private static BodyRef? RichestBiosphere(StarSystem system,
                                             HashSet<BodyRef> taken)
    {
        BodyRef? best = null;
        var bestLife = Biosphere.Barren;
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                var body = slot.Body;
                if (body == null || body.Biosphere <= bestLife) continue;
                var r = new BodyRef(s, slot.Index);
                if (taken.Contains(r)) continue;
                bestLife = body.Biosphere;
                best = r;
            }
        return best;
    }

    /// <summary>Where the port docks: most-settled body (ties by size, then
    /// star/slot order), else first habitable-band body, else first body,
    /// else None — the Epoch twin of SystemQuery.PortOrbit.</summary>
    public static BodyRef PortBody(StarSystem? system)
    {
        if (system == null) return BodyRef.None;
        var best = BodyRef.None;
        int bestRank = -1;
        var firstHabitable = BodyRef.None;
        var firstBody = BodyRef.None;
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                var body = slot.Body;
                if (body == null) continue;
                var at = new BodyRef(s, slot.Index);
                if (firstBody.IsNone) firstBody = at;
                if (firstHabitable.IsNone && slot.Band == OrbitBand.Habitable)
                    firstHabitable = at;
                if (body.Settlement == Settlement.None) continue;
                int rank = (int)body.Settlement * 1000 + body.Size;
                if (rank > bestRank) { bestRank = rank; best = at; }
            }
        if (!best.IsNone) return best;
        return !firstHabitable.IsNone ? firstHabitable : firstBody;
    }
}
```

In `src/Core/Epoch/ProjectOps.cs` `SpawnFacilityConstruction`, after `state.Facilities.Add(facility);` (line ~47) and before/after building the project, commit the hex and assign body refs. Replace the body of the method from the facility creation through the `return p;` with:

```csharp
        var facility = new Facility(state.Facilities.Count, c.TypeId,
            tier: 1, c.Hex, ownerActorId, state.WorldYear)
        { CommissionedYear = -1 };
        // groundbreaking is the §1 commit trigger: freeze the hex's system,
        // then decide this facility's body once — seeing bodies already
        // claimed by other facilities at this hex (the two-mines fix).
        var system = SystemRegistry.Commit(state, c.Hex);
        var portBody = BodySiting.PortBody(system);
        var claimed = new List<BodyRef>();
        foreach (var other in state.Facilities)           // id order (P6)
            if (other.Hex.Equals(c.Hex) && !other.Body.IsNone)
                claimed.Add(other.Body);
        facility.Body = BodySiting.Assign(system, type, portBody, claimed);
        state.Facilities.Add(facility);
        double years = Math.Max(1.0, def.ConstructionYears);
        var p = SpawnAt(state, ProjectKind.FacilityConstruction, ownerActorId,
                      funderActorId, c.PortId, c.Hex, years, priority,
                      planOrder,
                      startedYear == int.MinValue ? state.WorldYear : startedYear);
        double value = 0;
        foreach (var q in def.BuildCost)
        {
            p.PerYearBasket[(int)q.Good] = q.Quantity / years;
            value += q.Quantity
                     * Market.InitialPrice(state.Config.Economy, q.Good);
        }
        p.WagesPerYear = value / years;
        p.TypeId = c.TypeId;
        p.TargetId = facility.Id;
        p.Body = facility.Body;
        return p;
```

Ensure `System.Collections.Generic` is imported in `ProjectOps.cs` (for `List<BodyRef>`); it already uses `List<>`, so the using is present.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodySitingTests|FullyQualifiedName~ProjectTests|FullyQualifiedName~AllocationTests"`
Expected: PASS — body-siting green; project/allocation suites (which drive `SpawnFacilityConstruction`) still green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/BodySiting.cs src/Core/Epoch/ProjectOps.cs tests/Core.Tests/Epoch/BodySitingTests.cs
git commit -m "feat(epoch): decide facility body once at groundbreaking, claim-aware"
```

---

### Task 7: Atlas reads the decided placement

**Files:**
- Modify: `src/Core/Atlas/SystemQuery.cs` (`At`: facility/site rows read `Body`; settled hexes render the frozen system; retire `FacilityOrbit` as a per-render guess)
- Test: `tests/Core.Tests/Atlas/SystemQueryTests.cs` (extend)

**Interfaces:**
- Consumes: `Facility.Body`, `Project.Body` (Task 2); `SystemRegistry.IsSettled`, `state.SettledSystems` (Task 5); `BodyRef` (aliased as `OrbitRef` in this file).
- Produces: `SystemQuery.At` builds `StageFacilityRow.At` / `StageSiteRow.At` from the record's `Body` (converted to `OrbitRef`) instead of calling `FacilityOrbit(...)`; a settled hex renders bodies from `state.SettledSystems[hex]` (falling back to live generation when the registry lacks the hex — unsettled hexes render exactly as today). `FacilityOrbit` stays a public helper (still used by nothing in the render path; kept for reference/tests) but is no longer called by `At`.

- [ ] **Step 1: Write the failing test**

Append to `tests/Core.Tests/Atlas/SystemQueryTests.cs` (inside the class — it reuses the file's private `Base()` and `Hexes(...)` helpers). The row must report the facility's *assigned* `Body` verbatim, not a per-render re-guess:

```csharp
    [Fact]
    public void FacilityRow_RendersItsDecidedBody_NotAGuess()
    {
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        // a commissioned Mine whose body was DECIDED (not the type-affinity
        // guess FacilityOrbit would produce): slot (0,0), whatever it holds.
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, 1,
            hex, state.Actors[0].Id, 10) { Body = new BodyRef(0, 0) });
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);
        var row = Assert.Single(info.Facilities);
        Assert.Equal(new BodyRef(0, 0), row.At);
    }
```

Add `using StarGen.Core.Epoch;` to the file's usings if the `BodyRef` short name does not already resolve (the file already `using StarGen.Core.Epoch;` for `Facility`/`Project`, so `BodyRef` resolves; `row.At` is the `OrbitRef` alias of `BodyRef`, so the equality compiles and compares by value).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~SystemQueryTests`
Expected: FAIL — `At` currently computes the row's `At` via `FacilityOrbit`, which can differ from the assigned `Body`.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Atlas/SystemQuery.cs` `At(...)`:

Replace the system source so a settled hex renders the frozen bodies (equivalent bodies, but the one source of truth per §6), keeping live generation for unsettled hexes:

```csharp
        var state = model.State;
        StarSystem? system;
        if (StarGen.Core.Epoch.SystemRegistry.IsSettled(state, hex))
            system = state.SettledSystems[hex];
        else
        {
            var context = new GalaxyContext(model.Skeleton.Config)
            { Skeleton = model.Skeleton };
            system = Generator.Generate(context, hex).System;
        }
```

In the facilities loop, replace the `FacilityOrbit(...)` call with the decided body:

```csharp
            facilities.Add(new StageFacilityRow(f.Id, def.Name, def.Family,
                f.Tier, MarketEngine.IsActive(state, f), f.Condition,
                state.Actors[f.OwnerActorId].Name,
                system != null ? f.Body : OrbitRef.None));
```

In the sites loop, replace the `FacilityOrbit(...)` ternary with the project's decided body (falling back to the port body for non-facility kinds, whose `Body` is `None`):

```csharp
            var at = system == null
                ? OrbitRef.None
                : (!p.Body.IsNone ? p.Body : portAt);
            sites.Add(new StageSiteRow(p.Id, name, p.Progress, at));
```

Leave `FacilityOrbit`, `PortOrbit`, `First`, `RichestBiosphere`, `OrbitAngle` in place (public helpers; `PortOrbit` still supplies `portAt`). `FacilityOrbit` is now unreferenced by `At` — add a one-line doc note that it is retained for reference/tests and superseded by `BodySiting.Assign` at groundbreaking.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~SystemQueryTests|FullyQualifiedName~AtlasReadModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Atlas/SystemQuery.cs tests/Core.Tests/Atlas/SystemQueryTests.cs
git commit -m "refactor(atlas): SystemQuery reads decided body placement, not a per-render guess"
```

---

### Task 8: Extraction reads the claimed body's richness (the throughline)

**Files:**
- Modify: `src/Core/Epoch/MarketEngine.cs` (`SupplyLands` extraction path, ~line 154-170; a body-richness modifier)
- Create: `src/Core/Epoch/BodySiting.cs` — extend with `RichnessModifier` (same file as Task 6)
- Test: `tests/Core.Tests/Epoch/BodyExtractionTests.cs`

**Interfaces:**
- Consumes: `Facility.Body` (Task 2); `SystemRegistry` / `state.SettledSystems` (Task 5); `Body`, `BodyKind` (`StarGen.Core.Model`); `FieldsAt`, `ExtractionPotential` (`MarketEngine`).
- Produces:
  - `static double BodySiting.RichnessModifier(StarSystem? system, BodyRef body, InfraTypeId type)` — a deterministic multiplier in `[0.5, 1.5]` derived from the *specific claimed body* (e.g. belt/gas-giant/rock `Size` for extractors, `Biosphere` for agri), `1.0` when the body is `None`/absent or the system is null (so unsettled/legacy facilities are unchanged). Pure, no rolls.
  - `SupplyLands` multiplies the extraction `terrain` potential by `RichnessModifier(system, f.Body, f.TypeId)`, where `system` is `state.SettledSystems`'s frozen system for `f.Hex` (committed at groundbreaking, so an active facility's hex is always settled). This makes a mine on a rich body out-yield a mine on a poor one within the same hex.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyExtractionTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyExtractionTests
{
    private static StarSystem TwoBelts()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 6 } });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 1 } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void RicherBody_YieldsAHigherModifier()
    {
        var sys = TwoBelts();
        double rich = BodySiting.RichnessModifier(sys, new BodyRef(0, 0),
            InfraTypeId.Mine);
        double poor = BodySiting.RichnessModifier(sys, new BodyRef(0, 1),
            InfraTypeId.Mine);
        Assert.True(rich > poor);
        Assert.InRange(rich, 0.5, 1.5);
        Assert.InRange(poor, 0.5, 1.5);
    }

    [Fact]
    public void NoneBody_IsNeutralOne()
    {
        var sys = TwoBelts();
        Assert.Equal(1.0, BodySiting.RichnessModifier(sys, BodyRef.None,
            InfraTypeId.Mine), 9);
        Assert.Equal(1.0, BodySiting.RichnessModifier(null, new BodyRef(0, 0),
            InfraTypeId.Mine), 9);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyExtractionTests`
Expected: FAIL — `BodySiting.RichnessModifier` does not exist.

- [ ] **Step 3: Write minimal implementation**

Extend `src/Core/Epoch/BodySiting.cs` with:

```csharp
    /// <summary>A per-body extraction multiplier in [0.5, 1.5] from the
    /// SPECIFIC claimed body (locality slice §4 throughline: body-level
    /// richness variance finally reaches the price signal). Size drives the
    /// extractor bodies; biosphere drives agri. 1.0 (neutral) for a None
    /// body, a null system, or a missing body — so legacy/unsettled
    /// facilities are unchanged. Pure, deterministic, no rolls.</summary>
    public static double RichnessModifier(StarSystem? system, BodyRef body,
                                          InfraTypeId type)
    {
        if (system == null || body.IsNone) return 1.0;
        if (body.StarIndex < 0 || body.StarIndex >= system.Stars.Count)
            return 1.0;
        Body? b = null;
        foreach (var slot in system.Stars[body.StarIndex].Slots)
            if (slot.Index == body.SlotIndex) { b = slot.Body; break; }
        if (b == null) return 1.0;
        // Body.Size is a small integer scale; map it onto [0.5, 1.5] around
        // a neutral mid so a rich belt out-yields a poor one, an airless
        // agri world under-yields a lush one — bounded, never a mint.
        double signal = type switch
        {
            InfraTypeId.AgriComplex => (int)b.Biosphere,
            _ => b.Size,
        };
        double norm = System.Math.Max(0.0, System.Math.Min(1.0, signal / 6.0));
        return 0.5 + norm;               // [0.5, 1.5]
    }
```

In `src/Core/Epoch/MarketEngine.cs` `SupplyLands`, resolve the facility's frozen system and apply the modifier to the extraction terrain. After `var fields = FieldsAt(state, f.Hex);` (line ~154), add:

```csharp
            state.SettledSystems.TryGetValue(f.Hex, out var fSystem);
```

and inside the `foreach (var good in def.Produces)` loop, wrap the `terrain` computation (line ~167):

```csharp
                double terrain = ExtractionPotential((InfraTypeId)f.TypeId,
                                                     good, fields)
                    * BodySiting.RichnessModifier(fSystem, f.Body,
                                                  (InfraTypeId)f.TypeId);
```

(If `state.SettledSystems` values are typed `StarSystem?`, `fSystem` is already nullable; `RichnessModifier` accepts null.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyExtractionTests|FullyQualifiedName~ConservationTests|FullyQualifiedName~InteriorEconomyTests"`
Expected: PASS — richness modifier green; **`ConservationTests` still green** (a bounded multiplier on extraction throughput is not a mint — production shifting is intended, conservation holds because value flows through the same wage/sale paths).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/BodySiting.cs src/Core/Epoch/MarketEngine.cs tests/Core.Tests/Epoch/BodyExtractionTests.cs
git commit -m "feat(epoch): extraction reads the claimed body's richness (locality throughline)"
```

---

### Task 9: `Settlement.SettledHexes` sim-health metric

**Files:**
- Modify: `src/Core/Epoch/Health/MetricsOps.cs` (`MetricRow` gains a count; `Snapshot` fills it)
- Modify: `src/Core/Epoch/Health/MetricRegistry.cs` (one entry, name-sorted)
- Modify: `docs/SIMHEALTH.md` (document the metric)
- Test: `tests/Core.Tests/Epoch/MetricRegistryTests.cs` is the guard (extend or rely on existing enforcement) + a focused snapshot test in `tests/Core.Tests/Epoch/SettledSystemsTests.cs`

**Interfaces:**
- Consumes: `state.SettledSystems` (Task 5); `MetricRow`, `MetricsOps.Snapshot` (`StarGen.Core.Epoch`).
- Produces: `MetricRow` gains `int SettledHexes`; `MetricRegistry` gains `M("Settlement.SettledHexes", ...)`; `docs/SIMHEALTH.md` documents growth-with-visitation (no eviction — evidence-based scrutiny per §7).

- [ ] **Step 1: Write the failing test**

Append to `tests/Core.Tests/Epoch/SettledSystemsTests.cs`:

```csharp
    [Fact]
    public void SettledHexesMetric_CountsCommittedHexes()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.NotNull(MetricRegistry.Find("Settlement.SettledHexes"));
        SystemRegistry.Commit(state, state.Actors[0].Seat);
        SystemRegistry.Commit(state,
            new StarGen.Core.Model.HexCoordinate(
                state.Actors[0].Seat.Q + 3, state.Actors[0].Seat.R));
        var row = MetricsOps.Snapshot(state);
        Assert.Equal(2, row.SettledHexes);
        Assert.Equal(2.0,
            MetricRegistry.Find("Settlement.SettledHexes")!.Get(row), 9);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~SettledSystemsTests|FullyQualifiedName~MetricRegistryTests"`
Expected: FAIL — `MetricRow.SettledHexes` and the registry entry do not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/Health/MetricsOps.cs`, add `int SettledHexes` to the `MetricRow` record (append as the last positional parameter):

```csharp
public sealed record MetricRow(
    int Epoch, int WorldYear, MoneyRow Money,
    int LivePolities, int NegativeTreasuries,
    double MinPolityCredits, double MedianPolityCredits,
    double MaxPolityCredits,
    double Population, double MeanSoL,
    int EndowedEntries, double ConservationResidual,
    int SettledHexes);
```

In `Snapshot(...)`, pass the count in the `return new MetricRow(...)` (append `state.SettledSystems.Count` as the final argument):

```csharp
        return new MetricRow(state.EpochIndex, state.WorldYear, money,
            credits.Count, negative, min, median, max,
            pop, pop <= 0 ? 0.0 : sol / pop,
            endowed, residual, state.SettledSystems.Count);
```

In `src/Core/Epoch/Health/MetricRegistry.cs`, add the entry in name-sorted position (the `Settlement.*` family sorts after `Segment.*`, before any later family — insert after the `Segment.Population` entry):

```csharp
        // ---- Settlement (locality — frozen hex-tier state) ----
        M("Settlement.SettledHexes",
          "committed hexes with a frozen system (grows with visitation, no eviction)",
          r => r.SettledHexes),
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~SettledSystemsTests|FullyQualifiedName~MetricRegistryTests"`
Expected: PASS — snapshot count green; `MetricRegistryTests` (order/uniqueness/doc/accessor) green.

- [ ] **Step 5: Document + commit**

In `docs/SIMHEALTH.md`, add `Settlement.SettledHexes` under a new "Settlement" heading: what it means (frozen hex-tier states accumulate as construction/population touch hexes), healthy shape (monotonic non-decreasing, bounded by how much of the galaxy gets developed), and the known open question (no eviction proposed — this metric is how growth gets evidence-based scrutiny per the design's §7).

```bash
git add src/Core/Epoch/Health/MetricsOps.cs src/Core/Epoch/Health/MetricRegistry.cs docs/SIMHEALTH.md tests/Core.Tests/Epoch/SettledSystemsTests.cs
git commit -m "feat(health): Settlement.SettledHexes metric tracks frozen hex growth"
```

---

## Slice-end gates (run before handing off, not a task)

- `dotnet test StarSystemGeneration.sln` fully green — the hex-tier (Phase-1) suite never broke; `ConservationTests` and `DeterminismTests` green.
- Determinism byte-identity: two full runs at the same config produce byte-identical artifacts; save→load→save is byte-identical (Tasks 4 & 5 round-trip tests are the unit witnesses).
- Golden re-freeze: siting/extraction/atlas output legitimately changed (Tasks 6-8) — re-freeze the goldens once, at slice end (red-window inside the slice), per project discipline.
- REPL eyeball (the taste gate): drill the atlas SystemStage into a developed hex and confirm facilities sit on their *decided* bodies (two same-type extractors on different bodies), not a per-render guess. Pipe via `printf 'cmd\n' | dotnet run --project src/Inspector`.
