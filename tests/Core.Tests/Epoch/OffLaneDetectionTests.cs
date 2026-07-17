using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Off-lane smuggling detection (locality slice L2 §5): a freight
/// run that leaves the lane network (empty RouteLaneIds) risks a SEIZURE by a
/// covering hostile Patrol. One detection roll on channel 78, keyed like
/// piracy/interdiction; on a hit the cargo is landed at the detecting patrol
/// owner's nearest own port as its asks — a conserved transfer (P4), never a
/// mint or a sink. This is the sole new nondeterminism in the slice.</summary>
public class OffLaneDetectionTests
{
    [Fact]
    public void DetectionChannel_Exists()
    {
        Assert.Equal(78UL, (ulong)StarGen.Core.Rng.RollChannel.ShipmentDetection);
    }

    /// <summary>Owner (actor 0) with two ports NOT lane-linked (→ off-lane
    /// crawl) and a RIVAL (actor 1) Patrol docked at the drop point with a
    /// port to land a prize.</summary>
    private static (SimState State, Port Home, Port Dest, Port Prize)
        OffLaneUnderPatrol()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;

        var home = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var dest = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 6, a0.Seat.R), tier: 2,
            foundedYear: 0);
        var prize = new Port(2, a1.Id,
            new HexCoordinate(a0.Seat.Q + 6, a0.Seat.R + 3), tier: 2,
            foundedYear: 0);
        state.Ports.Add(home);
        state.Ports.Add(dest);
        state.Ports.Add(prize);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        state.Markets.Add(new Market(2, state.Config.Economy));
        // NO AddLane between home and dest → PlanRoute returns the off-lane
        // crawl (empty RouteLaneIds).

        // a rival patrol on station at the drop point
        var patrol = new FleetRecord(state.Fleets.Count, a1.Id, dest.Hex)
        {
            Posture = FleetPosture.Patrol,
            Body = BodyRef.None,
        };
        state.Fleets.Add(patrol);
        return (state, home, dest, prize);
    }

    /// <summary>An ACTIVE war between the shipment owner and the patrol
    /// owner — detection is hostile-only (§5), so nothing is seized in
    /// peacetime.</summary>
    private static void StageWar(SimState state, int attacker, int defender)
        => state.Wars.Add(new War(state.Wars.Count, "the Smuggling War",
            attacker, defender, CasusBelli.BorderIncident, -1,
            WarDemand.CedeObjectives, state.WorldYear));

    [Fact]
    public void OffLaneRun_UnderFullPatrolCover_IsSeized_ToThePatrolsPort()
    {
        var (state, home, dest, prize) = OffLaneUnderPatrol();
        StageWar(state, prize.OwnerActorId, home.OwnerActorId);
        state.Config.War.OffLaneDetectionPerCoveredYear = 1.0;  // certain
        state.Config.War.PatrolCoverageFalloff = 0.0;           // full cover

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, home.OwnerActorId,
            ShipmentChannel.Freight, home.Id, dest.Id, basket);

        // seized inside the sail — the crawl never resolves to InTransit
        Assert.Null(s);
        // the prize landed on the rival's port book (conserved transfer)
        Assert.Equal(100.0, BookOps.AskQty(state, prize.Id,
            (int)GoodId.Provisions), 6);
        // and it never reached the destination
        Assert.Equal(0.0, BookOps.AskQty(state, dest.Id,
            (int)GoodId.Provisions), 6);
        Assert.Contains(state.Staged,
            e => e.Type == WorldEventType.CargoSeized);
    }

    [Fact]
    public void OffLaneRun_ArrivesUnmolested_WhenNoHostilePatrolCovers()
    {
        var (state, home, dest, prize) = OffLaneUnderPatrol();
        // a real war IS on, so the only thing saving this run is ownership —
        // without the self-exclusion the cargo would be seized
        StageWar(state, prize.OwnerActorId, home.OwnerActorId);
        state.Config.War.OffLaneDetectionPerCoveredYear = 1.0;
        state.Config.War.PatrolCoverageFalloff = 0.0;
        // the only patrol belongs to the SHIPMENT OWNER — it never covers
        // against itself, so there is nothing to evade
        state.Fleets[0].OwnerActorId = home.OwnerActorId;

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, home.OwnerActorId,
            ShipmentChannel.Freight, home.Id, dest.Id, basket);

        // delivered (the 6-hex crawl finishes inside the 25-year span)
        Assert.Null(s);
        Assert.Equal(100.0, BookOps.AskQty(state, dest.Id,
            (int)GoodId.Provisions), 6);
        Assert.Equal(0.0, BookOps.AskQty(state, prize.Id,
            (int)GoodId.Provisions), 6);
        Assert.DoesNotContain(state.Staged,
            e => e.Type == WorldEventType.CargoSeized);
    }

    /// <summary>The hostile-only contract (§5, markets.md:97): detection
    /// gates on an ACTIVE WAR, so a FOREIGN patrol at peace with the shipment
    /// owner — an ally, a federation partner, a neutral — seizes NOTHING even
    /// sitting on the drop point with certain detection and full cover. Every
    /// polity stations escorts at its capital, so without this a frontier
    /// colony's founding-era supply runs would be looted by its friends.</summary>
    [Fact]
    public void PeacetimeForeignPatrol_SeizesNothing_CargoDelivers()
    {
        var (state, home, dest, prize) = OffLaneUnderPatrol();
        state.Config.War.OffLaneDetectionPerCoveredYear = 1.0;  // certain
        state.Config.War.PatrolCoverageFalloff = 0.0;           // full cover
        // NO war staged: the rival patrol is merely foreign, not hostile.
        Assert.NotEqual(home.OwnerActorId, state.Fleets[0].OwnerActorId);

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, home.OwnerActorId,
            ShipmentChannel.Freight, home.Id, dest.Id, basket);

        // delivered untouched — nothing landed on the foreigner's book
        Assert.Null(s);
        Assert.Equal(100.0, BookOps.AskQty(state, dest.Id,
            (int)GoodId.Provisions), 6);
        Assert.Equal(0.0, BookOps.AskQty(state, prize.Id,
            (int)GoodId.Provisions), 6);
        Assert.DoesNotContain(state.Staged,
            e => e.Type == WorldEventType.CargoSeized);
    }

    [Fact]
    public void PortlessPatrol_TakesNothing_CargoRunsThrough()
    {
        var (state, home, dest, prize) = OffLaneUnderPatrol();
        // a real war IS on — only the missing prize port stops the seizure
        StageWar(state, prize.OwnerActorId, home.OwnerActorId);
        state.Config.War.OffLaneDetectionPerCoveredYear = 1.0;
        state.Config.War.PatrolCoverageFalloff = 0.0;
        // strip the patrol owner's only port: it has nowhere to land a prize
        state.Ports.RemoveAt(2);
        state.Markets.RemoveAt(2);

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, home.OwnerActorId,
            ShipmentChannel.Freight, home.Id, dest.Id, basket);

        // no prize port → no seizure → the cargo delivers to the destination
        Assert.Null(s);
        Assert.Equal(100.0, BookOps.AskQty(state, dest.Id,
            (int)GoodId.Provisions), 6);
        Assert.DoesNotContain(state.Staged,
            e => e.Type == WorldEventType.CargoSeized);
    }
}
