using Xunit;
using StarGen.Core.Epoch;

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
        { Body = new BodyRef(0, 1) };
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
        var near = new PopulationSegment(0, port.Id, 0, 0, 1.0) { Body = new BodyRef(0, 0) };
        var far  = new PopulationSegment(1, port.Id, 0, 0, 1.0) { Body = new BodyRef(0, 3) };

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
}
