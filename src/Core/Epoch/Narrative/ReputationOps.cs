using System;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Stances and reputation (perception-and-news.md §Stances and
/// reputation): news arrival moves the observer's stance toward the actors
/// involved, filtered through the observer's temperament — militant
/// cultures respect bold conquest, open traders sanction treaty-breakers,
/// and dogmatic distance amplifies condemnation. Reputation is derived,
/// never stored globally: the stance table is per-audience, and the same
/// warlord is a monster to one audience and a hero to another. Stances
/// feed the warmth target (one wire reprices first contact, treaty gates,
/// and the war appetite) and decay toward indifference as memory fades.</summary>
public static class ReputationOps
{
    /// <summary>Memory fades: every stance drifts toward 0. Runs once per
    /// Perception, before this step's news lands.</summary>
    public static void DecayStances(SimState state)
    {
        double keep = Math.Max(0.0, 1.0 - state.Config.News.StanceDecayPerYear
                                          * state.Config.Sim.YearsPerEpoch);
        foreach (var a in state.Actors)                   // id order (P6)
        {
            var stances = a.Beliefs.Stances;
            for (int i = 0; i < stances.Count; i++)
                stances[stances.Keys[i]] *= keep;
        }
    }

    /// <summary>The observer's stance toward another actor: valenced
    /// attitude in [−1, 1], 0 when it has never heard a thing.</summary>
    public static double StanceOf(SimState state, int observerId, int subjectId)
        => state.Actors[observerId].Beliefs.Stances
            .TryGetValue(subjectId, out double s) ? s : 0.0;

    /// <summary>The pair-mean stance — the reputation term the warmth
    /// target consumes (a relation is repriced by what BOTH courts have
    /// heard about each other).</summary>
    public static double MutualStance(SimState state, int aId, int bId)
        => 0.5 * (StanceOf(state, aId, bId) + StanceOf(state, bId, aId));

    /// <summary>One arrived event judged by one observer: conduct deltas
    /// per involved actor, temperament-filtered, attenuated by the word's
    /// age (far polities hear diminished rumors). The per-event bases and
    /// tilts are structural constants (TUNING §Structural constants) —
    /// the calibration dials are the decay and the warmth weight.</summary>
    public static void Judge(SimState state, Actor observer, WorldEvent e,
                             double attenuation)
    {
        if (observer.Kind != ActorKind.Polity) return;
        var temperament = Temperament.Compose(state,
                                              state.PolityOf(observer.Id));
        double militancy = temperament.Militancy - 0.5;
        double openness = temperament.Openness - 0.5;
        switch (e.Type)
        {
            case WorldEventType.TreatyBroken
                when e.Payload is TreatyBrokenPayload tb:
                // open traders sanction treaty-breakers
                Move(state, observer, tb.BreakerPolityId,
                     (-0.15 - 0.20 * openness) * attenuation);
                break;
            case WorldEventType.WarDeclared
                when e.Payload is WarDeclaredPayload wd:
                // militant cultures respect bold conquest; a war of
                // annihilation horrifies everyone else
                Move(state, observer, wd.AttackerId,
                     ((WarDemand)wd.Demand == WarDemand.Annihilation
                         ? -0.20 + 0.20 * militancy
                         : -0.08 + 0.20 * militancy) * attenuation);
                break;
            case WorldEventType.SiegeBegun when e.Actors.Count > 0:
                // starvation sieges grind on larders — conduct reputation
                Move(state, observer, e.Actors[0],
                     (-0.08 + 0.10 * militancy) * attenuation);
                break;
            case WorldEventType.PeaceSettled
                when e.Payload is PeaceSettledPayload ps && ps.WinnerId >= 0:
                Move(state, observer, ps.WinnerId,
                     ((WarOutcome)ps.Outcome == WarOutcome.Annexed
                         // the loser is no more: infamy, unless you admire it
                         ? -0.25 + 0.30 * militancy
                         // an honored surrender: restraint reads well
                         : 0.05) * attenuation);
                break;
            case WorldEventType.EmergenceSuppressed
                when e.Payload is EmergenceSuppressedPayload es:
                Move(state, observer, es.HostPolityId,
                     (-0.10 - 0.20 * openness) * attenuation);
                break;
            case WorldEventType.NativesIntegrated
                when e.Payload is NativesIntegratedPayload ni:
                Move(state, observer, ni.HostPolityId,
                     (0.04 + 0.08 * openness) * attenuation);
                break;
            case WorldEventType.CorporationNationalized
                when e.Payload is CorporationNationalizedPayload cn:
                // the scandal the legitimacy hit only hinted at: capital
                // sanctions the seizing state (slice I arms the stub)
                Move(state, observer, cn.PolityId,
                     (-0.08 - 0.15 * openness) * attenuation);
                break;
            case WorldEventType.LoanDefaulted
                when e.Payload is LoanDefaultedPayload ld:
                Move(state, observer, ld.BorrowerActorId, -0.05 * attenuation);
                break;
            case WorldEventType.RevoltCrushed when e.Actors.Count > 0:
                Move(state, observer, e.Actors[0],
                     (-0.08 - 0.10 * openness) * attenuation);
                break;
            case WorldEventType.TreatySigned
                when e.Payload is TreatySignedPayload ts:
                // honored alliances land here too, modestly
                Move(state, observer, ts.PolityAId, 0.03 * attenuation);
                Move(state, observer, ts.PolityBId, 0.03 * attenuation);
                break;
        }
    }

    /// <summary>Rumors attenuate: the word's age against the pulse horizon,
    /// floored — even old news moves a stance a little.</summary>
    public static double Attenuation(SimState state, double ageYears)
    {
        double horizon = state.Config.News.PulseMaxYears;
        if (horizon <= 0) return 1.0;
        return Math.Max(0.25, 1.0 - 0.75 * Math.Min(1.0, ageYears / horizon));
    }

    /// <summary>Regional events spread by contact, not pulse: last epoch's
    /// regional news reaches every polity close enough to have heard it
    /// within the epoch (chronicle-and-poi.md §Visibility). Stateless —
    /// derived from the log tail each Perception.</summary>
    public static void SpreadRegional(SimState state,
                                      BeliefOps.NewsFieldCache fields)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        long lastEpochYear = state.WorldYear - years;
        var events = state.Log.Events;
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.WorldYear < lastEpochYear) break;
            if (e.WorldYear != lastEpochYear
                || e.Visibility != EventVisibility.Regional) continue;
            double[]? field = null;
            foreach (var a in state.Actors)               // id order (P6)
            {
                if (!a.Entered || a.Kind != ActorKind.Polity) continue;
                field ??= fields.FieldFor(state, e.Location);
                if (NewsOps.DelayYears(state, a.Id, field, e.Location) > years)
                    continue;
                Judge(state, a, e, attenuation: 1.0);
            }
        }
    }

    /// <summary>Apply one stance delta, clamped to [−1, 1]. A dogmatic
    /// audience condemns the doctrinally distant harder (theocracies judge
    /// by dogma): negative deltas amplify with the ideology gap.</summary>
    private static void Move(SimState state, Actor observer, int subjectId,
                             double delta)
    {
        if (subjectId == observer.Id || delta == 0.0) return;
        if (subjectId < 0 || subjectId >= state.Actors.Count
            || state.Actors[subjectId].Kind != ActorKind.Polity) return;
        if (delta < 0)
            delta *= 1.0 + RelationsOps.IdeologyGap(
                state.PolityOf(observer.Id), state.PolityOf(subjectId));
        var stances = observer.Beliefs.Stances;
        stances.TryGetValue(subjectId, out double current);
        stances[subjectId] = Math.Max(-1.0, Math.Min(1.0, current + delta));
    }
}
