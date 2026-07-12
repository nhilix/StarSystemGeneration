using System.Collections.Generic;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>One corporation-registry row (`corps` parity: facilities and
/// hulls counted over the corp's actor id) plus the projects its treasury
/// feeds (Project.FunderActorId — corp standing plans do NOT exist yet;
/// deferred to the contract-economy slice).</summary>
public sealed record CorpRow(int Id, int ActorId, string Name,
    CorporateNiche Niche, int HostPolityId, string? HostName, bool Active,
    double Credits, int FacilityCount, int Hulls, string? ExecutiveName,
    long FoundedYear, IReadOnlyList<int> FundedProjectIds);

/// <summary>K3: market/polity charter links — InteriorView
/// RenderCorporations parity.</summary>
public static class CorporationPanel
{
    public static List<CorpRow> Rows(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var rows = new List<CorpRow>();
        foreach (var corp in state.Corporations)          // id order (P6)
        {
            int facilities = 0;
            foreach (var f in state.Facilities)           // id order (P6)
                if (f.OwnerActorId == corp.ActorId) facilities++;
            int hulls = 0;
            foreach (var f in state.Fleets)               // id order (P6)
                if (f.OwnerActorId == corp.ActorId) hulls += f.TotalHulls;
            var funded = new List<int>();
            foreach (var p in state.Projects)             // id order (P6)
                if (p.InFlight && p.FunderActorId == corp.ActorId)
                    funded.Add(p.Id);
            rows.Add(new CorpRow(corp.Id, corp.ActorId, corp.Name,
                corp.Niche, corp.HostPolityId,
                corp.HostPolityId >= 0
                    ? state.Actors[corp.HostPolityId].Name : null,
                corp.Active, corp.Credits, facilities, hulls,
                corp.ExecutiveCharacterId >= 0
                    ? state.Characters[corp.ExecutiveCharacterId].Name : null,
                corp.FoundedYear, funded));
        }
        return rows;
    }
}
