using System;
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The fixed-horizon scheduler (spec §3): from the perceived
/// economy-as-rates it lays out a StandingPlan — what to build, where, and
/// in which world-year to break ground — packing every ordered project into
/// the income rate so nothing over-commits the treasury. Pure deterministic
/// math over the view + the same decision's other policies; no rolls, so
/// Intent stays P2-clean.</summary>
public static class Planner
{
    /// <summary>Assemble the standing schedule. Facilities and port raises
    /// come from the perceived candidate lists; hull batches from a D'Hondt
    /// apportionment of the ShipbuildingPriorities across own designs. Entries
    /// are score-ranked, then greedily packed into the earliest affordable
    /// integer start under the income rate (spec §3).</summary>
    public static StandingPlan BuildPlan(PerceptionView view,
        PolityPolicies policies, EpochSimConfig cfg)
    {
        if (view.Capability == null) return StandingPlan.Empty;
        int H = cfg.Sim.GenerationYears;
        if (H <= 0) return StandingPlan.Empty;
        // income plus the horizon-spread savings: treasuries exist to be
        // spent — packing against trailing income alone deadlocks a rich
        // polity whose receipts are lean real cash flow (contract economy)
        double capacity = view.Capability.IncomePerYear
                          + view.Capability.SavingsPerYear;

        // committed-cost timeline: each in-flight commitment fills the years
        // to its naive completion (spec §3 step 2)
        var timeline = new double[H];
        foreach (var c in view.Capability.Commitments)
        {
            int end = Math.Min(H, (int)Math.Ceiling(c.YearsRemaining));
            for (int y = 0; y < end; y++) timeline[y] += c.CostPerYear;
        }

        double militancy = view.SelfTemperament.Militancy;
        bool atWar = view.Wars.Count > 0;
        double civilBias = 1.0 - 0.3 * militancy;

        var desired = new List<(PlanEntry Entry, double Score)>();

        // facilities: the perceived siting scores, damped for the martial
        // and leaned toward supplied sites (stage 2: the brief is located —
        // a candidate whose port larder already holds the basket outranks
        // its twin at a bare frontier; the sprawl pays a coordination tax)
        foreach (var c in view.ConstructionCandidates)     // best-first per port
        {
            var entry = new PlanEntry(PlanEntryKind.Facility,
                ProjectPriority.Core, 0, c.TypeId, c.PortId, c.Hex, 1);
            desired.Add((entry,
                c.Score * civilBias * SupplyFactor(entry, view, cfg)));
        }

        // port raises: one per own under-max port, cheaper tiers preferred
        foreach (var port in view.OwnPorts)                // id order (P6)
        {
            if (port.Tier >= cfg.Infrastructure.MaxPortTier) continue;
            var entry = new PlanEntry(PlanEntryKind.PortRaise,
                ProjectPriority.Core, 0, -1, port.PortId,
                new HexCoordinate(0, 0), 1);
            double score = cfg.Controller.PortRaisePlanScore
                           / Math.Max(1, port.Tier) * civilBias
                           * SupplyFactor(entry, view, cfg);
            desired.Add((entry, score));
        }

        // hull batches: D'Hondt over the ShipbuildingPriorities-weighted own
        // designs. Slots accrue in WORLD-TIME (stage 2, P7): the cumulative
        // throughput clock floor(rate·year) grants each step exactly the
        // slots that matured inside its span — consecutive fine steps
        // telescope to the coarse total, so a 1y clock builds the same navy
        // per century as a 25y clock (the old Max(1,·) per-step floor fired
        // a unit batch every step at fine tick). Slots won by the same
        // design collapse into one batch (Count = slots won) — one project
        // per design, not per hull.
        double hullBase = 0.2 + 0.6 * militancy;
        double warFactor = atWar ? 2.0 : 1.0;
        var hullPriority = atWar ? ProjectPriority.War : ProjectPriority.Growth;
        foreach (var port in view.OwnPorts)                // id order (P6)
        {
            if (port.YardTiers <= 0) continue;
            double rate = port.YardTiers * cfg.Fleet.YardHullsPerTierPerYear;
            int slots = (int)Math.Floor(rate
                            * (view.WorldYear + cfg.Sim.YearsPerEpoch))
                        - (int)Math.Floor(rate * view.WorldYear);
            if (slots <= 0) continue;
            var granted = new Dictionary<int, int>();
            var bestClaimOf = new Dictionary<int, double>();
            for (int b = 0; b < slots; b++)
            {
                int bestId = -1;
                double bestClaim = 0;
                foreach (var d in view.OwnDesigns)         // design-id order (P6)
                {
                    if (!policies.ShipbuildingPriorities.TryGetValue(
                            d.DesignId, out double w) || w <= 0) continue;
                    granted.TryGetValue(d.DesignId, out int g);
                    double claim = w / (g + 1);
                    if (claim > bestClaim
                        || (claim == bestClaim && (bestId < 0 || d.DesignId < bestId)))
                    { bestClaim = claim; bestId = d.DesignId; }
                }
                if (bestId < 0) break;                     // nothing weighted
                if (!bestClaimOf.ContainsKey(bestId)) bestClaimOf[bestId] = bestClaim;
                granted[bestId] =
                    (granted.TryGetValue(bestId, out int gg) ? gg : 0) + 1;
            }
            foreach (var d in view.OwnDesigns)             // design-id order (P6)
            {
                if (!granted.TryGetValue(d.DesignId, out int count) || count <= 0)
                    continue;
                double score = hullBase * warFactor
                               * bestClaimOf[d.DesignId];
                // stranded settlers outrank routine shipbuilding: a polity
                // holding a colony's worth of expansion points with no
                // colony hull in reserve NEEDS the convoy first (slice CE —
                // the book economy packs fuller plans, and low-weighted
                // colony batches otherwise slide past the horizon forever)
                if (d.Role == ShipRole.Colony
                    && view.ColonyHullsAvailable == 0
                    && view.ExpansionPoints >= cfg.Expansion.ColonyCost)
                    score *= cfg.Controller.ColonyNeedBoost;
                desired.Add((new PlanEntry(PlanEntryKind.HullBatch,
                    hullPriority, 0, d.DesignId, port.PortId,
                    new HexCoordinate(0, 0), count), score));
            }
        }

        // score desc; ties by (Kind, PortId, TypeId, Hex.Q, Hex.R) — a fixed
        // order so the same view always yields the same plan (P6)
        desired.Sort((x, y) =>
        {
            int c = y.Score.CompareTo(x.Score);
            if (c != 0) return c;
            c = x.Entry.Kind.CompareTo(y.Entry.Kind);
            if (c != 0) return c;
            c = x.Entry.PortId.CompareTo(y.Entry.PortId);
            if (c != 0) return c;
            c = x.Entry.TypeId.CompareTo(y.Entry.TypeId);
            if (c != 0) return c;
            c = x.Entry.Hex.Q.CompareTo(y.Entry.Hex.Q);
            return c != 0 ? c : x.Entry.Hex.R.CompareTo(y.Entry.Hex.R);
        });

        // greedy pack: earliest integer start whose whole window fits under
        // the income rate; unaffordable work slides later or drops (spec §3)
        var plan = new List<PlanEntry>();
        foreach (var (entry, _) in desired)
        {
            if (plan.Count >= cfg.Controller.MaxPlanEntries) break;
            var (costPerYear, duration) = CostOf(entry, view, cfg);
            int dur = Math.Max(1, (int)Math.Ceiling(duration));
            int placed = -1;
            for (int s = 0; s < H; s++)
            {
                bool fits = true;
                int end = Math.Min(s + dur, H);
                for (int y = s; y < end; y++)
                    if (timeline[y] + costPerYear > capacity) { fits = false; break; }
                if (fits) { placed = s; break; }
            }
            if (placed < 0) continue;
            int e2 = Math.Min(placed + dur, H);
            for (int y = placed; y < e2; y++) timeline[y] += costPerYear;
            plan.Add(entry with { StartYear = view.WorldYear + placed });
        }

        return plan.Count == 0 ? StandingPlan.Empty : new StandingPlan(plan);
    }

    /// <summary>The packing's per-entry cost function: value drawn per world-
    /// year (goods at founding prices + construction wages) and the naive
    /// build duration in world-years. Mirrors what Groundbreak spends and
    /// what the CapabilityBrief's commitments report, so the schedule packs
    /// against the same rates it will later consume (spec §3).</summary>
    public static (double CostPerYear, double Duration) CostOf(
        PlanEntry entry, PerceptionView view, EpochSimConfig cfg)
    {
        var eco = cfg.Economy;
        switch (entry.Kind)
        {
            case PlanEntryKind.Facility:
            {
                var def = Substrate.Infrastructure.Get(
                    (Substrate.InfraTypeId)entry.TypeId);
                double years = Math.Max(1.0, def.ConstructionYears);
                double value = 0;
                foreach (var q in def.BuildCost)
                    value += q.Quantity * Market.InitialPrice(eco, q.Good);
                // goods + wages, each value/years (conservation, spec §2)
                return (2.0 * value / years, years);
            }
            case PlanEntryKind.PortRaise:
            {
                var ex = cfg.Expansion;
                double duration = Math.Max(1.0, ex.PortUpgradeYears);
                int tier = PortTierOf(view, entry.PortId);
                double goodsPerYear = tier * (
                    ex.PortUpgradeAlloysPerYearPerTier
                        * Market.InitialPrice(eco, Substrate.GoodId.Alloys)
                    + ex.PortUpgradeMachineryPerYearPerTier
                        * Market.InitialPrice(eco, Substrate.GoodId.Machinery)
                    + ex.PortUpgradeExoticsPerYearPerTier
                        * Market.InitialPrice(eco, Substrate.GoodId.RefinedExotics));
                double wagesPerYear = ex.PortUpgradeCostBase * tier / duration;
                return (goodsPerYear + wagesPerYear, duration);
            }
            case PlanEntryKind.HullBatch:
            {
                var (role, size) = DesignOf(view, entry.TypeId);
                double medium = DesignMath.ComponentsPerHull(cfg.Fleet,
                                                             ShipSize.Medium);
                double comp = DesignMath.ComponentsPerHull(cfg.Fleet, size);
                double duration = Math.Max(1.0,
                    cfg.Fleet.HullBuildYearsBase * (comp / medium));
                double perHull =
                    comp * Market.InitialPrice(eco, Substrate.GoodId.ShipComponents)
                    + DesignMath.ArmamentsPerHull(cfg.Fleet, role, size)
                        * Market.InitialPrice(eco, Substrate.GoodId.Armaments);
                return (2.0 * perHull * Math.Max(1, entry.Count) / duration,
                        duration);
            }
        }
        return (0.0, 1.0);
    }

    /// <summary>The supply lean (stage 2, spec §4b "Planner consequence"):
    /// 1 − w + w·coverage, coverage = the fraction of the entry's WHOLE
    /// build basket the site port's larder already holds (worst good).
    /// Bare larders everywhere damp uniformly — the lean only separates
    /// sites once somebody pre-positions.</summary>
    public static double SupplyFactor(PlanEntry entry, PerceptionView view,
                                      EpochSimConfig cfg)
    {
        double w = cfg.Controller.PlanSupplyWeight;
        if (w <= 0) return 1.0;
        IReadOnlyList<double>? stock = null;
        int portTier = 1;
        foreach (var p in view.OwnPorts)
            if (p.PortId == entry.PortId)
            { stock = p.Stock; portTier = p.Tier; break; }
        var (role, size) = entry.Kind == PlanEntryKind.HullBatch
            ? DesignOf(view, entry.TypeId)
            : (ShipRole.Freight, ShipSize.Medium);
        var basket = new double[Substrate.Goods.All.Count];
        double duration = EntryBasketPerYear(cfg, entry.Kind, entry.TypeId,
            entry.Count, portTier, role, size, basket);
        double coverage = 1.0;
        bool anyGood = false;
        for (int g = 0; g < basket.Length; g++)
        {
            if (basket[g] <= 0) continue;
            anyGood = true;
            double have = stock != null ? stock[g] : 0.0;
            coverage = Math.Min(coverage,
                Math.Min(1.0, have / (basket[g] * duration)));
        }
        return anyGood ? 1.0 - w + w * coverage : 1.0;
    }

    /// <summary>The per-year goods draw the entry's project will consume
    /// once ground breaks — mirrored from the spawn arithmetic so the
    /// scheduler and the quartermaster read the same rates (spec §4b).
    /// Fills <paramref name="into"/>, returns the build duration.</summary>
    public static double EntryBasketPerYear(EpochSimConfig cfg,
        PlanEntryKind kind, int typeId, int count, int portTier,
        ShipRole role, ShipSize size, double[] into)
    {
        Array.Clear(into, 0, into.Length);
        switch (kind)
        {
            case PlanEntryKind.Facility:
            {
                var def = Substrate.Infrastructure.Get(
                    (Substrate.InfraTypeId)typeId);
                double years = Math.Max(1.0, def.ConstructionYears);
                foreach (var q in def.BuildCost)
                    into[(int)q.Good] += q.Quantity / years;
                return years;
            }
            case PlanEntryKind.PortRaise:
            {
                var ex = cfg.Expansion;
                double years = Math.Max(1.0, ex.PortUpgradeYears);
                into[(int)Substrate.GoodId.Alloys] +=
                    ex.PortUpgradeAlloysPerYearPerTier * portTier;
                into[(int)Substrate.GoodId.Machinery] +=
                    ex.PortUpgradeMachineryPerYearPerTier * portTier;
                into[(int)Substrate.GoodId.RefinedExotics] +=
                    ex.PortUpgradeExoticsPerYearPerTier * portTier;
                return years;
            }
            case PlanEntryKind.HullBatch:
            {
                double medium = DesignMath.ComponentsPerHull(cfg.Fleet,
                                                             ShipSize.Medium);
                double comp = DesignMath.ComponentsPerHull(cfg.Fleet, size);
                double years = Math.Max(1.0,
                    cfg.Fleet.HullBuildYearsBase * (comp / medium));
                int n = Math.Max(1, count);
                into[(int)Substrate.GoodId.ShipComponents] +=
                    comp * n / years;
                double arms = DesignMath.ArmamentsPerHull(cfg.Fleet, role,
                                                          size);
                if (arms > 0)
                    into[(int)Substrate.GoodId.Armaments] += arms * n / years;
                return years;
            }
        }
        return 1.0;
    }

    private static int PortTierOf(PerceptionView view, int portId)
    {
        foreach (var p in view.OwnPorts)
            if (p.PortId == portId) return p.Tier;
        return 1;
    }

    private static (ShipRole Role, ShipSize Size) DesignOf(
        PerceptionView view, int designId)
    {
        foreach (var d in view.OwnDesigns)
            if (d.DesignId == designId) return (d.Role, d.Size);
        return (ShipRole.Freight, ShipSize.Medium);
    }
}
