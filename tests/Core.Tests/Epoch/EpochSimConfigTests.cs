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
}
