# Sim-Economy Deferred Ticket Batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the sim-economy slice's deferred tickets per `docs/superpowers/specs/2026-07-09-econ-deferred-tickets-design.md`: blockade strain as measured, serialized, consequence-bearing state (schema v5); extinction-correct war termination with capture restoration; famine/war-scar stacking; safe capital relocation; shared-front Contested guard; and the mechanical sweep (landless wealth, sentinel, decay coverage, WarStarted dedup close).

**Architecture:** All behavior changes land in the existing phase files (`src/Core/Galaxy/Sim/IncomePhase.cs`, `ResolutionPhase.cs`, `AllocationPhase.cs`, `ActionPhase.cs`) plus one new `Polity` field. The strain floor constant moves to `Economy.cs` (shared by income event, weariness, and REPL). Serializer bumps to schema v5 in task 8 (single golden re-freeze); REPL surfacing + acceptance close in task 9.

**Tech Stack:** C# / .NET (Core is netstandard2.1, **no external dependencies**, Unity-compatible); xUnit; line-based invariant-culture serializer.

## Global Constraints

- Gate for every task: `dotnet test StarSystemGeneration.sln` from repo root. Report the verbatim summary line. NEVER kill processes to unblock a gate; stop and report instead.
- No Unity project changes in this batch; Unity edit-mode gates are NOT required.
- Determinism discipline: fixed iteration order — cells by `SpiralIndex`, polities by `Id`, wars by `Id`. No new rolls are introduced by this batch; no RollChannel changes.
- Nothing in sim state may go negative or NaN — including the new `BlockadeLoss`.
- **Red window: tasks 1–8.** `SerializerTests.GoldenSnapshot_SmallGalaxyHeader` (event/polity count literals) MAY be temporarily red from task 1; mark any temporarily-failing golden/outcome-literal test with a `// TICKETMIGRATION` comment. The window closes at task 8 (schema v5 + single golden re-freeze). **Shape-band and invariant tests are NOT part of the window** (`ClaimedFraction…`, `ShapeBands_ReferenceConfig`, `WarOutcomes_BothPathsOccur_AcrossSeeds`, `NothingNegativeOrNaN…`): if one goes red, STOP, report DONE_WITH_CONCERNS with the observed numbers, and let the controller adjudicate — never widen a band or seed range yourself.
- Brief-provided tests can fail against brief-provided code (plan bugs): diagnose, apply the MINIMAL fix, prefer fixing implementation over weakening asserts, report DONE_WITH_CONCERNS with the arithmetic.
- Working branch: `econ-tickets` off `main`.
- Do not let `unity/ProjectSettings/*` or `.superpowers/*` working-tree churn ride into any commit — stage only the files each task names.

---

### Task 1: Blockade-loss classification + `Polity.BlockadeLoss`

**Files:**
- Modify: `src/Core/Galaxy/Polity.cs`
- Modify: `src/Core/Galaxy/Sim/Economy.cs`
- Modify: `src/Core/Galaxy/Sim/IncomePhase.cs` (full rewrite below)
- Test: `tests/Core.Tests/Galaxy/IncomePhaseTests.cs` (append)
- Test: `tests/Core.Tests/Galaxy/EconomyInvariantTests.cs` (extend two tests)

**Interfaces:**
- Consumes: `Economy.Route`, `Economy.Passable` (unchanged).
- Produces (later tasks rely on these exact names):
  - `Polity.BlockadeLoss` (`double`, default 0): last-epoch blockade-induced loss, reset every income phase, zero for extinct/landless polities.
  - `Economy.TradeBlockedFloor` (`public const double = 2.0`): shared strain floor (event + weariness + REPL).
  - `IncomePhase` no longer has a `HasLiveWar` method or a private `TradeBlockedFloor`.

- [ ] **Step 1: Write the failing tests.** Append to `tests/Core.Tests/Galaxy/IncomePhaseTests.cs` (inside the existing class):

```csharp
    /// <summary>Neutral-polity corridor severed by third-party contested cells (the
    /// parent spec §5 canonical scenario): P0 fights no war, yet its producer→consumer
    /// route is blockaded. Strain must accrue and, above the floor, fire TradeBlocked.
    /// Arithmetic (defaults: ProvisionsPerPop 0.5, density 0.5, terran affinity 1.0):
    /// consumer (dev 1, pop 8) nets 0.5 − 4.0 = −3.5; producer (dev 5, pop 1) nets
    /// 2.5 − 0.5 = +2.0, reachable only through the contested q=−1 column → the whole
    /// unfilled 3.5 classifies as blockade loss (a surplus IS reachable unblockaded).</summary>
    private static GalaxySkeleton SeveredNeutralFixture(double consumerPop)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        s.Species.Add(new SpeciesProfile
        {
            Id = 0, Name = "S0", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
            Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
        });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -2, CapitalR = 0 });
        foreach (var (q, r) in new[] { (-2, 0), (0, 0) })
        {
            var c = s.CellAt(new HexCoordinate(q, r));
            c.OwnerPolityId = 0; c.PopulationSpeciesId = 0;
        }
        var consumer = s.CellAt(new HexCoordinate(-2, 0));
        consumer.Population = consumerPop; consumer.DevelopmentTier = 1;
        var producer = s.CellAt(new HexCoordinate(0, 0));
        producer.Population = 1.0; producer.DevelopmentTier = 5;
        // Sever the corridor with third-party contest (P0 is at war with nobody):
        // the full q=−1 column cuts the radius-3 disc in two.
        foreach (var c in s.Cells.Where(c => c.Q == -1)) c.Contested = true;
        return s;
    }

    [Fact]
    public void NeutralPolity_SeveredByThirdPartyContest_AccruesStrain_AndFiresTradeBlocked()
    {
        var s = SeveredNeutralFixture(consumerPop: 8.0);
        IncomePhase.Run(s, 0);
        Assert.True(s.Polities[0].BlockadeLoss > Economy.TradeBlockedFloor,
            $"blockade loss {s.Polities[0].BlockadeLoss} must exceed the event floor");
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.TradeBlocked && e.ActorPolityId == 0);
    }

    [Fact]
    public void WarringPolity_WithNoSurplusAnywhere_AccruesNoStrain_NoEvent()
    {
        var s = SeveredNeutralFixture(consumerPop: 8.0);
        // Remove the producer's output entirely and put P0 at war: scarcity while at
        // war must NOT read as blockade (the old HasLiveWar-gated false positive).
        var producer = s.CellAt(new HexCoordinate(0, 0));
        producer.DevelopmentTier = 0; producer.Population = 0.0;
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 0, CapitalQ = 3, CapitalR = 0 });
        var enemyCell = s.CellAt(new HexCoordinate(3, 0));
        enemyCell.OwnerPolityId = 1; enemyCell.DevelopmentTier = 1;
        enemyCell.Population = 0.5; enemyCell.PopulationSpeciesId = 0;
        var war = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 0 };
        war.GoalCells.Add(enemyCell.Coord);
        war.FrontCells.Add(enemyCell.Coord);
        s.Wars.Add(war);
        IncomePhase.Run(s, 0);
        Assert.Equal(0.0, s.Polities[0].BlockadeLoss);
        Assert.DoesNotContain(s.Events, e => e.Type == GalaxyEventType.TradeBlocked);
    }

    /// <summary>Cross-polity classification: two non-belligerents with complementary
    /// provisions positions whose capital-capital path is severed by third-party
    /// contest. Arithmetic: P0 (0,0) dev 5 pop 1 nets +2.0 (its (1,0) dev 1 pop 1 cell
    /// nets 0); P1 (3,0) dev 1 pop 4 nets −1.5 ((2,0) dev 0 pop 0 nets 0) →
    /// give = 1.5, blocked by the contested q=1 column → both sides accrue 1.5
    /// (below the 2.0 event floor: strain state without the event).</summary>
    [Fact]
    public void CrossPolityTrade_BlockedPath_AccruesStrainOnBothPartners()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        for (int i = 0; i < 2; i++)
            s.Species.Add(new SpeciesProfile
            {
                Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
                Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
            });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = 0, CapitalR = 0 });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 3, CapitalR = 0 });
        var p0Cap = s.CellAt(new HexCoordinate(0, 0));
        p0Cap.OwnerPolityId = 0; p0Cap.DevelopmentTier = 5; p0Cap.Population = 1.0; p0Cap.PopulationSpeciesId = 0;
        var p0Edge = s.CellAt(new HexCoordinate(1, 0));
        p0Edge.OwnerPolityId = 0; p0Edge.DevelopmentTier = 1; p0Edge.Population = 1.0; p0Edge.PopulationSpeciesId = 0;
        var p1Edge = s.CellAt(new HexCoordinate(2, 0));
        p1Edge.OwnerPolityId = 1; p1Edge.DevelopmentTier = 0; p1Edge.Population = 0.0; p1Edge.PopulationSpeciesId = 1;
        var p1Cap = s.CellAt(new HexCoordinate(3, 0));
        p1Cap.OwnerPolityId = 1; p1Cap.DevelopmentTier = 1; p1Cap.Population = 4.0; p1Cap.PopulationSpeciesId = 1;
        // Sever every capital-capital path: contest the full q=1 and q=2 columns
        // (the polities still share the (1,0)-(2,0) border, so trade is attempted).
        foreach (var c in s.Cells.Where(c => c.Q == 1 || c.Q == 2)) c.Contested = true;
        IncomePhase.Run(s, 0);
        Assert.Equal(1.5, s.Polities[0].BlockadeLoss, 10);
        Assert.Equal(1.5, s.Polities[1].BlockadeLoss, 10);
        Assert.DoesNotContain(s.Events, e => e.Type == GalaxyEventType.TradeBlocked);
    }
```

In `tests/Core.Tests/Galaxy/EconomyInvariantTests.cs`:

1. In `NothingNegativeOrNaN_AcrossSeeds`, inside the `foreach (var p in s.Polities)` loop, add:

```csharp
                Assert.True(p.BlockadeLoss >= 0 && !double.IsNaN(p.BlockadeLoss));
```

2. In `Blockade_ReducesDeliveredFlow_ConstructedTwin`, after the existing `Assert.True(blockedPop < openPop, ...)`, add:

```csharp
        Assert.Equal(0.0, open.Polities[0].BlockadeLoss);
        Assert.True(blocked.Polities[0].BlockadeLoss > 0,
            "the severed twin's unfilled need classifies as blockade loss (surplus exists unblockaded)");
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~IncomePhaseTests|FullyQualifiedName~EconomyInvariantTests" 2>&1 | tail -15`
Expected: FAIL — compile errors (`BlockadeLoss`, `Economy.TradeBlockedFloor` not defined).

- [ ] **Step 3: Implement.**

`src/Core/Galaxy/Polity.cs` — append inside the class, after `ExoticsBalance`:

```csharp
    /// <summary>Last epoch's blockade-induced routing loss (deferred-tickets spec §3):
    /// deficits/trade a blockade (not mere scarcity) denied. Reset each income phase;
    /// feeds TradeBlocked, war-weariness hardship, and (stage 4) relations.</summary>
    public double BlockadeLoss { get; set; }
```

`src/Core/Galaxy/Sim/Economy.cs` — add after the `DisplayBaseline` field:

```csharp
    /// <summary>Blockade-strain floor (deferred-tickets spec §3): BlockadeLoss above
    /// this fires TradeBlocked and counts as war-weariness hardship.</summary>
    public const double TradeBlockedFloor = 2.0;
```

`src/Core/Galaxy/Sim/IncomePhase.cs` — replace the entire file with:

```csharp
using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 1 (economy spec §3/§5): per-cell production, intra-polity
/// surplus→deficit flows with throughput, cross-polity trade, shortage effects,
/// and population dynamics. Deterministic: polities by id, cells by spiral index.</summary>
public static class IncomePhase
{
    private const double Eps = 1e-9;
    private const double FamineEventFloor = 0.5;
    private const double PopGrowthBase = 0.05;
    private const double FamineShrink = 0.8;
    private const double ScarShrink = 0.95;

    /// <summary>Transit predicate ignoring blockades: used to classify a failed route
    /// as blockade-induced (a target IS reachable unblockaded) vs. plain scarcity
    /// (deferred-tickets spec §3). Only void is impassable.</summary>
    private static readonly Func<RegionCell, bool> Unblockaded = c => !c.IsVoid;

    public static void Run(GalaxySkeleton s, int epoch)
    {
        foreach (var cell in s.Cells) cell.RouteThroughput = 0.0;
        // Strain is a last-epoch snapshot like the balances; extinct and landless
        // polities hold zero (deferred-tickets spec §3).
        foreach (var polity in s.Polities) polity.BlockadeLoss = 0.0;

        // Per-polity, per-good remaining surplus/deficit after internal routing,
        // kept for the cross-polity pass. [polityId][good] → amount (+surplus/−deficit).
        var remaining = new Dictionary<int, double[]>();
        // Cells whose provisions deficit went unfilled (famine candidates).
        var unfed = new Dictionary<int, List<(RegionCell cell, double lack)>>();

        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) continue;
            var species = s.Species[polity.SpeciesId];
            var owned = EpochSim.Owned(s, polity);
            if (owned.Count == 0) continue;
            var passable = Economy.Passable(s, polity.Id);
            double[] totals = new double[3];
            unfed[polity.Id] = new List<(RegionCell, double)>();

            foreach (var good in new[] { Commodity.Provisions, Commodity.Ore })
            {
                var net = new Dictionary<int, double>();
                foreach (var cell in owned)
                    net[cell.SpiralIndex] = Economy.Produced(good, species, cell)
                                          - Economy.Consumed(good, s.Config, species, cell);
                totals[(int)good] = Sum(net);

                // Fill deficits from nearest surplus, cheapest-first by BFS distance.
                foreach (var deficit in owned)
                {
                    double need = -net[deficit.SpiralIndex];
                    Func<RegionCell, bool> isSurplus =
                        c => c.OwnerPolityId == polity.Id
                             && net.TryGetValue(c.SpiralIndex, out var v) && v > Eps;
                    while (need > Eps)
                    {
                        var path = Economy.Route(s, deficit, isSurplus, passable);
                        if (path == null)
                        {
                            // Blockade-induced only if a surplus IS reachable ignoring
                            // blockades; otherwise nothing exists for a blockade to deny.
                            if (Economy.Route(s, deficit, isSurplus, Unblockaded) != null)
                                polity.BlockadeLoss += need;
                            break;
                        }
                        var source = path[path.Count - 1];
                        double amount = Math.Min(need, net[source.SpiralIndex]);
                        net[source.SpiralIndex] -= amount;
                        net[deficit.SpiralIndex] += amount;
                        need -= amount;
                        foreach (var transit in path) transit.RouteThroughput += amount;
                    }
                    if (good == Commodity.Provisions && need > Eps)
                        unfed[polity.Id].Add((deficit, need));
                }
                // remaining surplus for cross-polity trade:
                double surplus = 0;
                foreach (var v in net.Values) if (v > Eps) surplus += v;
                double deficitLeft = 0;
                foreach (var v in net.Values) if (v < -Eps) deficitLeft += -v;
                GetRemaining(remaining, polity.Id)[(int)good] = surplus - deficitLeft;
            }

            // Exotics: produced at cells, consumed at polity level (tech, allocation phase).
            double exotics = 0;
            foreach (var cell in owned) exotics += Economy.Produced(Commodity.Exotics, species, cell);
            totals[(int)Commodity.Exotics] = exotics;
            GetRemaining(remaining, polity.Id)[(int)Commodity.Exotics] = exotics;

            polity.ProvisionsBalance = totals[0];
            polity.OreBalance = totals[1];
            polity.ExoticsBalance = totals[2];
        }

        CrossPolityTrade(s, remaining);
        ApplyPopulationAndEvents(s, epoch, unfed);
    }

    private static double[] GetRemaining(Dictionary<int, double[]> map, int id)
    {
        if (!map.TryGetValue(id, out var arr)) { arr = new double[3]; map[id] = arr; }
        return arr;
    }

    private static double Sum(Dictionary<int, double> net)
    {
        double t = 0; foreach (var v in net.Values) t += v; return t;
    }

    /// <summary>Matched complementary surpluses between graph-adjacent non-belligerents
    /// convert to wealth for both sides (spec §5); throughput rides the capital-capital
    /// path passable for BOTH parties. A blocked trade (path exists unblockaded but not
    /// under blockade rules) strains both partners.</summary>
    private static void CrossPolityTrade(GalaxySkeleton s, Dictionary<int, double[]> remaining)
    {
        for (int a = 0; a < s.Polities.Count; a++)
        {
            var pa = s.Polities[a];
            if (pa.Extinct || !remaining.ContainsKey(a)) continue;
            for (int b = a + 1; b < s.Polities.Count; b++)
            {
                var pb = s.Polities[b];
                if (pb.Extinct || !remaining.ContainsKey(b)) continue;
                if (s.AtWar(a, b)) continue;
                if (!SharesBorder(s, a, b)) continue;

                for (int g = 0; g < 3; g++)
                {
                    double give = Math.Min(Math.Max(0, remaining[a][g]), Math.Max(0, -remaining[b][g]))
                                + Math.Min(Math.Max(0, remaining[b][g]), Math.Max(0, -remaining[a][g]));
                    if (give <= Eps) continue;
                    var capA = s.CellAt(pa.CapitalCoord);
                    var capB = s.CellAt(pb.CapitalCoord);
                    Func<RegionCell, bool> isCapB = c => c.SpiralIndex == capB.SpiralIndex;
                    var path = Economy.Route(s, capA, isCapB,
                        c => Economy.Passable(s, a)(c) && Economy.Passable(s, b)(c));
                    if (path == null)
                    {
                        if (Economy.Route(s, capA, isCapB, Unblockaded) != null)
                        {
                            pa.BlockadeLoss += give;
                            pb.BlockadeLoss += give;
                        }
                        continue;
                    }
                    double wealth = give * s.Config.TradeIncomeWeight;
                    pa.Wealth += wealth;
                    pb.Wealth += wealth;
                    remaining[a][g] -= Math.Sign(remaining[a][g]) * Math.Min(Math.Abs(remaining[a][g]), give);
                    remaining[b][g] -= Math.Sign(remaining[b][g]) * Math.Min(Math.Abs(remaining[b][g]), give);
                    foreach (var transit in path) transit.RouteThroughput += give;
                }
            }
        }
    }

    private static bool SharesBorder(GalaxySkeleton s, int a, int b)
    {
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId != a) continue;
            foreach (var nc in HexGrid.Neighbors(cell.Coord))
                if (s.TryGetCell(nc, out var n) && n.OwnerPolityId == b) return true;
        }
        return false;
    }

    private static void ApplyPopulationAndEvents(GalaxySkeleton s, int epoch,
        Dictionary<int, List<(RegionCell cell, double lack)>> unfed)
    {
        foreach (var polity in s.Polities)
        {
            if (polity.Extinct || !unfed.ContainsKey(polity.Id)) continue;
            var starving = unfed[polity.Id];
            double famineMagnitude = 0, worstLack = 0;
            RegionCell? worst = null;
            foreach (var (cell, lack) in starving)
            {
                cell.Population = Math.Max(0, cell.Population * FamineShrink);
                famineMagnitude += lack;
                if (worst == null || lack > worstLack) { worst = cell; worstLack = lack; }
            }
            if (famineMagnitude > FamineEventFloor && worst != null)
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.Famine,
                    ActorPolityId = polity.Id, Q = worst.Q, R = worst.R,
                    Magnitude = famineMagnitude,
                });

            // Strain above the floor fires TradeBlocked for ANY polity — no live-war
            // gate: a neutral bystander severed by a third-party front qualifies, a
            // warring polity with a plain no-surplus famine does not (spec §3).
            if (polity.BlockadeLoss > Economy.TradeBlockedFloor)
            {
                var cap = s.CellAt(polity.CapitalCoord);
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.TradeBlocked,
                    ActorPolityId = polity.Id, Q = cap.Q, R = cap.R,
                    Magnitude = polity.BlockadeLoss,
                });
            }
        }

        // Growth + war-scar shrink for all owned cells not starving this epoch.
        var starvingSet = new HashSet<RegionCell>();
        foreach (var list in unfed.Values) foreach (var (cell, _) in list) starvingSet.Add(cell);
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId < 0 || starvingSet.Contains(cell)) continue;
            double cap = 1.0 + cell.DevelopmentTier;
            if (cell.Population < cap)
                cell.Population = Math.Min(cap,
                    cell.Population + PopGrowthBase * (1 + cell.DevelopmentTier) * 0.5);
            if (cell.Contested && cell.WarScarred)
                cell.Population = Math.Max(0, cell.Population * ScarShrink);
        }
    }
}
```

Notes for the implementer:
- The old `HasLiveWar` method and the private `TradeBlockedFloor` const are gone; the `blockedLoss` dictionary is replaced by direct `polity.BlockadeLoss` accrual.
- `isSurplus` is hoisted out of the `while` loop (same semantics; it captures `net`, which mutates as fills land — intended, identical to the old inline lambda).

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: the three new IncomePhase tests + extended invariant tests PASS. `GoldenSnapshot_SmallGalaxyHeader` MAY now fail on `Events.Count` (TradeBlocked events can fire at seed 7) — if so, add `// TICKETMIGRATION: golden re-freezes in task 8 (blockade classification changed the event stream)` above the failing assert and report which literal moved. Everything else green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Polity.cs src/Core/Galaxy/Sim/Economy.cs src/Core/Galaxy/Sim/IncomePhase.cs tests/Core.Tests/Galaxy/IncomePhaseTests.cs tests/Core.Tests/Galaxy/EconomyInvariantTests.cs
git commit -m "feat(econ): blockade strain measured per polity - re-route classification, HasLiveWar gate removed"
```

---

### Task 2: Weariness hardship hook for blockade strain

**Files:**
- Modify: `src/Core/Galaxy/Sim/ResolutionPhase.cs` (the `Weariness` method only)
- Test: `tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs` (append)

**Interfaces:**
- Consumes: `Polity.BlockadeLoss`, `Economy.TradeBlockedFloor` (task 1).
- Produces: nothing new — behavior change only.

- [ ] **Step 1: Write the failing test.** Append to `ResolutionPhaseTests`:

```csharp
    [Fact]
    public void BlockadeStrain_CountsAsHardship_ForWeariness()
    {
        var strained = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        var relaxed = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        strained.Polities[0].BlockadeLoss = Economy.TradeBlockedFloor + 1.0;
        ResolutionPhase.Run(strained, 0);
        ResolutionPhase.Run(relaxed, 0);
        // Identical seeds → identical battle rolls; only the 1.5× hardship multiplier differs.
        Assert.Equal(relaxed.Wars[0].AttackerWeariness * 1.5, strained.Wars[0].AttackerWeariness, 10);
        Assert.Equal(relaxed.Wars[0].DefenderWeariness, strained.Wars[0].DefenderWeariness, 10);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~BlockadeStrain_CountsAsHardship" 2>&1 | tail -8`
Expected: FAIL — the two weariness values are equal (no strain hardship yet).

- [ ] **Step 3: Implement.** In `ResolutionPhase.cs`, replace the `Weariness` method:

```csharp
    private static double Weariness(GalaxySkeleton s, Polity p, int cellsLostThisEpoch)
    {
        // Hardship: commodity deficits or blockade strain above the shared floor
        // (deferred-tickets spec §3) — blockading an enemy hastens their breaking.
        bool shortages = p.ProvisionsBalance < 0 || p.OreBalance < 0
            || p.BlockadeLoss > Economy.TradeBlockedFloor;
        return s.Config.WarWearinessRate
            * (shortages ? 1.5 : 1.0) * (1.0 + 0.2 * cellsLostThisEpoch);
    }
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: new test PASSES. Strain now shifts war termination timing at built-galaxy scale — golden may (further) drift inside the red window; shape-band tests must stay green (STOP and report if not).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/ResolutionPhase.cs tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs
git commit -m "feat(econ): blockade strain counts as war-weariness hardship"
```

---

### Task 3: War termination — extinction rules, skip-front, DefenderVictory restoration

**Files:**
- Modify: `src/Core/Galaxy/Sim/ResolutionPhase.cs` (the `Run` method)
- Test: `tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs` (append + strengthen one assert)

**Interfaces:**
- Consumes: existing `Broke`, `Weariness`, `HandleCapitalAndExtinction`, `FrontInOrder`, `LiveWarCount`.
- Produces: outcome-labeling contract later tasks/tests rely on — defender extinct → `AttackerVictory`; attacker extinct → `DefenderVictory`; both → `WhitePeace`; `DefenderVictory` reverts attacker-held front cells to the defender with `CellTaken` events (actor: defender, magnitude 0) and `AttackerCellsLost` increments.

- [ ] **Step 1: Write the failing tests.** Append to `ResolutionPhaseTests`:

```csharp
    [Fact]
    public void DefenderExtinct_LabelsAttackerVictory_NotWhitePeace()
    {
        var s = AtWarFixture(attackerStock: 100.0, defenderStock: 50.0);
        // Defender annihilated before this war resolves (e.g. a lower-id war this
        // epoch) while the attacker is simultaneously past its own breaking point:
        // extinction must dominate the label (old rules said WhitePeace).
        foreach (var c in s.Cells.Where(c => c.OwnerPolityId == 1)) c.OwnerPolityId = 0;
        s.Polities[1].Extinct = true;
        s.Wars[0].AttackerWeariness = 5.0;
        ResolutionPhase.Run(s, 0);
        Assert.True(s.Wars[0].Ended);
        Assert.Equal(WarOutcome.AttackerVictory, s.Wars[0].Outcome);
        // Skip-front: a war with an extinct side fights no battles and accrues no weariness.
        Assert.Equal(5.0, s.Wars[0].AttackerWeariness, 10);
    }

    [Fact]
    public void BothSidesExtinct_LabelsWhitePeace()
    {
        var s = AtWarFixture();
        foreach (var c in s.Cells.Where(c => c.OwnerPolityId >= 0)) c.OwnerPolityId = -1;
        s.Polities[0].Extinct = true;
        s.Polities[1].Extinct = true;
        ResolutionPhase.Run(s, 0);
        Assert.True(s.Wars[0].Ended);
        Assert.Equal(WarOutcome.WhitePeace, s.Wars[0].Outcome);
    }

    [Fact]
    public void DefenderVictory_RestoresCapturedFrontCells()
    {
        var s = AtWarFixture(attackerStock: 0.2, defenderStock: 50.0);
        var goal = s.CellAt(new HexCoordinate(1, 0));
        goal.OwnerPolityId = 0;              // attacker captured the goal in an earlier epoch
        s.Wars[0].AttackerWeariness = 5.0;   // attacker breaks now; defender is healthy
        ResolutionPhase.Run(s, 0);
        Assert.Equal(WarOutcome.DefenderVictory, s.Wars[0].Outcome);
        Assert.Equal(1, goal.OwnerPolityId);   // capture returned to the defender
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.CellTaken
            && e.ActorPolityId == 1 && e.TargetPolityId == 0 && e.Q == 1 && e.R == 0);
    }
```

Also strengthen `ExtinctAttacker_CannotKeepFightingOrAnnexTerritory`: after the existing `Assert.NotEqual(WarOutcome.AttackerVictory, s.Wars[1].Outcome);` add:

```csharp
        Assert.Equal(WarOutcome.DefenderVictory, s.Wars[1].Outcome);
```

Note: `BothSidesExtinct_LabelsWhitePeace` passes under the old rules too (both-broke → WhitePeace) — it locks the contract in rather than discriminating; the other two must fail pre-fix.

- [ ] **Step 2: Run tests to verify the two discriminating tests fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ResolutionPhaseTests" 2>&1 | tail -12`
Expected: `DefenderExtinct_LabelsAttackerVictory_NotWhitePeace` FAILS (outcome is WhitePeace) and `DefenderVictory_RestoresCapturedFrontCells` FAILS (goal stays owner 0 unless a battle happened to flip it — the outcome assert alone may pass; the owner assert must fail on the no-flip path. If both asserts pass, STOP and report: the roll flipped the cell, choose a different fixture seed via `MasterSeed` and report the change).

- [ ] **Step 3: Implement.** In `ResolutionPhase.cs`, replace the entire `Run` method:

```csharp
    public static void Run(GalaxySkeleton s, int epoch)
    {
        foreach (var war in s.Wars)
        {
            if (war.Ended) continue;
            var attacker = s.Polities[war.AttackerId];
            var defender = s.Polities[war.DefenderId];
            var aSpecies = s.Species[attacker.SpeciesId];
            var dSpecies = s.Species[defender.SpeciesId];

            // Extinct polities fight no fronts (deferred-tickets spec §4): a war whose
            // side died earlier (lower-id war this epoch, or any prior epoch) goes
            // straight to termination.
            if (!attacker.Extinct && !defender.Extinct)
            {
                double aCommit = CommitFraction * attacker.MilitaryStockpile / Math.Max(1, LiveWarCount(s, attacker.Id));
                double dCommit = CommitFraction * defender.MilitaryStockpile / Math.Max(1, LiveWarCount(s, defender.Id));
                double aStrength = Economy.WarStrength(aCommit, attacker.TechTier, aSpecies.Militancy);
                double dStrength = Economy.WarStrength(dCommit, defender.TechTier, dSpecies.Militancy);

                int aLostThisEpoch = 0, dLostThisEpoch = 0;
                foreach (var coord in FrontInOrder(s, war))
                {
                    var cell = s.CellAt(coord);
                    cell.Contested = true;
                    cell.WarScarred = true;
                    bool attackerHolds = cell.OwnerPolityId == war.AttackerId;
                    double holderStrength = attackerHolds ? aStrength : dStrength;
                    double takerStrength = attackerHolds ? dStrength : aStrength;
                    double pTake = 0.5 * Clamp(
                        0.5 + 0.5 * (takerStrength - holderStrength)
                                   / (takerStrength + holderStrength + 1.0), 0.05, 0.95);
                    var ctx = new RollContext(s.Config.MasterSeed, cell.Coord);
                    if (ctx.NextDouble(RollChannel.SimBattle, epoch, war.Id) < pTake)
                    {
                        int newOwner = attackerHolds ? war.DefenderId : war.AttackerId;
                        int oldOwner = cell.OwnerPolityId;
                        cell.OwnerPolityId = newOwner;
                        if (oldOwner == war.AttackerId) { aLostThisEpoch++; war.AttackerCellsLost++; }
                        else { dLostThisEpoch++; war.DefenderCellsLost++; }
                        s.Events.Add(new GalaxyEvent
                        {
                            Epoch = epoch, Type = GalaxyEventType.CellTaken,
                            ActorPolityId = newOwner, TargetPolityId = oldOwner,
                            Q = cell.Q, R = cell.R,
                            Magnitude = Math.Abs(takerStrength - holderStrength),
                        });
                        HandleCapitalAndExtinction(s, epoch, s.Polities[oldOwner], s.Polities[newOwner], cell);
                    }
                }

                attacker.MilitaryStockpile = Math.Max(0, attacker.MilitaryStockpile - aCommit * AttritionRate);
                defender.MilitaryStockpile = Math.Max(0, defender.MilitaryStockpile - dCommit * AttritionRate);

                war.AttackerWeariness += Weariness(s, attacker, aLostThisEpoch);
                war.DefenderWeariness += Weariness(s, defender, dLostThisEpoch);
            }

            // Termination (deferred-tickets spec §4): an extinct side loses outright;
            // otherwise the weariness/stockpile break logic decides.
            WarOutcome outcome;
            if (attacker.Extinct && defender.Extinct) outcome = WarOutcome.WhitePeace;
            else if (defender.Extinct) outcome = WarOutcome.AttackerVictory;
            else if (attacker.Extinct) outcome = WarOutcome.DefenderVictory;
            else
            {
                bool aBroke = Broke(war.AttackerWeariness, aSpecies, attacker);
                bool dBroke = Broke(war.DefenderWeariness, dSpecies, defender);
                if (!aBroke && !dBroke) continue;
                outcome = aBroke && dBroke ? WarOutcome.WhitePeace
                    : aBroke ? WarOutcome.DefenderVictory : WarOutcome.AttackerVictory;
            }

            if (outcome == WarOutcome.AttackerVictory)
                foreach (var gc in war.GoalCells)
                {
                    var cell = s.CellAt(gc);
                    if (cell.OwnerPolityId == war.DefenderId)
                    {
                        cell.OwnerPolityId = war.AttackerId;
                        war.DefenderCellsLost++;
                        s.Events.Add(new GalaxyEvent
                        {
                            Epoch = epoch, Type = GalaxyEventType.CellTaken,
                            ActorPolityId = war.AttackerId, TargetPolityId = war.DefenderId,
                            Q = cell.Q, R = cell.R, Magnitude = 0,
                        });
                        HandleCapitalAndExtinction(s, epoch, defender, attacker, cell);
                    }
                }
            else if (outcome == WarOutcome.DefenderVictory)
                // Settlement (deferred-tickets spec §4): captures return — front cells
                // are by construction originally the defender's. Also cleans zombie
                // cells captured by an attacker that later went extinct.
                foreach (var fc in war.FrontCells)
                {
                    var cell = s.CellAt(fc);
                    if (cell.OwnerPolityId == war.AttackerId)
                    {
                        cell.OwnerPolityId = war.DefenderId;
                        war.AttackerCellsLost++;
                        s.Events.Add(new GalaxyEvent
                        {
                            Epoch = epoch, Type = GalaxyEventType.CellTaken,
                            ActorPolityId = war.DefenderId, TargetPolityId = war.AttackerId,
                            Q = cell.Q, R = cell.R, Magnitude = 0,
                        });
                        HandleCapitalAndExtinction(s, epoch, attacker, defender, cell);
                    }
                }
            // WhitePeace: uti possidetis — you keep what you hold.

            foreach (var fc in war.FrontCells) s.CellAt(fc).Contested = false;
            war.Ended = true;
            war.Outcome = outcome;
            var origin = s.CellAt(war.GoalCells[0]);
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.WarEnded,
                ActorPolityId = war.AttackerId, TargetPolityId = war.DefenderId,
                Q = origin.Q, R = origin.R, Detail = (int)outcome,
                Magnitude = war.AttackerWeariness + war.DefenderWeariness,
            });
        }
    }
```

(The contest/attrition/weariness block is the existing code verbatim, now wrapped in the `!Extinct` guard; only the termination and settlement logic below it is new.)

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: all `ResolutionPhaseTests` PASS including the strengthened ghost-attacker assert. **Watch item:** `EconomyInvariantTests.WarOutcomes_BothPathsOccur_AcrossSeeds` — extinction-relabeling can remove formerly-mislabeled white peaces. If `whitePeaces == 0` across seeds 40–49 now, STOP and report DONE_WITH_CONCERNS with the per-seed outcome counts (do not widen the seed range yourself).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/ResolutionPhase.cs tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs
git commit -m "feat(econ): extinction-correct war termination + DefenderVictory capture restoration"
```

---

### Task 4: Famine + war-scar stacking

**Files:**
- Modify: `src/Core/Galaxy/Sim/IncomePhase.cs` (final loop of `ApplyPopulationAndEvents`)
- Test: `tests/Core.Tests/Galaxy/IncomePhaseTests.cs` (append)

**Interfaces:**
- Consumes: task 1's `ApplyPopulationAndEvents` shape.
- Produces: stacking contract — a starving contested+scarred cell nets ×0.8×0.95 = ×0.76 in one epoch; growth still skips starving cells.

- [ ] **Step 1: Write the failing test.** Append to `IncomePhaseTests`:

```csharp
    [Fact]
    public void FamineAndWarScar_StackOnTheSameCell()
    {
        var s = Fixture();
        var e = s.CellAt(new HexCoordinate(2, 0));
        e.Population = 10.0; e.DevelopmentTier = 1;   // starving, as in the famine test
        e.Contested = true; e.WarScarred = true;      // and besieged
        IncomePhase.Run(s, 0);
        // Famine ×0.8 then war-scar ×0.95: separate population pressures compound
        // (deferred-tickets spec §5).
        Assert.Equal(10.0 * 0.8 * 0.95, e.Population, 10);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~FamineAndWarScar_StackOnTheSameCell" 2>&1 | tail -8`
Expected: FAIL — population is 8.0 (famine only; the starving cell skipped the scar branch).

- [ ] **Step 3: Implement.** In `IncomePhase.ApplyPopulationAndEvents`, replace the final loop:

```csharp
        // Growth for fed cells; war-scar shrink for ALL besieged cells — famine and
        // siege are separate pressures and stack (deferred-tickets spec §5). The
        // growth guard is untouched: a starving cell never grows, a fed cell never
        // shrinks from feeding.
        var starvingSet = new HashSet<RegionCell>();
        foreach (var list in unfed.Values) foreach (var (cell, _) in list) starvingSet.Add(cell);
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId < 0) continue;
            if (!starvingSet.Contains(cell))
            {
                double cap = 1.0 + cell.DevelopmentTier;
                if (cell.Population < cap)
                    cell.Population = Math.Min(cap,
                        cell.Population + PopGrowthBase * (1 + cell.DevelopmentTier) * 0.5);
            }
            if (cell.Contested && cell.WarScarred)
                cell.Population = Math.Max(0, cell.Population * ScarShrink);
        }
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: new test PASSES; shape-band tests stay green (famine event counts are unaffected — scar shrink is not a famine; STOP and report if any band breaks).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/IncomePhase.cs tests/Core.Tests/Galaxy/IncomePhaseTests.cs
git commit -m "feat(econ): famine and war-scar shrink stack on besieged starving cells"
```

---

### Task 5: Capital relocation prefers uncontested cells

**Files:**
- Modify: `src/Core/Galaxy/Sim/ResolutionPhase.cs` (`HandleCapitalAndExtinction`)
- Test: `tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs` (append)

**Interfaces:**
- Consumes: task 3's `Run` (calls `HandleCapitalAndExtinction` from battle, annexation, and restoration paths).
- Produces: relocation ordering contract — non-contested first, then highest `DevelopmentTier`, then lowest `SpiralIndex`.

- [ ] **Step 1: Write the failing test.** Append to `ResolutionPhaseTests`:

```csharp
    [Fact]
    public void CapitalRelocation_PrefersUncontestedCells()
    {
        var s = AtWarFixture(attackerStock: 100.0, defenderStock: 0.05);
        // Defender's capital IS the goal cell; its other cells are a contested
        // high-dev cell and a safe low-dev cell. A fleeing government must not
        // relocate into an active battlefield when it has any choice.
        s.Polities[1].CapitalQ = 1; s.Polities[1].CapitalR = 0;
        s.CellAt(new HexCoordinate(2, 0)).Contested = true;   // dev 3 in the fixture
        var safePoor = s.CellAt(new HexCoordinate(3, 0));
        safePoor.OwnerPolityId = 1; safePoor.DevelopmentTier = 1;
        safePoor.Population = 0.5; safePoor.PopulationSpeciesId = 1;
        ResolutionPhase.Run(s, 0);   // defender stockpile 0.05 < break floor → AttackerVictory annexes the capital
        Assert.Equal(WarOutcome.AttackerVictory, s.Wars[0].Outcome);
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.LostCapital && e.TargetPolityId == 1);
        Assert.Equal(new HexCoordinate(3, 0), s.Polities[1].CapitalCoord);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~CapitalRelocation_PrefersUncontestedCells" 2>&1 | tail -8`
Expected: FAIL — capital relocates to (2,0) (highest dev wins regardless of contest).

- [ ] **Step 3: Implement.** In `ResolutionPhase.HandleCapitalAndExtinction`, replace the relocation pick:

```csharp
        if (loser.CapitalCoord.Equals(takenCell.Coord))
        {
            // Refuge ordering (deferred-tickets spec §6): non-contested first, then
            // development tier, then spiral index; a battlefield cell is chosen only
            // when everything owned is contested.
            RegionCell? best = null;
            foreach (var c in EpochSim.Owned(s, loser))
                if (best == null || BetterRefuge(c, best))
                    best = c;
            if (best != null)
            {
                loser.CapitalQ = best.Q;
                loser.CapitalR = best.R;
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.LostCapital,
                    ActorPolityId = victor.Id, TargetPolityId = loser.Id,
                    Q = takenCell.Q, R = takenCell.R,
                });
            }
        }
```

and add the helper below `HandleCapitalAndExtinction`:

```csharp
    private static bool BetterRefuge(RegionCell c, RegionCell best) =>
        (!c.Contested && best.Contested)
        || (c.Contested == best.Contested
            && (c.DevelopmentTier > best.DevelopmentTier
                || (c.DevelopmentTier == best.DevelopmentTier && c.SpiralIndex < best.SpiralIndex)));
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: new test PASSES; golden may drift further inside the window.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/ResolutionPhase.cs tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs
git commit -m "feat(econ): capital relocation prefers uncontested refuge cells"
```

---

### Task 6: Shared-front Contested guard (M-1)

**Files:**
- Modify: `src/Core/Galaxy/Sim/ResolutionPhase.cs` (termination demilitarize line)
- Test: `tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs` (append)

**Interfaces:**
- Consumes: task 3's `Run`.
- Produces: contract — a front cell stays `Contested` at end-of-war if any other live war lists it in `FrontCells`.

- [ ] **Step 1: Write the failing test.** Append to `ResolutionPhaseTests`:

```csharp
    /// <summary>Two wars against the same defender share a front cell; the ENDING war
    /// has the HIGHER id, so it resolves after the live war re-marked the cell this
    /// epoch — its unconditional demilitarize used to un-contest a cell the live war
    /// still fights over (final-review M-1).</summary>
    [Fact]
    public void SharedFrontCell_StaysContested_WhileAnotherWarIsLive()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        for (int i = 0; i < 3; i++)
            s.Species.Add(new SpeciesProfile { Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Polities.Add(new Polity { Id = 0, Name = "A", SpeciesId = 0, CapitalQ = 1, CapitalR = 0, MilitaryStockpile = 50.0 });
        s.Polities.Add(new Polity { Id = 1, Name = "C", SpeciesId = 1, CapitalQ = -2, CapitalR = 0, MilitaryStockpile = 50.0 });
        s.Polities.Add(new Polity { Id = 2, Name = "B", SpeciesId = 2, CapitalQ = 3, CapitalR = 0, MilitaryStockpile = 0.05 });
        var aCap = s.CellAt(new HexCoordinate(1, 0));
        aCap.OwnerPolityId = 0; aCap.DevelopmentTier = 3; aCap.Population = 2; aCap.PopulationSpeciesId = 0;
        var shared = s.CellAt(new HexCoordinate(0, 0));
        shared.OwnerPolityId = 0; shared.DevelopmentTier = 2; shared.Population = 1; shared.PopulationSpeciesId = 0;
        var cCap = s.CellAt(new HexCoordinate(-2, 0));
        cCap.OwnerPolityId = 1; cCap.DevelopmentTier = 3; cCap.Population = 2; cCap.PopulationSpeciesId = 1;
        var bCap = s.CellAt(new HexCoordinate(3, 0));
        bCap.OwnerPolityId = 2; bCap.DevelopmentTier = 3; bCap.Population = 2; bCap.PopulationSpeciesId = 2;

        var live = new War { Id = 0, AttackerId = 1, DefenderId = 0, StartEpoch = 0, Goal = WarGoal.Punitive };
        live.GoalCells.Add(shared.Coord); live.FrontCells.Add(shared.Coord);
        var ending = new War { Id = 1, AttackerId = 2, DefenderId = 0, StartEpoch = 0, Goal = WarGoal.Punitive };
        ending.GoalCells.Add(shared.Coord); ending.FrontCells.Add(shared.Coord);
        shared.Contested = true;
        s.Wars.Add(live); s.Wars.Add(ending);

        ResolutionPhase.Run(s, 0);
        Assert.True(s.Wars[1].Ended, "war 1's attacker starts below the stockpile break floor");
        Assert.False(s.Wars[0].Ended, "war 0 is healthy and must survive epoch 0");
        Assert.True(shared.Contested, "the live war still fights over the shared front cell (M-1)");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~SharedFrontCell_StaysContested" 2>&1 | tail -8`
Expected: FAIL on the `shared.Contested` assert (the ending war's demilitarize cleared it).

- [ ] **Step 3: Implement.** In `ResolutionPhase.Run`, replace the demilitarize line
`foreach (var fc in war.FrontCells) s.CellAt(fc).Contested = false;` with:

```csharp
            // Demilitarize the front — except cells another live war still fights over
            // (deferred-tickets spec §7, final-review M-1).
            foreach (var fc in war.FrontCells)
                if (!InAnotherLiveFront(s, war, fc))
                    s.CellAt(fc).Contested = false;
```

and add the helper (after `LiveWarCount`):

```csharp
    private static bool InAnotherLiveFront(GalaxySkeleton s, War ending, HexCoordinate fc)
    {
        foreach (var w in s.Wars)
            if (!w.Ended && w.Id != ending.Id && w.FrontCells.Contains(fc)) return true;
        return false;
    }
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: new test PASSES; `Wars_TerminateOrSurviveToFinalEpoch_NeverDangle` stays green (the guard only strengthens its live-front invariant).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/ResolutionPhase.cs tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs
git commit -m "fix(econ): ending war no longer un-contests a front cell another live war fights over"
```

---

### Task 7: Mechanical sweep — landless wealth, sentinel, decay coverage, WarStarted dedup

**Files:**
- Modify: `src/Core/Galaxy/Sim/AllocationPhase.cs` (landless branch)
- Modify: `src/Core/Galaxy/Sim/ActionPhase.cs` (candidate pick)
- Test: `tests/Core.Tests/Galaxy/AllocationPhaseTests.cs` (append)
- Test: `tests/Core.Tests/Galaxy/ActionPhaseTests.cs` (extend one test)

**Interfaces:**
- Consumes: nothing new.
- Produces: landless-alive polities end allocation with `Wealth == 0`; no behavior change from the sentinel refactor.

- [ ] **Step 1: Write the failing/locking tests.** Append to `AllocationPhaseTests`:

```csharp
    [Fact]
    public void LandlessPolity_WealthZeroed_NotStale()
    {
        var s = Fixture();
        var p = s.Polities[0];
        p.Wealth = 7.5;
        foreach (var c in s.Cells) c.OwnerPolityId = -1;
        AllocationPhase.Run(s, 0);
        Assert.Equal(0.0, p.Wealth);
    }

    [Fact]
    public void OreDeficit_DoublesStockpileDecay()
    {
        var starved = Fixture();
        var fed = Fixture();
        starved.Polities[0].OreBalance = -1.0;
        fed.Polities[0].OreBalance = 0.0;
        starved.Polities[0].MilitaryStockpile = fed.Polities[0].MilitaryStockpile = 100.0;
        AllocationPhase.Run(starved, 0);
        AllocationPhase.Run(fed, 0);
        // Identical income and budget splits; only the decay multiplier differs:
        // ×2 vs ×1 on a 100 stockpile at StockpileDecayRate 0.10 → exactly 10 apart.
        Assert.Equal(10.0,
            fed.Polities[0].MilitaryStockpile - starved.Polities[0].MilitaryStockpile, 10);
    }

    [Fact]
    public void UnpaidUpkeep_DoublesStockpileDecay()
    {
        var broke = Fixture();
        var solvent = Fixture();
        foreach (var c in broke.Cells) c.OwnerPolityId = -1;     // no income → cannot pay
        foreach (var c in solvent.Cells) c.OwnerPolityId = -1;   // no income, but no war either
        broke.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 99 });
        broke.Polities[0].MilitaryStockpile = solvent.Polities[0].MilitaryStockpile = 100.0;
        AllocationPhase.Run(broke, 0);
        AllocationPhase.Run(solvent, 0);
        Assert.Equal(80.0, broke.Polities[0].MilitaryStockpile, 10);     // ×(1 − 0.20)
        Assert.Equal(90.0, solvent.Polities[0].MilitaryStockpile, 10);   // ×(1 − 0.10)
    }
```

In `ActionPhaseTests.NoSecondWar_AgainstSameDefender`, after the existing one-live-war assert, add:

```csharp
        Assert.Equal(s.Wars.Count,
            s.Events.Count(e => e.Type == GalaxyEventType.WarStarted));
```

(one `WarStarted` per war object, never a duplicate — this closes the carried WarStarted-dedup ticket: the `AtWar` candidate gate enforces one live war per pair, and this locks event/registry agreement).

- [ ] **Step 2: Run tests to verify the wealth test fails**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~AllocationPhaseTests|FullyQualifiedName~ActionPhaseTests" 2>&1 | tail -12`
Expected: `LandlessPolity_WealthZeroed_NotStale` FAILS (wealth stays 7.5). The two decay tests and the dedup assert are expected to PASS against current code (coverage locks, not bug fixes) — if either decay test fails, STOP: re-derive the arithmetic in the test comment and report DONE_WITH_CONCERNS.

- [ ] **Step 3: Implement.** In `AllocationPhase.Run`, replace the landless branch:

```csharp
            if (owned.Count == 0)
            {
                polity.MilitaryStockpile = Math.Max(0, polity.MilitaryStockpile * (1.0 - decay));
                // Landless polities hold no treasury — a stale Wealth would serialize
                // and display forever (deferred-tickets spec §8).
                polity.Wealth = 0;
                expansionBudgets[polity.Id] = 0;
                continue;
            }
```

In `ActionPhase.DeclareWar`, replace the candidate pick (the `RegionCell? best = null; double bestScore = double.MinValue; ...` block):

```csharp
        RegionCell? best = null;
        double bestScore = 0.0;
        foreach (var c in candidates)
        {
            double v = score(c);
            if (best == null || v > bestScore
                || (v == bestScore && c.SpiralIndex < best.SpiralIndex))
            { best = c; bestScore = v; }
        }
```

(Behavior identical: the first candidate always seeds `best`, exactly as every real score beat `double.MinValue`; the tie-break no longer needs the redundant `best != null`. The `if (best == null) return;` line below stays — `candidates` is non-empty by the earlier guard, but the compiler can't know that.)

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: all four new/extended tests PASS; no other movement (the sentinel refactor must not change any outcome — if `ActionPhaseTests` or the golden move at THIS task, the refactor is wrong; revert and diagnose).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/AllocationPhase.cs src/Core/Galaxy/Sim/ActionPhase.cs tests/Core.Tests/Galaxy/AllocationPhaseTests.cs tests/Core.Tests/Galaxy/ActionPhaseTests.cs
git commit -m "fix(econ): landless wealth zeroed, sentinel refactor, decay-branch coverage, WarStarted dedup locked"
```

---

### Task 8: Serializer schema v5 + version literals + single golden re-freeze

**Files:**
- Modify: `src/Core/Galaxy/GalaxySkeleton.cs:11` (SchemaVersion)
- Modify: `src/Core/Galaxy/SkeletonSerializer.cs` (POLITY write + load)
- Test: `tests/Core.Tests/Galaxy/SerializerTests.cs`

**Interfaces:**
- Consumes: `Polity.BlockadeLoss` (task 1).
- Produces: schema v5 wire format — POLITY record gains `BlockadeLoss` ("R", invariant) as field 14 (0-indexed), after `ExoticsBalance`. No v4 loader.

- [ ] **Step 1: Write the failing tests.** In `tests/Core.Tests/Galaxy/SerializerTests.cs`:

1. Add after `Load_RejectsSchemaV3`:

```csharp
    [Fact]
    public void Load_RejectsSchemaV4()
    {
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader("STARGEN-SKELETON|4\nEND\n")));
    }
```

2. In `RoundTrip_PreservesEconomyState_AndWars`, right after `var original = Build();`, add:

```csharp
        original.Polities[0].BlockadeLoss = 1.25;   // ensure a nonzero strain round-trips
```

and inside the polity comparison loop add:

```csharp
            Assert.Equal(original.Polities[i].BlockadeLoss, loaded.Polities[i].BlockadeLoss);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~SerializerTests" 2>&1 | tail -12`
Expected: `Load_RejectsSchemaV4` FAILS (v4 is the current version and loads fine); the BlockadeLoss round-trip assert FAILS (field not serialized).

- [ ] **Step 3: Implement the schema bump.**

`src/Core/Galaxy/GalaxySkeleton.cs`:

```csharp
    public const int SchemaVersion = 5;
```

`src/Core/Galaxy/SkeletonSerializer.cs` — in `Save`, the POLITY line gains one field at the end:

```csharp
        foreach (var p in s.Polities)
            w.WriteLine(string.Join("|", "POLITY", p.Id.ToString(Inv), p.Name,
                p.SpeciesId.ToString(Inv), p.CapitalQ.ToString(Inv),
                p.CapitalR.ToString(Inv), p.Extinct ? "1" : "0",
                p.MilitaryStockpile.ToString("R", Inv), p.TechTier.ToString(Inv),
                p.ExoticsInvested.ToString("R", Inv), p.Wealth.ToString("R", Inv),
                p.ProvisionsBalance.ToString("R", Inv), p.OreBalance.ToString("R", Inv),
                p.ExoticsBalance.ToString("R", Inv), p.BlockadeLoss.ToString("R", Inv)));
```

In `Load`, the `case "POLITY":` initializer gains:

```csharp
                            ExoticsBalance = double.Parse(f[13], Inv),
                            BlockadeLoss = double.Parse(f[14], Inv),
```

- [ ] **Step 4: Update version literals faithfully** (mechanical, not golden facts) in `SerializerTests.cs`:

- `SchemaVersionMismatch_Throws_NeverSilentlyRebuilds`: `text.Replace("STARGEN-SKELETON|5", "STARGEN-SKELETON|999")`
- `Load_RecordBeforeConfig_Throws`: `"STARGEN-SKELETON|5\nANCHOR|0|0|1|0|0|-1\nEND\n"`
- `GoldenSnapshot_SmallGalaxyHeader`: `Assert.Equal("STARGEN-SKELETON|5", lines[0].TrimEnd('\r'));`

- [ ] **Step 5: Re-freeze the golden — exactly once, with provenance.**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~GoldenSnapshot" 2>&1 | tail -12`

Read the observed `Polities.Count` and `Events.Count` from the failure output, update the two literals, and EXTEND (never replace) the freeze-history comment:

```csharp
        // ... existing history ... Re-frozen for the deferred-ticket batch (schema v5):
        // blockade-loss classification (TradeBlocked can now fire without a live war),
        // strain-as-hardship weariness, extinction-correct termination with capture
        // restoration, famine+scar stacking, and safe capital relocation all shift the
        // event stream; observed <N> events (was 36), <M> polities (was 2).
```

Remove every `// TICKETMIGRATION` marker added in tasks 1–7. **The red window closes here.**

- [ ] **Step 6: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: ALL tests pass, zero skips. If a shape-band test is red, STOP and report DONE_WITH_CONCERNS with the observed values.

- [ ] **Step 7: Commit**

```bash
git add src/Core/Galaxy/GalaxySkeleton.cs src/Core/Galaxy/SkeletonSerializer.cs tests/Core.Tests/Galaxy/SerializerTests.cs
git commit -m "feat(econ): serializer schema v5 - BlockadeLoss on the polity record; golden re-frozen (intentional: deferred-ticket batch)"
```

---

### Task 9: REPL surfacing + acceptance (controller-led)

**Files:**
- Modify: `src/Inspector/Repl.cs` (polity panel balances line)
- Modify: `src/Inspector/EconomyReport.cs` (strain line)

**Interfaces:**
- Consumes: `Polity.BlockadeLoss`, `Economy.TradeBlockedFloor`.
- Produces: display only — no Core changes, goldens must NOT move in this task.

- [ ] **Step 1: Implement the polity panel line.** In `Repl.cs`, the `case "polity"` balances `Console.WriteLine` becomes:

```csharp
                    Console.WriteLine($"  balances: provisions {p.ProvisionsBalance:F1}"
                        + $" · ore {p.OreBalance:F1} · exotics {p.ExoticsBalance:F1}"
                        + $" (invested {p.ExoticsInvested:F1})"
                        + (p.BlockadeLoss > 0 ? $" · blockade loss {p.BlockadeLoss:F1}" : ""));
```

- [ ] **Step 2: Implement the stats strain line.** In `EconomyReport.Build`, after the `wars:` AppendLine, add:

```csharp
        var strainedPolities = living.Where(p => p.BlockadeLoss > 0).ToList();
        sb.AppendLine(strainedPolities.Count == 0
            ? "blockade strain: none"
            : $"blockade strain: {strainedPolities.Count} polities"
              + $" · total {strainedPolities.Sum(p => p.BlockadeLoss):F1}"
              + $" · {strainedPolities.Count(p => p.BlockadeLoss > Economy.TradeBlockedFloor)} above event floor");
```

- [ ] **Step 3: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: all green, identical counts to task 8 (display-only task).

- [ ] **Step 4: REPL acceptance (controller-led, spec §9).** From bash (NOT PowerShell — first piped line gets BOM-mangled):

```bash
printf 'galaxy 42 21\nstats 5\npolity 0\nchronicle\nmap war\nmap trade\nquit\n' | dotnet run --project src/Inspector
```

Verify and record in the task report:
- Strain visible: `stats` shows the blockade-strain line; at least one seed in 40–46 produces a nonzero strain or TradeBlocked event (`galaxy <seed> 8` sweep).
- Chronicle coherence: no WhitePeace ending against an extinct polity; DefenderVictory wars leave no attacker-owned pockets in restored territory (spot-check via `polity`/`cell`).
- Shape sanity at 42/r21 vs the previous acceptance (62.1% claimed, 30 living/2 extinct, 10 wars, 24 famines): report the new numbers and flag any drift that reads as a broken dial rather than intentional mechanics.

- [ ] **Step 5: Commit**

```bash
git add src/Inspector/Repl.cs src/Inspector/EconomyReport.cs
git commit -m "feat(econ): REPL surfaces blockade strain - polity panel line + stats block"
```

---

## Self-Review (completed at plan time)

- **Spec coverage:** §3 → tasks 1, 2, 8, 9; §4 → task 3; §5 → task 4; §6 → task 5; §7 → task 6; §8 → task 7; §9 → per-task tests + task 9 acceptance; §10 → no tasks (deferred by design).
- **Type consistency:** `Polity.BlockadeLoss` (double), `Economy.TradeBlockedFloor` (const double 2.0), `BetterRefuge`, `InAnotherLiveFront`, `Unblockaded` — names used consistently across tasks.
- **Known watch items (explicit contingencies in tasks):** WarOutcomes white-peace count after task 3; battle-roll sensitivity in `DefenderVictory_RestoresCapturedFrontCells`; decay-test arithmetic in task 7.
