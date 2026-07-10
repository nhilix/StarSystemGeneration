using System.Collections.Generic;
using System.Linq;
using System.Text;
using StarGen.Core.Epoch;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>Slice G's REPL surfaces: the polity panel (a polity you can read
/// like a story — form, reign, factions with bars, tech tiers), the sparse
/// character roster, the P8 biography view, the tech table, and the
/// corporation registry. Deterministic text, invariant culture.</summary>
public static class InteriorView
{
    private static string Bar(double v, int width = 10)
    {
        int fill = (int)System.Math.Round(System.Math.Clamp(v, 0, 1) * width);
        return new string('#', fill) + new string('-', width - fill);
    }

    // ---- the polity panel ----

    public static string RenderPolity(SimState state, int actorId)
    {
        if (actorId < 0 || actorId >= state.Actors.Count
            || state.Actors[actorId].Kind != ActorKind.Polity)
            return "no such polity";
        var actor = state.Actors[actorId];
        var pr = state.PolityOf(actorId);
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"#{actorId} {actor.Name} — seat ")
            + Invariant($"({actor.Seat.Q},{actor.Seat.R})")
            + (actor.Entered ? "" : " [not yet entered]"));
        var interior = pr.Interior;
        if (interior == null) return sb.ToString();

        var form = GovernmentForms.Get(interior.FormId);
        sb.AppendLine(Invariant($"  {form.Name} — legitimacy ")
            + Invariant($"{Bar(interior.Legitimacy)} {interior.Legitimacy:0.00} · ")
            + Invariant($"cohesion {Bar(interior.Cohesion)} {interior.Cohesion:0.00} · ")
            + Invariant($"enforcement {interior.Enforcement:0.00}"));
        sb.AppendLine(Invariant($"  official line: authority {1 - interior.OfficialIdeology[0]:0.00} · ")
            + Invariant($"communal {1 - interior.OfficialIdeology[1]:0.00} · ")
            + Invariant($"open {1 - interior.OfficialIdeology[2]:0.00} · ")
            + Invariant($"sacral {1 - interior.OfficialIdeology[3]:0.00}"));

        // the reign
        if (interior.RulerCharacterId >= 0)
        {
            var ruler = state.Characters[interior.RulerCharacterId];
            long reignFrom = ruler.BirthYear;
            foreach (var e in state.Log.ForCharacter(ruler.Id))
                if (e.Type is WorldEventType.RulerAscended
                    or WorldEventType.CoupStruck)
                    reignFrom = e.WorldYear;
            string house = ruler.DynastyId >= 0
                ? $" of house {state.Dynasties[ruler.DynastyId].Name}"
                  + Invariant($" (prestige {state.Dynasties[ruler.DynastyId].Prestige:0.0})")
                : "";
            sb.AppendLine(Invariant($"  ruler: {ruler.Name}{house} — reigning since ")
                + SimTraceView.YearLabel(reignFrom)
                + Invariant($", age {state.WorldYear - ruler.BirthYear}, renown {ruler.Renown:0.0}"));
        }
        foreach (var c in state.Characters)
            if (c.Alive && c.PolityId == actorId
                && c.Role is CharacterRole.Heir or CharacterRole.Marshal)
                sb.AppendLine(Invariant($"  {c.Role.ToString().ToLowerInvariant()}: ")
                    + c.Name + Invariant($", age {state.WorldYear - c.BirthYear}"));

        // tech tiers
        var t = pr.TechTier;
        sb.AppendLine(Invariant($"  tech: industrial {t[0]} · military {t[1]} · ")
            + Invariant($"astrogation {t[2]} · life {t[3]}"));

        // factions with strength/grievance bars
        bool any = false;
        foreach (var f in state.Factions)
        {
            if (f.PolityId != actorId || !f.Active) continue;
            if (!any) { sb.AppendLine("  factions:"); any = true; }
            string leader = f.LeaderCharacterId >= 0
                ? state.Characters[f.LeaderCharacterId].Name : "-";
            sb.AppendLine(Invariant($"    {f.Name} ({f.Basis.ToString().ToLowerInvariant()}) — ")
                + Invariant($"strength {Bar(f.Strength)} {f.Strength:0.00} · ")
                + Invariant($"grievance {Bar(System.Math.Min(1, f.Grievance))} {f.Grievance:0.00} · ")
                + Invariant($"militancy {f.Militancy:0.00} · chest {f.Wealth:0} — led by {leader}"));
        }
        if (!any) sb.AppendLine("  factions: none organized");

        // hosted corporations
        foreach (var corp in state.Corporations)
            if (corp.Active && corp.HostPolityId == actorId)
                sb.AppendLine(Invariant($"  charters: the {corp.Name} ")
                    + Invariant($"({corp.Niche.ToString().ToLowerInvariant()}, ")
                    + Invariant($"{corp.Credits:0} credits)"));
        return sb.ToString();
    }

    // ---- the sparse roster ----

    public static string RenderCharacters(SimState state, int polityId = -1)
    {
        var sb = new StringBuilder();
        int shown = 0;
        foreach (var c in state.Characters)                   // id order (P6)
        {
            if (polityId >= 0 && c.PolityId != polityId) continue;
            if (!c.Alive) continue;
            shown++;
            string role = c.Role.ToString().ToLowerInvariant();
            if (c.Notable != NotableType.None)
                role += $" ({c.Notable.ToString().ToLowerInvariant()})";
            sb.AppendLine(Invariant($"  #{c.Id,3} {c.Name,-16} {role,-24} ")
                + Invariant($"polity {c.PolityId,2} · age {state.WorldYear - c.BirthYear,4} · ")
                + Invariant($"renown {c.Renown:0.0}"));
        }
        string header = polityId >= 0
            ? Invariant($"living characters of polity #{polityId}: {shown}")
            : Invariant($"living characters: {shown} (of {state.Characters.Count} ever)");
        return header + "\n" + sb;
    }

    /// <summary>P8: a life reconstructed from the log with no extra
    /// authoring — born, rose, led, fell.</summary>
    public static string RenderBiography(SimState state, int characterId)
    {
        if (characterId < 0 || characterId >= state.Characters.Count)
            return "no such character";
        var c = state.Characters[characterId];
        var sb = new StringBuilder();
        string species = c.SpeciesId >= 0
            && c.SpeciesId < state.Skeleton.Species.Count
            ? state.Skeleton.Species[c.SpeciesId].Name : "?";
        sb.AppendLine(Invariant($"#{c.Id} {c.Name} — {species}, ")
            + (c.Alive
                ? Invariant($"age {state.WorldYear - c.BirthYear}")
                : Invariant($"{SimTraceView.YearLabel(c.BirthYear)}–{SimTraceView.YearLabel(c.DeathYear)}")));
        string role = c.Role.ToString().ToLowerInvariant();
        if (c.Notable != NotableType.None)
            role += $", {c.Notable.ToString().ToLowerInvariant()}";
        if (c.DynastyId >= 0)
            role += $", house {state.Dynasties[c.DynastyId].Name}";
        sb.AppendLine(Invariant($"  {role} of polity #{c.PolityId} · renown {c.Renown:0.0}"));
        sb.AppendLine(Invariant($"  boldness {c.Boldness:0.00} · zeal {c.Zeal:0.00} · ")
            + Invariant($"competence {c.Competence:0.00} · ambition {c.Ambition:0.00}"));
        int events = 0;
        foreach (var e in state.Log.ForCharacter(characterId))
        {
            events++;
            sb.AppendLine("  " + SimTraceView.Describe(e));
        }
        if (events == 0)
            sb.AppendLine("  (no chronicle presence — a quiet life)");
        return sb.ToString();
    }

    // ---- the tech table ----

    public static string RenderTech(SimState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("tech tiers (progress toward next) — "
                      + "industrial · military · astrogation · life");
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered) continue;
            sb.Append(Invariant($"  #{pr.ActorId,2} {state.Actors[pr.ActorId].Name,-16}"));
            for (int d = 0; d < 4; d++)
            {
                double threshold = Tech.Threshold(state.Config, pr.TechTier[d]);
                sb.Append(Invariant($"  t{pr.TechTier[d]} ({pr.TechProgress[d] / threshold:00%})"));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ---- the corporation registry ----

    public static string RenderCorporations(SimState state)
    {
        if (state.Corporations.Count == 0) return "no corporations yet";
        var sb = new StringBuilder();
        foreach (var corp in state.Corporations)              // id order (P6)
        {
            int facilities = state.Facilities.Count(
                f => f.OwnerActorId == corp.ActorId);
            int hulls = 0;
            foreach (var f in state.Fleets)
                if (f.OwnerActorId == corp.ActorId) hulls += f.TotalHulls;
            string exec = corp.ExecutiveCharacterId >= 0
                ? state.Characters[corp.ExecutiveCharacterId].Name : "-";
            string host = corp.HostPolityId >= 0
                ? Invariant($"polity #{corp.HostPolityId}") : "chartered nowhere";
            sb.AppendLine(Invariant($"  #{corp.Id,3} {corp.Name,-24} ")
                + Invariant($"{corp.Niche.ToString().ToLowerInvariant(),-11} {host,-18} ")
                + (corp.Active
                    ? Invariant($"credits {corp.Credits,8:0} · {facilities} facilities · ")
                      + Invariant($"{hulls} hulls · exec {exec}")
                    : Invariant($"[dead, founded {SimTraceView.YearLabel(corp.FoundedYear)}]")));
        }
        return sb.ToString();
    }
}
