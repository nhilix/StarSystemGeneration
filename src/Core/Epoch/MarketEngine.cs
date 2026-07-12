using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>One buyer's banded want at one market — recorded when the
/// port posts its aggregate band bids, consumed to apportion the fills
/// (and the refunds) back to the contributing segments.</summary>
public sealed record DemandRecord(
    int MarketIndex, int SegmentId, PopulationBand Band, int Good, double Quantity);

/// <summary>One aggregate band bid the port posted on its segments'
/// behalf — matching routes its fills to consumption and its unfilled
/// escrow back to the segments (contract-economy spec §2).</summary>
public sealed record BandBid(MarketOrder Order, int MarketIndex,
                             PopulationBand Band, double PostedQty);

/// <summary>Step-transient market bookkeeping — never state, never
/// serialized (P6: transients are not state). Holds the famine arithmetic,
/// the lane-capacity budget, and the routing maps that tie this step's
/// posted buy orders back to their posters.</summary>
public sealed class MarketStepScratch
{
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
    /// <summary>This step's posted buys, by poster kind — matching routes
    /// fills through these, the end-of-step sweep routes the refunds.</summary>
    public List<BandBid> BandBids { get; } = new List<BandBid>();
    public List<(MarketOrder Order, Project Project)> ProjectBids { get; }
        = new List<(MarketOrder, Project)>();
    public List<(MarketOrder Order, PolityRecord Polity, Port Port)>
        ProcureBids { get; }
        = new List<(MarketOrder, PolityRecord, Port)>();
    /// <summary>Relay (entrepôt) bids — B1 bridge machinery: fills go
    /// straight back on sale at the hub, refunds to the sovereign.</summary>
    public List<(MarketOrder Order, int PortId)> RelayBids { get; }
        = new List<(MarketOrder, int)>();
    /// <summary>Consumption that lifts asks directly (recipe inputs,
    /// upkeep, fleet supply, research feedstock) is invisible to the book —
    /// this is its PRICE SIGNAL, per market per good, fed to the reference
    /// drift only (never to the bridge's absorption, which reads real
    /// bids). The old demand-assembly formulas live on here.</summary>
    public double[][] SignalDemand { get; }

    public MarketStepScratch(SimState state)
    {
        SignalDemand = new double[state.Markets.Count][];
        for (int i = 0; i < SignalDemand.Length; i++)
            SignalDemand[i] = new double[Goods.All.Count];
        SubsistenceNeed = new double[state.Segments.Count];
        SubsistenceCleared = new double[state.Segments.Count];
        SoLNeed = new double[state.Segments.Count];
        SoLCleared = new double[state.Segments.Count];
        LaneCapacityUsed = new double[state.Lanes.Count];
        LaneFleetCapacity = new double[state.Lanes.Count];
        foreach (var lane in state.Lanes)                 // id order (P6)
            LaneFleetCapacity[lane.Id] = LaneMath.IsLive(state, lane)
                ? FleetOps.PostedCapacity(state, lane) : 0.0;
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

    /// <summary>True once the construction project delivered its years —
    /// commissioning is a project completion, never date arithmetic
    /// (spec §1).</summary>
    public static bool IsActive(SimState state, Facility f) =>
        f.CommissionedYear >= 0;

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

            double machineryGrade = BookOps.AskGrade(state, mIx,
                (int)GoodId.Machinery);
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
                    BookOps.PostSupply(state, mIx, f.OwnerActorId, (int)good,
                                       capacity, Potentials.RawGrade(terrain));
                else
                    RunRecipe(state, mIx, f, recipes, capacity);
            }
        }
    }

    /// <summary>Convert inputs to output through the best recipe the owner's
    /// Industrial tier allows (slice G: per-polity tech, the stub retired):
    /// advanced variants first (higher grade base), falling back by
    /// producible quantity. Inputs are lifted off the local asks, cheapest
    /// first, paid from the owner's working capital — the sellers are real
    /// now, and they are paid at their quotes.</summary>
    private static void RunRecipe(SimState state, int mIx, Facility f,
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
                    BookOps.AskQty(state, mIx, (int)q.Good) / q.Quantity);
            // quantity × grade base: an advanced variant wins on quality only
            // while its inputs allow real volume — a drop of the good stuff
            // never beats a vat of the standard issue
            double worth = byInputs * r.GradeBase;
            if (pick == null || worth > pickWorth)
            { pick = r; pickQty = byInputs; pickWorth = worth; }
        }
        if (pick == null || pickQty <= 0) return;

        // inputs are bought on working capital: the owner's ledger may dip
        // within the step — sales land as its own sell orders' fills, and
        // insolvency is Allocation's credit problem
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
            var (_, grade0, cost0) = BookOps.LiftAsks(state, mIx,
                (int)q.Good, qty * q.Quantity, budget: double.MaxValue);
            owner.Credits -= cost0;
            gradeSum += grade0 * q.Quantity;
            weightSum += q.Quantity;
        }

        double meanInputGrade = weightSum > 0 ? gradeSum / weightSum : 0.5;
        double grade = Grades.Output(pick, meanInputGrade, f.Tier, techTier);
        BookOps.PostSupply(state, mIx, f.OwnerActorId, (int)pick.Output,
                           qty, grade);
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

    /// <summary>The port posts its bands' bids (contract-economy spec §2):
    /// C's normalized profiles × the config's absolute per-capita rates ×
    /// segment size, embodiment-modulated, price-elastic against the
    /// reference price, with the organic baseline offsetting provisions
    /// (self-supply — unserviced systems are poor, not starving). One
    /// aggregate buy order per (port, good, band), escrowed from segment
    /// wealth pro-rata; the DemandRecords remember whose want it was, so
    /// fills and refunds apportion back. Bands express priority through
    /// PRICE: subsistence bids over fresh asks, comfort at reference,
    /// luxury only into gluts. Prohibition converts demand into the black
    /// book instead of deleting it.</summary>
    public static void PostBandBids(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        var want = new double[Goods.All.Count];           // per band, reused
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var market = state.Markets[mIx];
            Array.Clear(market.BlackBookDemand, 0, market.BlackBookDemand.Length);
            Array.Clear(market.BlackBookPrice, 0, market.BlackBookPrice.Length);
            var port = state.Ports[market.PortId];
            var law = (state.Actors[port.OwnerActorId].Policies as PolityPolicies
                       ?? PolityPolicies.Default).LawCode;
            double biosphere = Potentials.Biosphere(FieldsAt(state, port.Hex));

            foreach (var band in Bands)
            {
                double bidRatio = band switch
                {
                    PopulationBand.Subsistence => eco.SubsistenceBidPremium,
                    PopulationBand.StandardOfLiving => eco.SoLBidRatio,
                    _ => eco.LuxuryBidRatio,
                };
                Array.Clear(want, 0, want.Length);
                foreach (var seg in state.Segments)       // id order (P6)
                {
                    if (seg.PortId != port.Id || seg.Size <= 0) continue;
                    var embodiment = EmbodimentOf(state, seg.SpeciesId);
                    // demand is want backed by ability to pay: poverty
                    // reads as glut, not as a frozen high price
                    double budget = Math.Max(0.0, seg.Wealth);
                    // self-supply is embodiment-relative like the need it
                    // offsets: a lithic farms as little as it eats
                    double baseline =
                        Production.OrganicBaseline(seg.Size, biosphere) * years
                        * DemandProfiles.SubsistenceScale(embodiment);
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
                        double bid = market.Price[(int)good] * bidRatio;
                        if (bid > 0 && qty * bid > budget)
                            qty = budget / bid;           // poverty caps the want
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
                        // the escrow leaves the segment NOW; unfilled bids
                        // refund at the step's end (black-book wants go
                        // unserved until smuggling exists, H). The budget
                        // is one purse across the profile's goods — each
                        // escrow shrinks what the next good may cap to
                        seg.Wealth -= legal * bid;
                        budget = Math.Max(0.0, budget - legal * bid);
                        want[(int)good] += legal;
                        scratch.DemandRecords.Add(new DemandRecord(
                            mIx, seg.Id, band, (int)good, legal));
                    }
                }
                for (int g = 0; g < want.Length; g++)
                {
                    if (want[g] <= 0) continue;
                    double bid = market.Price[g] * bidRatio;
                    var order = OrderOps.PostBuy(state, port.OwnerActorId,
                        mIx, g, want[g], bid, state.WorldYear);
                    scratch.BandBids.Add(new BandBid(order, mIx, band,
                                                     want[g]));
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

    /// <summary>Every in-flight project posts its basket as REAL escrowed
    /// bids at its site (contract-economy spec §2) — the construction pull
    /// is literal now: a build boom is resting bids that raise prints and
    /// attract freight, and the goods are PAID FOR from the funder's
    /// treasury. A gate pair posts half at each end. Fills land in the
    /// project's laydown yard (DeliveredQty) for Feed; unfilled escrow
    /// refunds to the treasury at the step's end.</summary>
    public static void PostProjectBids(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var p in state.Projects)                 // id order (P6)
        {
            // an expedition's basket is cargo already aboard, not demand
            if (!p.InFlight || p.Kind == ProjectKind.ColonyExpedition)
                continue;
            // the pull tapers to the remaining work: a project a year from
            // done bids for a year's basket, not the whole span's
            double horizon = Math.Min(years,
                Math.Max(0.0, p.YearsRequired - p.YearsDelivered));
            if (horizon <= 0) continue;
            bool pair = p.Kind == ProjectKind.GatePair && p.TargetId >= 0;
            Span<int> ends = pair
                ? stackalloc int[2] { state.Lanes[p.TargetId].PortAId,
                                      state.Lanes[p.TargetId].PortBId }
                : stackalloc int[1] { p.PortId };
            double share = pair ? 0.5 : 1.0;
            foreach (int end in ends)
            {
                var market = state.Markets[end];
                for (int g = 0; g < p.PerYearBasket.Length; g++)
                {
                    if (p.PerYearBasket[g] <= 0) continue;
                    double want = share * (p.PerYearBasket[g] * horizon
                                           - p.DeliveredQty[g]);
                    if (want <= 1e-9) continue;
                    double bid = Math.Max(eco.PriceFloor,
                        market.Price[g] * eco.ProjectBidPremium);
                    // the treasury escrows the bid — goods cost money now
                    double affordable = ProjectOps.TreasuryAvailable(state, p);
                    double qty = Math.Min(want, affordable / bid);
                    if (qty <= 1e-9) continue;
                    ProjectOps.SpendTreasury(state, p, qty * bid);
                    var order = OrderOps.PostBuy(state, p.FunderActorId,
                        end, g, qty, bid, state.WorldYear);
                    scratch.ProjectBids.Add((order, p));
                }
            }
        }
    }

    /// <summary>Polity procurement posts bids (contract-economy spec §2):
    /// each own port bids toward ITS share of the standing target at the
    /// reference price, escrowed from the reserve treasury, bounded by
    /// built capacity. Fills bank into the port's stockpile; unfilled
    /// escrow refunds at the step's end.</summary>
    public static void PostProcurementBids(SimState state,
                                           MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            var targets = (state.Actors[pr.ActorId].Policies as PolityPolicies
                           ?? PolityPolicies.Default).StockpileTargets;
            if (targets.Count == 0) continue;
            int ownPorts = 0;
            foreach (var port in state.Ports)
                if (port.OwnerActorId == pr.ActorId) ownPorts++;
            if (ownPorts == 0) continue;
            foreach (var port in state.Ports)             // id order (P6)
            {
                if (port.OwnerActorId != pr.ActorId) continue;
                double cap = StockCapacityAt(state, port);
                var market = state.Markets[port.Id];
                for (int g = 0; g < Goods.All.Count; g++) // good-id order (P6)
                {
                    if (!targets.TryGetValue(g, out double target)) continue;
                    double want = Math.Min(target / ownPorts, cap)
                                  - port.StockQty[g];
                    if (want <= 0) continue;
                    double bid = Math.Max(eco.PriceFloor, market.Price[g]);
                    double qty = Math.Min(want,
                        Math.Max(0, pr.ReservePoints) / bid);
                    if (qty <= 1e-9) continue;
                    pr.ReservePoints -= qty * bid;
                    var order = OrderOps.PostBuy(state, pr.ActorId, port.Id,
                        g, qty, bid, state.WorldYear);
                    scratch.ProcureBids.Add((order, pr, port));
                }
            }
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

    /// <summary>The consumption SIGNAL (contract-economy carry-over of the
    /// old demand assembly): consumers who lift asks directly — recipe
    /// inputs at planned utilization, facility upkeep, fleet upkeep and
    /// wartime rations, naval procurement, research feedstock — register
    /// their per-step want here so the reference drift can price scarcity
    /// BEFORE the lift happens. Signal only: no escrow, no absorption —
    /// the bridge still moves goods against real resting bids, and the
    /// relay bids walk them down the price gradients this signal builds.</summary>
    public static void AddConsumptionSignal(SimState state,
                                            MarketStepScratch scratch)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        // industry: recipe inputs at the same price-throttled volume
        // production will attempt, plus catalog upkeep
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
                        scratch.SignalDemand[mIx][(int)q.Good]
                            += q.Quantity * potential;
                    break;                                // first viable variant
                }
            }
            double upkeepScale = Production.TierCostFactor(f.Tier) * years;
            foreach (var q in def.UpkeepPerYear)
                scratch.SignalDemand[mIx][(int)q.Good]
                    += q.Quantity * upkeepScale;
        }

        // fleets: upkeep and wartime rations at the home port
        var fleetKnobs = state.Config.Fleet;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.TotalHulls == 0) continue;
            // the signal points where the DRAW happens: a war-stationed
            // force victuals at its forward depot (spec §4), so that is
            // the reference price its burn should spike — not the home
            // port a sector behind the line (review wave, finding 10)
            int at = fleet.Posture is FleetPosture.Blockade
                         or FleetPosture.Expedition
                ? FleetOps.NearestOwnedPortId(state, fleet.OwnerActorId,
                                              fleet.Hex)
                : fleet.HomePortId;
            if (at < 0 || at >= state.Markets.Count) continue;
            double posture = fleet.Posture == FleetPosture.Reserve
                ? fleetKnobs.ReserveUpkeepFactor : 1.0;
            bool atWar = WarOps.AtWar(state, fleet.OwnerActorId);
            foreach (var g in fleet.Hulls)                // design-id order
            {
                var design = state.Designs[g.DesignId];
                var sheet = DesignRegistry.SheetOf(state, design);
                double draw = sheet[ShipStat.Upkeep] * g.Count
                              * fleetKnobs.UpkeepUnitsPerPointPerYear * years
                              * posture;
                scratch.SignalDemand[at][(int)GoodId.Fuel]
                    += draw * fleetKnobs.UpkeepFuelShare;
                int rest = ShipCatalog.IsWarship(design.Role)
                    ? (int)GoodId.Armaments : (int)GoodId.ShipComponents;
                scratch.SignalDemand[at][rest]
                    += draw * (1 - fleetKnobs.UpkeepFuelShare);
                if (atWar && ShipCatalog.IsWarship(design.Role))
                    scratch.SignalDemand[at][(int)GoodId.Provisions]
                        += g.Count * posture * years
                           * state.Config.War.RationsPerHullPerYear;
            }
        }

        // naval procurement and research feedstock at their landing ports
        double hullValue = DesignMath.ComponentsPerHull(fleetKnobs,
                ShipSize.Medium)
            * Market.InitialPrice(state.Config.Economy, GoodId.ShipComponents);
        var tech = state.Config.Tech;
        foreach (var pr in state.Polities)                // actor-id order (P6)
        {
            var actor = state.Actors[pr.ActorId];
            if (!actor.Entered) continue;
            if (pr.MilitaryPoints >= hullValue)
            {
                int at = YardPortOf(state, pr.ActorId);
                if (at >= 0)
                {
                    double surge = WarOps.AtWar(state, pr.ActorId)
                        ? state.Config.War.MobilizationFactor : 1.0;
                    scratch.SignalDemand[at][(int)GoodId.ShipComponents]
                        += fleetKnobs.MilitaryPullComponents * surge;
                    if (surge > 1.0)
                        scratch.SignalDemand[at][(int)GoodId.Armaments]
                            += fleetKnobs.MilitaryPullComponents * (surge - 1.0);
                }
            }
            var budget = (actor.Policies as PolityPolicies
                          ?? PolityPolicies.Default).Budget;
            if (budget.Research > 0)
            {
                int capital = -1;
                foreach (var port in state.Ports)         // id order (P6)
                    if (port.OwnerActorId == pr.ActorId)
                    { capital = port.Id; break; }
                if (capital >= 0)
                {
                    scratch.SignalDemand[capital][(int)GoodId.RefinedExotics]
                        += tech.ResearchPullExotics;
                    scratch.SignalDemand[capital][(int)GoodId.Compute]
                        += tech.ResearchPullCompute;
                }
            }
        }
    }

    /// <summary>Relay bids — funded re-export staging, KEPT past B2 (a
    /// flagged deviation: spread runs are single-lane, so hop-by-hop
    /// diffusion still needs the cheap end's sovereign staging goods until
    /// multi-hop actor runs land — the C18 carried flag). Wherever a live,
    /// hulled lane shows a price gradient, the cheap end's sovereign bids
    /// at its own reference price; the fills go straight back on sale at
    /// the hub (RouteFill), so the NEXT step's spread run carries them a
    /// hop onward. Without this, goods refuse to cross more than one hop
    /// and every frontier project starves; with it, entrepôts emerge.</summary>
    public static void PostRelayBids(SimState state, MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (scratch.Severed.Contains(lane.Id)) continue;
            double laneFlow = scratch.LaneFleetCapacity[lane.Id]
                              * eco.ReExportWeight;
            if (laneFlow <= 0) continue;
            var mA = state.Markets[lane.PortAId];
            var mB = state.Markets[lane.PortBId];
            for (int g = 0; g < mA.Price.Length; g++)
            {
                double pa = mA.Price[g], pb = mB.Price[g];
                if (pa <= 0 || pb <= 0 || pa == pb) continue;
                int hub = pb > pa ? lane.PortAId : lane.PortBId;
                double qty = laneFlow * Math.Abs(pb - pa) / Math.Max(pa, pb);
                var owner = state.PolityOf(state.Ports[hub].OwnerActorId);
                double bid = Math.Max(eco.PriceFloor,
                                      state.Markets[hub].Price[g]);
                qty = Math.Min(qty, Math.Max(0.0, owner.Credits) / bid);
                if (qty <= 1e-9) continue;
                owner.Credits -= qty * bid;               // real escrow
                var order = OrderOps.PostBuy(state, owner.ActorId, hub, g,
                    qty, bid, state.WorldYear);
                scratch.RelayBids.Add((order, hub));
            }
        }
    }

    // ------------------------------------------------------------------
    // Freight moves (the B1 bridge)
    // ------------------------------------------------------------------

    /// <summary>SPREAD RUNS (contract-economy spec §3 — the B1 bridge and
    /// its phantom "exporting polity merchants" are dead): each POSTED
    /// freight fleet's owner — corporation or merchant marine — works its
    /// own lane, buying the cheaper end's asks WITH ITS OWN CAPITAL and
    /// shipping toward the dearer end's resting bids, plus the speculative
    /// run when the far reference clears delivered cost (no reservation —
    /// cargo sells into whatever book exists on arrival, and a dead spread
    /// is the owner's loss). Both lane ends are one hop from the fleet's
    /// station: prices read fresh (P3 — multi-hop runs on perceived books
    /// are the flagged next pass). Freight fees died with the phantom:
    /// the hauler IS the trader. Returns shipments and units.</summary>
    public static (int Shipments, double Units) MoveFreight(
        SimState state, MarketStepScratch scratch)
    {
        int releases = ReleaseReserves(state);
        var eco = state.Config.Economy;
        int shipments = 0;
        double units = 0;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Posted
                || fleet.TargetId < 0 || fleet.TotalHulls == 0) continue;
            var lane = state.Lanes[fleet.TargetId];
            if (scratch.Severed.Contains(lane.Id)
                || !LaneMath.IsLive(state, lane)) continue;
            var portA = state.Ports[lane.PortAId];
            var portB = state.Ports[lane.PortBId];
            int dist = HexGrid.Distance(portA.Hex, portB.Hex);
            double speed = LaneMath.TransitSpeed(state, lane);
            int years = state.Config.Sim.YearsPerEpoch;
            // this fleet's own lift for the step
            double capacity = 0;
            foreach (var g0 in fleet.Hulls)               // design-id order
                capacity += FleetMath.PostedCapacityPerEpoch(
                    state.Config.Fleet,
                    DesignRegistry.SheetOf(state, state.Designs[g0.DesignId]),
                    g0.Count, speed, dist, years) * fleet.Readiness;
            var trader = state.LedgerOf(fleet.OwnerActorId);

            for (int g = 0; g < Goods.All.Count && capacity > 1e-9; g++)
            {
                double askA = BookOps.BestAsk(state, lane.PortAId, g);
                double askB = BookOps.BestAsk(state, lane.PortBId, g);
                if (askA == double.MaxValue && askB == double.MaxValue)
                    continue;
                var (src, dst) = askA <= askB ? (portA, portB)
                                              : (portB, portA);
                double pSrc = Math.Min(askA, askB);
                var mDst = state.Markets[dst.Id];

                var srcLevel = LegalityAt(state, src.OwnerActorId, g);
                var dstLevel = LegalityAt(state, dst.OwnerActorId, g);
                if (srcLevel == LegalityLevel.Prohibited
                    || dstLevel == LegalityLevel.Prohibited) continue;

                double fuelUnits = eco.FuelPerUnitPerHex * dist;
                double fuel = fuelUnits
                    * state.Markets[src.Id].Price[(int)GoodId.Fuel];
                // the dst-side gate's owner prices the crossing: corp toll,
                // customs at a foreign polity gate, free through your own
                // (lane-economics spec §4)
                double tariff = LaneFees.CrossingFeePerUnit(state, lane,
                    dst.Id, g, mDst.Price[g], fleet.OwnerActorId, out int feeTo);
                double friction = srcLevel == LegalityLevel.Restricted
                                  || dstLevel == LegalityLevel.Restricted
                    ? eco.RestrictedFriction * mDst.Price[g] : 0;
                double costPerUnit = pSrc + fuel + tariff + friction;
                // the trader's realized take is the post-tax owner share
                // of the destination sale — only bids above break-even count
                double dstTax = (state.Actors[dst.OwnerActorId].Policies
                    as PolityPolicies ?? PolityPolicies.Default).TaxRate;
                double bidFloor = costPerUnit
                    / Math.Max(1e-9, (1.0 - dstTax) * (1.0 - eco.LaborShare));
                double absorption = BookOps.BidDepthAbove(state, dst.Id, g,
                                                          bidFloor);
                // the speculative run: when the dear end's reference clears
                // delivered cost, cargo sails at the trader's risk — the
                // unsold surplus is what disciplines a cut-off price
                double dstRef = mDst.Price[g] * Math.Max(1e-9,
                    (1.0 - dstTax) * (1.0 - eco.LaborShare));
                double spec = dstRef > costPerUnit
                    ? capacity * eco.ReExportWeight
                      * (dstRef - costPerUnit) / dstRef
                    : 0.0;
                double qty = Math.Min(Math.Max(absorption, spec),
                    Math.Min(capacity,
                        BookOps.AskQty(state, src.Id, g) * eco.ExportShare));
                // a CORP trader fronts the whole run — goods, fuel, tolls —
                // from its own free capital (review wave: an unbounded
                // front dipped corps thousands negative into same-step
                // bankrupt-dissolution). The sovereign marine is different
                // plumbing, not different virtue: by MoveFreight the
                // treasury sits escrowed in the state's own procurement
                // and relay bids and refunds at this step's clear, so its
                // ledger reads empty mid-step — the state hauls on that
                // credit line and Allocation owns its solvency
                double budget = state.CorporationOf(fleet.OwnerActorId) != null
                    ? Math.Max(0.0, trader.Credits) : double.MaxValue;
                qty = Math.Min(qty, budget / Math.Max(1e-9, costPerUnit));
                if (qty <= 1e-9) continue;

                var (drawn, grade, cost) = BookOps.LiftAsks(state, src.Id,
                    g, qty, budget);
                if (drawn <= 0) continue;
                trader.Credits -= cost;
                if (tariff > 0 && feeTo >= 0)
                {
                    trader.Credits -= drawn * tariff;
                    var collector = state.LedgerOf(feeTo);
                    collector.Credits += drawn * tariff;
                    collector.Receipts += drawn * tariff;
                }
                if (friction > 0)
                {
                    // friction burns as fees at the destination port
                    trader.Credits -= drawn * friction;
                    var dstOwner = state.PolityOf(dst.OwnerActorId);
                    dstOwner.Credits += drawn * friction;
                    dstOwner.Receipts += drawn * friction;
                }
                // movement is never free: the fuel burn is bought off the
                // source book at real asks — a fuel-dry port's crawl shows
                // in its fuel prints
                var (_, _, fuelCost) = BookOps.LiftAsks(state, src.Id,
                    (int)GoodId.Fuel, drawn * fuelUnits,
                    budget: double.MaxValue);
                trader.Credits -= fuelCost;
                // transit time (spec §4b): a hop inside the step posts the
                // cargo at the destination now (sub-step blur, sells into
                // whatever book exists); a longer haul rides a shipment —
                // the TRADER owns the cargo and the arrival asks
                ShipmentOps.DispatchVia(state, fleet.OwnerActorId,
                    ShipmentChannel.Freight, src.Id, dst.Id,
                    new[] { lane.Id },
                    new[] { ShipmentOps.LaneLegYears(state, lane) },
                    new[] { (g, drawn, grade) }, scratch);
                scratch.LaneCapacityUsed[lane.Id] += drawn;
                capacity -= drawn;
                shipments++;
                units += drawn;
            }
        }
        return (shipments + releases, units);
    }

    /// <summary>Internal logistics, located (spec §4b): a port whose people
    /// starved last step releases ITS OWN provisions stockpile onto its own
    /// book as the sovereign's sell order — no polity pool, no teleport; a
    /// bare frontier larder is bare until a shipment lands.</summary>
    private static int ReleaseReserves(SimState state)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        int releases = 0;
        int g = (int)GoodId.Provisions;
        foreach (var port in state.Ports)                 // id order (P6)
        {
            if (port.StockQty[g] <= 0) continue;
            double shortfall = 0;
            foreach (var seg in state.Segments)
                if (seg.PortId == port.Id
                    && seg.LastSubsistence < eco.ReserveReleaseTrigger)
                    shortfall += seg.Size * eco.SubsistenceUnitsPerPopPerYear
                                 * years * (1.0 - seg.LastSubsistence);
            if (shortfall <= 0) continue;
            double grade = port.StockGrade[g];   // before the drain zeroes it
            double release = port.DrawStock(g, shortfall);
            BookOps.PostSupply(state, port.Id, port.OwnerActorId, g,
                               release, grade);
            releases++;
        }
        return releases;
    }

    /// <summary>Built stockpile capacity per good at a port (spec §4b): the
    /// port's own tier banks a little; active Depot tiers bank a lot — deep
    /// larders are constructed, not assumed.</summary>
    public static double StockCapacityAt(SimState state, Port port)
    {
        var eco = state.Config.Economy;
        return port.Tier * eco.StockCapPerPortTier
               + ActiveDepotTiersAt(state, port) * eco.StockCapPerDepotTier;
    }

    /// <summary>The port owner's active Depot tiers attached to this port's
    /// market — the larder's depth (capacity) and preservation (decay cut)
    /// both key off it. One derivation for the sim and the K3 panel.</summary>
    public static int ActiveDepotTiersAt(SimState state, Port port)
    {
        int tiers = 0;
        foreach (var f in state.Facilities)               // id order (P6)
            if (f.TypeId == (int)InfraTypeId.Depot
                && f.OwnerActorId == port.OwnerActorId
                && IsActive(state, f)
                && AttachedMarketIndex(state, f) == port.Id)
                tiers += f.Tier;
        return tiers;
    }

    /// <summary>Perishability multiplier on the durable stockpile decay
    /// rate (spec §4b): provisions rot, alloys do not. Extracted at K3 so
    /// the sim (Phases stockpile decay) and the market panel's larder
    /// readout share one derivation.</summary>
    public static double StockPerishFactor(GoodId good) => good switch
    {
        GoodId.Provisions => 10.0,
        GoodId.Organics => 5.0,
        GoodId.Medicine => 3.0,
        _ => 1.0,
    };

    private static LegalityLevel LegalityAt(SimState state, int actorId, int good) =>
        (state.Actors[actorId].Policies as PolityPolicies
         ?? PolityPolicies.Default).LawCode.TryGetValue(good, out var level)
            ? level : LegalityLevel.Legal;

    // ------------------------------------------------------------------
    // Matching, fill routing, and consequences
    // ------------------------------------------------------------------

    /// <summary>Cross every port's book (port-id order, P6), route the
    /// fills to their posters — band fills are consumed and apportioned
    /// back to segments pro-rata, project fills land in the laydown yard,
    /// procurement fills bank into the port stockpile — then cancel the
    /// step's remaining posted buys, refunding escrow where it came from
    /// (segments / funder treasury / reserve treasury). Famine and SoL
    /// consequences derive from FILL fractions (spec §2 step 5). Returns
    /// the famine count for the phase note.</summary>
    public static int MatchAndClear(SimState state, MarketStepScratch scratch)
    {
        // the reference price drifts on the BOOK'S imbalance — the old
        // rate-limited, tick-honest drift (P7), fed by the step's posted
        // bids PLUS the consumption signal (a flow, generation-normalized)
        // against the resting asks (a stock), snapshotted BEFORE matching
        // so tick granularity cannot skew it: a blockade's hungry bids ARE
        // the spike, a glut's resting asks ARE the crash.
        DriftReferencePrices(state, scratch);

        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var fills = OrderOps.MatchPort(state, mIx);
            foreach (var fill in fills)
                RouteFill(state, scratch, fill);
        }

        // the step's posted buys retire: unfilled escrow goes home
        foreach (var bb in scratch.BandBids)
        {
            double refund = OrderOps.CancelBuy(state, bb.Order);
            if (refund <= 0) continue;
            RefundSegments(state, scratch, bb, refund);
        }
        foreach (var (order, project) in scratch.ProjectBids)
        {
            double refund = OrderOps.CancelBuy(state, order);
            if (refund > 0) ProjectOps.RefundTreasury(state, project, refund);
        }
        foreach (var (order, pr, _) in scratch.ProcureBids)
        {
            double refund = OrderOps.CancelBuy(state, order);
            if (refund > 0) pr.ReservePoints += refund;
        }
        foreach (var (order, _) in scratch.RelayBids)
        {
            double refund = OrderOps.CancelBuy(state, order);
            if (refund > 0)
                state.LedgerOf(order.OwnerActorId).Credits += refund;
        }

        // consequences: famine arithmetic and the SoL drift
        var pop = state.Config.Population;
        int years = state.Config.Sim.YearsPerEpoch;
        int famines = 0;
        foreach (var port in state.Ports)                 // id order (P6)
        {
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

    /// <summary>The reference price per (port, good) drifts on the book as
    /// posted: bid quantity (flow, normalized to a generation's worth —
    /// P7) over resting ask quantity (stock), through the same
    /// exponent/rate-clamp/floor/ceiling the old price drift used.
    /// Dormant goods (no book either side) hold their price.</summary>
    private static void DriftReferencePrices(SimState state,
                                             MarketStepScratch scratch)
    {
        var eco = state.Config.Economy;
        double cap = Math.Exp(eco.PriceDriftMaxPerYear
                              * state.Config.Sim.YearsPerEpoch);
        const double eps = 1e-9;
        int n = Goods.All.Count;
        var unfilledBids = new double[state.Markets.Count * n];
        var unsoldAsks = new double[state.Markets.Count * n];
        foreach (var o in state.Orders)                   // id order (P6)
        {
            if (o.QtyRemaining <= 0) continue;
            if (o.Side == OrderSide.Buy)
                unfilledBids[o.PortId * n + o.Good] += o.QtyRemaining;
            else
                unsoldAsks[o.PortId * n + o.Good] += o.QtyRemaining;
        }
        for (int mIx = 0; mIx < state.Markets.Count; mIx++)
        {
            var market = state.Markets[mIx];
            for (int g = 0; g < n; g++)
            {
                double demand = (unfilledBids[mIx * n + g]
                                 + scratch.SignalDemand[mIx][g])
                                / state.Config.Sim.StepFraction;
                double supply = unsoldAsks[mIx * n + g];
                if (demand <= eps && supply <= eps) continue;   // dormant
                double factor = Math.Pow((demand + eps) / (supply + eps),
                                         eco.PriceDriftExponent);
                factor = Math.Min(cap, Math.Max(1.0 / cap, factor));
                double ceiling = Market.InitialPrice(eco, (GoodId)g)
                                 * eco.MaxPriceMultiple;
                market.Price[g] = Math.Min(ceiling,
                    Math.Max(eco.PriceFloor, market.Price[g] * factor));
            }
        }
    }

    /// <summary>Send one fill's goods to their poster: band fills are
    /// consumed (apportioned to the contributing segments' famine/SoL
    /// arithmetic pro-rata), project fills land in the laydown yard,
    /// procurement fills bank into the port's stockpile.</summary>
    private static void RouteFill(SimState state, MarketStepScratch scratch,
                                  OrderFill fill)
    {
        foreach (var bb in scratch.BandBids)
            if (ReferenceEquals(bb.Order, fill.Buy))
            {
                ApportionBandFill(state, scratch, bb, fill.Good, fill.Qty);
                return;                                   // consumed
            }
        foreach (var (order, project) in scratch.ProjectBids)
            if (ReferenceEquals(order, fill.Buy))
            {
                double total = project.DeliveredQty[fill.Good] + fill.Qty;
                project.DeliveredGrade[fill.Good] =
                    (project.DeliveredQty[fill.Good]
                         * project.DeliveredGrade[fill.Good]
                     + fill.Qty * fill.Grade) / total;
                project.DeliveredQty[fill.Good] = total;
                return;
            }
        foreach (var (order, _, port) in scratch.ProcureBids)
            if (ReferenceEquals(order, fill.Buy))
            {
                port.DepositStock(fill.Good, fill.Qty, fill.Grade);
                return;
            }
        foreach (var (order, portId) in scratch.RelayBids)
            if (ReferenceEquals(order, fill.Buy))
            {
                // staged for export: straight back on sale at the hub —
                // the next step's bridge carries it a hop onward
                BookOps.PostSupply(state, portId, order.OwnerActorId,
                    fill.Good, fill.Qty, fill.Grade);
                return;
            }
        // an untracked buy (none in B1): the goods bank at the buyer's port
        state.Ports[fill.Buy.PortId]
            .DepositStock(fill.Good, fill.Qty, fill.Grade);
    }

    /// <summary>Credit one band order's filled quantity back to the
    /// segments whose want it aggregated, pro-rata by contribution.</summary>
    private static void ApportionBandFill(SimState state,
        MarketStepScratch scratch, BandBid bb, int good, double qty)
    {
        if (bb.PostedQty <= 0) return;
        foreach (var r in scratch.DemandRecords)
        {
            if (r.MarketIndex != bb.MarketIndex || r.Band != bb.Band
                || r.Good != good) continue;
            double share = qty * r.Quantity / bb.PostedQty;
            if (bb.Band == PopulationBand.Subsistence)
                scratch.SubsistenceCleared[r.SegmentId] += share;
            else if (bb.Band == PopulationBand.StandardOfLiving)
                scratch.SoLCleared[r.SegmentId] += share;
        }
    }

    /// <summary>Return one band order's unfilled escrow to the segments
    /// that funded it, pro-rata by their escrow contribution.</summary>
    private static void RefundSegments(SimState state,
        MarketStepScratch scratch, BandBid bb, double refund)
    {
        double totalQty = 0;
        foreach (var r in scratch.DemandRecords)
            if (r.MarketIndex == bb.MarketIndex && r.Band == bb.Band
                && r.Good == bb.Order.Good)
                totalQty += r.Quantity;
        if (totalQty <= 0)
        {
            // no contributors on record: the port's people at large get it
            PayWages(state, state.Markets[bb.MarketIndex].PortId, refund);
            return;
        }
        foreach (var r in scratch.DemandRecords)
        {
            if (r.MarketIndex != bb.MarketIndex || r.Band != bb.Band
                || r.Good != bb.Order.Good) continue;
            state.Segments[r.SegmentId].Wealth
                += refund * r.Quantity / totalQty;
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
