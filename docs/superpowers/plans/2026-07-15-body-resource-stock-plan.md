# Body Resource Stock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the inert `RichnessModifier` decoration with real body-native extraction — Mine/Excavation dig down a finite, depletable per-body resource stock; Skimmer/Agri draw a renewable yield from the claimed body's own attributes; and groundbreaking rejects any extraction facility that resolves no eligible body.

**Architecture:** A new epoch-tier `SimState.BodyResources` registry (`Dictionary<(HexCoordinate, BodyRef), Stock>`) holds each depletable body's finite `(good, quantity, grade)`, rolled once at groundbreaking by `BodyResourceOps.Commit` (a stateless hash keyed by hex + body, mirroring `SystemRegistry.Commit`'s memoize-once idiom) and decremented by `BodyResourceOps.Extract`. `BodySiting` stops riding the port body for extraction types (so a no-substrate hex resolves `BodyRef.None`) and gains renewable yield/grade helpers replacing the retired `RichnessModifier`. `ProjectOps.SpawnFacilityConstruction` (and the colony-founding path in `CompleteExpedition`) route body assignment + stock roll through one shared `PlaceFacilityBody` helper; extraction groundbreaks that resolve `None` abort with no `Facility`/`Project`. `MarketEngine.SupplyLands` reads/depletes the stock for Mine/Excavation and reads the renewable body yield for Skimmer/Agri, retiring `ExtractionPotential`/`RichnessModifier` from the extraction path. The stock registry is genuinely serialized (a new `bodyresources` layer — real mutable state, not re-derived hex tier), and a `Extraction.BodyStockRemaining` metric tracks depletion.

**Tech Stack:** C# (`src/Core`, netstandard2.1 — C# 9 language level, so `record`/`readonly record struct` only, no C# 10+ features), xUnit (`tests/Core.Tests`), the line-based versioned `ArtifactSerializer`. Build/test: `dotnet test StarSystemGeneration.sln`.

## Global Constraints

- **Determinism (CLAUDE.md):** the stock roll is a stateless hash — `RollContext(state.Config.MasterSeed, hex).NextDouble(RollChannel.BodyResourceStock, star*100+slot)` — keyed `(hex, StarIndex, SlotIndex, channel)`, no shared mutable RNG state, independent of trigger order. Fixed iteration order everywhere: facilities by id, goods by catalog order, `BodyResources` iterated **sorted by (q, r, star, slot)** for any serialized/diagnostic output (P6). The hex tier is still never persisted (`SettledSystems` stores only the set); `BodyResources` is different — it is real mutable state and **is** serialized in full.
- **Conservation (P4):** this work mints or sinks **no credits**. Depletion is a real *goods* resource leaving a finite stock — not a credit mint, not a sink from nowhere; value still flows through the existing wage/sale paths in `SupplyLands`, and extraction is capped by remaining quantity so a facility can never post more than the body actually has. Goods are not a conserved quantity (they are produced and consumed); less production as a body depletes is the intended economic consequence. `ConservationTests` must stay green untouched.
- **The hex-tier generator is a pure function of `(GalaxyConfig, hex)`** — `SystemRegistry.Commit` freezes it idempotently. `BodyResourceOps.Commit` mirrors that memoize-once discipline: a repeat call for the same `(hex, body)` is a no-op, so the stock is rolled exactly once regardless of trigger order.
- **Language level:** `src/Core` targets netstandard2.1 and the Unity package compiles it as C# 9. Use `readonly record struct`, `record`, switch expressions, tuple keys — do NOT use `required`, list patterns, or other C# 10+ features. `Stock` is the existing `readonly struct` in `src/Core/Substrate/Grades.cs` — reuse it, add no new value type.
- **Serializer discipline:** layers append fields, never reorder; a new layer appends to the `Layers` table at the end. Writer and reader change in lockstep; the reader dispatches on the record tag (`f[0]`) and parses positional fields split on `|`.
- **Knob discipline:** every calibration dial exists in `KnobRegistry` (name-sorted within its `Economy.*` family) and is documented in `docs/TUNING.md`; `KnobRegistryTests` enforces order/uniqueness/round-trip. Structural constants (the `star*100+slot` index convention, the `[0.5,1.0]`/`[0,1]` yield bands) are NOT knobs.
- **Metric discipline:** every macro metric exists in `MetricRegistry` (name-sorted) and is documented in `docs/SIMHEALTH.md`; `MetricRegistryTests` enforces it. `MetricRow` carries levels/counts only, a pure function of state.
- **The design is the spec.** Two design-doc amendments are in scope and bundled into the task that produces the behavior they describe: `docs/design/substrate/commodities.md` (Stock homes, Task 2) and `docs/design/substrate/infrastructure.md` (the Terrain line, Task 5). No other design questions are reopened; `Siting.Score`, relocation/decommission, and adjacent-hex spillover stay out of scope (design §Scope).
- **TDD, frequent commits:** every task is failing-test → verify-fail → minimal impl → verify-pass → commit. Commit messages use conventional scopes (`feat(epoch):`, `feat(health):`), no Co-Authored-By trailer (the orchestrator adds it on merge).

**Context — Planner retry lifecycle (confirmed, no task changes it):** `Phases.GroundbreakFacility` guards `foreach f: if (f.Hex.Equals(entry.Hex)) return; // site taken` (`src/Core/Epoch/Phases.cs:1064-1065`) and its two call sites (`Phases.cs:1083`, `Interior/CorporationOps.cs:722`) ignore the return value and spend no points at groundbreak (wages/basket are streamed later by `Feed`). So a rejected extraction groundbreak creates nothing, leaves the plan entry live (site not taken), and is retried the same hex next cycle. That churn is harmless — `SystemRegistry.Commit` is idempotent and `BodyResourceOps.Commit` on a `None` body is a no-op, so no stray state accumulates — and it matches the design's "a failed attempt, not a standing wound in the sim state." This is existing `Planner.cs`/`Phases.cs` behavior; no task in this plan changes it.

---

### Task 1: Roll channel + two `Economy` stock knobs

**Files:**
- Modify: `src/Core/Rng/RollChannel.cs` (append one enum value)
- Modify: `src/Core/Epoch/EpochSimConfig.cs` (`EconomyKnobs`: two new dials, ~after line 685)
- Modify: `src/Core/Epoch/KnobRegistry.cs` (two registry entries, name-sorted in the `Economy.*` block, between `Economy.BlackMarketMarkup` at ~line 235-237 and `Economy.ConditionDecayPerYear` at ~line 238)
- Modify: `docs/TUNING.md` (document the two knobs)
- Test: `tests/Core.Tests/Epoch/BodyStockConfigTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `RollChannel.BodyResourceStock = 77` — the stateless-roll channel for the per-body stock draw (values are stable per the enum's registry discipline; current max is `ShipmentInterdiction = 76`).
  - `EconomyKnobs.BodyStockOreScale` (double, default `5000.0`) — expected body stock quantity = this × the region's raster richness.
  - `EconomyKnobs.BodyStockVarianceSpread` (double, default `0.4`) — fractional ± spread of the per-body stock roll around its expected quantity.
  - Both knobs registered in `KnobRegistry` (`KnobRegistry.All`), name-sorted.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyStockConfigTests.cs`:

```csharp
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyStockConfigTests
{
    [Fact]
    public void BothKnobs_AreRegisteredAndRoundTrip()
    {
        var eco = new EconomyKnobs
        {
            BodyStockOreScale = 5000.0,
            BodyStockVarianceSpread = 0.4,
        };
        Assert.Equal(5000.0, eco.BodyStockOreScale, 6);
        Assert.Equal(0.4, eco.BodyStockVarianceSpread, 6);

        bool ore = false, spread = false;
        foreach (var k in KnobRegistry.All)
        {
            if (k.Name == "Economy.BodyStockOreScale") ore = true;
            if (k.Name == "Economy.BodyStockVarianceSpread") spread = true;
        }
        Assert.True(ore, "Economy.BodyStockOreScale must be registered");
        Assert.True(spread, "Economy.BodyStockVarianceSpread must be registered");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyStockConfigTests`
Expected: FAIL — build error, `EconomyKnobs` has no `BodyStockOreScale` / `BodyStockVarianceSpread`.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Rng/RollChannel.cs`, append after `ShipmentInterdiction = 76,` (the last value, ~line 115):

```csharp

    // --- Body resource stocks (locality — body-resource-stock design). ---
    BodyResourceStock = 77,    // per-body depletable stock roll: RollContext keyed by hex, index = starIndex*100 + slotIndex, subIndex unused
```

In `src/Core/Epoch/EpochSimConfig.cs`, in `EconomyKnobs`, add after `StockCapPerDepotTier { get; set; } = 400.0;` (~line 685):

```csharp
    /// <summary>Expected finite resource stock (units) a new Mine/Excavation
    /// body is rolled with (body-resource-stock design): expected quantity =
    /// this × the region's raster richness score. Bigger → deeper deposits
    /// that outlast more epochs of extraction before a body runs dry.</summary>
    public double BodyStockOreScale { get; set; } = 5000.0;
    /// <summary>Fractional ± spread of a body's stock roll around its expected
    /// quantity (body-resource-stock design): 0 = every body at the same
    /// richness is identical; 0.4 = ±40% variance, so two belts in one rich
    /// hex differ.</summary>
    public double BodyStockVarianceSpread { get; set; } = 0.4;
```

In `src/Core/Epoch/KnobRegistry.cs`, insert both entries between the `Economy.BlackMarketMarkup` entry (ends ~line 237) and the `Economy.ConditionDecayPerYear` entry (~line 238) — `BodyStock…` sorts after `BlackMarketMarkup` and before `ConditionDecayPerYear`, `OreScale` before `VarianceSpread`:

```csharp
        K("Economy.BodyStockOreScale",
          "expected Mine/Excavation body stock (units) = this x raster richness",
          c => c.Economy.BodyStockOreScale,
          (c, v) => c.Economy.BodyStockOreScale = v),
        K("Economy.BodyStockVarianceSpread",
          "fractional +/- spread of a body's stock roll around its expected quantity",
          c => c.Economy.BodyStockVarianceSpread,
          (c, v) => c.Economy.BodyStockVarianceSpread = v),
```

In `docs/TUNING.md`, add the two knobs under the Economy section following the file's existing per-knob format, with their consequence-of-turning notes: `Economy.BodyStockOreScale` — scales how deep every extraction deposit is, so raising it lets a mine run many more epochs before its body depletes (and lowering it makes deposits exhaust fast, forcing relocation pressure once that lands); `Economy.BodyStockVarianceSpread` — how unequal two bodies at the same regional richness are, 0 making every deposit identical and higher values producing rich-vs-poor rock lottery within one hex.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyStockConfigTests|FullyQualifiedName~KnobRegistryTests"`
Expected: PASS — the new knobs resolve and round-trip; `KnobRegistryTests` still green (name-sorted, unique, documented, accessor round-trip).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Rng/RollChannel.cs src/Core/Epoch/EpochSimConfig.cs src/Core/Epoch/KnobRegistry.cs docs/TUNING.md tests/Core.Tests/Epoch/BodyStockConfigTests.cs
git commit -m "feat(epoch): body-stock roll channel + ore-scale/variance knobs"
```

---

### Task 2: `SimState.BodyResources` registry + `BodyResourceOps`

**Files:**
- Modify: `src/Core/Epoch/SimState.cs` (add the registry field + `using StarGen.Core.Substrate;`)
- Create: `src/Core/Epoch/BodyResourceOps.cs`
- Modify: `docs/design/substrate/commodities.md:49-50` (Stock homes gain body reserves)
- Test: `tests/Core.Tests/Epoch/BodyResourceOpsTests.cs`

**Interfaces:**
- Consumes: `RollChannel.BodyResourceStock`, `EconomyKnobs.BodyStockOreScale`/`BodyStockVarianceSpread` (Task 1); `RollContext` (`StarGen.Core.Rng`); `MarketEngine.FieldsAt`, `Potentials.Ore`/`Exotics`/`RawGrade` (`StarGen.Core.Substrate`); `Infrastructure.Get(...).Produces` (`StarGen.Core.Substrate`); `Stock` (`StarGen.Core.Substrate`); `BodyRef` (Task-existing).
- Produces:
  - `SimState.BodyResources` — `public Dictionary<(HexCoordinate Hex, BodyRef Body), Stock> BodyResources { get; }` (real mutable state; iterate sorted for output).
  - `static void BodyResourceOps.Commit(SimState state, HexCoordinate hex, BodyRef body, InfraTypeId type, StarSystem? system)` — idempotent first-touch roll. No-op unless `type` is `Mine`/`ExcavationSite` with a real body and non-null system. Expected quantity = `BodyStockOreScale × richness`, with `±BodyStockVarianceSpread` variance from the per-body hash; grade = `Potentials.RawGrade(richness)`.
  - `static double BodyResourceOps.Extract(SimState state, HexCoordinate hex, BodyRef body, double rated, out double grade)` — draws `min(rated, remaining)`, decrements (floored at 0, never negative), returns drawn quantity and the stock's grade; a dry/absent/renewable body draws `0`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyResourceOpsTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyResourceOpsTests
{
    // Commit only null-guards on the system; a bare non-null system suffices.
    private static StarSystem Sys() => new StarSystem("STK");

    [Fact]
    public void Commit_IsIdempotent_KeepsTheFirstRoll()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var body = new BodyRef(0, 0);
        BodyResourceOps.Commit(state, hex, body, InfraTypeId.Mine, Sys());
        double first = state.BodyResources[(hex, body)].Quantity;
        BodyResourceOps.Commit(state, hex, body, InfraTypeId.Mine, Sys());
        double second = state.BodyResources[(hex, body)].Quantity;
        Assert.Equal(first, second, 9);          // memoized, not re-rolled
    }

    [Fact]
    public void Commit_QuantityTracksRegionalRichness_WithinTheSpread()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 0),
                               InfraTypeId.Mine, Sys());
        double qty = state.BodyResources[(hex, new BodyRef(0, 0))].Quantity;

        var eco = state.Config.Economy;
        double richness = Potentials.Ore(MarketEngine.FieldsAt(state, hex));
        double expected = eco.BodyStockOreScale * richness;
        Assert.True(expected > 0);
        Assert.InRange(qty,
            expected * (1.0 - eco.BodyStockVarianceSpread),
            expected * (1.0 + eco.BodyStockVarianceSpread));
    }

    [Fact]
    public void Commit_TwoBodiesInOneHex_RollDifferentQuantities()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 0),
                               InfraTypeId.Mine, Sys());
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 1),
                               InfraTypeId.Mine, Sys());
        Assert.NotEqual(
            state.BodyResources[(hex, new BodyRef(0, 0))].Quantity,
            state.BodyResources[(hex, new BodyRef(0, 1))].Quantity);
    }

    [Fact]
    public void Extract_DrawsCappedByRemaining_AndFloorsAtZero()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var body = new BodyRef(0, 0);
        BodyResourceOps.Commit(state, hex, body, InfraTypeId.Mine, Sys());
        double total = state.BodyResources[(hex, body)].Quantity;
        Assert.True(total > 0);

        double drawn = BodyResourceOps.Extract(state, hex, body, total + 1000,
                                                out double grade);
        Assert.Equal(total, drawn, 6);           // capped by what's there
        Assert.True(grade > 0);
        Assert.Equal(0.0, state.BodyResources[(hex, body)].Quantity, 9);

        double again = BodyResourceOps.Extract(state, hex, body, 100, out _);
        Assert.Equal(0.0, again, 9);             // dry stays dry, never negative
    }

    [Fact]
    public void Commit_RenewableTypes_RollNoStock()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 0),
                               InfraTypeId.Skimmer, Sys());
        Assert.False(state.BodyResources.ContainsKey((hex, new BodyRef(0, 0))));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyResourceOpsTests`
Expected: FAIL — build error, `SimState.BodyResources` and `BodyResourceOps` do not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/SimState.cs`, add `using StarGen.Core.Substrate;` to the usings (for `Stock`), and add the registry field beside the other registries (e.g. after the `SettledSystems` property, ~line 57):

```csharp
    /// <summary>Depletable per-body resource stocks (body-resource-stock
    /// design), keyed (hex, body). Unlike SettledSystems this is REAL mutable
    /// state — rolled once when a Mine/ExcavationSite claims a body, then
    /// decremented as it extracts — so it is genuinely serialized, not
    /// re-derived. Iterate SORTED (q, r, star, slot) for any output (P6).</summary>
    public Dictionary<(HexCoordinate Hex, BodyRef Body), Stock> BodyResources
    { get; } = new Dictionary<(HexCoordinate, BodyRef), Stock>();
```

Create `src/Core/Epoch/BodyResourceOps.cs`:

```csharp
using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The depletable body-resource stock registry's ops
/// (body-resource-stock design). A Mine or ExcavationSite draws from a finite
/// (quantity, grade) rock rolled once at groundbreaking; extraction decrements
/// it until the body runs dry, then the facility simply produces nothing and
/// falls out of IsActive's revenue like any unprofitable asset. Skimmer/
/// AgriComplex are renewable and keep no stock here. Mirrors
/// SystemRegistry.Commit's memoize-once idiom; the roll is a stateless hash
/// keyed (hex, star, slot, channel).</summary>
public static class BodyResourceOps
{
    /// <summary>Roll a depletable body's finite stock once, the first time a
    /// Mine/ExcavationSite claims it (idempotent — a repeat call is a no-op).
    /// No-op for renewable/other types, a None body, or a null system: only
    /// Mine/ExcavationSite carry a stock. Expected quantity scales with the
    /// region's raster richness (the same Ore/Exotics score the siting used);
    /// a per-body hash gives real variance so two belts in one rich hex differ.
    /// Grade reuses Potentials.RawGrade's shape (no new grade math).</summary>
    public static void Commit(SimState state, HexCoordinate hex, BodyRef body,
                              InfraTypeId type, StarSystem? system)
    {
        if (type != InfraTypeId.Mine && type != InfraTypeId.ExcavationSite)
            return;
        if (system == null || body.IsNone) return;
        var key = (hex, body);
        if (state.BodyResources.ContainsKey(key)) return;   // memoize-once

        var eco = state.Config.Economy;
        var fields = MarketEngine.FieldsAt(state, hex);
        double richness = type == InfraTypeId.Mine
            ? Potentials.Ore(fields) : Potentials.Exotics(fields);
        double expected = eco.BodyStockOreScale * richness;
        // stateless per-body variance: RollContext keyed by hex, index encodes
        // star+slot exactly like BodyGenerator (starIndex*100 + slotIndex).
        var roll = new RollContext(state.Config.MasterSeed, hex);
        int idx = body.StarIndex * 100 + body.SlotIndex;
        double u = roll.NextDouble(RollChannel.BodyResourceStock, idx); // [0,1)
        double spread = eco.BodyStockVarianceSpread;
        double quantity = Math.Max(0.0,
            expected * (1.0 - spread + 2.0 * spread * u));
        double grade = Potentials.RawGrade(richness);
        var good = Infrastructure.Get(type).Produces[0];
        state.BodyResources[key] = new Stock(good, quantity, grade);
    }

    /// <summary>Extract up to <paramref name="rated"/> units from a body's
    /// stock, capped by what remains; decrement the registry (floored at zero,
    /// never negative) and hand back the drawn quantity and the stock's grade.
    /// A dry, absent, or renewable body draws nothing.</summary>
    public static double Extract(SimState state, HexCoordinate hex,
                                 BodyRef body, double rated, out double grade)
    {
        grade = 0.0;
        if (rated <= 0) return 0.0;
        var key = (hex, body);
        if (!state.BodyResources.TryGetValue(key, out var stock)
            || stock.Quantity <= 0) return 0.0;
        double drawn = Math.Min(rated, stock.Quantity);
        grade = stock.Grade;
        state.BodyResources[key] =
            new Stock(stock.Good, stock.Quantity - drawn, stock.Grade);
        return drawn;
    }
}
```

In `docs/design/substrate/commodities.md`, replace the Stock homes sentence (lines 49-50):

```markdown
Every stock of a good is `(quantity, grade)`, grade ∈ [0,1] — one scalar carried
wherever stocks live (markets, stockpiles, cargo holds, and depletable body
reserves — a Mine/Excavation body holds a finite `(good, quantity, grade)` stock
it is dug out of over time until the rock runs dry).
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyResourceOpsTests|FullyQualifiedName~SettledSystemsTests"`
Expected: PASS — ops green; `SettledSystemsTests` (which also touch `SimState`) still green.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/SimState.cs src/Core/Epoch/BodyResourceOps.cs docs/design/substrate/commodities.md tests/Core.Tests/Epoch/BodyResourceOpsTests.cs
git commit -m "feat(epoch): BodyResources registry + depletable stock ops"
```

---

### Task 3: `BodySiting` — extraction is body-native (Assign rejects, renewable yield replaces `RichnessModifier`)

**Files:**
- Modify: `src/Core/Epoch/BodySiting.cs` (change `Assign`'s terminal fallback; add `IsExtraction`, `RenewableYield`, `RenewableGrade`, private `BodyAt`)
- Modify: `tests/Core.Tests/Epoch/BodySitingTests.cs` (replace the port-fallback test with two None tests)
- Test: `tests/Core.Tests/Epoch/BodyYieldTests.cs` (new — renewable yield/grade)

> **Ordering note:** `RichnessModifier` is left **in place** here (its last caller, `MarketEngine.SupplyLands`, is only rewritten in Task 5) so the build stays green between tasks. Task 5 removes `RichnessModifier` and deletes `BodyExtractionTests.cs` in the same commit that removes the call. Task 3's `Assign` change touches only groundbreaking (Task 4), never `SupplyLands`, so it is safe to land alone.

**Interfaces:**
- Consumes: `BodyRef`, `StarSystem`/`Star`/`OrbitSlot`/`Body`/`BodyKind`/`Biosphere`/`OrbitBand` (`StarGen.Core.Model`); `InfraTypeId`, `Potentials.RawGrade` (`StarGen.Core.Substrate`).
- Produces:
  - `static BodyRef BodySiting.Assign(StarSystem?, InfraTypeId, BodyRef portBody, IEnumerable<BodyRef> claimed)` — unchanged for the substrate preference and claim-skipping, but now: an **extraction** type with no eligible body returns `BodyRef.None` (it does NOT ride the port body); only non-extraction (support/processing) rides the port.
  - `static bool BodySiting.IsExtraction(InfraTypeId type)` — the four extraction types (Mine/Skimmer/AgriComplex/ExcavationSite).
  - `static double BodySiting.RenewableYield(StarSystem?, BodyRef, InfraTypeId)` — Skimmer/Agri renewable terrain in `[0,1]` from the body's real attributes; `0` for a missing/None body or null system; `0` for non-renewable types.
  - `static double BodySiting.RenewableGrade(StarSystem?, BodyRef, InfraTypeId)` — `Potentials.RawGrade(RenewableYield(...))`.
  - `RichnessModifier` stays for now (still called by `SupplyLands`); it is retired entirely in Task 5 when its last caller goes (design §Provided interface).

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyYieldTests.cs`:

```csharp
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyYieldTests
{
    private static StarSystem Giant(int size)
    {
        var sys = new StarSystem("T");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.GasGiant, Size = size } });
        sys.Stars.Add(s0);
        return sys;
    }

    private static StarSystem World(Biosphere bio, int hydro)
    {
        var sys = new StarSystem("T");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 5,
                Biosphere = bio, Hydrographics = hydro } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void Skimmer_FatterGiant_YieldsMore_AcrossThePositiveBand()
    {
        double lean = BodySiting.RenewableYield(Giant(10), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        double fat = BodySiting.RenewableYield(Giant(14), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        Assert.Equal(0.5, lean, 9);              // any giant has mass: never 0
        Assert.Equal(1.0, fat, 9);
        Assert.True(fat > lean);
    }

    [Fact]
    public void Agri_LivingWateredWorld_OutyieldsBarrenDryRock()
    {
        double lush = BodySiting.RenewableYield(World(Biosphere.Flourishing, 70),
            new BodyRef(0, 0), InfraTypeId.AgriComplex);
        double barren = BodySiting.RenewableYield(World(Biosphere.Barren, 0),
            new BodyRef(0, 0), InfraTypeId.AgriComplex);
        Assert.True(lush > barren);
        Assert.True(barren > 0.0);               // subsistence floor: farmable
        Assert.InRange(lush, 0.0, 1.0);
    }

    [Fact]
    public void RenewableGrade_RicherBody_BetterGrade()
    {
        double fatGrade = BodySiting.RenewableGrade(Giant(14), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        double leanGrade = BodySiting.RenewableGrade(Giant(10), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        Assert.True(fatGrade > leanGrade);
        Assert.InRange(fatGrade, 0.15, 0.85);    // Potentials.RawGrade shape
    }

    [Fact]
    public void MissingBody_YieldsZero()
    {
        Assert.Equal(0.0, BodySiting.RenewableYield(null, new BodyRef(0, 0),
            InfraTypeId.Skimmer), 9);
        Assert.Equal(0.0, BodySiting.RenewableYield(Giant(12), BodyRef.None,
            InfraTypeId.Skimmer), 9);
    }
}
```

Also replace the `SecondMine_FallsToNone_WhenSubstrateAbsentAndPortAlreadyClaimed` test in `tests/Core.Tests/Epoch/BodySitingTests.cs` (lines 49-61) with these two:

```csharp
    [Fact]
    public void Mine_WithNoBeltOrRock_IsNone_NotThePortBody()
    {
        var sys = WithNoExtractionSubstrate();   // a gas giant only
        var port = BodySiting.PortBody(sys);
        Assert.False(port.IsNone);               // a port body exists...
        var first = BodySiting.Assign(sys, InfraTypeId.Mine, port,
            new List<BodyRef>());
        Assert.True(first.IsNone);               // ...but a mine won't ride it
    }

    [Fact]
    public void Skimmer_WithNoGasGiant_IsNone()
    {
        var sys = WithBelts();                   // belts + a rocky world, no giant
        var port = BodySiting.PortBody(sys);
        Assert.True(BodySiting.Assign(sys, InfraTypeId.Skimmer, port,
            new List<BodyRef>()).IsNone);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyYieldTests|FullyQualifiedName~BodySitingTests"`
Expected: FAIL — build error, `BodySiting.RenewableYield`/`RenewableGrade` do not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/BodySiting.cs`, change the terminal fallback in `Assign` and add `IsExtraction`. Replace the tail of `Assign` (currently `if (preferred.HasValue) return preferred.Value; return taken.Contains(portBody) ? BodyRef.None : portBody;`) with:

```csharp
        if (preferred.HasValue) return preferred.Value;
        // extraction is body-native now (body-resource-stock design): a Mine/
        // Skimmer/Agri/Excavation with no substrate-appropriate body has
        // nothing to draw from, so it resolves None (and groundbreaking rejects
        // it) rather than riding the port body. Only non-extraction support/
        // processing assets ride the port.
        if (IsExtraction(type)) return BodyRef.None;
        return taken.Contains(portBody) ? BodyRef.None : portBody;
    }

    /// <summary>The four extraction types, whose output roots in a specific
    /// body — a depletable stock for Mine/Excavation, a renewable yield for
    /// Skimmer/Agri — never in the port body.</summary>
    public static bool IsExtraction(InfraTypeId type) =>
        type == InfraTypeId.Mine || type == InfraTypeId.Skimmer
        || type == InfraTypeId.AgriComplex
        || type == InfraTypeId.ExcavationSite;
```

Add the renewable-yield helpers beside `RichnessModifier` (leave `RichnessModifier` in place — Task 5 removes it with its last caller). Insert:

```csharp
    /// <summary>Renewable extraction yield in [0,1] from the claimed body's own
    /// real attributes (body-resource-stock design) — a gas giant's mass for a
    /// Skimmer, a world's biosphere and water for an AgriComplex. No depletion:
    /// the giant and the living soil replenish at any facility's scale. 0 for a
    /// missing/None body, a null system, or a non-renewable type. Pure,
    /// deterministic, no rolls.</summary>
    public static double RenewableYield(StarSystem? system, BodyRef body,
                                        InfraTypeId type)
    {
        var b = BodyAt(system, body);
        if (b == null) return 0.0;
        if (type == InfraTypeId.Skimmer)
        {
            // GasGiantSize table spans 10-14; a fat giant out-yields a lean one
            // across a [0.5, 1.0] band (never zero — any giant has mass).
            double norm = System.Math.Max(0.0,
                System.Math.Min(1.0, (b.Size - 10.0) / 4.0));
            return 0.5 + 0.5 * norm;
        }
        if (type == InfraTypeId.AgriComplex)
        {
            // living, watered worlds farm best; a barren dry rock still
            // subsistence-farms a little. Biosphere 0-3, Hydrographics 0-100.
            double bio = System.Math.Max(0.0,
                System.Math.Min(1.0, (int)b.Biosphere / 3.0));
            double water = System.Math.Max(0.0,
                System.Math.Min(1.0, b.Hydrographics / 100.0));
            return System.Math.Max(0.0,
                System.Math.Min(1.0, 0.3 + 0.5 * bio + 0.2 * water));
        }
        return 0.0;
    }

    /// <summary>Grade of renewable extraction — richer body, better grade,
    /// through the existing Potentials.RawGrade shape (no new grade math).</summary>
    public static double RenewableGrade(StarSystem? system, BodyRef body,
                                        InfraTypeId type) =>
        Potentials.RawGrade(RenewableYield(system, body, type));

    private static Body? BodyAt(StarSystem? system, BodyRef body)
    {
        if (system == null || body.IsNone) return null;
        if (body.StarIndex < 0 || body.StarIndex >= system.Stars.Count)
            return null;
        foreach (var slot in system.Stars[body.StarIndex].Slots)
            if (slot.Index == body.SlotIndex) return slot.Body;
        return null;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyYieldTests|FullyQualifiedName~BodySitingTests"`
Expected: PASS — renewable yield/grade green; `BodySitingTests` green with the two new None tests; `BodyExtractionTests` still green (`RichnessModifier` intact until Task 5).

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/BodySiting.cs tests/Core.Tests/Epoch/BodySitingTests.cs tests/Core.Tests/Epoch/BodyYieldTests.cs
git commit -m "feat(epoch): body-native extraction — Assign rejects, renewable yield replaces RichnessModifier"
```

---

### Task 4: Groundbreak rolls the stock and rejects bodiless extraction

**Files:**
- Modify: `src/Core/Epoch/ProjectOps.cs` (`SpawnFacilityConstruction` → `Project?` with abort + shared `PlaceFacilityBody`; `CompleteExpedition` founding facilities route through `PlaceFacilityBody`)
- Modify: `src/Core/Epoch/Phases.cs` (entry `StarterIndustry` loop, ~lines 1437-1439, routes through `PlaceFacilityBody`)
- Test: `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (extend)

**Interfaces:**
- Consumes: `SystemRegistry.Commit`, `BodySiting.PortBody`/`Assign`/`IsExtraction` (Task 3), `BodyResourceOps.Commit` (Task 2), `Facility.Body`, `Project.Body`.
- Produces:
  - `static BodyRef ProjectOps.PlaceFacilityBody(SimState state, HexCoordinate hex, InfraTypeId type)` — **public** (the test fixtures and other creators call it): commits the hex's system, decides the body claim-aware, rolls the depletable stock (a no-op for non-Mine/Excavation or a None body), returns the assigned body.
  - `SpawnFacilityConstruction` now returns `Project?` — `null` (no `Facility`, no `Project` created) when an extraction type resolves no eligible body; unchanged otherwise (its two production call sites in `Phases.cs`/`CorporationOps.cs` ignore the return, so they still compile).
  - `CompleteExpedition`'s founding facilities carry a real `Body` and (for Mine/Excavation) a rolled stock — closing the design's "every Mine has a stock" invariant on the colony-founding path, which does NOT go through `SpawnFacilityConstruction`.
  - The entry `StarterIndustry` loop (`Phases.cs`, every new polity's starting Mine + AgriComplex) also routes through `PlaceFacilityBody` — the same invariant, on the one remaining facility-creation path that bypassed groundbreaking entirely. Unlike `SpawnFacilityConstruction`, this loop does NOT reject/skip a facility whose body resolves `None` — a polity's founding industry is mandatory civilization furniture, not a site selection the sim can decline; if a homeworld hex genuinely has no eligible body (expected to be vanishingly rare, since a homeworld's seat carries the founding population and is generated with a real inhabited world), that one starter facility simply produces nothing, exactly like a depleted or bodiless facility does anywhere else — no special-casing needed beyond calling the same helper.

- [ ] **Step 1: Write the failing test**

Append to `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (inside the class — it reuses the file's `RunHistory`, `FirstEnteredPolity`, `OwnPort` helpers):

```csharp
    [Theory]
    [InlineData((int)StarGen.Core.Substrate.InfraTypeId.Mine)]
    [InlineData((int)StarGen.Core.Substrate.InfraTypeId.Skimmer)]
    [InlineData((int)StarGen.Core.Substrate.InfraTypeId.AgriComplex)]
    [InlineData((int)StarGen.Core.Substrate.InfraTypeId.ExcavationSite)]
    public void Groundbreak_NoEligibleBody_RejectsOutright(int typeId)
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        // a committed system with NO bodies at all: every extraction type
        // resolves None (nothing to localize to).
        var hex = new StarGen.Core.Model.HexCoordinate(444, 444);
        var bodiless = new StarGen.Core.Model.StarSystem("T");
        bodiless.Stars.Add(new StarGen.Core.Model.Star());
        state.SettledSystems[hex] = bodiless;
        int facBefore = state.Facilities.Count;
        int projBefore = state.Projects.Count;
        var candidate = new ConstructionCandidate(typeId, hex, portId, 1.0);

        var p = ProjectOps.SpawnFacilityConstruction(state, pr.ActorId,
            pr.ActorId, candidate, ProjectPriority.Core, 0);

        Assert.Null(p);                                   // no project
        Assert.Equal(facBefore, state.Facilities.Count);  // no facility
        Assert.Equal(projBefore, state.Projects.Count);
    }

    [Fact]
    public void Groundbreak_WithEligibleBody_BuildsAndRollsAStock()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        // a committed system holding a rocky world: a Mine claims it.
        var hex = new StarGen.Core.Model.HexCoordinate(445, 445);
        var sys = new StarGen.Core.Model.StarSystem("T");
        var s0 = new StarGen.Core.Model.Star();
        s0.Slots.Add(new StarGen.Core.Model.OrbitSlot
        {
            Index = 0, Band = StarGen.Core.Model.OrbitBand.Habitable,
            Body = new StarGen.Core.Model.Body
            { Kind = StarGen.Core.Model.BodyKind.RockyWorld, Size = 6 }
        });
        sys.Stars.Add(s0);
        state.SettledSystems[hex] = sys;
        var candidate = new ConstructionCandidate(
            (int)StarGen.Core.Substrate.InfraTypeId.Mine, hex, portId, 1.0);

        var p = ProjectOps.SpawnFacilityConstruction(state, pr.ActorId,
            pr.ActorId, candidate, ProjectPriority.Core, 0);

        Assert.NotNull(p);
        var f = state.Facilities[p!.TargetId];
        Assert.False(f.Body.IsNone);                      // claimed the rock
        Assert.True(state.BodyResources.ContainsKey((hex, f.Body)),
            "a depletable Mine rolls its finite stock at groundbreaking");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~ProjectOpsTests`
Expected: FAIL — `SpawnFacilityConstruction` currently always creates a facility (never returns null) and does not roll a stock, so both new tests fail (the abort assertion and the `BodyResources.ContainsKey`).

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/ProjectOps.cs`, replace the whole `SpawnFacilityConstruction` method (lines 38-75) with the returning-`Project?` version plus the shared `PlaceFacilityBody` helper:

```csharp
    public static Project? SpawnFacilityConstruction(SimState state,
        int ownerActorId, int funderActorId, ConstructionCandidate c,
        ProjectPriority priority, int planOrder, int startedYear = int.MinValue)
    {
        var type = (Substrate.InfraTypeId)c.TypeId;
        var def = Substrate.Infrastructure.Get(type);
        // groundbreaking is the §1 commit trigger: freeze the hex's system,
        // decide this facility's body once (claim-aware — the two-mines fix),
        // and roll a depletable stock if it's a Mine/ExcavationSite.
        var body = PlaceFacilityBody(state, c.Hex, type);
        // no eligible body for an extraction type: reject the groundbreak
        // outright — no Facility, no Project (body-resource-stock design). A
        // None body rolled no stock, so nothing leaks. Support/processing
        // assets ride the port body (possibly None) and are never rejected.
        if (body.IsNone && BodySiting.IsExtraction(type)) return null;
        var facility = new Facility(state.Facilities.Count, c.TypeId,
            tier: 1, c.Hex, ownerActorId, state.WorldYear)
        { CommissionedYear = -1, Body = body };
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
    }

    /// <summary>Decide a new facility's body at its hex (claim-aware, skipping
    /// bodies other facilities already hold — the two-mines fix) and, for a
    /// depletable Mine/ExcavationSite, roll its finite resource stock once
    /// (idempotent). Commits the hex's system as a side effect. Returns the
    /// assigned body — None when no substrate-appropriate body exists (an
    /// extraction caller treats that as a rejected groundbreak; a support
    /// caller rides None). Shared by groundbreaking, colony founding, and the
    /// test fixtures so every extraction body gets its stock.</summary>
    public static BodyRef PlaceFacilityBody(SimState state, HexCoordinate hex,
                                            Substrate.InfraTypeId type)
    {
        var system = SystemRegistry.Commit(state, hex);
        var portBody = BodySiting.PortBody(system);
        var claimed = new List<BodyRef>();
        foreach (var other in state.Facilities)           // id order (P6)
            if (other.Hex.Equals(hex) && !other.Body.IsNone)
                claimed.Add(other.Body);
        var body = BodySiting.Assign(system, type, portBody, claimed);
        BodyResourceOps.Commit(state, hex, body, type, system);
        return body;
    }
```

(`HexCoordinate` is already imported in `ProjectOps.cs` via `using StarGen.Core.Model;`.)

Still in `ProjectOps.cs`, in `CompleteExpedition`, replace the founding-facility block (currently lines 574-581, `var founding = FoundingIndustry(...)` through the two `state.Facilities.Add(new Facility(...))` calls) with:

```csharp
        // the expedition ships the equipment for what it came for: the founding
        // facility matches the site's best extraction potential, plus a
        // subsistence farm when that isn't farming. Each founding asset decides
        // its body and rolls its stock at birth, exactly like a groundbroken
        // one (body-resource-stock design — a founding Mine is a real depletable
        // rock, not a bodiless dud). The Mine is added before the farm's body is
        // placed so the farm's claim scan skips the Mine's body.
        var founding = FoundingIndustry(state, p.Hex);
        var foundingBody = PlaceFacilityBody(state, p.Hex, founding);
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)founding, tier: 1, p.Hex, p.OwnerActorId, completionYear)
        { Body = foundingBody });
        if (founding != Substrate.InfraTypeId.AgriComplex)
        {
            var farmBody = PlaceFacilityBody(state, p.Hex,
                                             Substrate.InfraTypeId.AgriComplex);
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)Substrate.InfraTypeId.AgriComplex, tier: 1, p.Hex,
                p.OwnerActorId, completionYear) { Body = farmBody });
        }
```

In `src/Core/Epoch/Phases.cs`, replace the entry `StarterIndustry` loop (currently lines 1437-1439, `foreach (var (type, tier) in StarterIndustry) state.Facilities.Add(new Facility(...));`) so each starter facility gets a real body (and, for the Mine, a rolled stock) exactly like a groundbroken one:

```csharp
            foreach (var (type, tier) in StarterIndustry)
            {
                // founding industry is mandatory civilization furniture, not a
                // site the sim can reject — it always gets built, but now
                // carries a real body (and, for the Mine, a rolled depletable
                // stock) instead of riding None forever (body-resource-stock
                // design). A homeworld seat is generated with a real inhabited
                // world, so this resolving None is expected to be vanishingly
                // rare — and if it ever does, the facility just produces
                // nothing, same as any other bodiless/depleted asset.
                var body = PlaceFacilityBody(state, a.Seat, type);
                state.Facilities.Add(new Facility(state.Facilities.Count,
                    (int)type, tier, a.Seat, a.Id, state.WorldYear)
                { Body = body });
            }
```

(`PlaceFacilityBody` is `public` on `ProjectOps`; `Phases.cs` already calls other `ProjectOps`/cross-class statics in this method, so no new `using` is needed beyond confirming the existing one resolves — check the file's current usings before assuming.)

- [ ] **Step 3b: Extend the regression test for starter industry**

Append to `tests/Core.Tests/Epoch/ProjectOpsTests.cs` (or `PhasesTests.cs`/wherever entry/`RunHistory` is actually tested — check which test file already exercises polity entry before picking one, to avoid duplicating fixture setup):

```csharp
    [Fact]
    public void EntryStarterIndustry_CarriesARealBody_AndTheMineRollsAStock()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);   // entry already ran as part of seeding/history
        var pr = state.Polities[FirstEnteredPolity(state)];
        var mine = state.Facilities.Find(f =>
            f.OwnerActorId == pr.ActorId
            && f.TypeId == (int)StarGen.Core.Substrate.InfraTypeId.Mine
            && f.Hex.Equals(state.Actors[pr.ActorId].Seat));
        Assert.NotNull(mine);
        // homeworld seats are generated with a real inhabited world, so the
        // starter Mine is expected to resolve a real body, not None
        Assert.False(mine!.Body.IsNone);
        Assert.True(state.BodyResources.ContainsKey((mine.Hex, mine.Body)));
    }
```

Adapt the lookup to however `RunHistory`/`FirstEnteredPolity` actually expose the entry-created facilities and the actor's homeworld seat in the real test file — the point is: confirm the starter Mine has a real body and a rolled stock, not that this exact lookup expression compiles verbatim.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~ProjectOpsTests|FullyQualifiedName~AllocationEconomyTests"`
Expected: PASS — the abort theory and the build-and-roll test green; the starter-industry test confirms the entry Mine carries a real body and a rolled stock; existing `ProjectOpsTests` (mines at the homeworld seat, which carries a rocky world) still build non-null; `AllocationEconomyTests` still green (it asserts candidate-type variety, not the spawn return). If any count-sensitive existing test surfaces (a homeworld-region hex that genuinely lacks any extraction body), adjust that test's site to a body-bearing hex — the abort behavior is the design's intent, not a regression.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/ProjectOps.cs src/Core/Epoch/Phases.cs tests/Core.Tests/Epoch/ProjectOpsTests.cs
git commit -m "feat(epoch): groundbreak rolls body stock and rejects bodiless extraction"
```

---

### Task 5: `SupplyLands` depletes / yields from the claimed body

**Files:**
- Modify: `src/Core/Epoch/MarketEngine.cs` (`SupplyLands` per-good loop, ~lines 166-187; delete private `ExtractionPotential`, ~lines 1140-1148)
- Modify: `src/Core/Epoch/BodySiting.cs` (delete `RichnessModifier` now its last caller is gone)
- Delete: `tests/Core.Tests/Epoch/BodyExtractionTests.cs` (superseded — it tested `RichnessModifier`)
- Modify: `docs/design/substrate/infrastructure.md:55-61` (Terrain line + formula annotation)
- Modify: `tests/Core.Tests/Epoch/MarketSupplyTests.cs` (`Built` helper attaches a body + stock; add two extraction tests)

**Interfaces:**
- Consumes: `BodyResourceOps.Extract` (Task 2); `BodySiting.RenewableYield`/`RenewableGrade` (Task 3); `ProjectOps.PlaceFacilityBody` (Task 4, for the test fixture); `Production.Output`, `BookOps.PostSupply`, `Potentials.RawGrade`, `Goods.Get(...).Recipes` (existing).
- Produces:
  - `SupplyLands` extraction path: Mine/ExcavationSite draw `min(rated capacity, remaining stock)` from `BodyResources` and post at the stock's grade; Skimmer/AgriComplex post `Production.Output(...)` at a body-native renewable terrain and grade; processing is unchanged (neutral terrain 1.0, recipe path). `ExtractionPotential` and the `RichnessModifier` multiply are gone from this path.

- [ ] **Step 1: Write the failing test**

First update the `Built` helper in `tests/Core.Tests/Epoch/MarketSupplyTests.cs` (lines 33-42) so a mine built through the fixture has a real body + stock (extraction now roots in a body, so a bodiless mine would produce nothing):

```csharp
    private static Facility Built(SimState state, InfraTypeId type,
                                  HexCoordinate hex, int owner,
                                  double condition = 1.0)
    {
        // backdated past every catalog construction time — active now
        var f = new Facility(state.Facilities.Count, (int)type, 1, hex, owner,
                             state.WorldYear - 10) { Condition = condition };
        // extraction now roots in a specific body: decide it and roll the
        // depletable stock the same way groundbreaking does, so a Mine built
        // through this fixture actually has a rock to dig (body-resource-stock).
        f.Body = ProjectOps.PlaceFacilityBody(state, hex, type);
        state.Facilities.Add(f);
        return f;
    }
```

Then append two extraction tests to the same class:

```csharp
    [Fact]
    public void Extraction_DrawsFromTheBodyStock_CappedByWhatRemains()
    {
        var (state, port) = Fixture();
        var mine = Built(state, InfraTypeId.Mine, port.Hex, port.OwnerActorId);
        // shrink the body's stock to a tiny remainder: the mine can post no
        // more than what the rock has left this step, then it is dry
        state.BodyResources[(mine.Hex, mine.Body)] =
            new StarGen.Core.Substrate.Stock(GoodId.Ore, 3.0, 0.6);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.Equal(3.0, BookOps.AskQty(state, 0, (int)GoodId.Ore), 6);
        Assert.Equal(0.0,
            state.BodyResources[(mine.Hex, mine.Body)].Quantity, 9);
    }

    [Fact]
    public void Skimmer_ProducesFromItsGiant_WithoutRollingAStock()
    {
        var (state, port) = Fixture();
        // pre-seed the port hex's system with a gas giant so the Skimmer has a
        // real body to draw a renewable yield from
        var sys = new StarSystem("T");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.GasGiant, Size = 13 } });
        sys.Stars.Add(s0);
        state.SettledSystems[port.Hex] = sys;
        var skimmer = Built(state, InfraTypeId.Skimmer, port.Hex,
                            port.OwnerActorId);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Volatiles) > 0);
        // renewable: no stock entry was ever created for a Skimmer
        Assert.False(state.BodyResources.ContainsKey((skimmer.Hex, skimmer.Body)));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~MarketSupplyTests`
Expected: FAIL — `SupplyLands` still computes extraction from `ExtractionPotential(fields) × RichnessModifier`, ignoring the stock; the depletion-cap and stock-emptied assertions fail.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/MarketEngine.cs` `SupplyLands`, keep the `var fields = FieldsAt(state, f.Hex);` line (still used for the labor/embodiment term) and the `state.SettledSystems.TryGetValue(f.Hex, out var fSystem);` line, keep `double share = 1.0 / def.Produces.Count;`, and replace the whole `foreach (var good in def.Produces)` body (lines 166-187) with:

```csharp
            var t = (InfraTypeId)f.TypeId;
            foreach (var good in def.Produces)            // catalog order
            {
                double utilization = Math.Min(1.0,
                    Math.Max(cfg.Economy.MinUtilization,
                        market.Price[(int)good]
                        / Market.InitialPrice(cfg.Economy, good)));

                // Mine / ExcavationSite: draw from the body's finite stock,
                // rated by the platform and capped by what the rock has left
                // (body-resource-stock design). Output posts at the stock's
                // grade; at zero the facility simply produces nothing.
                if (t == InfraTypeId.Mine || t == InfraTypeId.ExcavationSite)
                {
                    double rated = Production.Output(def, f.Tier, 1.0,
                                       laborFactor, machineryGrade)
                                   * share * years * f.Condition * utilization;
                    double drawn = BodyResourceOps.Extract(state, f.Hex, f.Body,
                                       rated, out double stockGrade);
                    if (drawn > 0)
                        BookOps.PostSupply(state, mIx, f.OwnerActorId,
                                           (int)good, drawn, stockGrade);
                    continue;
                }

                // Skimmer / AgriComplex: renewable yield from the claimed
                // body's own real attributes (no depletion). Everything else
                // (processing) reads neutral terrain and runs its recipe.
                double terrain = t == InfraTypeId.Skimmer
                                 || t == InfraTypeId.AgriComplex
                    ? BodySiting.RenewableYield(fSystem, f.Body, t)
                    : 1.0;
                double capacity = Production.Output(def, f.Tier, terrain,
                                     laborFactor, machineryGrade)
                                  * share * years * f.Condition * utilization;
                if (capacity <= 0) continue;

                var recipes = Goods.Get(good).Recipes;
                if (recipes.Count == 0)
                {
                    double grade = t == InfraTypeId.Skimmer
                                   || t == InfraTypeId.AgriComplex
                        ? BodySiting.RenewableGrade(fSystem, f.Body, t)
                        : Potentials.RawGrade(terrain);
                    BookOps.PostSupply(state, mIx, f.OwnerActorId, (int)good,
                                       capacity, grade);
                }
                else
                    RunRecipe(state, mIx, f, recipes, capacity);
            }
```

Delete the now-unused private `ExtractionPotential` method (currently lines 1140-1148 — the `private static double ExtractionPotential(InfraTypeId type, GoodId good, CellFields fields) => type switch { … };` block). Leave `FieldsAt` in place (still used for the labor term and by `PostBandBids`/`FoundingIndustry`).

`SupplyLands` was `RichnessModifier`'s last caller. Now delete the entire `RichnessModifier` method (doc comment + method) from `src/Core/Epoch/BodySiting.cs`, and delete its test file — both are superseded by the renewable-yield helpers:

```bash
git rm tests/Core.Tests/Epoch/BodyExtractionTests.cs
```

(A repo-wide search for `RichnessModifier` should now return nothing outside the design spec/ledger docs.)

In `docs/design/substrate/infrastructure.md`, update the output formula's terrain annotation (line 55) and the Terrain bullet (lines 60-61):

```markdown
output = base(type, tier) × terrain(claimed body: depletable stock or renewable yield)
       × labor(domain population × embodiment affinity)
       × machineryGrade × automation(compute)
```

```markdown
- **Terrain**: extraction roots in the *specific claimed body*, not the hex
  aggregate. A Mine/Excavation draws down a finite, depletable per-body resource
  stock (rolled once from regional richness at groundbreaking, then dug out until
  the rock runs dry); a Skimmer/Agri-complex draws a renewable yield from the
  claimed body's own attributes (a gas giant's mass, a world's biosphere and
  water). Output *and grade* still root in geography — now body-native, not the
  raster aggregate ([commodities.md](commodities.md)).
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~MarketSupplyTests|FullyQualifiedName~ConservationTests|FullyQualifiedName~InteriorEconomyTests"`
Expected: PASS — extraction reads/depletes the stock and yields from the giant; `ConservationTests` still green (a bounded goods draw from a finite stock is not a credit mint — value flows through the same wage/sale paths). If `InteriorEconomyTests` or another full-run suite surfaces an assertion tied to the old per-step extraction magnitude, re-baseline that expectation to the body-native output (an intended economic consequence, per Global Constraints), not the retired formula.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/MarketEngine.cs src/Core/Epoch/BodySiting.cs docs/design/substrate/infrastructure.md tests/Core.Tests/Epoch/MarketSupplyTests.cs
git rm tests/Core.Tests/Epoch/BodyExtractionTests.cs
git commit -m "feat(epoch): SupplyLands depletes/yields from the claimed body, retire RichnessModifier"
```

---

### Task 6: Serialize the body-resource stocks (`bodyresources` layer v1)

**Files:**
- Modify: `src/Core/Epoch/ArtifactSerializer.cs` (`Layers` table; a new `bodyresources` layer save + read)
- Test: `tests/Core.Tests/Epoch/BodyResourceRoundTripTests.cs`

**Interfaces:**
- Consumes: `SimState.BodyResources` (Task 2); `Stock`, `GoodId` (`StarGen.Core.Substrate`); `BodyRef`, `HexCoordinate`.
- Produces: a `bodyresources` layer (`("bodyresources", 1)`) writing one `BODYRES q r star slot good qty grade` line per stock entry, **sorted by (q, r, star, slot)** (P6); the reader parses each into `state.BodyResources` directly (real state, not re-derived). This is the one layer that persists hex-adjacent bodies' data in full — because a depleted stock cannot be regenerated from the pure hex tier.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Tests/Epoch/BodyResourceRoundTripTests.cs`:

```csharp
using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyResourceRoundTripTests
{
    [Fact]
    public void BodyStocks_RoundTripByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.BodyResources[(new HexCoordinate(2, -1), new BodyRef(0, 3))]
            = new Stock(GoodId.Ore, 1234.5, 0.42);
        state.BodyResources[(new HexCoordinate(2, -1), new BodyRef(1, 0))]
            = new Stock(GoodId.Exotics, 7.0, 0.8);

        var text1 = ArtifactSerializer.ToText(state);
        var reloaded = ArtifactSerializer.Load(new StringReader(text1));
        var text2 = ArtifactSerializer.ToText(reloaded);

        Assert.Equal(text1, text2);
        var s = reloaded.BodyResources[(new HexCoordinate(2, -1),
                                        new BodyRef(0, 3))];
        Assert.Equal(GoodId.Ore, s.Good);
        Assert.Equal(1234.5, s.Quantity, 6);
        Assert.Equal(0.42, s.Grade, 6);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter FullyQualifiedName~BodyResourceRoundTripTests`
Expected: FAIL — the stocks are not written, so `reloaded.BodyResources` is empty (`KeyNotFoundException`) and the texts differ.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/ArtifactSerializer.cs`, add `("bodyresources", 1)` to the `Layers` table (append after `("settled", 1)`, line 34 — new layers append, never reorder):

```csharp
        ("shipments", 1), ("orders", 1), ("couriers", 1), ("settled", 1),
        ("bodyresources", 1),
```

In `Save(...)`, after the `settled` layer block and before `w.WriteLine("END");` (line 550), add:

```csharp
        Layer(w, "bodyresources");
        // REAL mutable state (unlike settled): the depletable per-body stocks
        // are serialized in full — a dug-out rock cannot re-derive from the
        // pure hex tier. Sorted (q, r, star, slot) for P6.
        var stockKeys = new List<(HexCoordinate Hex, BodyRef Body)>(
            state.BodyResources.Keys);
        stockKeys.Sort((x, y) =>
        {
            int c = x.Hex.Q.CompareTo(y.Hex.Q);
            if (c != 0) return c;
            c = x.Hex.R.CompareTo(y.Hex.R);
            if (c != 0) return c;
            c = x.Body.StarIndex.CompareTo(y.Body.StarIndex);
            return c != 0 ? c : x.Body.SlotIndex.CompareTo(y.Body.SlotIndex);
        });
        foreach (var k in stockKeys)
        {
            var s = state.BodyResources[k];
            w.WriteLine(Join("BODYRES", k.Hex.Q.ToString(Inv),
                k.Hex.R.ToString(Inv), k.Body.StarIndex.ToString(Inv),
                k.Body.SlotIndex.ToString(Inv), ((int)s.Good).ToString(Inv),
                R(s.Quantity), R(s.Grade)));
        }
```

In the reader's record switch (beside the `case "SETTLED":` at ~line 1628), add:

```csharp
                    case "BODYRES":
                        state!.BodyResources[(
                            new HexCoordinate(int.Parse(f[1], Inv),
                                              int.Parse(f[2], Inv)),
                            new BodyRef(int.Parse(f[3], Inv),
                                        int.Parse(f[4], Inv)))] =
                            new StarGen.Core.Substrate.Stock(
                                (StarGen.Core.Substrate.GoodId)int.Parse(f[5], Inv),
                                double.Parse(f[6], Inv), double.Parse(f[7], Inv));
                        break;
```

(`List<HexCoordinate>` / `List<(…)>` need `System.Collections.Generic`, already imported in the serializer for the `settled` layer.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~BodyResourceRoundTripTests|FullyQualifiedName~ArtifactTests|FullyQualifiedName~SettledSystemsTests"`
Expected: PASS — new round-trip byte-identical; `ArtifactTests`/`SettledSystemsTests` (full save→load→save) still green with the appended layer.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/ArtifactSerializer.cs tests/Core.Tests/Epoch/BodyResourceRoundTripTests.cs
git commit -m "feat(epoch): serialize body-resource stocks (bodyresources v1)"
```

---

### Task 7: `Extraction.BodyStockRemaining` depletion metric

**Files:**
- Modify: `src/Core/Epoch/Health/MetricsOps.cs` (`MetricRow` gains a field; `Snapshot` fills it)
- Modify: `src/Core/Epoch/Health/MetricRegistry.cs` (one entry, name-sorted — first family)
- Modify: `docs/SIMHEALTH.md` (document the metric)
- Test: `tests/Core.Tests/Epoch/SettledSystemsTests.cs` (extend — the metric/snapshot test home)

**Interfaces:**
- Consumes: `SimState.BodyResources` (Task 2); `MetricRow`, `MetricsOps.Snapshot`, `MetricRegistry.Find` (existing).
- Produces: `MetricRow` gains `double BodyStockRemaining`; `MetricRegistry` gains `Extraction.BodyStockRemaining`; `docs/SIMHEALTH.md` documents it as a depletion signal (falls monotonically between fresh groundbreaks, rises when new deposits are rolled — no eviction).

- [ ] **Step 1: Write the failing test**

Append to `tests/Core.Tests/Epoch/SettledSystemsTests.cs` (inside the class):

```csharp
    [Fact]
    public void BodyStockRemainingMetric_SumsRemainingStock()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.NotNull(MetricRegistry.Find("Extraction.BodyStockRemaining"));
        state.BodyResources[(state.Actors[0].Seat, new BodyRef(0, 0))]
            = new StarGen.Core.Substrate.Stock(
                StarGen.Core.Substrate.GoodId.Ore, 100.0, 0.5);
        state.BodyResources[(state.Actors[0].Seat, new BodyRef(0, 1))]
            = new StarGen.Core.Substrate.Stock(
                StarGen.Core.Substrate.GoodId.Ore, 25.0, 0.5);
        var row = MetricsOps.Snapshot(state);
        Assert.Equal(125.0, row.BodyStockRemaining, 6);
        Assert.Equal(125.0,
            MetricRegistry.Find("Extraction.BodyStockRemaining")!.Get(row), 6);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~SettledSystemsTests|FullyQualifiedName~MetricRegistryTests"`
Expected: FAIL — build error, `MetricRow.BodyStockRemaining` and the `Extraction.BodyStockRemaining` registry entry do not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/Core/Epoch/Health/MetricsOps.cs`, add `double BodyStockRemaining` as the last positional parameter of `MetricRow` (after `int SettledHexes`, line 35):

```csharp
public sealed record MetricRow(
    int Epoch, int WorldYear, MoneyRow Money,
    int LivePolities, int NegativeTreasuries,
    double MinPolityCredits, double MedianPolityCredits,
    double MaxPolityCredits,
    double Population, double MeanSoL,
    int EndowedEntries, double ConservationResidual,
    double CumulativeFiatIssued, double CumulativeSteadyIssuance,
    int SettledHexes, double BodyStockRemaining);
```

In `Snapshot(...)`, compute the total remaining stock before the `return new MetricRow(...)` (line 146) and append it as the final argument:

```csharp
        double bodyStock = 0;
        foreach (var s in state.BodyResources.Values) bodyStock += s.Quantity;

        return new MetricRow(state.EpochIndex, state.WorldYear, money,
            credits.Count, negative, min, median, max,
            pop, pop <= 0 ? 0.0 : sol / pop,
            endowed, residual, state.CumulativeFiatIssued,
            state.CumulativeSteadyIssuance, state.SettledSystems.Count,
            bodyStock);
```

In `src/Core/Epoch/Health/MetricRegistry.cs`, add the entry as the first family (`Extraction.*` sorts before `Money.*`), at the top of the `Table` initializer (before the `// ---- Money` block, line 25):

```csharp
        // ---- Extraction (body resource stocks — locality) ----
        M("Extraction.BodyStockRemaining",
          "total remaining depletable body-resource stock (falls as bodies deplete)",
          r => r.BodyStockRemaining),

```

In `docs/SIMHEALTH.md`, add `Extraction.BodyStockRemaining` under a new "Extraction" heading following the file's per-metric format: what it means (the sum of every depletable Mine/Excavation body's remaining stock), healthy shape (rises when new deposits are groundbroken, falls monotonically as active mines dig them out — a mature, mine-heavy galaxy trends down between founding waves), and the known open question (no eviction/relocation proposed yet — this metric is how the depletion rate gets evidence-based scrutiny before any relocation mechanic is designed).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Core.Tests --filter "FullyQualifiedName~SettledSystemsTests|FullyQualifiedName~MetricRegistryTests"`
Expected: PASS — snapshot sum green; `MetricRegistryTests` (order/uniqueness/docs/accessor) green with the new first-family entry.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Epoch/Health/MetricsOps.cs src/Core/Epoch/Health/MetricRegistry.cs docs/SIMHEALTH.md tests/Core.Tests/Epoch/SettledSystemsTests.cs
git commit -m "feat(health): Extraction.BodyStockRemaining depletion metric"
```

---

## Slice-end gates (run before handing off, not a task)

- `dotnet test StarSystemGeneration.sln` fully green — the hex-tier (Phase-1) suite never broke; `ConservationTests` and any determinism suite green. Full-run suites (`InteriorEconomyTests`, allocation/economy integration) may need a one-time re-baseline where they pinned the *old* per-step extraction magnitude — the body-native output is the design's intended economic consequence, not a regression.
- Determinism byte-identity: two full runs at the same config produce byte-identical artifacts (the stock roll is a stateless hash keyed by hex+body+seed); save→load→save is byte-identical (Task 6's round-trip test is the unit witness, plus the sorted `bodyresources` layer).
- Golden re-freeze: extraction output, facility counts (some extraction groundbreaks now abort), and the new `bodyresources` layer legitimately changed the artifacts — re-freeze the goldens once, at slice end (red-window inside the slice), per project discipline.
- REPL eyeball (the taste gate): drive the sim forward and inspect a developed hex — confirm a Mine posts ore that draws its body's stock down over epochs (and a poor body runs dry while a rich one keeps producing), a Skimmer/Agri yields steadily from its giant/biosphere with no stock entry, and a hex whose committed system holds no eligible body simply never grows that extraction facility. Pipe commands via `printf 'cmd\n' | dotnet run --project src/Inspector`.
