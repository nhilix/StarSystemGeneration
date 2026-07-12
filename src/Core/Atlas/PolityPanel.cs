using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>The reign line: ruler, house, and the log-derived reign
/// start (last RulerAscended/CoupStruck, else birth).</summary>
public sealed record RulerLine(int CharacterId, string Name,
    string? HouseName, double HousePrestige, long ReignFromYear,
    long Age, double Renown);

/// <summary>An heir or marshal beside the throne.</summary>
public sealed record CourtLine(int CharacterId, CharacterRole Role,
                               string Name, long Age);

/// <summary>One tech domain's tier and progress toward the next
/// (`tech` parity: TechProgress / Tech.Threshold).</summary>
public sealed record TechLine(int Domain, string DomainName, int Tier,
                              double ProgressFraction);

/// <summary>One organized faction (`polity` parity).</summary>
public sealed record FactionRow(int Id, string Name, FactionBasis Basis,
    double Strength, double Grievance, double Militancy, double Wealth,
    string LeaderName);

/// <summary>One hosted corporation charter line; CorpId links the
/// Corporation panel.</summary>
public sealed record CharterRow(int CorpId, string Name,
                                CorporateNiche Niche, double Credits);

/// <summary>One standing-plan entry with `eplan`'s in-flight star: a
/// matching project (kind + port + type) already broke ground.</summary>
public sealed record PlanRow(int Index, PlanEntryKind Kind,
    ProjectPriority Priority, int StartYear, string TypeDesign,
    int PortId, bool InFlight);

/// <summary>The Polity panel's card — `polity`/`tech` typed, plus the T1
/// additions: ReservePoints (the reserve treasury actors v7 draws project
/// baskets from) and the standing plan.</summary>
public sealed record PolityCard(
    int ActorId, string Name, HexCoordinate Seat, bool Entered,
    string? FormName, double Legitimacy, double Cohesion,
    double Enforcement, IReadOnlyList<double> OfficialLine,
    RulerLine? Ruler, IReadOnlyList<CourtLine> Court,
    IReadOnlyList<TechLine> Tech, IReadOnlyList<FactionRow> Factions,
    IReadOnlyList<CharterRow> Charters, double Credits,
    double ReservePoints, IReadOnlyList<PlanRow> Plan);

/// <summary>K3: the domain click / topbar-search target —
/// InteriorView.RenderPolity + RenderTech parity, `eplan` in-flight
/// derivation, ReservePoints straight from the polity record.</summary>
public static class PolityPanel
{
    private static readonly string[] DomainNames =
        { "industrial", "military", "astrogation", "life" };

    public static PolityCard? Card(AtlasReadModel model, EyeContext eye,
                                   int actorId)
    {
        var state = model.State;
        if (actorId < 0 || actorId >= state.Actors.Count
            || state.Actors[actorId].Kind != ActorKind.Polity)
            return null;
        var actor = state.Actors[actorId];
        var pr = state.PolityOf(actorId);
        var interior = pr.Interior;

        var officialLine = new double[4];
        RulerLine? rulerLine = null;
        if (interior != null)
        {
            for (int i = 0; i < 4; i++)   // the renderer's display axis
                officialLine[i] = 1 - interior.OfficialIdeology[i];
            if (interior.RulerCharacterId >= 0)
            {
                var ruler = state.Characters[interior.RulerCharacterId];
                long reignFrom = ruler.BirthYear;
                foreach (var e in state.Log.ForCharacter(ruler.Id))
                    if (e.Type is WorldEventType.RulerAscended
                        or WorldEventType.CoupStruck)
                        reignFrom = e.WorldYear;
                string? house = ruler.DynastyId >= 0
                    ? state.Dynasties[ruler.DynastyId].Name : null;
                double prestige = ruler.DynastyId >= 0
                    ? state.Dynasties[ruler.DynastyId].Prestige : 0;
                rulerLine = new RulerLine(ruler.Id, ruler.Name, house,
                    prestige, reignFrom, state.WorldYear - ruler.BirthYear,
                    ruler.Renown);
            }
        }

        var court = new List<CourtLine>();
        foreach (var c in state.Characters)               // id order (P6)
            if (c.Alive && c.PolityId == actorId
                && c.Role is CharacterRole.Heir or CharacterRole.Marshal)
                court.Add(new CourtLine(c.Id, c.Role, c.Name,
                    state.WorldYear - c.BirthYear));

        var tech = new List<TechLine>(4);
        for (int d = 0; d < 4; d++)
            tech.Add(new TechLine(d, DomainNames[d], pr.TechTier[d],
                pr.TechProgress[d]
                / Epoch.Tech.Threshold(state.Config, pr.TechTier[d])));

        var factions = new List<FactionRow>();
        foreach (var f in state.Factions)                 // id order (P6)
        {
            if (f.PolityId != actorId || !f.Active) continue;
            string leader = f.LeaderCharacterId >= 0
                ? state.Characters[f.LeaderCharacterId].Name : "-";
            factions.Add(new FactionRow(f.Id, f.Name, f.Basis, f.Strength,
                f.Grievance, f.Militancy, f.Wealth, leader));
        }

        var charters = new List<CharterRow>();
        foreach (var corp in state.Corporations)          // id order (P6)
            if (corp.Active && corp.HostPolityId == actorId)
                charters.Add(new CharterRow(corp.Id, corp.Name, corp.Niche,
                                            corp.Credits));

        return new PolityCard(actorId, actor.Name, actor.Seat,
            actor.Entered,
            interior != null ? GovernmentForms.Get(interior.FormId).Name : null,
            interior?.Legitimacy ?? 0, interior?.Cohesion ?? 0,
            interior?.Enforcement ?? 0, officialLine, rulerLine, court,
            tech, factions, charters, pr.Credits, pr.ReservePoints,
            PlanRows(state, actorId));
    }

    /// <summary>The standing plan with `eplan`'s in-flight star — an entry
    /// is live when a project of the matching kind, port, and type is
    /// still in flight (Repl.RenderPlan parity).</summary>
    private static List<PlanRow> PlanRows(SimState state, int actorId)
    {
        var rows = new List<PlanRow>();
        if (state.Actors[actorId].Policies is not PolityPolicies policies)
            return rows;
        var entries = policies.Plan.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var matchKind = e.Kind switch
            {
                PlanEntryKind.Facility => ProjectKind.FacilityConstruction,
                PlanEntryKind.PortRaise => ProjectKind.PortRaise,
                PlanEntryKind.HullBatch => ProjectKind.HullBatch,
                _ => (ProjectKind)(-1),
            };
            bool inFlight = false;
            foreach (var p in state.Projects)             // id order (P6)
                if (p.InFlight && p.Kind == matchKind && p.PortId == e.PortId
                    && p.TypeId == e.TypeId)
                { inFlight = true; break; }
            string typeDesign = e.Kind switch
            {
                PlanEntryKind.Facility when e.TypeId >= 0 =>
                    ((InfraTypeId)e.TypeId).ToString(),
                PlanEntryKind.HullBatch when e.TypeId >= 0
                        && e.TypeId < state.Designs.Count =>
                    System.FormattableString.Invariant(
                        $"{state.Designs[e.TypeId].Name} Mk {state.Designs[e.TypeId].Mark} x{e.Count}"),
                PlanEntryKind.PortRaise => "(port raise)",
                _ => "—",
            };
            rows.Add(new PlanRow(i, e.Kind, e.Priority, e.StartYear,
                                 typeDesign, e.PortId, inFlight));
        }
        return rows;
    }
}
