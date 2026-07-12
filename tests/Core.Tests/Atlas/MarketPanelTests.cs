using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Market panel — `market &lt;portId&gt;` parity
/// (MarketView.Render: per-good price/inventory/grade/cleared/black book,
/// segments, facilities, lanes-with-CUT) PLUS the located larder (T2):
/// Port.StockQty/StockGrade per good, capacity via
/// MarketEngine.StockCapacityAt, and the effective per-year decay.</summary>
public class MarketPanelTests
{
    private static (AtlasReadModel Model, SimState State) WithMarket()
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
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, 2, 7));
        state.Ports.Add(new Port(1, state.Actors[1].Id, b!.Value, 1, 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void TheHeaderAndGoodRowsReadTheMarketRegistry()
    {
        var (model, state) = WithMarket();
        var market = state.Markets[0];
        market.Price[(int)GoodId.Alloys] = 4.25;
        market.Inventory[(int)GoodId.Alloys] = 30;
        market.InventoryGrade[(int)GoodId.Alloys] = 0.7;
        market.LastCleared[(int)GoodId.Alloys] = 11;
        market.BlackBookDemand[(int)GoodId.Alloys] = 2.5;
        market.BlackBookPrice[(int)GoodId.Alloys] = 9.0;

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0);
        Assert.NotNull(card);
        Assert.Equal(0, card!.PortId);
        Assert.Equal(2, card.Tier);
        Assert.Equal(state.Ports[0].Hex, card.Hex);
        Assert.Equal(state.Actors[0].Name, card.OwnerName);
        Assert.Equal(7, card.FoundedYear);
        Assert.Equal(Goods.All.Count, card.Goods.Count);
        var row = card.Goods[(int)GoodId.Alloys];
        Assert.Equal(4.25, row.Price);
        Assert.Equal(30.0, row.Inventory);
        Assert.Equal(0.7, row.Grade);
        Assert.Equal(Grades.BandOf(0.7), row.GradeBand);
        Assert.Equal(11.0, row.LastCleared);
        Assert.Equal(2.5, row.BlackBookDemand);
        Assert.Equal(9.0, row.BlackBookPrice);
    }

    [Fact]
    public void TheLarderReadsStockAndSharedCapacity()
    {
        var (model, state) = WithMarket();
        var port = state.Ports[0];
        port.StockQty[(int)GoodId.Provisions] = 55;
        port.StockGrade[(int)GoodId.Provisions] = 0.4;
        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        var row = card.Goods[(int)GoodId.Provisions];
        Assert.Equal(55.0, row.StockQty);
        Assert.Equal(0.4, row.StockGrade);
        // one derivation, zero drift: capacity IS StockCapacityAt
        Assert.Equal(MarketEngine.StockCapacityAt(state, port),
                     card.StockCapacity);
        Assert.Equal(2 * state.Config.Economy.StockCapPerPortTier,
                     card.StockCapacity);
    }

    [Fact]
    public void ADepotDeepensTheLarderAndSlowsTheRot()
    {
        var (model, state) = WithMarket();
        var port = state.Ports[0];
        var depot = new Facility(0, (int)InfraTypeId.Depot, tier: 2,
            port.Hex, port.OwnerActorId, builtYear: 0);
        state.Facilities.Add(depot);
        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        Assert.Equal(MarketEngine.StockCapacityAt(state, port),
                     card.StockCapacity);
        var eco = state.Config.Economy;
        double cut = System.Math.Pow(eco.DepotDecayFactor, 2);
        // provisions rot 10× the durable rate; the depot's cut applies
        Assert.Equal(eco.StockpileDecayPerYear * 10.0 * cut,
            card.Goods[(int)GoodId.Provisions].StockDecayPerYear, 12);
        Assert.Equal(eco.StockpileDecayPerYear * 1.0 * cut,
            card.Goods[(int)GoodId.Alloys].StockDecayPerYear, 12);
    }

    [Fact]
    public void SegmentsFacilitiesAndCutLanesSurface()
    {
        var (model, state) = WithMarket();
        int cultureId = state.Cultures.Count;   // genesis seeds cultures
        state.Cultures.Add(new Culture(cultureId, "Testfolk", 0));
        state.Segments.Add(new PopulationSegment(0, portId: 0,
            speciesId: 0, cultureId: cultureId, size: 1.4)
        { SoL = 0.6, Wealth = 12, LastSubsistence = 0.9 });
        var lane = EpochTestKit.AddLane(state, 0, 1);
        EpochTestKit.BlockadePort(state, state.Actors[1].Id, portId: 0);

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        var seg = Assert.Single(card.Segments);
        Assert.Equal("Testfolk", seg.CultureName);
        Assert.Equal(1.4, seg.Size);
        Assert.Equal(0.9, seg.LastSubsistence);
        // the lane link reads CUT under the blockade (SeveredLaneIds)
        var link = Assert.Single(card.Lanes);
        Assert.Equal(lane.Id, link.LaneId);
        Assert.Equal(1, link.OtherPortId);
        Assert.True(link.Cut);
        // gates at this port surface as facilities, MarketView's filter
        Assert.Contains(card.Facilities, f => f.TypeName.Contains("Gate"));
    }

    [Fact]
    public void NoSuchPortReturnsNull()
    {
        var (model, state) = WithMarket();
        Assert.Null(MarketPanel.Card(model,
            EyeContext.God(state.WorldYear), 99));
    }
}
