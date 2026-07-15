using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyResourceOpsTests
{
    // Commit only null-guards on the system; a bare non-null system suffices.
    private static StarSystem Sys() => new StarSystem("STK");

    [Fact]
    public void Commit_IsIdempotent_KeepsTheFirstRoll()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var body = new BodyRef(0, 0);
        BodyResourceOps.Commit(state, hex, body, InfraTypeId.Mine, Sys());
        double first = state.BodyResources[(hex, body)].Quantity;
        BodyResourceOps.Commit(state, hex, body, InfraTypeId.Mine, Sys());
        double second = state.BodyResources[(hex, body)].Quantity;
        Assert.Equal(first, second, 9);          // memoized, not re-rolled
    }

    [Fact]
    public void Commit_QuantityTracksRegionalRichness_WithinTheSpread()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 0),
                               InfraTypeId.Mine, Sys());
        double qty = state.BodyResources[(hex, new BodyRef(0, 0))].Quantity;

        var eco = state.Config.Economy;
        double richness = Potentials.Ore(MarketEngine.FieldsAt(state, hex));
        double expected = eco.BodyStockOreScale * richness;
        Assert.True(expected > 0);
        Assert.InRange(qty,
            expected * (1.0 - eco.BodyStockVarianceSpread),
            expected * (1.0 + eco.BodyStockVarianceSpread));
    }

    [Fact]
    public void Commit_TwoBodiesInOneHex_RollDifferentQuantities()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 0),
                               InfraTypeId.Mine, Sys());
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 1),
                               InfraTypeId.Mine, Sys());
        Assert.NotEqual(
            state.BodyResources[(hex, new BodyRef(0, 0))].Quantity,
            state.BodyResources[(hex, new BodyRef(0, 1))].Quantity);
    }

    [Fact]
    public void Extract_DrawsCappedByRemaining_AndFloorsAtZero()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var body = new BodyRef(0, 0);
        BodyResourceOps.Commit(state, hex, body, InfraTypeId.Mine, Sys());
        double total = state.BodyResources[(hex, body)].Quantity;
        Assert.True(total > 0);

        double drawn = BodyResourceOps.Extract(state, hex, body, total + 1000,
                                                out double grade);
        Assert.Equal(total, drawn, 6);           // capped by what's there
        Assert.True(grade > 0);
        Assert.Equal(0.0, state.BodyResources[(hex, body)].Quantity, 9);

        double again = BodyResourceOps.Extract(state, hex, body, 100, out _);
        Assert.Equal(0.0, again, 9);             // dry stays dry, never negative
    }

    [Fact]
    public void Commit_RenewableTypes_RollNoStock()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        BodyResourceOps.Commit(state, hex, new BodyRef(0, 0),
                               InfraTypeId.Skimmer, Sys());
        Assert.False(state.BodyResources.ContainsKey((hex, new BodyRef(0, 0))));
    }
}
