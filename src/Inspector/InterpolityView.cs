using System.Text;
using StarGen.Core.Epoch;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>Slice H's REPL surfaces: the relations panel (per-pair warmth/
/// tension with their live source terms — the pressure gauge readable
/// before wars start) and the war panels (objectives, sieges, commanders,
/// exhaustion — a war you can read like a story). Deterministic text,
/// invariant culture.</summary>
public static class InterpolityView
{
    private static string Bar(double v, int width = 10)
    {
        int fill = (int)System.Math.Round(System.Math.Clamp(v, 0, 1) * width);
        return new string('#', fill) + new string('-', width - fill);
    }

    // ---- the relations panel ----

    public static string RenderRelations(SimState state, int polityId = -1)
    {
        var sb = new StringBuilder();
        int shown = 0;
        foreach (var rel in state.Relations)                  // creation order (P6)
        {
            if (polityId >= 0 && !rel.Involves(polityId)) continue;
            if (!RelationsOps.BothLive(state, rel)) continue;
            shown++;
            var a = state.Actors[rel.PolityAId];
            var b = state.Actors[rel.PolityBId];
            var war = WarOps.ActiveWarBetween(state, rel.PolityAId,
                                              rel.PolityBId);
            sb.AppendLine(Invariant($"#{rel.PolityAId} {a.Name} <> ")
                + Invariant($"#{rel.PolityBId} {b.Name}")
                + (war != null ? $" — AT WAR ({war.Name})" : ""));
            sb.AppendLine(Invariant($"  warmth  {Bar(rel.Warmth)} {rel.Warmth:0.00}   ")
                + Invariant($"tension {Bar(rel.Tension)} {rel.Tension:0.00}"));
            var w = rel.LastWarmthTerms;
            var t = rel.LastTensionTerms;
            sb.AppendLine(Invariant($"  warmth sources: base−strangeness {w[0]:0.00} · ")
                + Invariant($"trade {w[1]:0.00} · treaty {w[2]:0.00} · ")
                + Invariant($"dynastic {w[3]:0.00} · ideology {w[4]:0.00} · ")
                + Invariant($"reputation {w[5]:0.00}"));
            sb.AppendLine(Invariant($"  tension sources: overlap {t[0]:0.00} · ")
                + Invariant($"claims {t[1]:0.00} · interdiction {t[2]:0.00} · ")
                + Invariant($"ideology×zeal {t[3]:0.00} · agitation {t[4]:0.00} · ")
                + Invariant($"militancy {t[5]:0.00}"));
            string bond = rel.VassalPolityId >= 0
                ? Invariant($"vassalage (#{rel.VassalPolityId} kneels, since y{rel.VassalSinceYear})")
                : RungName(rel.Rung)
                  + (rel.RungYear >= 0
                      ? Invariant($" since y{rel.RungYear}") : "");
            sb.Append(Invariant($"  bond: {bond}"));
            if (rel.OfferedRung != TreatyRung.None)
                sb.Append(Invariant($" · on the table: {RungName(rel.OfferedRung)}")
                    + Invariant($" offered by #{rel.OfferedById}"));
            if (rel.DynasticTies > 0)
                sb.Append(Invariant($" · dynastic ties {rel.DynasticTies}"));
            if (rel.LastIncidentYear >= 0)
                sb.Append(Invariant($" · last incident y{rel.LastIncidentYear}"));
            sb.AppendLine();
            foreach (var claim in rel.Claims)
            {
                if (claim.Released) continue;
                sb.AppendLine(Invariant($"  claim: #{claim.HolderPolityId} holds a ")
                    + ClaimName(claim.Type)
                    + Invariant($" claim (subject {claim.SubjectId}, raised ")
                    + SimTraceView.YearLabel(claim.RaisedYear) + ")");
            }
        }
        string header = polityId >= 0
            ? Invariant($"relations of polity #{polityId}: {shown}")
            : Invariant($"live relations: {shown} (of {state.Relations.Count} ever met)");
        return header + "\n" + sb;
    }

    // ---- the war panels ----

    public static string RenderWars(SimState state)
    {
        var sb = new StringBuilder();
        int active = 0;
        foreach (var war in state.Wars)                       // id order (P6)
            if (war.Active) active++;
        sb.AppendLine(Invariant($"wars: {active} burning (of {state.Wars.Count} ever declared)"));
        foreach (var war in state.Wars)
        {
            string status = war.Active ? "ACTIVE"
                : "ended " + SimTraceView.YearLabel(war.EndedYear);
            int taken = 0;
            foreach (var o in war.Objectives)
                if (o.Status == ObjectiveStatus.Taken) taken++;
            sb.AppendLine(Invariant($"  #{war.Id} {war.Name} — ")
                + Invariant($"{state.Actors[war.AttackerId].Name} vs ")
                + Invariant($"{state.Actors[war.DefenderId].Name} · {status} · ")
                + CauseNameShort(war.Cause)
                + Invariant($" · objectives {taken}/{war.Objectives.Count}"));
        }
        return sb.ToString();
    }

    public static string RenderWar(SimState state, int warId)
    {
        if (warId < 0 || warId >= state.Wars.Count) return "no such war";
        var war = state.Wars[warId];
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"#{war.Id} {war.Name}")
            + (war.Active ? "" : " — ended " + SimTraceView.YearLabel(war.EndedYear)));
        sb.AppendLine("  " + SideLine(state, war, attacker: true));
        sb.AppendLine("  " + SideLine(state, war, attacker: false));
        string demand = war.Demand switch
        {
            WarDemand.CedeObjectives => "cede the objectives",
            WarDemand.Reparations => "reparations",
            WarDemand.Vassalize => "vassalage",
            WarDemand.Independence => "independence",
            WarDemand.Annihilation => "ANNIHILATION — no surrender accepted",
            _ => "submission",
        };
        sb.AppendLine(Invariant($"  cause: {CauseNameShort(war.Cause)} · demand: ")
            + demand
            + Invariant($" · declared ") + SimTraceView.YearLabel(war.DeclaredYear));
        sb.AppendLine("  fronts:");
        foreach (var o in war.Objectives)
        {
            string front = o.Type switch
            {
                WarObjectiveType.CapturePort =>
                    Invariant($"capture port #{o.TargetId}")
                    + (o.SiegeYears > 0 && o.Status == ObjectiveStatus.Contested
                        ? Invariant($" — under siege ({o.SiegeYears} ")
                          + (o.SiegeYears == 1 ? "year" : "years")
                          + Invariant($", falls at {WarConduct.SiegeThreshold(state, war, state.Ports[o.TargetId])})")
                        : ""),
                WarObjectiveType.BlockadeLane =>
                    Invariant($"blockade lane #{o.TargetId}")
                    + (o.SiegeYears > 0 ? Invariant($" — cut {o.SiegeYears} ")
                        + (o.SiegeYears == 1 ? "year" : "years") : ""),
                _ => "break the enemy fleet",
            };
            sb.AppendLine(Invariant($"    [{StatusGlyph(o.Status)}] ") + front);
        }
        // war fleets on station, with their commanders
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != war.AttackerId || fleet.TotalHulls == 0
                || fleet.Posture is not (FleetPosture.Blockade
                    or FleetPosture.Expedition)) continue;
            string commander = fleet.CommanderId >= 0
                ? " under " + state.Characters[fleet.CommanderId].Name : "";
            sb.AppendLine(Invariant($"  fleet #{fleet.Id}: {fleet.TotalHulls} hulls at ")
                + Invariant($"({fleet.Hex.Q},{fleet.Hex.R})") + commander
                + Invariant($" (readiness {fleet.Readiness:0.00})"));
        }
        // the war's chronicle so far
        foreach (var e in state.Log.Events)
            if ((e.Payload is BattleFoughtPayload b && b.WarId == war.Id)
                || (e.Payload is SiegeBegunPayload sg && sg.WarId == war.Id)
                || (e.Payload is PortCapturedPayload pc && pc.WarId == war.Id)
                || (e.Payload is PeaceSettledPayload ps && ps.WarId == war.Id))
                sb.AppendLine("  " + SimTraceView.Describe(e));
        return sb.ToString();
    }

    private static string SideLine(SimState state, War war, bool attacker)
    {
        int leader = attacker ? war.AttackerId : war.DefenderId;
        var allies = attacker ? war.AttackerAllies : war.DefenderAllies;
        double exhaustion = attacker
            ? war.AttackerExhaustion : war.DefenderExhaustion;
        double atStart = attacker
            ? war.AttackerStrengthAtStart : war.DefenderStrengthAtStart;
        double now = WarOps.SideStrength(state, war, attacker);
        var sb = new StringBuilder();
        sb.Append(attacker ? "attacker: " : "defender: ");
        sb.Append(Invariant($"#{leader} {state.Actors[leader].Name}"));
        foreach (var ally in allies)
            sb.Append(Invariant($" + #{ally} {state.Actors[ally].Name}"));
        sb.Append(Invariant($" — exhaustion {Bar(exhaustion)} {exhaustion:0.00}"));
        if (atStart > 0)
            sb.Append(Invariant($" · strength {now / atStart:0%} of mustered"));
        return sb.ToString();
    }

    private static char StatusGlyph(ObjectiveStatus status) => status switch
    {
        ObjectiveStatus.Taken => 'x',
        ObjectiveStatus.Abandoned => '~',
        _ => ' ',
    };

    private static string RungName(TreatyRung rung) => rung switch
    {
        TreatyRung.TradePact => "trade pact",
        TreatyRung.NonAggression => "non-aggression pact",
        TreatyRung.DefenseAlliance => "defense alliance",
        _ => "none",
    };

    private static string ClaimName(ClaimType type) => type switch
    {
        ClaimType.CulturalKin => "cultural-kin",
        ClaimType.LostTerritory => "lost-territory",
        ClaimType.Succession => "succession",
        ClaimType.Liberation => "liberation",
        _ => "standing",
    };

    private static string CauseNameShort(CasusBelli cause) => cause switch
    {
        CasusBelli.ResourceSeizure => "resource seizure",
        CasusBelli.ChokepointControl => "chokepoint control",
        CasusBelli.PunitiveInterdiction => "punitive response",
        CasusBelli.Crusade => "crusade",
        CasusBelli.Liberation => "liberation",
        CasusBelli.Containment => "containment",
        CasusBelli.SuccessionClaim => "succession claim",
        CasusBelli.GrievanceDischarge => "grievance discharge",
        CasusBelli.VassalSecession => "secession",
        CasusBelli.BorderIncident => "border incident",
        CasusBelli.CivilWar => "civil war",
        CasusBelli.Expulsion => "expulsion",
        _ => "war",
    };
}
