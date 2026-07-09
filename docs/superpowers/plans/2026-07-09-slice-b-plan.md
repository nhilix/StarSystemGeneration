# Slice B — Two-Plane State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **This slice runs under the lighter protocol (/CLAUDE.md): inline execution in the slice session, task ledger committed as work proceeds, subagents only for the one fresh-eyes review.**

**Goal:** Replace the prototype's per-cell political state with the designed two-plane model — sparse hex-addressed registries (ports, lanes, facilities, fleets, segments) over a slimmed natural raster, expansion as port establishment, territory derived from the port registry, and a new layer-sectioned artifact format — deleting the prototype sim outright.

**Architecture:** All new state lives in `src/Core/Epoch/` (`StarGen.Core.Epoch`) inside `SimState`, which gains a reference to the `GalaxySkeleton` natural raster plus five sparse registries in fixed id order. Territory is never stored: `PortDomains` computes service areas, overlap, and polity territory on demand from the port registry. The colonization chain runs through the existing seven-phase frame: Perception surfaces candidates and treasuries → the new `GenesisController` emits `FoundColonyAct` in Intent → Resolution establishes tier-1 ports → Allocation accrues stub income, raises port tiers, and builds lanes → Interior enters polities (homeworld = first port) and grows population segments. `ArtifactSerializer` writes the new layer-sectioned, per-layer-versioned artifact stamping both configs.

**Tech Stack:** C# netstandard2.1 (`src/Core`), net8.0 REPL (`src/Inspector`), xUnit (`tests/Core.Tests`). `dotnet test StarSystemGeneration.sln`.

## Global Constraints

- **The design is the spec** (`docs/design/frame/space-and-travel.md`, `frame/actors.md`, `substrate/infrastructure.md`, `narrative/handoff.md`, `frame/system-map.md` §Artifact discipline). Deviations require amending the design doc in the same branch, flagged to the user.
- **Hex-tier (Phase-1 generation) suite never breaks**; Tier-1 density + seeding passes stay green (the passes survive until Slice F; their *political outputs* are deleted with the prototype).
- **Determinism discipline**: stateless hash rolls keyed (step, actor id, channel); fixed iteration order everywhere (actors/ports/lanes/segments by id, cells by spiral index); invariant-culture rendering (see culture-flip test); no timing/environment in any serialized or traced output.
- **RollChannel values are stable**: 37–39 retire with `StubGenesis` (never reuse); next free value is **40**.
- **Event type values are stable**, appended into 100-blocks: political 300s, economic 200s.
- All rates are **per world-year**; the epoch (25y default) is an integration step, never a unit.
- New files/folders under `src/Core` need Unity `.meta` files (copy a sibling `.meta`, replace the guid with a fresh one); deleted `src/Core` files take their `.meta` with them.
- `unity/ProjectSettings` churn stays uncommitted.
- REPL piping: `printf 'cmd\nquit\n' | dotnet run --project src/Inspector` from bash (PowerShell mangles the first stdin line).
- Branch: `slice-b-two-plane-state`. Task ledger: `docs/superpowers/plans/2026-07-09-slice-b-ledger.md`, updated and committed as tasks complete.
- No goods/markets (C/D), no fleets beyond record structs (E), perception stays perfect-info (I), emergence schedule stays a stub roll (F).

## File Structure

**Created (src/Core/Epoch/, each with a `.meta`):**
| File | Responsibility |
|---|---|
| `Port.cs` | `Port` entry + `PortDomains` (service radius, servicing, owners-at, contested, territory queries) |
| `Lane.cs` | `Lane` entry + `LaneMath` (inter-port range, capacity, transit speed — derived, never stored) |
| `Facility.cs` | `Facility` record shape + registry conventions (no siting execution — C/D) |
| `FleetRecord.cs` | Minimal mobile-asset record (E fills it) |
| `PopulationSegment.cs` | Port-administered, species-tagged population quantity |
| `PolityRecord.cs` | Polity-specific sim state: species id, expansion/development treasuries |
| `ColonyValuation.cs` | Candidate enumeration + terrain-potential scoring (price signal stubs in until D) |
| `EpochGenesis.cs` | Seeds polity actors from the seeding passes' homeworld anchors (replaces `StubGenesis`) |
| `ArtifactSerializer.cs` | New layer-sectioned artifact format, versioned per layer, both configs stamped |

**Created (src/Inspector/):** `EpochMapView.cs` — ASCII domain/lane map over cell glyphs.

**Modified:** `EpochSimConfig.cs` (new knob families), `WorldEvent.cs` (types 200/201/301 + payloads), `RollChannel.cs` (append 40), `Actor.cs` (`Policies` slot), `SimState.cs` (skeleton + registries), `Phases.cs` (Perception/Allocation/Intent/Resolution/Interior bodies), `ControllerContract.cs` (`PerceptionView` extension, `GenesisController`), `SimTraceView.cs` (registry summary + new payload lines), `SkeletonBuilder.cs` (no sim call; homeworld pass slims), `RegionCell.cs` (natural raster only), `RegionContext.cs` (political fields out), `GalaxySkeleton.cs` (Polities/Wars/Events out), `GalaxyConfig.cs` (prototype sim knobs out), `Repl.cs`, `GalaxyMapView.cs` (political layers out), `StatsReport.cs` caller only.

**Deleted (with their `.meta`s and their tests):** `src/Core/Galaxy/EpochSim.cs`, `Sim/ActionPhase.cs`, `Sim/AllocationPhase.cs`, `Sim/Economy.cs`, `Sim/IncomePhase.cs`, `Sim/ResolutionPhase.cs`, `Polity.cs`, `War.cs`, `GalaxyEvent.cs`, `SkeletonSerializer.cs`, `src/Core/Epoch/StubGenesis.cs`; `src/Inspector/EconomyReport.cs`, `src/Inspector/ChronicleView.cs`; tests `Galaxy/{EpochSimTests, ActionPhaseTests, AllocationPhaseTests, IncomePhaseTests, ResolutionPhaseTests, EconomyTests, EconomyInvariantTests, FlowRoutingTests, SerializerTests}.cs`, `Epoch/StubGenesisTests.cs`.

**Kept deliberately:** `SpeciesProfile`, `Anchor` (incl. `Homeworld` type), all seeding passes, `GalaxyContext`, hex-tier generation, `TrivialController` (test/default AI for non-polity actors).

## Design decisions locked here (flag to user at plan review)

1. **"Records as structs"** from the kickoff is implemented as small sealed classes with mutable fields (matching `Actor`'s style) — ports/segments mutate (tier, size) and live in id-ordered `List<T>` registries; value-type semantics would force replace-on-mutate for no benefit.
2. **Void dilution of service range** is implemented as: hexes whose cell is void are never serviced (wilds stay dark). The design's "voids dilute effective range" full pathfinding form can deepen later without schema change.
3. **Stub income** (`StubIncomePerPortPerYear`): with no goods until C/D, Allocation needs an income source for investment. A flat per-port world-year rate, split by the standing budget weights, is the honest degenerate form Markets replaces in D. Unspent budget shares (military/research/…) evaporate until their slices land.
4. **Per-hex generation stops reading political state** (`RegionContext` slims; `SettlementScale` mechanism kept but returns natural 1.0). The design's "development = proximity-to-port" wiring into the hex tier arrives when the sim output feeds generation (post-B; atlas/handoff slices). Sequencing, not deviation.
5. **Standing policies stored on the actor** (`Actor.Policies`, written during Intent, read by the *next* step's Allocation) — implements "applied mechanically by other phases on subsequent steps" even though `PolityPolicies.Default` is the only value until D.
6. **Homeworld ports found at tier 2** at emergence (a civilization at spaceflight is past "outpost"); colony ports at tier 1.
7. **Colony founding resolves convoyless and same-step** (kickoff: journey stub; convoys arrive in E). Act collisions resolve in actor-id order; losers' treasuries are not charged.
8. **Artifact serializes state, not transients**: perception views, staged events, decisions, and the phase trace are rebuilt/re-renderable; controllers are reattached on load (`GenesisController` for polities). Policies aren't serialized while `Default` is the only value — D bumps the actors layer version when they become real state.

---

### Task 0: Branch and ledger

**Files:**
- Create: `docs/superpowers/plans/2026-07-09-slice-b-ledger.md`

- [ ] **Step 1: Branch**

```bash
git checkout -b slice-b-two-plane-state
```

- [ ] **Step 2: Write the ledger** — ordered checklist mirroring Tasks 0–12 of this plan, each with its gate, statuses unchecked. Header notes: this is the resumability record; update + commit as tasks complete.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/plans/2026-07-09-slice-b-ledger.md
git commit -m "docs: slice B ledger - two-plane state task checklist"
```

---

### Task 1: Config knobs, roll channel, event types

**Files:**
- Modify: `src/Core/Epoch/EpochSimConfig.cs`
- Modify: `src/Core/Rng/RollChannel.cs`
- Modify: `src/Core/Epoch/WorldEvent.cs`
- Test: `tests/Core.Tests/Epoch/EpochSimConfigTests.cs`, `tests/Core.Tests/Epoch/EventLogTests.cs`

**Interfaces:**
- Produces: `EpochSimConfig.Infrastructure` (`InfrastructureKnobs`) and `.Expansion` (`ExpansionKnobs`); `RollChannel.EpochEntrySchedule = 40`; `WorldEventType.{LaneOpened=200, PortTierRaised=201, PortEstablished=301}`; payload records `PortEstablishedPayload(string PolityName, int PortId)`, `LaneOpenedPayload(int PortAId, int PortBId)`, `PortTierRaisedPayload(int PortId, int NewTier)`.

- [ ] **Step 1: Failing tests** — extend `EpochSimConfigTests` (defaults sane, all rates world-year-denominated) and `EventLogTests` (family mapping of the three new types):

```csharp
[Fact]
public void InfrastructureAndExpansionKnobs_HaveSaneDefaults()
{
    var c = new EpochSimConfig();
    Assert.True(c.Infrastructure.ServiceRadiusBaseHexes >= 1);
    Assert.True(c.Infrastructure.MaxPortTier == 3);
    Assert.True(c.Expansion.StubIncomePerPortPerYear > 0);
    Assert.True(c.Expansion.ColonyCost > 0);
    Assert.True(c.Expansion.SegmentGrowthPerYear > 0 && c.Expansion.SegmentGrowthPerYear < 0.1);
}

[Theory]
[InlineData(WorldEventType.LaneOpened, EventFamily.Economic)]
[InlineData(WorldEventType.PortTierRaised, EventFamily.Economic)]
[InlineData(WorldEventType.PortEstablished, EventFamily.Political)]
public void NewEventTypes_MapToStableFamilyBlocks(WorldEventType t, EventFamily f) =>
    Assert.Equal(f, WorldEventTypes.FamilyOf(t));
```

- [ ] **Step 2: Run to verify failure** — `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~EpochSimConfigTests|FullyQualifiedName~EventLogTests"` → FAIL (missing members).

- [ ] **Step 3: Implement.** `EpochSimConfig` gains two families (and `GenesisKnobs` will slim in Task 4 — untouched here):

```csharp
/// <summary>Port/lane physical knobs (space-and-travel.md). Radii and ranges
/// in hexes; both growth axes are per-tier steps.</summary>
public sealed class InfrastructureKnobs
{
    /// <summary>Local service radius of a tier-1 port, in hexes.</summary>
    public int ServiceRadiusBaseHexes { get; set; } = 4;
    /// <summary>Additional service radius per tier above 1.</summary>
    public int ServiceRadiusPerTierHexes { get; set; } = 4;
    /// <summary>Inter-port (lane) reach of a tier-1 port, in hexes.</summary>
    public int InterPortRangeBaseHexes { get; set; } = 18;
    /// <summary>Additional inter-port reach per tier above 1.</summary>
    public int InterPortRangePerTierHexes { get; set; } = 8;
    public int MaxPortTier { get; set; } = 3;
    /// <summary>Homeworld ports establish at this tier at emergence.</summary>
    public int HomeworldPortTier { get; set; } = 2;
}

/// <summary>Expansion/colonization dials, per world-year where a rate.
/// StubIncome is the pre-market income placeholder Markets (D) replaces.</summary>
public sealed class ExpansionKnobs
{
    public double StubIncomePerPortPerYear { get; set; } = 1.0;
    /// <summary>Expansion points consumed by one colony founding.</summary>
    public double ColonyCost { get; set; } = 15.0;
    /// <summary>Off-lane colonization reach from any owned port, in hexes.</summary>
    public int ColonizationReachHexes { get; set; } = 24;
    /// <summary>Development points to raise a port: cost = base × current tier.</summary>
    public double PortUpgradeCostBase { get; set; } = 40.0;
    /// <summary>Development points per lane built.</summary>
    public double LaneCost { get; set; } = 25.0;
    public double HomeworldSegmentSize { get; set; } = 3.0;
    public double ColonySegmentSize { get; set; } = 0.5;
    /// <summary>Logistic population growth rate per world-year toward the port-tier cap.</summary>
    public double SegmentGrowthPerYear { get; set; } = 0.01;
    /// <summary>Segment size cap = port tier × this.</summary>
    public double SegmentCapPerTier { get; set; } = 2.0;
}
```
with `public InfrastructureKnobs Infrastructure { get; } = new InfrastructureKnobs();` and `public ExpansionKnobs Expansion { get; } = new ExpansionKnobs();` on `EpochSimConfig`. `RollChannel` appends (comment block "Slice B"): `EpochEntrySchedule = 40,  // stub emergence schedule until F: actor = polity id`. `WorldEventType` appends the three values; three payload records beside `PolityEmergedPayload`.

- [ ] **Step 4: Run to green**, full solution: `dotnet test StarSystemGeneration.sln` → all pass.
- [ ] **Step 5: Commit** — `feat(epoch): slice B knob families, roll channel 40, port/lane event types`

---

### Task 2: Registry entry types

**Files:**
- Create: `src/Core/Epoch/Port.cs` (+`.meta`), `Lane.cs`, `Facility.cs`, `FleetRecord.cs`, `PopulationSegment.cs`, `PolityRecord.cs` (+`.meta`s) — **entry types only**; `PortDomains`/`LaneMath` land in Task 3 in the same files.

**Interfaces:**
- Produces (exact shapes; all registries are id-ordered `List<T>` on `SimState` from Task 4):

```csharp
public sealed class Port
{
    public int Id { get; }
    public int OwnerActorId { get; set; }          // conquest transfers later; set-able now
    public HexCoordinate Hex { get; }              // the physical carrier (P4)
    public int Tier { get; set; }                  // 1..MaxPortTier
    public int FoundedYear { get; }
    public Port(int id, int ownerActorId, HexCoordinate hex, int tier, int foundedYear) { ... }
}
public sealed class Lane
{
    public int Id { get; }
    public int PortAId { get; }                    // invariant: PortAId < PortBId
    public int PortBId { get; }
    public int BuiltYear { get; }
    public Lane(int id, int portAId, int portBId, int builtYear)  // throws if portAId >= portBId
}
public sealed class Facility
{
    public int Id { get; }
    public int TypeId { get; }                     // Slice C catalog id; opaque here
    public int Tier { get; set; }
    public HexCoordinate Hex { get; }
    public int OwnerActorId { get; set; }
    public double Condition { get; set; } = 1.0;
    public int BuiltYear { get; }
}
public sealed class FleetRecord
{
    public int Id { get; }
    public int OwnerActorId { get; set; }
    public HexCoordinate Hex { get; set; }         // fleets move; Slice E fills the rest
}
public sealed class PopulationSegment
{
    public int Id { get; }
    public int PortId { get; }                     // administered per port domain (actors.md)
    public int SpeciesId { get; }
    public double Size { get; set; }
}
public sealed class PolityRecord
{
    public int ActorId { get; }
    public int SpeciesId { get; }
    public double ExpansionPoints { get; set; }
    public double DevelopmentPoints { get; set; }
}
```

- [ ] **Step 1: Write the types** with full constructors and doc comments citing their design doc (`space-and-travel.md` for Port/Lane, `infrastructure.md` for Facility, `actors.md` for FleetRecord/PopulationSegment). `Lane` constructor throws `ArgumentException` when `portAId >= portBId`.
- [ ] **Step 2: One structural test** (`tests/Core.Tests/Epoch/RegistryTypeTests.cs`): lane ordering invariant throws; port construction round-trips fields.

```csharp
[Fact]
public void Lane_RequiresOrderedPortIds()
{
    Assert.Throws<ArgumentException>(() => new Lane(0, 2, 1, 0));
    var lane = new Lane(0, 1, 2, 100);
    Assert.Equal((1, 2), (lane.PortAId, lane.PortBId));
}
```
- [ ] **Step 3: `.meta` files** for each new file: copy `src/Core/Epoch/Actor.cs.meta`, replace guid with output of `powershell -Command "[guid]::NewGuid().ToString('N')"` per file.
- [ ] **Step 4: Run** `dotnet test StarSystemGeneration.sln` → green.
- [ ] **Step 5: Commit** — `feat(epoch): sparse registry entry types (port, lane, facility, fleet, segment, polity record)`

---

### Task 3: Derived geography — PortDomains and LaneMath

**Files:**
- Modify: `src/Core/Epoch/Port.cs` (add `PortDomains`), `src/Core/Epoch/Lane.cs` (add `LaneMath`)
- Test: `tests/Core.Tests/Epoch/PortDomainTests.cs`

**Interfaces:**
- Consumes: `GalaxySkeleton.TryGetCell`, `HexGrid.{Distance, CellOf}`, Task 1 knobs, Task 2 types.
- Produces:

```csharp
public static class PortDomains
{
    public static int ServiceRadius(EpochSimConfig cfg, int tier);      // base + per×(tier-1)
    /// <summary>True iff the port's service area covers the hex: within radius
    /// AND the hex's cell exists and is not void (wilds stay dark).</summary>
    public static bool Services(GalaxySkeleton sk, EpochSimConfig cfg, Port port, HexCoordinate hex);
    /// <summary>Distinct owner actor ids whose ports service the hex, ascending.
    /// Count 0 = wilds; 1 = territory; ≥2 = contested-influence zone.</summary>
    public static void OwnersAt(GalaxySkeleton sk, EpochSimConfig cfg,
                                IReadOnlyList<Port> ports, HexCoordinate hex, List<int> into);
    public static bool IsContested(GalaxySkeleton sk, EpochSimConfig cfg,
                                   IReadOnlyList<Port> ports, HexCoordinate hex);
}
public static class LaneMath
{
    public static int InterPortRange(EpochSimConfig cfg, int tier);     // base + per×(tier-1)
    /// <summary>Both ends must reach: pairable iff Distance ≤ min of the two ranges.</summary>
    public static bool InRange(EpochSimConfig cfg, Port a, Port b);
    public static double Capacity(Port a, Port b);                     // (a.Tier + b.Tier) × 0.5
    public static double TransitSpeed(Port a, Port b);                 // 1.0 + 0.5 × min(tier)
}
```
(`OwnersAt` fills a caller-owned list — no per-call allocation in hot rendering loops; it clears the list first.)

- [ ] **Step 1: Failing tests** covering: radius formula per tier; a hex inside/outside radius; a void-cell hex never serviced; owners ascending and distinct across two same-owner ports; contested requires two *distinct* owners; `InRange` uses the min of both ranges; capacity/speed formulas. Use a small real skeleton for void behavior:

```csharp
private static GalaxySkeleton SmallSkeleton(ulong seed = 7, int radius = 6)
    => SkeletonBuilder.BuildShape(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });

[Fact]
public void Services_InsideRadius_NotVoid()
{
    var sk = SmallSkeleton();
    var cfg = new EpochSimConfig();
    var nonVoid = sk.Cells.First(c => !c.IsVoid);
    var port = new Port(0, 0, HexGrid.CellCenter(nonVoid.Coord), tier: 1, foundedYear: 0);
    Assert.True(PortDomains.Services(sk, cfg, port, port.Hex));
    Assert.False(PortDomains.Services(sk, cfg, port,
        new HexCoordinate(port.Hex.Q + PortDomains.ServiceRadius(cfg, 1) + 1, port.Hex.R)));
}

[Fact]
public void VoidCells_AreNeverServiced()
{
    var sk = SmallSkeleton();
    var cfg = new EpochSimConfig();
    var voidCell = sk.Cells.FirstOrDefault(c => c.IsVoid);
    if (voidCell == null) return;                       // seed 7 radius 6 has voids; guard anyway
    var port = new Port(0, 0, HexGrid.CellCenter(voidCell.Coord), tier: 3, foundedYear: 0);
    Assert.False(PortDomains.Services(sk, cfg, port, port.Hex));
}

[Fact]
public void OwnersAt_DistinctAscending_AndContested()
{
    var sk = SmallSkeleton();
    var cfg = new EpochSimConfig();
    var cell = sk.Cells.First(c => !c.IsVoid);
    var hex = HexGrid.CellCenter(cell.Coord);
    var ports = new List<Port> {
        new Port(0, 5, hex, 2, 0), new Port(1, 5, hex, 1, 0), new Port(2, 3, hex, 1, 0) };
    var owners = new List<int>();
    PortDomains.OwnersAt(sk, cfg, ports, hex, owners);
    Assert.Equal(new[] { 3, 5 }, owners);
    Assert.True(PortDomains.IsContested(sk, cfg, ports, hex));
}
```
- [ ] **Step 2: Verify failure** — `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~PortDomainTests"` → FAIL.
- [ ] **Step 3: Implement** exactly per the interface block. `Services`: `HexGrid.Distance(port.Hex, hex) <= ServiceRadius(cfg, port.Tier) && sk.TryGetCell(HexGrid.CellOf(hex), out var cell) && !cell.IsVoid`. `OwnersAt`: clear, scan ports in list order, insert-sorted distinct.
- [ ] **Step 4: Green** on the filter, then full solution.
- [ ] **Step 5: Commit** — `feat(epoch): derived port domains + lane math (territory computed, never stored)`

---

### Task 4: EpochGenesis over the seeding passes' homeworld anchors

Replaces `StubGenesis`; homeworld = first port, founded at entry in Interior.

**Files:**
- Create: `src/Core/Epoch/EpochGenesis.cs` (+`.meta`)
- Delete: `src/Core/Epoch/StubGenesis.cs` (+`.meta`)
- Modify: `src/Core/Epoch/SimState.cs`, `src/Core/Epoch/Actor.cs`, `src/Core/Epoch/Phases.cs` (Interior), `src/Core/Epoch/EpochSimConfig.cs` (slim `GenesisKnobs`), `src/Core/Galaxy/SkeletonBuilder.cs` (add `BuildNatural`), `src/Inspector/Repl.cs` (`epoch` command)
- Test: create `tests/Core.Tests/Epoch/EpochGenesisTests.cs`; delete `Epoch/StubGenesisTests.cs`; update `Epoch/{EpochEngineTests, DeterminismTests, ControllerContractTests, EpochSimConfigTests}.cs`

**Interfaces:**
- Consumes: homeworld anchors placed by `SkeletonBuilder.PassHomeworlds` (`AnchorType.Homeworld`, `SpeciesId`), `skeleton.Species` names, `RollChannel.EpochEntrySchedule`.
- Produces:

```csharp
// SkeletonBuilder — the natural-raster pipeline without the prototype sim.
// Task 5 deletes the old Build (which runs EpochSim) and renames this to Build.
public static GalaxySkeleton BuildNatural(GalaxyConfig config)   // shape + stellar + anchors + homeworlds

public static class EpochGenesis
{
    /// <summary>One polity actor per homeworld anchor, in cell spiral order:
    /// id = ordinal, name = species name, seat = anchor hex, entry epoch rolled
    /// on EpochEntrySchedule within Genesis.EmergenceWindowYears (stub until F),
    /// controller = GenesisController. Also creates the PolityRecord.</summary>
    public static SimState Seed(GalaxySkeleton skeleton, EpochSimConfig config);
}

// SimState additions (constructor becomes SimState(EpochSimConfig, GalaxySkeleton)):
public GalaxySkeleton Skeleton { get; }
public List<Port> Ports { get; } = new List<Port>();
public List<Lane> Lanes { get; } = new List<Lane>();
public List<Facility> Facilities { get; } = new List<Facility>();
public List<FleetRecord> Fleets { get; } = new List<FleetRecord>();
public List<PopulationSegment> Segments { get; } = new List<PopulationSegment>();
public List<PolityRecord> Polities { get; } = new List<PolityRecord>();  // actor-id order
public PolityRecord PolityOf(int actorId);                               // indexed lookup

// Actor addition:
public PolicySet? Policies { get; set; }   // standing policies, written each Intent
```
`GenesisKnobs` slims to `EmergenceWindowYears` only (`StubPolityCount`, `StubSeatRadiusHexes` die with StubGenesis). Until Task 7, `EpochGenesis` assigns `TrivialController` — the swap to `GenesisController` happens in Task 7.

- [ ] **Step 1: Failing tests** (`EpochGenesisTests`):

```csharp
private static (GalaxySkeleton sk, SimState state) Seeded(ulong seed = 42, int radius = 8)
{
    var gc = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius };
    var sk = SkeletonBuilder.BuildNatural(gc);
    var ec = new EpochSimConfig { MasterSeed = seed };
    return (sk, EpochGenesis.Seed(sk, ec));
}

[Fact]
public void SeedsOnePolityPerHomeworldAnchor_AtAnchorHexes()
{
    var (sk, state) = Seeded();
    var anchors = sk.Cells.SelectMany(c => c.Anchors)
                          .Where(a => a.Type == AnchorType.Homeworld).ToList();
    Assert.Equal(anchors.Count, state.Actors.Count);
    Assert.All(state.Actors, a => Assert.Equal(ActorKind.Polity, a.Kind));
    Assert.Equal(anchors.Select(a => a.Hex), state.Actors.Select(a => a.Seat));
    Assert.Equal(state.Actors.Count, state.Polities.Count);
}

[Fact]
public void EntryEpochs_Staggered_Deterministic()
{
    var (_, s1) = Seeded(); var (_, s2) = Seeded();
    Assert.Equal(s1.Actors.Select(a => a.EntryEpoch), s2.Actors.Select(a => a.EntryEpoch));
    int window = 500 / 25;
    Assert.All(s1.Actors, a => Assert.InRange(a.EntryEpoch, 0, window));
}

[Fact]
public void Entry_FoundsHomeworldPortAndSegment()
{
    var (_, state) = Seeded();
    new EpochEngine().Run(state);
    Assert.All(state.Actors, a => Assert.True(a.Entered));
    // every polity's first port is its seat at HomeworldPortTier, with a segment
    foreach (var a in state.Actors)
    {
        var home = state.Ports.First(p => p.OwnerActorId == a.Id);
        Assert.Equal(a.Seat, home.Hex);
        Assert.Equal(state.Config.Infrastructure.HomeworldPortTier, home.Tier);
        Assert.Contains(state.Segments, s => s.PortId == home.Id);
    }
}
```
- [ ] **Step 2: Verify failure** (missing `BuildNatural`, `EpochGenesis`, ctor).
- [ ] **Step 3: Implement.**
  - `SkeletonBuilder.BuildNatural`: `BuildShape` + `PassStellarPopulation` + `PassResourceAnchors` + `PassHomeworlds` (no `EpochSim.Run`). Old `Build` untouched until Task 5.
  - `SimState` ctor `(EpochSimConfig config, GalaxySkeleton skeleton)`; registries as above.
  - `EpochGenesis.Seed`: iterate `skeleton.Cells` (spiral order) → homeworld anchors → for ordinal `id`: entry roll `EpochRolls.NextInt(config.MasterSeed, RollChannel.EpochEntrySchedule, step: 0, actorId: id, 0, windowEpochs + 1)` with `windowEpochs = Math.Max(1, config.Genesis.EmergenceWindowYears / config.Sim.YearsPerEpoch)`; actor named `skeleton.Species[anchor.SpeciesId].Name`; `state.Polities.Add(new PolityRecord(id, anchor.SpeciesId))`.
  - `InteriorPhase` entry block additionally founds the homeworld port + segment before staging `PolityEmerged`:

```csharp
var port = new Port(state.Ports.Count, a.Id, a.Seat,
                    state.Config.Infrastructure.HomeworldPortTier, state.WorldYear);
state.Ports.Add(port);
state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
    state.PolityOf(a.Id).SpeciesId, state.Config.Expansion.HomeworldSegmentSize));
```
  - Update `EpochEngineTests`/`DeterminismTests`/`ControllerContractTests` to seed via the `Seeded` helper pattern (share it: `tests/Core.Tests/Epoch/EpochTestKit.cs`, static class with `Seeded`). Delete `StubGenesisTests.cs`. Update `Repl.cs` `epoch` case: `BuildNatural` a default-radius (arg 3 optional) skeleton with the same seed, then `EpochGenesis.Seed`.
- [ ] **Step 4: Green** — full solution (hex-tier untouched).
- [ ] **Step 5: Delete `StubGenesis.cs` + `.meta`; mark channels 37–39 comment "retired (slice B)"** in `RollChannel.cs`. Re-run full solution.
- [ ] **Step 6: Commit** — `feat(epoch): EpochGenesis seeds polities from homeworld anchors; homeworld = first port (retires StubGenesis, channels 37-39)`

---

### Task 5: Delete the prototype sim; slim the natural raster

The replacement state model exists (Tasks 2–4); the prototype goes now, whole.

**Files:**
- Delete (each with `.meta`): `src/Core/Galaxy/EpochSim.cs`, `src/Core/Galaxy/Sim/` (all five files + folder meta), `Polity.cs`, `War.cs`, `GalaxyEvent.cs`, `SkeletonSerializer.cs`
- Delete: `src/Inspector/EconomyReport.cs`, `src/Inspector/ChronicleView.cs`
- Delete tests: `Galaxy/{EpochSimTests, ActionPhaseTests, AllocationPhaseTests, IncomePhaseTests, ResolutionPhaseTests, EconomyTests, EconomyInvariantTests, FlowRoutingTests, SerializerTests}.cs`
- Modify: `src/Core/Galaxy/RegionCell.cs`, `RegionContext.cs`, `GalaxySkeleton.cs`, `GalaxyConfig.cs`, `SkeletonBuilder.cs`, `src/Inspector/Repl.cs`, `src/Inspector/GalaxyMapView.cs`
- Modify tests: `Galaxy/{SkeletonModelTests, SeedingPassTests, RegionIntegrationTests}.cs`

**Interfaces:**
- Produces the slimmed shapes every later task builds against:

```csharp
// RegionCell — the natural raster only (space-and-travel.md: the lattice
// carries no political meaning). Q, R, Coord, SpiralIndex, MeanDensity,
// IsVoid, IsChokepoint, Lean, Metallicity, Anchors — nothing else.

// GalaxySkeleton: Config, Cells, Species, CellAt/TryGetCell/CellForHex.
// Polities/Events/Wars/AtWar deleted. SchemaVersion const deleted
// (versioning moves into the artifact layers, Task 10).

// SkeletonBuilder.Build == BuildNatural (rename; BuildNatural alias removed).

// GalaxyConfig loses: EpochCount, YearsPerEpoch, WarWearinessRate,
// StockpileDecayRate, TechThresholdBase, TradeIncomeWeight, ProvisionsPerPop.

// RegionContext loses OwnerPolityId/WarScarred; SettlementScale returns 1.0
// (mechanism + BodyGenerator parameter kept for the post-B port wiring).

// PassHomeworlds: still places homeworld anchors + species profiles with
// identical rolls (channel discipline: HomeworldPlacement/SpeciesEmbodiment/…
// unchanged); no Polity objects, no cell political writes. Spacing/target
// logic now tracks placed homeworld cells in a local list.
```

- [ ] **Step 1: Delete the files** listed above (with `.meta`s under `src/Core`), including the `Sim/` folder meta.
- [ ] **Step 2: Slim the survivors** per the interface block. `PassHomeworlds` keeps its exact roll sequence (candidate ordering, `RollSpecies`, `SpeciesName`, `PickAnchorHex(..., 2)`) so anchor placement is byte-stable against Slice A-era seeds; only the `Polity`-object creation and the four cell political writes disappear, with `var placed = new List<RegionCell>()` replacing `s.Polities` for target/spacing checks (`HexGrid.Distance(placedCell.Coord, cell.Coord) < minSpacing`).
- [ ] **Step 3: Inspector fallout.** `Repl.cs`: `galaxy` summary line drops polity/event/war/claim counts (keep cells/chokepoints/ms); `cell` drops owner/dev/population/value/events block (keeps density/void/chokepoint/lean/metallicity/anchors + zoom); delete `polity` and `chronicle` cases (rebuilt on the sim in Task 11) and `gsave`/`gload` (replaced in Task 10); `stats` drops the `EconomyReport` line; `help` text updated. `GalaxyMapView`: layers slim to `density` (default) + `lean`; delete `polity/zone/dev/trade/economy/war` layer arms, `EconomyChar`, and their legends.
- [ ] **Step 4: Test fallout.** Delete the nine prototype test files. `SkeletonModelTests`: drop political-default asserts. `SeedingPassTests`: homeworld asserts move to anchors/species (count target, min spacing between homeworld-anchor cells, species/embodiment fit — keep those; drop `OwnerPolityId`/capital asserts). `RegionIntegrationTests`: drop `SettlementScale_RaisesSettlementInsidePolities`; natural-modifier tests stay.
- [ ] **Step 5: Full run** — `dotnet test StarSystemGeneration.sln` → green; hex-tier suite (Generation/*, Rng/*, Galaxy natural tests) intact. Build the Inspector: `dotnet build src/Inspector` → clean.
- [ ] **Step 6: Commit** — `feat!: delete prototype sim (EpochSim, Sim/*, Polity, War, GalaxyEvent, v5 serializer); RegionCell slims to the natural raster`

---

### Task 6: Allocation accrues stub income into polity treasuries

**Files:**
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase`, `IntentPhase`)
- Test: `tests/Core.Tests/Epoch/AllocationTests.cs` (new)

**Interfaces:**
- Consumes: `PolityRecord.{ExpansionPoints, DevelopmentPoints}`, `Actor.Policies`.
- Produces: per entered polity per epoch: `income = ownedPorts × StubIncomePerPortPerYear × YearsPerEpoch`; `ExpansionPoints += income × Budget.Expansion`; `DevelopmentPoints += income × Budget.Development` (weights from `actor.Policies as PolityPolicies ?? PolityPolicies.Default`). `IntentPhase` stores `decision.Policies` into `actor.Policies` after `Decide`. Trace note: `"income accrued for N polities"` (counts only — no doubles in trace).

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public void Allocation_AccruesBudgetSharesFromPortIncome()
{
    var (_, state) = EpochTestKit.Seeded();
    var engine = new EpochEngine();
    engine.Step(state);                       // entries found homeworld ports (Interior)
    var entered = state.Actors.Where(a => a.Entered).ToList();
    if (entered.Count == 0) { engine.Step(state); entered = state.Actors.Where(a => a.Entered).ToList(); }
    double before = state.PolityOf(entered[0].Id).ExpansionPoints;
    engine.Step(state);                       // next Allocation sees the port
    double expected = 1 /*port*/ * state.Config.Expansion.StubIncomePerPortPerYear
                      * state.Config.Sim.YearsPerEpoch
                      * PolityPolicies.Default.Budget.Expansion;
    Assert.Equal(before + expected, state.PolityOf(entered[0].Id).ExpansionPoints, 10);
}
```
- [ ] **Step 2: Verify failure**, **Step 3: implement** (ports counted by scan in id order; polities iterated in `state.Polities` order), **Step 4: green full solution**.
- [ ] **Step 5: Commit** — `feat(epoch): allocation applies standing budget weights to stub port income (markets replace in D)`

---

### Task 7: The expansion chain — valuation, perception, controller, resolver

**Files:**
- Create: `src/Core/Epoch/ColonyValuation.cs` (+`.meta`)
- Modify: `src/Core/Epoch/ControllerContract.cs` (`PerceptionView` + `GenesisController`), `Phases.cs` (`PerceptionPhase`, `ResolutionPhase`), `EpochGenesis.cs` (controller swap)
- Test: `tests/Core.Tests/Epoch/ExpansionTests.cs` (new); update `ControllerContractTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record ColonyCandidate(HexCoordinate Target, double Score);

public static class ColonyValuation
{
    /// <summary>Colony targets for a polity: non-void cells within
    /// ColonizationReachHexes of any owned port, no port of ours in the cell,
    /// target hex = first unported anchor hex in the cell, else the cell
    /// center (skip if occupied). Score = MeanDensity + 0.3×Metallicity
    /// + 0.4 if the cell has a non-homeworld anchor − 0.3 if another polity
    /// services the target (contested-influence friction). Price signals
    /// join the formula in Slice D. Deterministic: ordered by score desc,
    /// then cell spiral index; top `max`.</summary>
    public static IReadOnlyList<ColonyCandidate> CandidatesFor(SimState state, int polityId, int max = 8);
}

// PerceptionView additions (perfect-info until I; ctor grows accordingly):
public double ExpansionPoints { get; }
public IReadOnlyList<ColonyCandidate> ColonyCandidates { get; }

/// <summary>Genesis expansion AI: default standing policies; founds toward the
/// top candidate whenever the expansion treasury affords it. Constructed with
/// the config (its own policy costs — not world state; P2 intact).</summary>
public sealed class GenesisController : IController
{
    public GenesisController(EpochSimConfig config);
    public ControllerDecision Decide(PerceptionView perceived);   // ≤1 FoundColonyAct
}
```
- `PerceptionPhase` computes candidates + treasury into each entered polity's view (empty candidates for port-less polities). `ResolutionPhase` resolves `FoundColonyAct`s in decision (actor-id) order: skip unless actor entered ∧ target hex has no port ∧ target within reach of an owned port ∧ cell non-void ∧ `ExpansionPoints ≥ ColonyCost` (all against truth); on success: deduct, `Ports.Add(new Port(next, actor, target, 1, WorldYear))`, `Segments.Add(... ColonySegmentSize, polity species ...)`, stage `PortEstablished` (Generational, actors=[polity], location=target, magnitude 1.0, valence 1.0, Public, `PortEstablishedPayload(actorName, portId)`). Collisions: first actor id wins; losers uncharged. Trace: `"N acts, M ports established"`.
- `EpochGenesis` now assigns `new GenesisController(config)` to polity actors.

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void FullRun_EstablishesColonyPortsBeyondHomeworlds()
{
    var (_, state) = EpochTestKit.Seeded();          // 40 epochs default
    new EpochEngine().Run(state);
    int homeworlds = state.Actors.Count;
    Assert.True(state.Ports.Count > homeworlds,
        $"expected colonies beyond {homeworlds} homeworld ports, got {state.Ports.Count}");
    Assert.Contains(state.Log.Events, e => e.Type == WorldEventType.PortEstablished);
}

[Fact]
public void ColonyPorts_AreTier1_WithinReach_OnePortPerHex()
{
    var (_, state) = EpochTestKit.Seeded();
    new EpochEngine().Run(state);
    var cfg = state.Config;
    Assert.Equal(state.Ports.Count, state.Ports.Select(p => p.Hex).Distinct().Count());
    foreach (var e in state.Log.Events.Where(e => e.Type == WorldEventType.PortEstablished))
    {
        var port = state.Ports.First(p => p.Hex.Equals(e.Location));
        Assert.Equal(1, ((PortEstablishedPayload)e.Payload!).PortId >= 0 ? port.Tier : 0);
        Assert.Contains(state.Ports, o => o.OwnerActorId == port.OwnerActorId && o.Id != port.Id
            && HexGrid.Distance(o.Hex, port.Hex) <= cfg.Expansion.ColonizationReachHexes);
    }
}

[Fact]
public void Candidates_DeterministicAndReachBound()
{
    var (_, s1) = EpochTestKit.Seeded(); var (_, s2) = EpochTestKit.Seeded();
    new EpochEngine().Step(s1); new EpochEngine().Step(s2);
    foreach (var a in s1.Actors.Where(a => a.Entered))
        Assert.Equal(ColonyValuation.CandidatesFor(s1, a.Id).Select(c => (c.Target, c.Score)),
                     ColonyValuation.CandidatesFor(s2, a.Id).Select(c => (c.Target, c.Score)));
}
```
Also update `ControllerContractTests` for the grown `PerceptionView` ctor; add: `GenesisController` with affordable treasury + one candidate → exactly one `FoundColonyAct` at the top candidate; without → no acts.
- [ ] **Step 2: Verify failure. Step 3: Implement** per interface block. Candidate scan iterates `state.Skeleton.Cells` in spiral order; reach test scans the polity's ports. **Step 4: Green full solution.**
- [ ] **Step 5: Commit** — `feat(epoch): expansion = port establishment - colony valuation, genesis controller, FoundColonyAct resolver`

---

### Task 8: Port tier growth and lane building in Allocation

**Files:**
- Modify: `src/Core/Epoch/Phases.cs` (`AllocationPhase`)
- Test: `tests/Core.Tests/Epoch/AllocationTests.cs` (extend)

**Interfaces:**
- Consumes: `PolityRecord.DevelopmentPoints`, `LaneMath`, Task 1 events.
- Produces, per entered polity after income accrual, in this order (lanes first — the network precedes tall towers):
  1. **Lanes**: candidate pairs = own ports `(a,b), a.Id < b.Id`, not already in `state.Lanes`, `LaneMath.InRange`; ordered by `(HexGrid.Distance, a.Id, b.Id)`; while `DevelopmentPoints ≥ LaneCost`: build `Lane(next, a.Id, b.Id, WorldYear)`, deduct, stage `LaneOpened` (Economic, actors=[polity], location = midpoint hex of `a.Hex`/`b.Hex` via hex-lerp round, magnitude 1.0, valence 1.0, Regional, `LaneOpenedPayload(a.Id, b.Id)`).
  2. **Tier raises**: while an own port has `Tier < MaxPortTier` and `DevelopmentPoints ≥ PortUpgradeCostBase × Tier`: raise the lowest-tier (tie: lowest id) port, deduct, stage `PortTierRaised` (Economic, location = port hex, Regional, `PortTierRaisedPayload(portId, newTier)`).
  - Trace: `"income accrued for N polities, L lanes built, R ports raised"`.

- [ ] **Step 1: Failing tests** — full run: lanes exist, every lane satisfies `InRange` + same owner at build time + `PortAId < PortBId` + no duplicate pair; some port exceeds tier of founding; `PortTierRaised`/`LaneOpened` events present; two runs byte-identical registries:

```csharp
[Fact]
public void FullRun_BuildsLanes_RaisesTiers_Deterministically()
{
    var (_, s1) = EpochTestKit.Seeded(); var (_, s2) = EpochTestKit.Seeded();
    new EpochEngine().Run(s1); new EpochEngine().Run(s2);
    Assert.True(s1.Lanes.Count > 0, "no lanes built in 40 epochs");
    Assert.Equal(s1.Lanes.Count, s1.Lanes.Select(l => (l.PortAId, l.PortBId)).Distinct().Count());
    Assert.All(s1.Lanes, l => Assert.True(l.PortAId < l.PortBId));
    Assert.Contains(s1.Ports, p => p.Tier > 1 && p.FoundedYear > 0);   // a raised colony
    Assert.Equal(s1.Lanes.Select(l => (l.Id, l.PortAId, l.PortBId, l.BuiltYear)),
                 s2.Lanes.Select(l => (l.Id, l.PortAId, l.PortBId, l.BuiltYear)));
    Assert.Equal(s1.Ports.Select(p => (p.Id, p.OwnerActorId, p.Hex, p.Tier, p.FoundedYear)),
                 s2.Ports.Select(p => (p.Id, p.OwnerActorId, p.Hex, p.Tier, p.FoundedYear)));
}
```
- [ ] **Step 2–4: fail → implement → green.** Hex midpoint helper (cube lerp at t=0.5, standard cube round) is private to `AllocationPhase`.
- [ ] **Step 5: Commit** — `feat(epoch): allocation builds lanes and raises port tiers from development budget`

---

### Task 9: Interior grows population segments

**Files:**
- Modify: `src/Core/Epoch/Phases.cs` (`InteriorPhase`)
- Test: `tests/Core.Tests/Epoch/InteriorTests.cs` (new)

**Interfaces:**
- Produces: after entries, every segment integrates logistic growth: `cap = portTier × SegmentCapPerTier`; `Size += Size × SegmentGrowthPerYear × YearsPerEpoch × (1 − Size/cap)` clamped to `[0, cap]`; segments in id order. Trace gains `", K segments grow"`.

- [ ] **Step 1: Failing test** — after a full run, homeworld segments exceed their seed size and no segment exceeds its port's cap:

```csharp
[Fact]
public void Segments_GrowLogisticallyTowardTierCap()
{
    var (_, state) = EpochTestKit.Seeded();
    new EpochEngine().Run(state);
    var cfg = state.Config.Expansion;
    Assert.Contains(state.Segments, s => s.Size > cfg.HomeworldSegmentSize);
    foreach (var s in state.Segments)
        Assert.True(s.Size <= state.Ports[s.PortId].Tier * cfg.SegmentCapPerTier + 1e-9);
}
```
- [ ] **Step 2–4: fail → implement → green. Step 5: Commit** — `feat(epoch): interior integrates segment growth per world-year toward port-tier caps`

---

### Task 10: The new artifact format

**Files:**
- Create: `src/Core/Epoch/ArtifactSerializer.cs` (+`.meta`)
- Modify: `src/Inspector/Repl.cs` (`esave`/`eload`), `tests/Core.Tests/Epoch/DeterminismTests.cs`
- Test: `tests/Core.Tests/Epoch/ArtifactTests.cs` (new)

**Interfaces:**
- Produces:

```csharp
/// <summary>Layer-sectioned artifact (system-map.md §Artifact discipline,
/// narrative/handoff.md): each level's state is a section with its own schema
/// version; both configs stamped; line-based, invariant culture, "\n" newlines,
/// fixed ordering → byte-identical for identical state. The hex tier is never
/// persisted. Transients (perception, staged, decisions, trace) are not state.</summary>
public static class ArtifactSerializer
{
    public static string ToText(SimState state);
    public static void Save(SimState state, TextWriter writer);
    public static SimState Load(TextReader reader);   // InvalidDataException on malformed/mismatched versions
}
```
Format (every layer always present, records within in registry id / spiral order):

```
STARGEN-EPOCH|1
LAYER|config|1
GCONFIG|seed|radius|meanDensity|armCount|armTightness|armWidth|armStrength|coreRadius|discFalloff|mineralMult|precursorMult|homeworldRate|traversability
ESIM|yearsPerEpoch|epochCount          EGEN|emergenceWindowYears
EECO|warWeariness|stockpileDecay|provisionsPerPop
EINF|radiusBase|radiusPerTier|rangeBase|rangePerTier|maxTier|homeworldTier
EEXP|stubIncome|colonyCost|reach|upgradeBase|laneCost|homeSeg|colonySeg|segGrowth|segCap
LAYER|clock|1        → CLOCK|epochIndex|worldYear
LAYER|raster|1       → CELL|q|r|density|void|choke|lean|metallicity  +  ANCHOR|q|r|type|hexQ|hexR|speciesId
LAYER|species|1      → SPECIES|id|name|embodiment|6 temperament axes
LAYER|actors|1       → ACTOR|id|kind|name|seatQ|seatR|entryEpoch|entered  +  POLITY|actorId|speciesId|expansionPts|developmentPts
LAYER|ports|1        → PORT|id|owner|q|r|tier|foundedYear
LAYER|lanes|1        → LANE|id|portA|portB|builtYear
LAYER|facilities|1   → FACILITY|id|typeId|tier|q|r|owner|condition|builtYear   (empty in B)
LAYER|fleets|1       → FLEET|id|owner|q|r                                      (empty in B)
LAYER|segments|1     → SEGMENT|id|portId|speciesId|size
LAYER|events|1       → EVENT|id|year|stratum|type|actorIds(;)|q|r|magnitude|valence|visibility|payloadTag|payloadFields...
END
```
Doubles as `"R"` invariant; payload tags: `none`, `polityEmerged|name`, `portEstablished|name|portId`, `laneOpened|a|b`, `portTierRaised|portId|newTier`. Load: strict per-layer version check (mismatch → refuse with the layer name, mirroring the v5 message discipline); rebuilds `GalaxySkeleton` from GCONFIG then applies CELL/ANCHOR fields; reattaches `GenesisController` to polity actors; unknown record/layer tag → `InvalidDataException`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Artifact_ByteIdentical_AcrossIndependentRuns()
{
    var (_, s1) = EpochTestKit.Seeded(); var (_, s2) = EpochTestKit.Seeded();
    new EpochEngine().Run(s1); new EpochEngine().Run(s2);
    Assert.Equal(ArtifactSerializer.ToText(s1), ArtifactSerializer.ToText(s2));
}

[Fact]
public void Artifact_DifferentSeed_Diverges()
{
    var (_, s1) = EpochTestKit.Seeded(42); var (_, s2) = EpochTestKit.Seeded(43);
    new EpochEngine().Run(s1); new EpochEngine().Run(s2);
    Assert.NotEqual(ArtifactSerializer.ToText(s1), ArtifactSerializer.ToText(s2));
}

[Fact]
public void Artifact_LoadVsRebuild_Equivalence()
{
    var (_, built) = EpochTestKit.Seeded();
    new EpochEngine().Run(built);
    string a = ArtifactSerializer.ToText(built);
    var loaded = ArtifactSerializer.Load(new StringReader(a));
    Assert.Equal(a, ArtifactSerializer.ToText(loaded));           // save∘load = identity
}

[Fact]
public void Artifact_RefusesForeignAndVersionMismatch()
{
    Assert.Throws<InvalidDataException>(() => ArtifactSerializer.Load(new StringReader("nonsense")));
    var (_, s) = EpochTestKit.Seeded();
    string bumped = ArtifactSerializer.ToText(s).Replace("LAYER|ports|1", "LAYER|ports|9");
    Assert.Throws<InvalidDataException>(() => ArtifactSerializer.Load(new StringReader(bumped)));
}
```
Also add a culture-flip artifact test in `DeterminismTests` (render under `sv-SE` like the existing trace test).
- [ ] **Step 2–4: fail → implement → green** (full solution).
- [ ] **Step 5: REPL** — `esave <path>` (requires a stepped sim), `eload <path>` (sets the sim *and* `_galaxy` from the loaded skeleton so hex-tier browsing works); `help` updated.
- [ ] **Step 6: Commit** — `feat(epoch): layer-sectioned artifact format, versioned per layer, config-stamped; esave/eload`

---

### Task 11: REPL surface — domain map, chronicle, trace

**Files:**
- Create: `src/Inspector/EpochMapView.cs`
- Modify: `src/Core/Epoch/SimTraceView.cs`, `src/Inspector/Repl.cs`
- Test: `tests/Core.Tests/Epoch/DeterminismTests.cs` (trace gate already exists — keep green)

**Interfaces:**
- Produces:

```csharp
public static class EpochMapView
{
    /// <summary>One glyph per raster cell (offset canvas, doubled glyphs, same
    /// idiom as GalaxyMapView). Layers:
    ///  "domains" (default): ' ' void · '.' wilds · A–Z/a–z owner letter at the
    ///    cell center (actor id mod 52) · '?' contested (≥2 owners) · '*' cell
    ///    containing a port. Legend: letter=polity (ports count, domain cells).
    ///  "lanes": '*' port cells · '+' cells crossed by a lane's hex-line ·
    ///    '.' other non-void.</summary>
    public static string Render(SimState state, string layer = "domains");
}
```
`SimTraceView.Render` gains a registry summary block after the actor list — `ports: N · lanes: M · segments: K` and per-polity `#id name — P ports, top tier T` — every interpolation `Invariant`. `Describe` gains the three new payload lines (`"{name} establishes a port"`, `"lane opens between ports {a}–{b}"`, `"port {id} rises to tier {t}"`). `Repl`: field `_sim` (`SimState?`); `epoch <seed> [epochs] [radiusCells]` stores `_sim` (and `_galaxy` for hex browsing) after running and prints the trace; new commands `emap [domains|lanes]`, `chronicle [actorId]` (log events via `SimTraceView.Describe`, filtered per-actor view), both requiring `_sim`; `help` updated.

- [ ] **Step 1: Implement** (rendering is view code — the byte-identity gate covers `SimTraceView`; `EpochMapView` determinism follows from registry determinism; no new unit tests beyond keeping DeterminismTests green — the REPL smoke is the test).
- [ ] **Step 2: Smoke, piped (bash):**

```bash
printf 'epoch 42\nemap\nemap lanes\nchronicle\nquit\n' | dotnet run --project src/Inspector
```
Expected: trace with registry summary; a domain map showing letter-glow clusters around `*` ports with `?` at overlaps and dark voids; a lanes map with `+` webs; a founding chronicle (`PolityEmerged`/`PortEstablished` lines with years and hexes). This is the **eyeball artifact** — capture the exact commands in the ledger for the user gate.
- [ ] **Step 3: Full test run green. Step 4: Commit** — `feat(inspector): epoch domain/lane map, founding chronicle, registry trace summary`

---

### Task 12: Gates, review, freeze, wrap-up

**Files:**
- Create: `tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt`, `tests/Core.Tests/Epoch/GoldenTests.cs`; modify `tests/Core.Tests/Core.Tests.csproj` (copy Goldens to output), ledger, `docs/HANDOFF.md`, new `docs/superpowers/plans/2026-07-09-slice-d-kickoff-prompt.md`, kickoff checkbox.

- [ ] **Step 1: Mechanical gates** — `dotnet test StarSystemGeneration.sln` fully green; confirm hex-tier suite count unchanged from main (206 minus deleted-prototype tests plus new — record the arithmetic in the ledger); REPL smoke re-run.
- [ ] **Step 2: Fresh-eyes whole-branch review** — one subagent reviewing `git diff main...slice-b-two-plane-state` against this plan + the four design docs; then **one fix wave**; note both in the ledger.
- [ ] **Step 3: USER GATE — REPL eyeball acceptance.** The user runs `epoch 42` / `emap` / `emap lanes` / `chronicle`. "Looks right" = *empires as port-domain glows with organic borders*: clustered letter fields around `*` ports, contested `?` seams where reach collides, visible dark wilds/voids, lane webs, a readable founding chronicle. Tuning knobs if it looks wrong: `ColonyCost`, `StubIncomePerPortPerYear`, `ServiceRadius*`, `ColonizationReachHexes`. Iterate until accepted.
- [ ] **Step 4: Freeze goldens** (after acceptance — red-window closes): generate the reference artifact (seed 42, `GalaxyRadiusCells = 12`, default epoch config) via a tiny generator test or the REPL, commit it; `GoldenTests` builds the same config and byte-compares (normalize `\r\n` → `\n` on read); csproj gains `<None Include="Goldens\**" CopyToOutputDirectory="PreserveNewest" />`.
- [ ] **Step 5: USER GATE — merge decision.** On nod: merge to main locally (no push until say-so).
- [ ] **Step 6: Wrap-up** — update `docs/HANDOFF.md`; **write the Slice D kickoff prompt** (D needs B+C — check C's status first; real paths/interfaces from what landed, surprises section); flip `- [ ] Slice B complete` in the B kickoff; final ledger commit.

---

## Self-Review

- **Spec coverage:** kickoff scope → tasks: sparse registries (2, 4) · RegionCell slims / political fields deleted (5) · expansion chain decision→journey-stub→founding→growth (7, 6, 8) · `FoundColonyAct` resolver (7) · port tier growth via Allocation (8) · territory derived, never stored + overlap (3) · lanes built, capacity/speed from tiers (3, 8) · prototype deleted with tests, no adapters (5) · new artifact format + both determinism gates (10) · REPL map + chronicle (11) · gates/eyeball/goldens/wrap-up (12). Boundaries respected: no goods/markets, fleets as records only, perfect-info perception, emergence stub (channel 40).
- **Placeholder scan:** clean — every mechanic has its formula, order, and event; code blocks given for all novel logic; mechanical fallout enumerated by file and member.
- **Type consistency:** `PolityRecord.{ExpansionPoints, DevelopmentPoints}` (6, 7, 8, 10) · `PortDomains.{ServiceRadius, Services, OwnersAt, IsContested}` (3, 7, 11) · `LaneMath.{InterPortRange, InRange, Capacity, TransitSpeed}` (3, 8) · `EpochGenesis.Seed(GalaxySkeleton, EpochSimConfig)` (4, 6–11) · `SimState(config, skeleton)` + `PolityOf` (4 onward) · payload records named identically in Tasks 1, 7, 8, 10, 11 — consistent.
