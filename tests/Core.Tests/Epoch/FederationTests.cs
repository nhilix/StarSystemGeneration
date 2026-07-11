using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 3 — the top rungs (interpolity/relations.md
/// §Federation, §Vassalage): fusion births a NEW polity and retires the
/// parents whole (P4), vassalage binds tribute + the foreign-policy lock,
/// and the two exits fire on their clocks.</summary>
public class FederationTests
{
    private static SimState Run(int epochs = 24)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    private static void Continue(SimState state, int epochs)
    {
        state.Config.Sim.EpochCount = state.EpochIndex + epochs;
        new EpochEngine().Run(state);
    }

    private static double TotalCredits(SimState state)
    {
        double sum = 0;
        foreach (var p in state.Polities) sum += p.Credits;
        foreach (var c in state.Corporations) sum += c.Credits;
        foreach (var s in state.Segments) sum += s.Wealth;
        foreach (var f in state.Factions) sum += f.Wealth;
        return sum;
    }

    private static (int Built, int Wrecked, int Scrapped) HullLedger(SimState state)
    {
        int built = 0, wrecked = 0, scrapped = 0;
        foreach (var p in state.Polities)
        {
            built += p.HullsBuilt;
            wrecked += p.HullsWrecked;
            scrapped += p.HullsScrapped;
        }
        foreach (var c in state.Corporations)
        {
            built += c.HullsBuilt;
            wrecked += c.HullsWrecked;
            scrapped += c.HullsScrapped;
        }
        return (built, wrecked, scrapped);
    }

    [Fact]
    public void Federate_FusesANewPolity_AndConserves()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        double creditsBefore = TotalCredits(state);
        var ledgerBefore = HullLedger(state);
        int portsA = 0, portsB = 0;
        foreach (var p in state.Ports)
        {
            if (p.OwnerActorId == rel.PolityAId) portsA++;
            if (p.OwnerActorId == rel.PolityBId) portsB++;
        }
        Assert.True(portsA > 0 && portsB > 0);

        int newId = FederationOps.Federate(state, rel);

        // the fused polity is NEW and both parents left the stage
        Assert.True(newId >= 0);
        Assert.True(state.Actors[newId].Entered);
        Assert.False(state.Actors[rel.PolityAId].Entered);
        Assert.True(state.Actors[rel.PolityAId].Retired);
        Assert.True(state.Actors[rel.PolityBId].Retired);
        Assert.Null(state.PolityOf(rel.PolityAId).Interior);
        // every port moved; nobody's segments went anywhere
        int newPorts = 0;
        foreach (var p in state.Ports)
        {
            Assert.NotEqual(rel.PolityAId, p.OwnerActorId);
            Assert.NotEqual(rel.PolityBId, p.OwnerActorId);
            if (p.OwnerActorId == newId) newPorts++;
        }
        Assert.Equal(portsA + portsB, newPorts);
        // books and hulls conserve through the fusion (P4)
        Assert.Equal(creditsBefore, TotalCredits(state), 6);
        Assert.Equal(ledgerBefore, HullLedger(state));
        // the interior seats fresh with founding legitimacy high
        var young = state.PolityOf(newId);
        Assert.NotNull(young.Interior);
        Assert.True(young.Interior!.Legitimacy >= 0.7,
            "treaty-built federations start legitimate");
        // fresh designs registered so the yards keep building
        Assert.NotNull(DesignRegistry.Current(state, newId,
            ShipRole.Freight, ShipSize.Medium));
    }

    [Fact]
    public void FederationGate_RequiresSustainedWarmAlliance()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        // no alliance: closed
        rel.Rung = TreatyRung.None;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
        // fresh alliance: still closed
        rel.Rung = TreatyRung.DefenseAlliance;
        rel.RungEpoch = state.EpochIndex;
        rel.Warmth = 0.95;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
        // sustained + warm + aligned + open + cohesive: open
        rel.RungEpoch = state.EpochIndex
            - state.Config.Relations.FederationAllianceEpochs;
        var a = state.PolityOf(rel.PolityAId);
        var b = state.PolityOf(rel.PolityBId);
        for (int ax = 0; ax < 4; ax++)
            b.Interior!.OfficialIdeology[ax] = a.Interior!.OfficialIdeology[ax];
        a.Interior!.Cohesion = 0.8;
        b.Interior!.Cohesion = 0.8;
        // openness is composed — force it through the official line
        a.Interior.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.05;
        b.Interior.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.05;
        bool holds = FederationOps.FederationGateHolds(state, rel);
        // openness also carries species/ruler terms — accept either, but a
        // cold pair must never pass
        rel.Warmth = 0.1;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
        rel.Warmth = 0.95;
        Assert.True(holds || Temperament.Compose(state, a).Openness
                        < state.Config.Relations.FederationOpennessFloor
                    || Temperament.Compose(state, b).Openness
                        < state.Config.Relations.FederationOpennessFloor);
    }

    [Fact]
    public void Vassalage_Binds_TributeFlows_TableCloses()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        Assert.Equal(vassal, rel.VassalPolityId);
        Assert.Equal(overlord, FederationOps.OverlordOf(state, vassal));
        Assert.True(FederationOps.HasVassals(state, overlord));

        // tribute: a conserved vassal→overlord receipts share
        var vr = state.PolityOf(vassal);
        var or = state.PolityOf(overlord);
        vr.Receipts = 100;
        double vc = vr.Credits, oc = or.Credits;
        int paid = FederationOps.PayTribute(state);
        Assert.Equal(1, paid);
        double share = state.Config.Relations.VassalTributeShare;
        Assert.Equal(vc - 100 * share, vr.Credits, 9);
        Assert.Equal(oc + 100 * share, or.Credits, 9);
        Assert.Equal(100 * (1 - share), vr.Receipts, 9);

        // the foreign-policy lock: the vassal's treaties resolve to nothing
        var offer = new TreatyAct(vassal, overlord, 1, TreatyVerb.Offer);
        Assert.Equal(RelationsOps.TreatyOutcome.NoEffect,
                     RelationsOps.ResolveTreaty(state, offer));
    }

    [Fact]
    public void ChosenVassalage_NeedsRealWeakness()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        // a peer (similar strength) is refused
        var act = new VassalageAct(rel.PolityAId, rel.PolityBId, IsDemand: false);
        double a = FleetOps.WarStrength(state, rel.PolityAId);
        double b = FleetOps.WarStrength(state, rel.PolityBId);
        double ratio = b <= 0 ? 1.0 : a / b;
        rel.Warmth = 0.6;
        bool bound = FederationOps.TryBindVassal(state, act);
        Assert.Equal(ratio <= state.Config.Relations.VassalStrengthRatio, bound);
    }

    [Fact]
    public void Absorption_CompletesLongWarmVassalage()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        rel.VassalSinceEpoch = state.EpochIndex
            - state.Config.Relations.VassalAbsorptionEpochs;
        rel.Warmth = 0.9;
        state.PolityOf(overlord).Interior!.Cohesion = 0.7;
        double creditsBefore = TotalCredits(state);
        var (absorbed, seceded) = FederationOps.VassalExits(state);
        Assert.Equal(1, absorbed);
        Assert.Equal(0, seceded);
        Assert.True(state.Actors[vassal].Retired);
        Assert.Equal(-1, rel.VassalPolityId);
        foreach (var p in state.Ports)
            Assert.NotEqual(vassal, p.OwnerActorId);
        Assert.Equal(creditsBefore, TotalCredits(state), 6);
    }

    [Fact]
    public void Secession_FiresOnOverlordWeakness_AndLeavesAGrudge()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        state.PolityOf(overlord).Interior!.Cohesion = 0.2;
        var (absorbed, seceded) = FederationOps.VassalExits(state);
        Assert.Equal(0, absorbed);
        Assert.Equal(1, seceded);
        Assert.Equal(-1, rel.VassalPolityId);
        Assert.True(state.Actors[vassal].Entered);   // free again
        bool grudge = false;
        foreach (var c in rel.Claims)
            if (!c.Released && c.Type == ClaimType.LostTerritory
                && c.HolderPolityId == overlord) grudge = true;
        Assert.True(grudge, "the lost bond persists as a standing claim");
    }

    [Fact]
    public void RetiredActors_AndBondClocks_RoundTrip()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Rung = TreatyRung.DefenseAlliance;
        rel.RungEpoch = 7;
        FederationOps.Bind(state, state.Relations.Count > 1
            ? state.Relations[1] : rel, rel.PolityAId);
        state.Actors[rel.PolityBId].Retired = true;
        state.Actors[rel.PolityBId].Entered = false;

        var loaded = ArtifactSerializer.Load(
            new System.IO.StringReader(ArtifactSerializer.ToText(state)));

        Assert.True(loaded.Actors[rel.PolityBId].Retired);
        Assert.False(loaded.Actors[rel.PolityBId].Entered);
        for (int i = 0; i < state.Relations.Count; i++)
        {
            Assert.Equal(state.Relations[i].RungEpoch,
                         loaded.Relations[i].RungEpoch);
            Assert.Equal(state.Relations[i].VassalPolityId,
                         loaded.Relations[i].VassalPolityId);
            Assert.Equal(state.Relations[i].VassalSinceEpoch,
                         loaded.Relations[i].VassalSinceEpoch);
        }
        Assert.Equal(ArtifactSerializer.ToText(state),
                     ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void RetiredPolity_NeverReenters_AndSimContinues()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        FederationOps.Federate(state, rel);
        int actorCount = state.Actors.Count;
        Continue(state, 3);
        Assert.False(state.Actors[rel.PolityAId].Entered);
        Assert.False(state.Actors[rel.PolityBId].Entered);
        // no phantom homeworld re-founding for the retired
        foreach (var p in state.Ports)
        {
            Assert.NotEqual(rel.PolityAId, p.OwnerActorId);
            Assert.NotEqual(rel.PolityBId, p.OwnerActorId);
        }
        Assert.True(state.Actors.Count >= actorCount);
    }
}
