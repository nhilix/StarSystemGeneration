using System;
using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>AC2.F2 — recent flows: launches captured at step time carry
/// the moving economy the epoch-boundary registries can never show. The
/// purpose rule is FreightPurposeQuery's (from parts, because a sub-step
/// courier is already resolved when the registry would be asked); only
/// couriers and war convoys RENDER (user scope, 2026-07-22); corridors
/// aggregate so overdraw reads as intensity, not mud.</summary>
public class RecentFlowTests
{
    [Fact]
    public void FromParts_MatchesTheEfreightRule()
    {
        // rider decides first (War → war convoy, else courier); with no
        // rider the channel decides (Freight → spread run, else state haul)
        Assert.Equal(FreightPurpose.WarConvoy, FreightPurposeQuery.FromParts(
            ShipmentChannel.Requisition, true, CourierPriority.War));
        Assert.Equal(FreightPurpose.Courier, FreightPurposeQuery.FromParts(
            ShipmentChannel.Requisition, true, CourierPriority.Normal));
        Assert.Equal(FreightPurpose.SpreadRun, FreightPurposeQuery.FromParts(
            ShipmentChannel.Freight, false, CourierPriority.Normal));
        Assert.Equal(FreightPurpose.StateHaul, FreightPurposeQuery.FromParts(
            ShipmentChannel.Requisition, false, CourierPriority.Normal));
    }

    [Fact]
    public void Capture_DerivesThePurposeFromTheLaunch()
    {
        var route = new[] { new HexCoordinate(0, 0), new HexCoordinate(5, 0) };
        var launch = new ShipmentLaunch(7, 0, ShipmentChannel.Requisition,
            OriginPortId: 1, DestPortId: 2, RouteHexes: route,
            RiderContractId: 12, RiderPriority: CourierPriority.War,
            Qty: new double[] { 0, 5 });

        var flow = RecentFlowQuery.Capture(launch);

        Assert.Equal(7, flow.ShipmentId);
        Assert.Equal(FreightPurpose.WarConvoy, flow.Purpose);
        Assert.Equal(1, flow.OriginPortId);
        Assert.Equal(2, flow.DestPortId);
        Assert.Same(route, flow.RouteHexes);
        Assert.Equal(5.0, flow.Qty[1], 6);
    }

    [Fact]
    public void OnlyCouriersAndWarConvoys_Render()
    {
        Assert.True(RecentFlowQuery.Renders(FreightPurpose.Courier));
        Assert.True(RecentFlowQuery.Renders(FreightPurpose.WarConvoy));
        Assert.False(RecentFlowQuery.Renders(FreightPurpose.SpreadRun));
        Assert.False(RecentFlowQuery.Renders(FreightPurpose.StateHaul));
    }

    private static readonly HexCoordinate H0 = new(0, 0);
    private static readonly HexCoordinate H1 = new(10, 0);
    private static readonly HexCoordinate H2 = new(20, 0);
    private static readonly HexCoordinate H3 = new(0, 10);

    private static RecentFlow Flow(int id, FreightPurpose purpose,
        params HexCoordinate[] route) =>
        new(id, 0, purpose, 0, 1, route, new double[] { 3 });

    [Fact]
    public void Trails_FilterToTheRenderingPurposes()
    {
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier, H0, H1),
            Flow(2, FreightPurpose.StateHaul, H0, H1), // captured, not drawn
            Flow(3, FreightPurpose.SpreadRun, H0, H1),
        };

        var trails = RecentFlowQuery.Trails(flows);

        var mark = Assert.Single(trails);
        Assert.Equal(FreightPurpose.Courier, mark.Purpose);
        Assert.Equal(H0, mark.From);
        Assert.Equal(H1, mark.To);
        Assert.Equal(1, mark.Flows);
        Assert.Equal(RecentFlowQuery.TrailAlphaFloor, mark.Color.A);
    }

    [Fact]
    public void Trails_LaneRoutedFlow_RendersPerLeg_NeverTheStraightResult()
    {
        // eyeball fix: a lane-routed flow draws the SAILED route — one
        // stroke per leg (A→B, B→C), never a direct A→C line across hexes
        // no lane connects
        var flows = new[] { Flow(1, FreightPurpose.Courier, H0, H1, H2) };

        var trails = RecentFlowQuery.Trails(flows);

        Assert.Equal(2, trails.Count);
        Assert.Equal(H0, trails[0].From);
        Assert.Equal(H1, trails[0].To);
        Assert.Equal(H1, trails[1].From);
        Assert.Equal(H2, trails[1].To);
    }

    [Fact]
    public void Trails_OffLaneFlow_KeepsTheDirectLine()
    {
        // the honest special case: an off-lane crawl really sails the
        // straight line, so its captured route is the endpoint pair
        var flows = new[] { Flow(1, FreightPurpose.WarConvoy, H0, H2) };

        var trails = RecentFlowQuery.Trails(flows);

        var mark = Assert.Single(trails);
        Assert.Equal(H0, mark.From);
        Assert.Equal(H2, mark.To);
        Assert.Equal(WorksLens.FreightWarConvoy.R, mark.Color.R);
    }

    [Fact]
    public void Trails_ASharedLeg_StacksIntensityAcrossFlows()
    {
        // two flows converge onto the same middle leg (H1→H2): the shared
        // leg aggregates both — network utilization, per leg, not per
        // origin/dest pair
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier, H0, H1, H2),
            Flow(2, FreightPurpose.Courier, H3, H1, H2),
        };

        var trails = RecentFlowQuery.Trails(flows);

        Assert.Equal(3, trails.Count);              // first-seen leg order
        Assert.Equal((H0, H1, 1), (trails[0].From, trails[0].To, trails[0].Flows));
        Assert.Equal((H1, H2, 2), (trails[1].From, trails[1].To, trails[1].Flows));
        Assert.Equal((H3, H1, 1), (trails[2].From, trails[2].To, trails[2].Flows));
        Assert.Equal(RecentFlowQuery.TrailAlphaFloor
            + RecentFlowQuery.TrailAlphaPerExtraFlow, trails[1].Color.A);
    }

    [Fact]
    public void Trails_OpposedDirections_ShareOneCorridorLeg()
    {
        // the same lane sailed both ways is one corridor on the map —
        // intensity counts every flow crossing it, drawn at first-seen
        // orientation
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier, H0, H1),
            Flow(2, FreightPurpose.Courier, H1, H0),
        };

        var trails = RecentFlowQuery.Trails(flows);

        var mark = Assert.Single(trails);
        Assert.Equal(H0, mark.From);
        Assert.Equal(H1, mark.To);
        Assert.Equal(2, mark.Flows);
    }

    [Fact]
    public void Trails_ShipmentStillInFlight_YieldsNoTrail()
    {
        // Eyeball 4 finding: a shipment still in state.Shipments at the
        // queried moment is the live crawl's job to draw — trailing it
        // too double-draws the same origin→dest line
        var flows = new[] { Flow(1, FreightPurpose.Courier, H0, H1) };
        var inFlight = new[]
        {
            new Shipment(1, 0, ShipmentChannel.Freight, 0, 1, 0,
                Array.Empty<int>(), new double[] { 5 }),
        };

        var trails = RecentFlowQuery.Trails(flows, inFlight);

        Assert.Empty(trails);
    }

    [Fact]
    public void Trails_SameFlow_ShipmentDelivered_YieldsItsTrail()
    {
        // the same flow once its shipment is no longer in the registry
        // (delivered within the step, the common courier case) still
        // trails — only STILL in flight is suppressed
        var flows = new[] { Flow(1, FreightPurpose.Courier, H0, H1) };

        var trails = RecentFlowQuery.Trails(flows,
            inFlightShipments: Array.Empty<Shipment>());

        var mark = Assert.Single(trails);
        Assert.Equal(H0, mark.From);
        Assert.Equal(H1, mark.To);
    }

    [Fact]
    public void Trails_InFlightFilter_OnlySuppressesItsOwnShipment()
    {
        // one flow's shipment is still in flight, a second flow's is not —
        // only the matching one is suppressed
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier, H0, H1),
            Flow(2, FreightPurpose.Courier, H1, H2),
        };
        var inFlight = new[]
        {
            new Shipment(1, 0, ShipmentChannel.Freight, 0, 1, 0,
                Array.Empty<int>(), new double[] { 5 }),
        };

        var trails = RecentFlowQuery.Trails(flows, inFlight);

        var mark = Assert.Single(trails);
        Assert.Equal(H1, mark.From);
        Assert.Equal(H2, mark.To);
    }

    [Fact]
    public void Trails_AlphaRisesToTheCap()
    {
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier, H0, H1),
            Flow(2, FreightPurpose.Courier, H0, H1),
            Flow(3, FreightPurpose.Courier, H0, H1),
            Flow(4, FreightPurpose.Courier, H0, H1),
            Flow(5, FreightPurpose.Courier, H0, H1), // floor + 4·per > cap
            Flow(6, FreightPurpose.WarConvoy, H0, H1), // own purpose mark
        };

        var trails = RecentFlowQuery.Trails(flows);

        Assert.Equal(2, trails.Count);
        Assert.Equal(5, trails[0].Flows);
        Assert.Equal(RecentFlowQuery.TrailAlphaCap, trails[0].Color.A);
        Assert.Equal(FreightPurpose.WarConvoy, trails[1].Purpose);
        Assert.Equal(1, trails[1].Flows);
    }

    // ---- TimeMachine: flows live in-memory beside each keyframe ----

    private static string BaseText()
    {
        var state = EpochTestKit.Seeded().State;
        state.Config.Sim.EpochCount = 10;
        new EpochEngine().Run(state);
        return ArtifactSerializer.ToText(state);
    }

    [Fact]
    public void TheBaseKeyframe_HasNoFlows_NoStepPrecededIt()
    {
        var machine = new TimeMachine(BaseText());

        Assert.Empty(machine.Keyframes[0].RecentFlows);
        Assert.Empty(machine.CurrentFlows);
    }

    [Fact]
    public void Scrub_RecallsEachKeyframesOwnFlows()
    {
        var machine = new TimeMachine(BaseText());
        machine.Step(3);
        var perFrame = new List<IReadOnlyList<RecentFlow>>();
        for (int i = 0; i < machine.Keyframes.Count; i++)
            perFrame.Add(machine.Keyframes[i].RecentFlows);

        machine.ScrubTo(1);
        Assert.Same(perFrame[1], machine.CurrentFlows);
        machine.ScrubTo(0);
        Assert.Empty(machine.CurrentFlows);
        machine.ScrubTo(3);
        Assert.Same(perFrame[3], machine.CurrentFlows);
    }

    [Fact]
    public void SteppingOverRecordedFrames_RecallsNotRecaptures()
    {
        // scrub back then step forward: the recorded keyframes replay —
        // their flows come back exactly as captured, never re-observed
        var machine = new TimeMachine(BaseText());
        machine.Step(3);
        var recorded = machine.Keyframes[2].RecentFlows;

        machine.ScrubTo(0);
        machine.Step(2);

        Assert.Equal(2, machine.Position);
        Assert.Same(recorded, machine.CurrentFlows);
    }

    [Fact]
    public void SteppingTheSeededWorld_CapturesLaunches()
    {
        // the Eyeball 2 finding made mechanical: the boundary registry is
        // mostly empty while launches DO happen inside steps — the
        // keyframes' captured flows are the record of them
        var machine = new TimeMachine(BaseText());
        machine.Step(5);

        int captured = 0;
        foreach (var frame in machine.Keyframes)
            captured += frame.RecentFlows.Count;
        Assert.True(captured > 0,
            "no launches captured over 5 epochs of the seeded world");
    }
}
