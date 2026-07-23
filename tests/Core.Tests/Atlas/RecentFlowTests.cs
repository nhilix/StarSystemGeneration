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
        var launch = new ShipmentLaunch(7, 0, ShipmentChannel.Requisition,
            OriginPortId: 1, DestPortId: 2, RiderContractId: 12,
            RiderPriority: CourierPriority.War, Qty: new double[] { 0, 5 });

        var flow = RecentFlowQuery.Capture(launch);

        Assert.Equal(7, flow.ShipmentId);
        Assert.Equal(FreightPurpose.WarConvoy, flow.Purpose);
        Assert.Equal(1, flow.OriginPortId);
        Assert.Equal(2, flow.DestPortId);
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

    /// <summary>Two ports on a seeded world — enough for hex lookups.</summary>
    private static SimState PortedState()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        state.Ports.Add(new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0));
        return state;
    }

    private static RecentFlow Flow(int id, FreightPurpose purpose,
        int origin = 0, int dest = 1) =>
        new(id, 0, purpose, origin, dest, new double[] { 3 });

    [Fact]
    public void Trails_FilterToTheRenderingPurposes_AndResolvePortHexes()
    {
        var state = PortedState();
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier),
            Flow(2, FreightPurpose.StateHaul),      // captured, never drawn
            Flow(3, FreightPurpose.SpreadRun),
        };

        var trails = RecentFlowQuery.Trails(state, flows);

        var mark = Assert.Single(trails);
        Assert.Equal(FreightPurpose.Courier, mark.Purpose);
        Assert.Equal(state.Ports[0].Hex, mark.From);
        Assert.Equal(state.Ports[1].Hex, mark.To);
        Assert.Equal(1, mark.Flows);
        Assert.Equal(RecentFlowQuery.TrailAlphaFloor, mark.Color.A);
    }

    [Fact]
    public void Trails_AggregatePerCorridor_AlphaRisesToTheCap()
    {
        var state = PortedState();
        var flows = new[]
        {
            Flow(1, FreightPurpose.Courier),
            Flow(2, FreightPurpose.Courier),
            Flow(3, FreightPurpose.Courier),
            Flow(4, FreightPurpose.Courier),
            Flow(5, FreightPurpose.Courier),        // 5 → floor + 4·per > cap
            Flow(6, FreightPurpose.WarConvoy),      // own corridor mark
        };

        var trails = RecentFlowQuery.Trails(state, flows);

        Assert.Equal(2, trails.Count);              // first-seen order
        Assert.Equal(FreightPurpose.Courier, trails[0].Purpose);
        Assert.Equal(5, trails[0].Flows);
        Assert.Equal(RecentFlowQuery.TrailAlphaCap, trails[0].Color.A);
        Assert.Equal(FreightPurpose.WarConvoy, trails[1].Purpose);
        Assert.Equal(1, trails[1].Flows);
        // war convoy keeps the war-red identity, at trail alpha
        Assert.Equal(WorksLens.FreightWarConvoy.R, trails[1].Color.R);
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
