using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Patrol enforcement coverage as a spatial field (locality slice
/// §2): a Patrol fleet's reach weakens with hex-hop + local-hop distance
/// from its dock, instead of a flat domain-wide multiplier. No caller yet
/// (Task 6 wires the detection roll to this) — pure read model only.</summary>
public class PatrolCoverageTests
{
    [Fact]
    public void NoPatrol_IsZeroCoverage()
    {
        var (_, state) = EpochTestKit.Seeded();
        // EpochGenesis.Seed never populates state.Fleets (fleets are only
        // created by world-time ops: FleetOps/CorporationOps/Phases), so a
        // freshly-seeded state genuinely has zero fleets — this assertion
        // would fail loudly (not vacuously) if that ever changed.
        Assert.Empty(state.Fleets);
        Assert.Equal(0.0, PatrolCoverage.At(state, state.Actors[0].Seat,
            BodyRef.None, ownerActorId: 1), 9);
    }

    /// <summary>An ACTIVE war between two polities — coverage is hostile-only
    /// (§5), so a patrol projects nothing without one.</summary>
    private static void StageWar(SimState state, int attacker, int defender)
        => state.Wars.Add(new War(state.Wars.Count, "the Coverage War",
            attacker, defender, CasusBelli.BorderIncident, -1,
            WarDemand.CedeObjectives, state.WorldYear));

    [Fact]
    public void CoverageFallsOffWithDistanceFromTheDock()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = new HexCoordinate(0, 0);
        var enemy = 2;
        StageWar(state, attacker: enemy, defender: 1);
        var patrol = new FleetRecord(state.Fleets.Count, ownerActorId: enemy, hex)
        { Posture = FleetPosture.Patrol, Body = BodyRef.None };
        state.Fleets.Add(patrol);
        double atDock = PatrolCoverage.At(state, hex, BodyRef.None, ownerActorId: 1);
        double far = PatrolCoverage.At(state, new HexCoordinate(5, 0),
            BodyRef.None, ownerActorId: 1);
        Assert.True(atDock > far);
        Assert.Equal(0.0, PatrolCoverage.At(state, hex, BodyRef.None,
            ownerActorId: enemy), 9);      // own patrol never "covers" against self
    }

    /// <summary>The hostile-only contract (§5): a FOREIGN patrol not at war
    /// with the owner projects nothing — a peacetime run past an allied or
    /// neutral capital is safe. Without the war gate this returns full
    /// coverage, so the assertion is load-bearing.</summary>
    [Fact]
    public void PeacetimeForeignPatrol_ProjectsNoCoverage()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = new HexCoordinate(0, 0);
        state.Fleets.Add(new FleetRecord(state.Fleets.Count, ownerActorId: 2, hex)
        { Posture = FleetPosture.Patrol, Body = BodyRef.None });
        // no war staged → nothing to evade, even docked on the very hex
        Assert.Equal(0.0, PatrolCoverage.At(state, hex, BodyRef.None,
            ownerActorId: 1), 9);
    }
}
