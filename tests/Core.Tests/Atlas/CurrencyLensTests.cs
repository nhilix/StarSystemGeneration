using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The currency lens — CU-3 consolidation made visible (AC3.1):
/// each polity's slot carries its own currency's zone tint; two slots
/// sharing a currency id (a union, post-consolidation) share a color; a
/// retired currency's zone goes untinted; a currency-less slot is absent.</summary>
public class CurrencyLensTests
{
    private static (AtlasReadModel Model, SimState State, int[] Entered) WithPorts()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate? a = null, b = null;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            if (a == null) { a = HexGrid.CellCenter(cell.Coord); continue; }
            b = HexGrid.CellCenter(cell.Coord);
            break;
        }
        var entered = new System.Collections.Generic.List<int>();
        foreach (var actor in state.Actors)
        {
            if (actor.Kind != ActorKind.Polity || entered.Count == 2) continue;
            actor.Entered = true;
            entered.Add(actor.Id);
        }
        state.Ports.Add(new Port(0, entered[0], a!.Value, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, entered[1], b!.Value, tier: 2, foundedYear: 0));
        return (new AtlasReadModel(state), state, entered.ToArray());
    }

    [Fact]
    public void SlotCurrencyRunsParallelToTheSlots()
    {
        var (model, state, entered) = WithPorts();
        var eye = EyeContext.God(state.WorldYear);
        state.FoundCurrency(entered[0]);
        state.FoundCurrency(entered[1]);
        var slots = DomainLens.PolitySlots(model, eye);
        var currencies = CurrencyLens.SlotCurrency(model, eye, slots);
        Assert.Equal(slots.Count, currencies.Count);
        for (int i = 0; i < slots.Count; i++)
            Assert.Equal(state.PolityOf(slots[i]).CurrencyId, currencies[i]);
    }

    [Fact]
    public void APortWithNoCurrencyIsAbsent()
    {
        var (model, state, entered) = WithPorts();
        // no FoundCurrency call: the polities are dormant (CurrencyId == -1,
        // the pre-genesis sentinel — PolityRecord's default).
        Assert.Equal(-1, state.PolityOf(entered[0]).CurrencyId);
        Assert.Null(CurrencyLens.CurrencyColor(model, -1));
    }

    [Fact]
    public void UnionPortsShareAColor()
    {
        var (model, state, entered) = WithPorts();
        state.FoundCurrency(entered[0]);
        state.FoundCurrency(entered[1]);
        // simulate a CU-3 consolidation: both polities now share currency 0
        state.PolityOf(entered[1]).CurrencyId = 0;

        var colorA = CurrencyLens.CurrencyColor(model, state.PolityOf(entered[0]).CurrencyId);
        var colorB = CurrencyLens.CurrencyColor(model, state.PolityOf(entered[1]).CurrencyId);
        Assert.NotNull(colorA);
        Assert.Equal(colorA, colorB);
    }

    [Fact]
    public void ARetiredCurrencyDropsOut()
    {
        var (model, state, entered) = WithPorts();
        state.FoundCurrency(entered[0]);
        Assert.NotNull(CurrencyLens.CurrencyColor(model, 0));

        state.CurrencyOf(0).Retired = true;
        Assert.Null(CurrencyLens.CurrencyColor(model, 0));
    }

    [Fact]
    public void DistinctCurrenciesGetDistinctColors()
    {
        var (model, state, entered) = WithPorts();
        state.FoundCurrency(entered[0]);
        state.FoundCurrency(entered[1]);
        var colorA = CurrencyLens.CurrencyColor(model, 0);
        var colorB = CurrencyLens.CurrencyColor(model, 1);
        Assert.NotEqual(colorA, colorB);
        // the same golden-ratio idiom AtlasPalette.OwnerColor uses, keyed on
        // the currency id rather than the actor id
        Assert.Equal(AtlasPalette.OwnerColor(0), colorA);
        Assert.Equal(AtlasPalette.OwnerColor(1), colorB);
    }
}
