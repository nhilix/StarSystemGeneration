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
}
