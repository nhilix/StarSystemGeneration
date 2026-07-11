using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Contact and the per-pair relations gauges
/// (interpolity/relations.md): polities meet when reach overlaps; warmth and
/// tension drift toward targets recomputed from live, legible sources every
/// Interior phase. Pure state mechanics — no decisions (those are Intent's),
/// no rolls: friction is causal, only the spark (war.md, H5) rolls.</summary>
public static class RelationsOps
{
    /// <summary>One epoch of relations upkeep: contact detection, standing
    /// claim bookkeeping, warmth/tension drift. Returns counts for the
    /// phase note.</summary>
    /// <summary>Epochs a standing treaty offer stays on the table before it
    /// lapses (structural — offer churn, not calibration).</summary>
    private const int OfferExpiryEpochs = 4;

    public static (int Contacts, int ClaimsRaised) Step(SimState state)
    {
        var geometry = SurveyGeometry(state);
        int contacts = Contact(state, geometry);
        int claimsRaised = KinClaims(state);
        claimsRaised += LapseDynasticTies(state);
        ReleaseDeadSuccessionClaims(state);
        ExpireOffers(state);
        FederationOps.VassalExits(state);
        // sparks roll in the freshly surveyed contested space (war.md);
        // tension then drifts with the incident bumps already applied
        WarOps.Incidents(state, geometry);
        Recompute(state, geometry);
        return (contacts, claimsRaised);
    }

    /// <summary>The instrument that secured peace this generation seeds a
    /// war of succession two reigns later: a lapsed tie (its marriage
    /// generation dead) converts into a succession claim held by the
    /// prouder house (interpolity/relations.md §Dynastic instruments).</summary>
    private static int LapseDynasticTies(SimState state)
    {
        long lapseYears = (long)state.Config.Relations.DynasticTieLapseYears;
        int raised = 0;
        foreach (var relation in state.Relations)             // creation order (P6)
        {
            if (relation.DynasticTies <= 0 || relation.LastTieYear < 0
                || !BothLive(state, relation)) continue;
            if (state.WorldYear - relation.LastTieYear < lapseYears) continue;
            relation.DynasticTies--;
            relation.LastTieYear = relation.DynasticTies > 0
                ? state.WorldYear : -1;
            // the prouder house presses the claim the union created
            int holder = ProuderSide(state, relation);
            int dynasty = RulingDynasty(state, holder);
            if (dynasty < 0) continue;   // the claiming line lost its throne
            if (!relation.HasLiveClaim(ClaimType.Succession, holder, dynasty))
            {
                relation.Claims.Add(new RelationClaim(ClaimType.Succession,
                    holder, dynasty, state.WorldYear));
                raised++;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.ClaimRaised,
                    new[] { holder, relation.OtherOf(holder) },
                    state.Actors[holder].Seat, Magnitude: 1.0, Valence: -0.4,
                    EventVisibility.Public,
                    new ClaimRaisedPayload(holder, relation.OtherOf(holder),
                        (int)ClaimType.Succession, dynasty)));
            }
        }
        return raised;
    }

    /// <summary>A succession claim dies with its line: released once the
    /// claiming house no longer reigns at home.</summary>
    private static void ReleaseDeadSuccessionClaims(SimState state)
    {
        foreach (var relation in state.Relations)             // creation order (P6)
            foreach (var claim in relation.Claims)
            {
                if (claim.Released || claim.Type != ClaimType.Succession)
                    continue;
                if (RulingDynasty(state, claim.HolderPolityId) != claim.SubjectId)
                    Release(state, relation, ClaimType.Succession,
                            claim.HolderPolityId, claim.SubjectId);
            }
    }

    /// <summary>Which side's reigning house carries more prestige — ties to
    /// the lower actor id (P6).</summary>
    private static int ProuderSide(SimState state, PolityRelation relation)
    {
        return Prestige(relation.PolityBId) > Prestige(relation.PolityAId)
            ? relation.PolityBId : relation.PolityAId;
        double Prestige(int polityId)
        {
            int dynasty = RulingDynasty(state, polityId);
            return dynasty >= 0 ? state.Dynasties[dynasty].Prestige : -1;
        }
    }

    /// <summary>The dynasty on a polity's throne, or −1.</summary>
    public static int RulingDynasty(SimState state, int polityId)
    {
        var interior = state.PolityOf(polityId).Interior;
        if (interior == null || interior.RulerCharacterId < 0) return -1;
        var ruler = state.Characters[interior.RulerCharacterId];
        return ruler.Alive ? ruler.DynastyId : -1;
    }

    /// <summary>Both thrones are lineages — the forms dynastic instruments
    /// bind between (§Dynastic instruments).</summary>
    public static bool IsDynastic(SimState state, int polityId)
    {
        var interior = state.PolityOf(polityId).Interior;
        return interior != null
               && GovernmentForms.Get(interior.FormId).Succession
                   is SuccessionRule.Dynastic or SuccessionRule.RareDesignation;
    }

    /// <summary>Resolve a marriage or wardship: both thrones dynastic, the
    /// pair met and unbound, ties below the cap — warmth now (the tie rides
    /// the warmth target), the claim later (the lapse clock).</summary>
    public static bool ResolveDynasticInstrument(SimState state,
                                                 DynasticInstrumentAct act)
    {
        if (act.ActorId == act.TargetPolityId) return false;
        if (act.TargetPolityId >= state.Actors.Count
            || state.Actors[act.TargetPolityId].Kind != ActorKind.Polity)
            return false;
        var relation = state.RelationOf(act.ActorId, act.TargetPolityId);
        if (relation == null || !BothLive(state, relation)) return false;
        if (relation.VassalPolityId >= 0
            || FederationOps.OverlordOf(state, act.ActorId) >= 0
            || FederationOps.OverlordOf(state, act.TargetPolityId) >= 0)
            return false;   // the bound marry at their overlord's pleasure
        if (!IsDynastic(state, act.ActorId)
            || !IsDynastic(state, act.TargetPolityId)) return false;
        if (relation.DynasticTies >= 3) return false;
        relation.DynasticTies++;
        relation.LastTieYear = state.WorldYear;
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.DynasticInstrument,
            new[] { act.ActorId, act.TargetPolityId },
            state.Actors[act.TargetPolityId].Seat, Magnitude: 1.0,
            Valence: 0.5, EventVisibility.Public,
            new DynasticInstrumentPayload(act.ActorId, act.TargetPolityId,
                state.Actors[act.ActorId].Name,
                state.Actors[act.TargetPolityId].Name, (int)act.Instrument)));
        return true;
    }

    /// <summary>Both sides of a relation still on the stage — retired
    /// (federated, absorbed, extinct) pairs are history, not diplomacy.</summary>
    public static bool BothLive(SimState state, PolityRelation relation) =>
        state.Actors[relation.PolityAId].Entered
        && state.Actors[relation.PolityBId].Entered;

    /// <summary>Unanswered offers lapse quietly — the table clears.</summary>
    private static void ExpireOffers(SimState state)
    {
        foreach (var relation in state.Relations)             // creation order (P6)
            if (relation.OfferedRung != TreatyRung.None
                && state.EpochIndex - relation.OfferEpoch >= OfferExpiryEpochs)
            {
                relation.OfferedRung = TreatyRung.None;
                relation.OfferedById = -1;
                relation.OfferEpoch = -1;
            }
    }

    /// <summary>Per unordered polity pair: nearest port distance, the
    /// contested-overlap count (port pairs whose service areas touch — the
    /// space model's organic borders), and the closest pair's hexes (the
    /// flashpoint incidents roll at).</summary>
    public sealed class PairGeometry
    {
        public int MinPortDistance = int.MaxValue;
        public int OverlapPairs;
        public HexCoordinate ClosestA;
        public HexCoordinate ClosestB;
    }

    private static Dictionary<(int A, int B), PairGeometry> SurveyGeometry(
        SimState state)
    {
        var cfg = state.Config;
        var geometry = new Dictionary<(int, int), PairGeometry>();
        // service reach per port owner is owner-wide — cache the tech bonus
        var radiusBonus = new Dictionary<int, int>();
        for (int i = 0; i < state.Ports.Count; i++)
        {
            var pa = state.Ports[i];
            if (!state.Actors[pa.OwnerActorId].Entered) continue;
            for (int j = i + 1; j < state.Ports.Count; j++)
            {
                var pb = state.Ports[j];
                if (pb.OwnerActorId == pa.OwnerActorId
                    || !state.Actors[pb.OwnerActorId].Entered) continue;
                var key = pa.OwnerActorId < pb.OwnerActorId
                    ? (pa.OwnerActorId, pb.OwnerActorId)
                    : (pb.OwnerActorId, pa.OwnerActorId);
                if (!geometry.TryGetValue(key, out var g))
                    geometry[key] = g = new PairGeometry();
                int dist = HexGrid.Distance(pa.Hex, pb.Hex);
                if (dist < g.MinPortDistance)
                {
                    g.MinPortDistance = dist;
                    g.ClosestA = pa.Hex;
                    g.ClosestB = pb.Hex;
                }
                if (!radiusBonus.TryGetValue(pa.OwnerActorId, out int bonusA))
                    radiusBonus[pa.OwnerActorId] = bonusA =
                        TechOps.AstroRadiusBonus(state, pa.OwnerActorId);
                if (!radiusBonus.TryGetValue(pb.OwnerActorId, out int bonusB))
                    radiusBonus[pb.OwnerActorId] = bonusB =
                        TechOps.AstroRadiusBonus(state, pb.OwnerActorId);
                if (dist <= PortDomains.ServiceRadius(cfg, pa.Tier) + bonusA
                            + PortDomains.ServiceRadius(cfg, pb.Tier) + bonusB)
                    g.OverlapPairs++;
            }
        }
        return geometry;
    }

    // ---- contact ----

    /// <summary>Polities meet when reach overlaps — a first-contact event
    /// and a relation seeded at its source-computed targets (the initial
    /// stance: temperament compositions × strangeness × reputation, which
    /// is P3's and arrives in slice I).</summary>
    private static int Contact(SimState state,
        Dictionary<(int A, int B), PairGeometry> geometry)
    {
        var knobs = state.Config.Relations;
        int contacts = 0;
        // deterministic order: ascending (A, B) — geometry keys sorted
        var keys = new List<(int A, int B)>(geometry.Keys);
        keys.Sort();
        foreach (var key in keys)
        {
            var g = geometry[key];
            if (g.MinPortDistance > knobs.ContactReachHexes) continue;
            if (state.RelationOf(key.A, key.B) != null) continue;
            var relation = new PolityRelation(key.A, key.B, state.EpochIndex);
            state.Relations.Add(relation);
            // seed at the drift targets: the stance two strangers open with
            relation.Warmth = WarmthTarget(state, relation, tradeCapacity: 0);
            relation.Tension = TensionTarget(state, relation, g.OverlapPairs);
            contacts++;
            var seatA = state.Actors[key.A].Seat;
            var seatB = state.Actors[key.B].Seat;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.FirstContact,
                new[] { key.A, key.B },
                HexGrid.Round((seatA.Q + seatB.Q) * 0.5, (seatA.R + seatB.R) * 0.5),
                Magnitude: 1.0, Valence: 0.0, EventVisibility.Public,
                new FirstContactPayload(key.A, key.B,
                    state.Actors[key.A].Name, state.Actors[key.B].Name)));
        }
        return contacts;
    }

    // ---- standing claims ----

    /// <summary>Cultural-kin claims: a polity whose founding culture lives
    /// under the other's rule holds a standing claim until the kin are gone
    /// (assimilated, migrated, or liberated). Raised and released as
    /// chronicle events; released claims stay as history.</summary>
    private static int KinClaims(SimState state)
    {
        var floor = state.Config.Relations.KinClaimSegmentFloor;
        int raised = 0;
        foreach (var relation in state.Relations)             // creation order (P6)
        {
            if (!BothLive(state, relation)) continue;
            for (int side = 0; side < 2; side++)
            {
                int holder = side == 0 ? relation.PolityAId : relation.PolityBId;
                int other = relation.OtherOf(holder);
                var holderInterior = state.PolityOf(holder).Interior;
                if (holderInterior == null) continue;
                int kinCulture = holderInterior.FoundingCultureId;
                double kinSize = 0;
                foreach (var s in state.Segments)
                    if (s.Size > 0 && s.CultureId == kinCulture
                        && state.Ports[s.PortId].OwnerActorId == other)
                        kinSize += s.Size;
                // lost territory resolves when the holder takes it back —
                // grudges persist until their cause does not (review fix 3)
                foreach (var claim in relation.Claims)
                {
                    if (claim.Released
                        || claim.Type != ClaimType.LostTerritory
                        || claim.HolderPolityId != holder) continue;
                    if (claim.SubjectId >= 0
                        && claim.SubjectId < state.Ports.Count
                        && state.Ports[claim.SubjectId].OwnerActorId == holder)
                        Release(state, relation, ClaimType.LostTerritory,
                                holder, claim.SubjectId);
                }
                // liberation claims (suppressed emergences, H8) release the
                // same way kin claims do: when the people are out from
                // under the other's rule
                foreach (var claim in relation.Claims)
                {
                    if (claim.Released || claim.Type != ClaimType.Liberation
                        || claim.HolderPolityId != holder) continue;
                    double captiveSize = 0;
                    foreach (var s in state.Segments)
                        if (s.Size > 0 && s.CultureId == claim.SubjectId
                            && state.Ports[s.PortId].OwnerActorId == other)
                            captiveSize += s.Size;
                    if (captiveSize < floor)
                        Release(state, relation, ClaimType.Liberation,
                                holder, claim.SubjectId);
                }
                bool live = relation.HasLiveClaim(ClaimType.CulturalKin,
                                                  holder, kinCulture);
                if (kinSize >= floor && !live)
                {
                    relation.Claims.Add(new RelationClaim(ClaimType.CulturalKin,
                        holder, kinCulture, state.WorldYear));
                    raised++;
                    state.Staged.Add(new StagedEvent(
                        ClockStratum.Generational, WorldEventType.ClaimRaised,
                        new[] { holder, other }, state.Actors[holder].Seat,
                        Magnitude: kinSize, Valence: -0.3,
                        EventVisibility.Regional,
                        new ClaimRaisedPayload(holder, other,
                            (int)ClaimType.CulturalKin, kinCulture)));
                }
                else if (kinSize < floor && live)
                    Release(state, relation, ClaimType.CulturalKin, holder,
                            kinCulture);
            }
        }
        return raised;
    }

    /// <summary>Release every live claim matching (type, holder, subject) —
    /// the source resolved; tension may now decay.</summary>
    public static void Release(SimState state, PolityRelation relation,
                               ClaimType type, int holder, int subjectId)
    {
        foreach (var c in relation.Claims)
        {
            if (c.Released || c.Type != type || c.HolderPolityId != holder
                || c.SubjectId != subjectId) continue;
            c.Released = true;
            c.ReleasedYear = state.WorldYear;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.ClaimReleased,
                new[] { holder, relation.OtherOf(holder) },
                state.Actors[holder].Seat, Magnitude: 1.0, Valence: 0.3,
                EventVisibility.Regional,
                new ClaimReleasedPayload(holder, relation.OtherOf(holder),
                                         (int)type, subjectId)));
        }
    }

    // ---- warmth / tension ----

    /// <summary>Drift every relation's gauges toward their source-computed
    /// targets: rise fast, relax slow — and the tension target only falls
    /// when sources actually resolve.</summary>
    private static void Recompute(SimState state,
        Dictionary<(int A, int B), PairGeometry> geometry)
    {
        var knobs = state.Config.Relations;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var relation in state.Relations)             // creation order (P6)
        {
            if (!BothLive(state, relation)) continue;
            double trade = CrossTradeCapacity(state, relation);
            geometry.TryGetValue((relation.PolityAId, relation.PolityBId),
                                 out var g);
            double warmthTarget = WarmthTarget(state, relation, trade);
            double tensionTarget = TensionTarget(state, relation,
                                                 g?.OverlapPairs ?? 0);
            double warmthRate = Math.Min(1.0, knobs.WarmthDriftPerYear * years);
            relation.Warmth += (warmthTarget - relation.Warmth) * warmthRate;
            double tensionRate = Math.Min(1.0,
                (tensionTarget > relation.Tension
                    ? knobs.TensionRisePerYear
                    : knobs.TensionRelaxPerYear) * years);
            relation.Tension += (tensionTarget - relation.Tension) * tensionRate;
        }
    }

    /// <summary>The warmth target from live sources. Terms land in
    /// LastWarmthTerms for the REPL: [0] baseline − strangeness,
    /// [1] trade, [2] treaty, [3] dynastic ties, [4] −ideology cooling.</summary>
    public static double WarmthTarget(SimState state, PolityRelation relation,
                                      double tradeCapacity)
    {
        var knobs = state.Config.Relations;
        var a = state.PolityOf(relation.PolityAId);
        var b = state.PolityOf(relation.PolityBId);
        var tempA = Temperament.Compose(state, a);
        var tempB = Temperament.Compose(state, b);
        double meanOpenness = 0.5 * (tempA.Openness + tempB.Openness);
        double strangeness = Strangeness(state, a, b) * (1.0 - meanOpenness);
        double ideoGap = IdeologyGap(a, b);
        var t = relation.LastWarmthTerms;
        t[0] = 0.5 - knobs.StrangenessWeight * strangeness;
        t[1] = knobs.TradeWarmthWeight
               * Math.Min(1.0, tradeCapacity / knobs.TradeSaturation);
        t[2] = knobs.TreatyWarmthWeight * (int)relation.Rung
               / (double)(int)TreatyRung.DefenseAlliance;
        t[3] = knobs.DynasticTieWarmth * Math.Min(relation.DynasticTies, 3);
        t[4] = -knobs.IdeologyGapCooling * ideoGap;
        return Clamp01(t[0] + t[1] + t[2] + t[3] + t[4]);
    }

    /// <summary>The tension target from live sources. Terms land in
    /// LastTensionTerms for the REPL: [0] overlap, [1] claims,
    /// [2] interdiction, [3] ideology gap × zeal, [4] agitation,
    /// [5] militancy.</summary>
    public static double TensionTarget(SimState state, PolityRelation relation,
                                       int overlapPairs)
    {
        var knobs = state.Config.Relations;
        var a = state.PolityOf(relation.PolityAId);
        var b = state.PolityOf(relation.PolityBId);
        var tempA = Temperament.Compose(state, a);
        var tempB = Temperament.Compose(state, b);
        int liveClaims = 0;
        foreach (var c in relation.Claims)
            if (!c.Released) liveClaims++;
        var t = relation.LastTensionTerms;
        // entanglement reads by the relationship: strangers see a loaded
        // border, friends see a shared one (warmth damps the overlap term —
        // the soup either federates or fights, it doesn't simmer)
        t[0] = knobs.OverlapTensionWeight
               * Math.Min(1.0, overlapPairs / knobs.OverlapSaturation)
               * (1.0 - relation.Warmth);
        t[1] = Math.Min(1.0, knobs.ClaimTensionWeight * liveClaims);
        t[2] = knobs.InterdictionTensionWeight
               * InterdictionStrain(state, relation);
        t[3] = knobs.IdeologyTensionWeight * IdeologyGap(a, b)
               * MeanRulerZeal(state, a, b);
        t[4] = knobs.AgitationTensionWeight
               * Math.Max(MilitaryAgitation(state, a.ActorId),
                          MilitaryAgitation(state, b.ActorId));
        t[5] = knobs.MilitancyTensionWeight
               * 0.5 * (tempA.Militancy + tempB.Militancy);
        double target = Clamp01(t[0] + t[1] + t[2] + t[3] + t[4] + t[5]);
        // the non-aggression rung's teeth: standing friction is damped
        // while the pact holds (interpolity/relations.md §Treaties)
        if (relation.Rung >= TreatyRung.NonAggression)
            target *= 1.0 - knobs.NonAggressionDamping;
        return target;
    }

    /// <summary>How alien two polities read to each other before openness
    /// filters it: embodiment mismatch plus disposition distance. Zero for
    /// one species meeting itself (schism siblings).</summary>
    public static double Strangeness(SimState state, PolityRecord a,
                                     PolityRecord b)
    {
        if (a.SpeciesId == b.SpeciesId) return 0.0;
        if (a.SpeciesId < 0 || b.SpeciesId < 0
            || a.SpeciesId >= state.Skeleton.Species.Count
            || b.SpeciesId >= state.Skeleton.Species.Count) return 0.5;
        var sa = state.Skeleton.Species[a.SpeciesId];
        var sb = state.Skeleton.Species[b.SpeciesId];
        double traits = (Math.Abs(sa.Expansionism - sb.Expansionism)
                         + Math.Abs(sa.Cohesion - sb.Cohesion)
                         + Math.Abs(sa.Militancy - sb.Militancy)
                         + Math.Abs(sa.Openness - sb.Openness)
                         + Math.Abs(sa.Industry - sb.Industry)
                         + Math.Abs(sa.Adaptability - sb.Adaptability)) / 6.0;
        return Clamp01((sa.Embodiment == sb.Embodiment ? 0.0 : 0.5) + traits);
    }

    /// <summary>Mean official-ideology axis distance (0 when either interior
    /// is unseated — shape skeletons).</summary>
    public static double IdeologyGap(PolityRecord a, PolityRecord b)
    {
        if (a.Interior == null || b.Interior == null) return 0.0;
        double gap = 0;
        for (int ax = 0; ax < 4; ax++)
            gap += Math.Abs(a.Interior.OfficialIdeology[ax]
                            - b.Interior.OfficialIdeology[ax]);
        return gap / 4.0;
    }

    /// <summary>Ideological friction scales with who holds the thrones —
    /// zealots read doctrine gaps as casus belli material.</summary>
    private static double MeanRulerZeal(SimState state, PolityRecord a,
                                        PolityRecord b)
    {
        return 0.5 * (RulerZeal(state, a) + RulerZeal(state, b));
        static double RulerZeal(SimState state, PolityRecord pr)
        {
            int id = pr.Interior?.RulerCharacterId ?? -1;
            return id >= 0 && id < state.Characters.Count
                ? state.Characters[id].Zeal : 0.5;
        }
    }

    /// <summary>Military-faction pressure inside a polity — the sword
    /// agitating for discharge (strength × militancy of the loudest).</summary>
    private static double MilitaryAgitation(SimState state, int polityId)
    {
        double agitation = 0;
        foreach (var faction in state.Factions)               // id order (P6)
            if (faction.Active && faction.PolityId == polityId
                && faction.Basis == FactionBasis.Military)
                agitation = Math.Max(agitation,
                                     faction.Strength * faction.Militancy);
        return agitation;
    }

    /// <summary>Blockade fleets of one of the pair stationed at the other's
    /// ports — interdiction strain (real interdiction, H6 posts them).</summary>
    private static double InterdictionStrain(SimState state,
                                             PolityRelation relation)
    {
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Blockade || fleet.TargetId < 0
                || fleet.TargetId >= state.Ports.Count
                || fleet.TotalHulls == 0) continue;
            if (!relation.Involves(fleet.OwnerActorId)) continue;
            if (state.Ports[fleet.TargetId].OwnerActorId
                == relation.OtherOf(fleet.OwnerActorId)) return 1.0;
        }
        return 0.0;
    }

    /// <summary>Contested-overlap count for one pair, computed directly:
    /// port pairs whose service areas touch. The geometry survey computes
    /// this in bulk each Interior; this is the on-demand form the
    /// federation gate and perception briefs read.</summary>
    public static int OverlapPairs(SimState state, int polityA, int polityB)
    {
        var cfg = state.Config;
        int pairs = 0;
        foreach (var pa in state.Ports)                       // id order (P6)
        {
            if (pa.OwnerActorId != polityA) continue;
            foreach (var pb in state.Ports)
            {
                if (pb.OwnerActorId != polityB) continue;
                if (HexGrid.Distance(pa.Hex, pb.Hex)
                    <= PortDomains.ServiceRadius(cfg, pa.Tier)
                       + TechOps.AstroRadiusBonus(state, polityA)
                       + PortDomains.ServiceRadius(cfg, pb.Tier)
                       + TechOps.AstroRadiusBonus(state, polityB))
                    pairs++;
            }
        }
        return pairs;
    }

    /// <summary>Saturated overlap [0,1] — the entanglement share the
    /// federation gate discounts by.</summary>
    public static double OverlapShare(SimState state, int polityA, int polityB)
        => Math.Min(1.0, OverlapPairs(state, polityA, polityB)
                         / state.Config.Relations.OverlapSaturation);

    /// <summary>Posted freight capacity on lanes joining the pair's ports —
    /// the trade-volume warmth source (physical, derivable).</summary>
    public static double CrossTradeCapacity(SimState state,
                                            PolityRelation relation)
    {
        double capacity = 0;
        foreach (var lane in state.Lanes)                     // id order (P6)
        {
            int ownerA = state.Ports[lane.PortAId].OwnerActorId;
            int ownerB = state.Ports[lane.PortBId].OwnerActorId;
            if (ownerA == ownerB) continue;
            if (!relation.Involves(ownerA) || !relation.Involves(ownerB))
                continue;
            capacity += FleetOps.PostedCapacity(state, lane);
        }
        return capacity;
    }

    // ---- the treaty ladder (interpolity/relations.md §Treaties) ----

    public enum TreatyOutcome { NoEffect, Offered, Signed, Broken }

    /// <summary>Warmth required to offer or accept a rung — warmth gates
    /// ascent.</summary>
    public static double TreatyGate(EpochSimConfig cfg, TreatyRung rung) =>
        cfg.Relations.TreatyGateBase
        + cfg.Relations.TreatyGateStep * ((int)rung - 1);

    /// <summary>Tariff multiplier between two owners: the trade-pact cut
    /// when the first rung (or higher) stands, 1 otherwise. Markets call
    /// this at both tariff sites.</summary>
    public static double TariffFactor(SimState state, int ownerA, int ownerB)
    {
        var relation = state.RelationOf(ownerA, ownerB);
        return relation != null && relation.Rung >= TreatyRung.TradePact
            ? state.Config.Relations.PactTariffFactor : 1.0;
    }

    /// <summary>Resolve one treaty act — mutual consent in Resolution:
    /// offers sit on the relation until accepted, matched (mutual offers
    /// consent immediately), or lapsed; rungs climb one at a time; breaking
    /// is public and warmth crashes.</summary>
    public static TreatyOutcome ResolveTreaty(SimState state, TreatyAct act)
    {
        if (act.ActorId == act.TargetPolityId) return TreatyOutcome.NoEffect;
        if (act.ActorId >= state.Actors.Count
            || act.TargetPolityId >= state.Actors.Count
            || state.Actors[act.ActorId].Kind != ActorKind.Polity
            || state.Actors[act.TargetPolityId].Kind != ActorKind.Polity)
            return TreatyOutcome.NoEffect;
        var relation = state.RelationOf(act.ActorId, act.TargetPolityId);
        if (relation == null) return TreatyOutcome.NoEffect;   // no table before contact
        if (!BothLive(state, relation)) return TreatyOutcome.NoEffect;
        // vassalage's foreign-policy lock: the bound treat with no one
        if (FederationOps.OverlordOf(state, act.ActorId) >= 0
            || FederationOps.OverlordOf(state, act.TargetPolityId) >= 0
            || relation.VassalPolityId >= 0)
            return TreatyOutcome.NoEffect;
        var rung = (TreatyRung)act.Rung;
        switch (act.Verb)
        {
            case TreatyVerb.Offer:
                if (rung != relation.Rung + 1
                    || rung > TreatyRung.Federation)
                    return TreatyOutcome.NoEffect;
                if (relation.OfferedRung == rung
                    && relation.OfferedById == act.TargetPolityId)
                    return Sign(state, relation, rung);   // mutual offers consent
                relation.OfferedRung = rung;
                relation.OfferedById = act.ActorId;
                relation.OfferEpoch = state.EpochIndex;
                return TreatyOutcome.Offered;
            case TreatyVerb.Accept:
                if (relation.OfferedRung == TreatyRung.None
                    || relation.OfferedById != act.TargetPolityId
                    || relation.OfferedRung != relation.Rung + 1)
                    return TreatyOutcome.NoEffect;
                return Sign(state, relation, relation.OfferedRung);
            case TreatyVerb.Break:
            {
                if (relation.Rung == TreatyRung.None) return TreatyOutcome.NoEffect;
                var broken = relation.Rung;
                relation.Rung = TreatyRung.None;
                relation.RungEpoch = -1;
                relation.OfferedRung = TreatyRung.None;
                relation.OfferedById = -1;
                relation.OfferEpoch = -1;
                relation.Warmth = Math.Max(0.0, relation.Warmth
                    - state.Config.Relations.BreakWarmthPenalty);
                int other = act.TargetPolityId;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.TreatyBroken,
                    new[] { act.ActorId, other }, state.Actors[act.ActorId].Seat,
                    Magnitude: (int)broken, Valence: -0.7,
                    EventVisibility.Public,
                    new TreatyBrokenPayload(act.ActorId, other,
                        state.Actors[act.ActorId].Name,
                        state.Actors[other].Name, (int)broken)));
                return TreatyOutcome.Broken;
            }
            default:
                return TreatyOutcome.NoEffect;
        }
    }

    private static TreatyOutcome Sign(SimState state, PolityRelation relation,
                                      TreatyRung rung)
    {
        if (rung == TreatyRung.Federation)
        {
            // the top rung is not held, it is executed: the fusion gate
            // verifies on truth; a gate that fails voids the consent
            if (!FederationOps.FederationGateHolds(state, relation))
            {
                relation.OfferedRung = TreatyRung.None;
                relation.OfferedById = -1;
                relation.OfferEpoch = -1;
                return TreatyOutcome.NoEffect;
            }
            FederationOps.Federate(state, relation);
            return TreatyOutcome.Signed;
        }
        relation.Rung = rung;
        relation.RungEpoch = state.EpochIndex;
        relation.OfferedRung = TreatyRung.None;
        relation.OfferedById = -1;
        relation.OfferEpoch = -1;
        var seatA = state.Actors[relation.PolityAId].Seat;
        var seatB = state.Actors[relation.PolityBId].Seat;
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.TreatySigned,
            new[] { relation.PolityAId, relation.PolityBId },
            HexGrid.Round((seatA.Q + seatB.Q) * 0.5, (seatA.R + seatB.R) * 0.5),
            Magnitude: (int)rung, Valence: 0.7, EventVisibility.Public,
            new TreatySignedPayload(relation.PolityAId, relation.PolityBId,
                state.Actors[relation.PolityAId].Name,
                state.Actors[relation.PolityBId].Name, (int)rung)));
        return TreatyOutcome.Signed;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
