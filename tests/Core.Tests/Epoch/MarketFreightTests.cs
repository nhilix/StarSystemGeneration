using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Freight over the book (contract-economy spec §2 step 4): the
/// B1 bridge lifts the cheap end's asks toward the dear end's REAL resting
/// bids within posted capacity, with tariffs and legality at both ends;
/// procurement and project baskets post escrowed bids; reserve release
/// posts the sovereign's asks at starving ports.</summary>
public class MarketFreightTests
{
    /// <summary>Two entered polities (0 and 1), one port each, lane between
    /// them; a segment at each port.</summary>
    private static (SimState State, Port A, Port B) TwoPortFixture(
        bool sameOwner = false)
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[sameOwner ? 0 : 1];
        a0.Entered = true;
        a1.Entered = true;
        // pin provisioning dynamics: terran eaters regardless of seed rolls
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.TerranAnalog;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        // 10 hexes away: inside tier-2 lane reach (18+8)
        var hexB = new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R);
        var pb = new Port(1, a1.Id, hexB, tier: 2, foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int s0 = state.PolityOf(a0.Id).SpeciesId;
        int s1 = state.PolityOf(a1.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, s0, s0, 3.0) { Wealth = 500 });
        state.Segments.Add(new PopulationSegment(1, 1, s1, s1, 3.0) { Wealth = 500 });
        // the marine trades on the owner's OWN capital now (review wave,
        // finding 3) — a fixture polity needs a real working-capital float
        state.PolityOf(a0.Id).Credits = 5000;
        if (!sameOwner) state.PolityOf(a1.Id).Credits = 5000;
        state.WorldYear = 100;
        // freight only moves on posted hulls (slice E): six haulers on the lane
        EpochTestKit.PostFreight(state, a0.Id, laneId: 0, hulls: 6);
        return (state, pa, pb);
    }

    /// <summary>A resting bid at the destination — the REAL absorption the
    /// bridge reads (no phantom demand signal). Escrow drawn from the
    /// bidder's ledger.</summary>
    private static MarketOrder RestingBid(SimState state, int portId,
        int good, double qty, double bid)
    {
        var owner = state.Ports[portId].OwnerActorId;
        state.LedgerOf(owner).Credits -= qty * bid;
        return OrderOps.PostBuy(state, owner, portId, good, qty, bid,
            state.WorldYear + 1000);
    }

    [Fact]
    public void Bridge_MovesGoodsCheapToDear_WithinLaneCapacity()
    {
        var (state, pa, pb) = TwoPortFixture();
        int g = (int)GoodId.Provisions;
        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        RestingBid(state, 1, g, qty: 500,
            bid: state.Markets[0].Price[g] * 4);
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        // sub-step transit: the cargo posts at the destination as the
        // exporter's asks, ready for matching
        double moved = BookOps.AskQty(state, 1, g);
        Assert.True(moved > 0, "profitable resting bids should move goods");
        double cap = FleetOps.PostedCapacity(state, state.Lanes[0]);
        Assert.True(cap > 0, "six posted haulers should carry something");
        Assert.True(moved <= cap + 1e-9, $"moved {moved} over posted capacity {cap}");
        Assert.True(BookOps.AskQty(state, 0, g) < 1000);
        Assert.Equal(0.6, BookOps.AskGrade(state, 1, g), 6); // grade travels
    }

    [Fact]
    public void SeveredLane_CarriesNothing()
    {
        var (state, _, _) = TwoPortFixture();
        int g = (int)GoodId.Provisions;
        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        RestingBid(state, 1, g, 500, state.Markets[0].Price[g] * 4);
        EpochTestKit.BlockadePort(state, 1, portId: 0);
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, BookOps.AskQty(state, 1, g), 6);
    }

    [Fact]
    public void Tariff_IsPaidToTheDestinationPolity()
    {
        var (state, _, pb) = TwoPortFixture();
        int g = (int)GoodId.Provisions;
        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        state.Actors[pb.OwnerActorId].Policies = PolityPolicies.Default with
        {
            TariffSchedule = new Dictionary<int, double>
            { [g] = 0.10 },
        };
        RestingBid(state, 1, g, 500, state.Markets[0].Price[g] * 4);
        double before = state.PolityOf(pb.OwnerActorId).Credits;
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.True(BookOps.AskQty(state, 1, g) > 0,
            "a 10% tariff should not kill a 4x gap");
        Assert.True(state.PolityOf(pb.OwnerActorId).Credits > before,
            "the tariff take should land with the destination polity");
    }

    [Fact]
    public void Prohibition_AtEitherEnd_BlocksTheLane()
    {
        var (state, _, pb) = TwoPortFixture();
        int g = (int)GoodId.Narcotics;
        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        state.Actors[pb.OwnerActorId].Policies = PolityPolicies.Default with
        {
            LawCode = new Dictionary<int, LegalityLevel>
            { [g] = LegalityLevel.Prohibited },
        };
        RestingBid(state, 1, g, 500, state.Markets[0].Price[g] * 5);
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, BookOps.AskQty(state, 1, g), 6);
    }

    /// <summary>Stage 2 (spec §4b): the bridge's routed goods take transit
    /// time — a slow route turns the trade into a shipment record arriving
    /// in a future year; costs settle at departure, the cargo posts on the
    /// arrival book (no reservation — the spread-run rule).</summary>
    [Fact]
    public void Bridge_OverASlowRoute_BecomesAShipmentInTransit()
    {
        var (state, pa, pb) = TwoPortFixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        int g = (int)GoodId.Provisions;
        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        RestingBid(state, 1, g, 500, state.Markets[0].Price[g] * 4);
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, BookOps.AskQty(state, 1, g), 6);
        var s = Assert.Single(state.Shipments);
        Assert.Equal(ShipmentChannel.Freight, s.Channel);
        Assert.True(s.Qty[g] > 0, "the routed goods ride the shipment");
        Assert.True(BookOps.AskQty(state, 0, g) < 1000,
            "the goods left the source at departure");

        for (int i = 0; i < 4; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Empty(state.Shipments);
        Assert.True(BookOps.AskQty(state, 1, g) > 0,
            "arrival posts the cargo on the destination book");
    }

    /// <summary>Procurement posts escrowed bids and its fills bank into the
    /// LOCAL port stockpile — stock lands where it was bought (spec §4b).</summary>
    [Fact]
    public void Procurement_FillsThePortStockpile_FromItsOwnBook()
    {
        var (state, pa, _) = TwoPortFixture();
        int g = (int)GoodId.Provisions;
        EpochTestKit.Stock(state, 0, g, 500, 0.6);
        state.Actors[pa.OwnerActorId].Policies = PolityPolicies.Default with
        {
            StockpileTargets = new Dictionary<int, double>
            { [g] = 20.0 },
        };
        var pr = state.PolityOf(pa.OwnerActorId);
        // procurement spends the RESERVE treasury — a drained credit
        // balance no longer silences the quartermaster
        pr.Credits = 0;
        pr.ReservePoints = 100;
        var scratch = new MarketStepScratch(state);

        MarketEngine.PostProcurementBids(state, scratch);
        MarketEngine.MatchAndClear(state, scratch);

        Assert.True(pa.StockQty[g] > 0);
        Assert.True(pr.ReservePoints < 100, "procurement is bought, not taken");
        Assert.True(BookOps.AskQty(state, 0, g) < 500);
        Assert.Equal(0.6, pa.StockGrade[g], 6);
    }

    /// <summary>Capacity is built, not assumed (spec §4b): the port's own
    /// tier bounds the stockpile; depots extend it.</summary>
    [Fact]
    public void Procurement_StopsAtThePortsStockCapacity()
    {
        var (state, pa, _) = TwoPortFixture();
        state.PolityOf(pa.OwnerActorId).ReservePoints = 100;
        state.Config.Economy.StockCapPerPortTier = 3.0;   // tier 2 → cap 6
        int g = (int)GoodId.Provisions;
        EpochTestKit.Stock(state, 0, g, 500, 0.6);
        state.Actors[pa.OwnerActorId].Policies = PolityPolicies.Default with
        {
            StockpileTargets = new Dictionary<int, double>
            { [g] = 20.0 },
        };
        var scratch = new MarketStepScratch(state);

        MarketEngine.PostProcurementBids(state, scratch);
        MarketEngine.MatchAndClear(state, scratch);

        Assert.Equal(6.0, pa.StockQty[g], 6);
    }

    [Fact]
    public void StockRelease_FeedsTheStarvingPort_FromItsOwnStockpile()
    {
        var (state, pa, _) = TwoPortFixture();
        pa.StockQty[(int)GoodId.Provisions] = 100;
        pa.StockGrade[(int)GoodId.Provisions] = 0.5;
        state.Segments[0].LastSubsistence = 0.4;      // last step starved
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Provisions) > 0,
            "stockpiles should buffer famines (economy/markets.md §Stockpiles)");
        Assert.True(pa.StockQty[(int)GoodId.Provisions] < 100);
    }

    /// <summary>The construction pull is literal now: a project half a year
    /// from completion bids for half a year's basket, escrowed from the
    /// funder's treasury — not the whole span's.</summary>
    [Fact]
    public void ProjectBids_TaperToTheRemainingYears()
    {
        var (state, pa, _) = TwoPortFixture();
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise,
            pa.OwnerActorId, pa.OwnerActorId, pa.Id, pa.Hex,
            yearsRequired: 10.0, ProjectPriority.Core, 0);
        p.TargetId = pa.Id;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.YearsDelivered = 9.5;                    // half a year to go
        state.PolityOf(pa.OwnerActorId).DevelopmentPoints = 1000;
        var scratch = new MarketStepScratch(state);

        MarketEngine.PostProjectBids(state, scratch);

        var (order, project) = Assert.Single(scratch.ProjectBids);
        Assert.Same(p, project);
        Assert.Equal((int)GoodId.Alloys, order.Good);
        Assert.Equal(0.5, order.QtyRemaining, 6);
        Assert.True(order.EscrowCredits > 0, "the treasury escrows the bid");
    }

    /// <summary>The stockpile-target bids are located too: each own port
    /// bids its share of the target at ITS book — procurement is a market
    /// participant everywhere it banks.</summary>
    [Fact]
    public void ProcurementBids_LandPerPort()
    {
        var (state, pa, pb) = TwoPortFixture(sameOwner: true);
        state.Actors[pa.OwnerActorId].Policies = PolityPolicies.Default with
        {
            StockpileTargets = new Dictionary<int, double>
            { [(int)GoodId.Provisions] = 20.0 },
        };
        state.PolityOf(pa.OwnerActorId).ReservePoints = 100;
        var scratch = new MarketStepScratch(state);

        MarketEngine.PostProcurementBids(state, scratch);

        Assert.Equal(2, scratch.ProcureBids.Count);
        Assert.Equal(10.0, scratch.ProcureBids[0].Order.QtyRemaining, 6);
        Assert.Equal(10.0, scratch.ProcureBids[1].Order.QtyRemaining, 6);
        Assert.Equal(pa.Id, scratch.ProcureBids[0].Port.Id);
        Assert.Equal(pb.Id, scratch.ProcureBids[1].Port.Id);
    }

    /// <summary>C8: the trader is whoever POSTED the hulls — a corp fleet
    /// on the lane buys the spread with corp capital and owns the arrival
    /// asks; its profit books when they sell (P5: profit walks the goods).</summary>
    [Fact]
    public void SpreadRun_ACorpFleet_TradesWithItsOwnCapital()
    {
        // a lane whose ONLY posted hulls are a corp's — no merchant marine
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.TerranAnalog;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2, 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        state.WorldYear = 100;
        int g = (int)GoodId.Provisions;
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation,
            "Test Line", pa.Hex, 0, new CorporateController(state.Config))
        { Entered = true });
        var corp = new Corporation(0, actorId, "Test Line", 0,
            CorporateNiche.Freight, pa.Id, 0) { Credits = 500 };
        state.Corporations.Add(corp);
        EpochTestKit.PostFreight(state, actorId, laneId: 0, hulls: 6);

        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        state.LedgerOf(0).Credits += 4000;
        double bid = state.Markets[0].Price[g] * 4;
        state.LedgerOf(0).Credits -= 300 * bid;
        OrderOps.PostBuy(state, 0, 1, g, 300, bid, state.WorldYear + 1000);

        var scratch = new MarketStepScratch(state);
        MarketEngine.MoveFreight(state, scratch);

        // the corp paid for the goods out of its own book...
        Assert.True(corp.Credits < 500,
            "the trader fronts the purchase with its own capital");
        // ...and owns the asks now resting at the destination
        Assert.Contains(state.Orders, o => o.Side == OrderSide.Sell
            && o.PortId == 1 && o.Good == g && o.OwnerActorId == actorId);
    }

    /// <summary>Review fix (CE wave, finding 3): a spread run is fronted by
    /// the trader's OWN capital — a broke corp buys nothing, however steep
    /// the gradient, instead of dipping thousands negative and getting
    /// bankrupt-dissolved the same step.</summary>
    [Fact]
    public void SpreadRun_ABrokeTrader_BuysNothing()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.TerranAnalog;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2, 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        state.WorldYear = 100;
        int g = (int)GoodId.Provisions;
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation,
            "Empty Pockets", pa.Hex, 0, new CorporateController(state.Config))
        { Entered = true });
        var corp = new Corporation(0, actorId, "Empty Pockets", 0,
            CorporateNiche.Freight, pa.Id, 0) { Credits = 0 };
        state.Corporations.Add(corp);
        EpochTestKit.PostFreight(state, actorId, laneId: 0, hulls: 6);
        EpochTestKit.Stock(state, 0, g, 1000, 0.6);
        state.LedgerOf(0).Credits += 4000;
        double bid = state.Markets[0].Price[g] * 4;
        state.LedgerOf(0).Credits -= 300 * bid;
        OrderOps.PostBuy(state, 0, 1, g, 300, bid, state.WorldYear + 1000);

        var scratch = new MarketStepScratch(state);
        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, corp.Credits, 9);
        Assert.DoesNotContain(state.Orders, o => o.Side == OrderSide.Sell
            && o.PortId == 1 && o.OwnerActorId == actorId);
    }

    [Fact]
    public void GenesisController_TargetsAProvisionsReserve()
    {
        var view = new PerceptionView(0, 0, new int[0], ownPortCount: 3);
        var policies = (PolityPolicies)new GenesisController(new EpochSimConfig())
            .Decide(view).Policies;
        Assert.True(policies.StockpileTargets.TryGetValue(
            (int)GoodId.Provisions, out double target) && target > 0);
    }

    [Fact]
    public void ConnectedMarkets_Converge_SeveredMarkets_Spike()
    {
        // A produces provisions and fuel (a homeworld), B only consumes (a
        // colony) — freight burns fuel, so the source needs a fuel chain
        var (state, pa, pb) = TwoPortFixture(sameOwner: true);
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.AgriComplex, 2,
            pa.Hex, pa.OwnerActorId, state.WorldYear - 10));
        state.Facilities.Add(new Facility(1, (int)InfraTypeId.Skimmer, 1,
            pa.Hex, pa.OwnerActorId, state.WorldYear - 10));
        state.Facilities.Add(new Facility(2, (int)InfraTypeId.Refinery, 1,
            pa.Hex, pa.OwnerActorId, state.WorldYear - 10));
        var phase = new MarketsPhase();

        for (int i = 0; i < 8; i++) phase.Run(state);
        double connected = state.Markets[1].Price[(int)GoodId.Provisions];

        EpochTestKit.BlockadePort(state, 1, portId: 0);
        for (int i = 0; i < 4; i++) phase.Run(state);
        double blockaded = state.Markets[1].Price[(int)GoodId.Provisions];

        Assert.True(blockaded > connected * 1.5,
            $"blockade should spike the strangled port: {connected} -> {blockaded}");
    }

    [Fact]
    public void FullMarketStep_WithFreight_ConservesCredits()
    {
        var (state, pa, _) = TwoPortFixture();
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.AgriComplex, 2,
            pa.Hex, pa.OwnerActorId, state.WorldYear - 10));
        EpochTestKit.Stock(state, 0, (int)GoodId.Provisions, 200, 0.5);

        double before = Total(state);
        new MarketsPhase().Run(state);
        Assert.Equal(before, Total(state), 6);
    }

    /// <summary>Credits live in ledgers, segment wealth, open-order escrow,
    /// and courier fee escrow — all conserved together (spec §5).</summary>
    private static double Total(SimState state)
    {
        double total = 0;
        foreach (var p in state.Polities)
            total += p.Credits + p.DevelopmentPoints + p.MilitaryPoints
                     + p.ExpansionPoints + p.ReservePoints;
        foreach (var s in state.Segments) total += s.Wealth;
        foreach (var o in state.Orders) total += o.EscrowCredits;
        foreach (var c in state.Corporations) total += c.Credits;
        foreach (var c in state.Couriers) total += c.FeeEscrow;
        return total;
    }
}
