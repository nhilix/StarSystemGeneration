using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Stage 2 (spec §4b): shipments are records — goods leave the
/// origin at departure, exist ONLY in the shipment while in transit, and
/// land at arrival; transit years come from the route over the lane
/// network (gate tier sets speed), off-lane legs at slow crawl; a severed
/// or quarantined leg stalls the freight where it floats.</summary>
public class ShipmentTests
{
    /// <summary>One polity, two tier-2 ports 10 hexes apart, a live tier-2
    /// lane between them.</summary>
    private static (SimState State, Port A, Port B) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);          // tier-2 gates, live
        state.WorldYear = 100;
        return (state, pa, pb);
    }

    [Fact]
    public void Requisition_TravelsTheLane_AndLandsInTheDestinationStockpile()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        // 10 hexes / (1 hex/yr × tier-2 speed 2.0) = 5 transit years
        state.Config.Economy.FreightHexesPerYearBase = 1.0;
        int g = (int)GoodId.Alloys;
        pa.DepositStock(g, 40, 0.7);
        double qty = pa.DrawStock(g, 25);

        var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, qty, 0.7) });

        Assert.NotNull(s);
        // conservation: in transit the goods exist ONLY in the shipment
        Assert.Equal(15.0, pa.StockQty[g], 6);
        Assert.Equal(0.0, pb.StockQty[g], 6);
        Assert.Equal(25.0, s!.Qty[g], 6);
        Assert.Equal(5.0, s.TotalYears, 6);
        Assert.Single(s.RouteLaneIds);
        // departure sails the remainder of the dispatching step
        Assert.Equal(1.0, s.YearsInTransit, 6);

        // four more one-year Markets steps bring it home
        int arrivedAfter = -1;
        for (int step = 1; step <= 6 && arrivedAfter < 0; step++)
        {
            var scratch = new MarketStepScratch(state);
            ShipmentOps.Advance(state, scratch);
            if (state.Shipments.Count == 0) arrivedAfter = step;
        }
        Assert.Equal(4, arrivedAfter);
        Assert.Equal(25.0, pb.StockQty[g], 6);
        Assert.Equal(0.7, pb.StockGrade[g], 6);
    }

    [Fact]
    public void ShortHops_DeliverWithinTheStep_NoRecord()
    {
        var (state, pa, pb) = Fixture();     // default speed: 0.625y transit
        int g = (int)GoodId.Alloys;
        var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, 10.0, 0.5) });
        // a transit inside the step's span is sub-step blur: delivered now
        Assert.Null(s);
        Assert.Empty(state.Shipments);
        Assert.Equal(10.0, pb.StockQty[g], 6);
        Assert.Equal(0.5, pb.StockGrade[g], 6);
    }

    [Fact]
    public void NoLiveLane_CrawlsOffLane()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.OffLaneFreightHexesPerYear = 2.0;
        // wreck a gate: the lane dies, the route falls back to open space
        state.Facilities[state.Lanes[0].GateAId].Condition = 0;
        int g = (int)GoodId.Alloys;
        var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, 10.0, 0.5) });
        Assert.NotNull(s);
        Assert.Empty(s!.RouteLaneIds);
        Assert.Equal(5.0, s.TotalYears, 6);          // 10 hexes / 2 per year
    }

    /// <summary>The phase wiring: in-flight shipments sail (and arrivals
    /// land) at the top of every Markets step, before supply, demand, and
    /// the Allocation that follows — arrived goods are drawable this step.</summary>
    [Fact]
    public void MarketsPhase_SailsShipments_ArrivalsLandBeforeTheDraws()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        int g = (int)GoodId.Alloys;
        pa.DepositStock(g, 25, 0.7);
        state.Segments.Add(new PopulationSegment(0, 0,
            state.PolityOf(0).SpeciesId, state.PolityOf(0).SpeciesId, 1.0));
        ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, pa.DrawStock(g, 25), 0.7) });
        Assert.Single(state.Shipments);

        for (int i = 0; i < 4; i++) new MarketsPhase().Run(state);

        Assert.Empty(state.Shipments);
        Assert.Equal(25.0, pb.StockQty[g], 6);
    }

    /// <summary>Spec §4b: in-transit goods are lost to piracy — a band
    /// hunting a lane on the route takes the cargo to its haven (conserved
    /// loot, the fence pays the pirates). RollChannel 75, keyed (step,
    /// owner, shipment).</summary>
    [Fact]
    public void Piracy_TakesTheCargo_ToTheHaven()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        int g = (int)GoodId.Alloys;

        Shipment Sail(double lossPerYear)
        {
            state.Shipments.Clear();
            state.Config.Corporate.ShipmentLossPerHuntedYear = lossPerYear;
            pa.DepositStock(g, 25, 0.7);
            var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
                ShipmentChannel.Requisition, pa.Id, pb.Id,
                new[] { (g, pa.DrawStock(g, 25), 0.7) });
            Assert.NotNull(s);
            return s!;
        }

        // a band hunts the lane from its haven at port A
        state.Corporations.Add(new Corporation(0, state.Actors[0].Id,
            "Red Sails", -1, CorporateNiche.Raiding, homePortId: 0,
            foundedYear: 90) { TargetId = 0 });

        var doomed = Sail(lossPerYear: 1.0);
        double havenBefore = state.Markets[0].Inventory[g];
        ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Empty(state.Shipments);                    // taken
        Assert.Equal(0.0, pb.StockQty[g], 6);             // never arrived
        Assert.Equal(havenBefore + 25.0,
            state.Markets[0].Inventory[g], 6);            // loot at the haven
        _ = doomed;

        var lucky = Sail(lossPerYear: 0.0);
        for (int i = 0; i < 4; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Empty(state.Shipments);                    // arrived intact
        Assert.Equal(25.0, pb.StockQty[g], 6);
        _ = lucky;
    }

    [Fact]
    public void Shipments_RoundTrip_ByteIdentical()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;
        int g = (int)GoodId.Alloys;
        pa.DepositStock(g, 25, 0.7);
        ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, pa.DrawStock(g, 25), 0.7) });
        Assert.Single(state.Shipments);

        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));

        Assert.Equal(state.NextShipmentId, loaded.NextShipmentId);
        var l = Assert.Single(loaded.Shipments);
        var s = state.Shipments[0];
        Assert.Equal(s.Id, l.Id);
        Assert.Equal(s.OwnerActorId, l.OwnerActorId);
        Assert.Equal(s.Channel, l.Channel);
        Assert.Equal(s.Qty[g], l.Qty[g]);
        Assert.Equal(s.Grade[g], l.Grade[g]);
        Assert.Equal(s.YearsInTransit, l.YearsInTransit);
        Assert.Equal(s.TotalYears, l.TotalYears);
        Assert.Equal(s.RouteLaneIds, l.RouteLaneIds);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void Quarantine_StallsTheShipment_UntilTheLaneReopens()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        int g = (int)GoodId.Alloys;
        pa.DepositStock(g, 25, 0.7);
        var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, pa.DrawStock(g, 25), 0.7) });
        Assert.NotNull(s);

        // the lane closes: the freight floats where it is, the ETA slides
        state.Lanes[0].QuarantinedUntil = 100000;
        for (int i = 0; i < 3; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Single(state.Shipments);
        Assert.Equal(1.0, s!.YearsInTransit, 6);     // unmoved past departure

        // reopened: the last delivery resumes at the pace it left
        state.Lanes[0].QuarantinedUntil = -1;
        for (int i = 0; i < 4; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Empty(state.Shipments);
        Assert.Equal(25.0, pb.StockQty[g], 6);
    }
}
