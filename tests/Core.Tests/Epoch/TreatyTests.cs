using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 2 — the treaty ladder's first three rungs
/// (interpolity/relations.md §Treaties): mutual consent in Resolution,
/// warmth-gated ascent one rung at a time, teeth (tariff cut, tension
/// damping, cross-border pact lanes), and public breaking with a warmth
/// crash.</summary>
public class TreatyTests
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

    [Fact]
    public void WarmPairs_ClimbTheLadder_OneRungAtATime()
    {
        var state = Run();
        Assert.True(state.Relations.Count > 0);
        // force a very warm, calm pair and let diplomacy work
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Warmth = 0.9;
        rel.Tension = 0.0;
        Continue(state, 3);
        Assert.True(rel.Rung >= TreatyRung.TradePact,
            $"warm pair should have signed (rung {rel.Rung})");
        // signings chronicle, and rungs never skip: each signed rung is
        // exactly one above the previous for this pair
        int lastRung = 0;
        foreach (var e in state.Log.Events)
        {
            if (e.Type != WorldEventType.TreatySigned
                || e.Payload is not TreatySignedPayload p
                || p.PolityAId != rel.PolityAId
                || p.PolityBId != rel.PolityBId) continue;
            Assert.Equal(lastRung + 1, p.Rung);
            lastRung = p.Rung;
        }
        Assert.True(lastRung >= 1);
    }

    [Fact]
    public void ColdPairs_NeverSign()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Warmth = 0.1;
        rel.Tension = 0.9;
        int before = CountSignings(state, rel);
        Continue(state, 2);
        Assert.Equal(before, CountSignings(state, rel));
    }

    private static int CountSignings(SimState state, PolityRelation rel)
    {
        int n = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == WorldEventType.TreatySigned
                && e.Payload is TreatySignedPayload p
                && p.PolityAId == rel.PolityAId && p.PolityBId == rel.PolityBId)
                n++;
        return n;
    }

    [Fact]
    public void HostileTurn_BreaksTheRung_AndWarmthCrashes()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Rung = TreatyRung.DefenseAlliance;
        // hostile: tension pinned high by claims, warmth pinned low
        rel.Warmth = 0.05;
        rel.Tension = 0.95;
        for (int i = 0; i < 6; i++)
            rel.Claims.Add(new RelationClaim(ClaimType.LostTerritory,
                rel.PolityAId, 100 + i, state.WorldYear));
        Continue(state, 2);
        Assert.Equal(TreatyRung.None, rel.Rung);
        bool broken = false;
        foreach (var e in state.Log.Events)
            if (e.Type == WorldEventType.TreatyBroken) broken = true;
        Assert.True(broken, "the break must chronicle publicly");
    }

    [Fact]
    public void NonAggression_DampsTheTensionTarget()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Rung = TreatyRung.None;
        double loose = RelationsOps.TensionTarget(state, rel, overlapPairs: 3);
        rel.Rung = TreatyRung.NonAggression;
        double damped = RelationsOps.TensionTarget(state, rel, overlapPairs: 3);
        Assert.True(damped < loose, "the pact's teeth: standing friction damps");
        Assert.Equal(loose * (1 - state.Config.Relations.NonAggressionDamping),
                     damped, 12);
    }

    [Fact]
    public void TradePact_CutsTheTariff()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Rung = TreatyRung.None;
        Assert.Equal(1.0,
            RelationsOps.TariffFactor(state, rel.PolityAId, rel.PolityBId));
        rel.Rung = TreatyRung.TradePact;
        Assert.Equal(state.Config.Relations.PactTariffFactor,
            RelationsOps.TariffFactor(state, rel.PolityAId, rel.PolityBId));
        // strangers pay full freight
        Assert.Equal(1.0, RelationsOps.TariffFactor(state, 0, 999_999 - 1));
    }

    [Fact]
    public void TradePact_OpensCrossBorderLanes()
    {
        var state = Run();
        // find the closest related pair and give them a pact + funding
        PolityRelation? best = null;
        Port? bestPa = null, bestPb = null;
        int bestDist = int.MaxValue;
        foreach (var rel in state.Relations)
        {
            if (!RelationsOps.BothLive(state, rel)
                || WarOps.ActiveWarBetween(state, rel.PolityAId,
                                           rel.PolityBId) != null) continue;
            foreach (var pa in state.Ports)
            {
                if (pa.OwnerActorId != rel.PolityAId) continue;
                foreach (var pb in state.Ports)
                {
                    if (pb.OwnerActorId != rel.PolityBId) continue;
                    int d = HexGrid.Distance(pa.Hex, pb.Hex);
                    if (d < bestDist)
                    { bestDist = d; best = rel; bestPa = pa; bestPb = pb; }
                }
            }
        }
        Assert.NotNull(best);
        best!.Rung = TreatyRung.TradePact;
        best.Warmth = 0.8;
        best.Tension = 0.0;
        // gates cost real goods at both ends now (lane-economics spec §2):
        // fund the pair and stock the two border markets with the basket
        state.PolityOf(best.PolityAId).DevelopmentPoints += 1000;
        state.PolityOf(best.PolityBId).DevelopmentPoints += 1000;
        var basket = StarGen.Core.Substrate.Infrastructure.Get(
            StarGen.Core.Substrate.InfraTypeId.Gate).BuildCost;
        foreach (var port in new[] { bestPa!, bestPb! })
            foreach (var q in basket)
                port.DepositStock((int)q.Good, q.Quantity * 10, 0.6);
        // drive Allocation directly: a full continuation can federate the
        // partner away mid-test (histories churn)
        new AllocationPhase().Run(state);
        bool crossLane = false;
        foreach (var lane in state.Lanes)
        {
            int a = state.Ports[lane.PortAId].OwnerActorId;
            int b = state.Ports[lane.PortBId].OwnerActorId;
            if (a != b && best.Involves(a) && best.Involves(b))
                crossLane = true;
        }
        Assert.True(crossLane,
            $"pact partners at distance {bestDist} should pair ports");
    }

    [Fact]
    public void Offers_ExpireQuietly()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.OfferedRung = TreatyRung.TradePact;
        rel.OfferedById = rel.PolityAId;
        rel.OfferYear = state.WorldYear
            - 10 * state.Config.Sim.GenerationYears;
        // hold the pair too cold to re-offer or accept
        rel.Warmth = 0.0;
        rel.Tension = 0.9;
        Continue(state, 1);
        Assert.Equal(TreatyRung.None, rel.OfferedRung);
        Assert.Equal(-1, rel.OfferedById);
    }
}
