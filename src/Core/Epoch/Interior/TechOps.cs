using System;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>Tech advancement and diffusion (economy/technology.md): research
/// is an Allocation execution of the standing research split, consuming
/// Refined Exotics multiplied by Compute; trade contact and salvage keep
/// laggards in the race (espionage's slot is reserved for the intrigue
/// substrate). No dedicated research facility exists — exotics labs and
/// compute cores upstream are the bottlenecks.</summary>
public static class TechOps
{
    /// <summary>Starting tiers from the emergence schedule (technology.md /
    /// life-and-precursors.md §Starting conditions): everyone industrializes
    /// and flies at era standard; maturation quality and the late-emerger
    /// contact bonus lift Astrogation and Industrial; militants arrive armed.</summary>
    public static void SeedEntryTiers(SimState state, PolityRecord pr)
    {
        var species = state.Skeleton.Species[pr.SpeciesId];
        int lift = pr.EntryGradeBonus >= 0.10 ? 1 : 0;
        pr.TechTier[(int)TechDomain.Industrial] = Tech.EraStandardTier + lift;
        pr.TechTier[(int)TechDomain.Astrogation] = Tech.EraStandardTier + lift;
        pr.TechTier[(int)TechDomain.Military] = species.Militancy > 0.5 ? 2 : 1;
        pr.TechTier[(int)TechDomain.Life] = Tech.EraStandardTier;
    }

    /// <summary>The design-sheet tech input per chassis role: warships read
    /// the Military region, everything that flies reads Astrogation.</summary>
    public static int DesignTier(SimState state, int ownerActorId, ShipRole role)
        => Tech.Region(state, ownerActorId,
            role is ShipRole.Escort or ShipRole.Line
                ? TechDomain.Military : TechDomain.Astrogation);

    /// <summary>Astrogation extends a port's service radius past the era
    /// standard — whose ports reach farther is visible geography.</summary>
    public static int AstroRadiusBonus(SimState state, int ownerActorId) =>
        Math.Max(0, state.Config.Tech.AstroRadiusPerTierHexes
            * (Tech.Tier(state, ownerActorId, TechDomain.Astrogation)
               - Tech.EraStandardTier));

    /// <summary>Astrogation extends inter-port lane reach likewise.</summary>
    public static int AstroRangeBonus(SimState state, int ownerActorId) =>
        Math.Max(0, state.Config.Tech.AstroRangePerTierHexes
            * (Tech.Tier(state, ownerActorId, TechDomain.Astrogation)
               - Tech.EraStandardTier));

    /// <summary>Life-domain growth multiplier over the base demographic rate.</summary>
    public static double LifeGrowthFactor(SimState state, int ownerActorId) =>
        1.0 + state.Config.Tech.LifeGrowthPerTier
            * (Tech.Tier(state, ownerActorId, TechDomain.Life)
               - Tech.EraStandardTier);

    /// <summary>Execute the research line: draw Refined Exotics (the input)
    /// and Compute (the multiplier) from own markets at market prices —
    /// spending recycles as wages (P4) — and convert to progress through the
    /// standing split. Returns credits actually spent.</summary>
    public static double Research(SimState state, PolityRecord pr,
                                  ResearchSplit split, double pool)
    {
        if (pool <= 0) return 0;
        var knobs = state.Config.Tech;
        double remaining = pool;
        double exotics = 0, compute = 0;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != pr.ActorId || remaining <= 0) continue;
            var market = state.Markets[port.Id];
            exotics += Draw(state, market, port.Id,
                (int)Substrate.GoodId.RefinedExotics, ref remaining);
            compute += Draw(state, market, port.Id,
                (int)Substrate.GoodId.Compute, ref remaining);
        }
        if (exotics > 0)
        {
            // compute multiplies effectiveness; it never substitutes input
            double progress = exotics * knobs.ProgressPerExotic
                * (1.0 + knobs.ComputeBoost
                         * Math.Min(1.0, compute / Math.Max(1e-9, exotics)));
            Advance(state, pr, TechDomain.Industrial, progress * split.Industrial);
            Advance(state, pr, TechDomain.Military, progress * split.Military);
            Advance(state, pr, TechDomain.Astrogation, progress * split.Astrogation);
            Advance(state, pr, TechDomain.Life, progress * split.Life);
        }
        return pool - remaining;
    }

    private static double Draw(SimState state, Market market, int portId,
                               int good, ref double remaining)
    {
        // feedstock is bought off the book at real asks — the suppliers
        // are paid, not an anonymous wage pool
        var (units, _, cost) = BookOps.LiftAsks(state, portId, good,
            qty: double.MaxValue, budget: remaining);
        remaining -= cost;
        return units;
    }

    /// <summary>Accumulate progress; threshold crossings climb the ladder
    /// and chronicle a TechAdvance (technology.md §Advancement).</summary>
    public static void Advance(SimState state, PolityRecord pr,
                               TechDomain domain, double progress)
    {
        if (progress <= 0) return;
        int d = (int)domain;
        pr.TechProgress[d] += progress;
        while (pr.TechProgress[d]
               >= Tech.Threshold(state.Config, pr.TechTier[d]))
        {
            pr.TechProgress[d] -= Tech.Threshold(state.Config, pr.TechTier[d]);
            pr.TechTier[d]++;
            state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.TechAdvanced, new[] { pr.ActorId },
                state.Actors[pr.ActorId].Seat, Magnitude: pr.TechTier[d],
                Valence: 1.0, EventVisibility.Regional,
                new TechAdvancedPayload(pr.ActorId, d, pr.TechTier[d])));
        }
    }

    /// <summary>Diffusion's bounded ladder climb: you can learn from the
    /// goods you buy, but not lead with them — the climb STOPS at the cap
    /// (no phantom TechAdvance past it) and overflow progress is discarded,
    /// structurally (not just at sane rates).</summary>
    private static void AdvanceCapped(SimState state, PolityRecord pr,
                                      TechDomain domain, double progress,
                                      int maxTier)
    {
        int d = (int)domain;
        if (progress <= 0 || pr.TechTier[d] >= maxTier) return;
        pr.TechProgress[d] += progress;
        while (pr.TechTier[d] < maxTier
               && pr.TechProgress[d] >= Tech.Threshold(state.Config, pr.TechTier[d]))
        {
            pr.TechProgress[d] -= Tech.Threshold(state.Config, pr.TechTier[d]);
            pr.TechTier[d]++;
            state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.TechAdvanced, new[] { pr.ActorId },
                state.Actors[pr.ActorId].Seat, Magnitude: pr.TechTier[d],
                Valence: 1.0, EventVisibility.Regional,
                new TechAdvancedPayload(pr.ActorId, d, pr.TechTier[d])));
        }
        if (pr.TechTier[d] >= maxTier) pr.TechProgress[d] = 0;
    }

    /// <summary>The two live diffusion channels (technology.md §Diffusion):
    /// trade contact drifts laggards toward partners' tiers, capped one tier
    /// below the source; wreckage above your own ceiling teaches (battlefields
    /// are tech events — full salvage *consumption* is slice I; precursor
    /// digging likewise). The espionage channel slot is reserved.</summary>
    public static void Diffuse(SimState state)
    {
        var knobs = state.Config.Tech;
        int years = state.Config.Sim.YearsPerEpoch;

        // trade contact across sovereignty borders (schisms and, later,
        // treaties make them): rate ∝ posted volume × both sides' openness
        foreach (var lane in state.Lanes)                     // id order (P6)
        {
            var a = state.Ports[lane.PortAId];
            var b = state.Ports[lane.PortBId];
            if (a.OwnerActorId == b.OwnerActorId) continue;
            double capacity = FleetOps.PostedCapacity(state, lane);
            if (capacity <= 0) continue;
            var prA = state.PolityOf(a.OwnerActorId);
            var prB = state.PolityOf(b.OwnerActorId);
            double openness = Temperament.Compose(state, prA).Openness
                              * Temperament.Compose(state, prB).Openness;
            double rate = knobs.TradeDiffusionPerYear * years * openness
                          * Math.Min(1.0, capacity / knobs.TradeVolumeSaturation);
            for (int d = 0; d < 4; d++)
            {
                if (prA.TechTier[d] < prB.TechTier[d] - 1)
                    AdvanceCapped(state, prA, (TechDomain)d, rate,
                                  prB.TechTier[d] - 1);
                else if (prB.TechTier[d] < prA.TechTier[d] - 1)
                    AdvanceCapped(state, prB, (TechDomain)d, rate,
                                  prA.TechTier[d] - 1);
            }
        }

        // salvage: wreckage whose design out-grades your Military ceiling,
        // sitting in your reach, teaches while it rusts
        foreach (var wreck in state.Wreckage)                 // id order (P6)
        {
            double grade = state.Designs[wreck.DesignId].ComponentGrade;
            foreach (var pr in state.Polities)                // actor-id order
            {
                if (!state.Actors[pr.ActorId].Entered
                    || grade <= Tech.Ceiling(state, pr.ActorId,
                                             TechDomain.Military)) continue;
                bool inReach = false;
                foreach (var port in state.Ports)
                    if (port.OwnerActorId == pr.ActorId
                        && HexGrid.Distance(port.Hex, wreck.Hex)
                           <= PortDomains.ServiceRadius(state.Config, port.Tier)
                              + AstroRadiusBonus(state, pr.ActorId))
                    { inReach = true; break; }
                if (!inReach) continue;
                double lesson = knobs.SalvagePerHullPerYear * years * wreck.Hulls
                                // full consumption (slice I): a stripped
                                // field teaches nothing — the lesson scales
                                // with what still lies there
                                * FieldRemaining(state, wreck.Hex);
                Advance(state, pr, TechDomain.Military, lesson);
                Advance(state, pr, TechDomain.Industrial, lesson * 0.5);
            }
        }
    }

    /// <summary>Fraction of a wreck hex's hulls not yet salvaged away —
    /// 1.0 where no battlefield POI tracks depletion (small fields rust
    /// and teach as before).</summary>
    private static double FieldRemaining(SimState state, Model.HexCoordinate hex)
    {
        foreach (var poi in state.Pois)                       // id order (P6)
            if (poi.Type == PoiType.Battlefield && poi.Hex.Equals(hex))
                return poi.Magnitude <= 0 ? 1.0
                    : System.Math.Max(0.0, poi.SalvageRemaining / poi.Magnitude);
        return 1.0;
    }
}
