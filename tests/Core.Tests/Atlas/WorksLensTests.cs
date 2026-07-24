using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The works lens — the in-flight world (emap works parity, the
/// T2 layer): construction sites at their anchors, freight interpolated
/// along its route by sailed fraction, expedition convoys at their live
/// hexes. Stalled shipments read distinct (efreight's STALLED — the
/// blockade throttling logistics is map residue).</summary>
public class WorksLensTests
{
    private static (AtlasReadModel Model, SimState State) WithLane()
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
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, state.Actors[0].Id, b!.Value, tier: 2, foundedYear: 0));
        EpochTestKit.AddLane(state, 0, 1);
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void AnInFlightSiteMarksItsAnchor()
    {
        var (model, state) = WithLane();
        var project = new Project(0, ProjectKind.FacilityConstruction,
            state.Actors[0].Id, state.Actors[0].Id, portId: 0,
            state.Ports[0].Hex, yearsRequired: 10, startedYear: (int)state.WorldYear)
        { YearsDelivered = 4, LastFedFraction = 0.5 };
        state.Projects.Add(project);
        var mark = Assert.Single(
            WorksLens.Sites(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0, mark.ProjectId);
        Assert.Equal(state.Ports[0].Hex, mark.Hex);
        Assert.Equal(0.4, mark.Progress, precision: 9);
        Assert.Equal(0.5, mark.FedFraction, precision: 9);
    }

    [Fact]
    public void FinishedAndTravelingWorkLeavesNoSite()
    {
        var (model, state) = WithLane();
        state.Projects.Add(new Project(0, ProjectKind.PortRaise,
            state.Actors[0].Id, state.Actors[0].Id, 0, state.Ports[0].Hex,
            10, (int)state.WorldYear)
        { Completed = true });
        state.Projects.Add(new Project(1, ProjectKind.ColonyExpedition,
            state.Actors[0].Id, state.Actors[0].Id, 0, state.Ports[1].Hex,
            10, (int)state.WorldYear));
        Assert.Empty(WorksLens.Sites(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void AGatePairMarksBothEnds()
    {
        var (model, state) = WithLane();
        state.Projects.Add(new Project(0, ProjectKind.GatePair,
            state.Actors[0].Id, state.Actors[0].Id, 0, state.Ports[0].Hex,
            10, (int)state.WorldYear)
        { TargetId = 0 });
        var marks = WorksLens.Sites(model, EyeContext.God(state.WorldYear));
        Assert.Contains(marks, m => m.Hex.Equals(state.Ports[0].Hex));
        Assert.Contains(marks, m => m.Hex.Equals(state.Ports[1].Hex));
    }

    [Fact]
    public void FreightRidesItsRouteBySailedFraction()
    {
        var (model, state) = WithLane();
        var shipment = new Shipment(0, state.Actors[0].Id,
            ShipmentChannel.Requisition, originPortId: 0, destPortId: 1,
            departureYear: (int)state.WorldYear,
            routeLaneIds: new[] { 0 }, legYears: new[] { 10.0 })
        { YearsInTransit = 5.0 };
        state.Shipments.Add(shipment);
        var mark = Assert.Single(
            WorksLens.Freight(model, EyeContext.God(state.WorldYear)));
        var from = state.Ports[0].Hex;
        var to = state.Ports[1].Hex;
        Assert.Equal(HexGrid.Round(from.Q + (to.Q - from.Q) * 0.5,
                                   from.R + (to.R - from.R) * 0.5), mark.Hex);
        Assert.Equal(0.5, mark.Fraction, precision: 9);
        Assert.False(mark.Stalled);
        Assert.Equal(5.0, mark.RemainingYears, precision: 9);
    }

    [Fact]
    public void AClosedLegReadsStalled()
    {
        var (model, state) = WithLane();
        state.Shipments.Add(new Shipment(0, state.Actors[0].Id,
            ShipmentChannel.Freight, 0, 1, (int)state.WorldYear,
            new[] { 0 }, new[] { 10.0 }));
        state.Lanes[0].QuarantinedUntil = state.WorldYear + 5;
        var mark = Assert.Single(
            WorksLens.Freight(model, EyeContext.God(state.WorldYear)));
        Assert.True(mark.Stalled);
        // A stalled hold reads loud — its color departs the moving one's.
        state.Lanes[0].QuarantinedUntil = -1;
        var moving = Assert.Single(
            WorksLens.Freight(model, EyeContext.God(state.WorldYear)));
        Assert.NotEqual(moving.Color, mark.Color);
    }

    /// <summary>AC2.6: the four freight purposes read distinct colors while
    /// moving, war convoy loudest (WorksLens.FreightWarConvoy); a war
    /// convoy's rider is a War-priority courier contract carrying its
    /// shipment.</summary>
    [Fact]
    public void PurposeTintsTheMovingFreightMark_WarConvoyLoudest()
    {
        var (model, state) = WithLane();
        var stateHaul = new Shipment(0, state.Actors[0].Id,
            ShipmentChannel.Requisition, 0, 1, (int)state.WorldYear,
            new[] { 0 }, new[] { 10.0 });
        state.Shipments.Add(stateHaul);
        var spreadRun = new Shipment(1, state.Actors[0].Id,
            ShipmentChannel.Freight, 0, 1, (int)state.WorldYear,
            new[] { 0 }, new[] { 10.0 });
        state.Shipments.Add(spreadRun);
        var warShip = new Shipment(2, state.Actors[0].Id,
            ShipmentChannel.Requisition, 0, 1, (int)state.WorldYear,
            new[] { 0 }, new[] { 10.0 });
        state.Shipments.Add(warShip);
        state.Couriers.Add(new CourierContract(5, state.Actors[0].Id, 0, 1,
            10, CourierPriority.War, (int)state.WorldYear,
            (int)state.WorldYear + 5)
        { Status = CourierStatus.InTransit, ShipmentId = warShip.Id });

        var marks = WorksLens.Freight(model, EyeContext.God(state.WorldYear));
        Assert.Equal(3, marks.Count);
        var haulMark = marks[0];
        var spreadMark = marks[1];
        var warMark = marks[2];
        Assert.Equal(FreightPurpose.StateHaul, haulMark.Purpose);
        Assert.Equal(FreightPurpose.SpreadRun, spreadMark.Purpose);
        Assert.Equal(FreightPurpose.WarConvoy, warMark.Purpose);
        Assert.Equal(WorksLens.FreightStateHaul, haulMark.Color);
        Assert.Equal(WorksLens.FreightSpreadRun, spreadMark.Color);
        Assert.Equal(WorksLens.FreightWarConvoy, warMark.Color);
        // three distinct colors, none repeated
        Assert.NotEqual(haulMark.Color, spreadMark.Color);
        Assert.NotEqual(haulMark.Color, warMark.Color);
        Assert.NotEqual(spreadMark.Color, warMark.Color);
    }

    [Fact]
    public void AnOffLaneCrawlNeverStalls()
    {
        var (model, state) = WithLane();
        state.Shipments.Add(new Shipment(0, state.Actors[0].Id,
            ShipmentChannel.Requisition, 0, 1, (int)state.WorldYear,
            new int[0], new[] { 25.0 }));
        var mark = Assert.Single(
            WorksLens.Freight(model, EyeContext.God(state.WorldYear)));
        Assert.False(mark.Stalled);
    }

    /// <summary>AC4.1: an off-lane crawl's direct origin→dest line renders
    /// dashed — distinct from AC2.F2's solid recent-flow trails (a live
    /// crawl, not a memory). Dashes are ordered, non-overlapping fractions
    /// of the line, all carrying the shipment's id and a dimmer tint than
    /// the freight mark itself (WorksLens.FreightColorOf's full alpha).</summary>
    [Fact]
    public void AnOffLaneCrawlDashesItsDirectPath()
    {
        var (model, state) = WithLane();
        state.Shipments.Add(new Shipment(0, state.Actors[0].Id,
            ShipmentChannel.Requisition, 0, 1, (int)state.WorldYear,
            System.Array.Empty<int>(), new[] { 30.0 }));
        var dashes = WorksLens.CrawlPaths(model, EyeContext.God(state.WorldYear));
        Assert.NotEmpty(dashes);
        double lastEnd = -1;
        foreach (var d in dashes)
        {
            Assert.Equal(0, d.ShipmentId);
            Assert.Equal(state.Ports[0].Hex, d.Origin);
            Assert.Equal(state.Ports[1].Hex, d.Dest);
            Assert.InRange(d.FromFraction, 0.0, 1.0);
            Assert.InRange(d.ToFraction, 0.0, 1.0);
            Assert.True(d.ToFraction > d.FromFraction);
            Assert.True(d.FromFraction >= lastEnd);
            lastEnd = d.FromFraction;
        }
        // dimmer than the mark's own full-alpha purpose tint
        var full = WorksLens.FreightColorOf(FreightPurpose.SpreadRun, stalled: false);
        Assert.True(dashes[0].Color.A < full.A);
    }

    /// <summary>A lane-routed shipment never dashes — the crawl path is
    /// the off-lane special case (the honest line an off-lane runner
    /// actually sails; lane traffic follows the lane strokes instead).</summary>
    [Fact]
    public void ALaneRoutedShipmentHasNoCrawlPath()
    {
        var (model, state) = WithLane();
        state.Shipments.Add(new Shipment(0, state.Actors[0].Id,
            ShipmentChannel.Freight, 0, 1, (int)state.WorldYear,
            new[] { 0 }, new[] { 10.0 }));
        Assert.Empty(WorksLens.CrawlPaths(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void ExpeditionConvoysMarkTheirLiveHexes()
    {
        var (model, state) = WithLane();
        var design = DesignRegistry.Register(state, state.Actors[0].Id,
            ShipRole.Freight, ShipSize.Medium, grade: 0.5);
        var fleet = new FleetRecord(0, state.Actors[0].Id, state.Ports[0].Hex)
        { Posture = FleetPosture.Expedition };
        fleet.AddHulls(design.Id, 2, 0.5);
        state.Fleets.Add(fleet);
        var convoy = Assert.Single(
            WorksLens.Convoys(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(fleet.Hex, convoy.Hex);
        Assert.Equal(state.Actors[0].Id, convoy.OwnerActorId);
    }
}
