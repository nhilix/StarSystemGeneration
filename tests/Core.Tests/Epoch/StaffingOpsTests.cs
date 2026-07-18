using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Distance-weighted facility staffing (locality slice §3): a
/// segment on the facility's own body weights 1; segments farther by hex-hop
/// or local-hop weight less, so remote sites produce less. Production
/// magnitude only — no money flow (ConservationTests owns that boundary).</summary>
public class StaffingOpsTests
{
    [Fact]
    public void SegmentOnTheFacilitysBody_WeightsOne()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        var f = new Facility(0, (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1,
            a0.Seat, a0.Id, 0) { Body = new BodyRef(0, 1) };
        var seg = new PopulationSegment(0, port.Id, 0, 0, 3.0)
        { Hex = a0.Seat, Body = new BodyRef(0, 1) };
        state.Segments.Add(seg);
        Assert.Equal(1.0, StaffingOps.ProximityWeight(state, f, seg), 9);
    }

    [Fact]
    public void FartherBody_WeightsLess()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        var system = SystemRegistry.Commit(state, a0.Seat);   // freeze the hex's system
        var f = new Facility(0, (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1,
            a0.Seat, a0.Id, 0) { Body = new BodyRef(0, 0) };
        var near = new PopulationSegment(0, port.Id, 0, 0, 1.0)
        { Hex = a0.Seat, Body = new BodyRef(0, 0) };
        var far  = new PopulationSegment(1, port.Id, 0, 0, 1.0)
        { Hex = a0.Seat, Body = new BodyRef(0, 3) };

        // Load-bearing guard: the seat system (seed 42) really places slots 0
        // and 3 at different orbits, so the far segment's local-hop is > 0 and
        // the inequality below is not a vacuous 1.0 > 1.0.
        int cross = (int)state.Config.Economy.CrossStarHopOrbitSteps;
        Assert.True(OrbitGeometry.OrbitDistance(system!, f.Body, far.Body, cross) > 0);

        double wNear = StaffingOps.ProximityWeight(state, f, near);
        double wFar = StaffingOps.ProximityWeight(state, f, far);
        Assert.Equal(1.0, wNear, 9);              // same body → weight 1
        Assert.True(wFar < 1.0);                    // distant → strictly less
        Assert.True(wNear > wFar);
    }

    [Fact]
    public void ResidentSegment_OutweighsDistantPortHousehold()
    {
        // Task 2.4: hexHop measures segment-hex -> facility-hex, not
        // port-hex -> facility-hex, so a resident of the satellite hex
        // crews it at full weight while the port's distant households
        // crew it weakly, even though both segments share the same port.
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var facilityHex = new HexCoordinate(portHex.Q + 3, portHex.R);
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        var f = new Facility(0, (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1,
            facilityHex, a0.Id, 0);

        // resident: settled hex equals the facility's hex
        var resident = new PopulationSegment(0, port.Id, 0, 0, 1.0)
        { Hex = facilityHex };
        // port household: never relocated, settled hex still the port hex,
        // several hexes from the facility
        var portHousehold = new PopulationSegment(1, port.Id, 0, 0, 1.0)
        { Hex = portHex };

        double wResident = StaffingOps.ProximityWeight(state, f, resident);
        double wPortHousehold = StaffingOps.ProximityWeight(state, f, portHousehold);
        Assert.True(wResident > wPortHousehold);
    }

    [Fact]
    public void SegmentAtPortHex_MatchesOldPortHexBehavior()
    {
        // No-op-for-current-state property: every segment's Hex currently
        // defaults to its administering port's hex (Task 2.1), so this
        // rewire must reproduce exactly what the old port-hex -> facility-hex
        // measurement would have given: 1 / (1 + falloff * hexHop).
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var facilityHex = new HexCoordinate(portHex.Q + 2, portHex.R);
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        var f = new Facility(0, (int)StarGen.Core.Substrate.InfraTypeId.Mine, 1,
            facilityHex, a0.Id, 0);
        var seg = new PopulationSegment(0, port.Id, 0, 0, 1.0) { Hex = portHex };

        int hexHop = HexGrid.Distance(portHex, facilityHex);
        double expected = 1.0 / (1.0 + state.Config.Economy.StaffingDistanceFalloff * hexHop);
        Assert.Equal(expected, StaffingOps.ProximityWeight(state, f, seg), 9);
    }
}
