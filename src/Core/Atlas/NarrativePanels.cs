using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

// ---- belief: the fog of war made visible ----

/// <summary>What one polity believes about another, beside the truth
/// (`belief` parity): the visible gap between what happened and who knows
/// it yet.</summary>
public sealed record BeliefRow(int SubjectId, string SubjectName,
    double BelievedStrength, double TruthStrength,
    double DefensiveStrength, int MenuCount, long StaleYears);

/// <summary>A believed war beside its truth (active wars only).</summary>
public sealed record WarBeliefRow(int WarId, string WarName,
    double BelievedExhaustion, double TruthExhaustion,
    double StrengthShare, long StaleYears);

/// <summary>K3: the Polity panel's Belief tab — NarrativeView
/// RenderBeliefs parity.</summary>
public static class BeliefPanel
{
    public static List<BeliefRow> Rows(AtlasReadModel model, EyeContext eye,
        int observerId, int subjectId = -1)
    {
        var state = model.State;
        var rows = new List<BeliefRow>();
        if (observerId < 0 || observerId >= state.Actors.Count) return rows;
        var observer = state.Actors[observerId];
        foreach (var b in observer.Beliefs.Polities.Values) // subject order
        {
            if (subjectId >= 0 && b.SubjectId != subjectId) continue;
            rows.Add(new BeliefRow(b.SubjectId,
                state.Actors[b.SubjectId].Name, b.Strength,
                FleetOps.WarStrength(state, b.SubjectId),
                b.DefensiveStrength, b.Menu.Count,
                state.WorldYear - b.HeardYear));
        }
        return rows;
    }

    public static List<WarBeliefRow> WarRows(AtlasReadModel model,
        EyeContext eye, int observerId)
    {
        var state = model.State;
        var rows = new List<WarBeliefRow>();
        if (observerId < 0 || observerId >= state.Actors.Count) return rows;
        var observer = state.Actors[observerId];
        foreach (var wb in observer.Beliefs.Wars.Values)  // war-id order
        {
            var war = state.Wars[wb.WarId];
            if (!war.Active) continue;
            double truth = war.OnAttackerSide(observerId)
                ? war.AttackerExhaustion : war.DefenderExhaustion;
            rows.Add(new WarBeliefRow(war.Id, war.Name,
                wb.OwnSideExhaustion, truth, wb.OwnSideStrengthShare,
                state.WorldYear - wb.HeardYear));
        }
        return rows;
    }
}

// ---- news: pulses and their journeys ----

/// <summary>One pulse still spreading (`news` parity liveness: age within
/// PulseMaxYears and someone entered has not heard).</summary>
public sealed record PulseRow(int Id, long EmitYear, double AgeYears,
    HexCoordinate Origin, int HeardByCount, int EnteredCount,
    string EventText);

/// <summary>One delivery stop on a pulse's journey.</summary>
public sealed record DeliveryRow(int ActorId, string ActorName, long Year,
                                 long TransitYears);

/// <summary>A pulse's full journey (`news &lt;id&gt;` parity).</summary>
public sealed record PulseCard(int Id, long EmitYear, HexCoordinate Origin,
    string EventText, IReadOnlyList<DeliveryRow> Deliveries);

/// <summary>K3: the news lens's panel — NarrativeView RenderNews parity.</summary>
public static class NewsPanel
{
    /// <summary>Pulses still spreading, newest first (the renderer's
    /// reading order).</summary>
    public static List<PulseRow> LivePulses(AtlasReadModel model,
                                            EyeContext eye)
    {
        var state = model.State;
        int entered = 0;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) entered++;
        var rows = new List<PulseRow>();
        for (int i = state.Pulses.Count - 1; i >= 0; i--)
        {
            var p = state.Pulses[i];
            double age = state.WorldYear - p.EmitYear;
            if (age > state.Config.News.PulseMaxYears
                || p.Delivered.Count >= entered) continue;
            rows.Add(new PulseRow(p.Id, p.EmitYear, age, p.Origin,
                p.Delivered.Count, entered,
                SimTraceView.Describe(state.Log.Events[(int)p.EventId])));
        }
        return rows;
    }

    public static PulseCard? Journey(AtlasReadModel model, EyeContext eye,
                                     int pulseId)
    {
        var state = model.State;
        if (pulseId < 0 || pulseId >= state.Pulses.Count) return null;
        var p = state.Pulses[pulseId];
        var deliveries = new List<DeliveryRow>();
        foreach (var (actorId, year) in p.Delivered)
            deliveries.Add(new DeliveryRow(actorId,
                state.Actors[actorId].Name, year, year - p.EmitYear));
        return new PulseCard(p.Id, p.EmitYear, p.Origin,
            SimTraceView.Describe(state.Log.Events[(int)p.EventId]),
            deliveries);
    }
}

// ---- stances: reputation per audience ----

/// <summary>The renderer's judgment bands: a monster at ≤ −0.3, a hero
/// at ≥ 0.3 to this audience.</summary>
public enum StanceVerdict { Monster, Neutral, Hero }

/// <summary>One news-arrived judgment (`stances` parity).</summary>
public sealed record StanceRow(int ObserverId, string ObserverName,
    int SubjectId, string SubjectName, double Stance,
    StanceVerdict Verdict);

/// <summary>K3: NarrativeView RenderStances parity.</summary>
public static class StancesPanel
{
    public static StanceVerdict VerdictOf(double stance) =>
        stance <= -0.3 ? StanceVerdict.Monster
        : stance >= 0.3 ? StanceVerdict.Hero
        : StanceVerdict.Neutral;

    public static List<StanceRow> Rows(AtlasReadModel model, EyeContext eye,
                                       int observerId = -1)
    {
        var state = model.State;
        var rows = new List<StanceRow>();
        foreach (var a in state.Actors)                   // id order (P6)
        {
            if (observerId >= 0 && a.Id != observerId) continue;
            for (int i = 0; i < a.Beliefs.Stances.Count; i++)
            {
                int subject = a.Beliefs.Stances.Keys[i];
                double stance = a.Beliefs.Stances.Values[i];
                rows.Add(new StanceRow(a.Id, a.Name, subject,
                    state.Actors[subject].Name, stance, VerdictOf(stance)));
            }
        }
        return rows;
    }
}
