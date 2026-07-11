# Time & Logistics — Stage 1: The Project Ledger — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every piece of in-flight work (facility construction, port raises, gate
pairs, hull batches, colony expeditions, war mobilization) becomes a world-time
`Project` record with a per-year consumption basket, fed by priority-ordered
draws each Allocation, planned by a fixed-horizon scheduler in Intent.

**Architecture:** New `SimState.Projects` collection + `ProjectOps` (spawn /
advance / complete), a perceived `CapabilityBrief` assembled in Perception, a
deterministic `Planner` emitting a `StandingPlan` policy, and an Allocation
phase rewritten to two mechanical passes (groundbreak due entries, advance
in-flight projects). Spec: `docs/superpowers/specs/2026-07-11-time-and-logistics-design.md`
(§§1–5, Stage 1 sequencing). Stage 2 (located stockpiles + shipments) gets its
own plan at this stage's close, informed by what lands here.

**Tech Stack:** C# netstandard2.1 (`src/Core`), xUnit (`tests/Core.Tests`),
REPL inspector (`src/Inspector`). Build/test: `dotnet test StarSystemGeneration.sln`.

## Global Constraints

- Branch: `slice-t1-project-ledger` from `main`. This plan document doubles as
  the slice task ledger — keep the checkboxes updated and committed.
- The hex-tier (Phase-1 generation) suite must NEVER break.
- Determinism: fixed iteration orders everywhere — projects by
  (Priority, PlanOrder, Id) within a funder, funders by entered actor-id order;
  no new hash rolls (if one becomes necessary, next free RollChannel is 75).
- **No field on a Project may mention epochs or ticks** — durations and rates
  are world-years only. Every per-step quantity scales by
  `state.Config.Sim.YearsPerEpoch`.
- Conservation invariant: `PerYearBasket × YearsRequired` equals the lump the
  old code consumed (`InfraDef.BuildCost`; ships: components+armaments per hull).
- Goldens WILL change (red window): golden-dependent tests are re-frozen ONCE
  at slice end (Task 14), never mid-slice. Do not "fix" golden diffs earlier.
- Every new `src/Core` file gets a two-line `.meta` file with a fresh guid
  (copy any sibling `.meta`, `uuidgen` for the guid).
- Every new calibration constant goes in `KnobRegistry` (name-sorted table)
  AND gets a row in `docs/TUNING.md`. KnobRegistryTests enforces order.
- No compatibility adapters in the serializer — bump layer versions, update
  Save AND Load, migrate tests (greenfield rule).
- `unity/ProjectSettings` churn stays uncommitted.
- REPL piping: use bash `printf 'cmd\n' | dotnet run --project src/Inspector`.
- `state.WorldYear` during a step is the span's START year; the step
  integrates `[WorldYear, WorldYear + YearsPerEpoch)`. (`EpochEngine.Step`
  increments after the phases.)

---

### Task 1: The Project record and SimState registry

**Files:**
- Create: `src/Core/Epoch/Project.cs` (+ `Project.cs.meta`)
- Modify: `src/Core/Epoch/SimState.cs` (add registry)
- Test: `tests/Core.Tests/Epoch/ProjectTests.cs`

**Interfaces:**
- Produces: `enum ProjectKind { FacilityConstruction=0, PortRaise=1, GatePair=2, HullBatch=3, ColonyExpedition=4, Mobilization=5 }`;
  `enum ProjectPriority { War=0, Core=1, Growth=2 }`;
  `class Project` (fields below); `SimState.Projects : List<Project>`.
- Consumed by every later task.

- [ ] **Step 1: Write the failing test**

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ProjectTests
{
    [Fact]
    public void Project_ProgressAndDone_TrackDeliveredYears()
    {
        var p = new Project(0, ProjectKind.FacilityConstruction,
            ownerActorId: 1, funderActorId: 1, portId: 0,
            new HexCoordinate(0, 0), yearsRequired: 4.0, startedYear: 100);
        Assert.Equal(0.0, p.Progress, 9);
        Assert.True(p.InFlight);
        p.YearsDelivered = 2.0;
        Assert.Equal(0.5, p.Progress, 9);
        p.YearsDelivered = 4.0;
        Assert.Equal(1.0, p.Progress, 9);
    }

    [Fact]
    public void Project_ZeroDuration_IsInstantlyComplete()
    {
        var p = new Project(0, ProjectKind.ColonyExpedition, 1, 1, 0,
            new HexCoordinate(3, -1), yearsRequired: 0.0, startedYear: 50);
        Assert.Equal(1.0, p.Progress, 9);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ProjectTests"`
Expected: FAIL (compile error: `Project` not defined).

- [ ] **Step 3: Write the implementation**

`src/Core/Epoch/Project.cs`:

```csharp
using System;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>What kind of work a project delivers when its years are done
/// (spec §1) — a closed, versioned vocabulary; append-only, never renumber.</summary>
public enum ProjectKind
{
    FacilityConstruction = 0,
    PortRaise = 1,
    GatePair = 2,
    HullBatch = 3,
    ColonyExpedition = 4,
    Mobilization = 5,
}

/// <summary>Draw order among one funder's projects: lower drinks first
/// (the priority-ordered starvation cascade, spec §4).</summary>
public enum ProjectPriority { War = 0, Core = 1, Growth = 2 }

/// <summary>One piece of in-flight work (spec §1): a rate contract in
/// world-years — per-year basket, wages stream, years required/delivered.
/// Registry in SimState.Projects, id = creation order (P6). The site
/// exists at groundbreaking; completion fires the kind's payload. NOTHING
/// here counts epochs or ticks — time is state (P7).</summary>
public sealed class Project
{
    public int Id { get; }
    public ProjectKind Kind { get; }
    /// <summary>Who gets the result. Settable: conquest transfers
    /// site-anchored work at current progress (spec §1).</summary>
    public int OwnerActorId { get; set; }
    /// <summary>Whose treasury and reserves feed it (differs from owner
    /// for corp-built gates on a host polity's port).</summary>
    public int FunderActorId { get; set; }
    /// <summary>Anchor port: the site market draws come from and the wage
    /// sink. Travel kinds anchor at the staging port. Settable: conquest.</summary>
    public int PortId { get; set; }
    /// <summary>Site or travel-target hex — the P1 residue address.</summary>
    public HexCoordinate Hex { get; }
    public ProjectPriority Priority { get; set; }
    /// <summary>Position in the standing plan — the tie-break inside a
    /// priority class. Mechanical spawns use 0.</summary>
    public int PlanOrder { get; set; }
    /// <summary>Per-good consumption per world-year, indexed by GoodId
    /// (Goods.All.Count wide). All-zero for travel kinds.</summary>
    public double[] PerYearBasket { get; } =
        new double[Substrate.Goods.All.Count];
    /// <summary>Credits streamed to the site's households per world-year,
    /// drawn from the funder's treasury (construction employment).</summary>
    public double WagesPerYear { get; set; }
    public double YearsRequired { get; }
    public double YearsDelivered { get; set; }
    /// <summary>Scheduled groundbreaking world-year — may sit mid-span of
    /// a coarse step; Advance only credits years after it.</summary>
    public int StartedYear { get; }
    public bool Completed { get; set; }
    public bool Cancelled { get; set; }
    /// <summary>Fraction of the year-scaled basket met last Advance —
    /// the REPL's starvation readout.</summary>
    public double LastFedFraction { get; set; } = 1.0;

    // Completion payload, sparse by kind:
    /// <summary>FacilityConstruction/GatePair: InfraTypeId. HullBatch:
    /// ship design id. Others −1.</summary>
    public int TypeId { get; set; } = -1;
    /// <summary>FacilityConstruction: facility id under construction.
    /// PortRaise: port id. GatePair: lane id. ColonyExpedition: convoy
    /// fleet id. Mobilization: war id. Others −1.</summary>
    public int TargetId { get; set; } = -1;
    /// <summary>HullBatch: hulls in the batch. Others 0.</summary>
    public int Count { get; set; }
    /// <summary>Quantity-weighted mean grade of Ship Components drawn so
    /// far (HullBatch — the hull's grade at commissioning).</summary>
    public double AccumGrade { get; set; }
    public double AccumGradeWeight { get; set; }

    public bool InFlight => !Completed && !Cancelled;
    public double Progress => YearsRequired <= 0 ? 1.0
        : Math.Min(1.0, YearsDelivered / YearsRequired);

    public Project(int id, ProjectKind kind, int ownerActorId,
                   int funderActorId, int portId, HexCoordinate hex,
                   double yearsRequired, int startedYear)
    {
        Id = id;
        Kind = kind;
        OwnerActorId = ownerActorId;
        FunderActorId = funderActorId;
        PortId = portId;
        Hex = hex;
        YearsRequired = yearsRequired;
        StartedYear = startedYear;
    }
}
```

Create `src/Core/Epoch/Project.cs.meta` (two lines, fresh guid, same shape as
`src/Core/Epoch/Facility.cs.meta`).

In `src/Core/Epoch/SimState.cs`, after the `Plagues` list (line ~83), add:

```csharp
    /// <summary>In-flight work: every duration in the world is a project
    /// here (spec 2026-07-11 time-and-logistics §1) — id order (P6);
    /// completed and cancelled projects stay as history.</summary>
    public List<Project> Projects { get; } = new List<Project>();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ProjectTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/Project.cs src/Core/Epoch/Project.cs.meta src/Core/Epoch/SimState.cs tests/Core.Tests/Epoch/ProjectTests.cs
git commit -m "feat(time): Project record + SimState.Projects registry"
```

---

### Task 2: Facility commissioning replaces the IsActive date-check

**Files:**
- Modify: `src/Core/Epoch/Facility.cs`
- Modify: `src/Core/Epoch/MarketEngine.cs:109-113` (`IsActive`)
- Modify: `src/Core/Epoch/Lane.cs` (`LaneMath.IsLive`)
- Test: `tests/Core.Tests/Epoch/ProjectTests.cs` (extend)

**Interfaces:**
- Produces: `Facility.CommissionedYear : long` (−1 = under construction;
  otherwise the world-year output began). Constructor sets it to `builtYear`
  so every existing call site (genesis starters, colony founding, test kit)
  stays commissioned-at-birth; ONLY project groundbreaking sets −1.
- `MarketEngine.IsActive(state, f)` becomes `f.CommissionedYear >= 0`.
- `LaneMath.IsLive` additionally requires both gates commissioned.

- [ ] **Step 1: Write the failing test** (append to `ProjectTests.cs`)

```csharp
    [Fact]
    public void Facility_Uncommissioned_IsInactive_AndLaneIsDead()
    {
        var (_, state) = EpochTestKit.Seeded();
        var lane = EpochTestKit.AddLane(state, 0, 1);
        var gateA = state.Facilities[lane.GateAId];
        Assert.True(MarketEngine.IsActive(state, gateA));
        Assert.True(LaneMath.IsLive(state, lane));
        gateA.CommissionedYear = -1;               // under construction
        Assert.False(MarketEngine.IsActive(state, gateA));
        Assert.False(LaneMath.IsLive(state, lane));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~Facility_Uncommissioned"`
Expected: FAIL (no `CommissionedYear` member).

- [ ] **Step 3: Implement**

`Facility.cs` — add after `BuiltYear`:

```csharp
    /// <summary>World-year output began; −1 while under construction (the
    /// site exists before the facility does, P1 — spec §1 replaced the
    /// BuiltYear+ConstructionYears date-check with delivered work).</summary>
    public long CommissionedYear { get; set; }
```

and in the constructor body add `CommissionedYear = builtYear;`.

`MarketEngine.cs` — replace the `IsActive` body (lines 109–113):

```csharp
    /// <summary>True once the construction project delivered its years —
    /// commissioning is a project completion, never date arithmetic
    /// (spec §1).</summary>
    public static bool IsActive(SimState state, Facility f) =>
        f.CommissionedYear >= 0;
```

`Lane.cs` — in `LaneMath.IsLive`, extend the condition:

```csharp
    public static bool IsLive(SimState state, Lane lane) =>
        lane.GateAId >= 0 && lane.GateBId >= 0
        && state.Facilities[lane.GateAId].CommissionedYear >= 0
        && state.Facilities[lane.GateBId].CommissionedYear >= 0
        && state.Facilities[lane.GateAId].Condition
           >= state.Config.Infrastructure.GateFunctionalCondition
        && state.Facilities[lane.GateBId].Condition
           >= state.Config.Infrastructure.GateFunctionalCondition;
```

- [ ] **Step 4: Run the full suite** (this touches live behavior)

Run: `dotnet test StarSystemGeneration.sln`
Expected: PASS except possibly tests that relied on `IsActive`'s old delay
(a facility built this step used to be inactive until
`BuiltYear + ConstructionYears`; now non-project facilities are active at
birth). If a test asserted that delay, it is asserting the hand-wave this
slice removes: rewrite it to build via a project once Task 5 lands — mark it
`[Fact(Skip = "t1: converts to project in Task 5")]` for now and note it in
this file's checklist. Golden byte-diffs: leave for Task 14.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): Facility.CommissionedYear; IsActive/IsLive read commissioning"
```

---

### Task 3: Projects serialize — artifact layer + plan/polity fields

**Files:**
- Modify: `src/Core/Epoch/ArtifactSerializer.cs`
- Modify: `src/Core/Epoch/PolityRecord.cs` (add `LastIncomePerYear`, `Mobilization`)
- Modify: `src/Core/Epoch/Interior/Corporation.cs` (add `LastIncomePerYear`)
- Test: `tests/Core.Tests/Epoch/ProjectTests.cs` (extend)

**Interfaces:**
- Produces: artifact layers — `("facilities", 2)` (FACILITY line +
  CommissionedYear), `("actors", 6)` (POLITY line + LastIncomePerYear +
  Mobilization; new PLANE lines per plan entry — written in Task 7 when the
  plan exists; the version bump happens NOW so it bumps once),
  `("corporations", 3)` (CORP line + LastIncomePerYear), new trailing layer
  `("projects", 1)` with PROJECT lines.
- `PolityRecord.LastIncomePerYear : double` — last step's receipts ÷ span
  years, written by MarketsPhase (Task 4). `PolityRecord.Mobilization : double`
  (0..1). `Corporation.LastIncomePerYear : double`.
- `DeltaSerializer` is layer-generic — no changes needed.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Projects_RoundTrip_ByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr0 = state.Polities[0];
        var p = new Project(0, ProjectKind.PortRaise, pr0.ActorId,
            pr0.ActorId, 0, state.Ports[0].Hex, yearsRequired: 5.0,
            startedYear: 25)
        { TargetId = 0, Priority = ProjectPriority.Core, PlanOrder = 3,
          WagesPerYear = 8.0, YearsDelivered = 1.25, LastFedFraction = 0.6 };
        p.PerYearBasket[(int)StarGen.Core.Substrate.GoodId.Alloys] = 2.0;
        state.Projects.Add(p);
        pr0.LastIncomePerYear = 12.5;
        pr0.Mobilization = 0.4;
        string text = ArtifactSerializer.ToText(state);
        using var reader = new System.IO.StringReader(text);
        var loaded = ArtifactSerializer.Load(reader);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
        Assert.Single(loaded.Projects);
        Assert.Equal(5.0, loaded.Projects[0].YearsRequired, 9);
        Assert.Equal(0.4, loaded.PolityOf(1).Mobilization, 9);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~Projects_RoundTrip"`
Expected: FAIL (loaded artifact has no projects; POLITY line has no new fields).

- [ ] **Step 3: Implement**

Add the record fields:

```csharp
// PolityRecord.cs, after EntryGradeBonus:
    /// <summary>Last step's realized receipts per world-year — the trailing
    /// income rate the capability brief plans against (spec §2, P3:
    /// deliberately hindsight, never clairvoyance).</summary>
    public double LastIncomePerYear { get; set; }
    /// <summary>War-economy readiness 0..1: raised by fed Mobilization
    /// projects, decays at peace (spec §5).</summary>
    public double Mobilization { get; set; }

// Corporation.cs, beside its Credits property:
    /// <summary>Last step's receipts per world-year (spec §2).</summary>
    public double LastIncomePerYear { get; set; }
```

`ArtifactSerializer.cs`:

1. In the `Layers` table: `("actors", 5)` → `("actors", 6)`,
   `("facilities", 1)` → `("facilities", 2)`, `("corporations", 2)` →
   `("corporations", 3)`, and append `("projects", 1)` at the END of the table.
2. POLITY write line — append two fields:

```csharp
        foreach (var p in state.Polities)
            w.WriteLine(Join("POLITY", p.ActorId.ToString(Inv),
                p.SpeciesId.ToString(Inv), R(p.Credits),
                R(p.ExpansionPoints), R(p.DevelopmentPoints),
                R(p.EntryGradeBonus),
                // actors v6 (slice t1): trailing income rate + mobilization
                R(p.LastIncomePerYear), R(p.Mobilization)));
```

3. FACILITY write line — append `f.CommissionedYear.ToString(Inv)` as the
   last field (facilities v2).
4. CORP write line — append `R(c.LastIncomePerYear)` as the last field
   (corporations v3; find the CORP writer in the corporations layer and
   mirror the read side).
5. After the `Layer(w, "plagues")` block at the end of `Save`, add:

```csharp
        Layer(w, "projects");
        foreach (var p in state.Projects)
        {
            var basket = new List<string>();
            for (int g = 0; g < p.PerYearBasket.Length; g++)
                if (p.PerYearBasket[g] != 0)
                    basket.Add(g.ToString(Inv) + ":" + R(p.PerYearBasket[g]));
            w.WriteLine(Join("PROJECT", p.Id.ToString(Inv),
                ((int)p.Kind).ToString(Inv), p.OwnerActorId.ToString(Inv),
                p.FunderActorId.ToString(Inv), p.PortId.ToString(Inv),
                p.Hex.Q.ToString(Inv), p.Hex.R.ToString(Inv),
                ((int)p.Priority).ToString(Inv), p.PlanOrder.ToString(Inv),
                R(p.WagesPerYear), R(p.YearsRequired), R(p.YearsDelivered),
                p.StartedYear.ToString(Inv), B(p.Completed), B(p.Cancelled),
                R(p.LastFedFraction), p.TypeId.ToString(Inv),
                p.TargetId.ToString(Inv), p.Count.ToString(Inv),
                R(p.AccumGrade), R(p.AccumGradeWeight),
                string.Join(";", basket)));
        }
```

6. Load side: mirror exactly — extend the POLITY, FACILITY, and CORP parsers
   with the appended fields (same positions), and add a `"PROJECT"` case in
   the line dispatch that reconstructs the record (constructor args from
   fields 1–7 and 12–13, then the settable properties; parse the
   `good:qty;good:qty` basket tail with `double.Parse(..., Inv)`). Follow the
   existing parse style in `Load` — invariant culture, no TryParse fallbacks
   (a malformed artifact should throw).

- [ ] **Step 4: Run the serializer-touching suites**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~Serializ|FullyQualifiedName~Delta|FullyQualifiedName~Project"`
Expected: PASS. (DeltaTests pass untouched — deltas are layer-generic.)

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): projects artifact layer; actors v6, facilities v2, corporations v3"
```

---

### Task 4: ProjectOps — spawn, feed, advance, complete

**Files:**
- Create: `src/Core/Epoch/ProjectOps.cs` (+ `.meta`)
- Modify: `src/Core/Epoch/Phases.cs` (MarketsPhase: write `LastIncomePerYear`)
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs`

**Interfaces:**
- Produces (all `public static` on `ProjectOps` unless noted):
  - `Project Spawn(SimState state, ProjectKind kind, int ownerActorId, int funderActorId, int portId, HexCoordinate hex, double yearsRequired, ProjectPriority priority, int planOrder)` — appends to `state.Projects` with `StartedYear = state.WorldYear`, returns it. Overload `SpawnAt(..., int startedYear)` for scheduled starts.
  - `int AdvanceAll(SimState state)` — pass 1 (spec §4): for every entered
    funder in actor-id order, its in-flight projects in (Priority, PlanOrder,
    Id) order, feed and advance by this span's overlap; complete the done
    ones; returns completions.
  - `void Complete(SimState state, Project p)` — dispatch by kind (cases fill
    in across Tasks 5–11; this task implements FacilityConstruction, PortRaise,
    GatePair and leaves the others as recognized kinds completing to events only).
  - `void Cancel(SimState state, Project p)` — marks cancelled, stages an
    abandoned-works event.
- Consumes: `Market.Draw`, `MarketEngine.PayWages(state, portId, credits)`,
  `PolityRecord.ReserveQty/ReserveGrade` (Stage-1 interim reserve fallback),
  `Substrate.Goods.All`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ProjectOpsTests
{
    /// <summary>A project fed 100% delivers the span's years; fed ~60%
    /// delivers ~60% of them (starvation semantics, spec §1).</summary>
    [Fact]
    public void Advance_StarvedProject_DeliversTheFedFraction()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var market = state.Markets[port];
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, yearsRequired: 50.0,
            ProjectPriority.Core, planOrder: 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.WagesPerYear = 0.0;
        int years = state.Config.Sim.YearsPerEpoch;              // 25
        // stock exactly 60% of the span's need, wipe reserves
        market.Inventory[(int)GoodId.Alloys] = 0.6 * years;
        pr.ReserveQty[(int)GoodId.Alloys] = 0;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(0.6 * years, p.YearsDelivered, 6);
        Assert.Equal(0.6, p.LastFedFraction, 6);
        Assert.Equal(0.0, market.Inventory[(int)GoodId.Alloys], 6);
    }

    /// <summary>Priority order: the War-class project drinks the shared
    /// market dry before the Growth-class one sees a unit (spec §4).</summary>
    [Fact]
    public void Advance_PriorityCascade_WarDrinksFirst()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        int years = state.Config.Sim.YearsPerEpoch;
        var market = state.Markets[port];
        market.Inventory[(int)GoodId.Fuel] = 1.0 * years;   // one project's worth
        pr.ReserveQty[(int)GoodId.Fuel] = 0;
        var growth = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, 50.0,
            ProjectPriority.Growth, 0);
        growth.TargetId = port;
        growth.PerYearBasket[(int)GoodId.Fuel] = 1.0;
        var war = ProjectOps.Spawn(state, ProjectKind.Mobilization, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, 50.0,
            ProjectPriority.War, 0);
        war.PerYearBasket[(int)GoodId.Fuel] = 1.0;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(years, war.YearsDelivered, 6);          // fully fed
        Assert.Equal(0.0, growth.YearsDelivered, 6);         // starved
    }

    /// <summary>Completion fires once: a PortRaise raises its port tier
    /// the step its years are delivered, never before.</summary>
    [Fact]
    public void Advance_PortRaise_CompletesAndRaisesTheTier()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        int tierBefore = state.Ports[portId].Tier;
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, portId, state.Ports[portId].Hex, yearsRequired: 5.0,
            ProjectPriority.Core, 0);
        p.TargetId = portId;                    // empty basket: time-only
        ProjectOps.AdvanceAll(state);           // 25y span covers 5y need
        Assert.True(p.Completed);
        Assert.Equal(tierBefore + 1, state.Ports[portId].Tier);
    }

    /// <summary>Mid-span scheduled starts only credit the overlap: a
    /// project scheduled 20 years into a 25-year span delivers 5.</summary>
    [Fact]
    public void Advance_MidSpanStart_CreditsOnlyTheOverlap()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        var p = ProjectOps.SpawnAt(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, portId, state.Ports[portId].Hex, yearsRequired: 50.0,
            ProjectPriority.Core, 0, startedYear: state.WorldYear + 20);
        p.TargetId = portId;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(5.0, p.YearsDelivered, 6);
    }

    internal static int FirstEnteredPolity(SimState state)
    {
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) return a.Id;
        throw new Xunit.Sdk.XunitException("no entered polity");
    }

    internal static int OwnPort(SimState state, int actorId)
    {
        foreach (var port in state.Ports)
            if (port.OwnerActorId == actorId) return port.Id;
        throw new Xunit.Sdk.XunitException("no port");
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ProjectOpsTests"`
Expected: FAIL (`ProjectOps` not defined).

- [ ] **Step 3: Implement** `src/Core/Epoch/ProjectOps.cs` (+ `.meta`):

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;        // HexGrid (completion event midpoints)
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The project lifecycle (spec §§1,4): spawn at groundbreaking,
/// feed and advance every Allocation in priority order, fire the completion
/// payload when the years are delivered. Pure deterministic math over
/// ordered state — no rolls.</summary>
public static class ProjectOps
{
    public static Project Spawn(SimState state, ProjectKind kind,
        int ownerActorId, int funderActorId, int portId, HexCoordinate hex,
        double yearsRequired, ProjectPriority priority, int planOrder) =>
        SpawnAt(state, kind, ownerActorId, funderActorId, portId, hex,
                yearsRequired, priority, planOrder, state.WorldYear);

    public static Project SpawnAt(SimState state, ProjectKind kind,
        int ownerActorId, int funderActorId, int portId, HexCoordinate hex,
        double yearsRequired, ProjectPriority priority, int planOrder,
        int startedYear)
    {
        var p = new Project(state.Projects.Count, kind, ownerActorId,
                            funderActorId, portId, hex, yearsRequired,
                            startedYear)
        { Priority = priority, PlanOrder = planOrder };
        state.Projects.Add(p);
        return p;
    }

    /// <summary>Pass 1 (spec §4): per funder in entered actor-id order,
    /// projects in (priority, plan order, id) order — the scarcest input
    /// paces each project; earlier draws starve later ones at a shared
    /// market. Returns completions this step.</summary>
    public static int AdvanceAll(SimState state)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        int spanEnd = state.WorldYear + years;
        int completions = 0;
        var mine = new List<Project>();
        foreach (var actor in state.Actors)                   // id order (P6)
        {
            if (!actor.Entered) continue;
            mine.Clear();
            foreach (var p in state.Projects)                 // id order (P6)
                if (p.InFlight && p.FunderActorId == actor.Id) mine.Add(p);
            if (mine.Count == 0) continue;
            mine.Sort((x, y) =>
            {
                int c = x.Priority.CompareTo(y.Priority);
                if (c != 0) return c;
                c = x.PlanOrder.CompareTo(y.PlanOrder);
                return c != 0 ? c : x.Id.CompareTo(y.Id);
            });
            foreach (var p in mine)
            {
                double span = Math.Min(years, spanEnd - p.StartedYear);
                if (span <= 0) continue;                      // not yet due
                double need = Math.Min(span,
                    p.YearsRequired - p.YearsDelivered);
                if (need > 0) Feed(state, p, need);
                if (p.YearsDelivered >= p.YearsRequired - 1e-9)
                {
                    Complete(state, p);
                    completions++;
                }
            }
        }
        return completions;
    }

    /// <summary>Draw needYears of the basket from the site market, then
    /// the funder's banked reserves (Stage-1 interim — Stage 2 locates
    /// them); the met fraction (min across goods AND the wage stream)
    /// scales progress, consumption, and wages alike: a starved project
    /// neither hoards goods nor pays idle crews.</summary>
    private static void Feed(SimState state, Project p, double needYears)
    {
        var market = state.Markets[p.PortId];
        var funderPolity = FunderPolity(state, p.FunderActorId);
        double fraction = 1.0;
        for (int g = 0; g < p.PerYearBasket.Length; g++)
        {
            double want = p.PerYearBasket[g] * needYears;
            if (want <= 0) continue;
            double have = market.Inventory[g]
                + (funderPolity != null ? funderPolity.ReserveQty[g] : 0.0);
            fraction = Math.Min(fraction, Math.Min(1.0, have / want));
        }
        double wages = p.WagesPerYear * needYears;
        if (wages > 0)
        {
            double treasury = TreasuryAvailable(state, p);
            fraction = Math.Min(fraction,
                Math.Min(1.0, treasury / wages));
        }
        if (fraction > 0)
            for (int g = 0; g < p.PerYearBasket.Length; g++)
            {
                double take = p.PerYearBasket[g] * needYears * fraction;
                if (take <= 0) continue;
                double grade = market.InventoryGrade[g];
                double drawn = market.Draw(g, take);
                market.LastCleared[g] += drawn;
                double shortfall = take - drawn;
                if (shortfall > 0 && funderPolity != null)
                {
                    double fromReserve = Math.Min(shortfall,
                        funderPolity.ReserveQty[g]);
                    grade = funderPolity.ReserveGrade[g];
                    funderPolity.ReserveQty[g] -= fromReserve;
                    if (funderPolity.ReserveQty[g] <= 0)
                        funderPolity.ReserveGrade[g] = 0;
                }
                if (g == (int)GoodId.ShipComponents && take > 0)
                {
                    p.AccumGrade = (p.AccumGrade * p.AccumGradeWeight
                        + grade * take) / (p.AccumGradeWeight + take);
                    p.AccumGradeWeight += take;
                }
            }
        if (wages > 0 && fraction > 0)
        {
            SpendTreasury(state, p, wages * fraction);
            MarketEngine.PayWages(state, p.PortId, wages * fraction);
        }
        p.YearsDelivered = Math.Min(p.YearsRequired,
            p.YearsDelivered + fraction * needYears);
        p.LastFedFraction = fraction;
        if (p.Kind == ProjectKind.Mobilization && FunderPolity(state,
                p.OwnerActorId) is PolityRecord mob)
            mob.Mobilization = Math.Max(mob.Mobilization, p.Progress);
    }

    /// <summary>The treasury a kind streams wages from: development for
    /// civil works, military for hulls and mobilization, corp credits for
    /// corporate funders. Expeditions charged at the act, no stream.</summary>
    private static double TreasuryAvailable(SimState state, Project p)
    {
        var corp = state.CorporationOf(p.FunderActorId);
        if (corp != null) return Math.Max(0, corp.Credits);
        var pr = state.PolityOf(p.FunderActorId);
        return p.Kind switch
        {
            ProjectKind.HullBatch or ProjectKind.Mobilization
                => Math.Max(0, pr.MilitaryPoints),
            ProjectKind.ColonyExpedition => double.MaxValue,
            _ => Math.Max(0, pr.DevelopmentPoints),
        };
    }

    private static void SpendTreasury(SimState state, Project p, double amount)
    {
        var corp = state.CorporationOf(p.FunderActorId);
        if (corp != null) { corp.Credits -= amount; return; }
        var pr = state.PolityOf(p.FunderActorId);
        switch (p.Kind)
        {
            case ProjectKind.HullBatch:
            case ProjectKind.Mobilization: pr.MilitaryPoints -= amount; break;
            case ProjectKind.ColonyExpedition: break;
            default: pr.DevelopmentPoints -= amount; break;
        }
    }

    private static PolityRecord? FunderPolity(SimState state, int actorId)
    {
        foreach (var pr in state.Polities)
            if (pr.ActorId == actorId) return pr;
        return null;
    }

    /// <summary>The completion payload (spec §1). Kind cases land across
    /// tasks 5–11; each stages its chronicle event.</summary>
    public static void Complete(SimState state, Project p)
    {
        p.Completed = true;
        switch (p.Kind)
        {
            case ProjectKind.FacilityConstruction:
            {
                var f = state.Facilities[p.TargetId];
                f.CommissionedYear = state.WorldYear;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.FacilityBuilt,
                    new[] { p.OwnerActorId }, f.Hex, Magnitude: f.Tier,
                    Valence: 1.0, EventVisibility.Regional,
                    new FacilityBuiltPayload(f.Id, f.TypeId, f.Tier)));
                break;
            }
            case ProjectKind.PortRaise:
            {
                var port = state.Ports[p.TargetId];
                port.Tier++;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.PortTierRaised,
                    new[] { p.OwnerActorId }, port.Hex,
                    Magnitude: port.Tier, Valence: 1.0,
                    EventVisibility.Regional,
                    new PortTierRaisedPayload(port.Id, port.Tier)));
                break;
            }
            case ProjectKind.GatePair:
            {
                var lane = state.Lanes[p.TargetId];
                state.Facilities[lane.GateAId].CommissionedYear = state.WorldYear;
                state.Facilities[lane.GateBId].CommissionedYear = state.WorldYear;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.LaneOpened,
                    new[] { p.OwnerActorId },
                    HexGrid.Round(
                        (state.Ports[lane.PortAId].Hex.Q
                         + state.Ports[lane.PortBId].Hex.Q) * 0.5,
                        (state.Ports[lane.PortAId].Hex.R
                         + state.Ports[lane.PortBId].Hex.R) * 0.5),
                    Magnitude: state.Facilities[lane.GateAId].Tier,
                    Valence: 1.0, EventVisibility.Regional,
                    new LaneOpenedPayload(lane.PortAId, lane.PortBId)));
                break;
            }
            case ProjectKind.HullBatch:      // Task 8
            case ProjectKind.ColonyExpedition: // Task 9
                break;
            case ProjectKind.Mobilization:
            {
                var pr = state.PolityOf(p.OwnerActorId);
                pr.Mobilization = 1.0;
                break;
            }
        }
    }

    /// <summary>A cancelled site is residue with a date and an owner of
    /// record (P1) — sunk goods stay sunk; the next replan simply stops
    /// feeding it.</summary>
    public static void Cancel(SimState state, Project p)
    {
        p.Cancelled = true;
    }
}
```

Note: `WorldEventType.FacilityBuilt/PortTierRaised/LaneOpened` and their
payloads already exist (`WorldEvent.cs`) — reuse, no new event types here.
`HexGrid.Round(double, double)` exists (used by `AllocationPhase.Midpoint`).

In `Phases.cs` `MarketsPhase.Run`, after `MarketEngine.DistributePools(...)`,
add the trailing-income write:

```csharp
        int spanYears = state.Config.Sim.YearsPerEpoch;
        foreach (var pr in state.Polities)
            pr.LastIncomePerYear = pr.Receipts / spanYears;
        foreach (var corp in state.Corporations)
            if (corp.Active) corp.LastIncomePerYear = corp.Receipts / spanYears;
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ProjectOpsTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): ProjectOps spawn/feed/advance/complete with priority starvation"
```

---

### Task 5: Facility construction becomes a project (Allocation loses its first greedy loop)

**Files:**
- Create: `src/Core/Epoch/CapabilityOps.cs` (+ `.meta`) — the candidate scan
  extracted from `AllocationPhase.BuildFacilities` (the brief itself lands
  Task 6; this task lands the scan the groundbreak needs)
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase`)
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (extend)

**Interfaces:**
- Produces:
  - `record ConstructionCandidate(int TypeId, HexCoordinate Hex, int PortId, double Score)` (in `CapabilityOps.cs`).
  - `CapabilityOps.ConstructionCandidatesFor(SimState state, int actorId) : List<ConstructionCandidate>` — per own under-capacity port, top 3
    candidates by (score desc, TypeId asc, hex spiral order), using the exact
    siting-score × price-signal × saturation math from `BuildFacilities`
    (moved, not rewritten), MINUS the old `CanAfford` stock check (rates
    replace lumps; the market-stock gate is obsolete — spec §2).
  - `ProjectOps.SpawnFacilityConstruction(SimState state, int ownerActorId, int funderActorId, ConstructionCandidate c, ProjectPriority priority, int planOrder) : Project` — creates the Facility row
    (`CommissionedYear = -1`) at the hex, sets basket = `BuildCost /
    ConstructionYears` (× `TierCostFactor(1)` = 1), wages =
    administered value / ConstructionYears, `TypeId`/`TargetId`.
- Consumes: `Substrate.Siting.Score`, `Substrate.Infrastructure.Get`,
  `Market.InitialPrice`, `MarketEngine.FieldsAt/EmbodimentOf`,
  `TechOps.AstroRadiusBonus`, `PortDomains.ServiceRadius` — all already used
  inside `BuildFacilities`; they move with the code.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void FacilityConstruction_ConsumesOverYears_ThenCommissions()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        var market = state.Markets[portId];
        var def = StarGen.Core.Substrate.Infrastructure.Get(
            StarGen.Core.Substrate.InfraTypeId.Mine);
        // plenty of everything, deep treasury
        foreach (var q in def.BuildCost)
            market.Inventory[(int)q.Good] += q.Quantity * 2;
        pr.DevelopmentPoints += 1000;
        var candidate = new ConstructionCandidate(
            (int)StarGen.Core.Substrate.InfraTypeId.Mine,
            state.Ports[portId].Hex, portId, 1.0);
        var p = ProjectOps.SpawnFacilityConstruction(state, pr.ActorId,
            pr.ActorId, candidate, ProjectPriority.Core, 0);
        var f = state.Facilities[p.TargetId];
        Assert.Equal(-1, f.CommissionedYear);            // site, not facility
        Assert.False(MarketEngine.IsActive(state, f));
        // basket × years == the old lump (conservation invariant)
        foreach (var q in def.BuildCost)
            Assert.Equal(q.Quantity,
                p.PerYearBasket[(int)q.Good] * p.YearsRequired, 6);
        ProjectOps.AdvanceAll(state);                    // 25y span ≥ 2y build
        Assert.True(p.Completed);
        Assert.True(MarketEngine.IsActive(state, f));
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~FacilityConstruction_Consumes"`
Expected: FAIL (`ConstructionCandidate` / `SpawnFacilityConstruction` missing).

- [ ] **Step 3: Implement**

`CapabilityOps.cs` — new file; move the scan out of
`AllocationPhase.BuildFacilities` (Phases.cs:371–437) verbatim where possible:

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>A sited, scored construction option — a PERCEIVED candidate
/// (spec §2): the same list the planner AI ranks and a player would read
/// off the economy screen (P2).</summary>
public sealed record ConstructionCandidate(
    int TypeId, HexCoordinate Hex, int PortId, double Score);

/// <summary>Perception-side capability assembly (spec §2). The candidate
/// scan is the siting-score × price-signal × saturation math that lived in
/// AllocationPhase.BuildFacilities — moved here so deciding what to build
/// is Intent's and executing is Allocation's (Move 1 made honest).</summary>
public static class CapabilityOps
{
    /// <summary>Buildable producer types — the keystone port comes from
    /// colonization, gates from lane construction.</summary>
    internal static readonly Substrate.InfraTypeId[] BuildableTypes =
    {
        Substrate.InfraTypeId.Mine, Substrate.InfraTypeId.Skimmer,
        Substrate.InfraTypeId.AgriComplex, Substrate.InfraTypeId.ExcavationSite,
        Substrate.InfraTypeId.Refinery, Substrate.InfraTypeId.Chemworks,
        Substrate.InfraTypeId.Fabricator, Substrate.InfraTypeId.ExoticsLab,
        Substrate.InfraTypeId.Foundry, Substrate.InfraTypeId.Shipyard,
        Substrate.InfraTypeId.Arsenal, Substrate.InfraTypeId.ComputeCore,
        Substrate.InfraTypeId.Fortress,
    };

    /// <summary>Top 3 candidates per own under-capacity port, score-ranked
    /// (ties: lower TypeId, then cell spiral order). Under-construction
    /// facilities count against the port cap and occupy their hexes — a
    /// plan must not double-book a site.</summary>
    public static List<ConstructionCandidate> ConstructionCandidatesFor(
        SimState state, int actorId)
    {
        // [move the BuildFacilities scan here: per own port — cap check
        //  including uncommissioned facilities, cell loop within
        //  ServiceRadius + AstroRadiusBonus, BuildableTypes loop with the
        //  Fortress tech-tier gate, Siting.Score × PriceSignal /
        //  (1 + existing) — collecting up to the top 3 (score, type, hex)
        //  per port into the result instead of tracking one best; PickHex
        //  keeps its skip-taken-anchors logic and moves here too. DELETE
        //  the CanAfford stock gate — affordability is now the planner's
        //  rate packing + groundbreak's treasury check.]
    }

    // PriceSignal and PickHex move here INTACT from AllocationPhase
    // (Phases.cs — the private methods directly below BuildFacilities:
    // "Mean price-over-founding ratio…" and "First anchor hex in the cell
    // free of facilities…"). Cut, paste, change `private` → `internal`,
    // keep every expression byte-for-byte.
}
```

(The bracketed move is mechanical: cut Phases.cs:393–437 into the loop shape
above, replacing "track one best" with "insert into a per-port top-3 list".
Keep every scoring expression byte-for-byte.)

`ProjectOps.SpawnFacilityConstruction`:

```csharp
    /// <summary>Groundbreak a facility: the Facility row exists NOW at the
    /// hex, uncommissioned (P1 — the construction site is residue); basket
    /// = BuildCost / ConstructionYears (the conservation invariant); wages
    /// = administered value / ConstructionYears.</summary>
    public static Project SpawnFacilityConstruction(SimState state,
        int ownerActorId, int funderActorId, ConstructionCandidate c,
        ProjectPriority priority, int planOrder)
    {
        var type = (Substrate.InfraTypeId)c.TypeId;
        var def = Substrate.Infrastructure.Get(type);
        var facility = new Facility(state.Facilities.Count, c.TypeId,
            tier: 1, c.Hex, ownerActorId, state.WorldYear)
        { CommissionedYear = -1 };
        state.Facilities.Add(facility);
        double years = Math.Max(1.0, def.ConstructionYears);
        var p = Spawn(state, ProjectKind.FacilityConstruction, ownerActorId,
                      funderActorId, c.PortId, c.Hex, years, priority,
                      planOrder);
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
        return p;
    }
```

In `AllocationPhase`: delete the `BuildFacilities` method, the
`BuildableTypes` array, `CanAfford`, `PriceSignal`, `PickHex` (all moved or
retired), and the `facilitiesBuilt += BuildFacilities(...)` call. (The
replacement caller — Groundbreak from the plan — arrives Task 7; between
these tasks the sim simply builds no new polity facilities, which is fine:
goldens are already red.)

- [ ] **Step 4: Run the suite**

Run: `dotnet test StarSystemGeneration.sln`
Expected: ProjectOpsTests PASS. Tests that asserted facilities appear via the
old greedy loop will fail — inventory them, convert the ones that test
*construction happens under investment* to run enough epochs with the planner
(they'll pass again after Task 7; mark `Skip = "t1: planner lands Task 7"`),
and rewrite any that tested `BuildFacilities` internals against
`CapabilityOps.ConstructionCandidatesFor` now.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): facility construction spawns projects; candidate scan moves to perception side"
```

---

### Task 6: The capability brief in Perception

**Files:**
- Modify: `src/Core/Epoch/CapabilityOps.cs` (add brief types + builder)
- Modify: `src/Core/Epoch/ControllerContract.cs` (`PerceptionView` gains
  `Capability`, `ConstructionCandidates`, `OwnPorts`)
- Modify: `src/Core/Epoch/Phases.cs` (`PerceptionPhase.Run` assembles them)
- Test: `tests/Core.Tests/Epoch/CapabilityTests.cs`

**Interfaces:**
- Produces (in `CapabilityOps.cs`):

```csharp
/// <summary>One own port as the planner sees it (spec §2).</summary>
public sealed record PortBrief(int PortId, int Tier, int YardTiers);

/// <summary>One in-flight funding obligation: value drawn per world-year
/// (goods at founding prices + wages) and naive years to completion.</summary>
public sealed record CommitmentBrief(double CostPerYear, double YearsRemaining);

/// <summary>The perceived economy-as-rates (spec §2): what the planner
/// schedules against. Own-side facts, assembled fresh each Perception.</summary>
public sealed class CapabilityBrief
{
    public double IncomePerYear { get; }               // trailing (P3)
    public IReadOnlyList<double> GenerationPerYear { get; }  // per good
    public IReadOnlyList<CommitmentBrief> Commitments { get; }
    public double CommittedCostPerYear { get; }        // Σ commitments now

    public CapabilityBrief(double incomePerYear,
        IReadOnlyList<double> generationPerYear,
        IReadOnlyList<CommitmentBrief> commitments)
    {
        IncomePerYear = incomePerYear;
        GenerationPerYear = generationPerYear;
        Commitments = commitments;
        double sum = 0;
        foreach (var c in commitments) sum += c.CostPerYear;
        CommittedCostPerYear = sum;
    }
}
```

- `CapabilityOps.BriefFor(SimState state, int actorId) : CapabilityBrief` —
  own-side facts read fresh:
  - `IncomePerYear` = polity `LastIncomePerYear` (corp: its own field).
  - `GenerationPerYear[g]`: for each active own facility with products,
    `def.BaseOutputPerYear × Production.TierOutputFactor(f.Tier) ×
    f.Condition / def.Produces.Count` added to each product — a perception
    ESTIMATE, deliberately coarser than SupplyLands' truth (P3).
  - `Commitments`: one per in-flight project the actor funds:
    `CostPerYear` = Σ basket good × founding price + wages/yr;
    `YearsRemaining = YearsRequired − YearsDelivered` (fed-rate naive).
- `PerceptionView` ctor appends optional params (pattern of the existing
  ones): `CapabilityBrief? capability = null`,
  `IReadOnlyList<ConstructionCandidate>? constructionCandidates = null`,
  `IReadOnlyList<PortBrief>? ownPorts = null`, exposed as `Capability`
  (nullable), `ConstructionCandidates` (empty default), `OwnPorts` (empty
  default).
- `PerceptionPhase.Run`: inside the polity branch, build all three and pass
  them; `YardTiers` per port = Σ `f.Tier` of active own shipyards whose
  `AttachedMarketIndex == port.Id`.

- [ ] **Step 1: Write the failing test**

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class CapabilityTests
{
    [Fact]
    public void Brief_CommittedRates_ReflectInFlightProjects()
    {
        var (_, state) = EpochTestKit.Seeded();
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        int port = ProjectOpsTests.OwnPort(state, actor);
        var before = CapabilityOps.BriefFor(state, actor);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, actor, actor,
            port, state.Ports[port].Hex, 5.0, ProjectPriority.Core, 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Alloys] = 2.0;
        p.WagesPerYear = 4.0;
        var after = CapabilityOps.BriefFor(state, actor);
        double alloyPrice = Market.InitialPrice(state.Config.Economy,
                                                GoodId.Alloys);
        Assert.Equal(before.CommittedCostPerYear
                     + 2.0 * alloyPrice + 4.0,
                     after.CommittedCostPerYear, 6);
    }

    [Fact]
    public void Brief_ArrivesInPerceptionView()
    {
        var (_, state) = EpochTestKit.Seeded();
        new PerceptionPhase().Run(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = state.Actors[actor].Perception!;
        Assert.NotNull(view.Capability);
        Assert.NotEmpty(view.OwnPorts);
    }
}
```

- [ ] **Step 2: Run to verify failure** — filter `CapabilityTests`, expect
  compile failure (`BriefFor` missing).

- [ ] **Step 3: Implement** per the interface block above. `BriefFor` bodies
  are straightforward loops over `state.Facilities` / `state.Projects` with
  the formulas given; keep iteration in id order. In `PerceptionPhase.Run`,
  build them beside the existing polity-only briefs (where `designs`,
  `ownPorts` count etc. are assembled) and pass via the new ctor params.

- [ ] **Step 4: Run to verify pass** — filter `CapabilityTests`, expect PASS;
  then full suite for regressions.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): capability brief + construction candidates in PerceptionView"
```

---

### Task 7: StandingPlan, the Planner, and Allocation's groundbreak pass

**Files:**
- Create: `src/Core/Epoch/Plan.cs` (+ `.meta`) — plan types
- Create: `src/Core/Epoch/Planner.cs` (+ `.meta`) — the scheduler
- Modify: `src/Core/Epoch/Policies.cs` (`PolityPolicies` gains `Plan`)
- Modify: `src/Core/Epoch/ControllerContract.cs` (`GenesisController` emits it;
  `TrivialController` untouched — `StandingPlan.Empty` is the record default)
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase`: groundbreak pass +
  `ProjectOps.AdvanceAll` call; delete `RaisePorts`)
- Modify: `src/Core/Epoch/ArtifactSerializer.cs` (PLANE lines, actors layer
  already at v6)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` + `KnobRegistry.cs` +
  `docs/TUNING.md` (planner + port-raise knobs)
- Test: `tests/Core.Tests/Epoch/PlannerTests.cs`

**Interfaces:**
- `Plan.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>What one plan entry orders (spec §3). Kind discriminator is
/// append-only — program-style entries join later without a schema break.</summary>
public enum PlanEntryKind { Facility = 0, PortRaise = 1, HullBatch = 2 }

/// <summary>One scheduled project order: what, where, when, how urgent.
/// TypeId: InfraTypeId (Facility) or design id (HullBatch). StartYear is
/// an absolute world-year.</summary>
public sealed record PlanEntry(
    PlanEntryKind Kind, ProjectPriority Priority, int StartYear,
    int TypeId, int PortId, HexCoordinate Hex, int Count);

/// <summary>The standing schedule Intent emits and Allocation executes
/// (spec §3) — entries in plan order; regenerated every Intent, persisted
/// like any policy so a loaded artifact resumes mid-plan.</summary>
public sealed record StandingPlan(IReadOnlyList<PlanEntry> Entries)
{
    public static StandingPlan Empty { get; } =
        new StandingPlan(new PlanEntry[0]);
}
```

- `PolityPolicies` gains `StandingPlan Plan` as a new last positional field;
  `Default` passes `StandingPlan.Empty`. Every `policies with { … }` site
  keeps compiling (records).
- `Planner.BuildPlan(PerceptionView view, PolityPolicies policies, EpochSimConfig cfg) : StandingPlan`
  — pure function of the view + the SAME decision's other policies
  (ShipbuildingPriorities feed the hull entries; all inside one `Decide`,
  P2-clean). Algorithm (deterministic, no rolls):
  1. Horizon `H = cfg.Sim.GenerationYears`; capacity = `view.Capability.IncomePerYear` (credits/yr).
  2. Build a `double[H]` committed-cost timeline seeded from
     `view.Capability.Commitments` (each fills years `0..ceil(YearsRemaining)-1`).
  3. Assemble desired entries with scores:
     - Facility per `view.ConstructionCandidates`: score =
       `c.Score × (1.0 − 0.3 × view.SelfTemperament.Militancy)`;
       cost/yr and duration from the type's `BuildCost`/`ConstructionYears`
       at founding prices (+ wages = value/years, i.e. cost/yr = 2 ×
       value/years).
     - PortRaise per `view.OwnPorts` with `Tier < cfg.Infrastructure.MaxPortTier`:
       score = `cfg.Controller.PortRaisePlanScore / port.Tier ×
       (1.0 − 0.3 × Militancy)`; duration = `cfg.Expansion.PortUpgradeYears`;
       cost/yr = (basket value + `cfg.Expansion.PortUpgradeCostBase × Tier`)
       / duration (basket per Task 7 knobs below).
     - HullBatch: for each port with `YardTiers > 0`, D'Hondt over
       `policies.ShipbuildingPriorities`-weighted `view.OwnDesigns` granting up
       to `YardTiers` batches; score of each grant =
       `(0.2 + 0.6 × Militancy) × (atWar ? 2.0 : 1.0) × claim` where
       claim = weight/(granted+1) and atWar = `view.Wars.Count > 0`;
       duration = `cfg.Fleet.HullBuildYearsBase ×
       (DesignMath.ComponentsPerHull(cfg.Fleet, size) /
        DesignMath.ComponentsPerHull(cfg.Fleet, ShipSize.Medium))`;
       cost/yr = (components + armaments at founding prices)/duration × 2.
       Priority = `atWar ? ProjectPriority.War : ProjectPriority.Growth`;
       civil entries are `ProjectPriority.Core`.
  4. Sort by score desc; ties by (Kind asc, PortId asc, TypeId asc, Hex.Q,
     Hex.R).
  5. Pack greedily: for each entry, earliest integer start `s ∈ [0, H)` such
     that `timeline[y] + entryCostPerYear ≤ capacity` for every
     `y ∈ [s, min(s + ceil(duration), H))`; if found, stamp
     `StartYear = view.WorldYear + s`, add to the plan, update the timeline;
     else drop. Stop at `cfg.Controller.MaxPlanEntries`.
- `GenesisController.PoliciesFor` ends with
  `policies = policies with { Plan = Planner.BuildPlan(perceived, policies, _config) };`
  (after ShipbuildingPriorities are set — order matters).
- `AllocationPhase.Run`, per polity, replaces the deleted greedy calls with:

```csharp
            Groundbreak(state, pr, policies.Plan);
```

  and after the corporations `Operate` call (so corp spawns advance too),
  replaces nothing but ADDS:

```csharp
        int completions = ProjectOps.AdvanceAll(state);
```

  with `completions` appended to the phase note
  (`", N project completions"` when > 0).
- `Groundbreak(SimState, PolityRecord, StandingPlan)` (new private method):
  entries in order; skip when `entry.StartYear >= state.WorldYear +
  cfg.Sim.YearsPerEpoch` (not due). Truth checks per kind (Move 2), all
  against CURRENT state:
  - Facility: hex holds no facility; port still owned; attached facility
    count (including uncommissioned) < `port.Tier × FacilitiesPerPortTier`;
    `pr.DevelopmentPoints ≥` total administered value. Spawn via
    `ProjectOps.SpawnFacilityConstruction` with the entry's fields and
    `planOrder` = entry index.
  - PortRaise: port owned, `Tier < MaxPortTier`, no in-flight PortRaise
    project targeting it, `pr.DevelopmentPoints ≥ PortUpgradeCostBase × Tier`.
    Spawn `ProjectKind.PortRaise` with duration `PortUpgradeYears`, basket
    per year = `PortUpgradeAlloysPerYearPerTier × Tier` Alloys +
    `PortUpgradeMachineryPerYearPerTier × Tier` Machinery +
    `PortUpgradeExoticsPerYearPerTier × Tier` RefinedExotics; wages/yr =
    `PortUpgradeCostBase × Tier / PortUpgradeYears`; `TargetId = portId`.
  - HullBatch: port owned; design exists and owned; in-flight HullBatch
    count at the port < Σ active own shipyard tiers there;
    `pr.MilitaryPoints ≥` batch administered value. Spawn per Task 8's
    `SpawnHullBatch` (this task can stub the call site behind
    `PlanEntryKind.HullBatch` with a `continue` + `// Task 8` marker ONLY if
    tasks land in order — prefer landing Task 8 first in the same session if
    convenient).
  A failed truth check skips the entry without charge.
- Delete `RaisePorts` and its call.
- Serializer: after the POLICY line write, add per-entry lines (actors v6):

```csharp
            if (a.Policies is PolityPolicies withPlan)
                for (int ix = 0; ix < withPlan.Plan.Entries.Count; ix++)
                {
                    var e = withPlan.Plan.Entries[ix];
                    w.WriteLine(Join("PLANE", a.Id.ToString(Inv),
                        ix.ToString(Inv), ((int)e.Kind).ToString(Inv),
                        ((int)e.Priority).ToString(Inv),
                        e.StartYear.ToString(Inv), e.TypeId.ToString(Inv),
                        e.PortId.ToString(Inv), e.Hex.Q.ToString(Inv),
                        e.Hex.R.ToString(Inv), e.Count.ToString(Inv)));
                }
```

  Load: collect PLANE lines per actor (they follow its POLICY line), rebuild
  `StandingPlan`, attach via `policies with { Plan = … }`.
- New knobs (add to the config classes, `KnobRegistry` table name-sorted,
  and `docs/TUNING.md`):
  - `ExpansionKnobs.PortUpgradeYears = 5.0` — "world-years to raise a port one tier"
  - `ExpansionKnobs.PortUpgradeAlloysPerYearPerTier = 2.0`
  - `ExpansionKnobs.PortUpgradeMachineryPerYearPerTier = 1.0`
  - `ExpansionKnobs.PortUpgradeExoticsPerYearPerTier = 0.25`
  - `ControllerKnobs.MaxPlanEntries = 16` (int-rounded setter)
  - `ControllerKnobs.PortRaisePlanScore = 0.5`
  - `FleetKnobs.HullBuildYearsBase = 1.5`

- [ ] **Step 1: Write the failing tests**

```csharp
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class PlannerTests
{
    private static PerceptionView ViewOf(SimState state, int actorId)
    {
        new PerceptionPhase().Run(state);
        return state.Actors[actorId].Perception!;
    }

    [Fact]
    public void Plan_IsDeterministic_ForTheSameView()
    {
        var (_, state) = EpochTestKit.Seeded();
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = ViewOf(state, actor);
        var a = Planner.BuildPlan(view, PolityPolicies.Default, state.Config);
        var b = Planner.BuildPlan(view, PolityPolicies.Default, state.Config);
        Assert.Equal(a.Entries, b.Entries);
    }

    [Fact]
    public void Plan_NeverOverCommits_TheIncomeRate()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Config.Sim.EpochCount = 6;
        new EpochEngine().Run(state);              // real income history
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = ViewOf(state, actor);
        var plan = Planner.BuildPlan(view, PolityPolicies.Default,
                                     state.Config);
        // rebuild the packed timeline and assert the cap held
        int H = state.Config.Sim.GenerationYears;
        var timeline = new double[H];
        foreach (var c in view.Capability!.Commitments)
            for (int y = 0; y < System.Math.Min(H,
                     (int)System.Math.Ceiling(c.YearsRemaining)); y++)
                timeline[y] += c.CostPerYear;
        foreach (var e in plan.Entries)
        {
            var (costPerYear, duration) = Planner.CostOf(e, view, state.Config);
            int s = e.StartYear - view.WorldYear;
            for (int y = s; y < System.Math.Min(H,
                     s + (int)System.Math.Ceiling(duration)); y++)
            {
                timeline[y] += costPerYear;
                Assert.True(timeline[y]
                    <= view.Capability.IncomePerYear + 1e-6,
                    $"year {y} over-committed");
            }
        }
    }

    [Fact]
    public void Plan_SchedulesUnaffordableWork_LaterNotNever()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Config.Sim.EpochCount = 6;
        new EpochEngine().Run(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = ViewOf(state, actor);
        var plan = Planner.BuildPlan(view, PolityPolicies.Default,
                                     state.Config);
        // with real commitments some entries should start in the future —
        // the staggered schedule is the point (spec §3)
        if (plan.Entries.Count >= 2)
            Assert.Contains(plan.Entries,
                e => e.StartYear > view.WorldYear);
    }
}
```

(`Planner.CostOf(PlanEntry, PerceptionView, EpochSimConfig)` is `internal` —
expose the entry-costing used by packing so the test can replay it; add
`[assembly: InternalsVisibleTo("Core.Tests")]` only if not already granted —
check `src/Core` for an existing `InternalsVisibleTo`; if none, make `CostOf`
public instead.)

- [ ] **Step 2: Verify failure** — filter `PlannerTests`, compile error.

- [ ] **Step 3: Implement** per the interface block. Order inside
  `AllocationPhase.Run` after this task:

```
ServiceLoans → PayTribute → per polity: budget split, appeasement, research,
SpawnMobilizations (Task 10 — absent until then), Groundbreak(plan),
BuildLanes (still instant until Task 9), ManagePostures, SupplyFleets,
RunUpkeep, DecayReserves → CorporationOps.Operate → TechOps.Diffuse →
ProjectOps.AdvanceAll → Borrow
```

- [ ] **Step 4: Verify green** — filter `PlannerTests` then the full suite.
  Un-skip the Task-5 skipped tests that waited on the planner; they should
  now see facilities appear again over epochs (via plan → groundbreak →
  advance → commission). Confirm with the REPL smoke:

```bash
printf 'epoch 42\nestep\nestep\nestep\n' | dotnet run --project src/Inspector
```

Expected: Allocation trace notes show "project completions" appearing by the
second or third step.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests docs/TUNING.md
git commit -m "feat(time): StandingPlan + fixed-horizon Planner; Allocation groundbreaks the plan"
```

---

### Task 8: Hull batches — shipbuilding takes years

**Files:**
- Modify: `src/Core/Epoch/ProjectOps.cs` (`SpawnHullBatch`, HullBatch completion)
- Modify: `src/Core/Epoch/FleetOps.cs` (delete `BuildFleets` + its D'Hondt
  queue helper if unused elsewhere; keep `HomeFleet`)
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase`: remove
  `FleetOps.BuildFleets` call; `Groundbreak` HullBatch case goes live)
- Modify: `src/Core/Epoch/Rng/RollChannel.cs` — note: `YardSlots` channel
  becomes unused; leave the enum entry (append-only vocabulary), delete only
  the rolling code with `BuildFleets`.
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (extend)

**Interfaces:**
- `ProjectOps.SpawnHullBatch(SimState state, int ownerActorId, int portId, ShipDesign design, int count, ProjectPriority priority, int planOrder) : Project` —
  duration = `cfg.Fleet.HullBuildYearsBase × (ComponentsPerHull(size) /
  ComponentsPerHull(Medium))`; basket/yr = `(components + armaments) × count
  / duration` (components → `GoodId.ShipComponents`, armaments →
  `GoodId.Armaments` when > 0); wages/yr = administered value / duration;
  `TypeId = design.Id`, `Count = count`.
- Completion case `HullBatch` in `ProjectOps.Complete`:

```csharp
            case ProjectKind.HullBatch:
            {
                var design = state.Designs[p.TypeId];
                double grade = p.AccumGradeWeight > 0 ? p.AccumGrade : 0.5;
                var advanced = DesignRegistry.MaybeAdvanceMark(state, design,
                    grade, state.Ports[p.PortId].Hex);
                FleetOps.HomeFleet(state, p.OwnerActorId,
                    state.Ports[p.PortId]).AddHulls(advanced.Id, p.Count,
                                                    grade);
                state.PolityOf(p.OwnerActorId).HullsBuilt += p.Count;
                break;
            }
```

  (Check `HomeFleet`'s access level — it is used inside FleetOps today; make
  it `internal` if private. If `MaybeAdvanceMark`'s signature differs, match
  the call `BuildFleets` used — same arguments, same semantics.)
- Consumes: `DesignMath.ComponentsPerHull/ArmamentsPerHull`,
  `DesignRegistry.MaybeAdvanceMark`, `FleetOps.HomeFleet` — all exist.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void HullBatch_CommissionsHullsAtCompletion_NotBefore()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        ShipDesign? design = null;
        foreach (var d in state.Designs)
            if (d.OwnerActorId == pr.ActorId) { design = d; break; }
        Assert.NotNull(design);
        var market = state.Markets[portId];
        market.Inventory[(int)GoodId.ShipComponents] += 100;
        market.Inventory[(int)GoodId.Armaments] += 100;
        pr.MilitaryPoints += 1000;
        int built = pr.HullsBuilt;
        var p = ProjectOps.SpawnHullBatch(state, pr.ActorId, portId,
            design!, count: 2, ProjectPriority.Growth, 0);
        Assert.Equal(built, pr.HullsBuilt);          // nothing yet
        ProjectOps.AdvanceAll(state);                // span covers the build
        Assert.True(p.Completed);
        Assert.Equal(built + 2, pr.HullsBuilt);
    }
```

- [ ] **Step 2: Verify failure** — `SpawnHullBatch` missing.

- [ ] **Step 3: Implement** per the interfaces; delete
  `FleetOps.BuildFleets` and its call (`hullsLaid += …`) in AllocationPhase;
  wire `Groundbreak`'s `PlanEntryKind.HullBatch` case to `SpawnHullBatch`
  (design resolved from `entry.TypeId` with an owned-design truth check).

- [ ] **Step 4: Run the full suite.** Fleet/economy tests asserting same-step
  hull laydown convert to run more epochs or spawn batches directly. The navy
  now arrives late by design — tests that starved because the yard produced
  instantly may need their epoch counts raised, not their assertions relaxed.

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): hull batches replace instant yard laydown"
```

---

### Task 9: Gate pairs and colony expeditions take time

**Files:**
- Modify: `src/Core/Epoch/ProjectOps.cs` (`SpawnGatePair`,
  `SpawnExpedition`, expedition completion — the founding body moves here)
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase.BuildLanePair` →
  spawn; `BuildGate` keeps goods-free shell; `ResolutionPhase.TryFound` splits)
- Modify: `src/Core/Epoch/Interior/CorporationOps.cs` (`InvestGateLanes` and
  `InvestFacilities` spawn projects)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` + `KnobRegistry.cs` +
  `docs/TUNING.md`: `FleetKnobs.ExpeditionHexesPerYear = 6.0` — "off-lane
  convoy speed, hexes per world-year".
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (extend)

**Interfaces:**
- `ProjectOps.SpawnGatePair(SimState state, int ownerActorId, int funderActorId, Port a, Port b, int tier, ProjectPriority priority, int planOrder) : Project`:
  - creates BOTH gate facilities now (uncommissioned, `CommissionedYear=-1`,
    at each port's hex, tier = gate tier) — they hold their gate slots from
    groundbreaking (`LaneNetwork.HasFreeGateSlot` counts facility rows, so
    no change needed there — VERIFY with a quick read of
    `LaneNetwork.HasFreeGateSlot` and adjust if it filters on IsActive);
  - creates the Lane row now with both gate ids (it reads dead until both
    commission — Task 2's `IsLive`);
  - one project: `PortId = a.Id` (Stage-1 interim: the pair draws at the A
    end + funder reserves; per-end draws are Stage 2 — leave a `// stage-2:`
    comment), duration = gate `ConstructionYears`, basket/yr = full PAIR
    basket (`2 × BuildCost × TierCostFactor(tier) / ConstructionYears`),
    wages/yr = pair administered value / years, `TypeId =
    (int)InfraTypeId.Gate`, `TargetId = lane.Id`.
- `AllocationPhase.BuildLanePair` becomes a thin wrapper calling
  `SpawnGatePair` (keep the dev-treasury affordability check in `BuildLanes`;
  DELETE `GatePairGoodsPresent` and its call — goods now arrive over years;
  DELETE the goods-drawing body of `BuildGate` — `SpawnGatePair` replaces
  both ends; remove `BuildGate` entirely once CorporationOps stops calling
  it this task).
- `CorporationOps.InvestGateLanes`: replace the two `BuildGate` calls + lane
  creation + event with one `SpawnGatePair(state, corp.ActorId,
  corp.ActorId, bestA, bestB!, bestTier, ProjectPriority.Growth, 0)`; keep
  `corp.Credits -= bestCost` as the commitment (it becomes the wage stream's
  source via `TreasuryAvailable` — actually REMOVE the upfront debit, the
  stream charges credits; keep only the affordability CHECK).
- `CorporationOps.InvestFacilities`: the terminal block (draw goods, debit,
  `new Facility`, event — lines ~750–765) becomes
  `ProjectOps.SpawnFacilityConstruction(state, corp.ActorId, corp.ActorId,
  new ConstructionCandidate((int)type, port.Hex, port.Id, score), 
  ProjectPriority.Growth, 0)` with the affordability check retained and the
  upfront debit removed (stream charges).
- `ResolutionPhase.TryFound` splits at the convoy dispatch:
  - KEEP: validation, `ExpansionPoints` charge, hull removal, convoy fleet
    creation (Expedition posture), `ConvoyDispatched` event, fuel draw.
  - REPLACE everything from `convoy.Hex = act.Target;` (line 1132) to the
    encroachment loop's end with:

```csharp
        var p = ProjectOps.SpawnExpedition(state, act.ActorId, staging.Id,
            act.Target, convoy.Id, offLane);
```

- `ProjectOps.SpawnExpedition(SimState state, int ownerActorId, int stagingPortId, HexCoordinate target, int convoyFleetId, int offLaneHexes) : Project`
  — duration = `offLaneHexes / cfg.Fleet.ExpeditionHexesPerYear`, empty
  basket, no wages, `Priority = ProjectPriority.Core`, `PortId =
  stagingPortId`, `Hex = target`, `TargetId = convoyFleetId`.
- Expedition en-route visibility: in `Feed` — travel kinds skip the goods
  loop but still advance; after advancing, for `ColonyExpedition` move the
  convoy: 

```csharp
        if (p.Kind == ProjectKind.ColonyExpedition && p.TargetId >= 0)
        {
            var convoy = state.Fleets[p.TargetId];
            var from = state.Ports[p.PortId].Hex;
            convoy.Hex = HexGrid.Round(
                from.Q + (p.Hex.Q - from.Q) * p.Progress,
                from.R + (p.Hex.R - from.R) * p.Progress);
        }
```

- Expedition completion case in `Complete`: the moved founding body —
  port + market + colony segment (+ official ideology), founding facilities
  (CommissionED at birth: the expedition shipped the equipment — the
  existing convention), convoy → Reserve at the new port + hull scrapped,
  `PortEstablished` event, founder mint, encroachment tension bumps. Move
  `FoundingIndustry` from `ResolutionPhase` to `ProjectOps` (private static,
  unchanged). Guard: if the target hex gained a port while the convoy flew,
  the expedition completes as a FAILED founding — convoy returns to Reserve
  at its staging port, no port created (stage a `ConvoyDispatched`-family
  event only if a matching type exists; otherwise no event — do not invent
  event types this slice).

- [ ] **Step 1: Write the failing tests**

```csharp
    [Fact]
    public void GatePair_LaneOpensOnlyWhenCommissioned()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        // two own ports without a mutual lane; give the polity a second
        // port if the seed left it with one (skip in that case)
        var own = new System.Collections.Generic.List<Port>();
        foreach (var port in state.Ports)
            if (port.OwnerActorId == pr.ActorId) own.Add(port);
        if (own.Count < 2) return;                    // seed-shaped: skip
        var p = ProjectOps.SpawnGatePair(state, pr.ActorId, pr.ActorId,
            own[0], own[1], tier: 1, ProjectPriority.Core, 0);
        var lane = state.Lanes[p.TargetId];
        Assert.False(LaneMath.IsLive(state, lane));   // half a highway is none
        state.Markets[own[0].Id].Inventory[(int)GoodId.Alloys] += 100;
        state.Markets[own[0].Id].Inventory[(int)GoodId.Machinery] += 100;
        state.Markets[own[0].Id].Inventory[(int)GoodId.Composites] += 100;
        pr.DevelopmentPoints += 1000;
        ProjectOps.AdvanceAll(state);
        Assert.True(p.Completed);
        Assert.True(LaneMath.IsLive(state, lane));
    }

    [Fact]
    public void Expedition_FoundsThePort_OnlyOnArrival()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int staging = OwnPort(state, pr.ActorId);
        int portsBefore = state.Ports.Count;
        var convoy = new FleetRecord(state.Fleets.Count, pr.ActorId,
            state.Ports[staging].Hex)
        { Posture = FleetPosture.Expedition, HomePortId = staging };
        state.Fleets.Add(convoy);
        var target = new StarGen.Core.Model.HexCoordinate(
            state.Ports[staging].Hex.Q + 12, state.Ports[staging].Hex.R);
        var p = ProjectOps.SpawnExpedition(state, pr.ActorId, staging,
            target, convoy.Id, offLaneHexes: 12);
        Assert.True(p.YearsRequired > 0);
        Assert.Equal(portsBefore, state.Ports.Count);  // in flight
        ProjectOps.AdvanceAll(state);                  // 25y ≥ 12/6 = 2y
        Assert.True(p.Completed);
        Assert.Equal(portsBefore + 1, state.Ports.Count);
    }
```

(If the expedition target lands on a void cell or taken hex for some seed,
pick the target by scanning `ColonyValuation.CandidatesFor(state,
pr.ActorId)` first and use `candidates[0].Target`; keep the test
deterministic — seed 42 is fixed.)

- [ ] **Step 2: Verify failure.**
- [ ] **Step 3: Implement** per the interfaces.
- [ ] **Step 4: Full suite.** Colonization/lane tests asserting same-step
  founding/opening now need epochs; founding-link tests (isolated ports)
  should observe the gate-pair PROJECT existing after one step and the lane
  LIVE after construction years — that is the honest wave the lane branch
  deferred (HANDOFF: "no stock-on-hand gate … durations belong in
  world-time state").
- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests docs/TUNING.md
git commit -m "feat(time): gate pairs and colony expeditions run in world-time"
```

---

### Task 10: War mobilization ramps

**Files:**
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase`: `SpawnMobilizations`
  + peace decay)
- Modify: `src/Core/Epoch/FleetOps.cs` (`WarStrength` reads mobilization)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` + `KnobRegistry.cs` +
  `docs/TUNING.md`: `WarKnobs.MobilizationYears = 3.0`,
  `WarKnobs.MobilizationArmamentsPerYear = 3.0`,
  `WarKnobs.MobilizationFuelPerYear = 4.0`,
  `WarKnobs.DemobilizationPerYear = 0.15`.
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (extend)

**Interfaces:**
- In `AllocationPhase.Run`, per polity, before `Groundbreak`:

```csharp
            SpawnMobilizations(state, pr);
```

```csharp
    /// <summary>A belligerent raises a Mobilization project at its capital
    /// (spec §5): readiness ramps over years while consuming war materiel;
    /// fronts fight at CURRENT readiness — early battles use the standing
    /// force. At peace the ramp decays and in-flight mobilizations cancel.</summary>
    private static void SpawnMobilizations(SimState state, PolityRecord pr)
    {
        var cfg = state.Config;
        int years = cfg.Sim.YearsPerEpoch;
        bool atWar = WarOps.AtWar(state, pr.ActorId);
        if (!atWar)
        {
            foreach (var p in state.Projects)             // id order (P6)
                if (p.InFlight && p.Kind == ProjectKind.Mobilization
                    && p.OwnerActorId == pr.ActorId)
                    ProjectOps.Cancel(state, p);
            pr.Mobilization = System.Math.Max(0.0, pr.Mobilization
                - cfg.War.DemobilizationPerYear * years);
            return;
        }
        if (pr.Mobilization >= 1.0) return;
        foreach (var p in state.Projects)
            if (p.InFlight && p.Kind == ProjectKind.Mobilization
                && p.OwnerActorId == pr.ActorId) return;  // already raising
        int capital = -1;
        foreach (var port in state.Ports)                 // id order (P6)
            if (port.OwnerActorId == pr.ActorId) { capital = port.Id; break; }
        if (capital < 0) return;
        var proj = ProjectOps.Spawn(state, ProjectKind.Mobilization,
            pr.ActorId, pr.ActorId, capital, state.Ports[capital].Hex,
            cfg.War.MobilizationYears * (1.0 - pr.Mobilization),
            ProjectPriority.War, 0);
        proj.PerYearBasket[(int)Substrate.GoodId.Armaments] =
            cfg.War.MobilizationArmamentsPerYear;
        proj.PerYearBasket[(int)Substrate.GoodId.Fuel] =
            cfg.War.MobilizationFuelPerYear;
    }
```

- `FleetOps.WarStrength` — scale a polity's strength by its war economy:

```csharp
    public static double WarStrength(SimState state, int actorId)
    {
        double strength = 0;
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != actorId || fleet.TotalHulls == 0) continue;
            var v = Vectors(state, fleet);
            strength += (v.Strike + v.Sustained) * fleet.Readiness;
        }
        // the war economy multiplies the standing force: a fed mobilization
        // ramp reaches the full MobilizationFactor surge (spec §5)
        if (state.Actors[actorId].Kind == ActorKind.Polity)
            strength *= 1.0 + (state.Config.War.MobilizationFactor - 1.0)
                        * state.PolityOf(actorId).Mobilization;
        return strength;
    }
```

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Mobilization_RampsWithFeed_AndDecaysAtPeace()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var p = ProjectOps.Spawn(state, ProjectKind.Mobilization,
            pr.ActorId, pr.ActorId, port, state.Ports[port].Hex,
            yearsRequired: 3.0, ProjectPriority.War, 0);
        p.PerYearBasket[(int)GoodId.Armaments] = 1.0;
        state.Markets[port].Inventory[(int)GoodId.Armaments] += 100;
        pr.MilitaryPoints += 100;
        ProjectOps.AdvanceAll(state);
        Assert.True(p.Completed);
        Assert.Equal(1.0, pr.Mobilization, 6);
    }
```

- [ ] **Step 2: Verify failure** (Mobilization stays 0 — the completion case
  from Task 4 already sets it; if Task 4 landed, this passes immediately:
  then assert the DECAY path instead by running `AllocationPhase` on a
  peaceful polity with `Mobilization = 1.0` and asserting it dropped by
  `DemobilizationPerYear × years`).

- [ ] **Step 3: Implement** per the interfaces (knobs first, then the two
  code sites).

- [ ] **Step 4: Full suite** + war-shaped smoke:

```bash
printf 'epoch 42\n' | dotnet run --project src/Inspector
```

Expected: wars still ignite and settle over a 40-epoch run (the trace shows
declarations and settlements); mobilization changes strength ratios, so war
COUNTS may shift — that is tuning-expected, not a failure. If wars vanish
entirely or every war annihilates, revisit `MobilizationFactor` coupling
before proceeding (flag to the user at the eyeball gate).

- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests docs/TUNING.md
git commit -m "feat(time): war mobilization ramps in world-time; WarStrength reads the war economy"
```

---

### Task 11: Construction demand becomes real market demand

**Files:**
- Modify: `src/Core/Epoch/MarketEngine.cs` (`AddConstructionPull`)
- Test: `tests/Core.Tests/Epoch/CapabilityTests.cs` (extend)

**Interfaces:**
- `AddConstructionPull` keeps its stockpile-target block VERBATIM and
  replaces the speculative under-capacity-port block (lines ~451–467) with
  the projects sum (spec §4):

```csharp
        // in-flight work IS the construction demand: every project's
        // per-year basket registers at its site market for the span —
        // a build boom raises alloy prices for its whole duration (P5)
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var p in state.Projects)                 // id order (P6)
        {
            if (!p.InFlight) continue;
            for (int g = 0; g < p.PerYearBasket.Length; g++)
                if (p.PerYearBasket[g] > 0)
                    scratch.Demand[p.PortId][g] += p.PerYearBasket[g] * years;
        }
```

- The `ConstructionDevGate` / `ConstructionPullAlloys/Machinery/Composites`
  knobs become dead: REMOVE them from `InfrastructureKnobs`, the
  `KnobRegistry` table, and `docs/TUNING.md` (greenfield — no dead dials).

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void ConstructionPull_ReadsInFlightProjects()
    {
        var (_, state) = EpochTestKit.Seeded();
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        int port = ProjectOpsTests.OwnPort(state, actor);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, actor, actor,
            port, state.Ports[port].Hex, 5.0, ProjectPriority.Core, 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Machinery] = 2.0;
        var scratch = new MarketStepScratch(state);
        MarketEngine.AddConstructionPull(state, scratch);
        int years = state.Config.Sim.YearsPerEpoch;
        Assert.True(scratch.Demand[port][(int)GoodId.Machinery]
                    >= 2.0 * years);
    }
```

(If `MarketStepScratch`'s constructor is internal to the phase, follow how
existing market tests build it — check `tests/Core.Tests` for prior
`MarketStepScratch` usage and copy that pattern.)

- [ ] **Step 2: Verify failure.** — demand comes out as the old flat pull.
- [ ] **Step 3: Implement**, delete the three dead knobs + registry rows +
  TUNING rows.
- [ ] **Step 4: Full suite** (KnobRegistryTests catches a missed row).
- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests docs/TUNING.md
git commit -m "feat(time): construction pull reads in-flight projects; dead pull knobs removed"
```

---

### Task 12: Conquest transfers in-flight work

**Files:**
- Modify: wherever port capture transfers facilities — find it:
  `grep -rn "OwnerActorId =" src/Core/Epoch/Interpolity/` (the capture site
  in `WarResolution.cs`/`WarConduct.cs` that reassigns port + facility
  owners). Add project transfer beside facility transfer.
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (extend)

**Interfaces:**
- At the capture site, after facilities transfer:

```csharp
        foreach (var p in state.Projects)                 // id order (P6)
            if (p.InFlight && p.PortId == capturedPortId)
            {
                p.OwnerActorId = newOwnerActorId;
                p.FunderActorId = newOwnerActorId;
            }
```

  (Adapt the two local variable names to the capture site's own; the
  semantics are spec §1: site-anchored state transfers at current progress;
  the conqueror's next replan keeps or cancels.)
- Exception: `ColonyExpedition` projects do NOT transfer (the convoy is a
  fleet, not a site) — skip `p.Kind == ProjectKind.ColonyExpedition`.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Conquest_TransfersInFlightProjects_AtCurrentProgress()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, 5.0,
            ProjectPriority.Core, 0);
        p.TargetId = port;
        p.YearsDelivered = 2.0;
        // find any other entered polity to play conqueror
        int other = -1;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity
                && a.Id != pr.ActorId) { other = a.Id; break; }
        if (other < 0) return;                            // seed-shaped: skip
        WarResolution.TransferPort(state, port, other);   // ← use the REAL
        // capture entry point found in Step 3; adjust this call to it
        Assert.Equal(other, p.OwnerActorId);
        Assert.Equal(2.0, p.YearsDelivered, 9);           // progress kept
    }
```

- [ ] **Step 2: Locate the real capture path** (`grep` above), adjust the
  test to drive it (if capture is only reachable through `FightWars`, extract
  the port-transfer block into an internal `TransferPort(state, portId,
  newOwner)` helper as part of this task — a seam worth owning), verify the
  test fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Full suite.**
- [ ] **Step 5: Commit**

```bash
git add -A src/Core tests
git commit -m "feat(time): conquest transfers in-flight projects with the port"
```

---

### Task 13: REPL surface — eprojects and eplan

**Files:**
- Modify: `src/Inspector/Repl.cs` (new cases beside `elanes`, line ~245; help
  text beside the other `e*` entries)
- Test: manual REPL smoke (the inspector has no unit harness; the eyeball
  gate covers it)

**Interfaces:**
- `eprojects [actorId]` — table of in-flight projects (all, or one funder):
  `id · kind · owner · port · pri · fed% · delivered/required yrs · eta`.
  `eta = WorldYear + ceil((YearsRequired − YearsDelivered) /
  max(LastFedFraction, 0.05))` — the honest ETA under current starvation.
  Completed/cancelled shown only with `eprojects all`.
- `eplan <actorId>` — the actor's standing plan: `# · kind · pri · start ·
  type/design · port`; a `*` marks entries already matched by an in-flight
  project (same kind+port+type still in flight).
- Follow `elanes`'s exact structure: a `case "eprojects" when _sim != null:`
  arm rendering via a private method; a bare `case "eprojects":` arm printing
  the no-sim hint. Register both in the help text where `elanes` is listed.

- [ ] **Step 1: Implement** (no unit test — inspector surface).
- [ ] **Step 2: Smoke it**

```bash
printf 'epoch 42\neprojects\neplan 1\nestep\neprojects\n' | dotnet run --project src/Inspector
```

Expected: a non-empty project table after the first `estep` at latest (the
seed's polities plan and groundbreak in step 1); plan entries print with
future start years; no exception on empty ids.

- [ ] **Step 3: Commit**

```bash
git add src/Inspector/Repl.cs
git commit -m "feat(time): eprojects/eplan REPL surface"
```

---

### Task 14: FineTick honesty, golden re-freeze, docs, wrap-up

**Files:**
- Test: `tests/Core.Tests/Epoch/FineTickTests.cs` (extend)
- Modify: golden fixtures (regenerate ONCE — the red window closes here; the
  regen one-liner is documented at `git show 27fefe7~1:docs/HANDOFF.md`)
- Modify: `docs/design/frame/simulation-flow.md`,
  `docs/design/frame/controller-contract.md`,
  `docs/design/economy/assets-and-investment.md`,
  `docs/design/substrate/infrastructure.md`, `docs/design/frame/time.md`,
  `docs/design/economy/markets.md`, `docs/design/fleets/ships-and-fleets.md`,
  `docs/design/interpolity/war.md` (per spec §"Design-tree amendments" —
  present tense, final-design voice; each amended mechanic keeps/updates its
  P1 evidence)
- Modify: `docs/HANDOFF.md` (rewrite for this slice; next-up = Stage 2 plan)
- Create: `docs/superpowers/plans/2026-07-11-time-stage2-kickoff-prompt.md`
  (the located-logistics kickoff: what Stage 1 left ready — real file paths,
  real interfaces, surprises — plus Stage 2 scope from the spec §4b)

**Interfaces:** none new — this task closes the slice.

- [ ] **Step 1: Write the P7 honesty test**

```csharp
    /// <summary>THE durations test (spec §6): the same artifact stepped
    /// coarse (25y) and fine (1y) commissions its facilities at the same
    /// WORLD-YEARS within an honest band — completions are world-time
    /// state, not step artifacts.</summary>
    [Fact]
    public void FineTick_ProjectCompletions_LandOnWorldYears_NotSteps()
    {
        var artifact = ArtifactSerializer.ToText(Prologue());
        List<(ProjectKind Kind, int Year)> CompletionsAfter(
            int steps, int yearsPerEpoch)
        {
            using var reader = new StringReader(artifact);
            var s = ArtifactSerializer.Load(reader);
            int before = s.Projects.Count;
            ContinueFine(s, steps, yearsPerEpoch);
            var done = new List<(ProjectKind, int)>();
            for (int i = before; i < s.Projects.Count; i++)
                if (s.Projects[i].Completed)
                    done.Add((s.Projects[i].Kind,
                        s.Projects[i].StartedYear
                        + (int)System.Math.Ceiling(
                            s.Projects[i].YearsRequired)));
            return done;
        }
        var coarse = CompletionsAfter(steps: 2, yearsPerEpoch: 25);
        var fine = CompletionsAfter(steps: 50, yearsPerEpoch: 1);
        // both clocks complete work; the fine clock is never SLOWER in
        // world-time than the coarse by more than one coarse span
        Assert.NotEmpty(coarse);
        Assert.NotEmpty(fine);
        int coarseCount = coarse.Count, fineCount = fine.Count;
        Assert.InRange(fineCount, (int)(coarseCount * 0.5),
                       (int)System.Math.Ceiling(coarseCount * 2.0));
    }
```

(The band is deliberately loose — divergence in WHICH projects run is
expected (different perception cadence); divergence in whether construction
HAPPENS at all is the failure this test guards. Tighten only with evidence.)

- [ ] **Step 2: Conservation check on the suite's existing conservation
  tests** — find them (`grep -rn "conserv" tests/Core.Tests --include=*.cs -il`)
  and extend any world-total goods assertion to note projects consume from
  markets only (no new pool exists in Stage 1 — in-transit goods arrive with
  Stage 2). If a test sums facility BuildCost lumps at build events, it now
  sums project draws — rewrite to track market outflows.

- [ ] **Step 3: Full suite green, then re-freeze goldens once**

Run: `dotnet test StarSystemGeneration.sln`
Every failure must now be either a golden byte-diff or a test that encodes
the instant-completion hand-wave. Re-freeze goldens with the documented
regen procedure; rewrite the stragglers honestly. Run twice for determinism:

```bash
dotnet test StarSystemGeneration.sln && dotnet test StarSystemGeneration.sln
```

Expected: identical PASS both runs.

- [ ] **Step 4: Design-tree amendments** (the spec's list, one commit):
  simulation-flow's Allocation/Intent rows describe plan execution and the
  capability brief; controller-contract adds "standing plan (prioritized
  project schedule)" to polity policies; assets-and-investment §Construction
  rewrites to the project model (site at groundbreaking, per-year draws,
  starvation, ETA); infrastructure notes ConstructionYears is load-bearing
  and adds the port-upgrade basket; time.md adds "durations are world-time
  state — an action decided in Intent completes in year Y"; markets.md's
  construction pull paragraph; ships-and-fleets' yard section (batches);
  war.md's mobilization section (the ramp).

- [ ] **Step 5: HANDOFF rewrite + Stage 2 kickoff prompt** — HANDOFF's
  next-up points at the Stage 2 kickoff; the kickoff carries: located
  stockpiles replace `PolityRecord.ReserveQty` (every read/write site listed
  — grep them fresh), `Shipment` records + transit years over `LaneNetwork`,
  `MoveFreight` transit conversion, the requisition channel, per-end gate
  draws (the `// stage-2:` comment left in Task 9), located capability brief,
  and the flagged future passes (contract economy, front supply lines).

- [ ] **Step 6: Final commits, merge decision**

```bash
git add -A docs tests
git commit -m "docs(time): design tree amended to the project model; goldens re-frozen; stage-2 kickoff"
```

Present to the user: REPL eyeball (`eprojects`/`eplan`/`efreight`-less run,
throttle test: quarantine a lane feeding a construction site and watch the
ETA slide) and the merge decision. Merge to main locally only on their nod;
push only when they say to.

---

## Self-Review (run before handing off)

1. **Spec coverage:** §1 Project record → Tasks 1–4; §2 brief → Task 6;
   §3 plan/scheduler → Task 7; §4 Allocation passes → Tasks 4, 5, 7;
   §4 construction-pull knock-on → Task 11; §5 sweep: facilities (5), ports
   (7), gates (9), hulls (8), expeditions (9), mobilization (10), conquest
   transfer (12); §6 determinism/serialization (3), REPL (13), FineTick +
   goldens + docs (14). §4b located goods → Stage 2 plan (explicitly out).
2. **Placeholders:** the two bracketed "move this code" blocks (Task 5) are
   moves of code quoted in this plan's source references, not inventions;
   Task 12's capture-site grep is a locate-then-apply with the exact
   insertion given. No TBDs.
3. **Type consistency:** `ProjectKind`/`ProjectPriority`/`Project` fields as
   defined in Task 1 are used verbatim in Tasks 3–12;
   `ConstructionCandidate(TypeId, Hex, PortId, Score)` consistent across
   Tasks 5–7; `StandingPlan`/`PlanEntry` across Tasks 7–8; knob names
   consistent between Task 7/9/10 config blocks and their registry rows.
