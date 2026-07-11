# Lane Economics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace flat-cost all-pairs lane building with gate-facility economics per `docs/superpowers/specs/2026-07-11-lane-economics-design.md` — tiered Gate facilities set reach and cost, a detour/congestion rule stops redundant webs, freight corps build cross-domain lanes, and crossing fees follow gate ownership.

**Architecture:** Gates are ordinary `Facility` rows (new `InfraTypeId.Gate`), one per lane end, capped per port by tier. `Lane` gains gate ids + a saturation counter (lanes layer v3). A new `LaneNetwork` does deterministic shortest-path eligibility; a new `LaneFees` replaces the two tariff sites. `Phases.BuildLanes` is rewritten; `CorporationOps` gains a freight-line gate-building act.

**Tech Stack:** C# netstandard2.1 (`src/Core`), xUnit (`tests/Core.Tests`), REPL inspector (`src/Inspector`). Build/test: `dotnet test StarSystemGeneration.sln`.

## Global Constraints

- Work on branch `lane-economics` off `main`. Commit after every task.
- Hex-tier (Phase-1 generation) suite must stay green at every commit.
- Determinism: fixed iteration order (id order, P6); no wall-clock, no unkeyed randomness. Pathfinding tie-breaks by lower lane id.
- Greenfield: delete replaced knobs/code outright; no compatibility shims. The golden re-freezes once, in the final task, with the reason in the commit message.
- `unity/ProjectSettings` churn stays uncommitted.
- Piping to the REPL uses bash: `printf 'cmd\n' | dotnet run --project src/Inspector` (PowerShell mangles the first stdin line).
- Config knob defaults (spec §New/changed knobs): `GateSlotsPerPortTier=2`, gate reach 8/16/28 hexes by tier, `DetourFactor=1.8`, `ExpressSaturationFloor=0.9`, `SaturatedEpochsForExpress=3`, `GateTollRate=0.05`.
- **Two spec deviations, both to be flagged to the user and written into the design docs in Task 8:** (1) gates are built in *pairs by a single funder in one step* — the "half-built" state arises from a gate being destroyed later, not from staged cross-actor funding (escrowed joint funding is YAGNI); (2) no emergence gate-seeding — at emergence no lanes exist at all, and starter industry stocks the goods the first gates need.

---

### Task 1: Gate catalog row and config knobs (additive only)

**Files:**
- Modify: `src/Core/Substrate/Infrastructure.cs` (enum + table)
- Modify: `src/Core/Substrate/Siting.cs` (siting arm)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (InfrastructureKnobs ~line 771, ExpansionKnobs ~809, EconomyKnobs ~633, CorporateKnobs ~364)
- Modify: `src/Core/Epoch/KnobRegistry.cs` (new entries only; deletions happen in Task 4)
- Test: `tests/Core.Tests/Epoch/LaneGateTests.cs` (create)

**Interfaces:**
- Produces: `InfraTypeId.Gate = 15`; knobs `Infrastructure.GateSlotsPerPortTier`, `Infrastructure.GateReachTier1Hexes/GateReachTier2Hexes/GateReachTier3Hexes`, `Infrastructure.GateFunctionalCondition`, `Expansion.DetourFactor`, `Expansion.ExpressSaturationFloor`, `Expansion.SaturatedEpochsForExpress`, `Economy.GateTollRate`, `Corporate.MaxGateLanes`, `Corporate.GateTensionCeiling`, `Corporate.PiracyLengthPerHex`.
- Consumes: nothing new.

- [ ] **Step 1: Branch**

```bash
git checkout -b lane-economics main
```

- [ ] **Step 2: Write the failing test**

Create `tests/Core.Tests/Epoch/LaneGateTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneGateTests
{
    [Fact]
    public void GateRow_ExistsInTheCatalog_SupportFamily_NoOutput()
    {
        var def = Infrastructure.Get(InfraTypeId.Gate);
        Assert.Equal("Gate", def.Name);
        Assert.Equal(InfraFamily.Support, def.Family);
        Assert.Empty(def.Produces);
        Assert.Equal(0, def.BaseOutputPerYear);
        Assert.NotEmpty(def.BuildCost);
    }

    [Fact]
    public void GateKnobs_CarryTheSpecDefaults()
    {
        var cfg = new EpochSimConfig();
        Assert.Equal(2, cfg.Infrastructure.GateSlotsPerPortTier);
        Assert.Equal(8, cfg.Infrastructure.GateReachTier1Hexes);
        Assert.Equal(16, cfg.Infrastructure.GateReachTier2Hexes);
        Assert.Equal(28, cfg.Infrastructure.GateReachTier3Hexes);
        Assert.Equal(1.8, cfg.Expansion.DetourFactor);
        Assert.Equal(0.9, cfg.Expansion.ExpressSaturationFloor);
        Assert.Equal(3, cfg.Expansion.SaturatedEpochsForExpress);
        Assert.Equal(0.05, cfg.Economy.GateTollRate);
    }
}
```

- [ ] **Step 3: Run to verify both fail**

Run: `dotnet test StarSystemGeneration.sln --filter LaneGateTests`
Expected: FAIL — `InfraTypeId.Gate` / knob properties do not exist (compile error counts).

- [ ] **Step 4: Implement**

In `src/Core/Substrate/Infrastructure.cs` append to the enum (append-only, never renumber):

```csharp
    Fortress = 14,
    Gate = 15,           // lane terminus: paired port infrastructure
```

Append to `Table` after the Fortress row (table index must equal enum value):

```csharp
        new(InfraTypeId.Gate, "Gate", InfraFamily.Support, None,
            new[] { Q(GoodId.Alloys, 15), Q(GoodId.Machinery, 10), Q(GoodId.Composites, 5) }, 3,
            new[] { Q(GoodId.Machinery, 0.1) }, 0, 0.5),
```

Update the class doc comment ("15-row" → "16-row"). In `src/Core/Substrate/Siting.cs` add an arm next to Depot/Fortress (gates are placed by the lane builder, not the siting scorer, but the switch must be total):

```csharp
            InfraTypeId.Gate => portness,
```

In `src/Core/Epoch/EpochSimConfig.cs`:

`InfrastructureKnobs` — **add** (do not yet delete `InterPortRange*`; Task 4 does):

```csharp
    /// <summary>Gate slots a port hosts per tier — the lane-degree cap:
    /// hub ports must grow before they fan out (lane-economics spec §1).</summary>
    public int GateSlotsPerPortTier { get; set; } = 2;
    /// <summary>Max lane length linkable by a tier-1 gate pair, in hexes.</summary>
    public int GateReachTier1Hexes { get; set; } = 8;
    public int GateReachTier2Hexes { get; set; } = 16;
    public int GateReachTier3Hexes { get; set; } = 28;
    /// <summary>Condition below which a gate stops functioning and its
    /// lane goes dead (war damage severs without touching the port).</summary>
    public double GateFunctionalCondition { get; set; } = 0.25;
```

`ExpansionKnobs` — add:

```csharp
    /// <summary>A direct lane is redundant while the network path is within
    /// this factor of the direct distance (lane-economics spec §3).</summary>
    public double DetourFactor { get; set; } = 1.8;
    /// <summary>Used/capacity ratio at which a lane counts as saturated.</summary>
    public double ExpressSaturationFloor { get; set; } = 0.9;
    /// <summary>Consecutive saturated Markets steps after which a congested
    /// corridor earns a direct express bypass despite the detour rule.</summary>
    public int SaturatedEpochsForExpress { get; set; } = 3;
```

`EconomyKnobs` — add:

```csharp
    /// <summary>Corp-owned gate toll as a share of the destination price —
    /// the trader→gate-owner flow (lane-economics spec §4).</summary>
    public double GateTollRate { get; set; } = 0.05;
```

`CorporateKnobs` — add:

```csharp
    /// <summary>Gate-lane pairs a freight line may own.</summary>
    public int MaxGateLanes { get; set; } = 3;
    /// <summary>Relation tension at or above which a corp won't bridge two
    /// polities (non-hostility bar; no treaty required).</summary>
    public double GateTensionCeiling { get; set; } = 0.7;
    /// <summary>Piracy exposure per hex of lane length — longer lanes tempt
    /// raiders at thinner cargo (lane-economics spec §5).</summary>
    public double PiracyLengthPerHex { get; set; } = 0.05;
```

In `src/Core/Epoch/KnobRegistry.cs`, add `K(...)` entries following the file's existing pattern (see the `Relations.PactTariffFactor` entry at ~line 836) for: `Infrastructure.GateSlotsPerPortTier`, the three `GateReachTierNHexes`, `Infrastructure.GateFunctionalCondition`, `Expansion.DetourFactor`, `Expansion.ExpressSaturationFloor`, `Expansion.SaturatedEpochsForExpress`, `Economy.GateTollRate`, `Corporate.MaxGateLanes`, `Corporate.GateTensionCeiling`, `Corporate.PiracyLengthPerHex`. Int knobs follow the file's int-knob pattern (grep `(int)` in the file for the casting convention used by e.g. hex-range knobs).

- [ ] **Step 5: Run tests**

Run: `dotnet test StarSystemGeneration.sln --filter "LaneGateTests|KnobRegistryTests|InfrastructureTests"`
Expected: PASS (KnobRegistryTests may enumerate all knobs — if it asserts a count, update the count).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(lanes): Gate catalog row + lane-economics knobs (additive)"
```

---

### Task 2: Lane gate ids, saturation counter, gate-based LaneMath, serializer v3

**Files:**
- Modify: `src/Core/Epoch/Lane.cs`
- Modify: `src/Core/Epoch/ArtifactSerializer.cs:29` (lanes layer version), `:135-140` (write), `:888-892` (read)
- Modify: `src/Core/Epoch/FleetOps.cs:252,381,409` (LaneMath call sites), `TrafficPerYear`
- Modify: `src/Core/Epoch/MarketEngine.cs:61-62` (dead lanes get zero capacity)
- Modify: `tests/Core.Tests/Epoch/EpochTestKit.cs` (AddLane helper)
- Test: `tests/Core.Tests/Epoch/LaneGateTests.cs` (extend)

**Interfaces:**
- Produces: `Lane.GateAId`/`Lane.GateBId` (int, settable, default −1; GateAId belongs to PortAId's end), `Lane.SaturatedEpochs` (int, settable, default 0); `LaneMath.ReachHexes(EpochSimConfig, int gateTier)`, `LaneMath.RequiredGateTier(EpochSimConfig, int distanceHexes, int astroBonusHexes)` (returns −1 when out of reach), `LaneMath.IsLive(SimState, Lane)`, `LaneMath.Capacity(SimState, Lane)`, `LaneMath.TransitSpeed(SimState, Lane)`; test helper `EpochTestKit.AddLane(SimState state, int portAId, int portBId, int gateTier = 2, int ownerActorId = -1)` returning the `Lane` (ownerActorId −1 = each gate owned by its port's owner).
- Consumes: `InfraTypeId.Gate`, gate knobs (Task 1).

- [ ] **Step 1: Write the failing tests** (append to `LaneGateTests.cs`)

```csharp
    [Fact]
    public void RequiredGateTier_StepsWithDistance_AndFailsPastTier3()
    {
        var cfg = new EpochSimConfig();
        Assert.Equal(1, LaneMath.RequiredGateTier(cfg, 8, 0));
        Assert.Equal(2, LaneMath.RequiredGateTier(cfg, 9, 0));
        Assert.Equal(3, LaneMath.RequiredGateTier(cfg, 28, 0));
        Assert.Equal(-1, LaneMath.RequiredGateTier(cfg, 29, 0));
        Assert.Equal(1, LaneMath.RequiredGateTier(cfg, 9, 1));   // astro stretch
    }

    [Fact]
    public void Lane_IsLiveOnlyWhileBothGatesStandAndFunction()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunUntilTwoPorts(state);
        var lane = EpochTestKit.AddLane(state, 0, 1);
        Assert.True(LaneMath.IsLive(state, lane));
        state.Facilities[lane.GateAId].Condition = 0.1;   // raided below floor
        Assert.False(LaneMath.IsLive(state, lane));
    }

    [Fact]
    public void CapacityAndSpeed_DeriveFromGateTiers()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunUntilTwoPorts(state);
        var lane = EpochTestKit.AddLane(state, 0, 1, gateTier: 3);
        Assert.Equal(3.0, LaneMath.Capacity(state, lane));
        Assert.Equal(2.5, LaneMath.TransitSpeed(state, lane));
    }

    [Fact]
    public void Serializer_RoundTripsGateIdsAndSaturation()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunUntilTwoPorts(state);
        var lane = EpochTestKit.AddLane(state, 0, 1);
        lane.SaturatedEpochs = 2;
        var loaded = ArtifactSerializer.FromText(ArtifactSerializer.ToText(state));
        Assert.Equal(lane.GateAId, loaded.Lanes[lane.Id].GateAId);
        Assert.Equal(lane.GateBId, loaded.Lanes[lane.Id].GateBId);
        Assert.Equal(2, loaded.Lanes[lane.Id].SaturatedEpochs);
    }

    /// <summary>Advance the engine until two ports exist (colonization) —
    /// mirrors how other epoch tests obtain multi-port states. If the seeded
    /// state already has 2+ ports, this is a no-op.</summary>
    private static void RunUntilTwoPorts(SimState state)
    {
        var engine = new EpochEngine();
        for (int i = 0; i < 40 && state.Ports.Count < 2; i++)
            engine.Step(state);
        Assert.True(state.Ports.Count >= 2, "test needs two ports");
    }
```

(Adjust `EpochEngine` stepping to the engine's actual single-step API — grep `class EpochEngine` for the method other tests use, e.g. `Run`/`Step`; `FineTickTests.cs` shows the idiom. If seeded states routinely have several ports immediately, drop the loop.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test StarSystemGeneration.sln --filter LaneGateTests`
Expected: FAIL (missing members).

- [ ] **Step 3: Implement**

`Lane.cs` — add to the class:

```csharp
    /// <summary>Gate facility at each end (lane-economics spec §2): GateAId
    /// stands at PortAId's system. −1 only mid-construction; a lane whose
    /// gate is destroyed keeps the id — the ruin is the half-built state.</summary>
    public int GateAId { get; set; } = -1;
    public int GateBId { get; set; } = -1;
    /// <summary>Consecutive saturated Markets steps (used/capacity ≥
    /// ExpressSaturationFloor) — the express-bypass earn-in clock.</summary>
    public int SaturatedEpochs { get; set; }
```

Replace `LaneMath` entirely:

```csharp
/// <summary>Lane quantities derived from the two gate facilities' tiers
/// (lane-economics spec §2) — functions, never stored state. Reach, capacity
/// and speed all live in the built thing.</summary>
public static class LaneMath
{
    public static int ReachHexes(EpochSimConfig cfg, int gateTier) => gateTier switch
    {
        1 => cfg.Infrastructure.GateReachTier1Hexes,
        2 => cfg.Infrastructure.GateReachTier2Hexes,
        _ => cfg.Infrastructure.GateReachTier3Hexes,
    };

    /// <summary>Smallest gate tier whose reach (plus the builder's
    /// Astrogation bonus) covers the distance; −1 when even tier 3 can't.</summary>
    public static int RequiredGateTier(EpochSimConfig cfg, int distanceHexes,
                                       int astroBonusHexes)
    {
        for (int tier = 1; tier <= 3; tier++)
            if (distanceHexes <= ReachHexes(cfg, tier) + astroBonusHexes)
                return tier;
        return -1;
    }

    /// <summary>Live iff both gates stand and function — a raided gate
    /// severs the lane without touching the port.</summary>
    public static bool IsLive(SimState state, Lane lane) =>
        lane.GateAId >= 0 && lane.GateBId >= 0
        && state.Facilities[lane.GateAId].Condition
           >= state.Config.Infrastructure.GateFunctionalCondition
        && state.Facilities[lane.GateBId].Condition
           >= state.Config.Infrastructure.GateFunctionalCondition;

    /// <summary>Bulk throughput per world-year unit: the gate-tier sum, halved.</summary>
    public static double Capacity(SimState state, Lane lane) =>
        (state.Facilities[lane.GateAId].Tier
         + state.Facilities[lane.GateBId].Tier) * 0.5;

    /// <summary>Transit speed multiplier over off-lane crossing: the weaker
    /// gate bounds the lane.</summary>
    public static double TransitSpeed(SimState state, Lane lane) =>
        1.0 + 0.5 * System.Math.Min(state.Facilities[lane.GateAId].Tier,
                                    state.Facilities[lane.GateBId].Tier);
}
```

Delete `InterPortRange` and `InRange`; keep no port-based overloads. Migrate call sites:
- `FleetOps.cs:252`: `weights[i] = LaneMath.IsLive(state, lanes[i]) ? LaneMath.Capacity(state, lanes[i]) : 0.0;`
- `FleetOps.cs:381` (`PostedCapacity`) and `:409` (`TrafficPerYear`): `double speed = LaneMath.TransitSpeed(state, lane);` — and in both, return 0 first when `!LaneMath.IsLive(state, lane)`.
- `Phases.cs:715` (`BuildLanes`) still references `InRange` — Task 4 rewrites that method; until then make it compile by replacing the check with `if (LaneMath.RequiredGateTier(cfg, HexGrid.Distance(a.Hex, b.Hex), rangeBonus) < 0) continue;` (behavior shifts; the golden re-freeze happens once at the end).

`MarketEngine.cs:61-62` — dead lanes move nothing and parity skips them (capacity 0 already short-circuits both):

```csharp
        foreach (var lane in state.Lanes)                 // id order (P6)
            LaneFleetCapacity[lane.Id] = LaneMath.IsLive(state, lane)
                ? FleetOps.PostedCapacity(state, lane) : 0.0;
```

`ArtifactSerializer.cs` — bump `("lanes", 2)` → `("lanes", 3)`; write the three new fields:

```csharp
            w.WriteLine(Join("LANE", l.Id.ToString(Inv), l.PortAId.ToString(Inv),
                l.PortBId.ToString(Inv), l.BuiltYear.ToString(Inv),
                l.QuarantinedUntil.ToString(Inv), l.GateAId.ToString(Inv),
                l.GateBId.ToString(Inv), l.SaturatedEpochs.ToString(Inv)));
```

Read side:

```csharp
                    case "LANE":
                        state!.Lanes.Add(new Lane(int.Parse(f[1], Inv), int.Parse(f[2], Inv),
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv))
                        {
                            QuarantinedUntil = long.Parse(f[5], Inv),
                            GateAId = int.Parse(f[6], Inv),
                            GateBId = int.Parse(f[7], Inv),
                            SaturatedEpochs = int.Parse(f[8], Inv),
                        });
                        break;
```

`EpochTestKit.cs` — add:

```csharp
    /// <summary>Build a linked gate pair and its lane directly — the
    /// registry state the Allocation builder would produce, minus the
    /// treasury/goods flow (unit tests aren't economies).</summary>
    public static Lane AddLane(SimState state, int portAId, int portBId,
                               int gateTier = 2, int ownerActorId = -1)
    {
        if (portAId > portBId) (portAId, portBId) = (portBId, portAId);
        var a = state.Ports[portAId];
        var b = state.Ports[portBId];
        var gateA = new Facility(state.Facilities.Count,
            (int)StarGen.Core.Substrate.InfraTypeId.Gate, gateTier, a.Hex,
            ownerActorId >= 0 ? ownerActorId : a.OwnerActorId, state.WorldYear);
        state.Facilities.Add(gateA);
        var gateB = new Facility(state.Facilities.Count,
            (int)StarGen.Core.Substrate.InfraTypeId.Gate, gateTier, b.Hex,
            ownerActorId >= 0 ? ownerActorId : b.OwnerActorId, state.WorldYear);
        state.Facilities.Add(gateB);
        var lane = new Lane(state.Lanes.Count, portAId, portBId,
                            state.WorldYear)
        { GateAId = gateA.Id, GateBId = gateB.Id };
        state.Lanes.Add(lane);
        return lane;
    }
```

Then grep the test tree for direct lane construction and migrate every hit to the helper (they now need gates or freight dies):

```bash
grep -rn "new Lane(" tests/
```

- [ ] **Step 4: Run the full suite**

Run: `dotnet test StarSystemGeneration.sln`
Expected: everything green **except** `GoldenTests` (serializer format + builder behavior changed — that is the red window; it stays red until Task 8 re-freezes) and possibly `ArtifactTests`/`DeltaTests` if they embed LANE lines — update those fixtures now to the v3 shape.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(lanes): gate-pair lanes - gate ids + saturation on Lane, gate-based LaneMath, lanes layer v3"
```

---

### Task 3: LaneNetwork — deterministic pathfinding and eligibility

**Files:**
- Create: `src/Core/Epoch/LaneNetwork.cs`
- Modify: `src/Core/Epoch/MarketEngine.cs` (saturation counter update at end of the Markets step, where the scratch is finalized — grep `new MarketStepScratch` for the owning method)
- Test: `tests/Core.Tests/Epoch/LaneNetworkTests.cs` (create)

**Interfaces:**
- Produces: `LaneNetwork.ShortestPath(SimState, int portAId, int portBId)` → `(int PathHexes, List<int> LaneIds)` with `PathHexes = -1` when unreachable; `LaneNetwork.GateCount(SimState, Port)` (gates whose hex is the port's hex); `LaneNetwork.HasFreeGateSlot(SimState, Port)`; `LaneNetwork.DirectLaneEligible(SimState, int portAId, int portBId)` (detour rule + congestion waiver, spec §3 rules 3–4).
- Consumes: `LaneMath.IsLive`, `Lane.SaturatedEpochs`, Task 1 knobs.

- [ ] **Step 1: Write the failing tests**

Create `tests/Core.Tests/Epoch/LaneNetworkTests.cs`:

```csharp
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneNetworkTests
{
    // Chain A-B-C roughly on a line: the network path A→C is close to the
    // direct distance, so a direct A-C lane is redundant (detour rule).
    [Fact]
    public void DirectLane_RedundantWhileTheChainIsShortEnough()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        EpochTestKit.AddLane(state, a, b);
        EpochTestKit.AddLane(state, b, c);
        Assert.False(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void DirectLane_EligibleWhenNoPathExists()
    {
        var state = ThreePortChain(out int a, out _, out int c);
        Assert.True(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void SaturatedChain_EarnsTheExpressBypass()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        var ab = EpochTestKit.AddLane(state, a, b);
        var bc = EpochTestKit.AddLane(state, b, c);
        int need = state.Config.Expansion.SaturatedEpochsForExpress;
        ab.SaturatedEpochs = need;
        bc.SaturatedEpochs = need;
        Assert.True(LaneNetwork.DirectLaneEligible(state, a, c));
        bc.SaturatedEpochs = need - 1;      // one cool link blocks the waiver
        Assert.False(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void GateSlots_CapByPortTier()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        var port = state.Ports[a];
        port.Tier = 1;                       // 1 × GateSlotsPerPortTier = 2
        EpochTestKit.AddLane(state, a, b);
        Assert.True(LaneNetwork.HasFreeGateSlot(state, port));
        EpochTestKit.AddLane(state, a, c);
        Assert.False(LaneNetwork.HasFreeGateSlot(state, port));
    }

    /// <summary>A seeded state with three ports laid out as a chain: pick
    /// the seeded homeworld and manufacture two more ports at hexes 6 and
    /// 12 hexes out along one axis (owner = the same polity, markets added
    /// like colonization does).</summary>
    private static SimState ThreePortChain(out int a, out int b, out int c)
    {
        var (_, state) = EpochTestKit.Seeded();
        var home = state.Ports[0];
        a = home.Id;
        b = AddPort(state, home, dq: 6);
        c = AddPort(state, home, dq: 12);
        return state;
    }

    private static int AddPort(SimState state, Port home, int dq)
    {
        var hex = new StarGen.Core.Model.HexCoordinate(home.Hex.Q + dq, home.Hex.R);
        var port = new Port(state.Ports.Count, home.OwnerActorId, hex,
                            tier: 2, state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        return port.Id;
    }
}
```

(Adjust `HexCoordinate`/`Port`/`Market` constructor namespaces to what `Phases.cs:968-971` actually uses — copy that founding idiom verbatim.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test StarSystemGeneration.sln --filter LaneNetworkTests`
Expected: FAIL — `LaneNetwork` does not exist.

- [ ] **Step 3: Implement `src/Core/Epoch/LaneNetwork.cs`**

```csharp
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>Deterministic queries over the live-lane graph (lane-economics
/// spec §3): shortest network paths, gate-slot budgets, and the anti-web
/// eligibility rule the builders share. Everything iterates in id order and
/// tie-breaks on lower ids (P6).</summary>
public static class LaneNetwork
{
    /// <summary>Dijkstra over live lanes weighted by hex length. Returns
    /// (−1, empty) when no path. O(P²) scans — port counts are small and
    /// determinism beats a heap here.</summary>
    public static (int PathHexes, List<int> LaneIds) ShortestPath(
        SimState state, int fromPortId, int toPortId)
    {
        int n = state.Ports.Count;
        var dist = new int[n];
        var viaLane = new int[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = int.MaxValue; viaLane[i] = -1; }
        dist[fromPortId] = 0;
        for (int round = 0; round < n; round++)
        {
            int u = -1;
            for (int i = 0; i < n; i++)          // lowest dist, tie: lower id
                if (!done[i] && dist[i] != int.MaxValue
                    && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0) break;
            done[u] = true;
            if (u == toPortId) break;
            foreach (var lane in state.Lanes)     // id order (P6)
            {
                if (!LaneMath.IsLive(state, lane)) continue;
                int v = lane.PortAId == u ? lane.PortBId
                      : lane.PortBId == u ? lane.PortAId : -1;
                if (v < 0 || done[v]) continue;
                int w = HexGrid.Distance(state.Ports[lane.PortAId].Hex,
                                         state.Ports[lane.PortBId].Hex);
                if (dist[u] + w < dist[v])
                { dist[v] = dist[u] + w; viaLane[v] = lane.Id; }
            }
        }
        if (dist[toPortId] == int.MaxValue) return (-1, new List<int>());
        var path = new List<int>();
        for (int at = toPortId; viaLane[at] >= 0;)
        {
            var lane = state.Lanes[viaLane[at]];
            path.Add(lane.Id);
            at = lane.PortAId == at ? lane.PortBId : lane.PortAId;
        }
        path.Reverse();
        return (dist[toPortId], path);
    }

    /// <summary>Gates standing at this port's hex — live or ruined, a slot
    /// is a slot (a wrecked gate still occupies its berth until repaired).</summary>
    public static int GateCount(SimState state, Port port)
    {
        int count = 0;
        foreach (var f in state.Facilities)
            if (f.TypeId == (int)Substrate.InfraTypeId.Gate
                && f.Hex.Equals(port.Hex)) count++;
        return count;
    }

    public static bool HasFreeGateSlot(SimState state, Port port) =>
        GateCount(state, port)
        < port.Tier * state.Config.Infrastructure.GateSlotsPerPortTier;

    /// <summary>Spec §3 rules 3–4: eligible when unreachable, when the
    /// network detour is worse than DetourFactor × direct, or when every
    /// lane on the shortest path has run saturated long enough to earn the
    /// express bypass.</summary>
    public static bool DirectLaneEligible(SimState state, int portAId, int portBId)
    {
        var cfg = state.Config;
        var (pathHexes, laneIds) = ShortestPath(state, portAId, portBId);
        if (pathHexes < 0) return true;
        int direct = HexGrid.Distance(state.Ports[portAId].Hex,
                                      state.Ports[portBId].Hex);
        if (pathHexes > cfg.Expansion.DetourFactor * direct) return true;
        foreach (var laneId in laneIds)
            if (state.Lanes[laneId].SaturatedEpochs
                < cfg.Expansion.SaturatedEpochsForExpress) return false;
        return true;
    }
}
```

- [ ] **Step 4: Wire the saturation counter**

At the end of the Markets step, in the method that owns the `MarketStepScratch` (after freight has moved and `LaneCapacityUsed` is final):

```csharp
        // the express earn-in clock: consecutive saturated steps (spec §3.4)
        foreach (var lane in state.Lanes)                 // id order (P6)
            lane.SaturatedEpochs =
                scratch.LaneFleetCapacity[lane.Id] > 0
                && scratch.LaneCapacityUsed[lane.Id]
                   / scratch.LaneFleetCapacity[lane.Id]
                   >= state.Config.Expansion.ExpressSaturationFloor
                ? lane.SaturatedEpochs + 1 : 0;
```

- [ ] **Step 5: Run tests**

Run: `dotnet test StarSystemGeneration.sln --filter "LaneNetworkTests|LaneGateTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(lanes): LaneNetwork - deterministic shortest paths, gate slots, detour+congestion eligibility"
```

---

### Task 4: Rewrite the polity lane builder; delete the dead knobs

**Files:**
- Modify: `src/Core/Epoch/Phases.cs:682-738` (`BuildLanes`), `:369-375` (exclude gates from the industrial facility cap)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (delete `Expansion.LaneCost`, `Infrastructure.InterPortRangeBaseHexes`, `Infrastructure.InterPortRangePerTierHexes`)
- Modify: `src/Core/Epoch/KnobRegistry.cs` (delete their entries)
- Test: `tests/Core.Tests/Epoch/LaneBuilderTests.cs` (create)

**Interfaces:**
- Consumes: `LaneNetwork.*`, `LaneMath.RequiredGateTier`, `Production.TierCostFactor(int)`, the `BuildFacilities` goods-draw idiom (`Phases.cs:426-443`).
- Produces: rewritten `private static int BuildLanes(SimState, PolityRecord, List<Port>)` — same call signature, gate economics inside; helper `private static double GateValue(EpochSimConfig cfg, int tier)` (administered founding value of one gate: Σ BuildCost qty × InitialPrice × TierCostFactor(tier)); helper `private static bool GateGoodsPresent(SimState, PolityRecord, Market, int tier)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Core.Tests/Epoch/LaneBuilderTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneBuilderTests
{
    /// <summary>The seed-42 default run must not produce all-pairs webs:
    /// mean lane degree over ports stays well under (ports−1) within any
    /// single polity — the topology assertion from the spec (§7).</summary>
    [Fact]
    public void DefaultHistory_BuildsTreesAndHubs_NotAllPairsWebs()
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
            { MasterSeed = 42, GalaxyRadiusCells = 12 };
        var state = EpochGenesis.Seed(
            StarGen.Core.Galaxy.SkeletonBuilder.Build(gc),
            new EpochSimConfig { MasterSeed = 42 });
        new EpochEngine().Run(state);

        foreach (var pr in state.Polities)
        {
            var ports = new System.Collections.Generic.List<Port>();
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ports.Add(p);
            if (ports.Count < 4) continue;      // webs need bodies
            int degreeSum = 0;
            foreach (var lane in state.Lanes)
                foreach (var p in ports)
                    if (lane.PortAId == p.Id || lane.PortBId == p.Id)
                        degreeSum++;
            double meanDegree = (double)degreeSum / ports.Count;
            // all-pairs would be ports−1; a healthy network sits near 2
            Assert.True(meanDegree <= 0.6 * (ports.Count - 1) || meanDegree <= 3.0,
                $"polity {pr.ActorId}: mean lane degree {meanDegree:0.00} "
                + $"across {ports.Count} ports smells like a web");
        }
    }

    [Fact]
    public void EveryBuiltLane_HasBothGates_WithinSlotBudgets()
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
            { MasterSeed = 42, GalaxyRadiusCells = 12 };
        var state = EpochGenesis.Seed(
            StarGen.Core.Galaxy.SkeletonBuilder.Build(gc),
            new EpochSimConfig { MasterSeed = 42 });
        new EpochEngine().Run(state);

        foreach (var lane in state.Lanes)
        {
            Assert.True(lane.GateAId >= 0 && lane.GateBId >= 0);
            Assert.Equal((int)InfraTypeId.Gate,
                         state.Facilities[lane.GateAId].TypeId);
        }
        foreach (var port in state.Ports)
            Assert.True(LaneNetwork.GateCount(state, port) <= port.Tier
                * state.Config.Infrastructure.GateSlotsPerPortTier,
                $"port {port.Id} over its gate-slot budget");
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test StarSystemGeneration.sln --filter LaneBuilderTests`
Expected: `EveryBuiltLane_HasBothGates` FAILS (current builder sets no gate ids). The topology test may pass or fail — record which.

- [ ] **Step 3: Rewrite `BuildLanes` in `Phases.cs`**

Replace the whole method (keep the pact-ports pool):

```csharp
    /// <summary>Gate-pair lane construction (lane-economics spec §§1–3):
    /// candidates are own-port pairs plus pact-partner ports (one end must
    /// be own), filtered by gate reach, slot budgets, and the network
    /// detour/congestion rule; the cheapest eligible pair builds first while
    /// the development treasury and both port markets afford the two gates.
    /// One funder pays both ends in one step — half-built gates only ever
    /// arise from later destruction.</summary>
    private static int BuildLanes(SimState state, PolityRecord pr, List<Port> ownPorts)
    {
        var cfg = state.Config;
        int built = 0;
        int rangeBonus = TechOps.AstroRangeBonus(state, pr.ActorId);
        var pactPorts = new List<Port>();
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId == pr.ActorId
                || !state.Actors[port.OwnerActorId].Entered) continue;
            var relation = state.RelationOf(pr.ActorId, port.OwnerActorId);
            if (relation != null && relation.Rung >= TreatyRung.TradePact)
                pactPorts.Add(port);
        }
        while (true)
        {
            Port? bestA = null, bestB = null;
            int bestTier = 0, bestDist = int.MaxValue;
            double bestCost = double.MaxValue;
            for (int i = 0; i < ownPorts.Count; i++)
                for (int j = i + 1; j < ownPorts.Count + pactPorts.Count; j++)
                {
                    var a = ownPorts[i];
                    var b = j < ownPorts.Count ? ownPorts[j]
                        : pactPorts[j - ownPorts.Count];
                    if (a.Id > b.Id) (a, b) = (b, a);
                    if (LaneExists(state, a.Id, b.Id)) continue;
                    int dist = HexGrid.Distance(a.Hex, b.Hex);
                    int tier = LaneMath.RequiredGateTier(cfg, dist, rangeBonus);
                    if (tier < 0) continue;                       // out of reach
                    if (!LaneNetwork.HasFreeGateSlot(state, a)
                        || !LaneNetwork.HasFreeGateSlot(state, b)) continue;
                    if (!LaneNetwork.DirectLaneEligible(state, a.Id, b.Id)) continue;
                    double cost = 2.0 * GateValue(cfg, tier);
                    if (pr.DevelopmentPoints < cost) continue;
                    if (!GateGoodsPresent(state, pr, state.Markets[a.Id], tier)
                        || !GateGoodsPresent(state, pr, state.Markets[b.Id], tier))
                        continue;
                    if (cost < bestCost || (cost == bestCost && (dist < bestDist
                        || (dist == bestDist && (bestA == null || a.Id < bestA.Id
                            || (a.Id == bestA.Id && b.Id < bestB!.Id))))))
                    { bestCost = cost; bestDist = dist; bestTier = tier;
                      bestA = a; bestB = b; }
                }
            if (bestA == null) break;
            pr.DevelopmentPoints -= bestCost;
            int gateA = BuildGate(state, pr.ActorId, bestA, bestTier, pr);
            int gateB = BuildGate(state, pr.ActorId, bestB!, bestTier, pr);
            var lane = new Lane(state.Lanes.Count, bestA.Id, bestB!.Id,
                                state.WorldYear)
            { GateAId = gateA, GateBId = gateB };
            state.Lanes.Add(lane);
            built++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.LaneOpened,
                new[] { pr.ActorId }, Midpoint(bestA.Hex, bestB.Hex),
                Magnitude: bestTier, Valence: 1.0, EventVisibility.Regional,
                new LaneOpenedPayload(bestA.Id, bestB.Id)));
        }
        return built;
    }

    /// <summary>Administered founding value of one gate at a tier — the
    /// same founding-price convention CanAfford uses.</summary>
    private static double GateValue(EpochSimConfig cfg, int tier)
    {
        var def = Substrate.Infrastructure.Get(Substrate.InfraTypeId.Gate);
        double value = 0;
        foreach (var q in def.BuildCost)
            value += q.Quantity * Market.InitialPrice(cfg.Economy, q.Good)
                     * Production.TierCostFactor(tier);
        return value;
    }

    /// <summary>Gate build basket physically present at this end's market
    /// plus the funder's banked reserves (the CanAfford convention).</summary>
    private static bool GateGoodsPresent(SimState state, PolityRecord pr,
                                         Market market, int tier)
    {
        var def = Substrate.Infrastructure.Get(Substrate.InfraTypeId.Gate);
        foreach (var q in def.BuildCost)
            if (market.Inventory[(int)q.Good] + pr.ReserveQty[(int)q.Good]
                < q.Quantity * Production.TierCostFactor(tier)) return false;
        return true;
    }

    /// <summary>Draw one gate's build basket from its port market (reserve
    /// fallback, the BuildFacilities idiom), pay construction wages, and
    /// register the facility. Returns the facility id.</summary>
    private static int BuildGate(SimState state, int ownerActorId, Port port,
                                 int tier, PolityRecord? funderReserves)
    {
        var cfg = state.Config;
        var def = Substrate.Infrastructure.Get(Substrate.InfraTypeId.Gate);
        var market = state.Markets[port.Id];
        double scale = Production.TierCostFactor(tier);
        double value = 0;
        foreach (var q in def.BuildCost)
        {
            double need = q.Quantity * scale;
            value += need * Market.InitialPrice(cfg.Economy, q.Good);
            double fromMarket = market.Draw((int)q.Good, need);
            market.LastCleared[(int)q.Good] += fromMarket;
            double fromReserve = need - fromMarket;
            if (fromReserve > 0 && funderReserves != null)
            {
                funderReserves.ReserveQty[(int)q.Good] = Math.Max(0,
                    funderReserves.ReserveQty[(int)q.Good] - fromReserve);
                if (funderReserves.ReserveQty[(int)q.Good] <= 0)
                    funderReserves.ReserveGrade[(int)q.Good] = 0;
            }
        }
        MarketEngine.PayWages(state, port.Id, value);
        var gate = new Facility(state.Facilities.Count,
            (int)Substrate.InfraTypeId.Gate, tier, port.Hex, ownerActorId,
            state.WorldYear);
        state.Facilities.Add(gate);
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.FacilityBuilt,
            new[] { ownerActorId }, port.Hex, Magnitude: tier, Valence: 1.0,
            EventVisibility.Regional,
            new FacilityBuiltPayload(gate.Id, gate.TypeId, tier)));
        return gate.Id;
    }
```

In `BuildFacilities` (`Phases.cs:369-375`), gates must not eat industrial slots — add to the attached count loop:

```csharp
                if (f.TypeId == (int)Substrate.InfraTypeId.Gate) continue;
```

Delete `Expansion.LaneCost` and `Infrastructure.InterPortRangeBaseHexes`/`InterPortRangePerTierHexes` from `EpochSimConfig.cs`, and their `K(...)` entries from `KnobRegistry.cs` (grep `"Expansion.LaneCost"` and `"Infrastructure.InterPortRange"`). Fix any remaining compile references (grep `LaneCost\b` and `InterPortRange`).

- [ ] **Step 4: Run the suite**

Run: `dotnet test StarSystemGeneration.sln`
Expected: LaneBuilderTests PASS; hex-tier suite green; GoldenTests still red (expected until Task 8). If `EpochSimConfigTests`/`KnobRegistryTests` assert knob counts or names, update them.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(lanes): gate-economics polity builder - cheapest eligible pair, slots, detour+congestion; LaneCost and port-range knobs deleted"
```

---

### Task 5: LaneFees — tolls and gate-anchored customs

**Files:**
- Create: `src/Core/Epoch/LaneFees.cs`
- Modify: `src/Core/Epoch/MarketEngine.cs:627-628` + `ApplyImportParity` (636-673), `Arbitrage` tariff block (777-786, 824-830)
- Test: `tests/Core.Tests/Epoch/LaneFeeTests.cs` (create)

**Interfaces:**
- Produces: `LaneFees.CrossingFeePerUnit(SimState state, Lane lane, int dstPortId, int good, double dstPrice, int shipperActorId, out int recipientActorId)` — the dst-side gate decides: shipper-owned → 0; corp-owned → `GateTollRate × dstPrice` to the corp; polity-owned + foreign shipper → gate owner's `TariffSchedule[good] × dstPrice × RelationsOps.TariffFactor(shipper, owner)`; polity-owned + same-polity shipper → 0. `recipientActorId = -1` when the fee is 0.
- Consumes: `Lane.GateAId/GateBId`, `ActorKind.Corporation` (`Actor.cs:7`), `state.LedgerOf(int)` (`SimState.cs:100`).

- [ ] **Step 1: Write the failing tests**

Create `tests/Core.Tests/Epoch/LaneFeeTests.cs`:

```csharp
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneFeeTests
{
    private const int Good = 0;
    private const double DstPrice = 10.0;

    [Fact]
    public void ShipperOwnedGate_CrossesFree()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        state.Facilities[DstGate(lane, dstPort)].OwnerActorId = shipper;
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(0.0, fee);
        Assert.Equal(-1, to);
    }

    [Fact]
    public void CorpOwnedGate_TollsEveryoneElse()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        int corpActor = AddCorpActor(state);
        state.Facilities[DstGate(lane, dstPort)].OwnerActorId = corpActor;
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(state.Config.Economy.GateTollRate * DstPrice, fee, 10);
        Assert.Equal(corpActor, to);
    }

    [Fact]
    public void PolityGate_SamePolityFreight_Free()
    {
        var (state, lane, dstPort, _) = CrossBorderLane();
        int gateOwner = state.Facilities[DstGate(lane, dstPort)].OwnerActorId;
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, gateOwner, out int to);
        Assert.Equal(0.0, fee);
        Assert.Equal(-1, to);
    }

    [Fact]
    public void PolityGate_ForeignFreight_PaysCustomsToTheGateOwner()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        int gateOwner = state.Facilities[DstGate(lane, dstPort)].OwnerActorId;
        SetTariff(state, gateOwner, Good, 0.2);
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(0.2 * DstPrice * RelationsOps.TariffFactor(state, shipper,
            gateOwner), fee, 10);
        Assert.Equal(gateOwner, to);
    }

    private static int DstGate(Lane lane, int dstPortId) =>
        lane.PortAId == dstPortId ? lane.GateAId : lane.GateBId;

    /// <summary>Two ports of two different entered polities linked by a
    /// lane; each gate owned by its port's polity. Returns the state, the
    /// lane, the dst port id (the second polity's), and the shipper actor
    /// (the first polity — the foreigner at dst).</summary>
    private static (SimState, Lane, int, int) CrossBorderLane()
    {
        var (_, state) = EpochTestKit.Seeded();
        // find or force two entered polities with a port each; if the seed
        // enters only one, manufacture a second port owned by another actor
        // exactly as LaneNetworkTests.AddPort does, with a different owner.
        var portA = state.Ports[0];
        int other = -1;
        foreach (var actor in state.Actors)
            if (actor.Id != portA.OwnerActorId
                && actor.Kind == ActorKind.Polity) { other = actor.Id; break; }
        var hex = new StarGen.Core.Model.HexCoordinate(portA.Hex.Q + 6, portA.Hex.R);
        var portB = new Port(state.Ports.Count, other, hex, tier: 2,
                             state.WorldYear);
        state.Ports.Add(portB);
        state.Markets.Add(new Market(portB.Id, state.Config.Economy));
        var lane = EpochTestKit.AddLane(state, portA.Id, portB.Id);
        return (state, lane, portB.Id, portA.OwnerActorId);
    }

    private static int AddCorpActor(SimState state)
    {
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, "Test Line",
            state.Ports[0].Hex, state.EpochIndex, new CorporateController())
        { Entered = true });
        state.Corporations.Add(new Corporation(state.Corporations.Count,
            actorId, "Test Line", state.Ports[0].OwnerActorId,
            CorporateNiche.Freight, 0, state.WorldYear));
        return actorId;
    }

    private static void SetTariff(SimState state, int actorId, int good,
                                  double rate)
    {
        var policies = state.Actors[actorId].Policies as PolityPolicies
                       ?? PolityPolicies.Default;
        var schedule = new System.Collections.Generic.Dictionary<int, double>(
            policies.TariffSchedule) { [good] = rate };
        state.Actors[actorId].Policies =
            policies with { TariffSchedule = schedule };
    }
}
```

(Adjust `Actor`/`Policies` construction to the real API — `CorporationOps.cs:380-390` shows the corp-actor founding idiom to copy; `ControllerContract.cs:411-420` shows the TariffSchedule `with` idiom.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test StarSystemGeneration.sln --filter LaneFeeTests`
Expected: FAIL — `LaneFees` missing.

- [ ] **Step 3: Implement `src/Core/Epoch/LaneFees.cs`**

```csharp
namespace StarGen.Core.Epoch;

/// <summary>Per-crossing fees decided by the destination-side gate's owner
/// (lane-economics spec §4): vertical integration crosses free, corp gates
/// toll, polity gates charge customs on foreign freight only — the existing
/// tariff machinery relocated to a physical collection point, charged once,
/// at the gate the shipment enters through.</summary>
public static class LaneFees
{
    /// <summary>Fee per unit at destination price, and who collects it
    /// (−1 when free). Legality/prohibition checks stay with the caller.</summary>
    public static double CrossingFeePerUnit(SimState state, Lane lane,
        int dstPortId, int good, double dstPrice, int shipperActorId,
        out int recipientActorId)
    {
        recipientActorId = -1;
        int gateId = lane.PortAId == dstPortId ? lane.GateAId : lane.GateBId;
        if (gateId < 0) return 0;
        int owner = state.Facilities[gateId].OwnerActorId;
        if (owner == shipperActorId) return 0;        // your gate, your road
        if (state.Actors[owner].Kind == ActorKind.Corporation)
        {
            recipientActorId = owner;
            return state.Config.Economy.GateTollRate * dstPrice;
        }
        // polity gate: customs on foreign freight, once, at entry
        var policies = state.Actors[owner].Policies as PolityPolicies
                       ?? PolityPolicies.Default;
        if (!policies.TariffSchedule.TryGetValue(good, out double rate)
            || rate <= 0) return 0;
        double fee = rate * dstPrice
            * Interpolity.RelationsOps.TariffFactor(state, shipperActorId, owner);
        if (fee <= 0) return 0;
        recipientActorId = owner;
        return fee;
    }
}
```

(Fix the `RelationsOps` namespace qualifier to match its actual namespace — grep `namespace` in `RelationsOps.cs`.)

- [ ] **Step 4: Rewire the two tariff sites in `MarketEngine.cs`**

`Arbitrage` — replace lines 777-786 (the `double tariff = 0; if (src.OwnerActorId != dst.OwnerActorId) {...}` block) with:

```csharp
                double tariff = LaneFees.CrossingFeePerUnit(state, lane,
                    dst.Id, g, pDst, src.OwnerActorId, out int feeTo);
```

and replace the settlement block at 824-830 with:

```csharp
                if (tariff > 0 && feeTo >= 0)
                {
                    exporter.Credits -= drawn * tariff;
                    var collector = state.LedgerOf(feeTo);
                    collector.Credits += drawn * tariff;
                    collector.Receipts += drawn * tariff;
                }
```

`ApplyImportParity` — change the signature to `(SimState state, double[][] snapshot, Lane lane, int srcId, int dstId)`, update the two call sites (627-628) to pass `lane`, and replace the tariff computation (659-664) with:

```csharp
            double tariff = LaneFees.CrossingFeePerUnit(state, lane, dstId, g,
                snapshot[dstId][g], src.OwnerActorId, out _);
```

- [ ] **Step 5: Run the suite**

Run: `dotnet test StarSystemGeneration.sln`
Expected: LaneFeeTests PASS; `MarketFreightTests` and friends green (their lanes come from `AddLane`, whose polity-owned gates reproduce today's tariff behavior exactly — same fee, same recipient). GoldenTests still red.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(lanes): LaneFees - gate-ownership crossing fees replace the two tariff sites; corp tolls, gate-anchored customs"
```

---

### Task 6: Freight corps build cross-domain gate lanes

**Files:**
- Modify: `src/Core/Epoch/Interior/CorporationOps.cs` (`Operate` switch ~line 458, `AddCorporateDemand` Freight case ~637, new `InvestGateLanes`, extract pair-profit helper from `LaneCarriesProfit` ~195)
- Modify: `src/Core/Epoch/Phases.cs` (make `BuildGate` `internal static` so CorporationOps reuses it — or copy the idiom; **reuse, don't copy**)
- Test: `tests/Core.Tests/Epoch/CorpGateLaneTests.cs` (create)

**Interfaces:**
- Consumes: `LaneNetwork.*`, `LaneMath.RequiredGateTier`, `AllocationPhase.BuildGate(state, ownerActorId, port, tier, funderReserves: null)` (corp goods come from markets only — no polity reserves), `WarOps.ActiveWarBetween`, `state.RelationOf`.
- Produces: `private static void InvestGateLanes(SimState, Corporation, CorporationPolicies)` called from `Operate` for `CorporateNiche.Freight` (after `InvestFleet`); `internal static bool PairCarriesProfit(SimState state, int portAId, int portBId, double margin)` (the `LaneCarriesProfit` math on a port pair, no lane needed — refactor `LaneCarriesProfit` to delegate to it).

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/CorpGateLaneTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class CorpGateLaneTests
{
    /// <summary>A freight corp with fat credits, a profitable price gap
    /// across a non-hostile border in gate reach, and stocked markets builds
    /// the pair of gates and the lane — and owns both gates.</summary>
    [Fact]
    public void FreightCorp_BridgesANonHostileBorder()
    {
        var (state, corp, homePort, foreignPort) = CrossBorderOpportunity();
        int lanesBefore = state.Lanes.Count;

        CorporationOps.Operate(state);

        Assert.Equal(lanesBefore + 1, state.Lanes.Count);
        var lane = state.Lanes[state.Lanes.Count - 1];
        Assert.Equal(corp.ActorId, state.Facilities[lane.GateAId].OwnerActorId);
        Assert.Equal(corp.ActorId, state.Facilities[lane.GateBId].OwnerActorId);
    }

    [Fact]
    public void FreightCorp_WillNotBridgeAHostileBorder()
    {
        var (state, corp, homePort, foreignPort) = CrossBorderOpportunity();
        var rel = state.RelationOf(
            state.Ports[homePort].OwnerActorId,
            state.Ports[foreignPort].OwnerActorId);
        rel!.Tension = 1.0;                       // hostile: over the ceiling
        int lanesBefore = state.Lanes.Count;

        CorporationOps.Operate(state);

        Assert.Equal(lanesBefore, state.Lanes.Count);
    }

    /// <summary>Two entered polities, one port each within tier-2 gate
    /// reach; a freight corp at the first with deep credits; both markets
    /// stocked with the gate basket; a fat provisions price gap so the pair
    /// carries profit. Returns (state, corp, homePortId, foreignPortId).</summary>
    private static (SimState, Corporation, int, int) CrossBorderOpportunity()
    {
        // construct exactly as LaneFeeTests.CrossBorderLane does, minus the
        // lane; then: found the corp actor as LaneFeeTests.AddCorpActor
        // does with HomePortId = ports[0], HostPolityId = its owner, and
        //   corp.Credits = 10_000;
        // stock both markets: foreach good in Gate BuildCost basket ×
        //   TierCostFactor(2): market.Deposit(good, 3 × need, 0.8);
        // price gap: dstMarket.Price[Provisions] = 5 × srcMarket price and
        //   srcMarket.Deposit(Provisions, 50, 0.8) so there's stock to ship;
        // ensure a relation exists between the two polities at low tension
        //   (state.RelationOf(...) — create via the relations registry
        //   idiom RelationsTests uses if null).
        // Return the tuple.
        throw new System.NotImplementedException("build per comment");
    }
}
```

Implement the helper per its comment before running (copy idioms from `LaneFeeTests`, `RelationsTests`; this is scaffolding code, not production).

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test StarSystemGeneration.sln --filter CorpGateLaneTests`
Expected: FAIL — no lane appears (corps don't build lanes yet).

- [ ] **Step 3: Implement**

In `Phases.cs` change `BuildGate` from `private` to `internal` (same class, callable as `AllocationPhase.BuildGate`).

In `CorporationOps.cs`, refactor `LaneCarriesProfit` (line ~195): extract the body into

```csharp
    /// <summary>Would a hauler between these two ports clear the margin?
    /// The arbitrage's own per-unit math on a hypothetical pair — shared by
    /// the niche watcher (existing lanes) and the corp gate builder
    /// (prospective ones).</summary>
    internal static bool PairCarriesProfit(SimState state, int portAId,
                                           int portBId, double margin)
    { /* the existing LaneCarriesProfit body, ports looked up by id */ }

    private static bool LaneCarriesProfit(SimState state, Lane lane,
                                          double margin) =>
        PairCarriesProfit(state, lane.PortAId, lane.PortBId, margin);
```

Add the builder and call it from `Operate`'s Freight case (line ~458):

```csharp
                case CorporateNiche.Freight:
                    InvestFleet(state, corp, policies);
                    InvestGateLanes(state, corp, policies);
                    break;
```

```csharp
    /// <summary>The freight line's founding act (lane-economics spec §4):
    /// bridge a profitable, non-hostile border with a corp-owned gate pair.
    /// One lane per epoch, host-polity ports × foreign ports, deterministic
    /// scan, cheapest eligible profitable pair first. No treaty required —
    /// profit walks across the border before diplomats do.</summary>
    private static void InvestGateLanes(SimState state, Corporation corp,
                                        CorporationPolicies policies)
    {
        if (corp.HostPolityId < 0) return;            // outlaws build nothing
        var cfg = state.Config;
        var knobs = cfg.Corporate;
        int gatesOwned = 0;
        foreach (var f in state.Facilities)
            if (f.OwnerActorId == corp.ActorId
                && f.TypeId == (int)InfraTypeId.Gate) gatesOwned++;
        if (gatesOwned / 2 >= knobs.MaxGateLanes) return;

        Port? bestA = null, bestB = null;
        int bestTier = 0;
        double bestCost = double.MaxValue;
        foreach (var home in state.Ports)                 // id order (P6)
        {
            if (home.OwnerActorId != corp.HostPolityId) continue;
            foreach (var afar in state.Ports)             // id order (P6)
            {
                if (afar.OwnerActorId == corp.HostPolityId
                    || !state.Actors[afar.OwnerActorId].Entered) continue;
                if (state.Actors[afar.OwnerActorId].Kind != ActorKind.Polity)
                    continue;
                // non-hostile: not at war, tension under the ceiling
                if (WarOps.ActiveWarBetween(state, corp.HostPolityId,
                        afar.OwnerActorId) != null) continue;
                var rel = state.RelationOf(corp.HostPolityId, afar.OwnerActorId);
                if (rel != null && rel.Tension >= knobs.GateTensionCeiling)
                    continue;
                var (a, b) = home.Id < afar.Id ? (home, afar) : (afar, home);
                if (LaneExists(state, a.Id, b.Id)) continue;
                int dist = HexGrid.Distance(a.Hex, b.Hex);
                int tier = LaneMath.RequiredGateTier(cfg, dist, 0);
                if (tier < 0) continue;
                if (!LaneNetwork.HasFreeGateSlot(state, a)
                    || !LaneNetwork.HasFreeGateSlot(state, b)) continue;
                if (!LaneNetwork.DirectLaneEligible(state, a.Id, b.Id)) continue;
                if (!PairCarriesProfit(state, a.Id, b.Id,
                        knobs.FreightNicheMargin)) continue;
                double cost = 2.0 * GateValue(state, tier);
                if (corp.Credits * policies.Investment.Facilities < cost)
                    continue;
                if (!GateBasketStocked(state, state.Markets[a.Id], tier)
                    || !GateBasketStocked(state, state.Markets[b.Id], tier))
                    continue;
                if (cost < bestCost || (cost == bestCost
                    && (bestA == null || a.Id < bestA.Id
                        || (a.Id == bestA.Id && b.Id < bestB!.Id))))
                { bestCost = cost; bestTier = tier; bestA = a; bestB = b; }
            }
        }
        if (bestA == null) return;
        corp.Credits -= bestCost;
        int gateA = AllocationPhase.BuildGate(state, corp.ActorId, bestA,
                                              bestTier, funderReserves: null);
        int gateB = AllocationPhase.BuildGate(state, corp.ActorId, bestB!,
                                              bestTier, funderReserves: null);
        var lane = new Lane(state.Lanes.Count, bestA.Id, bestB!.Id,
                            state.WorldYear)
        { GateAId = gateA, GateBId = gateB };
        state.Lanes.Add(lane);
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.LaneOpened,
            new[] { corp.ActorId, corp.HostPolityId,
                    bestB.OwnerActorId == corp.HostPolityId
                        ? bestA.OwnerActorId : bestB.OwnerActorId },
            bestA.Hex, Magnitude: bestTier, Valence: 1.0,
            EventVisibility.Regional,
            new LaneOpenedPayload(bestA.Id, bestB.Id)));
    }

    private static double GateValue(SimState state, int tier)
    {
        var def = Infrastructure.Get(InfraTypeId.Gate);
        double value = 0;
        foreach (var q in def.BuildCost)
            value += q.Quantity
                     * Market.InitialPrice(state.Config.Economy, q.Good)
                     * Production.TierCostFactor(tier);
        return value;
    }

    private static bool GateBasketStocked(SimState state, Market market, int tier)
    {
        var def = Infrastructure.Get(InfraTypeId.Gate);
        foreach (var q in def.BuildCost)
            if (market.Inventory[(int)q.Good]
                < q.Quantity * Production.TierCostFactor(tier)) return false;
        return true;
    }

    private static bool LaneExists(SimState state, int aId, int bId)
    {
        foreach (var l in state.Lanes)
            if (l.PortAId == aId && l.PortBId == bId) return true;
        return false;
    }
```

In `AddCorporateDemand`'s Freight case (line ~637), pull the gate basket too, so the goods the builder needs get hauled in:

```csharp
                case CorporateNiche.Freight:
                    if (corp.Credits <= 0) break;
                    scratch.Demand[home][(int)GoodId.ShipComponents]
                        += state.Config.Corporate.FreightPullComponents;
                    foreach (var q in Infrastructure.Get(InfraTypeId.Gate).BuildCost)
                        scratch.Demand[home][(int)q.Good] += q.Quantity;
                    break;
```

- [ ] **Step 4: Run the suite**

Run: `dotnet test StarSystemGeneration.sln`
Expected: CorpGateLaneTests PASS; CorporationTests green; GoldenTests still red.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(lanes): freight lines bridge non-hostile borders with corp-owned gate pairs"
```

---

### Task 7: Piracy exposure scales with lane length

**Files:**
- Modify: `src/Core/Epoch/Interior/CorporationOps.cs` (`DetectNiche` raiding scan, ~line 159-169)
- Test: `tests/Core.Tests/Epoch/CorporationTests.cs` (extend)

**Interfaces:**
- Consumes: `Corporate.PiracyLengthPerHex` (Task 1).
- Produces: behavior only — longer lawless lanes clear the raid floor at thinner cargo.

- [ ] **Step 1: Write the failing test** (append to `CorporationTests.cs`)

```csharp
    /// <summary>Length is exposure (lane-economics spec §5): with capacity
    /// just under the raid floor, a long lane tempts a pirate band where a
    /// short one doesn't. Build two lawless single-lane states differing
    /// only in lane length and compare DetectNiche outcomes via the public
    /// WatchNiches surface (a band founds or it doesn't).</summary>
    [Fact]
    public void LongLanes_TemptPiratesAtThinnerCargo()
    {
        // state A: navyless polity, one lane of ~4 hexes, posted capacity C
        //   set just below RaidCapacityFloor → WatchNiches founds no band.
        // state B: identical but the lane spans ~20 hexes (place the second
        //   port farther), same capacity C → the exposure multiplier
        //   (1 + PiracyLengthPerHex × dist) lifts it over the floor →
        //   WatchNiches founds a band (state.Corporations gains a Raiding corp).
        // Build both with EpochTestKit.Seeded + AddPort + AddLane +
        // EpochTestKit.PostFreight (hull count tuned to sit just under the
        // floor for the short lane; read RaidCapacityFloor from config).
    }
```

Fill the body per the comment; assert on `state.Corporations` containing/lacking a `CorporateNiche.Raiding` entry after `CorporationOps.WatchNiches(state)`.

- [ ] **Step 2: Run to verify failure** — the long-lane half fails (no exposure term yet).

- [ ] **Step 3: Implement** — in `DetectNiche`'s raiding loop (~line 159), replace the capacity comparison:

```csharp
        foreach (var lane in state.Lanes)
        {
            var src = state.Ports[lane.PortAId];
            if (src.OwnerActorId != pr.ActorId) continue;
            bool haven = InRuinsShadow(state, lane);
            if (!navyless && !haven) continue;
            double floor = knobs.RaidCapacityFloor
                * (haven ? state.Config.Poi.LawlessRaidFactor : 1.0);
            // length is exposure: more hexes, more ambush points (spec §5)
            double exposure = 1.0 + knobs.PiracyLengthPerHex
                * HexGrid.Distance(src.Hex, state.Ports[lane.PortBId].Hex);
            if (FleetOps.PostedCapacity(state, lane) * exposure >= floor)
                return (CorporateNiche.Raiding, lane.Id);
        }
```

- [ ] **Step 4: Run** `dotnet test StarSystemGeneration.sln --filter CorporationTests` — PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(lanes): piracy exposure scales with lane length"
```

---

### Task 8: REPL surface, design-doc amendments, golden re-freeze, full gates

**Files:**
- Modify: `src/Inspector/Repl.cs` (new `elanes` command; help text at line ~36)
- Modify: `src/Inspector/EpochMapView.cs` (lanes mode: mark dead lanes)
- Modify: `docs/design/frame/space-and-travel.md` (§Lanes), `docs/design/substrate/infrastructure.md` (facility table + gate row), `docs/design/economy/corporations.md` (freight-line acts), `docs/design/economy/markets.md` (tariff collection point)
- Modify: `tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt` (regenerate)
- Modify: `docs/HANDOFF.md`

**Interfaces:** none new — surfacing and closure.

- [ ] **Step 1: `elanes` command** — in `Repl.cs`, add beside `equarantine` (line ~222):

```csharp
                case "elanes":
                {
                    if (_sim.Lanes.Count == 0)
                    { Console.WriteLine("no lanes yet"); break; }
                    foreach (var lane in _sim.Lanes)
                    {
                        var a = _sim.Ports[lane.PortAId];
                        var b = _sim.Ports[lane.PortBId];
                        int dist = StarGen.Core.Galaxy.HexGrid.Distance(a.Hex, b.Hex);
                        var gA = lane.GateAId >= 0 ? _sim.Facilities[lane.GateAId] : null;
                        var gB = lane.GateBId >= 0 ? _sim.Facilities[lane.GateBId] : null;
                        string Owner(StarGen.Core.Epoch.Facility? g) => g == null
                            ? "—" : _sim.Actors[g.OwnerActorId].Name;
                        bool live = StarGen.Core.Epoch.LaneMath.IsLive(_sim, lane);
                        Console.WriteLine(
                            $"#{lane.Id} {a.Id}<->{b.Id} {dist,3}hx "
                            + $"T{gA?.Tier ?? 0}/{gB?.Tier ?? 0} "
                            + (live ? "live" : "DEAD")
                            + (lane.SaturatedEpochs > 0
                                ? $" sat×{lane.SaturatedEpochs}" : "")
                            + $"  gates: {Owner(gA)} / {Owner(gB)}");
                    }
                    break;
                }
```

Add to the help block: `elanes — every lane with gate tiers, owners, liveness, saturation`. In `EpochMapView.cs`'s lanes mode, render dead lanes distinctly (e.g. `·` instead of the live lane glyph — follow the file's existing glyph conventions).

- [ ] **Step 2: Eyeball run** (bash):

```bash
printf 'egen\nerun\nelanes\nemap lanes\nquit\n' | dotnet run --project src/Inspector
```

(Adjust `egen`/`erun` to the REPL's actual generate/run commands — the help header lists them.) Confirm: no all-pairs webs in `emap lanes`, `elanes` shows mixed gate tiers, at least one cross-polity or corp-owned lane in a multi-polity run. **This is the user's REPL eyeball gate — show them the output before proceeding.**

- [ ] **Step 3: Amend the design docs** (same branch — the design is the spec):
- `space-and-travel.md` §Lanes: rewrite to the gate model — paired tiered Gate facilities, reach/capacity/speed from gate tiers, slot budgets per port tier, the detour/congestion builder rule, corp-built non-hostile border lanes, the fee table, and the two implementation clarifications: gates pair at construction by a single funder (a destroyed far gate leaves the survivor standing — the half-built state), and length-as-exposure piracy.
- `infrastructure.md`: add the Gate row to the facility table (Support family, "lane terminus; reach/capacity by tier; one per lane end", siting "port systems only, gate-slot budget = tier × GateSlotsPerPortTier").
- `corporations.md`: freight-line acts gain "builds gate pairs across profitable non-hostile borders; owns and tolls them".
- `markets.md`: tariffs collect at the entering gate (LaneFees), not at an abstract market boundary; charged once per border crossing.

- [ ] **Step 4: Re-freeze the golden.** Temporarily add to `GoldenTests.ReferenceArtifact_MatchesTheFrozenGolden`, before the assert:

```csharp
        // TEMP regeneration — delete before commit
        File.WriteAllText(@"..\..\..\Goldens\slice-b-artifact-seed42.txt",
            ArtifactSerializer.ToText(state));
```

Run the single test once, delete the line, run the full suite:

```bash
dotnet test StarSystemGeneration.sln
```

Expected: **everything green**, including GoldenTests and DeterminismTests.

- [ ] **Step 5: Update `docs/HANDOFF.md`** — lane economics landed on `lane-economics`, spec + plan paths, red window closed, next-up unchanged (K2).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(lanes): elanes REPL surface, design docs amended to gate model, golden re-frozen (lane economics red window closed)"
```

Merge decision (to main) is the user's — stop and ask.

---

## Self-review notes

- Spec §6's "emergence gate seeding" is intentionally dropped (Global Constraints deviation 2): no lanes exist at emergence, so there is nothing to seed; the first builder pass handles it.
- Spec §2's staged cross-actor funding is intentionally simplified (deviation 1); the half-built *visual* state survives via gate destruction.
- `LaneMath.InRange`'s pact behavior (one end own) is preserved in both builders.
- The four fee-table rows map to LaneFeeTests' four tests one-to-one.
- Type consistency: `BuildGate` produces `int` facility id, consumed by Tasks 4 and 6; `PairCarriesProfit` extracted in Task 6 only — Task 4 does not reference it.
