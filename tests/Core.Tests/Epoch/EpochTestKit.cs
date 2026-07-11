using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Shared seeding helper: the real genesis pipeline over a small
/// galaxy — natural raster passes, then polity seeding from homeworld anchors.</summary>
public static class EpochTestKit
{
    public static (GalaxySkeleton Skeleton, SimState State) Seeded(
        ulong seed = 42, int radiusCells = 8)
    {
        var gc = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radiusCells };
        var skeleton = SkeletonBuilder.Build(gc);
        var state = EpochGenesis.Seed(skeleton, new EpochSimConfig { MasterSeed = seed });
        return (skeleton, state);
    }

    /// <summary>Build a linked gate pair and its lane directly — the
    /// registry state the Allocation builder would produce, minus the
    /// treasury/goods flow (unit tests aren't economies).</summary>
    public static Lane AddLane(SimState state, int portAId, int portBId,
                               int gateTier = 2, int ownerActorId = -1)
    {
        if (portAId > portBId) (portAId, portBId) = (portBId, portAId);
        var a = state.Ports[portAId];
        var b = state.Ports[portBId];
        var gateA = new Facility(state.Facilities.Count,
            (int)StarGen.Core.Substrate.InfraTypeId.Gate, gateTier, a.Hex,
            ownerActorId >= 0 ? ownerActorId : a.OwnerActorId, state.WorldYear);
        state.Facilities.Add(gateA);
        var gateB = new Facility(state.Facilities.Count,
            (int)StarGen.Core.Substrate.InfraTypeId.Gate, gateTier, b.Hex,
            ownerActorId >= 0 ? ownerActorId : b.OwnerActorId, state.WorldYear);
        state.Facilities.Add(gateB);
        var lane = new Lane(state.Lanes.Count, portAId, portBId, state.WorldYear)
        { GateAId = gateA.Id, GateBId = gateB.Id };
        state.Lanes.Add(lane);
        return lane;
    }

    /// <summary>Post a freight fleet on a lane (slice E: freight only moves
    /// on posted hulls) — registers a hauler design for the owner if none
    /// exists. Returns the posted fleet.</summary>
    public static FleetRecord PostFreight(SimState state, int actorId,
                                          int laneId, int hulls)
    {
        var design = DesignRegistry.Current(state, actorId,
                ShipRole.Freight, ShipSize.Medium)
            ?? DesignRegistry.Register(state, actorId,
                ShipRole.Freight, ShipSize.Medium, grade: 0.5);
        var lane = state.Lanes[laneId];
        var home = state.Ports[lane.PortAId];
        var fleet = new FleetRecord(state.Fleets.Count, actorId, home.Hex)
        {
            Posture = FleetPosture.Posted,
            TargetId = laneId,
            HomePortId = home.Id,
        };
        fleet.AddHulls(design.Id, hulls, 0.5);
        state.Fleets.Add(fleet);
        state.PolityOf(actorId).HullsBuilt += hulls;
        return fleet;
    }

    /// <summary>The first relation whose parties are both still on the
    /// stage and not at war with each other — the crafting anchor most
    /// slice-H tests ride (federations retire actors mid-history, so
    /// Relations[0] can be a dead pair).</summary>
    public static PolityRelation FirstLiveRelation(SimState state)
    {
        foreach (var rel in state.Relations)
            if (RelationsOps.BothLive(state, rel)
                && WarOps.ActiveWarBetween(state, rel.PolityAId,
                                           rel.PolityBId) == null
                // unbonded parties: vassalage locks diplomacy and wars
                && rel.VassalPolityId < 0
                && FederationOps.OverlordOf(state, rel.PolityAId) < 0
                && FederationOps.OverlordOf(state, rel.PolityBId) < 0)
                return rel;
        throw new System.InvalidOperationException(
            "no live unbonded at-peace relation in this history");
    }

    /// <summary>Station a blockade squadron at a port's approaches — the
    /// real interdiction that replaced the debug lane-cut hook (slice H):
    /// every lane touching the port severs via FleetOps.SeveredLaneIds.</summary>
    public static FleetRecord BlockadePort(SimState state, int actorId,
                                           int portId, int hulls = 2)
    {
        var design = DesignRegistry.Current(state, actorId,
                ShipRole.Escort, ShipSize.Light)
            ?? DesignRegistry.Register(state, actorId,
                ShipRole.Escort, ShipSize.Light, grade: 0.5);
        var fleet = new FleetRecord(state.Fleets.Count, actorId,
                                    state.Ports[portId].Hex)
        {
            Posture = FleetPosture.Blockade,
            TargetId = portId,
        };
        fleet.AddHulls(design.Id, hulls, 0.5);
        state.Fleets.Add(fleet);
        state.PolityOf(actorId).HullsBuilt += hulls;
        return fleet;
    }
}
