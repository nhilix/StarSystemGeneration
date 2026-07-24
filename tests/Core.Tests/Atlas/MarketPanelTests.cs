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
        // the shelf is the book now (slice CE): a resting sell is the row
        EpochTestKit.Stock(state, 0, (int)GoodId.Alloys, 30, 0.7);
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

    // ---- AC2.4: the order book ('ebook' parity) rides the same good row.

    [Fact]
    public void ABareGoodHasNoAsksOrBids()
    {
        var (model, state) = WithMarket();
        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        var row = card.Goods[(int)GoodId.Alloys];
        Assert.Empty(row.Asks);
        Assert.Empty(row.Bids);
    }

    [Fact]
    public void AsksSortCheapestFirstWithOwnerNameAndRefDelta()
    {
        var (model, state) = WithMarket();
        var market = state.Markets[0];
        market.Price[(int)GoodId.Alloys] = 5.0;
        // two resting sells from two different owners — dearer posted first,
        // so a naive id-order read would get the order wrong
        var dear = OrderOps.PostSell(state, state.Actors[1].Id, 0,
            (int)GoodId.Alloys, 8, 0.6, ask: 6.5, expiryYear: 1000);
        var cheap = OrderOps.PostSell(state, state.Actors[0].Id, 0,
            (int)GoodId.Alloys, 12, 0.9, ask: 4.5, expiryYear: 1000);

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        var row = card.Goods[(int)GoodId.Alloys];
        Assert.Equal(2, row.Asks.Count);
        Assert.Equal(cheap.Id, row.Asks[0].OrderId);
        Assert.Equal(state.Actors[0].Id, row.Asks[0].OwnerActorId);
        Assert.Equal(state.Actors[0].Name, row.Asks[0].OwnerName);
        Assert.Equal(12.0, row.Asks[0].Qty);
        Assert.Equal(0.9, row.Asks[0].Grade);
        Assert.Equal(4.5, row.Asks[0].LimitPrice);
        Assert.Equal(4.5 - 5.0, row.Asks[0].RefDelta, 9);
        Assert.Equal(dear.Id, row.Asks[1].OrderId);
        Assert.Equal(state.Actors[1].Name, row.Asks[1].OwnerName);
        Assert.Equal(6.5 - 5.0, row.Asks[1].RefDelta, 9);
        Assert.Empty(row.Bids);
    }

    [Fact]
    public void BidsSortDearestFirstWithEscrow()
    {
        var (model, state) = WithMarket();
        var market = state.Markets[0];
        market.Price[(int)GoodId.Provisions] = 3.0;
        var low = OrderOps.PostBuy(state, state.Actors[0].Id, 0,
            (int)GoodId.Provisions, 5, bid: 2.0, expiryYear: 1000);
        var high = OrderOps.PostBuy(state, state.Actors[1].Id, 0,
            (int)GoodId.Provisions, 3, bid: 3.75, expiryYear: 1000);

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        var row = card.Goods[(int)GoodId.Provisions];
        Assert.Empty(row.Asks);
        Assert.Equal(2, row.Bids.Count);
        Assert.Equal(high.Id, row.Bids[0].OrderId);
        Assert.Equal(state.Actors[1].Name, row.Bids[0].OwnerName);
        Assert.Equal(3.0, row.Bids[0].Qty);
        Assert.Equal(3.75, row.Bids[0].LimitPrice);
        Assert.Equal(3.75 - 3.0, row.Bids[0].RefDelta, 9);
        Assert.Equal(3 * 3.75, row.Bids[0].EscrowCredits, 9);
        Assert.Equal(low.Id, row.Bids[1].OrderId);
        Assert.Equal(5 * 2.0, row.Bids[1].EscrowCredits, 9);
    }

    [Fact]
    public void AZeroQtyOrderDoesNotSurfaceInTheBook()
    {
        var (model, state) = WithMarket();
        var dead = OrderOps.PostSell(state, state.Actors[0].Id, 0,
            (int)GoodId.Alloys, 4, 0.5, ask: 5.0, expiryYear: 1000);
        dead.QtyRemaining = 0;

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        Assert.Empty(card.Goods[(int)GoodId.Alloys].Asks);
    }

    // ---- AC3.3: the market states its currency, headline (not per-row).

    [Fact]
    public void TheCardNamesTheOwningPolitysCurrency()
    {
        var (model, state) = WithMarket();
        // seeded polities start pre-genesis; mint one for port 0's owner
        var currency = state.FoundCurrency(state.Actors[0].Id);

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        Assert.Equal(currency.Id, card.CurrencyId);
        Assert.Equal(currency.Name, card.CurrencyName);
        // the SAME hop state.LocalCurrencyOf(portId) makes — zero drift
        Assert.Equal(state.LocalCurrencyOf(0), card.CurrencyId);
    }

    [Fact]
    public void ACurrencylessPortCarriesTheAbsentSentinel()
    {
        var (model, state) = WithMarket();
        Assert.True(state.LocalCurrencyOf(0) < 0);   // pre-genesis, never founded

        var card = MarketPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        Assert.Equal(-1, card.CurrencyId);
        Assert.Null(card.CurrencyName);
    }
}
