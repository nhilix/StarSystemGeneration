using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The refresh machinery behind compressed belief (P3): a snapshot
/// per (observer, subject) refreshes from truth when the traffic-derived
/// news delay allows and freezes between refreshes. Decisions read the
/// snapshots; consequences run on truth (frame/simulation-flow.md Move 2).</summary>
public static class BeliefOps
{
    /// <summary>Per-step memo of news delay fields by origin hex — one
    /// Dijkstra per distinct origin, shared across observers. Scratch,
    /// rebuilt each Perception (traffic moved).</summary>
    public sealed class NewsFieldCache
    {
        private readonly Dictionary<HexCoordinate, double[]> _fields
            = new Dictionary<HexCoordinate, double[]>();

        public double[] FieldFor(SimState state, HexCoordinate origin)
        {
            if (!_fields.TryGetValue(origin, out var field))
            {
                field = NewsOps.DelayFromHex(state, origin);
                _fields[origin] = field;
            }
            return field;
        }
    }

    /// <summary>Where news about an actor is born: its capital port's hex
    /// (the first port it owns, id order), falling back to its seat.</summary>
    public static HexCoordinate CapitalHexOf(SimState state, int actorId)
    {
        foreach (var p in state.Ports)                    // id order (P6)
            if (p.OwnerActorId == actorId) return p.Hex;
        return state.Actors[actorId].Seat;
    }

    /// <summary>News delay in world-years from a subject actor's capital to
    /// the observer's nearest ear.</summary>
    public static double DelayBetween(SimState state, int observerId,
                                      int subjectId, NewsFieldCache fields)
    {
        var origin = CapitalHexOf(state, subjectId);
        return NewsOps.DelayYears(state, observerId,
                                  fields.FieldFor(state, origin), origin);
    }

    /// <summary>The observer's belief about one polity, refreshed from truth
    /// when due: on first sight (contact is an information event), and
    /// whenever the years since the last snapshot cover the news delay.
    /// Between refreshes every field freezes — that IS the staleness.</summary>
    public static PolityBelief About(SimState state, int observerId,
                                     int subjectId,
                                     IReadOnlyDictionary<int, double> strengths,
                                     NewsFieldCache fields)
    {
        var beliefs = state.Actors[observerId].Beliefs;
        bool fresh = !beliefs.Polities.TryGetValue(subjectId, out var b);
        if (fresh)
        {
            b = new PolityBelief(subjectId);
            beliefs.Polities.Add(subjectId, b);
        }
        if (fresh || state.WorldYear - b!.HeardYear
                >= DelayBetween(state, observerId, subjectId, fields))
        {
            b!.HeardYear = state.WorldYear;
            b.Strength = strengths.TryGetValue(subjectId, out double s) ? s : 0;
            b.DefensiveStrength = DefensiveStrength(state, subjectId, strengths);
            b.Menu.Clear();
            foreach (var (cause, subject) in WarOps.Menu(state, observerId,
                                                         subjectId))
                b.Menu.Add(new CasusBelliOption(cause, subject));
            b.ObjectiveCandidates.Clear();
            b.ObjectiveCandidates.AddRange(
                ObjectiveCandidates(state, observerId, subjectId));
        }
        return b!;
    }

    /// <summary>The belligerent's belief about a war it is in: the front
    /// reports route from the opponent leader's capital. First sight is
    /// fresh — a declaration happens TO you.</summary>
    public static WarBelief AboutWar(SimState state, int observerId, War war,
                                     NewsFieldCache fields)
    {
        var beliefs = state.Actors[observerId].Beliefs;
        bool fresh = !beliefs.Wars.TryGetValue(war.Id, out var b);
        if (fresh)
        {
            b = new WarBelief(war.Id);
            beliefs.Wars.Add(war.Id, b);
        }
        bool attackerSide = war.OnAttackerSide(observerId);
        int opponent = attackerSide ? war.DefenderId : war.AttackerId;
        if (fresh || state.WorldYear - b!.HeardYear
                >= DelayBetween(state, observerId, opponent, fields))
        {
            b!.HeardYear = state.WorldYear;
            b.OwnSideExhaustion = attackerSide
                ? war.AttackerExhaustion : war.DefenderExhaustion;
            double atStart = attackerSide
                ? war.AttackerStrengthAtStart : war.DefenderStrengthAtStart;
            b.OwnSideStrengthShare = atStart <= 0 ? 1.0
                : WarOps.SideStrength(state, war, attackerSide) / atStart;
            int taken = 0;
            foreach (var o in war.Objectives)
                if (o.Status == ObjectiveStatus.Taken) taken++;
            b.ObjectivesTaken = taken;
        }
        return b!;
    }

    /// <summary>The host's belief about a chartered corporation's books —
    /// they are wherever the headquarters is.</summary>
    public static CorpBelief AboutCorporation(SimState state, int observerId,
                                              Corporation corp,
                                              NewsFieldCache fields)
    {
        var beliefs = state.Actors[observerId].Beliefs;
        bool fresh = !beliefs.Corporations.TryGetValue(corp.Id, out var b);
        if (fresh)
        {
            b = new CorpBelief(corp.Id);
            beliefs.Corporations.Add(corp.Id, b);
        }
        var origin = corp.HomePortId >= 0 && corp.HomePortId < state.Ports.Count
            ? state.Ports[corp.HomePortId].Hex
            : state.Actors[corp.ActorId].Seat;
        double delay = NewsOps.DelayYears(state, observerId,
            fields.FieldFor(state, origin), origin);
        if (fresh || state.WorldYear - b!.HeardYear >= delay)
        {
            b!.HeardYear = state.WorldYear;
            b.Credits = corp.Credits;
        }
        return b!;
    }

    /// <summary>What an attacker prices: the target plus everyone bound to
    /// defend it — defense-alliance partners, its vassals, its overlord.
    /// Truth at snapshot time (moved from PerceptionPhase, slice I).</summary>
    public static double DefensiveStrength(SimState state, int polityId,
        IReadOnlyDictionary<int, double> strengths)
    {
        double total = strengths.TryGetValue(polityId, out double own) ? own : 0;
        foreach (var rel in state.Relations)              // creation order (P6)
        {
            if (!rel.Involves(polityId)
                || !RelationsOps.BothLive(state, rel)) continue;
            int other = rel.OtherOf(polityId);
            if (rel.Rung == TreatyRung.DefenseAlliance
                || rel.VassalPolityId >= 0)
                total += strengths.TryGetValue(other, out double s) ? s : 0;
        }
        return total;
    }

    /// <summary>Mechanical war-target enumeration: the other side's nearest
    /// ports (chokepoints first), its busiest lane, and its navy — what a
    /// declaration's objective set is picked from. Truth at snapshot time
    /// (moved from PerceptionPhase, slice I).</summary>
    public static List<WarObjectiveSpec> ObjectiveCandidates(SimState state,
        int selfId, int otherId)
    {
        var candidates = new List<WarObjectiveSpec>();
        var ports = new List<(bool Chokepoint, int Distance, int Id)>();
        foreach (var target in state.Ports)               // id order (P6)
        {
            if (target.OwnerActorId != otherId) continue;
            int best = int.MaxValue;
            foreach (var own in state.Ports)
                if (own.OwnerActorId == selfId)
                {
                    int d = HexGrid.Distance(own.Hex, target.Hex);
                    if (d < best) best = d;
                }
            bool chokepoint = state.Skeleton.TryGetCell(
                HexGrid.CellOf(target.Hex), out var cell) && cell.IsChokepoint;
            ports.Add((chokepoint, best, target.Id));
        }
        ports.Sort((x, y) => x.Chokepoint != y.Chokepoint
            ? (x.Chokepoint ? -1 : 1)
            : x.Distance != y.Distance ? x.Distance.CompareTo(y.Distance)
            : x.Id.CompareTo(y.Id));
        for (int i = 0; i < ports.Count && i < 3; i++)
            candidates.Add(new WarObjectiveSpec(WarObjectiveType.CapturePort,
                                                ports[i].Id));
        Lane? busiest = null;
        double busiestCapacity = 0;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (state.Ports[lane.PortAId].OwnerActorId != otherId
                && state.Ports[lane.PortBId].OwnerActorId != otherId) continue;
            double capacity = FleetOps.PostedCapacity(state, lane);
            if (capacity > busiestCapacity)
            { busiestCapacity = capacity; busiest = lane; }
        }
        if (busiest != null)
            candidates.Add(new WarObjectiveSpec(WarObjectiveType.BlockadeLane,
                                                busiest.Id));
        candidates.Add(new WarObjectiveSpec(WarObjectiveType.DestroyFleet,
                                            otherId));
        return candidates;
    }
}
