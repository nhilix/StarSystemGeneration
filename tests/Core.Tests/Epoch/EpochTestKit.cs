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
