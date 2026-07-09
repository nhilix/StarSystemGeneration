using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class EpochSimConfigTests
{
    [Fact]
    public void Defaults_MatchTimeDesign()
    {
        var c = new EpochSimConfig();
        Assert.Equal(25, c.Sim.YearsPerEpoch);   // one epoch ≈ a generation (frame/time.md)
        Assert.Equal(40, c.Sim.EpochCount);      // default history depth ~1,000y
        Assert.Equal(1000, c.Sim.YearsPerEpoch * c.Sim.EpochCount);
    }

    [Fact]
    public void EconomyRates_AreWorldYearDenominated()
    {
        var c = new EpochSimConfig();
        // Integrating one epoch of each dial reproduces the prototype's
        // per-epoch magnitudes at its 50y step — the rates are per-year (P7).
        Assert.Equal(0.15, c.Economy.WarWearinessPerYear * 50, 10);
        Assert.Equal(0.10, c.Economy.StockpileDecayPerYear * 50, 10);
        Assert.Equal(0.50, c.Economy.ProvisionsPerPopPerYear * 50, 10);
    }

    [Fact]
    public void GenesisKnobs_SeedStubEmergence()
    {
        var c = new EpochSimConfig();
        Assert.True(c.Genesis.StubPolityCount > 0);
        // staggered entry fits inside the default history window
        Assert.True(c.Genesis.EmergenceWindowYears
                    < c.Sim.YearsPerEpoch * c.Sim.EpochCount);
    }

    [Fact]
    public void InfrastructureAndExpansionKnobs_HaveSaneDefaults()
    {
        var c = new EpochSimConfig();
        Assert.True(c.Infrastructure.ServiceRadiusBaseHexes >= 1);
        Assert.Equal(3, c.Infrastructure.MaxPortTier);
        Assert.InRange(c.Infrastructure.HomeworldPortTier, 1, c.Infrastructure.MaxPortTier);
        // lane reach exceeds local service reach at every tier (space-and-travel.md:
        // inter-port range is the longer, separate growth axis)
        Assert.True(c.Infrastructure.InterPortRangeBaseHexes
                    > c.Infrastructure.ServiceRadiusBaseHexes);
        Assert.True(c.Expansion.StubIncomePerPortPerYear > 0);
        Assert.True(c.Expansion.ColonyCost > 0);
        Assert.True(c.Expansion.ColonizationReachHexes > 0);
        Assert.True(c.Expansion.PortUpgradeCostBase > 0);
        Assert.True(c.Expansion.LaneCost > 0);
        // growth is a per-world-year rate, not a per-epoch magnitude (P7)
        Assert.InRange(c.Expansion.SegmentGrowthPerYear, 0.0001, 0.1);
        Assert.True(c.Expansion.HomeworldSegmentSize > c.Expansion.ColonySegmentSize);
    }
}
