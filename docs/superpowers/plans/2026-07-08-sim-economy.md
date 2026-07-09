# Sim Economy Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement regional-spec stages 2–3 — phase-pipeline epoch sim with budgets, military stockpiles, commodities/flows/blockades, tech tiers, population, and persistent wars — per `docs/superpowers/specs/2026-07-08-sim-economy-design.md`.

**Architecture:** `EpochSim.Run` becomes an orchestrator over four global phases per epoch (income → allocation → action → resolution), each in `src/Core/Galaxy/Sim/`. Pure economy math lives in `Economy.cs` (no skeleton mutation). Serializer bumps to schema v4 in one task near the end (red-window discipline: goldens break at the rewrite task, re-freeze exactly once at the serializer task). REPL gains trade/economy/war layers plus `polity` and `chronicle` commands.

**Tech Stack:** C# / .NET (Core is netstandard2.1, **no external dependencies**, Unity-compatible — no C# 10+-only API surface); xUnit tests; line-based invariant-culture serializer.

## Global Constraints

- Gate for every task: `dotnet test StarSystemGeneration.sln` from repo root. Report the verbatim summary line and the `test-results.xml` LastWriteTime if produced. NEVER kill processes to unblock a gate; stop and report instead.
- No Unity project changes in this slice; Unity edit-mode gates are NOT required (Core additions are additive and netstandard2.1-safe).
- Determinism discipline (regional spec §7): fixed iteration order — cells by `SpiralIndex`, polities by `Id`, wars by `Id`; every roll on a `RollChannel` keyed by (epoch, cell/polity id). RollChannel values are STABLE: never renumber/reuse; retired rolls keep their value with a comment.
- Nothing in sim state may go negative or NaN: budgets, balances, population, stockpile, weariness (spec §5 invariant — deficits manifest as shortage effects, not negative numbers).
- Red window: tasks 8–9. The golden snapshot (`SerializerTests.GoldenSnapshot_SmallGalaxyHeader`) and any sim-outcome-literal tests MAY be temporarily red between the EpochSim rewrite (task 8) and the serializer v4 re-freeze (task 9). Mark every temporarily-failing test with a `// ECONMIGRATION` comment. All other tests must stay green in every task. The window closes at task 9 (all green).
- Working branch: `sim-economy` off `main`.
- Do not let `unity/ProjectSettings/*` or `.superpowers/*` working-tree churn ride into any commit — stage only the files each task names.

---

### Task 1: Data model — Polity/RegionCell/War/event/config/channel additions

**Files:**
- Modify: `src/Core/Galaxy/Polity.cs`
- Modify: `src/Core/Galaxy/RegionCell.cs`
- Modify: `src/Core/Galaxy/GalaxyEvent.cs`
- Modify: `src/Core/Galaxy/GalaxyConfig.cs`
- Modify: `src/Core/Galaxy/GalaxySkeleton.cs`
- Modify: `src/Core/Rng/RollChannel.cs`
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (population seeding in `PassHomeworlds` only)
- Create: `src/Core/Galaxy/War.cs`
- Test: `tests/Core.Tests/Galaxy/SkeletonModelTests.cs` (append new test class file section — add tests to the existing file)

**Interfaces:**
- Consumes: existing model types only.
- Produces (later tasks rely on these exact names):
  - `Polity`: `double MilitaryStockpile`, `int TechTier`, `double ExoticsInvested`, `double Wealth`, `double ProvisionsBalance`, `double OreBalance`, `double ExoticsBalance`
  - `RegionCell`: `double Population`, `int PopulationSpeciesId` (default −1), `double RouteThroughput`
  - `War` class + `WarGoal` enum (`Ore, Exotics, Chokepoint, Punitive`) + `WarOutcome` enum (`Ongoing, AttackerVictory, DefenderVictory, WhitePeace`)
  - `GalaxySkeleton`: `List<War> Wars`, `bool AtWar(int a, int b)`
  - `GalaxyEventType`: appended `WarEnded, TechAdvance, Famine, TradeBlocked`; `GalaxyEvent.Detail` (int)
  - `GalaxyConfig`: `WarWearinessRate=0.15`, `StockpileDecayRate=0.10`, `TechThresholdBase=10.0`, `TradeIncomeWeight=0.5`, `ProvisionsPerPop=1.0`
  - `RollChannel.SimBattle = 36`
  - Homeworld cells seeded `Population = 3.0`, `PopulationSpeciesId = <species id>`

- [ ] **Step 1: Write the failing tests** — append to `tests/Core.Tests/Galaxy/SkeletonModelTests.cs`:

```csharp
    [Fact]
    public void EconModel_DefaultsAreNeutral()
    {
        var p = new Polity();
        Assert.Equal(0.0, p.MilitaryStockpile);
        Assert.Equal(0, p.TechTier);
        Assert.Equal(0.0, p.Wealth);
        var c = new RegionCell();
        Assert.Equal(0.0, c.Population);
        Assert.Equal(-1, c.PopulationSpeciesId);
        Assert.Equal(0.0, c.RouteThroughput);
        var w = new War();
        Assert.False(w.Ended);
        Assert.Equal(WarOutcome.Ongoing, w.Outcome);
    }

    [Fact]
    public void Homeworlds_SeedPopulation()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 });
        foreach (var p in s.Polities)
        {
            var cell = s.CellAt(p.CapitalCoord);
            Assert.True(cell.Population >= 3.0, "homeworld cells start populated");
            Assert.Equal(p.SpeciesId, cell.PopulationSpeciesId);
        }
    }

    [Fact]
    public void AtWar_ReadsLiveWarsOnly()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        s.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 1 });
        s.Wars.Add(new War { Id = 1, AttackerId = 2, DefenderId = 3, Ended = true });
        Assert.True(s.AtWar(0, 1));
        Assert.True(s.AtWar(1, 0));
        Assert.False(s.AtWar(2, 3));
        Assert.False(s.AtWar(0, 2));
    }
```

Note: `Homeworlds_SeedPopulation` asserts `>= 3.0` (not `== 3.0`) so later population growth at the capital during the sim cannot break it.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~SkeletonModelTests" 2>&1 | tail -20`
Expected: FAIL — compile errors (`MilitaryStockpile`, `War`, `Wars` not defined).

- [ ] **Step 3: Implement the model additions**

`src/Core/Galaxy/War.cs` (new file, complete contents):

```csharp
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

public enum WarGoal { Ore, Exotics, Chokepoint, Punitive }
public enum WarOutcome { Ongoing, AttackerVictory, DefenderVictory, WhitePeace }

/// <summary>Persistent war registry entry (economy spec §4/§6). Ended wars are
/// retained forever, mirroring extinct-polity retention.</summary>
public sealed class War
{
    public int Id { get; set; }
    public int AttackerId { get; set; }
    public int DefenderId { get; set; }
    public int StartEpoch { get; set; }
    public WarGoal Goal { get; set; }
    /// <summary>Initial target cluster (≤3 cells) the victor annexes.</summary>
    public List<HexCoordinate> GoalCells { get; } = new();
    /// <summary>Goal cells plus cells either side took in this war; contested while live.</summary>
    public List<HexCoordinate> FrontCells { get; } = new();
    public double AttackerWeariness { get; set; }
    public double DefenderWeariness { get; set; }
    public int AttackerCellsLost { get; set; }
    public int DefenderCellsLost { get; set; }
    public bool Ended { get; set; }
    public WarOutcome Outcome { get; set; }
}
```

`src/Core/Galaxy/Polity.cs` — add inside the class, after `Extinct`:

```csharp
    // --- Economy state (economy spec §4). Budget splits are transient, not stored. ---
    public double MilitaryStockpile { get; set; }
    public int TechTier { get; set; }
    public double ExoticsInvested { get; set; }
    public double Wealth { get; set; }
    /// <summary>Last epoch's polity-level net per good; war goals and shortage effects read these.</summary>
    public double ProvisionsBalance { get; set; }
    public double OreBalance { get; set; }
    public double ExoticsBalance { get; set; }
```

`src/Core/Galaxy/RegionCell.cs` — add inside the class, after `WarScarred`:

```csharp
    /// <summary>Species-tagged population (economy spec §4): grows with development +
    /// provisions surplus, shrinks under famine and war scarring.</summary>
    public double Population { get; set; }
    public int PopulationSpeciesId { get; set; } = -1;
    /// <summary>Last-epoch snapshot of flow magnitude transiting this cell.</summary>
    public double RouteThroughput { get; set; }
```

`src/Core/Galaxy/GalaxyEvent.cs` — replace the enum line and add `Detail`:

```csharp
public enum GalaxyEventType
{
    CellClaimed, WarStarted, CellTaken, LostCapital, PolityExtinct,
    WarEnded, TechAdvance, Famine, TradeBlocked,
}
```

and inside `GalaxyEvent` after `Magnitude`:

```csharp
    /// <summary>Type-specific payload (economy spec §4): WarStarted → (int)WarGoal,
    /// WarEnded → (int)WarOutcome, TechAdvance → tier reached. 0 otherwise.</summary>
    public int Detail { get; set; }
```

`src/Core/Galaxy/GalaxyConfig.cs` — add at the end of the class:

```csharp
    // --- Economy knobs (economy spec §7). Seeded defaults, artifact-stamped. ---
    /// <summary>Base war-weariness accrual per epoch at war.</summary>
    public double WarWearinessRate { get; set; } = 0.15;
    /// <summary>Fractional military-stockpile decay per epoch.</summary>
    public double StockpileDecayRate { get; set; } = 0.10;
    /// <summary>Exotics cost of tech tier 1; geometric ladder (×3 per tier) above.</summary>
    public double TechThresholdBase { get; set; } = 10.0;
    /// <summary>Wealth gained per unit of matched cross-polity trade.</summary>
    public double TradeIncomeWeight { get; set; } = 0.5;
    /// <summary>Provisions consumed per population unit per epoch — the famine dial.</summary>
    public double ProvisionsPerPop { get; set; } = 1.0;
```

`src/Core/Galaxy/GalaxySkeleton.cs` — add after `public List<GalaxyEvent> Events { get; } = new();`:

```csharp
    public List<War> Wars { get; } = new();
```

and add this method after `CellForHex`:

```csharp
    /// <summary>True iff a live (non-ended) war exists between the two polities.</summary>
    public bool AtWar(int a, int b)
    {
        foreach (var w in Wars)
            if (!w.Ended && ((w.AttackerId == a && w.DefenderId == b)
                          || (w.AttackerId == b && w.DefenderId == a)))
                return true;
        return false;
    }
```

`src/Core/Rng/RollChannel.cs` — change the two sim entries at the end to:

```csharp
    SimDevelopment = 34,       // retired with the stage-1 coin-flip loop - value must not be reused
    SimWar = 35,               // war-declaration gate: index = epoch, subIndex = polity id
    SimBattle = 36,            // front-cell contest: index = epoch, subIndex = war id (cell-keyed context)
```

`src/Core/Galaxy/SkeletonBuilder.cs` — in `PassHomeworlds`, after `cell.DevelopmentTier = 2;` add:

```csharp
            cell.Population = 3.0;
            cell.PopulationSpeciesId = id;
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 112 total (109 prior + 3 new). All green — additions are inert until the phases land.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/War.cs src/Core/Galaxy/Polity.cs src/Core/Galaxy/RegionCell.cs src/Core/Galaxy/GalaxyEvent.cs src/Core/Galaxy/GalaxyConfig.cs src/Core/Galaxy/GalaxySkeleton.cs src/Core/Rng/RollChannel.cs src/Core/Galaxy/SkeletonBuilder.cs tests/Core.Tests/Galaxy/SkeletonModelTests.cs
git commit -m "feat(econ): data model - war registry, econ fields, knobs, channels, homeworld population"
```

---

### Task 2: Economy pure functions

**Files:**
- Create: `src/Core/Galaxy/Sim/Economy.cs`
- Test: `tests/Core.Tests/Galaxy/EconomyTests.cs` (create)

**Interfaces:**
- Consumes: `EpochSim.Affinity(SpeciesProfile, RegionCell)` (existing, `internal static`), task-1 model fields.
- Produces (exact signatures later tasks call):
  - `enum Commodity { Provisions, Ore, Exotics }` (in `Economy.cs`)
  - `Economy.ProvisionsPotential(SpeciesProfile, RegionCell) → double`
  - `Economy.OrePotential(RegionCell) → double`
  - `Economy.ExoticsPotential(RegionCell) → double`
  - `Economy.HasAnchor(RegionCell, AnchorType) → bool`
  - `Economy.Produced(Commodity, SpeciesProfile owner, RegionCell) → double`
  - `Economy.Consumed(Commodity, GalaxyConfig, SpeciesProfile owner, RegionCell) → double`
  - `Economy.DietFactor(Embodiment) → double`
  - `Economy.SystemValue(SpeciesProfile, RegionCell) → double`
  - `Economy.TechThreshold(GalaxyConfig, int tier) → double`
  - `Economy.DevCeiling(int techTier) → int`
  - `Economy.WarStrength(double committed, int techTier, double militancy) → double`
  - `Economy.DisplayBaseline` — static `SpeciesProfile` (TerranAnalog) for owner-less display reads

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/EconomyTests.cs` (new file, complete contents):

```csharp
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class EconomyTests
{
    private static SpeciesProfile Species(Embodiment e = Embodiment.TerranAnalog) => new()
    {
        Id = 0, Embodiment = e, Expansionism = 0.5, Cohesion = 0.5,
        Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
    };

    private static RegionCell Cell() => new()
    {
        Q = 0, R = 0, MeanDensity = 0.5, Lean = StellarLean.Balanced,
        Metallicity = 0.5, DevelopmentTier = 2, Population = 1.0,
    };

    [Fact]
    public void Provisions_AreEmbodimentRelative()
    {
        var bright = Cell(); bright.Lean = StellarLean.YoungBright;
        var dim = Cell(); dim.Lean = StellarLean.OldDim;
        Assert.True(Economy.ProvisionsPotential(Species(Embodiment.Aquatic), bright)
                  > Economy.ProvisionsPotential(Species(Embodiment.Aquatic), dim));
        Assert.True(Economy.ProvisionsPotential(Species(Embodiment.Cryophilic), dim)
                  > Economy.ProvisionsPotential(Species(Embodiment.Cryophilic), bright));
    }

    [Fact]
    public void MineralAnchor_DominatesOreProduction()
    {
        var plain = Cell();
        var anchored = Cell();
        anchored.Anchors.Add(new Anchor { Type = AnchorType.MineralRich, Hex = anchored.Coord });
        Assert.True(Economy.OrePotential(anchored) > Economy.OrePotential(plain) + 1.0);
    }

    [Fact]
    public void PrecursorSite_DominatesExotics()
    {
        var plain = Cell();
        var site = Cell();
        site.Anchors.Add(new Anchor { Type = AnchorType.PrecursorSite, Hex = site.Coord });
        Assert.True(Economy.ExoticsPotential(site) > 10 * Economy.ExoticsPotential(plain));
    }

    [Fact]
    public void LithicsAndMachines_BarelyConsumeProvisions()
    {
        var config = new GalaxyConfig();
        var cell = Cell();
        double terran = Economy.Consumed(Commodity.Provisions, config, Species(), cell);
        double lithic = Economy.Consumed(Commodity.Provisions, config, Species(Embodiment.Lithic), cell);
        double machine = Economy.Consumed(Commodity.Provisions, config, Species(Embodiment.Machine), cell);
        Assert.True(lithic < terran * 0.3);
        Assert.True(machine < terran * 0.2);
    }

    [Fact]
    public void SystemValue_RewardsThroughputAndChokepoints()
    {
        var plain = Cell();
        var busy = Cell(); busy.RouteThroughput = 4.0;
        var choke = Cell(); choke.IsChokepoint = true;
        Assert.True(Economy.SystemValue(Species(), busy) > Economy.SystemValue(Species(), plain));
        Assert.True(Economy.SystemValue(Species(), choke) > Economy.SystemValue(Species(), plain));
    }

    [Fact]
    public void TechLadder_IsGeometric()
    {
        var config = new GalaxyConfig { TechThresholdBase = 10.0 };
        Assert.Equal(10.0, Economy.TechThreshold(config, 0));
        Assert.Equal(30.0, Economy.TechThreshold(config, 1));
        Assert.Equal(90.0, Economy.TechThreshold(config, 2));
    }

    [Fact]
    public void DevCeiling_StartsAtStageOneCap_AndIsTechScaled()
    {
        Assert.Equal(5, Economy.DevCeiling(0));   // stage-1 flat cap preserved at tier 0
        Assert.Equal(7, Economy.DevCeiling(2));
        Assert.Equal(9, Economy.DevCeiling(20));  // absolute cap 9 (single map glyph)
    }

    [Fact]
    public void WarStrength_ScalesWithTechAndMilitancy()
    {
        double baseline = Economy.WarStrength(10, 0, 0.5);
        Assert.True(Economy.WarStrength(10, 2, 0.5) > baseline);
        Assert.True(Economy.WarStrength(10, 0, 0.9) > baseline);
        Assert.Equal(0.0, Economy.WarStrength(0, 5, 1.0));
    }

    [Fact]
    public void Production_IsNonNegative_EverywhereReasonable()
    {
        foreach (var lean in new[] { StellarLean.Balanced, StellarLean.YoungBright,
                                     StellarLean.OldDim, StellarLean.RemnantGraveyard })
            foreach (var e in new[] { Embodiment.TerranAnalog, Embodiment.Aquatic,
                                      Embodiment.Cryophilic, Embodiment.Lithic,
                                      Embodiment.Hive, Embodiment.Machine })
            {
                var cell = Cell(); cell.Lean = lean;
                foreach (Commodity good in System.Enum.GetValues(typeof(Commodity)))
                    Assert.True(Economy.Produced(good, Species(e), cell) >= 0);
            }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~EconomyTests" 2>&1 | tail -10`
Expected: FAIL — compile error, `Economy` not defined.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/Sim/Economy.cs` (new file + new folder, complete contents):

```csharp
using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

public enum Commodity { Provisions, Ore, Exotics }

/// <summary>Pure economy math (economy spec §5): production, consumption, value,
/// tech ladder, war strength, and BFS flow routing. Never mutates the skeleton —
/// phases apply the results.</summary>
public static class Economy
{
    /// <summary>Neutral species for owner-less display reads (inspector layers).</summary>
    public static readonly SpeciesProfile DisplayBaseline = new()
    {
        Id = -1, Name = "baseline", Embodiment = Embodiment.TerranAnalog,
        Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
        Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
    };

    public static bool HasAnchor(RegionCell cell, AnchorType type)
    {
        foreach (var a in cell.Anchors) if (a.Type == type) return true;
        return false;
    }

    /// <summary>Provisions fertility through the owner's embodiment (spec §5):
    /// aquatics farm bright-star cells, cryophiles the dim reaches.</summary>
    public static double ProvisionsPotential(SpeciesProfile species, RegionCell cell) =>
        EpochSim.Affinity(species, cell) * cell.MeanDensity;

    public static double OrePotential(RegionCell cell) =>
        cell.Metallicity + (HasAnchor(cell, AnchorType.MineralRich) ? 1.5 : 0.0);

    public static double ExoticsPotential(RegionCell cell) =>
        (HasAnchor(cell, AnchorType.PrecursorSite) ? 1.0 : 0.0)
        + (cell.Lean == StellarLean.RemnantGraveyard ? 0.05 : 0.02);

    public static double Produced(Commodity good, SpeciesProfile owner, RegionCell cell) => good switch
    {
        Commodity.Provisions => ProvisionsPotential(owner, cell) * cell.DevelopmentTier
                                * (0.5 + 0.5 * Math.Min(1.0, cell.Population)),
        Commodity.Ore => OrePotential(cell) * cell.DevelopmentTier * 0.5,
        _ => ExoticsPotential(cell) * cell.DevelopmentTier * 0.5,
    };

    /// <summary>Embodiment diet discount (spec §5: lithics/machines barely need provisions).</summary>
    public static double DietFactor(Embodiment e) => e switch
    {
        Embodiment.Lithic => 0.2,
        Embodiment.Machine => 0.1,
        _ => 1.0,
    };

    public static double Consumed(Commodity good, GalaxyConfig config, SpeciesProfile owner, RegionCell cell) => good switch
    {
        Commodity.Provisions => cell.Population * config.ProvisionsPerPop * DietFactor(owner.Embodiment),
        Commodity.Ore => 0.2 * cell.DevelopmentTier,
        _ => 0.0,   // exotics are consumed at polity level by tech investment
    };

    /// <summary>System value (spec §5): production potential + throughput + strategic position.
    /// War-goal selection maximizes this within its goal type.</summary>
    public static double SystemValue(SpeciesProfile owner, RegionCell cell) =>
        ProvisionsPotential(owner, cell) + OrePotential(cell) + ExoticsPotential(cell)
        + 0.5 * cell.RouteThroughput + (cell.IsChokepoint ? 2.0 : 0.0);

    /// <summary>Cumulative exotics investment required to reach tier+1 (geometric, ×3).</summary>
    public static double TechThreshold(GalaxyConfig config, int tier) =>
        config.TechThresholdBase * Math.Pow(3.0, tier);

    /// <summary>Development-tier ceiling: stage-1 flat cap 5 at tech 0, +1 per tier, max 9.</summary>
    public static int DevCeiling(int techTier) => Math.Min(9, 5 + techTier);

    public static double WarStrength(double committedStockpile, int techTier, double militancy) =>
        committedStockpile * (1.0 + 0.5 * techTier) * (0.5 + militancy);

    /// <summary>Transit predicate for polity flows (spec §5): blockaded = contested or
    /// owned by a belligerent of the flow's owner. Unclaimed non-void space is open.</summary>
    public static Func<RegionCell, bool> Passable(GalaxySkeleton s, int polityId) =>
        c => !c.IsVoid && !c.Contested
             && (c.OwnerPolityId < 0 || c.OwnerPolityId == polityId
                 || !s.AtWar(c.OwnerPolityId, polityId));

    /// <summary>Deterministic BFS from a cell to the nearest cell satisfying
    /// <paramref name="isTarget"/>, transiting only <paramref name="passable"/> cells
    /// (endpoints exempt). Returns the full path including both endpoints, or null.
    /// Neighbor order = HexGrid.Neighbors order; ties resolve by discovery order.</summary>
    public static List<RegionCell>? Route(GalaxySkeleton s, RegionCell from,
        Func<RegionCell, bool> isTarget, Func<RegionCell, bool> passable)
    {
        var parent = new Dictionary<int, int>();
        var seen = new HashSet<int> { from.SpiralIndex };
        var queue = new Queue<RegionCell>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var nc in HexGrid.Neighbors(cur.Coord))
            {
                if (!s.TryGetCell(nc, out var n) || seen.Contains(n.SpiralIndex)) continue;
                seen.Add(n.SpiralIndex);
                parent[n.SpiralIndex] = cur.SpiralIndex;
                if (isTarget(n)) return BuildPath(s, from, n, parent);
                if (passable(n)) queue.Enqueue(n);
            }
        }
        return null;
    }

    private static List<RegionCell> BuildPath(GalaxySkeleton s, RegionCell from,
        RegionCell target, Dictionary<int, int> parent)
    {
        var path = new List<RegionCell> { target };
        int cur = target.SpiralIndex;
        while (cur != from.SpiralIndex)
        {
            cur = parent[cur];
            path.Add(s.Cells[cur]);
        }
        path.Reverse();
        return path;
    }
}
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 121 total (112 + 9 new). `Route` is exercised in task 3.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/Economy.cs tests/Core.Tests/Galaxy/EconomyTests.cs
git commit -m "feat(econ): pure economy math - potentials, production, value, tech ladder, BFS routing"
```

---

### Task 3: Flow routing tests (blockade semantics)

**Files:**
- Test: `tests/Core.Tests/Galaxy/FlowRoutingTests.cs` (create)
- Modify: `src/Core/Galaxy/Sim/Economy.cs` (only if a routing bug surfaces; expected no change)

**Interfaces:**
- Consumes: `Economy.Route`, `Economy.Passable`, `GalaxySkeleton.AtWar`, task-1 model.
- Produces: confidence + the hand-built-skeleton test pattern later tasks reuse.

- [ ] **Step 1: Write the tests** — `tests/Core.Tests/Galaxy/FlowRoutingTests.cs` (new file, complete contents):

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>Hand-built-skeleton routing tests. GalaxySkeleton's constructor
/// enumerates the cell lattice from config; tests then hand-set cell state.</summary>
public class FlowRoutingTests
{
    private static GalaxySkeleton Blank()
    {
        // Radius 3 lattice; all cells non-void by default for routing clarity.
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        return s;
    }

    [Fact]
    public void Route_FindsShortestPath_AndIncludesEndpoints()
    {
        var s = Blank();
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(3, 0));
        var path = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        Assert.NotNull(path);
        Assert.Equal(from.SpiralIndex, path![0].SpiralIndex);
        Assert.Equal(target.SpiralIndex, path[^1].SpiralIndex);
        Assert.Equal(4, path.Count);   // 0,0 → 1,0 → 2,0 → 3,0 (BFS shortest = 3 steps)
    }

    [Fact]
    public void Route_DetoursAroundContestedCells()
    {
        var s = Blank();
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(2, 0));
        var direct = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0))!;
        int directLen = direct.Count;
        s.CellAt(new HexCoordinate(1, 0)).Contested = true;   // block the straight line
        var detour = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        Assert.NotNull(detour);
        Assert.True(detour!.Count >= directLen, "blocked direct hop forces an equal-or-longer path");
        Assert.DoesNotContain(detour, c => c.Contested);
    }

    [Fact]
    public void Route_BelligerentTerritoryBlocks_NeutralDoesNot()
    {
        var s = Blank();
        s.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 1 });
        // Wall of enemy cells across the middle column q=1 (all r).
        foreach (var c in s.Cells.Where(c => c.Q == 1)) c.OwnerPolityId = 1;
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(3, -1));
        var blocked = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        // The q=1 wall spans the whole lattice: no path for a belligerent of polity 1.
        Assert.Null(blocked);
        // A third polity not at war with 1 routes straight through.
        var neutral = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 2));
        Assert.NotNull(neutral);
    }

    [Fact]
    public void Route_TargetOnBlockedCell_IsStillReachable_EndpointExempt()
    {
        var s = Blank();
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(1, 0));
        target.Contested = true;   // endpoint exemption: target check precedes passable check
        var path = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        Assert.NotNull(path);
    }

    [Fact]
    public void Route_IsDeterministic()
    {
        var s1 = Blank(); var s2 = Blank();
        var p1 = Economy.Route(s1, s1.Cells[0], c => c.SpiralIndex == 30, Economy.Passable(s1, 0));
        var p2 = Economy.Route(s2, s2.Cells[0], c => c.SpiralIndex == 30, Economy.Passable(s2, 0));
        Assert.Equal(p1!.Select(c => c.SpiralIndex), p2!.Select(c => c.SpiralIndex));
    }
}
```

- [ ] **Step 2: Run the new tests**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~FlowRoutingTests" 2>&1 | tail -10`
Expected: PASS (5/5). If `Route_FindsShortestPath` fails on the exact count, verify BFS marks `seen` before the target check as written; if `Route_BelligerentTerritoryBlocks` fails, check whether the q=1 column truly spans the radius-3 lattice — if a gap exists at the rim, extend the wall to `c.Q == 1 || (c.Q == 2 && c.R == -3)`-style until `blocked` is null, and note the adjustment in the report.

- [ ] **Step 3: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 126 total.

- [ ] **Step 4: Commit**

```bash
git add tests/Core.Tests/Galaxy/FlowRoutingTests.cs
git commit -m "test(econ): BFS flow routing - shortest path, blockade detour/severance, determinism"
```

---

### Task 4: IncomePhase — production, flows, throughput, shortages, population

**Files:**
- Create: `src/Core/Galaxy/Sim/IncomePhase.cs`
- Test: `tests/Core.Tests/Galaxy/IncomePhaseTests.cs` (create)

**Interfaces:**
- Consumes: `Economy.*` (task 2), model fields (task 1), `EpochSim.Affinity`.
- Produces: `IncomePhase.Run(GalaxySkeleton s, int epoch)` — sets `RouteThroughput` (reset each call), polity `*Balance` fields, adds trade wealth to `Wealth`, mutates `Population`, and appends `Famine` / `TradeBlocked` events.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/IncomePhaseTests.cs` (new file, complete contents):

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class IncomePhaseTests
{
    /// <summary>Two-polity fixture on a blank radius-3 lattice: P0 (terran) owns a
    /// 3-cell west chain, P1 owns one east cell. Densities/metallicity hand-set.</summary>
    private static GalaxySkeleton Fixture()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; c.Metallicity = 0.3; }
        for (int i = 0; i < 2; i++)
        {
            s.Species.Add(new SpeciesProfile
            {
                Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
                Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
            });
        }
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -2, CapitalR = 0 });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 2, CapitalR = 0 });
        foreach (var (q, r) in new[] { (-2, 0), (-1, 0), (0, 0) })
        {
            var c = s.CellAt(new HexCoordinate(q, r));
            c.OwnerPolityId = 0; c.DevelopmentTier = 2; c.Population = 1.0; c.PopulationSpeciesId = 0;
        }
        var e = s.CellAt(new HexCoordinate(2, 0));
        e.OwnerPolityId = 1; e.DevelopmentTier = 2; e.Population = 1.0; e.PopulationSpeciesId = 1;
        return s;
    }

    [Fact]
    public void Run_SetsBalances_AndNothingGoesNegativeOrNaN()
    {
        var s = Fixture();
        IncomePhase.Run(s, 0);
        foreach (var p in s.Polities)
        {
            Assert.False(double.IsNaN(p.ProvisionsBalance) || double.IsNaN(p.OreBalance)
                      || double.IsNaN(p.ExoticsBalance) || double.IsNaN(p.Wealth));
            Assert.True(p.Wealth >= 0);
        }
        foreach (var c in s.Cells)
        {
            Assert.True(c.Population >= 0);
            Assert.True(c.RouteThroughput >= 0);
            Assert.False(double.IsNaN(c.Population) || double.IsNaN(c.RouteThroughput));
        }
    }

    [Fact]
    public void SurplusRoutesToDeficit_AndAccumulatesThroughput()
    {
        var s = Fixture();
        // Make the P0 capital a heavy consumer (big population), its chain-end a producer.
        var capital = s.CellAt(new HexCoordinate(-2, 0));
        capital.Population = 4.0; capital.DevelopmentTier = 1;
        var farm = s.CellAt(new HexCoordinate(0, 0));
        farm.DevelopmentTier = 5; farm.Population = 1.0;
        IncomePhase.Run(s, 0);
        var middle = s.CellAt(new HexCoordinate(-1, 0));
        Assert.True(middle.RouteThroughput > 0, "flow from farm to capital transits the middle cell");
    }

    [Fact]
    public void UnfedCells_Famine_ShrinksPopulation_AndLogsEvent()
    {
        var s = Fixture();
        // Starve P1: huge population, no development to feed it, no trade partner
        // adjacency (P0 is 2+ cells away and produces little surplus).
        var e = s.CellAt(new HexCoordinate(2, 0));
        e.Population = 10.0; e.DevelopmentTier = 1;
        double before = e.Population;
        IncomePhase.Run(s, 3);
        Assert.True(e.Population < before, "famine shrinks population");
        Assert.Contains(s.Events, ev => ev.Type == GalaxyEventType.Famine && ev.ActorPolityId == 1);
    }

    [Fact]
    public void FedCells_GrowTowardDevelopmentCap()
    {
        var s = Fixture();
        var c = s.CellAt(new HexCoordinate(0, 0));
        c.DevelopmentTier = 4; c.Population = 0.5;
        IncomePhase.Run(s, 0);
        Assert.True(c.Population > 0.5, "fed cells grow");
        for (int i = 0; i < 50; i++) IncomePhase.Run(s, i);
        Assert.True(c.Population <= 1 + c.DevelopmentTier + 1e-9, "population caps at 1 + dev tier");
    }

    [Fact]
    public void Throughput_IsSnapshot_ResetEachRun()
    {
        var s = Fixture();
        var capital = s.CellAt(new HexCoordinate(-2, 0));
        capital.Population = 4.0; capital.DevelopmentTier = 1;
        s.CellAt(new HexCoordinate(0, 0)).DevelopmentTier = 5;
        IncomePhase.Run(s, 0);
        double t1 = s.CellAt(new HexCoordinate(-1, 0)).RouteThroughput;
        IncomePhase.Run(s, 1);
        double t2 = s.CellAt(new HexCoordinate(-1, 0)).RouteThroughput;
        Assert.True(t2 <= t1 * 2 + 1.0, "throughput must not accumulate across epochs unboundedly");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~IncomePhaseTests" 2>&1 | tail -10`
Expected: FAIL — compile error, `IncomePhase` not defined.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/Sim/IncomePhase.cs` (new file, complete contents):

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
    private const double TradeBlockedFloor = 2.0;
    private const double PopGrowthBase = 0.05;
    private const double FamineShrink = 0.8;
    private const double ScarShrink = 0.95;

    public static void Run(GalaxySkeleton s, int epoch)
    {
        foreach (var cell in s.Cells) cell.RouteThroughput = 0.0;

        // Per-polity, per-good remaining surplus/deficit after internal routing,
        // kept for the cross-polity pass. [polityId][good] → amount (+surplus/−deficit).
        var remaining = new Dictionary<int, double[]>();
        // Cells whose provisions deficit went unfilled (famine candidates).
        var unfed = new Dictionary<int, List<(RegionCell cell, double lack)>>();
        var blockedLoss = new Dictionary<int, double>();

        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) continue;
            var species = s.Species[polity.SpeciesId];
            var owned = EpochSim.Owned(s, polity);
            if (owned.Count == 0) continue;
            var passable = Economy.Passable(s, polity.Id);
            double[] totals = new double[3];
            unfed[polity.Id] = new List<(RegionCell, double)>();
            blockedLoss[polity.Id] = 0.0;

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
                    while (need > Eps)
                    {
                        var path = Economy.Route(s, deficit,
                            c => c.OwnerPolityId == polity.Id
                                 && net.TryGetValue(c.SpiralIndex, out var v) && v > Eps,
                            passable);
                        if (path == null)
                        {
                            blockedLoss[polity.Id] += need;
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

        CrossPolityTrade(s, remaining, blockedLoss);
        ApplyPopulationAndEvents(s, epoch, unfed, blockedLoss);
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
    /// path passable for BOTH parties, else the trade is blocked.</summary>
    private static void CrossPolityTrade(GalaxySkeleton s,
        Dictionary<int, double[]> remaining, Dictionary<int, double> blockedLoss)
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
                    var path = Economy.Route(s, capA, c => c.SpiralIndex == capB.SpiralIndex,
                        c => Economy.Passable(s, a)(c) && Economy.Passable(s, b)(c));
                    if (path == null)
                    {
                        blockedLoss[a] = blockedLoss.TryGetValue(a, out var la) ? la + give : give;
                        blockedLoss[b] = blockedLoss.TryGetValue(b, out var lb) ? lb + give : give;
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
        Dictionary<int, List<(RegionCell cell, double lack)>> unfed,
        Dictionary<int, double> blockedLoss)
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

            if (blockedLoss.TryGetValue(polity.Id, out var lost) && lost > TradeBlockedFloor
                && HasLiveWar(s, polity.Id))
            {
                var cap = s.CellAt(polity.CapitalCoord);
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.TradeBlocked,
                    ActorPolityId = polity.Id, Q = cap.Q, R = cap.R, Magnitude = lost,
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
            cell.Population = Math.Min(cap,
                cell.Population + PopGrowthBase * (1 + cell.DevelopmentTier) * 0.5);
            if (cell.Contested && cell.WarScarred)
                cell.Population = Math.Max(0, cell.Population * ScarShrink);
        }
    }

    private static bool HasLiveWar(GalaxySkeleton s, int polityId)
    {
        foreach (var w in s.Wars)
            if (!w.Ended && (w.AttackerId == polityId || w.DefenderId == polityId)) return true;
        return false;
    }
}
```

This requires `EpochSim.Owned` to be visible: in `src/Core/Galaxy/EpochSim.cs`, change `private static List<RegionCell> Owned(...)` to `internal static List<RegionCell> Owned(...)` (one-word change; the stage-1 loop keeps working — it is removed in task 8).

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 131 total. `IncomePhase` is not yet wired into `EpochSim`, so all prior behavior is unchanged.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/IncomePhase.cs src/Core/Galaxy/EpochSim.cs tests/Core.Tests/Galaxy/IncomePhaseTests.cs
git commit -m "feat(econ): income phase - production, pathed flows, throughput, famine, trade blockade"
```

---

### Task 5: AllocationPhase — budgets, stockpile, development spending, tech

**Files:**
- Create: `src/Core/Galaxy/Sim/AllocationPhase.cs`
- Test: `tests/Core.Tests/Galaxy/AllocationPhaseTests.cs` (create)

**Interfaces:**
- Consumes: `Economy.TechThreshold`, `Economy.DevCeiling`, `EpochSim.Owned`, task-1 fields, `GalaxySkeleton.Wars`.
- Produces: `AllocationPhase.Run(GalaxySkeleton s, int epoch) → Dictionary<int, double>` — expansion budget per polity id, consumed by `ActionPhase` (task 6). Mutates `Wealth`, `MilitaryStockpile`, `DevelopmentTier`, `ExoticsInvested`, `TechTier`; appends `TechAdvance` events.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/AllocationPhaseTests.cs` (new file, complete contents):

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class AllocationPhaseTests
{
    private static GalaxySkeleton Fixture(double militancy = 0.5, double industry = 0.5)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        s.Species.Add(new SpeciesProfile
        {
            Id = 0, Name = "S0", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = militancy,
            Openness = 0.5, Industry = industry, Adaptability = 0.5,
        });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = 0, CapitalR = 0 });
        foreach (var (q, r) in new[] { (0, 0), (1, 0), (0, 1) })
        {
            var c = s.CellAt(new HexCoordinate(q, r));
            c.OwnerPolityId = 0; c.DevelopmentTier = 3; c.Population = 2.0; c.PopulationSpeciesId = 0;
        }
        return s;
    }

    [Fact]
    public void Run_ProducesNonNegativeBudgets_AndGrowsStockpile()
    {
        var s = Fixture();
        var budgets = AllocationPhase.Run(s, 0);
        Assert.True(budgets[0] >= 0);
        Assert.True(s.Polities[0].MilitaryStockpile > 0, "military spend grows the stockpile");
        Assert.True(s.Polities[0].Wealth >= 0);
    }

    [Fact]
    public void Stockpile_DecaysWithoutSpending()
    {
        var s = Fixture();
        var p = s.Polities[0];
        p.MilitaryStockpile = 100.0;
        foreach (var c in s.Cells) c.OwnerPolityId = -1;   // no income at all
        AllocationPhase.Run(s, 0);
        Assert.True(p.MilitaryStockpile < 100.0, "stockpile decays");
        Assert.True(p.MilitaryStockpile >= 0);
    }

    [Fact]
    public void DevelopmentSpending_RaisesTiers_UpToCeiling()
    {
        var s = Fixture(industry: 0.9);
        int before = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        for (int e = 0; e < 30; e++) { s.Polities[0].OreBalance = 1.0; AllocationPhase.Run(s, e); }
        int after = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        Assert.True(after > before, "development budget raises tiers");
        Assert.All(s.Cells.Where(c => c.OwnerPolityId == 0),
            c => Assert.True(c.DevelopmentTier <= Economy.DevCeiling(s.Polities[0].TechTier)));
    }

    [Fact]
    public void OreDeficit_StallsDevelopment()
    {
        var s = Fixture(industry: 0.9);
        s.Polities[0].OreBalance = -5.0;
        int before = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        AllocationPhase.Run(s, 0);
        int after = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ExoticsSurplus_CrossesTechThresholds_AndLogsEvent()
    {
        var s = Fixture(industry: 0.9);
        var p = s.Polities[0];
        p.ExoticsBalance = 100.0;   // >> TechThresholdBase 10
        AllocationPhase.Run(s, 0);
        Assert.True(p.TechTier >= 1, "big exotics surplus crosses tier 1");
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.TechAdvance && e.ActorPolityId == 0);
        Assert.Equal(p.TechTier, s.Events.Last(e => e.Type == GalaxyEventType.TechAdvance).Detail);
    }

    [Fact]
    public void WarUpkeep_DrainsWealth_AndUnpaidUpkeepSteepensDecay()
    {
        var sPeace = Fixture();
        var sWar = Fixture();
        sWar.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 99 });
        sPeace.Polities[0].MilitaryStockpile = sWar.Polities[0].MilitaryStockpile = 50.0;
        AllocationPhase.Run(sPeace, 0);
        AllocationPhase.Run(sWar, 0);
        // At war: upkeep paid out of wealth AND allocation shifts toward military,
        // so the at-war polity ends with less wealth than the peaceful twin.
        Assert.True(sWar.Polities[0].Wealth <= sPeace.Polities[0].Wealth + 1e-9);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~AllocationPhaseTests" 2>&1 | tail -10`
Expected: FAIL — compile error, `AllocationPhase` not defined.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/Sim/AllocationPhase.cs` (new file, complete contents):

```csharp
using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 2 (economy spec §3/§5/§6): wealth income, four-way
/// temperament-weighted budget split (war-overridden), stockpile grow/decay,
/// development spending under the tech ceiling, exotics → tech-tier ladder.</summary>
public static class AllocationPhase
{
    private const double DevIncomePerTier = 0.35;
    private const double DevIncomeBase = 1.5;
    private const double UpkeepPerWar = 0.5;

    /// <summary>Returns the expansion budget per polity id for ActionPhase.</summary>
    public static Dictionary<int, double> Run(GalaxySkeleton s, int epoch)
    {
        var expansionBudgets = new Dictionary<int, double>();
        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) { expansionBudgets[polity.Id] = 0; continue; }
            var species = s.Species[polity.SpeciesId];
            var owned = EpochSim.Owned(s, polity);
            if (owned.Count == 0) { expansionBudgets[polity.Id] = 0; continue; }

            int devSum = 0;
            foreach (var c in owned) devSum += c.DevelopmentTier;
            polity.Wealth += DevIncomeBase
                + DevIncomePerTier * devSum * (1.0 + 0.1 * polity.TechTier);

            int liveWars = CountLiveWars(s, polity.Id);
            bool atWar = liveWars > 0;
            double upkeep = UpkeepPerWar * liveWars;
            double paid = Math.Min(polity.Wealth, upkeep);
            polity.Wealth -= paid;
            bool upkeepUnpaid = paid < upkeep - 1e-9;

            double wExp = species.Expansionism;
            double wDev = species.Industry;
            double wMil = species.Militancy * (atWar ? 2.0 : 1.0);
            double wSum = wExp + wDev + wMil;
            double pool = polity.Wealth;
            double expBudget = pool * wExp / wSum;
            double devBudget = pool * wDev / wSum;
            double milBudget = pool * wMil / wSum;
            polity.Wealth = 0;

            // Stockpile: decays always; steeper when upkeep unpaid or ore-starved.
            double decay = s.Config.StockpileDecayRate
                * ((upkeepUnpaid || polity.OreBalance < 0) ? 2.0 : 1.0);
            polity.MilitaryStockpile =
                Math.Max(0, polity.MilitaryStockpile * (1.0 - decay)) + milBudget;

            // Development: cheapest-first (tier, then spiral), stalled by ore deficit.
            if (polity.OreBalance >= 0)
            {
                int ceiling = Economy.DevCeiling(polity.TechTier);
                while (true)
                {
                    RegionCell? cheapest = null;
                    foreach (var c in owned)
                        if (c.DevelopmentTier < ceiling
                            && (cheapest == null
                                || c.DevelopmentTier < cheapest.DevelopmentTier
                                || (c.DevelopmentTier == cheapest.DevelopmentTier
                                    && c.SpiralIndex < cheapest.SpiralIndex)))
                            cheapest = c;
                    if (cheapest == null) break;
                    double cost = 1.0 + cheapest.DevelopmentTier;
                    if (devBudget < cost) break;
                    devBudget -= cost;
                    cheapest.DevelopmentTier++;
                }
            }

            // Tech: exotics surplus invests, Industry-scaled; cumulative geometric ladder.
            if (polity.ExoticsBalance > 0)
            {
                polity.ExoticsInvested += polity.ExoticsBalance * (0.5 + species.Industry);
                while (polity.ExoticsInvested >= Economy.TechThreshold(s.Config, polity.TechTier))
                {
                    polity.TechTier++;
                    var cap = s.CellAt(polity.CapitalCoord);
                    s.Events.Add(new GalaxyEvent
                    {
                        Epoch = epoch, Type = GalaxyEventType.TechAdvance,
                        ActorPolityId = polity.Id, Q = cap.Q, R = cap.R,
                        Detail = polity.TechTier,
                    });
                }
            }

            expansionBudgets[polity.Id] = expBudget;
        }
        return expansionBudgets;
    }

    private static int CountLiveWars(GalaxySkeleton s, int polityId)
    {
        int n = 0;
        foreach (var w in s.Wars)
            if (!w.Ended && (w.AttackerId == polityId || w.DefenderId == polityId)) n++;
        return n;
    }
}
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 137 total.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/AllocationPhase.cs tests/Core.Tests/Galaxy/AllocationPhaseTests.cs
git commit -m "feat(econ): allocation phase - budgets, stockpile, dev spending, tech ladder"
```

---

### Task 6: ActionPhase — expansion and war declaration with goals

**Files:**
- Create: `src/Core/Galaxy/Sim/ActionPhase.cs`
- Test: `tests/Core.Tests/Galaxy/ActionPhaseTests.cs` (create)

**Interfaces:**
- Consumes: `AllocationPhase.Run`'s budget dictionary shape, `Economy.SystemValue/OrePotential/ExoticsPotential`, `EpochSim.Owned`, `EpochSim.Affinity`, `RollChannel.SimWar`.
- Produces: `ActionPhase.Run(GalaxySkeleton s, int epoch, Dictionary<int, double> expansionBudgets)`. Creates `War` objects (goal cells marked `Contested`), appends `CellClaimed`/`WarStarted` events (`WarStarted.Detail = (int)Goal`). New claims get `DevelopmentTier=1`, `Population=0.1`, `PopulationSpeciesId=<claimer's species>`.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/ActionPhaseTests.cs` (new file, complete contents):

```csharp
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class ActionPhaseTests
{
    private static GalaxySkeleton TwoPolities(double militancy0 = 0.9)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; c.Metallicity = 0.3; }
        s.Species.Add(new SpeciesProfile { Id = 0, Name = "S0", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = militancy0, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Species.Add(new SpeciesProfile { Id = 1, Name = "S1", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.1, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = 0, CapitalR = 0, MilitaryStockpile = 5.0 });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 1, CapitalR = 0, MilitaryStockpile = 5.0 });
        var c0 = s.CellAt(new HexCoordinate(0, 0));
        c0.OwnerPolityId = 0; c0.DevelopmentTier = 2; c0.Population = 2; c0.PopulationSpeciesId = 0;
        var c1 = s.CellAt(new HexCoordinate(1, 0));
        c1.OwnerPolityId = 1; c1.DevelopmentTier = 2; c1.Population = 2; c1.PopulationSpeciesId = 1;
        return s;
    }

    [Fact]
    public void Expansion_SpendsBudget_SeedsPopulationAndTier()
    {
        var s = TwoPolities();
        ActionPhase.Run(s, 0, new Dictionary<int, double> { [0] = 10.0, [1] = 0.0 });
        var claimed = s.Cells.Where(c => c.OwnerPolityId == 0).ToList();
        Assert.True(claimed.Count > 1, "expansion budget claims frontier cells");
        foreach (var c in claimed.Where(c => !(c.Q == 0 && c.R == 0)))
        {
            Assert.Equal(1, c.DevelopmentTier);
            Assert.Equal(0, c.PopulationSpeciesId);
            Assert.True(c.Population > 0);
        }
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.CellClaimed && e.ActorPolityId == 0);
    }

    [Fact]
    public void ZeroBudget_ClaimsNothing()
    {
        var s = TwoPolities();
        ActionPhase.Run(s, 0, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.Equal(1, s.Cells.Count(c => c.OwnerPolityId == 0));
    }

    [Fact]
    public void HighMilitancy_EventuallyDeclaresWar_WithGoalAndContest()
    {
        var s = TwoPolities(militancy0: 0.95);
        s.Polities[0].OreBalance = -3.0;   // worst deficit → Ore goal
        for (int epoch = 0; epoch < 40 && s.Wars.Count == 0; epoch++)
            ActionPhase.Run(s, epoch, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.NotEmpty(s.Wars);
        var war = s.Wars[0];
        Assert.Equal(0, war.AttackerId);
        Assert.Equal(1, war.DefenderId);
        Assert.Equal(WarGoal.Ore, war.Goal);
        Assert.InRange(war.GoalCells.Count, 1, 3);
        Assert.All(war.GoalCells, gc => Assert.True(s.CellAt(gc).Contested));
        Assert.Equal(war.GoalCells.Count, war.FrontCells.Count);
        var declared = s.Events.Single(e => e.Type == GalaxyEventType.WarStarted);
        Assert.Equal((int)WarGoal.Ore, declared.Detail);
    }

    [Fact]
    public void NoSecondWar_AgainstSameDefender()
    {
        var s = TwoPolities(militancy0: 0.95);
        for (int epoch = 0; epoch < 80; epoch++)
            ActionPhase.Run(s, epoch, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.True(s.Wars.Count(w => !w.Ended) <= 1, "one live war per polity pair");
    }

    [Fact]
    public void DepletedStockpile_PreventsDeclaration()
    {
        var s = TwoPolities(militancy0: 0.95);
        s.Polities[0].MilitaryStockpile = 0.0;
        for (int epoch = 0; epoch < 40; epoch++)
            ActionPhase.Run(s, epoch, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.Empty(s.Wars);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ActionPhaseTests" 2>&1 | tail -10`
Expected: FAIL — compile error, `ActionPhase` not defined.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/Sim/ActionPhase.cs` (new file, complete contents):

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 3 (economy spec §3/§6): expansion spends its budget on
/// frontier cells (stage-1 affinity cost model unchanged); militancy-gated war
/// declarations create persistent War objects with deficit-driven goals.</summary>
public static class ActionPhase
{
    private const double MinStockpileToDeclare = 0.5;
    private const int MaxGoalCells = 3;

    public static void Run(GalaxySkeleton s, int epoch, Dictionary<int, double> expansionBudgets)
    {
        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) continue;
            Expand(s, polity, epoch,
                expansionBudgets.TryGetValue(polity.Id, out var b) ? b : 0.0);
            DeclareWar(s, polity, epoch);
        }
    }

    private static void Expand(GalaxySkeleton s, Polity polity, int epoch, double budget)
    {
        if (budget <= 0) return;
        var species = s.Species[polity.SpeciesId];
        var owned = EpochSim.Owned(s, polity);
        if (owned.Count == 0) return;

        var seen = new HashSet<int>();
        var frontier = new List<(RegionCell cell, double cost)>();
        foreach (var cell in owned)
            foreach (var nc in HexGrid.Neighbors(cell.Coord))
                if (s.TryGetCell(nc, out var n) && n.OwnerPolityId < 0 && !n.IsVoid
                    && seen.Add(n.SpiralIndex))
                    frontier.Add((n, Cost(species, n)));
        frontier.Sort((x, y) => x.cost != y.cost
            ? x.cost.CompareTo(y.cost)
            : x.cell.SpiralIndex.CompareTo(y.cell.SpiralIndex));

        foreach (var (cell, cost) in frontier)
        {
            if (budget < cost) break;
            budget -= cost;
            cell.OwnerPolityId = polity.Id;
            cell.DevelopmentTier = 1;
            cell.Population = 0.1;
            cell.PopulationSpeciesId = species.Id;
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.CellClaimed,
                ActorPolityId = polity.Id, Q = cell.Q, R = cell.R,
            });
        }
    }

    /// <summary>Stage-1 cost model, unchanged (spec §3 architecture note).</summary>
    private static double Cost(SpeciesProfile species, RegionCell cell) =>
        1.0 / (0.05 + cell.MeanDensity * EpochSim.Affinity(species, cell))
        + (cell.IsChokepoint ? 2.0 : 0.0);

    private static void DeclareWar(GalaxySkeleton s, Polity polity, int epoch)
    {
        if (polity.MilitaryStockpile < MinStockpileToDeclare) return;
        var species = s.Species[polity.SpeciesId];
        var ctx = new RollContext(s.Config.MasterSeed, polity.CapitalCoord);
        if (ctx.NextDouble(RollChannel.SimWar, epoch, polity.Id) >= species.Militancy * 0.25)
            return;

        // Border cells of neighbors we are NOT already at war with, by owner.
        var candidates = new List<RegionCell>();
        var seen = new HashSet<int>();
        foreach (var cell in EpochSim.Owned(s, polity))
            foreach (var nc in HexGrid.Neighbors(cell.Coord))
                if (s.TryGetCell(nc, out var n) && n.OwnerPolityId >= 0
                    && n.OwnerPolityId != polity.Id
                    && !s.AtWar(polity.Id, n.OwnerPolityId)
                    && seen.Add(n.SpiralIndex))
                    candidates.Add(n);
        if (candidates.Count == 0) return;

        // Goal type from worst deficit (spec §6); no meaningful deficit →
        // chokepoint seizure if available, else punitive by system value.
        WarGoal goal;
        Func<RegionCell, double> score;
        if (polity.OreBalance < 0 && polity.OreBalance <= polity.ExoticsBalance)
        { goal = WarGoal.Ore; score = Economy.OrePotential; }
        else if (polity.ExoticsBalance < 0)
        { goal = WarGoal.Exotics; score = Economy.ExoticsPotential; }
        else if (candidates.Exists(c => c.IsChokepoint))
        { goal = WarGoal.Chokepoint; score = c => c.IsChokepoint ? 1.0 : 0.0; }
        else
        { goal = WarGoal.Punitive; score = c => Economy.SystemValue(species, c); }

        RegionCell? best = null;
        double bestScore = double.MinValue;
        foreach (var c in candidates)
        {
            double v = score(c);
            if (v > bestScore || (v == bestScore && best != null && c.SpiralIndex < best.SpiralIndex))
            { best = c; bestScore = v; }
        }
        if (best == null) return;
        int defenderId = best.OwnerPolityId;
        var defender = s.Polities[defenderId];

        var war = new War
        {
            Id = s.Wars.Count, AttackerId = polity.Id, DefenderId = defenderId,
            StartEpoch = epoch, Goal = goal,
        };
        war.GoalCells.Add(best.Coord);
        foreach (var nc in HexGrid.Neighbors(best.Coord))
        {
            if (war.GoalCells.Count >= MaxGoalCells) break;
            if (s.TryGetCell(nc, out var n) && n.OwnerPolityId == defenderId)
                war.GoalCells.Add(n.Coord);
        }
        foreach (var gc in war.GoalCells)
        {
            war.FrontCells.Add(gc);
            s.CellAt(gc).Contested = true;
        }
        s.Wars.Add(war);
        s.Events.Add(new GalaxyEvent
        {
            Epoch = epoch, Type = GalaxyEventType.WarStarted,
            ActorPolityId = polity.Id, TargetPolityId = defenderId,
            Q = best.Q, R = best.R, Detail = (int)goal,
            Magnitude = polity.MilitaryStockpile + defender.MilitaryStockpile,
        });
    }
}
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 142 total.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/ActionPhase.cs tests/Core.Tests/Galaxy/ActionPhaseTests.cs
git commit -m "feat(econ): action phase - budgeted expansion, war declaration with deficit-driven goals"
```

---

### Task 7: ResolutionPhase — fronts, weariness, termination

**Files:**
- Create: `src/Core/Galaxy/Sim/ResolutionPhase.cs`
- Test: `tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs` (create)

**Interfaces:**
- Consumes: `Economy.WarStrength`, `RollChannel.SimBattle`, `War` (task 1), `EpochSim.Owned`.
- Produces: `ResolutionPhase.Run(GalaxySkeleton s, int epoch)`. Flips contested cells, accrues weariness, terminates wars (`WarEnded` event, `Detail=(int)Outcome`), handles capital relocation (`LostCapital`) and extinction (`PolityExtinct`) — absorbing the stage-1 logic.

- [ ] **Step 1: Write the failing tests** — `tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs` (new file, complete contents):

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class ResolutionPhaseTests
{
    /// <summary>Two polities, one declared war over one goal cell owned by P1.</summary>
    private static GalaxySkeleton AtWarFixture(double attackerStock = 20.0, double defenderStock = 1.0)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        for (int i = 0; i < 2; i++)
            s.Species.Add(new SpeciesProfile { Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -1, CapitalR = 0, MilitaryStockpile = attackerStock });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 1, CapitalR = 0, MilitaryStockpile = defenderStock });
        var a = s.CellAt(new HexCoordinate(-1, 0));
        a.OwnerPolityId = 0; a.DevelopmentTier = 3; a.Population = 2; a.PopulationSpeciesId = 0;
        var goal = s.CellAt(new HexCoordinate(1, 0));
        goal.OwnerPolityId = 1; goal.DevelopmentTier = 2; goal.Population = 1; goal.PopulationSpeciesId = 1;
        var cap1 = s.CellAt(new HexCoordinate(2, 0));
        cap1.OwnerPolityId = 1; cap1.DevelopmentTier = 3; cap1.Population = 2; cap1.PopulationSpeciesId = 1;
        s.Polities[1].CapitalQ = 2; s.Polities[1].CapitalR = 0;
        var war = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 0, Goal = WarGoal.Punitive };
        war.GoalCells.Add(goal.Coord);
        war.FrontCells.Add(goal.Coord);
        goal.Contested = true;
        s.Wars.Add(war);
        return s;
    }

    [Fact]
    public void EveryWar_Terminates()
    {
        var s = AtWarFixture(attackerStock: 5.0, defenderStock: 5.0);
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);
        Assert.True(s.Wars[0].Ended, "weariness accrues monotonically; wars must end");
        Assert.NotEqual(WarOutcome.Ongoing, s.Wars[0].Outcome);
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.WarEnded
            && e.Detail == (int)s.Wars[0].Outcome);
    }

    [Fact]
    public void OverwhelmingAttacker_WinsAndAnnexesGoal()
    {
        var s = AtWarFixture(attackerStock: 100.0, defenderStock: 0.05);
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);
        Assert.Equal(WarOutcome.AttackerVictory, s.Wars[0].Outcome);
        Assert.Equal(0, s.CellAt(new HexCoordinate(1, 0)).OwnerPolityId);
        Assert.False(s.CellAt(new HexCoordinate(1, 0)).Contested, "fronts demilitarize at termination");
    }

    [Fact]
    public void ContestedCells_GetWarScarred()
    {
        var s = AtWarFixture();
        ResolutionPhase.Run(s, 0);
        Assert.True(s.CellAt(new HexCoordinate(1, 0)).WarScarred);
    }

    [Fact]
    public void Weariness_AccruesMonotonically_WhileLive()
    {
        var s = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        double last = 0;
        for (int epoch = 0; epoch < 10 && !s.Wars[0].Ended; epoch++)
        {
            ResolutionPhase.Run(s, epoch);
            Assert.True(s.Wars[0].AttackerWeariness >= last);
            last = s.Wars[0].AttackerWeariness;
        }
        Assert.True(last > 0);
    }

    [Fact]
    public void LosingLastCell_MarksExtinct_AndVictorHoldsEverything()
    {
        var s = AtWarFixture(attackerStock: 100.0, defenderStock: 0.05);
        // Make the goal cell P1's ONLY cell → its loss is extinction.
        var cap1 = s.CellAt(new HexCoordinate(2, 0));
        cap1.OwnerPolityId = -1; cap1.DevelopmentTier = 0; cap1.Population = 0; cap1.PopulationSpeciesId = -1;
        s.Polities[1].CapitalQ = 1; s.Polities[1].CapitalR = 0;
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);
        Assert.True(s.Polities[1].Extinct);
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.PolityExtinct && e.TargetPolityId == 1);
        Assert.Contains(s.Polities, p => p.Id == 1);   // retained, flagged
    }

    [Fact]
    public void StockpilesAttrit_WhileFighting()
    {
        var s = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        ResolutionPhase.Run(s, 0);
        Assert.True(s.Polities[0].MilitaryStockpile < 50.0);
        Assert.True(s.Polities[1].MilitaryStockpile < 50.0);
        Assert.True(s.Polities[0].MilitaryStockpile >= 0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~ResolutionPhaseTests" 2>&1 | tail -10`
Expected: FAIL — compile error, `ResolutionPhase` not defined.

- [ ] **Step 3: Implement** — `src/Core/Galaxy/Sim/ResolutionPhase.cs` (new file, complete contents):

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 4 (economy spec §6): active wars contest their fronts,
/// weariness accrues, termination resolves victory / white peace; capital
/// relocation and extinction absorbed from the stage-1 loop.</summary>
public static class ResolutionPhase
{
    private const double CommitFraction = 0.5;
    private const double AttritionRate = 0.3;
    private const double StockpileBreakFloor = 0.1;

    public static void Run(GalaxySkeleton s, int epoch)
    {
        foreach (var war in s.Wars)
        {
            if (war.Ended) continue;
            var attacker = s.Polities[war.AttackerId];
            var defender = s.Polities[war.DefenderId];
            var aSpecies = s.Species[attacker.SpeciesId];
            var dSpecies = s.Species[defender.SpeciesId];

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

            bool aBroke = Broke(war.AttackerWeariness, aSpecies, attacker);
            bool dBroke = Broke(war.DefenderWeariness, dSpecies, defender) || defender.Extinct;
            if (!aBroke && !dBroke) continue;

            WarOutcome outcome = aBroke && dBroke ? WarOutcome.WhitePeace
                : aBroke ? WarOutcome.DefenderVictory : WarOutcome.AttackerVictory;
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

    /// <summary>Fronts grow: goal cells plus any border cell between the belligerents
    /// adjacent to a front cell, ordered by spiral index for determinism.</summary>
    private static List<HexCoordinate> FrontInOrder(GalaxySkeleton s, War war)
    {
        var cells = new List<RegionCell>();
        foreach (var coord in war.FrontCells) cells.Add(s.CellAt(coord));
        cells.Sort((x, y) => x.SpiralIndex.CompareTo(y.SpiralIndex));
        var result = new List<HexCoordinate>();
        foreach (var c in cells) result.Add(c.Coord);
        return result;
    }

    private static double Weariness(GalaxySkeleton s, Polity p, int cellsLostThisEpoch)
    {
        bool shortages = p.ProvisionsBalance < 0 || p.OreBalance < 0;
        return s.Config.WarWearinessRate
            * (shortages ? 1.5 : 1.0) * (1.0 + 0.2 * cellsLostThisEpoch);
    }

    private static bool Broke(double weariness, SpeciesProfile species, Polity p) =>
        weariness >= 0.5 + species.Cohesion || p.MilitaryStockpile < StockpileBreakFloor;

    private static int LiveWarCount(GalaxySkeleton s, int polityId)
    {
        int n = 0;
        foreach (var w in s.Wars)
            if (!w.Ended && (w.AttackerId == polityId || w.DefenderId == polityId)) n++;
        return n;
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>Stage-1 capital-relocation and extinction logic, absorbed (spec §6).</summary>
    private static void HandleCapitalAndExtinction(GalaxySkeleton s, int epoch,
        Polity loser, Polity victor, RegionCell takenCell)
    {
        if (loser.CapitalCoord.Equals(takenCell.Coord))
        {
            RegionCell? best = null;
            foreach (var c in EpochSim.Owned(s, loser))
                if (best == null || c.DevelopmentTier > best.DevelopmentTier
                    || (c.DevelopmentTier == best.DevelopmentTier && c.SpiralIndex < best.SpiralIndex))
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
        if (EpochSim.Owned(s, loser).Count == 0 && !loser.Extinct)
        {
            loser.Extinct = true;
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.PolityExtinct,
                ActorPolityId = victor.Id, TargetPolityId = loser.Id,
                Q = takenCell.Q, R = takenCell.R,
            });
        }
    }
}
```

- [ ] **Step 4: Run the full gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 148 total.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/Sim/ResolutionPhase.cs tests/Core.Tests/Galaxy/ResolutionPhaseTests.cs
git commit -m "feat(econ): resolution phase - front contests, weariness, victory/white-peace termination"
```

---

### Task 8: EpochSim rewrite — phase pipeline orchestrator (RED WINDOW OPENS)

**Files:**
- Modify: `src/Core/Galaxy/EpochSim.cs` (gut the stage-1 loop; keep `Affinity` and `Owned`)
- Modify: `tests/Core.Tests/Galaxy/EpochSimTests.cs` (expectations updated)
- Possibly modify: `tests/Core.Tests/Galaxy/SerializerTests.cs`, `tests/Core.Tests/Galaxy/RegionIntegrationTests.cs` (ECONMIGRATION markers only)

**Interfaces:**
- Consumes: all four phases (tasks 4–7).
- Produces: `EpochSim.Run(GalaxySkeleton)` — same signature, new behavior. `EpochSim.Affinity` and `EpochSim.Owned` remain `internal static` at their current signatures.

- [ ] **Step 1: Replace `EpochSim.cs` body** — the file becomes exactly:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch-sim orchestrator (economy spec §3): each epoch runs the four
/// global phases in order — income → allocation → action → resolution. Stage 5's
/// news phase will slot in before allocation. Deterministic iteration throughout:
/// cells by SpiralIndex, polities by Id, wars by Id.</summary>
public static class EpochSim
{
    public static void Run(GalaxySkeleton s)
    {
        for (int epoch = 0; epoch < s.Config.EpochCount; epoch++)
        {
            IncomePhase.Run(s, epoch);
            var expansionBudgets = AllocationPhase.Run(s, epoch);
            ActionPhase.Run(s, epoch, expansionBudgets);
            ResolutionPhase.Run(s, epoch);
        }
    }

    internal static List<RegionCell> Owned(GalaxySkeleton s, Polity p) =>
        s.Cells.Where(c => c.OwnerPolityId == p.Id).ToList();

    /// <summary>Species-relative terrain (regional spec §6.1): how comfortable a
    /// cell's expected world mix is for an embodiment. Read by expansion cost and
    /// provisions production.</summary>
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
}
```

(The old `Expand`, `Develop`, `War`, `Ctx`, and `Adjacent` members are deleted — their logic now lives in the phases. `RollChannel.SimDevelopment` is retired, already commented in task 1.)

- [ ] **Step 2: Run the full gate and triage**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -30`
Expected outcome and triage rules:
- `SerializerTests.GoldenSnapshot_SmallGalaxyHeader` WILL fail (event count literal `30` no longer holds). Mark with `// ECONMIGRATION: re-freeze in serializer-v4 task` above the failing asserts and change nothing else about it.
- `EpochSimTests` behavior tests (determinism, wilds remain, registry tracing, chronology, extinct retention) SHOULD pass — the invariants are behavior-preserving. If `Polities_Expand_ButWildsRemain` or `ClaimedFraction_AtReferenceConfig_IsWithinAcceptanceBand` fails, do NOT retune sim constants in this task: mark the specific assert with `// ECONMIGRATION: retune in shape-band task` and record the observed value in the task report.
- `RegionIntegrationTests` should pass (it tests hex↔skeleton relationships, not sim literals). If any test fails on a sim-outcome literal, apply the same ECONMIGRATION marker treatment and report it.
- Everything else must be green. Any other failure is a bug in tasks 4–7 — stop and fix before proceeding.

- [ ] **Step 3: Update `EpochSimTests` chronology expectation if needed**

`EventLog_IsChronological_AndReferentiallyIntact` should still pass (phases append within ascending epochs). Verify it does; no edit expected.

- [ ] **Step 4: Re-run the gate; confirm only ECONMIGRATION-marked tests are red**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -10`
Expected: failures limited to ECONMIGRATION-marked tests (ideally exactly one: the golden). Report the exact red list verbatim.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Galaxy/EpochSim.cs tests/Core.Tests/Galaxy/EpochSimTests.cs tests/Core.Tests/Galaxy/SerializerTests.cs
git commit -m "feat(econ)!: EpochSim becomes the income/allocation/action/resolution phase pipeline (red window open)"
```

---

### Task 9: Serializer schema v4 (RED WINDOW CLOSES)

**Files:**
- Modify: `src/Core/Galaxy/GalaxySkeleton.cs` (`SchemaVersion` 3 → 4)
- Modify: `src/Core/Galaxy/SkeletonSerializer.cs`
- Modify: `tests/Core.Tests/Galaxy/SerializerTests.cs`

**Interfaces:**
- Consumes: all task-1 fields, `War`.
- Produces: schema v4 wire format (exact field orders below) — the REPL and any future loader rely on it.

Wire-format deltas (append-only within each record):
- Header: `STARGEN-SKELETON|4`
- `CONFIG` appends 5 fields: `WarWearinessRate, StockpileDecayRate, TechThresholdBase, TradeIncomeWeight, ProvisionsPerPop` (all "R"-formatted) → indices 16–20.
- `POLITY` appends 7 fields: `MilitaryStockpile, TechTier, ExoticsInvested, Wealth, ProvisionsBalance, OreBalance, ExoticsBalance` → indices 7–13 (doubles "R"-formatted, TechTier int).
- `CELL` appends 3 fields: `Population ("R"), PopulationSpeciesId (int), RouteThroughput ("R")` → indices 12–14.
- New `WAR` records, written after all CELL/ANCHOR records and before EVENT records, ordered by id:
  `WAR|id|attackerId|defenderId|startEpoch|goal|attWear|defWear|attLost|defLost|ended|outcome|goalCells|frontCells`
  where each cell list is `q:r` pairs joined by `;`, or the literal `-` when empty.
- `EVENT` appends 1 field: `Detail` (int) → index 8.

- [ ] **Step 1: Update the failing/affected tests first** — in `tests/Core.Tests/Galaxy/SerializerTests.cs`:
  - `SchemaVersionMismatch_Throws...`: change `"STARGEN-SKELETON|3"` → `"STARGEN-SKELETON|4"` in the `Replace` call.
  - `Load_RecordBeforeConfig_Throws`: fixture header `"STARGEN-SKELETON|3\n..."` → `"STARGEN-SKELETON|4\n..."`.
  - `Load_RejectsSchemaV2`: unchanged (still rejects).
  - `GoldenSnapshot_SmallGalaxyHeader`: change header literal to `"STARGEN-SKELETON|4"`; delete the ECONMIGRATION marker; replace the two frozen literals with the values observed on first run (see step 4).
  - Add these new tests to the class:

```csharp
    [Fact]
    public void RoundTrip_PreservesEconomyState_AndWars()
    {
        var original = Build();
        // Ensure at least one war exists to round-trip even if this seed fought none.
        if (original.Wars.Count == 0)
        {
            var w = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 3,
                Goal = WarGoal.Chokepoint, AttackerWeariness = 0.4, DefenderWeariness = 1.1,
                AttackerCellsLost = 1, DefenderCellsLost = 2, Ended = true,
                Outcome = WarOutcome.WhitePeace };
            w.GoalCells.Add(original.Cells[0].Coord);
            original.Wars.Add(w);
        }
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(original)));
        Assert.Equal(SkeletonSerializer.ToText(original), SkeletonSerializer.ToText(loaded));
        Assert.Equal(original.Wars.Count, loaded.Wars.Count);
        for (int i = 0; i < original.Wars.Count; i++)
        {
            Assert.Equal(original.Wars[i].Goal, loaded.Wars[i].Goal);
            Assert.Equal(original.Wars[i].Outcome, loaded.Wars[i].Outcome);
            Assert.Equal(original.Wars[i].GoalCells, loaded.Wars[i].GoalCells);
            Assert.Equal(original.Wars[i].FrontCells, loaded.Wars[i].FrontCells);
        }
        for (int i = 0; i < original.Polities.Count; i++)
        {
            Assert.Equal(original.Polities[i].MilitaryStockpile, loaded.Polities[i].MilitaryStockpile);
            Assert.Equal(original.Polities[i].TechTier, loaded.Polities[i].TechTier);
            Assert.Equal(original.Polities[i].Wealth, loaded.Polities[i].Wealth);
        }
        for (int i = 0; i < original.Cells.Count; i++)
        {
            Assert.Equal(original.Cells[i].Population, loaded.Cells[i].Population);
            Assert.Equal(original.Cells[i].PopulationSpeciesId, loaded.Cells[i].PopulationSpeciesId);
            Assert.Equal(original.Cells[i].RouteThroughput, loaded.Cells[i].RouteThroughput);
        }
    }

    [Fact]
    public void RoundTrip_PreservesEconomyKnobs()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig
        {
            MasterSeed = 11, GalaxyRadiusCells = 3,
            WarWearinessRate = 0.2, StockpileDecayRate = 0.05,
            TechThresholdBase = 20.0, TradeIncomeWeight = 0.8, ProvisionsPerPop = 1.5,
        });
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(s)));
        Assert.Equal(0.2, loaded.Config.WarWearinessRate);
        Assert.Equal(0.05, loaded.Config.StockpileDecayRate);
        Assert.Equal(20.0, loaded.Config.TechThresholdBase);
        Assert.Equal(0.8, loaded.Config.TradeIncomeWeight);
        Assert.Equal(1.5, loaded.Config.ProvisionsPerPop);
    }

    [Fact]
    public void Load_RejectsSchemaV3()
    {
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader("STARGEN-SKELETON|3\nEND\n")));
    }
```

- [ ] **Step 2: Bump the version and extend the serializer**

`GalaxySkeleton.cs`: `public const int SchemaVersion = 4;`

`SkeletonSerializer.cs` — `Save`:
- CONFIG line: append to the `string.Join` argument list:

```csharp
            c.WarWearinessRate.ToString("R", Inv), c.StockpileDecayRate.ToString("R", Inv),
            c.TechThresholdBase.ToString("R", Inv), c.TradeIncomeWeight.ToString("R", Inv),
            c.ProvisionsPerPop.ToString("R", Inv)));
```

- POLITY line: append:

```csharp
                p.MilitaryStockpile.ToString("R", Inv), p.TechTier.ToString(Inv),
                p.ExoticsInvested.ToString("R", Inv), p.Wealth.ToString("R", Inv),
                p.ProvisionsBalance.ToString("R", Inv), p.OreBalance.ToString("R", Inv),
                p.ExoticsBalance.ToString("R", Inv)));
```

- CELL line: append:

```csharp
                cell.Population.ToString("R", Inv), cell.PopulationSpeciesId.ToString(Inv),
                cell.RouteThroughput.ToString("R", Inv)));
```

- After the cell/anchor loop and before the events loop, insert:

```csharp
        foreach (var war in s.Wars)
            w.WriteLine(string.Join("|", "WAR", war.Id.ToString(Inv),
                war.AttackerId.ToString(Inv), war.DefenderId.ToString(Inv),
                war.StartEpoch.ToString(Inv), ((int)war.Goal).ToString(Inv),
                war.AttackerWeariness.ToString("R", Inv), war.DefenderWeariness.ToString("R", Inv),
                war.AttackerCellsLost.ToString(Inv), war.DefenderCellsLost.ToString(Inv),
                war.Ended ? "1" : "0", ((int)war.Outcome).ToString(Inv),
                CellList(war.GoalCells), CellList(war.FrontCells)));
```

with this private helper added to the class:

```csharp
    private static string CellList(System.Collections.Generic.List<HexCoordinate> cells)
    {
        if (cells.Count == 0) return "-";
        var parts = new string[cells.Count];
        for (int i = 0; i < cells.Count; i++)
            parts[i] = cells[i].Q.ToString(Inv) + ":" + cells[i].R.ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseCellList(string field, System.Collections.Generic.List<HexCoordinate> into)
    {
        if (field == "-") return;
        foreach (var pair in field.Split(';'))
        {
            var qr = pair.Split(':');
            into.Add(new HexCoordinate(int.Parse(qr[0], Inv), int.Parse(qr[1], Inv)));
        }
    }
```

- EVENT line: append `, e.Detail.ToString(Inv)` as the final joined field.

`SkeletonSerializer.cs` — `Load` switch:
- `CONFIG` case: append property initializers `WarWearinessRate = double.Parse(f[16], Inv), StockpileDecayRate = double.Parse(f[17], Inv), TechThresholdBase = double.Parse(f[18], Inv), TradeIncomeWeight = double.Parse(f[19], Inv), ProvisionsPerPop = double.Parse(f[20], Inv),`
- `POLITY` case: append `MilitaryStockpile = double.Parse(f[7], Inv), TechTier = int.Parse(f[8], Inv), ExoticsInvested = double.Parse(f[9], Inv), Wealth = double.Parse(f[10], Inv), ProvisionsBalance = double.Parse(f[11], Inv), OreBalance = double.Parse(f[12], Inv), ExoticsBalance = double.Parse(f[13], Inv),`
- `CELL` case: append statements `cell.Population = double.Parse(f[12], Inv); cell.PopulationSpeciesId = int.Parse(f[13], Inv); cell.RouteThroughput = double.Parse(f[14], Inv);`
- New `WAR` case:

```csharp
                    case "WAR":
                        var war = new War
                        {
                            Id = int.Parse(f[1], Inv), AttackerId = int.Parse(f[2], Inv),
                            DefenderId = int.Parse(f[3], Inv), StartEpoch = int.Parse(f[4], Inv),
                            Goal = (WarGoal)int.Parse(f[5], Inv),
                            AttackerWeariness = double.Parse(f[6], Inv),
                            DefenderWeariness = double.Parse(f[7], Inv),
                            AttackerCellsLost = int.Parse(f[8], Inv),
                            DefenderCellsLost = int.Parse(f[9], Inv),
                            Ended = f[10] == "1", Outcome = (WarOutcome)int.Parse(f[11], Inv),
                        };
                        ParseCellList(f[12], war.GoalCells);
                        ParseCellList(f[13], war.FrontCells);
                        s!.Wars.Add(war);
                        break;
```

- `EVENT` case: append `Detail = int.Parse(f[8], Inv),`

- [ ] **Step 3: Run the serializer tests**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~SerializerTests" 2>&1 | tail -15`
Expected: all pass EXCEPT `GoldenSnapshot_SmallGalaxyHeader` (stale frozen literals).

- [ ] **Step 4: Re-freeze the golden**

Temporarily print the observed values: run this from repo root and read the two numbers off the failure message (`Assert.Equal` shows expected vs actual):

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~GoldenSnapshot" 2>&1 | tail -15`

Set the two literals (`s.Polities.Count`, `s.Events.Count`) in `GoldenSnapshot_SmallGalaxyHeader` to the observed actual values, and update the comment to note they were re-frozen for the economy slice. Do NOT adjust any sim constant to steer these numbers.

- [ ] **Step 5: Run the FULL gate — the red window must close**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, everything green (151 total: 148 + 3 new serializer tests). Also confirm no `ECONMIGRATION` markers remain: `grep -rn "ECONMIGRATION" tests/ src/` must return nothing (remove any stragglers by resolving them, not deleting tests).

- [ ] **Step 6: Commit**

```bash
git add src/Core/Galaxy/GalaxySkeleton.cs src/Core/Galaxy/SkeletonSerializer.cs tests/Core.Tests/Galaxy/SerializerTests.cs
git commit -m "feat(econ)!: skeleton schema v4 - econ fields, war registry, event detail; goldens re-frozen (red window closed)"
```

---

### Task 10: Invariant suite, blockade end-to-end, shape bands

**Files:**
- Create: `tests/Core.Tests/Galaxy/EconomyInvariantTests.cs`
- Modify: `tests/Core.Tests/Galaxy/EpochSimTests.cs` (claimed-fraction band finalized if marked in task 8)
- Possibly modify: `src/Core/Galaxy/Sim/*.cs` constants (tuning dials, see step 3)

**Interfaces:**
- Consumes: full pipeline via `SkeletonBuilder.Build`.
- Produces: the slice's acceptance bands; later REPL work and Unity parity trust these.

- [ ] **Step 1: Write the invariant tests** — `tests/Core.Tests/Galaxy/EconomyInvariantTests.cs` (new file, complete contents):

```csharp
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>Spec §9 sim invariants + shape bands over built galaxies.</summary>
public class EconomyInvariantTests
{
    private static GalaxySkeleton Build(ulong seed, int radius = 8) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });

    [Fact]
    public void NothingNegativeOrNaN_AcrossSeeds()
    {
        for (ulong seed = 40; seed < 45; seed++)
        {
            var s = Build(seed);
            foreach (var p in s.Polities)
            {
                Assert.True(p.MilitaryStockpile >= 0 && !double.IsNaN(p.MilitaryStockpile));
                Assert.True(p.Wealth >= 0 && !double.IsNaN(p.Wealth));
                Assert.True(p.ExoticsInvested >= 0 && !double.IsNaN(p.ExoticsInvested));
                Assert.True(p.TechTier >= 0);
                Assert.False(double.IsNaN(p.ProvisionsBalance) || double.IsNaN(p.OreBalance)
                          || double.IsNaN(p.ExoticsBalance));
            }
            foreach (var c in s.Cells)
            {
                Assert.True(c.Population >= 0 && !double.IsNaN(c.Population));
                Assert.True(c.RouteThroughput >= 0 && !double.IsNaN(c.RouteThroughput));
            }
            foreach (var w in s.Wars)
            {
                Assert.True(w.AttackerWeariness >= 0 && w.DefenderWeariness >= 0);
                Assert.NotEmpty(w.GoalCells);
            }
        }
    }

    [Fact]
    public void Wars_TerminateOrSurviveToFinalEpoch_NeverDangle()
    {
        for (ulong seed = 40; seed < 45; seed++)
        {
            var s = Build(seed);
            foreach (var w in s.Wars)
            {
                if (w.Ended) Assert.NotEqual(WarOutcome.Ongoing, w.Outcome);
                else
                    // Live-at-final-epoch wars are the war-zone source: front stays contested.
                    Assert.All(w.FrontCells, fc => Assert.True(s.CellAt(fc).Contested));
            }
            // Every ended war produced a WarEnded event with matching outcome.
            foreach (var w in s.Wars.Where(w => w.Ended))
                Assert.Contains(s.Events, e => e.Type == GalaxyEventType.WarEnded
                    && e.ActorPolityId == w.AttackerId && e.TargetPolityId == w.DefenderId
                    && e.Detail == (int)w.Outcome);
        }
    }

    [Fact]
    public void Blockade_ReducesDeliveredFlow_ConstructedTwin()
    {
        GalaxySkeleton MakeTwin(bool blockaded)
        {
            var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
            foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
            s.Species.Add(new SpeciesProfile { Id = 0, Name = "S0",
                Embodiment = Embodiment.TerranAnalog, Expansionism = 0.5, Cohesion = 0.5,
                Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
            s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -2, CapitalR = 0 });
            foreach (var (q, r) in new[] { (-2, 0), (-1, 0), (0, 0) })
            {
                var c = s.CellAt(new HexCoordinate(q, r));
                c.OwnerPolityId = 0; c.PopulationSpeciesId = 0;
            }
            var consumer = s.CellAt(new HexCoordinate(-2, 0));
            consumer.Population = 4.0; consumer.DevelopmentTier = 1;
            var producer = s.CellAt(new HexCoordinate(0, 0));
            producer.Population = 1.0; producer.DevelopmentTier = 5;
            s.CellAt(new HexCoordinate(-1, 0)).Population = 0.1;
            s.CellAt(new HexCoordinate(-1, 0)).DevelopmentTier = 1;
            if (blockaded)
                // Sever every route between producer and consumer: contest the full
                // q=-1 column AND the alternate lattice detours around it.
                foreach (var c in s.Cells.Where(c => c.Q == -1)) c.Contested = true;
            return s;
        }

        var open = MakeTwin(false);
        var blocked = MakeTwin(true);
        IncomePhase.Run(open, 0);
        IncomePhase.Run(blocked, 0);
        double openPop = open.CellAt(new HexCoordinate(-2, 0)).Population;
        double blockedPop = blocked.CellAt(new HexCoordinate(-2, 0)).Population;
        Assert.True(blockedPop < openPop,
            $"blockaded twin must starve: open {openPop} vs blocked {blockedPop}");
    }

    [Fact]
    public void Throughput_OnlyOnCellsConnectedToOwnedTerritory()
    {
        var s = Build(42);
        foreach (var c in s.Cells.Where(c => c.RouteThroughput > 0))
            Assert.False(c.IsVoid, "flow never transits void cells");
    }

    [Fact]
    public void ShapeBands_ReferenceConfig()
    {
        var s = Build(42);
        var claimable = s.Cells.Where(c => !c.IsVoid).ToList();
        double claimed = (double)claimable.Count(c => c.OwnerPolityId >= 0) / claimable.Count;
        Assert.InRange(claimed, 0.2, 0.8);   // reopened 73.5% conversation: ceiling now 0.8

        int living = s.Polities.Count(p => !p.Extinct);
        Assert.InRange(living, 2, s.Polities.Count);

        // Economy actually ran: someone produced, someone has a stockpile.
        Assert.Contains(s.Polities, p => p.MilitaryStockpile > 0);
        Assert.Contains(s.Cells, c => c.RouteThroughput > 0);

        // Famines are possible but not the norm (famine dial sanity).
        int famines = s.Events.Count(e => e.Type == GalaxyEventType.Famine);
        Assert.True(famines < s.Config.EpochCount * s.Polities.Count,
            "famine every polity-epoch means the dial is broken");
    }

    [Fact]
    public void WarOutcomes_BothPathsOccur_AcrossSeeds()
    {
        int victories = 0, whitePeaces = 0;
        for (ulong seed = 40; seed < 50; seed++)
        {
            var s = Build(seed);
            victories += s.Wars.Count(w => w.Outcome is WarOutcome.AttackerVictory or WarOutcome.DefenderVictory);
            whitePeaces += s.Wars.Count(w => w.Outcome == WarOutcome.WhitePeace);
        }
        Assert.True(victories > 0, "no war ever produced a victor across 10 seeds - resolution is broken");
        Assert.True(whitePeaces + victories > 0, "no wars ended at all across 10 seeds");
    }
}
```

- [ ] **Step 2: Run the new tests**

Run: `dotnet test StarSystemGeneration.sln --filter "FullyQualifiedName~EconomyInvariantTests" 2>&1 | tail -15`

- [ ] **Step 3: Tune if bands fail — the dials, in preference order**

Tuning is EXPECTED here; adjust constants only (never structure), re-run after each change, and record every adjustment in the task report:
1. Claimed fraction too low → raise `AllocationPhase.DevIncomeBase` (1.5 → up to 3.0) or the expansion weight's effective share; too high → lower `DevIncomeBase` toward 1.0.
2. No wars / no victors → lower `ActionPhase.MinStockpileToDeclare` (0.5 → 0.25) or raise `ResolutionPhase.CommitFraction` (0.5 → 0.7); wars never ending → raise `GalaxyConfig.WarWearinessRate` default (0.15 → 0.25).
3. Famine storms → raise homeworld seed development or lower `GalaxyConfig.ProvisionsPerPop` default (1.0 → 0.8). Famine never → raise it.
4. If a `GalaxyConfig` default changes, the serializer round-trip tests are unaffected (values, not schema), but the task-9 golden counts may shift — re-freeze once more and say so in the commit message.
Also finalize any `ECONMIGRATION`-marked band in `EpochSimTests` now (align its range with `ShapeBands_ReferenceConfig` and remove the marker).

- [ ] **Step 4: Run the FULL gate**

Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5`
Expected: PASS, 157 total, zero ECONMIGRATION markers in the tree.

- [ ] **Step 5: Commit**

```bash
git add tests/Core.Tests/Galaxy/EconomyInvariantTests.cs tests/Core.Tests/Galaxy/EpochSimTests.cs src/Core/Galaxy/Sim
git commit -m "test(econ): invariant suite, blockade twin scenario, shape bands (+tuning)"
```

---

### Task 11: Inspector REPL — layers, polity, chronicle, economy stats

**Files:**
- Modify: `src/Inspector/GalaxyMapView.cs` (three new layers + legends)
- Modify: `src/Inspector/Repl.cs` (`polity`, `chronicle` commands; stats economy block; help text)
- Create: `src/Inspector/EconomyReport.cs`
- Create: `src/Inspector/ChronicleView.cs`
- Test: none automated (Inspector has no test project — REPL verification is task 12); the gate is compile + full suite green.

**Interfaces:**
- Consumes: everything shipped in tasks 1–10; `Economy.DisplayBaseline`.
- Produces: REPL surface for the live acceptance (task 12).

- [ ] **Step 1: Map layers** — in `GalaxyMapView.CellChar`, add three arms before the default `_ =>` arm:

```csharp
        "trade" => c.IsVoid ? ' '
            : c.RouteThroughput <= 0 ? '.'
            : DensityRamp[System.Math.Clamp((int)(System.Math.Sqrt(c.RouteThroughput) * 3.0), 1, 9)],
        "economy" => c.IsVoid ? ' ' : EconomyChar(s, c),
        "war" => c.IsVoid ? ' ' : c.Contested ? '!' : c.WarScarred ? 'x'
            : c.IsChokepoint ? '^' : '.',
```

and add these private helpers to the class:

```csharp
    private static char EconomyChar(GalaxySkeleton s, RegionCell c)
    {
        var species = c.OwnerPolityId >= 0
            ? s.Species[s.Polities[c.OwnerPolityId].SpeciesId]
            : Economy.DisplayBaseline;
        double p = Economy.ProvisionsPotential(species, c);
        double o = Economy.OrePotential(c);
        double e = Economy.ExoticsPotential(c);
        bool anchored = c.Anchors.Count > 0;
        char glyph = p >= o && p >= e ? 'p' : o >= e ? 'o' : 'e';
        return anchored ? char.ToUpperInvariant(glyph) : glyph;
    }
```

and in `Legend` add:

```csharp
        "trade" => "route throughput: .=none " + DensityRamp.Substring(1) + " low->high",
        "economy" => "p/o/e=dominant production (P/O/E=anchored) provisions/ore/exotics",
        "war" => "!=contested front x=war-scarred ^=chokepoint .=quiet",
```

- [ ] **Step 2: EconomyReport** — `src/Inspector/EconomyReport.cs` (new file, complete contents):

```csharp
using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;

namespace StarGen.Inspector;

/// <summary>Economy aggregates appended to `stats` when a galaxy is loaded
/// (economy spec §8): production totals, famines, war ledger, tech spread,
/// throughput distribution.</summary>
public static class EconomyReport
{
    public static string Build(GalaxySkeleton s)
    {
        var sb = new StringBuilder();
        var living = s.Polities.Where(p => !p.Extinct).ToList();
        sb.AppendLine("— economy —");
        sb.AppendLine($"polities: {living.Count} living / {s.Polities.Count - living.Count} extinct"
            + $" · mean tech tier {(living.Count == 0 ? 0 : living.Average(p => p.TechTier)):F1}"
            + $" · total stockpile {living.Sum(p => p.MilitaryStockpile):F1}");
        sb.AppendLine($"balances (sum of living): provisions {living.Sum(p => p.ProvisionsBalance):F1}"
            + $" · ore {living.Sum(p => p.OreBalance):F1}"
            + $" · exotics {living.Sum(p => p.ExoticsBalance):F1}");
        int started = s.Events.Count(e => e.Type == GalaxyEventType.WarStarted);
        int ended = s.Events.Count(e => e.Type == GalaxyEventType.WarEnded);
        int white = s.Wars.Count(w => w.Outcome == WarOutcome.WhitePeace);
        sb.AppendLine($"wars: {started} started · {ended} ended ({white} white peace)"
            + $" · {s.Wars.Count(w => !w.Ended)} live"
            + $" · famines {s.Events.Count(e => e.Type == GalaxyEventType.Famine)}"
            + $" · trade blocked {s.Events.Count(e => e.Type == GalaxyEventType.TradeBlocked)}");
        var busy = s.Cells.Where(c => c.RouteThroughput > 0).ToList();
        sb.AppendLine(busy.Count == 0
            ? "trade: no routed flows"
            : $"trade: {busy.Count} transit cells · max throughput {busy.Max(c => c.RouteThroughput):F1}"
              + $" · total {busy.Sum(c => c.RouteThroughput):F1}");
        return sb.ToString();
    }
}
```

- [ ] **Step 3: ChronicleView** — `src/Inspector/ChronicleView.cs` (new file, complete contents):

```csharp
using System.Text;
using StarGen.Core.Galaxy;

namespace StarGen.Inspector;

/// <summary>Event-log browser (economy spec §8): renders all event types, optional
/// polity filter, most recent last. `Describe` is the single formatting authority.</summary>
public static class ChronicleView
{
    public static string Build(GalaxySkeleton s, int polityFilter = -1, int tail = 60)
    {
        var sb = new StringBuilder();
        int shown = 0, matched = 0;
        foreach (var e in s.Events)
            if (polityFilter < 0 || e.ActorPolityId == polityFilter || e.TargetPolityId == polityFilter)
                matched++;
        int skip = matched - tail;
        foreach (var e in s.Events)
        {
            if (polityFilter >= 0 && e.ActorPolityId != polityFilter && e.TargetPolityId != polityFilter)
                continue;
            if (skip-- > 0) continue;
            sb.AppendLine(Describe(s, e));
            shown++;
        }
        if (shown == 0) sb.AppendLine("no matching events");
        else if (matched > shown) sb.AppendLine($"({matched - shown} earlier events omitted)");
        return sb.ToString();
    }

    public static string Describe(GalaxySkeleton s, GalaxyEvent e)
    {
        string actor = Name(s, e.ActorPolityId);
        string target = e.TargetPolityId >= 0 ? Name(s, e.TargetPolityId) : "";
        string at = $"[{e.Q},{e.R}]";
        return e.Type switch
        {
            GalaxyEventType.CellClaimed => $"epoch {e.Epoch}: {actor} claimed {at}",
            GalaxyEventType.WarStarted =>
                $"epoch {e.Epoch}: {actor} declared a {(WarGoal)e.Detail} war on {target} at {at}",
            GalaxyEventType.CellTaken => $"epoch {e.Epoch}: {actor} took {at} from {target}",
            GalaxyEventType.LostCapital => $"epoch {e.Epoch}: {target} lost its capital {at} to {actor}",
            GalaxyEventType.PolityExtinct => $"epoch {e.Epoch}: {actor} extinguished {target} at {at}",
            GalaxyEventType.WarEnded =>
                $"epoch {e.Epoch}: war of {actor} vs {target} ended - {(WarOutcome)e.Detail} at {at}",
            GalaxyEventType.TechAdvance => $"epoch {e.Epoch}: {actor} reached tech tier {e.Detail}",
            GalaxyEventType.Famine =>
                $"epoch {e.Epoch}: famine in {actor} territory around {at} (magnitude {e.Magnitude:F1})",
            GalaxyEventType.TradeBlocked =>
                $"epoch {e.Epoch}: {actor}'s trade blockaded (lost {e.Magnitude:F1})",
            _ => $"epoch {e.Epoch}: {e.Type} {actor} {at}",
        };
    }

    private static string Name(GalaxySkeleton s, int id) =>
        id >= 0 && id < s.Polities.Count ? s.Polities[id].Name : $"polity {id}";
}
```

- [ ] **Step 4: Repl wiring** — in `src/Inspector/Repl.cs`:
- Replace the two `help` layer/command lines with:

```csharp
                    Console.WriteLine("seed <n> | galaxy <seed> [radiusCells] | goto <q> <r> | next | prev | reroll");
                    Console.WriteLine("find <criterion> | stats <n> | map [layer] | cell <q> <r> | polity <id> | chronicle [polityId]");
                    Console.WriteLine("gsave <path> | gload <path> | quit");
                    Console.WriteLine("map layers: density | polity | zone | dev | lean | trade | economy | war");
                    Console.WriteLine("find criteria: overlay | <overlay-id> | settled | sapient");
```

- In the `stats` case, after the existing `Console.WriteLine(StatsReport.Build(...));` add:

```csharp
                    if (_galaxy?.Skeleton is { } statsSk) Console.WriteLine(EconomyReport.Build(statsSk));
```

(mind the `break;` stays after both lines).
- Add two new cases before `default:`:

```csharp
                case "polity" when parts.Length == 2 && _galaxy?.Skeleton is { } skPol
                        && int.TryParse(parts[1], out var polityId):
                {
                    if (polityId < 0 || polityId >= skPol.Polities.Count)
                    { Console.WriteLine("no such polity"); break; }
                    var p = skPol.Polities[polityId];
                    var sp = skPol.Species[p.SpeciesId];
                    int cells = 0; double pop = 0;
                    foreach (var c in skPol.Cells)
                        if (c.OwnerPolityId == p.Id) { cells++; pop += c.Population; }
                    Console.WriteLine($"{p.Name} (id {p.Id}){(p.Extinct ? " EXTINCT" : "")}"
                        + $" · species {sp.Name} ({sp.Embodiment}) · capital [{p.CapitalQ},{p.CapitalR}]");
                    Console.WriteLine($"  {cells} cells · population {pop:F1} · tech tier {p.TechTier}"
                        + $" · stockpile {p.MilitaryStockpile:F1} · wealth {p.Wealth:F1}");
                    Console.WriteLine($"  balances: provisions {p.ProvisionsBalance:F1}"
                        + $" · ore {p.OreBalance:F1} · exotics {p.ExoticsBalance:F1}"
                        + $" (invested {p.ExoticsInvested:F1})");
                    foreach (var w in skPol.Wars)
                    {
                        if (w.AttackerId != p.Id && w.DefenderId != p.Id) continue;
                        string other = skPol.Polities[w.AttackerId == p.Id ? w.DefenderId : w.AttackerId].Name;
                        double wear = w.AttackerId == p.Id ? w.AttackerWeariness : w.DefenderWeariness;
                        Console.WriteLine(w.Ended
                            ? $"  war vs {other}: {w.Goal}, ended epoch-started {w.StartEpoch} - {w.Outcome}"
                            : $"  war vs {other}: {w.Goal}, since epoch {w.StartEpoch}, weariness {wear:F2}");
                    }
                    break;
                }
                case "chronicle" when _galaxy?.Skeleton is { } skChr:
                {
                    int filter = parts.Length >= 2 && int.TryParse(parts[1], out var pf) ? pf : -1;
                    Console.WriteLine(ChronicleView.Build(skChr, filter));
                    break;
                }
```

- In the `galaxy` build-summary `Console.WriteLine`, extend with wars: after `{chokepoints} chokepoints` append `+ $", {skeleton.Wars.Count} wars ({skeleton.Wars.Count(w => !w.Ended)} live)"` (add `using System.Linq;` is already present).
- In `CellChar`'s `dev` layer (`GalaxyMapView.cs`), change `System.Math.Min(5, c.DevelopmentTier)` → `System.Math.Min(9, c.DevelopmentTier)` (tech-raised ceilings render).
- In the `cell` case output, after the owner line add:

```csharp
                    Console.WriteLine($"  population {cell.Population:F1}"
                        + (cell.PopulationSpeciesId >= 0 ? $" ({sk.Species[cell.PopulationSpeciesId].Name})" : "")
                        + $" · throughput {cell.RouteThroughput:F1}"
                        + $" · value {StarGen.Core.Galaxy.Economy.SystemValue(cell.OwnerPolityId >= 0 ? sk.Species[sk.Polities[cell.OwnerPolityId].SpeciesId] : StarGen.Core.Galaxy.Economy.DisplayBaseline, cell):F1}");
```

(`using StarGen.Core.Galaxy;` is already present — drop the namespace qualifiers accordingly.)
- The `cell` case's per-cell event loop prints raw `e.Type`; switch it to `ChronicleView.Describe(sk, e)`:

```csharp
                    foreach (var e in sk.Events)
                        if (e.Q == qcx && e.R == qcy)
                            Console.WriteLine("  " + ChronicleView.Describe(sk, e));
```

- [ ] **Step 5: Build + gate + smoke**

Run: `dotnet build StarSystemGeneration.sln 2>&1 | tail -3` — expect no errors.
Run: `dotnet test StarSystemGeneration.sln 2>&1 | tail -5` — expect PASS, 157.
Smoke (bash, NOT PowerShell — first piped stdin line gets BOM-mangled in PowerShell):

```bash
printf 'galaxy 42 8\nmap trade\nmap economy\nmap war\npolity 0\nchronicle 0\nstats 50\nquit\n' | dotnet run --project src/Inspector | tail -60
```

Expected: all three maps render with legends, polity 0 prints econ lines, chronicle prints readable history, stats ends with the economy block, no exceptions.

- [ ] **Step 6: Commit**

```bash
git add src/Inspector/GalaxyMapView.cs src/Inspector/Repl.cs src/Inspector/EconomyReport.cs src/Inspector/ChronicleView.cs
git commit -m "feat(econ): REPL surface - trade/economy/war layers, polity + chronicle commands, economy stats"
```

---

### Task 12: Live REPL acceptance + docs (controller-led)

**This task is controller-led (no subagent):** the acceptance is an eyeball pass per the economy spec §9, followed by doc updates. No Unity editor required.

**Files:**
- Modify: `docs/DESIGN.md` (regional paragraph slice note)
- Modify: `docs/HANDOFF.md` (if the session updates it)

- [ ] **Step 1: Acceptance script** (bash):

```bash
printf 'galaxy 42 21\nmap trade\nmap economy\nmap war\nmap polity\nstats 200\nchronicle\nquit\n' | dotnet run --project src/Inspector > .superpowers/econ-acceptance.txt
```

Eyeball criteria (spec §9): throughput concentrates on corridors/chokepoints rather than uniform smear; economy layer shows metallicity/anchor structure; war layer shows localized fronts/scars, not full-map noise; chronicle reads as coherent history (declared → taken → ended arcs, tech advances, occasional famines); polity panels of two warring polities show drained stockpiles. Record observations; tune dials (task-10 list) with individual commits if anything is off.

- [ ] **Step 2: `polity` / `cell` spot-checks** on interesting ids found in step 1; confirm numbers tell consistent stories.

- [ ] **Step 3: DESIGN.md** — in the regional-generation paragraph of §4, extend the slice sentence to note: slice 2 (sim economy, 2026-07-08 spec) covers sim stages 2–3 — budgets/stockpiles, commodities/flows/blockades, tech tiers, population, persistent wars.

- [ ] **Step 4: Full gate one last time**, then commit docs:

```bash
dotnet test StarSystemGeneration.sln
git add docs/DESIGN.md
git commit -m "docs: mark regional slice 2 (sim economy) complete in DESIGN.md"
```

Then: final whole-branch review (fable) + one fix wave → finishing-a-development-branch → merge to main, verify tests, delete branch. User pushes manually.

---

## Self-Review Notes (already applied)

- **Spec coverage:** §3 pipeline → tasks 4–8; §4 model/schema → tasks 1, 9; §5 economy → tasks 2–5; §6 war → tasks 6–7; §7 knobs → task 1; §8 REPL → task 11; §9 testing → tasks 3, 10; §9 live acceptance → task 12. `TradeBlocked` fires only for at-war polities above a loss floor (spec's magnitude-floor wording, blockade-attributable heuristic — noted in `IncomePhase`).
- **Type consistency:** `Economy.Route` returns `List<RegionCell>?`; phases consume `Dictionary<int,double>` budgets; `War` list fields are `List<HexCoordinate>`; event payload is `Detail` (int) everywhere.
- **Known simplifications (deliberate, spec-compatible):** fronts do not auto-grow beyond goal cells except via termination transfers (spec allows "goal cells plus cells taken"); cross-polity trade wealth is symmetric; `TradeBlocked` attribution heuristic as above. All noted for the final review.
