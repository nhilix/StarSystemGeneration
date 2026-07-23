using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>AC2.F2 — the PASSIVE shipment-launch observer: a null observer
/// (the default, every serializer load) means strictly zero side effects;
/// a set observer sees every launch — including the sub-step deliveries
/// that never reach SimState.Shipments and are invisible at every epoch
/// boundary (the Eyeball 2 finding this seam exists to fix).</summary>
public class ShipmentObserverTests
{
    /// <summary>ShipmentTests' fixture: one polity, two tier-2 ports 10
    /// hexes apart, a live tier-2 lane between them.</summary>
    private static (SimState State, Port A, Port B) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);          // tier-2 gates, live
        state.WorldYear = 100;
        return (state, pa, pb);
    }

    [Fact]
    public void Dispatch_WithAnObserver_SeesTheLaunch()
    {
        var (state, pa, pb) = Fixture();
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        int g = (int)GoodId.Alloys;
        var seen = new List<ShipmentLaunch>();
        state.ShipmentObserver = l => seen.Add(l);

        var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, 25.0, 0.7) });

        Assert.NotNull(s);                          // multi-step: survives
        var launch = Assert.Single(seen);
        Assert.Equal(s!.Id, launch.ShipmentId);
        Assert.Equal(pa.OwnerActorId, launch.OwnerActorId);
        Assert.Equal(ShipmentChannel.Requisition, launch.Channel);
        Assert.Equal(pa.Id, launch.OriginPortId);
        Assert.Equal(pb.Id, launch.DestPortId);
        Assert.Equal(-1, launch.RiderContractId);   // no courier rides it
        Assert.Equal(25.0, launch.Qty[g], 6);
    }

    [Fact]
    public void SubStepDelivery_IsStillSeen_ThatIsThePoint()
    {
        // default speed: 0.625y transit inside a 25y step — Dispatch
        // returns null, the registry stays empty, and the epoch boundary
        // would never show this cargo. The observer is the only witness.
        var (state, pa, pb) = Fixture();
        int g = (int)GoodId.Alloys;
        var seen = new List<ShipmentLaunch>();
        state.ShipmentObserver = l => seen.Add(l);

        var s = ShipmentOps.Dispatch(state, pa.OwnerActorId,
            ShipmentChannel.Requisition, pa.Id, pb.Id,
            new[] { (g, 10.0, 0.5) });

        Assert.Null(s);
        Assert.Empty(state.Shipments);
        var launch = Assert.Single(seen);
        Assert.Equal(pa.Id, launch.OriginPortId);
        Assert.Equal(pb.Id, launch.DestPortId);
        Assert.Equal(10.0, launch.Qty[g], 6);
    }

    [Fact]
    public void CourierAccept_CarriesTheRiderOntoTheLaunch()
    {
        // the capture-time rider: at Accept the contract is still Open and
        // a sub-step transit resolves it immediately — the registry lookup
        // (CourierOps.OfShipment) can never derive this purpose, so the
        // launch carries the rider explicitly
        var (state, pa, pb) = Fixture();
        int g = (int)GoodId.Provisions;
        pa.DepositStock(g, 40, 0.6);
        var c = CourierOps.Post(state, pa.OwnerActorId, pa.Id, pb.Id,
            new[] { (g, 20.0) }, fee: 5.0, CourierPriority.War);
        Assert.NotNull(c);
        var seen = new List<ShipmentLaunch>();
        state.ShipmentObserver = l => seen.Add(l);

        Assert.True(CourierOps.Accept(state, c!, pa.OwnerActorId));

        var launch = Assert.Single(seen);
        Assert.Equal(c!.Id, launch.RiderContractId);
        Assert.Equal(CourierPriority.War, launch.RiderPriority);
        Assert.Equal(ShipmentChannel.Requisition, launch.Channel);
        Assert.Equal(20.0, launch.Qty[g], 6);
    }

    [Fact]
    public void EngineStep_ThreadsTheObserverForTheStep_AndResetsIt()
    {
        var state = EpochTestKit.Seeded().State;
        var engine = new EpochEngine
        { ShipmentObserver = _ => { } };

        engine.Step(state);

        // reset-safe: the tap never outlives the step it was set for
        Assert.Null(state.ShipmentObserver);
    }

    [Fact]
    public void TheObserver_IsPassive_TheSteppedWorldIsByteIdentical()
    {
        // the hard invariant: observer set vs observer null must produce
        // bit-identical worlds — no rolls, no allocation, no order change
        var plain = EpochTestKit.Seeded().State;
        var tapped = EpochTestKit.Seeded().State;
        var plainEngine = new EpochEngine();
        var tappedEngine = new EpochEngine
        { ShipmentObserver = _ => { } };

        for (int i = 0; i < 12; i++)
        {
            plainEngine.Step(plain);
            tappedEngine.Step(tapped);
        }

        Assert.Equal(ArtifactSerializer.ToText(plain),
                     ArtifactSerializer.ToText(tapped));
    }
}
