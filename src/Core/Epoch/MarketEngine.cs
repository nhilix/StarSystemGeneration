using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>One supplier's deposit this step — the attribution the revenue
/// pool is distributed against at clearing.</summary>
public sealed record SupplyRecord(int MarketIndex, int OwnerActorId, double Value);

/// <summary>One buyer's banded want at one market — assembled by demand,
/// consumed by clearing in band-priority order.</summary>
public sealed record DemandRecord(
    int MarketIndex, int SegmentId, PopulationBand Band, int Good, double Quantity);

/// <summary>Step-transient market bookkeeping — never state, never serialized
/// (P6: transients are not state). Buyers pay into the per-market pool;
/// clearing distributes it to this step's suppliers.</summary>
public sealed class MarketStepScratch
{
    /// <summary>Credits paid in by buyers this step, per market index.</summary>
    public double[] PoolByMarket { get; }
    /// <summary>Deposit attribution, in facility id order.</summary>
    public List<SupplyRecord> Supplies { get; } = new List<SupplyRecord>();
    /// <summary>Aggregate legal demand per market per good — the price signal
    /// and the freight gradient input.</summary>
    public double[][] Demand { get; }
    /// <summary>Banded population demand, in market → segment → band order.</summary>
    public List<DemandRecord> DemandRecords { get; } = new List<DemandRecord>();
    /// <summary>Subsistence units needed / cleared per segment id — the
    /// famine arithmetic.</summary>
    public double[] SubsistenceNeed { get; }
    public double[] SubsistenceCleared { get; }
    public double[] SoLNeed { get; }
    public double[] SoLCleared { get; }
    /// <summary>Freight units already carried per lane id this step,
    /// budgeted against the posted capacity.</summary>
    public double[] LaneCapacityUsed { get; }
    /// <summary>Posted freight capacity per lane id this epoch — the
    /// fleet-capacity interface (fleets/ships-and-fleets.md): Σ cargo ×
    /// trips of the Posted fleets. A lane without hulls moves nothing.</summary>
    public double[] LaneFleetCapacity { get; }
    /// <summary>Lanes closed this step: debug cuts + blockade postures,
    /// derived once from fleet state.</summary>
    public HashSet<int> Severed { get; }

    public MarketStepScratch(SimState state)
    {
        PoolByMarket = new double[state.Markets.Count];
        Demand = new double[state.Markets.Count][];
        for (int i = 0; i < Demand.Length; i++)
            Demand[i] = new double[Goods.All.Count];
        SubsistenceNeed = new double[state.Segments.Count];
        SubsistenceCleared = new double[state.Segments.Count];
        SoLNeed = new double[state.Segments.Count];
        SoLCleared = new double[state.Segments.Count];
        LaneCapacityUsed = new double[state.Lanes.Count];
        LaneFleetCapacity = new double[state.Lanes.Count];
        foreach (var lane in state.Lanes)                 // id order (P6)
            LaneFleetCapacity[lane.Id] = FleetOps.PostedCapacity(state, lane);
        Severed = FleetOps.SeveredLaneIds(state);
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
    /// multiplier is 1.0 at 0.5, so missing machinery neither helps nor hurts.
    /// (Structural, not a knob: 0.5 is the grade system's defined midpoint.)</summary>
    private const double DefaultMachineryGrade = 0.5;

    private static readonly PopulationBand[] Bands =
    {
        PopulationBand.Subsistence, PopulationBand.StandardOfLiving,
        PopulationBand.Luxury,
    };

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
        if (best >= 0) return best;
        // a portless owner (a corporation, slice G) trades at the nearest
        // sovereign's port — cross-border portfolios attach where they sit
        foreach (var p in state.Ports)
        {
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

    // ------------------------------------------------------------------
    // Step 1 — supply lands
    // ------------------------------------------------------------------

    /// <summary>Every active facility produces per C's formula and sells into
    /// its attached market. Extraction reads the genesis fields at its hex
    /// (output AND grade root in geography); processing consumes market
    /// inventory through recipes, paying input costs from the owner's credits
    /// into the market pool. Wages precede sales: the labor share of each
    /// deposit's value goes to the staffing segments immediately, giving
    /// households purchasing power this same step.</summary>
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
                double utilization = Math.Min(1.0,
                    Math.Max(cfg.Economy.MinUtilization,
                        market.Price[(int)good]
                        / Market.InitialPrice(cfg.Economy, good)));
                double capacity = Production.Output(def, f.Tier, terrain,
                                     laborFactor, machineryGrade)
                                  * share * years * f.Condition * utilization;
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

    /// <summary>Convert inputs to output through the best recipe the owner's
    /// Industrial tier allows (slice G: per-polity tech, the stub retired):
    /// advanced variants first (higher grade base), falling back by
    /// producible quantity. Inputs are drawn from the market at its mean
    /// grades and paid for from the owner's credits.</summary>
    private static void RunRecipe(SimState state, MarketStepScratch scratch,
                                  int mIx, Facility f,
                                  IReadOnlyList<Recipe> recipes, double capacity)
    {
        var market = state.Markets[mIx];
        int techTier = Tech.Tier(state, f.OwnerActorId, TechDomain.Industrial);
        Recipe? pick = null;
        double pickQty = 0, pickWorth = 0;
        foreach (var r in recipes)
        {
            if (r.MinTechTier > techTier) continue;
            double byInputs = capacity;
            foreach (var q in r.Inputs)
                byInputs = Math.Min(byInputs,
                    market.Inventory[(int)q.Good] / q.Quantity);
            // quantity × grade base: an advanced variant wins on quality only
            // while its inputs allow real volume — a drop of the good stuff
            // never beats a vat of the standard issue
            double worth = byInputs * r.GradeBase;
            if (pick == null || worth > pickWorth)
            { pick = r; pickQty = byInputs; pickWorth = worth; }
        }
        if (pick == null || pickQty <= 0) return;

        // inputs are bought at market prices on working capital: the owner's
        // ledger may dip within the step — sales revenue lands at
        // distribution, and insolvency is Allocation's credit problem
        var owner = state.LedgerOf(f.OwnerActorId);
        double costPerUnit = 0;
        foreach (var q in pick.Inputs)
            costPerUnit += q.Quantity * market.Price[(int)q.Good];
        // no production at a loss: a facility whose inputs cost more than its
        // output sells idles, freeing the feedstock for whoever values it —
        // working capital does not fund value destruction
        if (market.Price[(int)pick.Output] <= costPerUnit) return;
        double qty = pickQty;

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

    /// <summary>Labor share to the staffing segments, pro-rata by size —
    /// household income is earned from realized revenue, not assumed
    /// (economy/markets.md §Household income). Unsold goods pay nobody.
    /// Also the construction-wage channel: treasury spending on lanes, tiers,
    /// and facilities pays the building port's households, so investment
    /// recycles into circulation instead of vanishing.</summary>
    internal static void PayWages(SimState state, int portId, double wage)
    {
        if (wage <= 0) return;
        double totalSize = 0;
        foreach (var s in state.Segments)
            if (s.PortId == portId) totalSize += s.Size;
        if (totalSize <= 0)
        {
            // an unpeopled port has no payroll: the sum reverts to the port's
            // polity rather than vanishing (P4 — credits are never destroyed)
            var owner = state.PolityOf(state.Ports[portId].OwnerActorId);
            owner.Credits += wage;
            return;
        }
        foreach (var s in state.Segments)
            if (s.PortId == portId)
                s.Wealth += wage * s.Size / totalSize;
    }

    // ------------------------------------------------------------------
    // Step 2 — demand assembles
    // ------------------------------------------------------------------

    /// <summary>Population demand per band: C's normalized profiles × the
    /// config's absolute per-capita rates × segment size, embodiment-
    /// modulated, price-elastic, with the organic baseline offsetting
    /// provisions (self-supply — unserviced systems are poor, not starving).
    /// Prohibition converts demand into the black book instead of deleting
    /// it.</summary>
    public static void AssembleDemand(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var market = state.Markets[mIx];
            Array.Clear(market.BlackBookDemand, 0, market.BlackBookDemand.Length);
            Array.Clear(market.BlackBookPrice, 0, market.BlackBookPrice.Length);
            var port = state.Ports[market.PortId];
            var law = (state.Actors[port.OwnerActorId].Policies as PolityPolicies
                       ?? PolityPolicies.Default).LawCode;
            double biosphere = Potentials.Biosphere(FieldsAt(state, port.Hex));

            foreach (var seg in state.Segments)           // id order (P6)
            {
                if (seg.PortId != port.Id || seg.Size <= 0) continue;
                var embodiment = EmbodimentOf(state, seg.SpeciesId);
                // demand is want backed by ability to pay: the price signal
                // reads income-backed demand, so poverty reads as glut, not
                // as a frozen high price
                double budget = Math.Max(0.0, seg.Wealth);
                // self-supply is embodiment-relative like the need it offsets:
                // a lithic farms as little as it eats
                double baseline =
                    Production.OrganicBaseline(seg.Size, biosphere) * years
                    * DemandProfiles.SubsistenceScale(embodiment);

                foreach (var band in Bands)
                {
                    double rate = band switch
                    {
                        PopulationBand.Subsistence =>
                            eco.SubsistenceUnitsPerPopPerYear
                            * DemandProfiles.SubsistenceScale(embodiment),
                        PopulationBand.StandardOfLiving => eco.SoLUnitsPerPopPerYear,
                        _ => eco.LuxuryUnitsPerPopPerYear,
                    };
                    double bandTotal = seg.Size * rate * years;
                    foreach (var (good, weight) in
                             DemandProfiles.Population(embodiment, band))
                    {
                        double qty = bandTotal * weight;
                        if (band == PopulationBand.Subsistence)
                        {
                            scratch.SubsistenceNeed[seg.Id] += qty;
                            if (good == GoodId.Provisions && baseline > 0)
                            {
                                double offset = Math.Min(qty, baseline);
                                qty -= offset;
                                scratch.SubsistenceCleared[seg.Id] += offset;
                            }
                        }
                        else if (band == PopulationBand.StandardOfLiving)
                            scratch.SoLNeed[seg.Id] += qty;
                        if (qty <= 0) continue;

                        qty *= ElasticFactor(eco, market, (int)good, band);
                        double price = market.Price[(int)good];
                        if (price > 0 && qty * price > budget)
                            qty = budget / price;         // poverty caps the want
                        if (qty <= 0) continue;
                        double black = law.TryGetValue((int)good, out var level)
                            ? level switch
                            {
                                LegalityLevel.Prohibited => qty,
                                LegalityLevel.Restricted => qty * 0.5,
                                _ => 0.0,
                            } : 0.0;
                        if (black > 0)
                        {
                            market.BlackBookDemand[(int)good] += black;
                            market.BlackBookPrice[(int)good] =
                                market.Price[(int)good] * eco.BlackMarketMarkup;
                        }
                        double legal = qty - black;
                        if (legal <= 0) continue;
                        // only the legal basket spends the budget: black-book
                        // wants go unserved until smuggling exists (H)
                        budget -= legal * price;
                        scratch.Demand[mIx][(int)good] += legal;
                        scratch.DemandRecords.Add(new DemandRecord(
                            mIx, seg.Id, band, (int)good, legal));
                    }
                }
            }
        }
    }

    /// <summary>Demand's price response around the founding price, clamped —
    /// elasticity by band.</summary>
    private static double ElasticFactor(EconomyKnobs eco, Market market,
                                        int good, PopulationBand band)
    {
        double basePrice = Market.InitialPrice(eco, (GoodId)good);
        if (basePrice <= 0) return 1.0;
        double elasticity = band switch
        {
            PopulationBand.Subsistence => eco.SubsistenceElasticity,
            PopulationBand.StandardOfLiving => eco.SoLElasticity,
            _ => eco.LuxuryElasticity,
        };
        double factor = Math.Pow(market.Price[good] / basePrice, -elasticity);
        return Math.Min(eco.ElasticCeiling, Math.Max(eco.ElasticFloor, factor));
    }

    /// <summary>Industry inputs are demand (commodities.md demand model #2):
    /// every active facility's recipe inputs at potential output plus its
    /// upkeep draw register at its market, so the price signal keeps the
    /// chain's feedstocks produced — without this, machinery reads as
    /// wanted-by-nobody, floors, and the whole industrial base rots.</summary>
    public static void AddIndustrialDemand(SimState state, MarketStepScratch scratch)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var f in state.Facilities)               // id order (P6)
        {
            if (!IsActive(state, f)) continue;
            int techTier = Tech.Tier(state, f.OwnerActorId, TechDomain.Industrial);
            int mIx = AttachedMarketIndex(state, f);
            if (mIx < 0) continue;
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            var market = state.Markets[mIx];
            double share = def.Produces.Count > 0 ? 1.0 / def.Produces.Count : 0;
            foreach (var good in def.Produces)
            {
                // planned, not maximal: the same price throttle production
                // runs under, so input demand tracks what will really be made
                double utilization = Math.Min(1.0,
                    Math.Max(state.Config.Economy.MinUtilization,
                        market.Price[(int)good]
                        / Market.InitialPrice(state.Config.Economy, good)));
                double potential = def.BaseOutputPerYear
                                   * Production.TierOutputFactor(f.Tier)
                                   * share * years * utilization * f.Condition;
                foreach (var r in Goods.Get(good).Recipes)
                {
                    if (r.MinTechTier > techTier) continue;
                    foreach (var q in r.Inputs)
                        scratch.Demand[mIx][(int)q.Good] += q.Quantity * potential;
                    break;                                // first viable variant
                }
            }
            double upkeepScale = Production.TierCostFactor(f.Tier) * years;
            foreach (var q in def.UpkeepPerYear)
                scratch.Demand[mIx][(int)q.Good] += q.Quantity * upkeepScale;
        }
    }

    /// <summary>Development pulls its own materials: an under-capacity port
    /// of a polity with a development budget registers demand for the
    /// construction basket, so the price rises and freight hauls it in —
    /// industry demand per the design's demand model, sized to one facility
    /// per epoch. Unmet stockpile targets likewise register at the capital:
    /// polity procurement is a market participant (market-geography.md).</summary>
    public static void AddConstructionPull(SimState state, MarketStepScratch scratch)
    {
        var infra = state.Config.Infrastructure;
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            if (!state.Actors[pr.ActorId].Entered) continue;
            var targets = (state.Actors[pr.ActorId].Policies as PolityPolicies
                           ?? PolityPolicies.Default).StockpileTargets;
            if (targets.Count > 0)
                foreach (var port in state.Ports)         // capital = first own
                {
                    if (port.OwnerActorId != pr.ActorId) continue;
                    for (int g = 0; g < Goods.All.Count; g++)
                        if (targets.TryGetValue(g, out double target)
                            && target > pr.ReserveQty[g])
                            scratch.Demand[port.Id][g] += target - pr.ReserveQty[g];
                    break;
                }
            if (pr.DevelopmentPoints < infra.ConstructionDevGate) continue;
            foreach (var port in state.Ports)             // id order (P6)
            {
                if (port.OwnerActorId != pr.ActorId) continue;
                int cap = port.Tier * infra.FacilitiesPerPortTier;
                int attached = 0;
                foreach (var f in state.Facilities)
                    if (f.OwnerActorId == pr.ActorId
                        && AttachedMarketIndex(state, f) == port.Id) attached++;
                if (attached >= cap) continue;
                scratch.Demand[port.Id][(int)GoodId.Alloys]
                    += infra.ConstructionPullAlloys;
                scratch.Demand[port.Id][(int)GoodId.Machinery]
                    += infra.ConstructionPullMachinery;
                scratch.Demand[port.Id][(int)GoodId.Composites]
                    += infra.ConstructionPullComposites;
            }
        }
    }

    /// <summary>Military-construction demand (the MilitaryConstruction
    /// use-case D left wired but unused): a polity whose military treasury
    /// could pay for hulls registers Ship Components demand at its yard
    /// port — the capital until a yard exists. Without this pull the
    /// components price floors, no shipyard ever out-scores a mine, and the
    /// navy never gets built (the E bootstrap loop: demand → price signal →
    /// yard sites → components flow → hulls lay down).</summary>
    public static void AddMilitaryDemand(SimState state, MarketStepScratch scratch)
    {
        var fleet = state.Config.Fleet;
        double hullValue = DesignMath.ComponentsPerHull(fleet, ShipSize.Medium)
            * Market.InitialPrice(state.Config.Economy, GoodId.ShipComponents);
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            if (!state.Actors[pr.ActorId].Entered) continue;
            if (pr.MilitaryPoints < hullValue) continue;  // can't pay, don't pull
            int at = YardPortOf(state, pr.ActorId);
            if (at < 0) continue;
            scratch.Demand[at][(int)GoodId.ShipComponents]
                += fleet.MilitaryPullComponents;
        }
    }

    /// <summary>A funded research line pulls its feedstocks (slice G,
    /// technology.md): exotics and compute demand registers at the capital,
    /// so the price signal sites the labs and cores research bottlenecks on.</summary>
    public static void AddResearchDemand(SimState state, MarketStepScratch scratch)
    {
        var tech = state.Config.Tech;
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            var actor = state.Actors[pr.ActorId];
            if (!actor.Entered) continue;
            var budget = (actor.Policies as PolityPolicies
                          ?? PolityPolicies.Default).Budget;
            if (budget.Research <= 0) continue;
            int capital = -1;
            foreach (var port in state.Ports)             // id order (P6)
                if (port.OwnerActorId == pr.ActorId) { capital = port.Id; break; }
            if (capital < 0) continue;
            scratch.Demand[capital][(int)GoodId.RefinedExotics]
                += tech.ResearchPullExotics;
            scratch.Demand[capital][(int)GoodId.Compute]
                += tech.ResearchPullCompute;
        }
    }

    /// <summary>The port a polity's naval procurement lands at: the first
    /// port (id order) with an active own shipyard attached, else the
    /// capital (first own port — the AddConstructionPull convention).</summary>
    internal static int YardPortOf(SimState state, int actorId)
    {
        int capital = -1;
        foreach (var port in state.Ports)                 // id order (P6)
        {
            if (port.OwnerActorId != actorId) continue;
            if (capital < 0) capital = port.Id;
            foreach (var f in state.Facilities)           // id order (P6)
                if (f.OwnerActorId == actorId
                    && f.TypeId == (int)InfraTypeId.Shipyard
                    && IsActive(state, f)
                    && AttachedMarketIndex(state, f) == port.Id)
                    return port.Id;
        }
        return capital;
    }

    /// <summary>Re-export demand (economy/markets.md §2): bids from
    /// arbitrageurs who see outbound gradients bid up a hub's price even with
    /// zero local consumption — without this term goods refuse to enter
    /// markets that don't personally want them; with it, entrepôts emerge.
    /// A pure price signal: nothing is bought here.</summary>
    public static void AddReExportDemand(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (scratch.Severed.Contains(lane.Id)) continue;
            // hubs pull only what posted hulls could actually carry out —
            // no fleet, no re-export bid (the design's capacity interface)
            double laneFlow = scratch.LaneFleetCapacity[lane.Id]
                              * eco.ReExportWeight;
            if (laneFlow <= 0) continue;
            var mA = state.Markets[lane.PortAId];
            var mB = state.Markets[lane.PortBId];
            for (int g = 0; g < mA.Price.Length; g++)
            {
                double pa = mA.Price[g], pb = mB.Price[g];
                if (pa <= 0 || pb <= 0 || pa == pb) continue;
                // demand lands at the cheap end, scaled by the gradient
                if (pb > pa)
                    scratch.Demand[lane.PortAId][g] += laneFlow * (pb - pa) / pb;
                else
                    scratch.Demand[lane.PortBId][g] += laneFlow * (pa - pb) / pa;
            }
        }
    }

    // ------------------------------------------------------------------
    // Step 3 — price adjusts
    // ------------------------------------------------------------------

    /// <summary>Each (market, good) price drifts toward clearing: excess
    /// demand pushes up, glut pushes down, rate-limited per world-year —
    /// markets never perfectly clear; persistent gradients ARE the trade
    /// opportunities. Lane-connected markets are additionally disciplined by
    /// import parity: nobody pays ceiling prices for what a neighbor sells
    /// at glut plus transport — a blockade (severed lane) removes the
    /// alternative and with it the cap, which is exactly the spike.</summary>
    public static void AdjustPrices(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        double cap = Math.Exp(eco.PriceDriftMaxPerYear
                              * state.Config.Sim.YearsPerEpoch);
        const double eps = 1e-9;
        // parity reads pre-drift prices so lane order cannot matter (P6)
        var snapshot = new double[state.Markets.Count][];
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
            snapshot[mIx] = (double[])state.Markets[mIx].Price.Clone();

        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var market = state.Markets[mIx];
            for (int g = 0; g < market.Price.Length; g++)
            {
                double demand = scratch.Demand[mIx][g];
                double supply = market.Inventory[g];
                if (demand <= eps && supply <= eps) continue;   // dormant good
                double factor = Math.Pow((demand + eps) / (supply + eps),
                                         eco.PriceDriftExponent);
                factor = Math.Min(cap, Math.Max(1.0 / cap, factor));
                double ceiling = Market.InitialPrice(eco, (GoodId)g)
                                 * eco.MaxPriceMultiple;
                market.Price[g] = Math.Min(ceiling,
                    Math.Max(eco.PriceFloor, market.Price[g] * factor));
            }
        }

        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            // parity discipline needs a lane goods can actually cross:
            // severed or hull-less lanes cap nothing — an unserved market
            // spikes exactly like a blockaded one (visible naval shortage)
            if (scratch.Severed.Contains(lane.Id)
                || scratch.LaneFleetCapacity[lane.Id] <= 0) continue;
            ApplyImportParity(state, snapshot, lane.PortAId, lane.PortBId);
            ApplyImportParity(state, snapshot, lane.PortBId, lane.PortAId);
        }
    }

    /// <summary>Cap the destination's price at what importing from this
    /// neighbor would cost: source price + freight + fuel + tariff +
    /// friction, grossed up by the exporter's realized margin (tax and labor
    /// share) plus headroom so the trade still pays.</summary>
    private static void ApplyImportParity(SimState state, double[][] snapshot,
                                          int srcId, int dstId)
    {
        var eco = state.Config.Economy;
        var src = state.Ports[srcId];
        var dst = state.Ports[dstId];
        int dist = HexGrid.Distance(src.Hex, dst.Hex);
        double freight = eco.FreightCostPerUnitPerHex * dist;
        double fuel = eco.FuelPerUnitPerHex * dist
                      * snapshot[srcId][(int)GoodId.Fuel];
        var dstPolicies = state.Actors[dst.OwnerActorId].Policies
                          as PolityPolicies ?? PolityPolicies.Default;
        double margin = (1.0 - dstPolicies.TaxRate) * (1.0 - eco.LaborShare);
        if (margin <= 0) return;
        var dstMarket = state.Markets[dstId];
        for (int g = 0; g < dstMarket.Price.Length; g++)
        {
            // no supply to import against, no parity
            if (state.Markets[srcId].Inventory[g] <= 0) continue;
            var srcLevel = LegalityAt(state, src.OwnerActorId, g);
            var dstLevel = LegalityAt(state, dst.OwnerActorId, g);
            if (srcLevel == LegalityLevel.Prohibited
                || dstLevel == LegalityLevel.Prohibited) continue;
            double tariff = 0;
            if (src.OwnerActorId != dst.OwnerActorId
                && dstPolicies.TariffSchedule.TryGetValue(g, out double rate))
                tariff = rate * snapshot[dstId][g];
            double friction = srcLevel == LegalityLevel.Restricted
                              || dstLevel == LegalityLevel.Restricted
                ? eco.RestrictedFriction * snapshot[dstId][g] : 0;
            double parity = (snapshot[srcId][g] + freight + fuel + tariff + friction)
                            / margin * eco.ParityHeadroom;
            if (parity < dstMarket.Price[g])
                dstMarket.Price[g] = Math.Max(eco.PriceFloor, parity);
        }
    }

    // ------------------------------------------------------------------
    // Step 4 — freight moves
    // ------------------------------------------------------------------

    /// <summary>Step 4 — freight (economy/markets.md §4), three generators in
    /// deterministic order, all within lane capacity (the fleet-capacity stub
    /// E replaces): reserve release to starving ports (internal logistics —
    /// markets see only the endpoint), lane arbitrage on price gaps net of
    /// freight + fuel + tariffs with legality at both ends, and polity
    /// procurement toward stockpile targets. Perfect-info prices until I;
    /// explicit escrowed contract objects await carriers (E). Returns the
    /// shipment count and the units moved for the phase note — counts alone
    /// mislead once capacity is real (one full hold reads like a fifth of
    /// five drip runs).</summary>
    public static (int Shipments, double Units) MoveFreight(
        SimState state, MarketStepScratch scratch)
    {
        int shipments = ReleaseReserves(state, scratch);
        var (trades, units) = Arbitrage(state, scratch);
        Procure(state, scratch);
        return (shipments + trades, units);
    }

    /// <summary>Internal logistics, D scale: a polity whose port starved last
    /// step sells provisions reserves into that market (reserves buffer
    /// famines — economy/markets.md §Stockpiles). Reserves are polity-
    /// aggregate until wars make them spatial (H).</summary>
    private static int ReleaseReserves(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        int releases = 0;
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            int g = (int)GoodId.Provisions;
            if (pr.ReserveQty[g] <= 0) continue;
            foreach (var port in state.Ports)             // id order (P6)
            {
                if (port.OwnerActorId != pr.ActorId) continue;
                double shortfall = 0;
                foreach (var seg in state.Segments)
                    if (seg.PortId == port.Id
                        && seg.LastSubsistence < eco.ReserveReleaseTrigger)
                        shortfall += seg.Size * eco.SubsistenceUnitsPerPopPerYear
                                     * years * (1.0 - seg.LastSubsistence);
                if (shortfall <= 0) continue;
                double release = Math.Min(pr.ReserveQty[g], shortfall);
                double grade = pr.ReserveGrade[g];   // before the drain zeroes it
                pr.ReserveQty[g] -= release;
                if (pr.ReserveQty[g] <= 0) pr.ReserveGrade[g] = 0;
                Deposit(state, scratch, port.Id, pr.ActorId, g, release, grade);
                releases++;
                if (pr.ReserveQty[g] <= 0) break;
            }
        }
        return releases;
    }

    /// <summary>Arbitrage freight: shipments move cheap → dear along lanes
    /// wherever the gap clears freight + fuel + tariff costs, within the
    /// lane's shared capacity. The exporting polity's merchants front the
    /// purchase and are paid as the destination's suppliers; every cost is a
    /// conserved ledger move (fees into the source pool, tariffs to the
    /// destination polity).</summary>
    private static (int Trades, double Units) Arbitrage(
        SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int shipments = 0;
        double units = 0;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (scratch.Severed.Contains(lane.Id)) continue;
            var portA = state.Ports[lane.PortAId];
            var portB = state.Ports[lane.PortBId];
            // the posted-posture capacity: what this lane's hulls can lift
            // this epoch (a lane without freighters arbitrages nothing)
            double capacity = scratch.LaneFleetCapacity[lane.Id]
                              - scratch.LaneCapacityUsed[lane.Id];
            if (capacity <= 0) continue;
            int dist = HexGrid.Distance(portA.Hex, portB.Hex);

            for (int g = 0; g < Goods.All.Count && capacity > 0; g++)
            {
                var (src, dst) = state.Markets[lane.PortAId].Price[g]
                                 <= state.Markets[lane.PortBId].Price[g]
                    ? (portA, portB) : (portB, portA);
                var mSrc = state.Markets[src.Id];
                var mDst = state.Markets[dst.Id];
                double pSrc = mSrc.Price[g], pDst = mDst.Price[g];
                if (mSrc.Inventory[g] <= 0 || pDst <= pSrc) continue;

                var srcLevel = LegalityAt(state, src.OwnerActorId, g);
                var dstLevel = LegalityAt(state, dst.OwnerActorId, g);
                if (srcLevel == LegalityLevel.Prohibited
                    || dstLevel == LegalityLevel.Prohibited) continue;

                double freight = eco.FreightCostPerUnitPerHex * dist;
                double fuelUnits = eco.FuelPerUnitPerHex * dist;
                double fuel = fuelUnits * mSrc.Price[(int)GoodId.Fuel];
                double tariff = 0;
                if (src.OwnerActorId != dst.OwnerActorId)
                {
                    var schedule = (state.Actors[dst.OwnerActorId].Policies
                        as PolityPolicies ?? PolityPolicies.Default).TariffSchedule;
                    if (schedule.TryGetValue(g, out double rate))
                        tariff = rate * pDst;
                }
                double friction = srcLevel == LegalityLevel.Restricted
                                  || dstLevel == LegalityLevel.Restricted
                    ? eco.RestrictedFriction * pDst : 0;
                double costPerUnit = pSrc + freight + fuel + tariff + friction;
                // the exporter's realized take is the post-tax owner share of
                // the destination sale, not the sticker price — gate on that
                double dstTax = (state.Actors[dst.OwnerActorId].Policies
                    as PolityPolicies ?? PolityPolicies.Default).TaxRate;
                double expectedNet = pDst * (1.0 - dstTax)
                                     * (1.0 - eco.LaborShare);
                if (expectedNet <= costPerUnit) continue; // no profit, no hull

                var exporter = state.PolityOf(src.OwnerActorId);
                // ship what the destination will absorb beyond its stock —
                // assembled demand includes the re-export term, so hubs pull
                double absorption = Math.Max(0.0,
                    scratch.Demand[dst.Id][g] - mDst.Inventory[g]);
                double qty = Math.Min(absorption,
                    Math.Min(mSrc.Inventory[g] * eco.ExportShare, capacity));
                if (qty <= 0) continue;
                // merchants trade on working capital, like producers
                // (slice D's RunRecipe convention): the ledger may dip
                // within the step — the sale's payout lands at distribution,
                // the trade is margin-gated above, and insolvency is
                // Allocation's credit problem. Clamping to a deficit-financed
                // treasury killed every shipment in the galaxy.

                double grade = mSrc.InventoryGrade[g];
                double drawn = mSrc.Draw(g, qty);
                if (drawn <= 0) continue;
                mSrc.LastCleared[g] += drawn;
                exporter.Credits -= drawn * (pSrc + freight + fuel);
                scratch.PoolByMarket[src.Id] += drawn * (pSrc + fuel);
                // the freight fee pays whoever posted the hulls — freight
                // lines (and merchant marines) book real revenue (slice G;
                // corporations.md: unserved *profitable* lanes)
                PayHaulers(state, lane, drawn * freight);
                if (tariff > 0)
                {
                    exporter.Credits -= drawn * tariff;
                    var dstOwner = state.PolityOf(dst.OwnerActorId);
                    dstOwner.Credits += drawn * tariff;
                    dstOwner.Receipts += drawn * tariff;
                }
                if (friction > 0)
                {
                    // friction burns as fees at the destination port
                    exporter.Credits -= drawn * friction;
                    var dstOwner = state.PolityOf(dst.OwnerActorId);
                    dstOwner.Credits += drawn * friction;
                    dstOwner.Receipts += drawn * friction;
                }
                // movement is never free: traffic pulls on the fuel market —
                // and burns it physically now that hulls exist (the slice-D
                // deferral); a fuel-dry port still ships at monetized cost,
                // its fuel price carrying the scarcity
                scratch.Demand[src.Id][(int)GoodId.Fuel] += drawn * fuelUnits;
                double fuelDrawn = mSrc.Draw((int)GoodId.Fuel, drawn * fuelUnits);
                mSrc.LastCleared[(int)GoodId.Fuel] += fuelDrawn;
                Deposit(state, scratch, dst.Id, src.OwnerActorId, g, drawn, grade);
                scratch.LaneCapacityUsed[lane.Id] += drawn;
                capacity -= drawn;
                shipments++;
                units += drawn;
            }
        }
        return (shipments, units);
    }

    /// <summary>The freight fee splits across the lane's posted fleets by
    /// hull count — a conserved exporter→hauler flow (P4). The lane always
    /// has haulers when freight moved (capacity IS their hulls).</summary>
    private static void PayHaulers(SimState state, Lane lane, double fee)
    {
        if (fee <= 0) return;
        int hulls = 0;
        foreach (var fleet in state.Fleets)
            if (fleet.Posture == FleetPosture.Posted && fleet.TargetId == lane.Id)
                hulls += fleet.TotalHulls;
        if (hulls <= 0)
        {
            // shouldn't happen (capacity implies hulls), but a fee must
            // land somewhere: the source port's sovereign takes it
            var fallback = state.LedgerOf(
                state.Ports[lane.PortAId].OwnerActorId);
            fallback.Credits += fee;
            fallback.Receipts += fee;
            return;
        }
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Posted
                || fleet.TargetId != lane.Id || fleet.TotalHulls == 0) continue;
            var owner = state.LedgerOf(fleet.OwnerActorId);
            double share = fee * fleet.TotalHulls / hulls;
            owner.Credits += share;
            owner.Receipts += share;
        }
    }

    /// <summary>Polity procurement: buy toward standing stockpile targets
    /// from own markets, in actor-id then port-id then good-id order.</summary>
    private static void Procure(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            var targets = (state.Actors[pr.ActorId].Policies as PolityPolicies
                           ?? PolityPolicies.Default).StockpileTargets;
            if (targets.Count == 0) continue;
            for (int g = 0; g < Goods.All.Count; g++)     // good-id order (P6)
            {
                if (!targets.TryGetValue(g, out double target)) continue;
                double deficit = target - pr.ReserveQty[g];
                if (deficit <= 0) continue;
                foreach (var port in state.Ports)         // id order (P6)
                {
                    if (port.OwnerActorId != pr.ActorId || deficit <= 0) continue;
                    var market = state.Markets[port.Id];
                    double price = market.Price[g];
                    double qty = Math.Min(deficit,
                                          market.Inventory[g] * eco.ExportShare);
                    if (price > 0 && pr.Credits < qty * price)
                        qty = Math.Max(0, pr.Credits / price);
                    if (qty <= 0) continue;
                    double grade = market.InventoryGrade[g];
                    double drawn = market.Draw(g, qty);
                    if (drawn <= 0) continue;
                    market.LastCleared[g] += drawn;
                    pr.Credits -= drawn * price;
                    scratch.PoolByMarket[port.Id] += drawn * price;
                    double total = pr.ReserveQty[g] + drawn;
                    pr.ReserveGrade[g] = (pr.ReserveQty[g] * pr.ReserveGrade[g]
                                          + drawn * grade) / total;
                    pr.ReserveQty[g] = total;
                    deficit -= drawn;
                }
            }
        }
    }

    private static LegalityLevel LegalityAt(SimState state, int actorId, int good) =>
        (state.Actors[actorId].Policies as PolityPolicies
         ?? PolityPolicies.Default).LawCode.TryGetValue(good, out var level)
            ? level : LegalityLevel.Legal;

    // ------------------------------------------------------------------
    // Step 5 — clearing and consequences
    // ------------------------------------------------------------------

    /// <summary>Consumption satisfies band priority per good; buyers pay the
    /// (adjusted) price from their wealth into the market pool. Unmet
    /// subsistence chronicles a famine; the SoL scalar drifts toward the
    /// standard-of-living band's cleared fraction. Returns the famine count
    /// (the phase note reports it).</summary>
    public static int Clear(SimState state, MarketStepScratch scratch)
    {
        var pop = state.Config.Population;
        int years = state.Config.Sim.YearsPerEpoch;
        int famines = 0;
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var market = state.Markets[mIx];
            var port = state.Ports[market.PortId];
            foreach (var band in Bands)                   // priority order
            {
                // totals for this band at this market, against live inventory
                for (int g = 0; g < market.Price.Length; g++)
                {
                    double total = 0;
                    foreach (var r in scratch.DemandRecords)
                        if (r.MarketIndex == mIx && r.Band == band && r.Good == g)
                            total += r.Quantity;
                    if (total <= 0) continue;
                    double fraction = Math.Min(1.0, market.Inventory[g] / total);
                    if (fraction <= 0) continue;
                    foreach (var r in scratch.DemandRecords)
                    {
                        if (r.MarketIndex != mIx || r.Band != band || r.Good != g)
                            continue;
                        var seg = state.Segments[r.SegmentId];
                        double take = r.Quantity * fraction;
                        double price = market.Price[g];
                        if (price > 0 && take * price > seg.Wealth)
                            take = seg.Wealth / price;     // poverty caps the basket
                        double drawn = market.Draw(g, take);
                        if (drawn <= 0) continue;
                        double cost = drawn * price;
                        seg.Wealth -= cost;
                        scratch.PoolByMarket[mIx] += cost;
                        market.LastCleared[g] += drawn;
                        if (band == PopulationBand.Subsistence)
                            scratch.SubsistenceCleared[r.SegmentId] += drawn;
                        else if (band == PopulationBand.StandardOfLiving)
                            scratch.SoLCleared[r.SegmentId] += drawn;
                    }
                }
            }

            // consequences: famine arithmetic and the SoL drift
            double portNeed = 0, portCleared = 0;
            foreach (var seg in state.Segments)
            {
                if (seg.PortId != port.Id) continue;
                double need = scratch.SubsistenceNeed[seg.Id];
                seg.LastSubsistence = need > 0
                    ? Math.Min(1.0, scratch.SubsistenceCleared[seg.Id] / need)
                    : 1.0;
                portNeed += need;
                portCleared += Math.Min(scratch.SubsistenceCleared[seg.Id], need);
                double solNeed = scratch.SoLNeed[seg.Id];
                double solFraction = solNeed > 0
                    ? Math.Min(1.0, scratch.SoLCleared[seg.Id] / solNeed)
                    : seg.SoL;                            // no want, no news
                seg.SoL = Math.Min(1.0, Math.Max(0.0,
                    seg.SoL + (solFraction - seg.SoL) * pop.SoLDriftPerYear * years));
            }
            if (portNeed > 0 && portCleared / portNeed < pop.FamineLine)
            {
                double shortfall = 1.0 - portCleared / portNeed;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.FamineStruck,
                    new[] { port.OwnerActorId }, port.Hex,
                    Magnitude: shortfall, Valence: -1.0, EventVisibility.Regional,
                    new FamineStruckPayload(port.Id, shortfall)));
                famines++;
            }
        }
        return famines;
    }

    // ------------------------------------------------------------------
    // Revenue distribution
    // ------------------------------------------------------------------

    /// <summary>The pool of buyer payments per market: transaction tax to the
    /// port's polity, the rest to this step's suppliers pro-rata by supplied
    /// value (capped at that value — unsold goods earn nothing), each payout
    /// split labor-share to the staffing segments and remainder to the owner;
    /// surplus from carried-inventory sales accrues to the port owner. Every
    /// credit is a conserved ledger move (P4).</summary>
    public static void DistributePools(SimState state, MarketStepScratch scratch)
    {
        double laborShare = state.Config.Economy.LaborShare;
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            double pool = scratch.PoolByMarket[mIx];
            if (pool <= 0) continue;
            var port = state.Ports[state.Markets[mIx].PortId];
            var portOwner = state.PolityOf(port.OwnerActorId);
            double taxRate = (state.Actors[port.OwnerActorId].Policies
                              as PolityPolicies ?? PolityPolicies.Default).TaxRate;
            double tax = pool * taxRate;
            portOwner.Credits += tax;
            portOwner.Receipts += tax;
            double net = pool - tax;

            double totalSupplied = 0;
            foreach (var s in scratch.Supplies)
                if (s.MarketIndex == mIx) totalSupplied += s.Value;
            if (totalSupplied > 0)
            {
                double payRatio = Math.Min(1.0, net / totalSupplied);
                foreach (var s in scratch.Supplies)
                {
                    if (s.MarketIndex != mIx) continue;
                    double payout = s.Value * payRatio;
                    PayWages(state, port.Id, payout * laborShare);
                    var supplier = state.LedgerOf(s.OwnerActorId);
                    supplier.Credits += payout * (1.0 - laborShare);
                    supplier.Receipts += payout * (1.0 - laborShare);
                }
                net -= Math.Min(net, totalSupplied);
            }
            portOwner.Credits += net;   // carried-inventory sales
            portOwner.Receipts += net;
        }
    }

    // ------------------------------------------------------------------
    // Shared lookups
    // ------------------------------------------------------------------

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

    /// <summary>Shape-only skeletons (frame tests) carry no species profiles —
    /// read as the terran default.</summary>
    internal static Embodiment EmbodimentOf(SimState state, int speciesId) =>
        speciesId >= 0 && speciesId < state.Skeleton.Species.Count
            ? state.Skeleton.Species[speciesId].Embodiment
            : Embodiment.TerranAnalog;

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
                embodiment = EmbodimentOf(state, s.SpeciesId);
            }
        }
        return embodiment;
    }
}
