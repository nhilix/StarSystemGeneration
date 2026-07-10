using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>One supplier's deposit this step — the attribution the revenue
/// pool is distributed against at clearing (labor share to the port's
/// segments, remainder to the owner).</summary>
public sealed record SupplyRecord(int MarketIndex, int OwnerActorId, double Value);

/// <summary>Step-transient market bookkeeping — never state, never serialized
/// (P6: transients are not state). Buyers pay into the per-market pool;
/// clearing distributes it to this step's suppliers.</summary>
public sealed class MarketStepScratch
{
    /// <summary>Credits paid in by buyers this step, per market index.</summary>
    public double[] PoolByMarket { get; }
    /// <summary>Deposit attribution, in facility id order.</summary>
    public List<SupplyRecord> Supplies { get; } = new List<SupplyRecord>();

    public MarketStepScratch(int marketCount)
    {
        PoolByMarket = new double[marketCount];
    }
}

/// <summary>The market step's mechanics (economy/markets.md), run by
/// MarketsPhase in the design's fixed order: supply lands → demand assembles →
/// price adjusts → freight moves → clearing and consequences. Everything is
/// deterministic and roll-free: facilities in id order, goods in id order,
/// markets in port-id order (P6).</summary>
public static class MarketEngine
{
    /// <summary>Neutral machinery grade when a market holds none — the grade
    /// multiplier is 1.0 at 0.5, so missing machinery neither helps nor hurts.</summary>
    private const double DefaultMachineryGrade = 0.5;

    /// <summary>The market a facility sells into: its owner's nearest port
    /// (tie: lower port id) — derived, never stored. −1 when the owner has no
    /// port (orphaned by conquest; production idles).</summary>
    public static int AttachedMarketIndex(SimState state, Facility f)
    {
        int best = -1, bestDist = int.MaxValue;
        foreach (var p in state.Ports)
        {
            if (p.OwnerActorId != f.OwnerActorId) continue;
            int dist = HexGrid.Distance(p.Hex, f.Hex);
            if (dist < bestDist) { bestDist = dist; best = p.Id; }
        }
        return best;
    }

    /// <summary>True once construction time has elapsed (assets-and-
    /// investment.md: the site exists before the facility does).</summary>
    public static bool IsActive(SimState state, Facility f) =>
        state.WorldYear >= f.BuiltYear
                           + Infrastructure.Get((InfraTypeId)f.TypeId).ConstructionYears;

    /// <summary>Step 1 — supply lands: every active facility produces per
    /// C's formula and sells into its attached market. Extraction reads the
    /// genesis fields at its hex (output AND grade root in geography);
    /// processing consumes market inventory through recipes, paying input
    /// costs from the owner's credits into the market pool.</summary>
    public static void SupplyLands(SimState state, MarketStepScratch scratch)
    {
        var cfg = state.Config;
        int years = cfg.Sim.YearsPerEpoch;
        foreach (var f in state.Facilities)               // id order (P6)
        {
            if (!IsActive(state, f)) continue;
            int mIx = AttachedMarketIndex(state, f);
            if (mIx < 0) continue;
            var market = state.Markets[mIx];
            var port = state.Ports[market.PortId];
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            if (def.Produces.Count == 0) continue;        // keystone/support

            var fields = FieldsAt(state, f.Hex);
            double labor = 0;
            var embodiment = DominantEmbodiment(state, port.Id, ref labor);
            double laborFactor = Production.LaborFactor(labor,
                Potentials.EmbodimentAffinity(embodiment, fields),
                automationCompute: 0.0, def.LaborRequired);

            double machineryGrade = market.Inventory[(int)GoodId.Machinery] > 0
                ? market.InventoryGrade[(int)GoodId.Machinery]
                : DefaultMachineryGrade;
            double share = 1.0 / def.Produces.Count;

            foreach (var good in def.Produces)            // catalog order
            {
                double terrain = ExtractionPotential((InfraTypeId)f.TypeId,
                                                     good, fields);
                double capacity = Production.Output(def, f.Tier, terrain,
                                     laborFactor, machineryGrade)
                                  * share * years * f.Condition;
                if (capacity <= 0) continue;

                var recipes = Goods.Get(good).Recipes;
                if (recipes.Count == 0)
                    Deposit(state, scratch, mIx, f.OwnerActorId, (int)good,
                            capacity, Potentials.RawGrade(terrain));
                else
                    RunRecipe(state, scratch, mIx, f, recipes, capacity);
            }
        }
    }

    /// <summary>Convert inputs to output through the best recipe the config
    /// tech stub allows: advanced variants first (higher grade base), falling
    /// back by producible quantity. Inputs are drawn from the market at its
    /// mean grades and paid for from the owner's credits.</summary>
    private static void RunRecipe(SimState state, MarketStepScratch scratch,
                                  int mIx, Facility f,
                                  IReadOnlyList<Recipe> recipes, double capacity)
    {
        var market = state.Markets[mIx];
        int techTier = state.Config.Economy.TechTierStub;
        Recipe? pick = null;
        double pickQty = 0;
        foreach (var r in recipes)
        {
            if (r.MinTechTier > techTier) continue;
            double byInputs = capacity;
            foreach (var q in r.Inputs)
                byInputs = Math.Min(byInputs,
                    market.Inventory[(int)q.Good] / q.Quantity);
            bool better = pick == null
                || (r.Kind == RecipeKind.Advanced && pick.Kind == RecipeKind.Standard
                    && byInputs > 0)
                || (r.Kind == pick.Kind && byInputs > pickQty);
            if (better) { pick = r; pickQty = byInputs; }
        }
        if (pick == null || pickQty <= 0) return;

        // affordability bounds quantity: inputs are bought at market prices
        var owner = state.PolityOf(f.OwnerActorId);
        double costPerUnit = 0;
        foreach (var q in pick.Inputs)
            costPerUnit += q.Quantity * market.Price[(int)q.Good];
        double qty = pickQty;
        if (costPerUnit > 0 && owner.Credits < qty * costPerUnit)
            qty = Math.Max(0, owner.Credits / costPerUnit);
        if (qty <= 0) return;

        double gradeSum = 0, weightSum = 0;
        foreach (var q in pick.Inputs)
        {
            gradeSum += market.InventoryGrade[(int)q.Good] * q.Quantity;
            weightSum += q.Quantity;
            double drawn = market.Draw((int)q.Good, qty * q.Quantity);
            market.LastCleared[(int)q.Good] += drawn;
        }
        double cost = qty * costPerUnit;
        owner.Credits -= cost;
        scratch.PoolByMarket[mIx] += cost;

        double meanInputGrade = weightSum > 0 ? gradeSum / weightSum : 0.5;
        double grade = Grades.Output(pick, meanInputGrade, f.Tier, techTier);
        Deposit(state, scratch, mIx, f.OwnerActorId, (int)pick.Output, qty, grade);
    }

    private static void Deposit(SimState state, MarketStepScratch scratch,
                                int mIx, int ownerActorId, int good,
                                double qty, double grade)
    {
        var market = state.Markets[mIx];
        market.Deposit(good, qty, grade);
        scratch.Supplies.Add(new SupplyRecord(mIx, ownerActorId,
                                              qty * market.Price[good]));
    }

    /// <summary>Terrain potential per extraction type at the facility's cell;
    /// 1.0 for processing (the formula's neutral terrain).</summary>
    private static double ExtractionPotential(InfraTypeId type, GoodId good,
                                              CellFields fields) => type switch
    {
        InfraTypeId.Mine => Potentials.Ore(fields),
        InfraTypeId.Skimmer => Potentials.Volatiles(fields),
        InfraTypeId.AgriComplex => Potentials.Biosphere(fields),
        InfraTypeId.ExcavationSite => Potentials.Exotics(fields),
        _ => 1.0,
    };

    /// <summary>Adapt B's cell to C's plain-argument fields at a hex (the
    /// `infra` REPL command's pattern). Void/missing cells read as barren.</summary>
    public static CellFields FieldsAt(SimState state, HexCoordinate hex)
    {
        if (!state.Skeleton.TryGetCell(HexGrid.CellOf(hex), out var cell))
            return new CellFields(0, StellarLean.Balanced, 0, false, false);
        bool mineral = false, precursor = false;
        foreach (var a in cell.Anchors)
        {
            if (a.Type == AnchorType.MineralRich) mineral = true;
            if (a.Type == AnchorType.PrecursorSite) precursor = true;
        }
        return new CellFields(cell.MeanDensity, cell.Lean, cell.Metallicity,
                              mineral, precursor);
    }

    /// <summary>Total workforce and dominant embodiment (largest segment,
    /// tie: lower id) of the port's population.</summary>
    private static Embodiment DominantEmbodiment(SimState state, int portId,
                                                 ref double labor)
    {
        double biggest = -1;
        var embodiment = Embodiment.TerranAnalog;
        foreach (var s in state.Segments)                 // id order (P6)
        {
            if (s.PortId != portId) continue;
            labor += s.Size;
            if (s.Size > biggest)
            {
                biggest = s.Size;
                // shape-only skeletons (frame tests) carry no species profiles
                embodiment = s.SpeciesId >= 0
                             && s.SpeciesId < state.Skeleton.Species.Count
                    ? state.Skeleton.Species[s.SpeciesId].Embodiment
                    : Embodiment.TerranAnalog;
            }
        }
        return embodiment;
    }
}
