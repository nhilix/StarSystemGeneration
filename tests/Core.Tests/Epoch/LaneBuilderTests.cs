using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneBuilderTests
{
    private static SimState ReferenceRun()
    {
        var gc = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 12 };
        var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                                      new EpochSimConfig { MasterSeed = 42 });
        new EpochEngine().Run(state);
        return state;
    }

    /// <summary>The seed-42 default run must not produce all-pairs webs:
    /// mean lane degree over any polity's ports stays well under
    /// (ports−1) — the topology assertion from the spec (§7).</summary>
    [Fact]
    public void DefaultHistory_BuildsTreesAndHubs_NotAllPairsWebs()
    {
        var state = ReferenceRun();
        foreach (var pr in state.Polities)
        {
            var ports = new List<Port>();
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ports.Add(p);
            if (ports.Count < 4) continue;      // webs need bodies
            int degreeSum = 0;
            foreach (var lane in state.Lanes)
                foreach (var p in ports)
                    if (lane.PortAId == p.Id || lane.PortBId == p.Id)
                        degreeSum++;
            double meanDegree = (double)degreeSum / ports.Count;
            // all-pairs would be ports−1; a healthy network sits near 2
            Assert.True(meanDegree <= 0.6 * (ports.Count - 1) || meanDegree <= 3.0,
                $"polity {pr.ActorId}: mean lane degree {meanDegree:0.00} "
                + $"across {ports.Count} ports smells like a web");
        }
    }

    [Fact]
    public void EveryBuiltLane_HasBothGates_WithinSlotBudgets()
    {
        var state = ReferenceRun();
        Assert.NotEmpty(state.Lanes);
        foreach (var lane in state.Lanes)
        {
            Assert.True(lane.GateAId >= 0 && lane.GateBId >= 0);
            Assert.Equal((int)InfraTypeId.Gate,
                         state.Facilities[lane.GateAId].TypeId);
            Assert.Equal((int)InfraTypeId.Gate,
                         state.Facilities[lane.GateBId].TypeId);
        }
        foreach (var port in state.Ports)
            Assert.True(LaneNetwork.GateCount(state, port) <= port.Tier
                * state.Config.Infrastructure.GateSlotsPerPortTier,
                $"port {port.Id} over its gate-slot budget");
    }
}
