using System;
using System.Linq;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Tables;
using Xunit;

namespace StarGen.Core.Tests.Tables;

public class WeightedTableTests
{
    [Fact]
    public void Pick_MapsRollAcrossCumulativeWeights()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 3));
        Assert.Equal("a", table.Pick(0.0));
        Assert.Equal("a", table.Pick(0.24));
        Assert.Equal("b", table.Pick(0.26));
        Assert.Equal("b", table.Pick(0.999));
    }

    [Fact]
    public void Modifier_ZeroWeight_ExcludesEntry()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 1));
        for (double r = 0; r < 1; r += 0.05)
            Assert.Equal("b", table.Pick(r, item => item == "a" ? 0 : 1));
    }

    [Fact]
    public void Modifier_ShiftsDistribution()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 1));
        var ctx = new RollContext(7, new HexCoordinate(0, 0));
        int aCount = 0;
        for (int i = 0; i < 4000; i++)
            if (table.Pick(ctx.NextDouble(RollChannel.BodyKind, i), it => it == "a" ? 3 : 1) == "a")
                aCount++;
        Assert.InRange(aCount / 4000.0, 0.70, 0.80); // expect ~0.75
    }

    [Fact]
    public void SamplingWithoutModifier_RoughlyMatchesWeights()
    {
        var table = new WeightedTable<string>(("a", 1), ("b", 4));
        var ctx = new RollContext(11, new HexCoordinate(1, 1));
        int aCount = 0;
        for (int i = 0; i < 5000; i++)
            if (table.Pick(ctx.NextDouble(RollChannel.BodySize, i)) == "a") aCount++;
        Assert.InRange(aCount / 5000.0, 0.16, 0.24); // expect ~0.20
    }

    [Fact]
    public void InvalidConstruction_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WeightedTable<string>());
        Assert.Throws<ArgumentException>(() => new WeightedTable<string>(("a", -1)));
        Assert.Throws<ArgumentException>(() => new WeightedTable<string>(("a", 0), ("b", 0)));
    }

    [Fact]
    public void AllWeightsZeroedByModifier_Throws()
    {
        var table = new WeightedTable<string>(("a", 1));
        Assert.Throws<InvalidOperationException>(() => table.Pick(0.5, _ => 0));
    }
}
