using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneGateTests
{
    [Fact]
    public void GateRow_ExistsInTheCatalog_SupportFamily_NoOutput()
    {
        var def = Infrastructure.Get(InfraTypeId.Gate);
        Assert.Equal("Gate", def.Name);
        Assert.Equal(InfraFamily.Support, def.Family);
        Assert.Empty(def.Produces);
        Assert.Equal(0, def.BaseOutputPerYear);
        Assert.NotEmpty(def.BuildCost);
    }

    [Fact]
    public void GateKnobs_CarryTheSpecDefaults()
    {
        var cfg = new EpochSimConfig();
        Assert.Equal(2, cfg.Infrastructure.GateSlotsPerPortTier);
        Assert.Equal(8, cfg.Infrastructure.GateReachTier1Hexes);
        Assert.Equal(16, cfg.Infrastructure.GateReachTier2Hexes);
        Assert.Equal(28, cfg.Infrastructure.GateReachTier3Hexes);
        Assert.Equal(1.8, cfg.Expansion.DetourFactor);
        Assert.Equal(0.9, cfg.Expansion.ExpressSaturationFloor);
        Assert.Equal(3, cfg.Expansion.SaturatedEpochsForExpress);
        Assert.Equal(0.05, cfg.Economy.GateTollRate);
    }

    [Fact]
    public void RequiredGateTier_StepsWithDistance_AndFailsPastTier3()
    {
        var cfg = new EpochSimConfig();
        Assert.Equal(1, LaneMath.RequiredGateTier(cfg, 8, 0));
        Assert.Equal(2, LaneMath.RequiredGateTier(cfg, 9, 0));
        Assert.Equal(3, LaneMath.RequiredGateTier(cfg, 28, 0));
        Assert.Equal(-1, LaneMath.RequiredGateTier(cfg, 29, 0));
        Assert.Equal(1, LaneMath.RequiredGateTier(cfg, 9, 1));   // astro stretch
    }

    [Fact]
    public void Lane_IsLiveOnlyWhileBothGatesStandAndFunction()
    {
        var state = TwoPortState();
        var lane = EpochTestKit.AddLane(state, 0, 1);
        Assert.True(LaneMath.IsLive(state, lane));
        state.Facilities[lane.GateAId].Condition = 0.1;   // raided below floor
        Assert.False(LaneMath.IsLive(state, lane));
    }

    [Fact]
    public void CapacityAndSpeed_DeriveFromGateTiers()
    {
        var state = TwoPortState();
        var lane = EpochTestKit.AddLane(state, 0, 1, gateTier: 3);
        Assert.Equal(3.0, LaneMath.Capacity(state, lane));
        Assert.Equal(2.5, LaneMath.TransitSpeed(state, lane));
    }

    [Fact]
    public void Serializer_RoundTripsGateIdsAndSaturation()
    {
        var state = TwoPortState();
        var lane = EpochTestKit.AddLane(state, 0, 1);
        lane.SaturatedYears = 2;
        var loaded = ArtifactSerializer.Load(
            new System.IO.StringReader(ArtifactSerializer.ToText(state)));
        Assert.Equal(lane.GateAId, loaded.Lanes[lane.Id].GateAId);
        Assert.Equal(lane.GateBId, loaded.Lanes[lane.Id].GateBId);
        Assert.Equal(2, loaded.Lanes[lane.Id].SaturatedYears);
    }

    /// <summary>A seeded state advanced until two ports exist — colonization
    /// gets there on its own; the helper just fast-forwards to it.</summary>
    internal static SimState TwoPortState()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        for (int i = 0; i < 60 && state.Ports.Count < 2; i++)
            engine.Step(state);
        Assert.True(state.Ports.Count >= 2, "test needs two ports");
        return state;
    }
}
