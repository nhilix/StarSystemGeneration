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
    /// <summary>Freight units already carried per lane id this step — the
    /// fleet-capacity stub's budget (E replaces it with posted capacity).</summary>
    public double[] LaneCapacityUsed { get; }

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
    /// <summary>Price response of demand per band: subsistence near-inelastic,
    /// standard-of-living moderate, luxury elastic (economy/markets.md §2).</summary>
    private const double SubsistenceElasticity = 0.1;
    private const double SoLElasticity = 0.5;
    private const double LuxuryElasticity = 1.3;
    /// <summary>Demand's price-response clamp — hunger doubles at best, never
    /// explodes.</summary>
    private const double ElasticFloor = 0.25, ElasticCeiling = 2.0;
    /// <summary>Drift shape: price moves by (demand/supply)^exponent, clamped
    /// by the per-year rate limit.</summary>
    private const double DriftExponent = 0.5;
    private const double PriceFloor = 0.01;
    /// <summary>Absolute ceiling as a multiple of the founding price — spikes
    /// stay legible without running away over long famines.</summary>
    private const double MaxPriceMultiple = 1000.0;
    /// <summary>Black-book margin over the open price — prohibition converts
    /// demand, it never deletes it (commodities.md legality).</summary>
    private const double BlackMarketMarkup = 2.5;
    /// <summary>Aggregate subsistence fraction below which a famine event is
    /// chronicled.</summary>
    private const double FamineThreshold = 0.95;

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

    /// <summary>Labor share to the staffing segments, pro-rata by size —
    /// household income is earned from realized revenue, not assumed
    /// (economy/markets.md §Household income). Unsold goods pay nobody.</summary>
    private static void PayWages(SimState state, int portId, double wage)
    {
        if (wage <= 0) return;
        double totalSize = 0;
        foreach (var s in state.Segments)
            if (s.PortId == portId) totalSize += s.Size;
        if (totalSize <= 0) return;
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
                        budget -= qty * price;
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
                                market.Price[(int)good] * BlackMarketMarkup;
                        }
                        double legal = qty - black;
                        if (legal <= 0) continue;
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
            PopulationBand.Subsistence => SubsistenceElasticity,
            PopulationBand.StandardOfLiving => SoLElasticity,
            _ => LuxuryElasticity,
        };
        double factor = Math.Pow(market.Price[good] / basePrice, -elasticity);
        return Math.Min(ElasticCeiling, Math.Max(ElasticFloor, factor));
    }

    /// <summary>Re-export demand (economy/markets.md §2): bids from
    /// arbitrageurs who see outbound gradients bid up a hub's price even with
    /// zero local consumption — without this term goods refuse to enter
    /// markets that don't personally want them; with it, entrepôts emerge.
    /// A pure price signal: nothing is bought here.</summary>
    public static void AddReExportDemand(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (state.SeveredLanes.Contains(lane.Id)) continue;
            var a = state.Ports[lane.PortAId];
            var b = state.Ports[lane.PortBId];
            double laneFlow = LaneMath.Capacity(a, b) * years * eco.ReExportWeight;
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
    /// opportunities.</summary>
    public static void AdjustPrices(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        double cap = Math.Exp(eco.PriceDriftMaxPerYear
                              * state.Config.Sim.YearsPerEpoch);
        const double eps = 1e-9;
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var market = state.Markets[mIx];
            for (int g = 0; g < market.Price.Length; g++)
            {
                double demand = scratch.Demand[mIx][g];
                double supply = market.Inventory[g];
                if (demand <= eps && supply <= eps) continue;   // dormant good
                double factor = Math.Pow((demand + eps) / (supply + eps),
                                         DriftExponent);
                factor = Math.Min(cap, Math.Max(1.0 / cap, factor));
                double ceiling = Market.InitialPrice(eco, (GoodId)g) * MaxPriceMultiple;
                market.Price[g] = Math.Min(ceiling,
                    Math.Max(PriceFloor, market.Price[g] * factor));
            }
        }
    }

    // ------------------------------------------------------------------
    // Step 4 — freight moves
    // ------------------------------------------------------------------

    /// <summary>Share of a market's stock arbitrage may lift per step —
    /// damping against flow oscillation.</summary>
    private const double ExportShare = 0.5;
    /// <summary>Restricted goods carry a per-unit friction cost (permits,
    /// inspections, seizure risk) — evadable at margin cost by design.</summary>
    private const double RestrictedFriction = 0.5;
    /// <summary>Subsistence fraction below which a polity releases reserves
    /// into the starving port's market.</summary>
    private const double ReserveReleaseTrigger = 0.9;

    /// <summary>Step 4 — freight (economy/markets.md §4), three generators in
    /// deterministic order, all within lane capacity (the fleet-capacity stub
    /// E replaces): reserve release to starving ports (internal logistics —
    /// markets see only the endpoint), lane arbitrage on price gaps net of
    /// freight + fuel + tariffs with legality at both ends, and polity
    /// procurement toward stockpile targets. Perfect-info prices until I;
    /// explicit escrowed contract objects await carriers (E). Returns the
    /// shipment count for the phase note.</summary>
    public static int MoveFreight(SimState state, MarketStepScratch scratch)
    {
        int shipments = 0;
        shipments += ReleaseReserves(state, scratch);
        shipments += Arbitrage(state, scratch);
        Procure(state, scratch);
        return shipments;
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
                    if (seg.PortId == port.Id && seg.LastSubsistence < ReserveReleaseTrigger)
                        shortfall += seg.Size * eco.SubsistenceUnitsPerPopPerYear
                                     * years * (1.0 - seg.LastSubsistence);
                if (shortfall <= 0) continue;
                double release = Math.Min(pr.ReserveQty[g], shortfall);
                pr.ReserveQty[g] -= release;
                if (pr.ReserveQty[g] <= 0) pr.ReserveGrade[g] = 0;
                Deposit(state, scratch, port.Id, pr.ActorId, g, release,
                        pr.ReserveGrade[g]);
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
    private static int Arbitrage(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        int shipments = 0;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (state.SeveredLanes.Contains(lane.Id)) continue;
            var portA = state.Ports[lane.PortAId];
            var portB = state.Ports[lane.PortBId];
            double capacity = LaneMath.Capacity(portA, portB) * years
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
                    ? RestrictedFriction * pDst : 0;
                double costPerUnit = pSrc + freight + fuel + tariff + friction;
                if (pDst <= costPerUnit) continue;        // no profit, no hull

                var exporter = state.PolityOf(src.OwnerActorId);
                // ship what the destination will absorb beyond its stock —
                // assembled demand includes the re-export term, so hubs pull
                double absorption = Math.Max(0.0,
                    scratch.Demand[dst.Id][g] - mDst.Inventory[g]);
                double qty = Math.Min(absorption,
                    Math.Min(mSrc.Inventory[g] * ExportShare, capacity));
                if (costPerUnit > 0 && exporter.Credits < qty * costPerUnit)
                    qty = Math.Max(0, exporter.Credits / costPerUnit);
                if (qty <= 0) continue;

                double grade = mSrc.InventoryGrade[g];
                double drawn = mSrc.Draw(g, qty);
                if (drawn <= 0) continue;
                mSrc.LastCleared[g] += drawn;
                exporter.Credits -= drawn * (pSrc + freight + fuel);
                scratch.PoolByMarket[src.Id] += drawn * (pSrc + freight + fuel);
                if (tariff > 0)
                {
                    exporter.Credits -= drawn * tariff;
                    state.PolityOf(dst.OwnerActorId).Credits += drawn * tariff;
                }
                if (friction > 0)
                {
                    // friction burns as fees at the destination port
                    exporter.Credits -= drawn * friction;
                    state.PolityOf(dst.OwnerActorId).Credits += drawn * friction;
                }
                // movement is never free: traffic pulls on the fuel market
                scratch.Demand[src.Id][(int)GoodId.Fuel] += drawn * fuelUnits;
                Deposit(state, scratch, dst.Id, src.OwnerActorId, g, drawn, grade);
                scratch.LaneCapacityUsed[lane.Id] += drawn;
                capacity -= drawn;
                shipments++;
            }
        }
        return shipments;
    }

    /// <summary>Polity procurement: buy toward standing stockpile targets
    /// from own markets, in actor-id then port-id then good-id order.</summary>
    private static void Procure(SimState state, MarketStepScratch scratch)
    {
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
                    double qty = Math.Min(deficit, market.Inventory[g] * ExportShare);
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
            if (portNeed > 0 && portCleared / portNeed < FamineThreshold)
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
                    state.PolityOf(s.OwnerActorId).Credits
                        += payout * (1.0 - laborShare);
                }
                net -= Math.Min(net, totalSupplied);
            }
            portOwner.Credits += net;   // carried-inventory sales
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
