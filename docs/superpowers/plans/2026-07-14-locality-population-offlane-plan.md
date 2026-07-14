# Locality — Population & Off-lane (Consequences) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**FOLLOW-ON:** This plan depends on `2026-07-14-locality-bodies-addressable-plan.md` being merged first. It consumes `BodyRef`, `OrbitGeometry`, `SystemRegistry`, `SettledSystems`, `BodySiting`, and the body-ref fields that plan lands. Do not start it until Plan 1's gates pass.

**Goal:** Give population segments a real body address, weight facility staffing by intra-system distance, and make off-lane travel a real elective alternative to a blockaded lane (with a detection-roll risk model driven by Patrol orbital coverage) — the load-bearing consequences of Plan 1's addressing foundation.

**Architecture:** Segments gain a body at creation (the settled port/colony body — the finest cheap address). Colony-founding facilities that bypassed `SpawnFacilityConstruction` get the same claim-aware body assignment. Facility staffing weights each segment's labor by `hex-hop + local-hop` distance to the facility's body. Patrol enforcement coverage falls off with `OrbitDistance` from where the fleet is docked; that same coverage modulates a new **detection roll** for off-lane freight, so a route through under-covered space is safer to run. `ShipmentOps.PlanRoute` gains a first-class off-lane alternative computed alongside the lane path; a blockaded/quarantined/dead lane makes the off-lane route a real elected option instead of a stall.

**Tech Stack:** C# (`src/Core`, netstandard2.1 / C# 9), xUnit (`tests/Core.Tests`), the deterministic hash-roll layer (`Rng.RollChannel` / `EpochRolls`).

## Global Constraints

- **Determinism (CLAUDE.md):** the ONLY new nondeterminism permitted is the detection roll, keyed `(step = epoch, actor = shipment owner, subIndex = shipment id)` on a NEW `RollChannel.ShipmentDetection = 77` — the exact pattern the piracy (75) and interdiction (76) channels already use. Fixed iteration order everywhere (registries by id, dictionaries sorted before iterating — P6). No floating hash-roll nondeterminism; no per-render state mutation.
- **Conservation (P4):** this slice mints or sinks **nothing**. A detected off-lane cargo is *seized*, never destroyed: it posts (conserved) to the detecting patrol's nearest owned port exactly as the interdiction path already does (`BookOps.PostSupply` + `FleetOps.NearestOwnedPortId`); a patrol with no port to land a prize takes nothing. `ConservationTests` stays green untouched. Staffing re-weighting redistributes *who* earns the labor share, never the total.
- **Language level:** netstandard2.1 / C# 9 — `record`, `readonly record struct`, switch expressions only.
- **Knob discipline:** new dials go in `KnobRegistry` (name-sorted, `KnobRegistryTests`-enforced) and `docs/TUNING.md`.
- **DEFERRED — do NOT write tasks for these (design boundary):** the exact off-lane *election* weighting formula (urgency / cargo value / risk tolerance) — this plan implements the structural capability and a minimal severed-lane election only; local-hop cost scaling with port tier / astrogation tech; intra-domain population *relocation* between bodies over time (only the *arrival* address gets finer); local-hop travel visualization in the atlas.
- **TDD, frequent commits:** failing-test → verify-fail → minimal impl → verify-pass → commit. Conventional-commit scopes, no Co-Authored-By trailer.

---

### Task 1: Segments gain a body at creation

**Files:**
- Create: `src/Core/Epoch/PopulationSiting.cs`
- Modify: `src/Core/Epoch/Phases.cs` (homeworld segment ~line 1244; migration `FindOrFoundSegment` ~line 1521)
- Modify: `src/Core/Epoch/ProjectOps.cs` (colony segment ~line 550)
- Modify: `src/Core/Epoch/Interpolity/NativeOps.cs` (native segment ~line 114)
- Test: `tests/Core.Tests/Epoch/PopulationSitingTests.cs`

**Interfaces:**
- Consumes: `SystemRegistry.Commit`, `BodySiting.PortBody`, `BodyRef`, `PopulationSegment.Body` (Plan 1); `state.Ports[portId].Hex`.
- Produces:
  - `static BodyRef PopulationSiting.Assign(SimState state, int portId)` — commits the port's hex (idempotent) and returns the settled port body (`BodySiting.PortBody`) as the segment's arrival address; `BodyRef.None` for a bodiless reach. This is the "finest cheap address within the domain" — the fuller best-opportunity-across-domain scoring is a deferred refinement (a domain-wide body scan would re-commit far more hexes than the design's §4 chicken-and-egg note allows).
  - Each of the four segment-creation sites sets `segment.Body = PopulationSiting.Assign(state, <its portId>)` right after constructing the segment.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/PopulationSitingTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class PopulationSitingTests
{
    [Fact]
    public void Assign_SettlesAtThePortBody_AndCommitsTheHex()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));

        var body = PopulationSiting.Assign(state, port.Id);

        Assert.True(SystemRegistry.IsSettled(state, port.Hex));
        Assert.Equal(BodySiting.PortBody(state.SettledSystems[port.Hex]), body);
    }

    [Fact]
    public void HomeworldSegments_HaveARealBody_AfterGenesis()
    {
        var (_, state) = EpochTestKit.Seeded();
        // genesis seeds homeworld ports + segments; every homeworld segment
        // now carries a body ref (None only where the reach is bodiless).
        int withBody = 0;
        foreach (var s in state.Segments)
            if (!s.Body.IsNone) withBody++;
        Assert.True(withBody > 0,
            "expected at least one seeded segment to carry a real body ref");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~PopulationSitingTests`
Expected: FAIL — `PopulationSiting` does not exist; seeded segments carry `None`.

- [ ] **Step 3: Write minimal implementation**

Create `src/Core/Epoch/PopulationSiting.cs`:

```csharp
namespace StarGen.Core.Epoch;

/// <summary>Where a new segment settles within its administering port's
/// domain (locality slice §3). The arrival address is the settled port body
/// — the finest address that is cheap and always committed (the port's hex
/// is frozen the moment the port exists). A port-domain can hold several
/// segments across different bodies as colonies and outposts found their
/// own cores. Intra-domain relocation over time is deferred (design
/// boundary); only the arrival address gets finer here.</summary>
public static class PopulationSiting
{
    public static BodyRef Assign(SimState state, int portId)
    {
        if (portId < 0 || portId >= state.Ports.Count) return BodyRef.None;
        var system = SystemRegistry.Commit(state, state.Ports[portId].Hex);
        return BodySiting.PortBody(system);
    }
}
```

In `src/Core/Epoch/Phases.cs`, homeworld segment (~line 1244), after `var homeSegment = new PopulationSegment(...) { ... };` and before it is added to `state.Segments`, set:

```csharp
            homeSegment.Body = PopulationSiting.Assign(state, port.Id);
```

In `FindOrFoundSegment` (~line 1521), after `var founded = new PopulationSegment(...) { ... };` and before `state.Segments.Add(founded);`, set:

```csharp
        founded.Body = PopulationSiting.Assign(state, portId);
```

In `src/Core/Epoch/ProjectOps.cs` colony segment (~line 550), after `var colonySegment = new PopulationSegment(...) { ... };` and before `state.Segments.Add(colonySegment);`, set:

```csharp
        colonySegment.Body = PopulationSiting.Assign(state, port.Id);
```

In `src/Core/Epoch/Interpolity/NativeOps.cs` native segment (~line 114), after the `var segment = new PopulationSegment(...)` construction and before it is added, set `segment.Body = PopulationSiting.Assign(state, <the native segment's portId argument>);` — use the same port id passed to the `PopulationSegment` constructor at that site.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~PopulationSitingTests|FullyQualifiedName~InteriorTests|FullyQualifiedName~NativeEmergenceTests"`
Expected: PASS — siting green; interior/native suites still green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/PopulationSiting.cs src/Core/Epoch/Phases.cs src/Core/Epoch/ProjectOps.cs src/Core/Epoch/Interpolity/NativeOps.cs tests/Core.Tests/Epoch/PopulationSitingTests.cs
git commit -m "feat(epoch): segments settle at a real body at creation"
```

---

### Task 2: Colony-founding facilities get claim-aware body refs

The colony-completion path in `ProjectOps` (~line 561-572) builds its founding facilities directly via `new Facility(...)`, bypassing `SpawnFacilityConstruction` — so their `Body` defaults to `None` and their extraction (Plan 1 Task 8) never sees a body. Extract the founding into a testable helper, assign claim-aware bodies there.

**Files:**
- Modify: `src/Core/Epoch/ProjectOps.cs` (extract `FoundColonyFacilities`; call it from the colony-completion block ~line 561-572)
- Test: `tests/Core.Tests/Epoch/ColonyBodyTests.cs`

**Interfaces:**
- Consumes: `SystemRegistry.Commit`, `BodySiting.PortBody`, `BodySiting.Assign`, `BodyRef`, `Facility.Body` (Plan 1); `FoundingIndustry(SimState, HexCoordinate)` (existing private in `ProjectOps`).
- Produces: `static void ProjectOps.FoundColonyFacilities(SimState state, HexCoordinate hex, int ownerActorId, long year)` — commits the hex, founds the industry facility + (unless it is the agri complex) a subsistence agri complex, each with a claim-aware `Body`; two same-type extractors never collapse onto one body. The colony-completion block calls it in place of its inline `new Facility(...)` pair.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/ColonyBodyTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ColonyBodyTests
{
    [Fact]
    public void FoundColonyFacilities_AssignsRealBodies_AtABodyBearingHex()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        var owner = state.Actors[0].Id;
        // find a hex whose committed system actually holds bodies, so the
        // founded facilities have a real body to claim.
        HexCoordinate? sited = null;
        foreach (var cell in skeleton.Cells)
        {
            var hex = HexGrid.CellCenter(cell.Coord);
            var sys = SystemRegistry.Commit(state, hex);
            if (sys != null && sys.Stars.Count > 0)
            {
                foreach (var star in sys.Stars)
                    foreach (var slot in star.Slots)
                        if (slot.Body != null) { sited = hex; break; }
            }
            if (sited != null) break;
        }
        Assert.NotNull(sited);

        int before = state.Facilities.Count;
        ProjectOps.FoundColonyFacilities(state, sited!.Value, owner, 100);
        Assert.True(state.Facilities.Count > before);
        for (int i = before; i < state.Facilities.Count; i++)
            Assert.False(state.Facilities[i].Body.IsNone,
                "a colony facility at a body-bearing hex must claim a body");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~ColonyBodyTests`
Expected: FAIL — `ProjectOps.FoundColonyFacilities` does not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/ProjectOps.cs`, add the helper (public so the test reaches it; `FoundingIndustry` is already in this class):

```csharp
    /// <summary>Found a colony's opening facilities at a hex with decided,
    /// claim-aware body refs (locality slice §3/§4): the founding industry
    /// plus a subsistence agri complex when the industry isn't farming. Two
    /// same-type extractors never collapse onto one body. Commits the hex.</summary>
    public static void FoundColonyFacilities(SimState state,
        Model.HexCoordinate hex, int ownerActorId, long year)
    {
        var founding = FoundingIndustry(state, hex);
        var system = SystemRegistry.Commit(state, hex);
        var portBody = BodySiting.PortBody(system);
        var claimed = new List<BodyRef>();
        var f0 = new Facility(state.Facilities.Count, (int)founding, tier: 1,
            hex, ownerActorId, year)
        { Body = BodySiting.Assign(system, founding, portBody, claimed) };
        if (!f0.Body.IsNone) claimed.Add(f0.Body);
        state.Facilities.Add(f0);
        if (founding != Substrate.InfraTypeId.AgriComplex)
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)Substrate.InfraTypeId.AgriComplex, tier: 1, hex,
                ownerActorId, year)
            {
                Body = BodySiting.Assign(system,
                    Substrate.InfraTypeId.AgriComplex, portBody, claimed)
            });
    }
```

Then replace the colony-completion block's inline facility pair (~line 561-572 — the `var founding = FoundingIndustry(...)` through both `new Facility(...)` adds) with a single call, using the block's real `completionYear` and `p.OwnerActorId`:

```csharp
        FoundColonyFacilities(state, p.Hex, p.OwnerActorId, completionYear);
```

Confirm against the actual source that the removed lines founded exactly the industry + conditional agri complex (lines 564-572) and nothing else moved with them; preserve any surrounding code. Ensure `Model.HexCoordinate` resolves (the file uses `StarGen.Core.Model`; `HexCoordinate` short name likely already resolves — drop the `Model.` qualifier if so).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~ColonyBodyTests|FullyQualifiedName~ProjectTests|FullyQualifiedName~AllocationTests"`
Expected: PASS — helper green; colony/project suites still green (same facilities founded, now bodied).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/ProjectOps.cs tests/Core.Tests/Epoch/ColonyBodyTests.cs
git commit -m "feat(epoch): colony-founded facilities get claim-aware body refs"
```

---

### Task 3: Staffing weights labor by hex-hop + local-hop distance

**Files:**
- Create: `src/Core/Epoch/StaffingOps.cs`
- Modify: `src/Core/Epoch/MarketEngine.cs` (`SupplyLands` labor path ~line 154-160; `DominantEmbodiment` reuse)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (`EconomyKnobs.StaffingDistanceFalloff`)
- Modify: `src/Core/Epoch/KnobRegistry.cs` (one entry, name-sorted)
- Modify: `docs/TUNING.md`
- Test: `tests/Core.Tests/Epoch/StaffingOpsTests.cs`

**Interfaces:**
- Consumes: `OrbitGeometry.OrbitDistance`, `HexGrid.Distance`, `PopulationSegment.Body`, `Facility.Body`, `state.SettledSystems` (Plan 1); `state.Ports`, `state.Segments`.
- Produces:
  - `static double StaffingOps.ProximityWeight(SimState state, Facility f, PopulationSegment seg)` — `1 / (1 + falloff × (hexHop + localHop))`, where `hexHop = HexGrid.Distance(port.Hex, f.Hex)` for the segment's port, and `localHop = OrbitGeometry.OrbitDistance(system, seg.Body, f.Body, crossStar)` when both share the facility's hex/system (else `0`). A segment on the facility's exact body has weight `1.0`; a distant one contributes less. Pure.
  - `static double StaffingOps.WeightedWorkforce(SimState state, Facility f, int portId)` — the distance-weighted labor sum over the port's segments (the design's "nearest-by hex+local-hop" staffing). `SupplyLands` uses it in place of the flat labor sum.
  - `EconomyKnobs.StaffingDistanceFalloff` (double, default `0.15`). The exact falloff curve is tunable — this is the minimal defensible weight, not a tuned one.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/StaffingOpsTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class StaffingOpsTests
{
    [Fact]
    public void SegmentOnTheFacilitysBody_WeightsOne()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        var f = new Facility(0, (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1,
            a0.Seat, a0.Id, 0) { Body = new BodyRef(0, 1) };
        var seg = new PopulationSegment(0, port.Id, 0, 0, 3.0)
        { Body = new BodyRef(0, 1) };
        state.Segments.Add(seg);
        Assert.Equal(1.0, StaffingOps.ProximityWeight(state, f, seg), 9);
    }

    [Fact]
    public void FartherBody_WeightsLess()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        SystemRegistry.Commit(state, a0.Seat);   // freeze the hex's system
        var f = new Facility(0, (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1,
            a0.Seat, a0.Id, 0) { Body = new BodyRef(0, 0) };
        var near = new PopulationSegment(0, port.Id, 0, 0, 1.0)
        { Body = new BodyRef(0, 0) };
        var far = new PopulationSegment(1, port.Id, 0, 0, 1.0)
        { Body = new BodyRef(0, 3) };
        Assert.True(StaffingOps.ProximityWeight(state, f, near)
                  > StaffingOps.ProximityWeight(state, f, far));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~StaffingOpsTests`
Expected: FAIL — `StaffingOps` and the knob do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Core/Epoch/StaffingOps.cs`:

```csharp
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Distance-weighted facility staffing (locality slice §3): a
/// facility draws labor from its domain's segments, but proximity now
/// weights who works where — an airless mine can be crewed by commute from
/// a habitat one local-hop away, at a cost. Mirrors AttachedMarketIndex's
/// nearest-port pattern, one level finer. Pure; the exact falloff curve is
/// a tunable, not a design fork.</summary>
public static class StaffingOps
{
    /// <summary>Proximity weight in (0, 1]: 1 on the facility's own body,
    /// falling with hex-hop + local-hop distance from it.</summary>
    public static double ProximityWeight(SimState state, Facility f,
                                         PopulationSegment seg)
    {
        var eco = state.Config.Economy;
        int hexHop = 0;
        if (seg.PortId >= 0 && seg.PortId < state.Ports.Count)
            hexHop = HexGrid.Distance(state.Ports[seg.PortId].Hex, f.Hex);
        int localHop = 0;
        if (hexHop == 0 && !seg.Body.IsNone && !f.Body.IsNone
            && state.SettledSystems.TryGetValue(f.Hex, out var system)
            && system != null)
            localHop = OrbitGeometry.OrbitDistance(system, seg.Body, f.Body,
                (int)eco.CrossStarHopOrbitSteps);
        double dist = hexHop + localHop;
        return 1.0 / (1.0 + eco.StaffingDistanceFalloff * dist);
    }

    /// <summary>Distance-weighted workforce a facility can draw from its
    /// attached port's segments — the labor sum SupplyLands consumes.</summary>
    public static double WeightedWorkforce(SimState state, Facility f, int portId)
    {
        double labor = 0;
        foreach (var seg in state.Segments)               // id order (P6)
        {
            if (seg.PortId != portId) continue;
            labor += seg.Size * ProximityWeight(state, f, seg);
        }
        return labor;
    }
}
```

In `src/Core/Epoch/EpochSimConfig.cs` `EconomyKnobs`, add (near the income/labor knobs, e.g. after `LaborShare`):

```csharp
    /// <summary>How steeply facility staffing weight falls with hex-hop +
    /// local-hop distance from the facility's body (locality slice §3):
    /// weight = 1/(1 + this × distance). 0 recovers flat domain staffing.</summary>
    public double StaffingDistanceFalloff { get; set; } = 0.15;
```

In `src/Core/Epoch/KnobRegistry.cs`, add name-sorted in the `Economy.*` block:

```csharp
        K("Economy.StaffingDistanceFalloff",
          "steepness of staffing weight falloff with hex+local-hop distance",
          c => c.Economy.StaffingDistanceFalloff,
          (c, v) => c.Economy.StaffingDistanceFalloff = v),
```

In `src/Core/Epoch/MarketEngine.cs` `SupplyLands`, replace the flat labor sum with the weighted workforce. The current code (~line 155-157) is:

```csharp
            double labor = 0;
            var embodiment = DominantEmbodiment(state, port.Id, ref labor);
```

Change to keep `DominantEmbodiment` for the embodiment pick but take the labor magnitude from `StaffingOps` (a distance-weighted total for THIS facility):

```csharp
            double flatLabor = 0;
            var embodiment = DominantEmbodiment(state, port.Id, ref flatLabor);
            double labor = StaffingOps.WeightedWorkforce(state, f, port.Id);
```

(The embodiment stays the port's dominant one; only the labor *magnitude* becomes distance-weighted — the minimal change that makes distance "genuinely weight who works where" without reshaping the embodiment/affinity model.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~StaffingOpsTests|FullyQualifiedName~InteriorEconomyTests|FullyQualifiedName~ConservationTests|FullyQualifiedName~KnobRegistryTests"`
Expected: PASS — staffing green; conservation green (labor-share value still flows, only its magnitude/attribution shifts); knob registry green.

- [ ] **Step 5: Document + commit**

Add `Economy.StaffingDistanceFalloff` to `docs/TUNING.md` (consequence: higher = facilities off the population core are starved of labor).

```bash
git add src/Core/Epoch/StaffingOps.cs src/Core/Epoch/MarketEngine.cs src/Core/Epoch/EpochSimConfig.cs src/Core/Epoch/KnobRegistry.cs docs/TUNING.md tests/Core.Tests/Epoch/StaffingOpsTests.cs
git commit -m "feat(epoch): staff facilities by hex+local-hop distance-weighted labor"
```

---

### Task 4: Patrol coverage falls off with orbital distance

**Files:**
- Create: `src/Core/Epoch/PatrolCoverage.cs`
- Test: `tests/Core.Tests/Epoch/PatrolCoverageTests.cs`

**Interfaces:**
- Consumes: `OrbitGeometry.OrbitDistance`, `HexGrid.Distance`, `FleetRecord.Posture`, `FleetRecord.Hex`, `FleetRecord.Body`, `state.SettledSystems` (Plan 1); `FleetPosture.Patrol`.
- Produces:
  - `static double PatrolCoverage.At(SimState state, HexCoordinate hex, BodyRef body, int ownerActorId)` — the strongest Patrol coverage any fleet NOT owned by `ownerActorId` projects onto `(hex, body)`: for each Patrol fleet, coverage `= max(0, 1 − falloff × (hexHop + localHop))` from its dock (`fleet.Hex`, `fleet.Body`), taking the max across fleets. `0` where no patrol reaches. Deterministic (fleet-id order, max — order-insensitive). Pure. This is the §2 consequence that a body far from a patrol's dock, or in an under-covered hex, is mechanically a better place to run off-lane freight — no separate smuggling mechanic needed.
  - Reuses `Economy.StaffingDistanceFalloff`? No — add a dedicated `War.PatrolCoverageFalloff` (double, default `0.1`) so patrol reach tunes independently of staffing. Register it (name-sorted in `War.*`).
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (`WarKnobs.PatrolCoverageFalloff`), `src/Core/Epoch/KnobRegistry.cs`, `docs/TUNING.md`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/PatrolCoverageTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class PatrolCoverageTests
{
    [Fact]
    public void NoPatrol_IsZeroCoverage()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.Equal(0.0, PatrolCoverage.At(state, state.Actors[0].Seat,
            BodyRef.None, ownerActorId: 1), 9);
    }

    [Fact]
    public void CoverageFallsOffWithDistanceFromTheDock()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = new HexCoordinate(0, 0);
        var enemy = 2;
        var patrol = new FleetRecord(state.Fleets.Count, ownerActorId: enemy, hex)
        { Posture = FleetPosture.Patrol, Body = BodyRef.None };
        state.Fleets.Add(patrol);
        double atDock = PatrolCoverage.At(state, hex, BodyRef.None, ownerActorId: 1);
        double far = PatrolCoverage.At(state, new HexCoordinate(5, 0),
            BodyRef.None, ownerActorId: 1);
        Assert.True(atDock > far);
        Assert.Equal(0.0, PatrolCoverage.At(state, hex, BodyRef.None,
            ownerActorId: enemy), 9);      // own patrol never "covers" against self
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~PatrolCoverageTests`
Expected: FAIL — `PatrolCoverage` and the knob do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Core/Epoch/PatrolCoverage.cs`:

```csharp
using System;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Patrol enforcement coverage as a spatial field (locality slice
/// §2): a Patrol fleet's reach weakens with hex-hop + local-hop distance
/// from wherever it is docked, instead of a flat domain-wide multiplier.
/// The strongest hostile patrol coverage onto a point is what an off-lane
/// runner must evade (§5's detection roll reads this). Deterministic: max
/// across fleets is order-insensitive.</summary>
public static class PatrolCoverage
{
    public static double At(SimState state, HexCoordinate hex, BodyRef body,
                            int ownerActorId)
    {
        var war = state.Config.War;
        var eco = state.Config.Economy;
        double best = 0.0;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Patrol) continue;
            if (fleet.OwnerActorId == ownerActorId) continue;
            int hexHop = HexGrid.Distance(fleet.Hex, hex);
            int localHop = 0;
            if (hexHop == 0 && !fleet.Body.IsNone && !body.IsNone
                && state.SettledSystems.TryGetValue(hex, out var system)
                && system != null)
                localHop = OrbitGeometry.OrbitDistance(system, fleet.Body, body,
                    (int)eco.CrossStarHopOrbitSteps);
            double cover = 1.0 - war.PatrolCoverageFalloff * (hexHop + localHop);
            if (cover > best) best = cover;
        }
        return Math.Max(0.0, best);
    }
}
```

In `src/Core/Epoch/EpochSimConfig.cs` `WarKnobs`, add (near `InterdictionReachHexes`):

```csharp
    /// <summary>How steeply Patrol enforcement coverage falls with hex-hop +
    /// local-hop distance from the fleet's dock (locality slice §2):
    /// coverage = max(0, 1 − this × distance).</summary>
    public double PatrolCoverageFalloff { get; set; } = 0.1;
```

In `src/Core/Epoch/KnobRegistry.cs`, add name-sorted in the `War.*` block:

```csharp
        K("War.PatrolCoverageFalloff",
          "steepness of Patrol coverage falloff with hex+local-hop distance",
          c => c.War.PatrolCoverageFalloff,
          (c, v) => c.War.PatrolCoverageFalloff = v),
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~PatrolCoverageTests|FullyQualifiedName~KnobRegistryTests"`
Expected: PASS.

- [ ] **Step 5: Document + commit**

Add `War.PatrolCoverageFalloff` to `docs/TUNING.md` (consequence: higher = patrols only guard their immediate approaches, off-lane runs get safer farther out).

```bash
git add src/Core/Epoch/PatrolCoverage.cs src/Core/Epoch/EpochSimConfig.cs src/Core/Epoch/KnobRegistry.cs docs/TUNING.md tests/Core.Tests/Epoch/PatrolCoverageTests.cs
git commit -m "feat(epoch): Patrol coverage falls off with orbital distance from dock"
```

---

### Task 5: Off-lane alternative in `PlanRoute` + severed-lane election

**Files:**
- Modify: `src/Core/Epoch/ShipmentOps.cs` (`PlanRoute`; a new `OffLaneRoute` + elective `PlanBestRoute`)
- Test: `tests/Core.Tests/Epoch/OffLaneRouteTests.cs`

**Interfaces:**
- Consumes: `LaneNetwork.ShortestPath`, `HexGrid.Distance`, `FleetOps.SeveredLaneIds`, `LaneMath.IsLive`, `state.Lanes`, `state.Ports`, `Economy.OffLaneFreightHexesPerYear` (existing).
- Produces:
  - `static (IReadOnlyList<int> LaneIds, IReadOnlyList<double> LegYears) ShipmentOps.OffLaneRoute(SimState state, int fromPortId, int toPortId)` — the single off-lane crawl leg (`empty laneIds`, `[hexes / OffLaneFreightHexesPerYear]`), computed for ANY pair (the existing fallback formula, promoted to a first-class alternative).
  - `static (IReadOnlyList<int> LaneIds, IReadOnlyList<double> LegYears) ShipmentOps.PlanBestRoute(SimState state, int fromPortId, int toPortId, ISet<int> severed)` — computes the lane path AND the off-lane alternative; returns the off-lane route when the lane path is absent OR when its first leg is currently severed/quarantined/dead (a blockaded lane becomes a real elected second option), else the lane path. `PlanRoute` stays as-is for callers that only want the lane path; `Dispatch`/`DispatchVia` continue to use `PlanRoute` for un-blockaded routing — only the elective path introduced here consults `PlanBestRoute`. The exact election weighting (urgency/value/risk tolerance) is deferred; this is the minimal severed-lane election.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/OffLaneRouteTests.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class OffLaneRouteTests
{
    private static SimState TwoLinkedPorts(out int a, out int b)
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new StarGen.Core.Model.HexCoordinate(a0.Seat.Q + 6, a0.Seat.R),
            tier: 2, foundedYear: 0);
        state.Ports.Add(pa); state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        a = 0; b = 1;
        return state;
    }

    [Fact]
    public void OffLaneRoute_IsASingleCrawlLeg()
    {
        var state = TwoLinkedPorts(out int a, out int b);
        var (laneIds, legYears) = ShipmentOps.OffLaneRoute(state, a, b);
        Assert.Empty(laneIds);
        Assert.Single(legYears);
        Assert.True(legYears[0] > 0);
    }

    [Fact]
    public void PlanBestRoute_TakesTheLane_WhenOpen()
    {
        var state = TwoLinkedPorts(out int a, out int b);
        var (laneIds, _) = ShipmentOps.PlanBestRoute(state, a, b,
            new HashSet<int>());
        Assert.NotEmpty(laneIds);        // the live lane
    }

    [Fact]
    public void PlanBestRoute_GoesOffLane_WhenTheLaneIsSevered()
    {
        var state = TwoLinkedPorts(out int a, out int b);
        var severed = new HashSet<int> { 0 };   // sever the only lane
        var (laneIds, legYears) = ShipmentOps.PlanBestRoute(state, a, b, severed);
        Assert.Empty(laneIds);           // elected the off-lane alternative
        Assert.Single(legYears);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~OffLaneRouteTests`
Expected: FAIL — `OffLaneRoute` / `PlanBestRoute` do not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/ShipmentOps.cs`, add after `PlanRoute` (~line 113):

```csharp
    /// <summary>The off-lane crawl alternative for any port pair (locality
    /// slice §5): a single leg at OffLaneFreightHexesPerYear, empty lane
    /// list. The existing PlanRoute fallback, promoted to a first-class
    /// option computed ALONGSIDE the lane path — supply that leaves the
    /// highways pays for it in years, but a blockade no longer stalls it.</summary>
    public static (IReadOnlyList<int> LaneIds, IReadOnlyList<double> LegYears)
        OffLaneRoute(SimState state, int fromPortId, int toPortId)
    {
        double hexes = HexGrid.Distance(state.Ports[fromPortId].Hex,
                                        state.Ports[toPortId].Hex);
        return (new int[0], new[]
            { hexes / Math.Max(1e-9,
                state.Config.Economy.OffLaneFreightHexesPerYear) });
    }

    /// <summary>Elective routing (locality slice §5): the lane path when it
    /// exists and its first leg is open; the off-lane alternative when no
    /// lane path exists OR the lane path's first leg is currently severed,
    /// quarantined, or dead — a blockaded lane becomes a real second option,
    /// not a stall. The precise value/urgency-weighted election is deferred
    /// (design boundary); this is the minimal severed-lane election.</summary>
    public static (IReadOnlyList<int> LaneIds, IReadOnlyList<double> LegYears)
        PlanBestRoute(SimState state, int fromPortId, int toPortId,
                      ISet<int> severed)
    {
        var (laneIds, legYears) = PlanRoute(state, fromPortId, toPortId);
        if (laneIds.Count == 0)
            return (laneIds, legYears);              // already off-lane
        var first = state.Lanes[laneIds[0]];
        bool closed = severed.Contains(first.Id)
            || first.QuarantinedUntil > state.WorldYear
            || !LaneMath.IsLive(state, first);
        return closed ? OffLaneRoute(state, fromPortId, toPortId)
                      : (laneIds, legYears);
    }
```

(`ISet<int>` needs `using System.Collections.Generic;` — already imported in `ShipmentOps.cs`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~OffLaneRouteTests|FullyQualifiedName~LaneNetworkTests|FullyQualifiedName~StockpileTests"`
Expected: PASS — off-lane routing green; existing routing/requisition suites still green (`PlanRoute` unchanged for their callers).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/ShipmentOps.cs tests/Core.Tests/Epoch/OffLaneRouteTests.cs
git commit -m "feat(epoch): first-class off-lane route alternative + severed-lane election"
```

---

### Task 6: Detection roll on off-lane legs (conserved seizure)

**Files:**
- Modify: `src/Core/Rng/RollChannel.cs` (`ShipmentDetection = 77`)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (`WarKnobs.OffLaneDetectionPerCoveredYear`)
- Modify: `src/Core/Epoch/KnobRegistry.cs`, `docs/TUNING.md`
- Modify: `src/Core/Epoch/ShipmentOps.cs` (`Sail`: roll detection on off-lane legs)
- Test: `tests/Core.Tests/Epoch/OffLaneDetectionTests.cs`

**Interfaces:**
- Consumes: `PatrolCoverage.At` (Task 4); `FleetOps.NearestOwnedPortId`, `BookOps.PostSupply` (existing conserved-seizure primitives — the exact pattern the interdiction path in `Sail` uses); `EpochRolls.NextDouble`, `Rng.RollChannel`; `Shipment.RouteLaneIds` (empty = off-lane).
- Produces:
  - `RollChannel.ShipmentDetection = 77`.
  - `WarKnobs.OffLaneDetectionPerCoveredYear` (double, default `0.2`).
  - In `Sail`, when the shipment is on an off-lane leg (`RouteLaneIds.Count == 0`), accumulate the strongest hostile Patrol coverage along the crawl (sampled at the destination hex/body — the drop point the runner must reach) × years sailed; roll detection once on channel 77 with probability `1 − (1 − OffLaneDetectionPerCoveredYear × coverage)^years`; on a hit, the cargo is SEIZED — posted (conserved, P4) to the detecting patrol owner's nearest owned port, exactly like the interdiction seizure. A patrol owner with no port lands nothing (no seizure). Returns `Lost` on seizure, mirroring the piracy/interdiction outcomes.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/OffLaneDetectionTests.cs` (modeled on `FrontSupplyTests.ContestedLeg_SeizesCargo_ToTheInterdictorsPort` — the same conserved-seizure shape):

```csharp
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class OffLaneDetectionTests
{
    [Fact]
    public void DetectionChannel_Exists()
    {
        Assert.Equal(77UL, (ulong)StarGen.Core.Rng.RollChannel.ShipmentDetection);
    }

    [Fact]
    public void CoveredOffLaneRun_SeizesCargo_ToThePatrolPort_Conserved()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Config.War.OffLaneDetectionPerCoveredYear = 1.0;  // certain
        state.Config.War.PatrolCoverageFalloff = 0.0;           // full cover
        var owner = state.Actors[0];
        var rival = state.Actors[1];
        // owner's two ports, NOT lane-linked → PlanRoute yields an off-lane leg
        var home = new Port(state.Ports.Count, owner.Id,
            new HexCoordinate(0, 0), tier: 2, foundedYear: 0);
        state.Ports.Add(home);
        state.Markets.Add(new Market(home.Id, state.Config.Economy));
        var dest = new Port(state.Ports.Count, owner.Id,
            new HexCoordinate(6, 0), tier: 2, foundedYear: 0);
        state.Ports.Add(dest);
        state.Markets.Add(new Market(dest.Id, state.Config.Economy));
        // rival Patrol docked at the destination, with a rival port to land a prize
        var prize = new Port(state.Ports.Count, rival.Id,
            new HexCoordinate(6, 1), tier: 2, foundedYear: 0);
        state.Ports.Add(prize);
        state.Markets.Add(new Market(prize.Id, state.Config.Economy));
        state.Fleets.Add(new FleetRecord(state.Fleets.Count, rival.Id, dest.Hex)
        { Posture = FleetPosture.Patrol });

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, owner.Id,
            ShipmentChannel.Freight, home.Id, dest.Id, basket);

        Assert.Null(s);                        // detected + seized inside the sail
        Assert.Equal(100.0, BookOps.AskQty(state, prize.Id,
            (int)GoodId.Provisions), 6);       // conserved to the patrol's port
        Assert.Equal(0.0, BookOps.AskQty(state, dest.Id,
            (int)GoodId.Provisions), 6);       // did NOT reach the destination
    }
}
```

(This mirrors the interdiction seizure test exactly — a certain-detection off-lane run posts its cargo to the patrolling owner's nearest port, conserved, and the shipment resolves out of the registry. If `Dispatch`'s off-lane leg happens to span more than one step at these distances, drive it with `ShipmentOps.Advance(state, new MarketStepScratch(state))` in a loop until the shipment registry empties, then assert the same conserved landing — the seizure is the same either way.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~OffLaneDetectionTests`
Expected: FAIL — `RollChannel.ShipmentDetection` does not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Rng/RollChannel.cs`, append after `ShipmentInterdiction = 76` (append-only):

```csharp

    // --- Locality / off-lane smuggling (locality slice §5). ---
    ShipmentDetection = 77,    // off-lane seizure by a covering patrol: step = epoch, actor = shipment owner, subIndex = shipment id
```

In `src/Core/Epoch/EpochSimConfig.cs` `WarKnobs`, add near `InterdictionLossPerContestedYear`:

```csharp
    /// <summary>Off-lane seizure probability per world-year sailed under full
    /// hostile Patrol coverage (locality slice §5) — the smuggling detection
    /// roll, channel 77, damped by distance from patrol docks (PatrolCoverage).</summary>
    public double OffLaneDetectionPerCoveredYear { get; set; } = 0.2;
```

Register it name-sorted in `KnobRegistry.cs` (`War.*` block):

```csharp
        K("War.OffLaneDetectionPerCoveredYear",
          "off-lane seizure probability per world-year at full patrol coverage",
          c => c.War.OffLaneDetectionPerCoveredYear,
          (c, v) => c.War.OffLaneDetectionPerCoveredYear = v),
```

In `src/Core/Epoch/ShipmentOps.cs` `Sail`, add detection accounting for off-lane legs. Inside the `while` leg loop, after the existing hunted/contested accumulation and where `leg >= s.RouteLaneIds.Count` (an off-lane leg has an empty `RouteLaneIds`), accumulate covered years. Declare accumulators near the top of `Sail` beside `huntedYears`:

```csharp
        double coveredYears = 0;
        double maxCoverage = 0;
```

Inside the loop, after `s.YearsInTransit += sail;` (before `budget -= sail;`), for an off-lane shipment sample the destination's coverage against the owner:

```csharp
            if (s.RouteLaneIds.Count == 0)
            {
                double cover = PatrolCoverage.At(state,
                    state.Ports[s.DestPortId].Hex,
                    BodyRef.None, s.OwnerActorId);
                if (cover > 0)
                {
                    coveredYears += sail;
                    if (cover > maxCoverage) maxCoverage = cover;
                }
            }
```

After the piracy and interdiction blocks, before the final delivery check (`if (s.YearsInTransit >= s.TotalYears - 1e-9)`), add the detection seizure:

```csharp
        if (coveredYears > 0 && maxCoverage > 0)
        {
            // find the covering patrol owner with a port to land the prize:
            // the strongest hostile Patrol onto the drop point (fleet-id
            // order for a deterministic tiebreak).
            int patrolOwner = -1;
            double bestCover = 0;
            foreach (var fleet in state.Fleets)           // id order (P6)
            {
                if (fleet.Posture != FleetPosture.Patrol
                    || fleet.OwnerActorId == s.OwnerActorId) continue;
                double c = 1.0 - state.Config.War.PatrolCoverageFalloff
                    * HexGrid.Distance(fleet.Hex, state.Ports[s.DestPortId].Hex);
                if (c > bestCover) { bestCover = c; patrolOwner = fleet.OwnerActorId; }
            }
            int prizePort = patrolOwner >= 0
                ? FleetOps.NearestOwnedPortId(state, patrolOwner,
                    state.Ports[s.DestPortId].Hex)
                : -1;
            double p = 1.0 - Math.Pow(
                1.0 - state.Config.War.OffLaneDetectionPerCoveredYear * maxCoverage,
                coveredYears);
            if (prizePort >= 0
                && EpochRolls.NextDouble(state.Config.MasterSeed,
                    Rng.RollChannel.ShipmentDetection, state.EpochIndex,
                    s.OwnerActorId, s.Id) < p)
            {
                for (int g = 0; g < s.Qty.Length; g++)
                    if (s.Qty[g] > 0)
                        BookOps.PostSupply(state, prizePort, patrolOwner,
                            g, s.Qty[g], s.Grade[g]);
                return SailOutcome.Lost;
            }
        }
```

(This reuses the exact conserved-seizure primitives the interdiction block already uses — `FleetOps.NearestOwnedPortId` + `BookOps.PostSupply` — so the cargo is redistributed, never minted or sunk. `PatrolCoverage`, `BodyRef`, `HexGrid` are all already reachable from `ShipmentOps`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~OffLaneDetectionTests|FullyQualifiedName~ConservationTests|FullyQualifiedName~FixWaveTests|FullyQualifiedName~KnobRegistryTests"`
Expected: PASS — detection green; **`ConservationTests` green** (seizure is a conserved transfer); existing shipment suites green.

- [ ] **Step 5: Document + commit**

Add `War.OffLaneDetectionPerCoveredYear` to `docs/TUNING.md`. Update `docs/design/economy/markets.md` (black book / smuggling-leakage sections) to note the off-lane detection roll is now the real supply path behind smuggler-supplied prohibited goods, and `docs/SIMHEALTH.md`'s conservation section to record the off-lane seizure as another conserved redistribution (like piracy/interdiction), not a sink.

```bash
git add src/Core/Rng/RollChannel.cs src/Core/Epoch/EpochSimConfig.cs src/Core/Epoch/KnobRegistry.cs src/Core/Epoch/ShipmentOps.cs docs/TUNING.md docs/design/economy/markets.md docs/SIMHEALTH.md tests/Core.Tests/Epoch/OffLaneDetectionTests.cs
git commit -m "feat(epoch): off-lane detection roll — conserved smuggling seizure by patrol coverage"
```

---

### Task 7: Courier job board routes off-lane when severed

**Files:**
- Modify: `src/Core/Epoch/CourierOps.cs` (`AcceptOpen` ~line 66: consult `PlanBestRoute`)
- Test: `tests/Core.Tests/Epoch/OffLaneRouteTests.cs` (extend) or `tests/Core.Tests/Epoch/CourierOffLaneTests.cs`

**Interfaces:**
- Consumes: `ShipmentOps.PlanBestRoute` (Task 5); `FleetOps.SeveredLaneIds`; `CourierContract`.
- Produces: `AcceptOpen`'s routing consults `PlanBestRoute(state, origin, dest, severed)` so a courier whose lane is blockaded is fulfilled off-lane (self-hauled by the poster, as the off-lane branch already handles) instead of stalling — the design's "War-priority couriers reach for it when a front's lane is fully severed."

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/CourierOffLaneTests.cs`. The failing case: a lane exists but is severed and carries NO posted freight fleet, so the lane-fulfiller branch finds no carrier and (today) the courier stays `Open`; after the fix, `PlanBestRoute` returns the off-lane route and the poster self-fulfills.

```csharp
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class CourierOffLaneTests
{
    [Fact]
    public void SeveredCourier_IsAccepted_OffLane_NotStalled()
    {
        var (_, state) = EpochTestKit.Seeded();
        var owner = state.Actors[0];
        var a = new Port(state.Ports.Count, owner.Id,
            new StarGen.Core.Model.HexCoordinate(0, 0), tier: 2, foundedYear: 0);
        state.Ports.Add(a);
        state.Markets.Add(new Market(a.Id, state.Config.Economy));
        var b = new Port(state.Ports.Count, owner.Id,
            new StarGen.Core.Model.HexCoordinate(6, 0), tier: 2, foundedYear: 0);
        state.Ports.Add(b);
        state.Markets.Add(new Market(b.Id, state.Config.Economy));
        EpochTestKit.AddLane(state, a.Id, b.Id);           // a lane, no posted fleet
        a.DepositStock((int)GoodId.Provisions, 50.0, 0.5); // the poster's larder

        var basket = new List<(int Good, double Qty)>
            { ((int)GoodId.Provisions, 20.0) };
        var c = CourierOps.Post(state, owner.Id, a.Id, b.Id, basket,
            fee: 1.0, CourierPriority.Normal);
        Assert.NotNull(c);

        // sever the only lane, then run the board
        var rival = state.Actors[1];
        EpochTestKit.BlockadePort(state, rival.Id, b.Id);
        int accepted = CourierOps.AcceptOpen(state);

        Assert.True(accepted >= 1,
            "a severed-lane courier must be accepted off-lane, not stalled Open");
        Assert.NotEqual(CourierStatus.Open, c!.Status);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~CourierOffLaneTests`
Expected: FAIL — `AcceptOpen` routes via `PlanRoute` (lane path only), so the severed lane still has a non-empty `laneIds`, no carrier is found on it, and the courier stays `Open` (`accepted == 0`).

Note: confirm `CourierContract.Status` and `CourierStatus.Open` are the real member names (they appear in `ShipmentOps.Inbound` and `CourierOps`); adjust the status assertion to the actual enum if it differs. If `AcceptOpen` retires/dispatches accepted couriers such that `c.Status` is `InTransit`/`Delivered`, the `NotEqual(Open)` assertion still holds.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/CourierOps.cs` `AcceptOpen`, compute the severed set once and route via `PlanBestRoute`. Before the `foreach (var c in open)` loop, add:

```csharp
        var severed = FleetOps.SeveredLaneIds(state);
```

Change the route computation inside the loop (~line 66) from:

```csharp
            var (laneIds, _) = ShipmentOps.PlanRoute(state, c.OriginPortId,
                                                     c.DestPortId);
```

to:

```csharp
            var (laneIds, _) = ShipmentOps.PlanBestRoute(state, c.OriginPortId,
                                                         c.DestPortId, severed);
```

The existing `laneIds.Count > 0` branch (lane fulfiller) and the off-lane self-fulfillment branch below it already handle both outcomes — a severed lane now yields `laneIds.Count == 0` and falls into the off-lane self-haul path.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~CourierOffLaneTests|FullyQualifiedName~StockpileTests|FullyQualifiedName~ConservationTests"`
Expected: PASS — courier off-lane routing green; requisition/courier suites and conservation green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/CourierOps.cs tests/Core.Tests/Epoch/CourierOffLaneTests.cs
git commit -m "feat(epoch): courier board routes off-lane when the lane is severed"
```

---

## Slice-end gates (run before handing off, not a task)

- `dotnet test StarSystemGeneration.sln` fully green — the hex-tier (Phase-1) suite never broke; `ConservationTests` and `DeterminismTests` green (the detection roll is the only new draw, keyed correctly on channel 77).
- Determinism byte-identity: two full runs at the same config produce byte-identical artifacts; save→load→save is byte-identical (segment/fleet body refs already round-trip from Plan 1).
- Golden re-freeze: staffing re-weighting (Task 3), off-lane election (Tasks 5-7), and detection seizures (Task 6) legitimately change sim output — re-freeze the goldens once, at slice end.
- REPL eyeball (the taste gate): confirm a blockaded lane now shows freight electing an off-lane crawl instead of stalling in place, and that population segments now carry a real body ref (inspect a segment's `Body` — the data exists below the port for the first time; *rendering* segments on the system stage is deferred with local-hop travel visualization, design boundary). Pipe via `printf 'cmd\n' | dotnet run --project src/Inspector`.
