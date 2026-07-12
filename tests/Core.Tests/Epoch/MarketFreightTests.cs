using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D task 4 — freight (economy/markets.md §4): re-export
/// demand, lane arbitrage within capacity, tariffs and legality at both ends,
/// polity procurement into reserves, reserve release to famine ports, and the
/// severed-lane debug hook (the blockade eyeball).</summary>
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
        state.PolityOf(a0.Id).Credits = 500;
        if (!sameOwner) state.PolityOf(a1.Id).Credits = 500;
        state.WorldYear = 100;
        // freight only moves on posted hulls (slice E): six haulers on the lane
        EpochTestKit.PostFreight(state, a0.Id, laneId: 0, hulls: 6);
        return (state, pa, pb);
    }

    [Fact]
    public void ReExport_BidsUpTheOutboundGradient()
    {
        var (state, _, _) = TwoPortFixture();
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mB.Price[(int)GoodId.Provisions] = mA.Price[(int)GoodId.Provisions] * 3;
        var scratch = new MarketStepScratch(state);

        MarketEngine.AddReExportDemand(state, scratch);

        Assert.True(scratch.Demand[0][(int)GoodId.Provisions] > 0,
            "outbound gradient should register demand at the source hub");
        Assert.Equal(0.0, scratch.Demand[1][(int)GoodId.Provisions]);
    }

    [Fact]
    public void Arbitrage_MovesGoodsCheapToDear_WithinLaneCapacity()
    {
        var (state, pa, pb) = TwoPortFixture();
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mA.Deposit((int)GoodId.Provisions, 1000, 0.6);
        mB.Price[(int)GoodId.Provisions] = mA.Price[(int)GoodId.Provisions] * 4;
        var scratch = new MarketStepScratch(state);
        scratch.Demand[1][(int)GoodId.Provisions] = 500;   // hungry destination

        MarketEngine.MoveFreight(state, scratch);

        double moved = mB.Inventory[(int)GoodId.Provisions];
        Assert.True(moved > 0, "profitable gap should move goods");
        double cap = FleetOps.PostedCapacity(state, state.Lanes[0]);
        Assert.True(cap > 0, "six posted haulers should carry something");
        Assert.True(moved <= cap + 1e-9, $"moved {moved} over posted capacity {cap}");
        Assert.True(mA.Inventory[(int)GoodId.Provisions] < 1000);
        Assert.Equal(0.6, mB.InventoryGrade[(int)GoodId.Provisions]);  // grade travels
    }

    [Fact]
    public void SeveredLane_CarriesNothing()
    {
        var (state, _, _) = TwoPortFixture();
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mA.Deposit((int)GoodId.Provisions, 1000, 0.6);
        mB.Price[(int)GoodId.Provisions] = mA.Price[(int)GoodId.Provisions] * 4;
        EpochTestKit.BlockadePort(state, 1, portId: 0);
        var scratch = new MarketStepScratch(state);
        scratch.Demand[1][(int)GoodId.Provisions] = 500;

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, mB.Inventory[(int)GoodId.Provisions]);
    }

    [Fact]
    public void Tariff_IsPaidToTheDestinationPolity()
    {
        var (state, _, pb) = TwoPortFixture();
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mA.Deposit((int)GoodId.Provisions, 1000, 0.6);
        mB.Price[(int)GoodId.Provisions] = mA.Price[(int)GoodId.Provisions] * 4;
        state.Actors[pb.OwnerActorId].Policies = PolityPolicies.Default with
        {
            TariffSchedule = new Dictionary<int, double>
            { [(int)GoodId.Provisions] = 0.10 },
        };
        double before = state.PolityOf(pb.OwnerActorId).Credits;
        var scratch = new MarketStepScratch(state);
        scratch.Demand[1][(int)GoodId.Provisions] = 500;

        MarketEngine.MoveFreight(state, scratch);

        Assert.True(mB.Inventory[(int)GoodId.Provisions] > 0,
            "a 10% tariff should not kill a 4x gap");
        Assert.True(state.PolityOf(pb.OwnerActorId).Credits > before,
            "the tariff take should land with the destination polity");
    }

    [Fact]
    public void Prohibition_AtEitherEnd_BlocksTheLane()
    {
        var (state, _, pb) = TwoPortFixture();
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mA.Deposit((int)GoodId.Narcotics, 1000, 0.6);
        mB.Price[(int)GoodId.Narcotics] = mA.Price[(int)GoodId.Narcotics] * 5;
        state.Actors[pb.OwnerActorId].Policies = PolityPolicies.Default with
        {
            LawCode = new Dictionary<int, LegalityLevel>
            { [(int)GoodId.Narcotics] = LegalityLevel.Prohibited },
        };
        var scratch = new MarketStepScratch(state);
        scratch.Demand[1][(int)GoodId.Narcotics] = 500;

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, mB.Inventory[(int)GoodId.Narcotics]);
    }

    /// <summary>Stage 2 (spec §4b, §5 Freight-delivery row): the market
    /// channel's routed goods take transit time — a slow route turns the
    /// trade into a shipment record arriving in a future year; costs still
    /// settle at departure, the sale lands with arrival.</summary>
    [Fact]
    public void Arbitrage_OverASlowRoute_BecomesAShipmentInTransit()
    {
        var (state, pa, pb) = TwoPortFixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mA.Deposit((int)GoodId.Provisions, 1000, 0.6);
        mB.Price[(int)GoodId.Provisions] = mA.Price[(int)GoodId.Provisions] * 4;
        var scratch = new MarketStepScratch(state);
        scratch.Demand[1][(int)GoodId.Provisions] = 500;

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(0.0, mB.Inventory[(int)GoodId.Provisions]);
        var s = Assert.Single(state.Shipments);
        Assert.Equal(ShipmentChannel.Freight, s.Channel);
        Assert.True(s.Qty[(int)GoodId.Provisions] > 0,
            "the routed goods ride the shipment");
        Assert.True(mA.Inventory[(int)GoodId.Provisions] < 1000,
            "the goods left the source at departure");

        for (int i = 0; i < 4; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Empty(state.Shipments);
        Assert.True(mB.Inventory[(int)GoodId.Provisions] > 0,
            "arrival puts the cargo on the destination shelf");
    }

    /// <summary>Stage 2 (spec §4b): procurement buys into the LOCAL port
    /// stockpile — stock lands where it was bought, never in a polity pool.</summary>
    [Fact]
    public void Procurement_FillsThePortStockpile_FromItsOwnMarket()
    {
        var (state, pa, _) = TwoPortFixture();
        var mA = state.Markets[0];
        mA.Deposit((int)GoodId.Provisions, 500, 0.6);
        state.Actors[pa.OwnerActorId].Policies = PolityPolicies.Default with
        {
            StockpileTargets = new Dictionary<int, double>
            { [(int)GoodId.Provisions] = 20.0 },
        };
        var pr = state.PolityOf(pa.OwnerActorId);
        double creditsBefore = pr.Credits;
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.True(pa.StockQty[(int)GoodId.Provisions] > 0);
        Assert.True(pr.Credits < creditsBefore, "procurement is bought, not taken");
        Assert.True(mA.Inventory[(int)GoodId.Provisions] < 500);
        Assert.Equal(0.6, pa.StockGrade[(int)GoodId.Provisions]);
    }

    /// <summary>Capacity is built, not assumed (spec §4b): the port's own
    /// tier bounds the stockpile; depots extend it.</summary>
    [Fact]
    public void Procurement_StopsAtThePortsStockCapacity()
    {
        var (state, pa, _) = TwoPortFixture();
        state.Config.Economy.StockCapPerPortTier = 3.0;   // tier 2 → cap 6
        var mA = state.Markets[0];
        mA.Deposit((int)GoodId.Provisions, 500, 0.6);
        state.Actors[pa.OwnerActorId].Policies = PolityPolicies.Default with
        {
            StockpileTargets = new Dictionary<int, double>
            { [(int)GoodId.Provisions] = 20.0 },
        };
        var scratch = new MarketStepScratch(state);

        MarketEngine.MoveFreight(state, scratch);

        Assert.Equal(6.0, pa.StockQty[(int)GoodId.Provisions], 6);
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

        Assert.True(state.Markets[0].Inventory[(int)GoodId.Provisions] > 0,
            "stockpiles should buffer famines (economy/markets.md §Stockpiles)");
        Assert.True(pa.StockQty[(int)GoodId.Provisions] < 100);
    }

    /// <summary>The stockpile-target pull is located too: each own port's
    /// deficit against its share of the target registers at THAT port —
    /// procurement is a market participant everywhere it banks.</summary>
    [Fact]
    public void StockpileTargetDemand_LandsPerPort()
    {
        var (state, pa, pb) = TwoPortFixture(sameOwner: true);
        state.Actors[pa.OwnerActorId].Policies = PolityPolicies.Default with
        {
            StockpileTargets = new Dictionary<int, double>
            { [(int)GoodId.Provisions] = 20.0 },
        };
        var scratch = new MarketStepScratch(state);

        MarketEngine.AddConstructionPull(state, scratch);

        Assert.Equal(10.0, scratch.Demand[pa.Id][(int)GoodId.Provisions], 6);
        Assert.Equal(10.0, scratch.Demand[pb.Id][(int)GoodId.Provisions], 6);
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
        state.Markets[0].Deposit((int)GoodId.Provisions, 200, 0.5);

        double before = Total(state);
        new MarketsPhase().Run(state);
        Assert.Equal(before, Total(state), 6);
    }

    private static double Total(SimState state)
    {
        double total = 0;
        foreach (var p in state.Polities) total += p.Credits;
        foreach (var s in state.Segments) total += s.Wealth;
        return total;
    }
}
