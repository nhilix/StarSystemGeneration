using System.Collections.Generic;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>One standing claim on a pair.</summary>
public sealed record ClaimRow(ClaimType Type, int HolderPolityId,
                              string HolderName, int SubjectId,
                              long RaisedYear);

/// <summary>One live pair (`relations` parity): warmth/tension with their
/// six source terms each (base−strangeness · trade · treaty · dynastic ·
/// ideology · reputation / overlap · claims · interdiction · ideology×zeal
/// · agitation · militancy), the bond, and standing claims.</summary>
public sealed record RelationRow(int PolityAId, string PolityAName,
    int PolityBId, string PolityBName, int? WarId, string? WarName,
    double Warmth, double Tension, IReadOnlyList<double> WarmthTerms,
    IReadOnlyList<double> TensionTerms, TreatyRung Rung, long RungYear,
    int VassalPolityId, long VassalSinceYear, TreatyRung OfferedRung,
    int OfferedById, int DynasticTies, long LastIncidentYear,
    IReadOnlyList<ClaimRow> Claims);

/// <summary>K3: the Relations tab — InterpolityView.RenderRelations
/// parity: BothLive pairs only, creation order (P6).</summary>
public static class RelationsPanel
{
    public static List<RelationRow> Rows(AtlasReadModel model,
        EyeContext eye, int polityId = -1)
    {
        var state = model.State;
        var rows = new List<RelationRow>();
        foreach (var rel in state.Relations)              // creation order
        {
            if (polityId >= 0 && !rel.Involves(polityId)) continue;
            if (!RelationsOps.BothLive(state, rel)) continue;
            var war = WarOps.ActiveWarBetween(state, rel.PolityAId,
                                              rel.PolityBId);
            var claims = new List<ClaimRow>();
            foreach (var claim in rel.Claims)
            {
                if (claim.Released) continue;
                claims.Add(new ClaimRow(claim.Type, claim.HolderPolityId,
                    state.Actors[claim.HolderPolityId].Name,
                    claim.SubjectId, claim.RaisedYear));
            }
            rows.Add(new RelationRow(rel.PolityAId,
                state.Actors[rel.PolityAId].Name, rel.PolityBId,
                state.Actors[rel.PolityBId].Name, war?.Id, war?.Name,
                rel.Warmth, rel.Tension,
                (double[])rel.LastWarmthTerms.Clone(),
                (double[])rel.LastTensionTerms.Clone(), rel.Rung,
                rel.RungYear, rel.VassalPolityId, rel.VassalSinceYear,
                rel.OfferedRung, rel.OfferedById, rel.DynasticTies,
                rel.LastIncidentYear, claims));
        }
        return rows;
    }
}
