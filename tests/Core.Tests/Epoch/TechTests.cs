using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 6: tech domains — four per-polity tier ladders,
/// research as Allocation execution (Refined Exotics × Compute), TechAdvance
/// events, ceilings/regions consumed by grades and design sheets, trade +
/// salvage diffusion, starting tiers from the emergence schedule. The
/// TechTierStub is retired.</summary>
public class TechTests
{
    [Fact]
    public void EntryTiers_SeedFromTheEmergenceSchedule()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered) continue;
            int lift = pr.EntryGradeBonus >= 0.10 ? 1 : 0;
            Assert.Equal(Tech.EraStandardTier + lift,
                pr.TechTier[(int)TechDomain.Industrial]);
            Assert.Equal(Tech.EraStandardTier + lift,
                pr.TechTier[(int)TechDomain.Astrogation]);
            Assert.InRange(pr.TechTier[(int)TechDomain.Military], 1, 2);
            Assert.Equal(Tech.EraStandardTier, pr.TechTier[(int)TechDomain.Life]);
        }
    }

    [Fact]
    public void Research_ConsumesExotics_AdvancesTheLadder_Conserved()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var pr = state.Polities.First(p => state.Actors[p.ActorId].Entered);
        var port = state.Ports.First(p => p.OwnerActorId == pr.ActorId);
        var market = state.Markets[port.Id];
        // stock the shelves and fund the line
        market.Inventory[(int)StarGen.Core.Substrate.GoodId.RefinedExotics] = 100;
        market.Inventory[(int)StarGen.Core.Substrate.GoodId.Compute] = 100;
        int tierBefore = pr.TechTier[(int)TechDomain.Industrial];
        double progressBefore = pr.TechProgress[(int)TechDomain.Industrial];
        double spent = TechOps.Research(state, pr,
            new ResearchSplit(1.0, 0, 0, 0), pool: 200.0);
        Assert.True(spent > 0, "a funded line with stocked shelves spent nothing");
        Assert.True(spent <= 200.0 + 1e-9);
        Assert.True(pr.TechTier[(int)TechDomain.Industrial] > tierBefore
                    || pr.TechProgress[(int)TechDomain.Industrial] > progressBefore,
            "research produced no progress");
    }

    [Fact]
    public void Thresholds_AreGeometric()
    {
        var config = new EpochSimConfig();
        Assert.Equal(config.Tech.BaseThreshold, Tech.Threshold(config, 1));
        Assert.Equal(config.Tech.BaseThreshold * config.Tech.ThresholdGrowth,
                     Tech.Threshold(config, 2), 9);
        Assert.True(Tech.Threshold(config, 4) > Tech.Threshold(config, 3));
    }

    [Fact]
    public void FullRun_TiersAdvanceWithoutRunaway()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        foreach (var pr in state.Polities)
            for (int d = 0; d < 4; d++)
                Assert.InRange(pr.TechTier[d], 1, 6);
        // designs carry real per-owner tiers, not a config constant
        foreach (var design in state.Designs)
            Assert.InRange(design.TechTier, 1, 6);
    }

    [Fact(Skip = "t1: converts to project in Task 5")]
    public void TradeDiffusion_CapsOneTierBelowTheSource()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        new EpochEngine().Run(state);
        // manufacture a cross-border lane with a tech gap and posted hulls
        if (state.Lanes.Count == 0 || state.Polities.Count < 2) return;
        var lane = state.Lanes[0];
        var a = state.Ports[lane.PortAId];
        var b = state.Ports[lane.PortBId];
        if (a.OwnerActorId == b.OwnerActorId)
            b.OwnerActorId = state.Polities
                .First(p => p.ActorId != a.OwnerActorId).ActorId;
        var laggard = state.PolityOf(a.OwnerActorId);
        var leader = state.PolityOf(b.OwnerActorId);
        for (int d = 0; d < 4; d++)
        {
            laggard.TechTier[d] = 1;
            laggard.TechProgress[d] = 0;
            leader.TechTier[d] = 4;
        }
        EpochTestKit.PostFreight(state, a.OwnerActorId, lane.Id, 5);
        state.Config.Tech.TradeDiffusionPerYear = 1e6;   // saturate the drift
        for (int i = 0; i < 3; i++) TechOps.Diffuse(state);
        for (int d = 0; d < 4; d++)
            Assert.True(laggard.TechTier[d] <= 3,
                $"diffusion pushed domain {d} to {laggard.TechTier[d]}, past source-1");
    }

    [Fact]
    public void TechState_RoundTripsThroughTheArtifact()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        foreach (var pr in state.Polities)
        {
            var lp = loaded.PolityOf(pr.ActorId);
            for (int d = 0; d < 4; d++)
            {
                Assert.Equal(pr.TechTier[d], lp.TechTier[d]);
                Assert.Equal(pr.TechProgress[d], lp.TechProgress[d]);
            }
        }
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
