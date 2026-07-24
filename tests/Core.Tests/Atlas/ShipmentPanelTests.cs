using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Shipment card (NEW, T2) — click a works-lens freight
/// mark. Parity target: `efreight` (Repl.RenderFreight): route, cargo,
/// sailed/total, live eta = WorldYear + ceil(TotalYears − YearsInTransit),
/// STALLED when the CURRENT leg is severed, quarantined (`&gt;`, the
/// faithful clock edge), or dead.</summary>
public class ShipmentPanelTests
{
    private static (AtlasReadModel Model, SimState State, Lane Lane)
        WithShipment()
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
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, 2, 0));
        state.Ports.Add(new Port(1, state.Actors[1].Id, b!.Value, 2, 0));
        var lane = EpochTestKit.AddLane(state, 0, 1);
        var s = new Shipment(0, state.Actors[0].Id, ShipmentChannel.Freight,
            originPortId: 0, destPortId: 1,
            departureYear: (int)state.WorldYear,
            routeLaneIds: new[] { lane.Id }, legYears: new[] { 6.0 })
        { YearsInTransit = 2.5 };
        s.Qty[(int)GoodId.Provisions] = 12.0;
        s.Grade[(int)GoodId.Provisions] = 0.6;
        state.Shipments.Add(s);
        return (new AtlasReadModel(state), state, lane);
    }

    [Fact]
    public void TheCardCarriesRouteCargoAndSailedYears()
    {
        var (model, state, _) = WithShipment();
        var card = Assert.Single(
            ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0, card.OriginPortId);
        Assert.Equal(1, card.DestPortId);
        Assert.Equal(1, card.LaneCount);
        Assert.Equal(ShipmentChannel.Freight, card.Channel);
        Assert.Equal(state.Actors[0].Name, card.OwnerName);
        Assert.Equal(2.5, card.SailedYears);
        Assert.Equal(6.0, card.TotalYears);
        var line = Assert.Single(card.Cargo);
        Assert.Equal(GoodId.Provisions, line.Good);
        Assert.Equal(12.0, line.Qty);
        Assert.Equal(0.6, line.Grade);
    }

    [Fact]
    public void TheLiveEtaIsCeilOfTheRemainingYears()
    {
        var (model, state, _) = WithShipment();
        var card = Assert.Single(
            ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.False(card.Stalled);
        Assert.Equal(state.WorldYear + 4, card.EtaYear);   // ceil(3.5)
    }

    [Fact]
    public void AQuarantinedCurrentLegStalls_OnTheFaithfulClockEdge()
    {
        var (model, state, lane) = WithShipment();
        var eye = EyeContext.God(state.WorldYear);
        // efreight's explicit check reads `>`, but SeveredLaneIds folds
        // quarantines in at `>=` first — so the EFFECTIVE efreight edge is
        // `>=`, and a lane quarantined exactly TO WorldYear stalls. Ported
        // faithfully (the K2 ledger's clock-edge note resolves this way).
        lane.QuarantinedUntil = state.WorldYear - 1;
        Assert.False(Assert.Single(ShipmentPanel.Cards(model, eye)).Stalled);
        lane.QuarantinedUntil = state.WorldYear;
        var card = Assert.Single(ShipmentPanel.Cards(model, eye));
        Assert.True(card.Stalled);
        Assert.Null(card.EtaYear);
    }

    [Fact]
    public void ABlockadedApproachStallsTheShipment()
    {
        var (model, state, _) = WithShipment();
        EpochTestKit.BlockadePort(state, state.Actors[1].Id, portId: 0);
        var card = Assert.Single(
            ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.True(card.Stalled);
    }

    [Fact]
    public void AnOffLaneCrawlNeverStalls()
    {
        var (model, state, _) = WithShipment();
        var crawl = new Shipment(1, state.Actors[0].Id,
            ShipmentChannel.Requisition, originPortId: 0, destPortId: 1,
            departureYear: (int)state.WorldYear,
            routeLaneIds: System.Array.Empty<int>(),
            legYears: new[] { 9.0 });
        state.Shipments.Add(crawl);
        var cards = ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear));
        Assert.Equal(2, cards.Count);
        Assert.False(cards[1].Stalled);
        Assert.Equal(state.WorldYear + 9, cards[1].EtaYear);
    }

    /// <summary>AC4.1: OffLane flags by the RenderFreight idiom
    /// (RouteLaneIds.Count == 0) — lane-routed false, off-lane true; no
    /// war staged, so the peacetime detection context reads clean even
    /// off-lane (nothing hostile to evade — PatrolCoverage's own §5 gate).</summary>
    [Fact]
    public void OffLaneFlagsByRouteLaneCount_CleanContextWithNoWar()
    {
        var (model, state, _) = WithShipment();
        var crawl = new Shipment(1, state.Actors[0].Id,
            ShipmentChannel.Requisition, originPortId: 0, destPortId: 1,
            departureYear: (int)state.WorldYear,
            routeLaneIds: System.Array.Empty<int>(), legYears: new[] { 9.0 });
        state.Shipments.Add(crawl);
        var cards = ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear));
        Assert.False(cards[0].OffLane);
        Assert.False(cards[0].CrossesPatrolledSpace);
        Assert.True(cards[1].OffLane);
        Assert.False(cards[1].CrossesPatrolledSpace);
    }

    /// <summary>AC4.1: a peacetime hostile-would-be patrol projects nothing
    /// (PatrolCoverage's own hostile-only gate) — only an ACTIVE war
    /// between the patrol's owner and the shipment's owner, with coverage
    /// actually reaching the direct path, flips the context flag. Never a
    /// duplicated seizure roll — a bool read of PatrolCoverage.At only.</summary>
    [Fact]
    public void CrossesPatrolledSpace_OnlyUnderActiveWarWithCoverageOnThePath()
    {
        var (model, state, _) = WithShipment();
        var crawl = new Shipment(1, state.Actors[0].Id,
            ShipmentChannel.Requisition, originPortId: 0, destPortId: 1,
            departureYear: (int)state.WorldYear,
            routeLaneIds: System.Array.Empty<int>(), legYears: new[] { 9.0 });
        state.Shipments.Add(crawl);
        int enemy = 99;
        // patrol docked but no war yet — clean
        state.Fleets.Add(new FleetRecord(state.Fleets.Count, ownerActorId: enemy,
            state.Ports[1].Hex) { Posture = FleetPosture.Patrol, Body = BodyRef.None });
        var before = ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear));
        Assert.False(before[1].CrossesPatrolledSpace);
        // war declared — now the crawl's context flags the crossing; the
        // lane-routed sibling is unaffected (off-lane-only context)
        state.Wars.Add(new War(state.Wars.Count, "the Coverage War",
            enemy, state.Actors[0].Id, CasusBelli.BorderIncident, -1,
            WarDemand.CedeObjectives, state.WorldYear));
        var after = ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear));
        Assert.True(after[1].CrossesPatrolledSpace);
        Assert.False(after[0].CrossesPatrolledSpace);
    }

    [Fact]
    public void OneCardByShipmentId_TheFreightMarkClickTarget()
    {
        var (model, state, _) = WithShipment();
        var card = ShipmentPanel.Card(model,
            EyeContext.God(state.WorldYear), shipmentId: 0);
        Assert.NotNull(card);
        Assert.Equal(0, card!.Id);
        Assert.Null(ShipmentPanel.Card(model,
            EyeContext.God(state.WorldYear), shipmentId: 99));
    }

    /// <summary>AC2.6: with no rider courier, the card's Purpose reads by
    /// channel alone (the fixture shipment is Freight-channel) and there
    /// is no Rider row.</summary>
    [Fact]
    public void WithNoRider_PurposeReadsByChannelAndRiderIsNull()
    {
        var (model, state, _) = WithShipment();
        var card = Assert.Single(
            ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(FreightPurpose.SpreadRun, card.Purpose);
        Assert.Null(card.Rider);
    }

    /// <summary>AC2.6: a War-priority rider reads as a war convoy and
    /// carries the SAME row `econtracts`/the job board would print — no
    /// duplicated contract formatting.</summary>
    [Fact]
    public void AWarPriorityRiderReadsAsAWarConvoyWithItsContractRow()
    {
        var (model, state, _) = WithShipment();
        var s = state.Shipments[0];
        var c = new CourierContract(11, s.OwnerActorId, s.OriginPortId,
            s.DestPortId, 42.0, CourierPriority.War, (int)state.WorldYear,
            (int)state.WorldYear + 5)
        { Status = CourierStatus.InTransit, ShipmentId = s.Id };
        state.Couriers.Add(c);

        var card = Assert.Single(
            ShipmentPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(FreightPurpose.WarConvoy, card.Purpose);
        Assert.NotNull(card.Rider);
        Assert.Equal(11, card.Rider!.Id);
        Assert.Equal(42.0, card.Rider.FeeEscrow);
    }
}
