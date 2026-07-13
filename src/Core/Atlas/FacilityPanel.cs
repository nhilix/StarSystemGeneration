using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>The Facility inspector's card (K5, NEW): the orbit-view
/// facility click's subject — type, family, tier, condition, owner, what
/// it makes, and where it trades. Active and market attachment ride the
/// SAME MarketEngine derivations the sim uses (zero drift).</summary>
public sealed record FacilityCard(
    int Id, string TypeName, InfraFamily Family, int Tier, HexCoordinate Hex,
    int OwnerActorId, string OwnerName, ActorKind OwnerKind,
    int OwnerCorpId,
    double Condition, bool Active, bool Commissioned, int BuiltYear,
    int MarketPortId, IReadOnlyList<string> Produces);

/// <summary>K5: the SystemStage's facility click target.</summary>
public static class FacilityPanel
{
    public static FacilityCard? Card(AtlasReadModel model, EyeContext eye,
                                     int facilityId)
    {
        var state = model.State;
        if (facilityId < 0 || facilityId >= state.Facilities.Count)
            return null;
        var f = state.Facilities[facilityId];
        var def = Infrastructure.Get((InfraTypeId)f.TypeId);
        var owner = state.Actors[f.OwnerActorId];

        var produces = new List<string>(def.Produces.Count);
        foreach (var good in def.Produces)
            produces.Add(Goods.Get(good).Name);

        // a corp owner's panel subject is its REGISTRY id, not its actor
        // id — the Corporations panel filters on CorpRow.Id
        int ownerCorpId = -1;
        if (owner.Kind == ActorKind.Corporation)
            foreach (var corp in state.Corporations)      // id order (P6)
                if (corp.ActorId == f.OwnerActorId)
                {
                    ownerCorpId = corp.Id;
                    break;
                }

        return new FacilityCard(f.Id, def.Name, def.Family, f.Tier, f.Hex,
            f.OwnerActorId, owner.Name, owner.Kind, ownerCorpId,
            f.Condition,
            MarketEngine.IsActive(state, f), f.CommissionedYear >= 0,
            f.BuiltYear, MarketEngine.AttachedMarketIndex(state, f),
            produces);
    }
}
